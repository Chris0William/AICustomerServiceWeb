using System.ComponentModel;
using System.Text;
using System.Diagnostics;
using Microsoft.SemanticKernel;
using MySql.Data.MySqlClient;
using AICustomerServiceWeb.Services;
using AICustomerServiceWeb.Models;

namespace AICustomerServiceWeb.Tools;

public class DatabaseTool
{
    private readonly string _connectionString;
    private readonly Kernel _kernel;
    private readonly RAGFlowService _ragflow;

    public DatabaseTool(
        string connectionString,
        Kernel kernel,
        RAGFlowService ragflow)
    {
        _connectionString = connectionString;
        _kernel = kernel;
        _ragflow = ragflow;
    }

    [KernelFunction]
    [Description(@"查询数据库获取实时数据。
适用场景：
- 统计查询（如：有多少用户？设备数量？）
- 列表查询（如：显示所有部门、查看用户列表）
- 筛选查询（如：查询某部门的用户、某时间段的任务）
- 聚合分析（如：按部门统计人数）

自动将自然语言转换为SQL并执行。")]
    public async Task<string> QueryDatabase(
        [Description("用户的自然语言问题")] string question)
    {
        Console.WriteLine($"[DatabaseTool] ========== QueryDatabase CALLED ==========");
        Console.WriteLine($"[DatabaseTool] Question: {question}");
        Console.WriteLine($"[DatabaseTool] Thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");

        var processLog = new StringBuilder();
        var sw = Stopwatch.StartNew();

        // 创建执行详情对象
        var executionDetails = new ExecutionDetails();

        try
        {
            processLog.AppendLine("🔍 **数据库查询执行过程**");
            processLog.AppendLine();

            processLog.AppendLine("**步骤1**: 从RAGFlow检索Q2SQL示例...");
            var (q2sqlContent, q2sqlDetails) = await _ragflow.RetrieveQ2SQLExamplesWithDetails(question, 8);
            var q2sqlExamples = q2sqlContent;
            var exampleCount = q2sqlDetails?.RetrievedItems?.Count ?? 0;
            processLog.AppendLine($"✅ 检索到 {exampleCount} 条相关Q2SQL示例");
            processLog.AppendLine();

            // 记录RAGFlow检索结果
            if (q2sqlDetails != null)
            {
                Console.WriteLine($"[DatabaseTool] Q2SQL Details - Retrieved: {q2sqlDetails.RetrievedItems?.Count ?? 0} items");
                if (q2sqlDetails.RetrievedItems != null && q2sqlDetails.RetrievedItems.Count > 0)
                {
                    Console.WriteLine($"[DatabaseTool] First Q2SQL item: {q2sqlDetails.RetrievedItems[0].Content.Substring(0, Math.Min(100, q2sqlDetails.RetrievedItems[0].Content.Length))}...");
                }

                executionDetails.RAGFlowSteps.Add(new RAGFlowStep
                {
                    StepNumber = 1,
                    StepName = "Q2SQL示例检索",
                    KnowledgeBaseId = q2sqlDetails.KnowledgeBaseId,
                    QueryText = q2sqlDetails.QueryText,
                    RetrievedCount = q2sqlDetails.RetrievedItems?.Count ?? 0,
                    RetrievedItems = q2sqlDetails.RetrievedItems?.Select(item => new Models.RetrievedItem
                    {
                        Content = item.Content,
                        Similarity = item.Similarity,
                        DocumentName = item.DocumentName
                    }).ToList() ?? new List<Models.RetrievedItem>(),
                    ExecutionTimeMs = q2sqlDetails.ExecutionTimeMs
                });
            }
            else
            {
                Console.WriteLine("[DatabaseTool] Q2SQL Details is null!");
            }

            processLog.AppendLine("**步骤2**: 检索相关表结构（DDL+字段描述）...");
            var (ddlContent, ddlDetails) = await _ragflow.RetrieveDDLWithDetails(question, 10);

            // 如果检索结果太少，记录警告
            if (ddlDetails?.RetrievedItems?.Count == 0)
            {
                Console.WriteLine($"[DatabaseTool] WARNING: No DDL found for question: {question}");
                Console.WriteLine("[DatabaseTool] This may cause LLM to generate incorrect table names!");
            }

            var ddl = ddlContent;
            var ddlCount = ddlDetails?.RetrievedItems?.Count ?? 0;
            processLog.AppendLine($"✅ 检索到 {ddlCount} 个相关表结构和字段描述");
            processLog.AppendLine();

            // 记录DDL检索结果
            if (ddlDetails != null)
            {
                Console.WriteLine($"[DatabaseTool] DDL Details - Retrieved: {ddlDetails.RetrievedItems?.Count ?? 0} items");
                if (ddlDetails.RetrievedItems != null && ddlDetails.RetrievedItems.Count > 0)
                {
                    Console.WriteLine($"[DatabaseTool] First DDL item: {ddlDetails.RetrievedItems[0].Content.Substring(0, Math.Min(100, ddlDetails.RetrievedItems[0].Content.Length))}...");
                }

                executionDetails.RAGFlowSteps.Add(new RAGFlowStep
                {
                    StepNumber = 2,
                    StepName = "DDL+描述检索",
                    KnowledgeBaseId = ddlDetails.KnowledgeBaseId,
                    QueryText = ddlDetails.QueryText,
                    RetrievedCount = ddlDetails.RetrievedItems?.Count ?? 0,
                    RetrievedItems = ddlDetails.RetrievedItems?.Select(item => new Models.RetrievedItem
                    {
                        Content = item.Content,
                        Similarity = item.Similarity,
                        DocumentName = item.DocumentName
                    }).ToList() ?? new List<Models.RetrievedItem>(),
                    ExecutionTimeMs = ddlDetails.ExecutionTimeMs
                });
            }
            else
            {
                Console.WriteLine("[DatabaseTool] DDL Details is null!");
            }

            processLog.AppendLine("**步骤3**: 检索业务规则文档...");
            var (businessRulesContent, businessRulesDetails) = await _ragflow.RetrieveBusinessRulesWithDetails(question, 5);
            var businessRules = businessRulesContent;
            var businessRulesCount = businessRulesDetails?.RetrievedItems?.Count ?? 0;
            processLog.AppendLine($"✅ 检索到 {businessRulesCount} 条相关业务规则");
            processLog.AppendLine();

            // 记录业务规则检索结果
            if (businessRulesDetails != null)
            {
                Console.WriteLine($"[DatabaseTool] Business Rules Details - Retrieved: {businessRulesDetails.RetrievedItems?.Count ?? 0} items");
                if (businessRulesDetails.RetrievedItems != null && businessRulesDetails.RetrievedItems.Count > 0)
                {
                    Console.WriteLine($"[DatabaseTool] First Business Rule item: {businessRulesDetails.RetrievedItems[0].Content.Substring(0, Math.Min(100, businessRulesDetails.RetrievedItems[0].Content.Length))}...");
                }

                executionDetails.RAGFlowSteps.Add(new RAGFlowStep
                {
                    StepNumber = 3,
                    StepName = "业务规则检索",
                    KnowledgeBaseId = businessRulesDetails.KnowledgeBaseId,
                    QueryText = businessRulesDetails.QueryText,
                    RetrievedCount = businessRulesDetails.RetrievedItems?.Count ?? 0,
                    RetrievedItems = businessRulesDetails.RetrievedItems?.Select(item => new Models.RetrievedItem
                    {
                        Content = item.Content,
                        Similarity = item.Similarity,
                        DocumentName = item.DocumentName
                    }).ToList() ?? new List<Models.RetrievedItem>(),
                    ExecutionTimeMs = businessRulesDetails.ExecutionTimeMs
                });
            }
            else
            {
                Console.WriteLine("[DatabaseTool] Business Rules Details is null!");
            }

            processLog.AppendLine("**步骤4**: 生成SQL语句...");
            var sqlGenerateStart = DateTime.Now;
            var sqlPrompt = BuildSQLPrompt(question, q2sqlExamples, ddl, businessRules);
            var sqlResponse = await _kernel.InvokePromptAsync(sqlPrompt);
            var sql = ExtractSQL(sqlResponse.ToString());
            var sqlGenerateTime = (int)(DateTime.Now - sqlGenerateStart).TotalMilliseconds;

            // 记录生成的SQL
            executionDetails.GeneratedSQL = sql;

            // 检查是否是ERROR
            if (sql.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
            {
                processLog.AppendLine("❌ LLM无法基于当前表结构生成SQL");
                processLog.AppendLine($"原因: {sql}");

                executionDetails.Status = "Failed";
                executionDetails.ErrorMessage = sql;
                executionDetails.TotalExecutionTime = (int)sw.ElapsedMilliseconds;

                AICustomerServiceWeb.Services.ExecutionContext.LastDatabaseExecution = processLog.ToString();
                AICustomerServiceWeb.Services.ExecutionContext.CurrentExecutionDetails = executionDetails;
                return "抱歉，当前数据库结构无法回答您的问题。可能的原因：\n1. 缺少相关的数据表或字段\n2. 字段命名不符合查询需求\n\n建议：\n- 请尝试更换问法或提供更具体的查询条件\n- 联系管理员检查数据库结构是否完整";
            }

            processLog.AppendLine("✅ SQL生成完成");
            processLog.AppendLine();
            processLog.AppendLine("```sql");
            processLog.AppendLine(sql);
            processLog.AppendLine("```");
            processLog.AppendLine();

            processLog.AppendLine("**步骤4**: 执行SQL查询...");

            string result;
            int maxRetries = 2;
            string? finalSql = sql;
            string? lastError = null;
            int retryCount = 0;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var execStart = DateTime.Now;
                    result = await ExecuteSQL(sql);
                    var execTime = (int)(DateTime.Now - execStart).TotalMilliseconds;
                    processLog.AppendLine("✅ 查询执行成功");
                    processLog.AppendLine();

                    // 记录执行成功
                    executionDetails.Status = "Success";
                    executionDetails.ResultRowCount = ExtractRowCount(result);
                    executionDetails.TotalExecutionTime = (int)sw.ElapsedMilliseconds;

                    // 保存执行详情到ExecutionContext
                    AICustomerServiceWeb.Services.ExecutionContext.LastDatabaseExecution = processLog.ToString();
                    AICustomerServiceWeb.Services.ExecutionContext.CurrentExecutionDetails = executionDetails;

                    Console.WriteLine($"[DatabaseTool] ExecutionDetails saved with {executionDetails.RAGFlowSteps.Count} RAGFlow steps");
                    Console.WriteLine($"[DatabaseTool] GeneratedSQL: {executionDetails.GeneratedSQL?.Length ?? 0} chars");
                    Console.WriteLine($"[DatabaseTool] ResultRowCount: {executionDetails.ResultRowCount}");

                    // 返回执行过程 + 查询结果
                    return processLog.ToString() + "\n" + result;
                }
                catch (MySqlException ex)
                {
                    processLog.AppendLine($"❌ 尝试 {attempt}/{maxRetries} 执行失败: {ex.Message}");
                    lastError = ex.Message;
                    retryCount = attempt - 1;

                    if (attempt < maxRetries)
                    {
                        processLog.AppendLine($"🔄 正在重新生成SQL...");
                        processLog.AppendLine();

                        var retryStart = DateTime.Now;
                        sqlPrompt = BuildSQLPrompt(question, q2sqlExamples, ddl, businessRules, ex.Message);
                        sqlResponse = await _kernel.InvokePromptAsync(sqlPrompt);
                        sql = ExtractSQL(sqlResponse.ToString());
                        var retryTime = (int)(DateTime.Now - retryStart).TotalMilliseconds;
                        finalSql = sql;

                        // 更新SQL
                        executionDetails.GeneratedSQL = sql;

                        processLog.AppendLine("**重新生成的SQL**:");
                        processLog.AppendLine("```sql");
                        processLog.AppendLine(sql);
                        processLog.AppendLine("```");
                        processLog.AppendLine();
                    }
                    else
                    {
                        processLog.AppendLine();
                        processLog.AppendLine($"❌ SQL执行失败（已重试{maxRetries}次）: {ex.Message}");

                        // 记录执行失败
                        executionDetails.Status = "Failed";
                        executionDetails.ErrorMessage = lastError;
                        executionDetails.TotalExecutionTime = (int)sw.ElapsedMilliseconds;

                        AICustomerServiceWeb.Services.ExecutionContext.LastDatabaseExecution = processLog.ToString();
                        AICustomerServiceWeb.Services.ExecutionContext.CurrentExecutionDetails = executionDetails;
                        return $"数据库查询失败：{ex.Message}";
                    }
                }
            }

            AICustomerServiceWeb.Services.ExecutionContext.LastDatabaseExecution = processLog.ToString();
            return "未知错误";
        }
        catch (Exception ex)
        {
            processLog.AppendLine();
            processLog.AppendLine($"❌ 执行失败: {ex.Message}");

            // 记录失败
            executionDetails.Status = "Failed";
            executionDetails.ErrorMessage = ex.Message;
            executionDetails.TotalExecutionTime = (int)sw.ElapsedMilliseconds;

            AICustomerServiceWeb.Services.ExecutionContext.LastDatabaseExecution = processLog.ToString();
            AICustomerServiceWeb.Services.ExecutionContext.CurrentExecutionDetails = executionDetails;
            return $"数据库查询失败：{ex.Message}";
        }
    }

    private string BuildSQLPrompt(string question, string examples, string ddl, string businessRules, string? errorFeedback = null)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("你是一个MySQL SQL专家。请根据用户问题生成准确的查询语句。");
        prompt.AppendLine();

        if (!string.IsNullOrEmpty(examples))
        {
            prompt.AppendLine("参考示例（类似问题的SQL）：");
            prompt.AppendLine(examples);
            prompt.AppendLine();
        }

        if (!string.IsNullOrEmpty(ddl))
        {
            prompt.AppendLine("相关表结构和字段说明：");
            prompt.AppendLine(ddl);
            prompt.AppendLine();
        }

        if (!string.IsNullOrEmpty(businessRules))
        {
            prompt.AppendLine("业务规则说明（帮助理解数据关系和业务逻辑）：");
            prompt.AppendLine(businessRules);
            prompt.AppendLine();
        }

        if (!string.IsNullOrEmpty(errorFeedback))
        {
            prompt.AppendLine("⚠️ 上次生成的SQL执行失败：");
            prompt.AppendLine(errorFeedback);
            prompt.AppendLine("请重新生成正确的SQL。");
            prompt.AppendLine();
        }

        prompt.AppendLine($"用户问题：{question}");
        prompt.AppendLine();
        prompt.AppendLine("🚨 严格要求：");
        prompt.AppendLine("1. 只返回SQL语句，不要任何解释");
        prompt.AppendLine("2. 使用反引号包裹表名和字段名");
        prompt.AppendLine("3. 考虑 IsDeleted=0 筛选有效数据");
        prompt.AppendLine("4. 添加合理的LIMIT限制（默认20条）");
        prompt.AppendLine("5. 如果是统计查询，使用COUNT()");
        prompt.AppendLine("6. 🔴 只能使用上述提供的表结构和字段");
        prompt.AppendLine("7. 🔴 严禁编造或猜测不存在的表名和字段名");
        prompt.AppendLine("8. 🔴 优先参考业务规则文档理解数据模型和表之间的关联关系");
        prompt.AppendLine("9. 🔴 如果业务规则与DDL都提供了相关信息，以业务规则为准");
        prompt.AppendLine("10. 如果实在无法生成SQL，返回：ERROR: 无法基于提供的表结构回答此问题");
        prompt.AppendLine();
        prompt.AppendLine("SQL：");

        return prompt.ToString();
    }

    private string ExtractSQL(string response)
    {
        var sql = response.Trim();

        // 检查是否是错误消息
        if (sql.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
        {
            return sql; // 保持ERROR格式，在主方法中处理
        }

        if (sql.Contains("```sql"))
        {
            var start = sql.IndexOf("```sql") + 6;
            var end = sql.IndexOf("```", start);
            if (end > start)
            {
                sql = sql.Substring(start, end - start).Trim();
            }
        }
        else if (sql.Contains("```"))
        {
            var start = sql.IndexOf("```") + 3;
            var end = sql.IndexOf("```", start);
            if (end > start)
            {
                sql = sql.Substring(start, end - start).Trim();
            }
        }

        if (sql.StartsWith("SQL:", StringComparison.OrdinalIgnoreCase))
        {
            sql = sql.Substring(4).Trim();
        }

        // 最后检查是否包含非SQL关键字（可能是错误信息）
        if (sql.Contains("无法") || sql.Contains("抱歉") || sql.Contains("不足") || sql.Contains("ERROR"))
        {
            return "ERROR: LLM返回的不是有效SQL语句";
        }

        return sql;
    }

    private async Task<string> ExecuteSQL(string sql)
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new MySqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        var result = new StringBuilder();

        var columns = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        result.AppendLine();
        result.AppendLine("| " + string.Join(" | ", columns) + " |");
        result.AppendLine("|" + string.Join("|", columns.Select(_ => "---")) + "|");

        int rowCount = 0;
        while (await reader.ReadAsync() && rowCount < 20)
        {
            var values = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                values.Add(reader.GetValue(i)?.ToString() ?? "NULL");
            }
            result.AppendLine("| " + string.Join(" | ", values) + " |");
            rowCount++;
        }

        if (rowCount == 0)
        {
            result.AppendLine("(查询结果为空)");
        }

        return result.ToString();
    }

    private int? ExtractRowCount(string result)
    {
        // Try to count rows from table format
        if (result.Contains("|---"))
        {
            var lines = result.Split('\n');
            int count = 0;
            bool inData = false;
            foreach (var line in lines)
            {
                if (line.Contains("|---"))
                {
                    inData = true;
                    continue;
                }
                if (inData && line.StartsWith("|") && !line.Contains("查询结果为空"))
                {
                    count++;
                }
            }
            return count > 0 ? count : null;
        }
        return null;
    }
}