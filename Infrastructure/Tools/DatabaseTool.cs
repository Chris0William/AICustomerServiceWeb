using AICustomerServiceWeb2.Core.Interfaces;
using AICustomerServiceWeb2.Core.Models;
using AICustomerServiceWeb2.Core.Tools;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace AICustomerServiceWeb2.Infrastructure.Tools;

/// <summary>
/// 数据库查询工具 - 完整实现
/// 集成 RAGFlow 知识库检索和真实数据库查询
/// </summary>
public class DatabaseTool : IAgentTool
{
    private readonly ILogger<DatabaseTool> _logger;
    private readonly IRAGFlowService _ragflowService;
    private readonly IDatabaseService _databaseService;
    private readonly Kernel _kernel;

    public string Name => "database_query";

    public string Description => "查询数据库获取数据。会自动从知识库检索相关schema和示例，生成SQL并执行。";

    public string ParametersSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""question"": {
      ""type"": ""string"",
      ""description"": ""用户的查询问题""
    }
  },
  ""required"": [""question""]
}";

    public DatabaseTool(
        ILogger<DatabaseTool> logger,
        IRAGFlowService ragflowService,
        IDatabaseService databaseService,
        Kernel kernel)
    {
        _logger = logger;
        _ragflowService = ragflowService;
        _databaseService = databaseService;
        _kernel = kernel;
    }

    public async Task<ToolResult> ExecuteAsync(
        string parameters,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var executionSteps = new List<object>();

        try
        {
            _logger.LogInformation("[DatabaseTool] ========== 开始执行 ==========");
            _logger.LogInformation("[DatabaseTool] 参数: {Parameters}", parameters);

            // 1. 解析参数
            var param = JsonConvert.DeserializeObject<dynamic>(parameters);
            string question = param?.question ?? context.UserMessage;

            _logger.LogInformation("[DatabaseTool] 问题: {Question}", question);

            // 2. RAGFlow 检索 Q2SQL 示例
            var q2sqlResponse = await _ragflowService.RetrieveQ2SQLExamplesAsync(question, 8, cancellationToken);
            executionSteps.Add(new
            {
                step = "Q2SQL检索",
                kb = "Q2SQL示例库",
                count = q2sqlResponse.Items.Count,
                time_ms = q2sqlResponse.ElapsedMs
            });
            _logger.LogInformation("[DatabaseTool] Q2SQL检索完成: {Count} 条", q2sqlResponse.Items.Count);

            // 3. RAGFlow 检索 DDL 结构
            var ddlResponse = await _ragflowService.RetrieveDDLSchemasAsync(question, 10, cancellationToken);
            executionSteps.Add(new
            {
                step = "DDL检索",
                kb = "DDL结构库",
                count = ddlResponse.Items.Count,
                time_ms = ddlResponse.ElapsedMs
            });
            _logger.LogInformation("[DatabaseTool] DDL检索完成: {Count} 条", ddlResponse.Items.Count);

            // 4. RAGFlow 检索业务规则
            var rulesResponse = await _ragflowService.RetrieveBusinessRulesAsync(question, 5, cancellationToken);
            executionSteps.Add(new
            {
                step = "业务规则检索",
                kb = "业务规则库",
                count = rulesResponse.Items.Count,
                time_ms = rulesResponse.ElapsedMs
            });
            _logger.LogInformation("[DatabaseTool] 业务规则检索完成: {Count} 条", rulesResponse.Items.Count);

            // 5. 使用 LLM 生成 SQL
            var sqlGenerationPrompt = BuildSqlGenerationPrompt(question, q2sqlResponse, ddlResponse, rulesResponse);
            _logger.LogDebug("[DatabaseTool] SQL生成提示词:\n{Prompt}", sqlGenerationPrompt);

            var sqlGenerationStart = Stopwatch.StartNew();
            var sqlResult = await _kernel.InvokePromptAsync(sqlGenerationPrompt, cancellationToken: cancellationToken);
            sqlGenerationStart.Stop();

            var generatedSql = sqlResult.ToString().Trim();
            // 清理 SQL (移除 markdown 代码块标记)
            generatedSql = CleanSql(generatedSql);

            executionSteps.Add(new
            {
                step = "SQL生成",
                sql = generatedSql,
                time_ms = sqlGenerationStart.ElapsedMilliseconds
            });
            _logger.LogInformation("[DatabaseTool] SQL生成完成:\n{Sql}", generatedSql);

            // 6. 验证 SQL
            var validationResult = await _databaseService.ValidateSqlAsync(generatedSql, "Production", cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors);
                _logger.LogWarning("[DatabaseTool] SQL验证失败: {Errors}", errors);

                executionSteps.Add(new
                {
                    step = "SQL验证",
                    valid = false,
                    errors = validationResult.Errors
                });

                return new ToolResult
                {
                    Success = false,
                    Error = $"SQL验证失败: {errors}",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    Metadata = new Dictionary<string, object>
                    {
                        ["execution_steps"] = executionSteps,
                        ["sql"] = generatedSql
                    }
                };
            }

            executionSteps.Add(new
            {
                step = "SQL验证",
                valid = true,
                warnings = validationResult.Warnings
            });
            _logger.LogInformation("[DatabaseTool] SQL验证通过");

            // 7. 执行 SQL
            var executeRequest = new SqlExecutionRequest
            {
                Sql = generatedSql,
                ConnectionName = "Production",
                TimeoutSeconds = 30
            };

            var executeResponse = await _databaseService.ExecuteQueryAsync(executeRequest, cancellationToken);

            if (!executeResponse.Success)
            {
                _logger.LogError("[DatabaseTool] SQL执行失败: {Error}", executeResponse.ErrorMessage);

                executionSteps.Add(new
                {
                    step = "SQL执行",
                    success = false,
                    error = executeResponse.ErrorMessage,
                    time_ms = executeResponse.ElapsedMs
                });

                return new ToolResult
                {
                    Success = false,
                    Error = $"SQL执行失败: {executeResponse.ErrorMessage}",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    Metadata = new Dictionary<string, object>
                    {
                        ["execution_steps"] = executionSteps,
                        ["sql"] = generatedSql
                    }
                };
            }

            executionSteps.Add(new
            {
                step = "SQL执行",
                success = true,
                row_count = executeResponse.AffectedRows,
                time_ms = executeResponse.ElapsedMs
            });
            _logger.LogInformation("[DatabaseTool] SQL执行成功: {RowCount} 行", executeResponse.AffectedRows);

            // 8. 格式化结果
            var formattedOutput = FormatQueryResult(question, generatedSql, executeResponse, executionSteps);

            stopwatch.Stop();

            _logger.LogInformation("[DatabaseTool] ========== 执行完成 ==========");
            _logger.LogInformation("[DatabaseTool] 总耗时: {Ms}ms", stopwatch.ElapsedMilliseconds);

            return new ToolResult
            {
                Success = true,
                Output = formattedOutput,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Metadata = new Dictionary<string, object>
                {
                    ["sql"] = generatedSql,
                    ["row_count"] = executeResponse.AffectedRows,
                    ["execution_steps"] = executionSteps,
                    ["query_result"] = executeResponse.Rows
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DatabaseTool] 执行异常");

            stopwatch.Stop();

            return new ToolResult
            {
                Success = false,
                Error = $"数据库查询失败: {ex.Message}",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Metadata = new Dictionary<string, object>
                {
                    ["execution_steps"] = executionSteps
                }
            };
        }
    }

    public Task<ValidationResult> ValidateParametersAsync(string parameters)
    {
        try
        {
            var param = JsonConvert.DeserializeObject<dynamic>(parameters);

            if (param == null)
            {
                return Task.FromResult(ValidationResult.Invalid("参数不能为空"));
            }

            return Task.FromResult(ValidationResult.Valid());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ValidationResult.Invalid($"参数格式错误: {ex.Message}"));
        }
    }

    /// <summary>
    /// 构建 SQL 生成提示词
    /// </summary>
    private string BuildSqlGenerationPrompt(
        string question,
        RAGFlowResponse q2sqlResponse,
        RAGFlowResponse ddlResponse,
        RAGFlowResponse rulesResponse)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("你是一个专业的SQL生成专家。请根据用户问题和提供的上下文信息生成准确的MySQL查询语句。");
        prompt.AppendLine();
        prompt.AppendLine($"**用户问题**: {question}");
        prompt.AppendLine();

        // Q2SQL 示例
        if (q2sqlResponse.Items.Any())
        {
            prompt.AppendLine("**参考示例 (Q2SQL)**:");
            foreach (var item in q2sqlResponse.Items.Take(5))
            {
                prompt.AppendLine($"- {item.Content}");
            }
            prompt.AppendLine();
        }

        // DDL 结构
        if (ddlResponse.Items.Any())
        {
            prompt.AppendLine("**数据库表结构 (DDL)**:");
            foreach (var item in ddlResponse.Items.Take(5))
            {
                prompt.AppendLine(item.Content);
                prompt.AppendLine();
            }
        }

        // 业务规则
        if (rulesResponse.Items.Any())
        {
            prompt.AppendLine("**业务规则**:");
            foreach (var item in rulesResponse.Items.Take(3))
            {
                prompt.AppendLine($"- {item.Content}");
            }
            prompt.AppendLine();
        }

        prompt.AppendLine("**SQL生成要求**:");
        prompt.AppendLine("1. 使用反引号包裹表名和列名");
        prompt.AppendLine("2. 所有查询必须包含 `IsDeleted=0` 条件");
        prompt.AppendLine("3. 非聚合查询必须添加 `LIMIT 20`");
        prompt.AppendLine("4. 只返回SQL语句，不要返回任何解释");
        prompt.AppendLine("5. SQL应该完整且可以直接执行");
        prompt.AppendLine();
        prompt.AppendLine("请生成SQL:");

        return prompt.ToString();
    }

    /// <summary>
    /// 清理 SQL (移除 markdown 标记)
    /// </summary>
    private string CleanSql(string sql)
    {
        // 移除 ```sql 和 ```
        sql = sql.Replace("```sql", "").Replace("```", "").Trim();
        return sql;
    }

    /// <summary>
    /// 格式化查询结果
    /// </summary>
    private string FormatQueryResult(
        string question,
        string sql,
        SqlExecutionResponse response,
        List<object> executionSteps)
    {
        var output = new StringBuilder();

        output.AppendLine("🔍 **数据库查询执行过程**");
        output.AppendLine();

        output.AppendLine("**1. RAGFlow知识库检索**");
        foreach (var step in executionSteps.Where(s => ((dynamic)s).step.ToString().Contains("检索")))
        {
            var s = (dynamic)step;
            output.AppendLine($"- {s.kb}：检索到 {s.count} 条相关内容 (耗时 {s.time_ms}ms)");
        }
        output.AppendLine();

        output.AppendLine("**2. SQL生成**");
        output.AppendLine("```sql");
        output.AppendLine(sql);
        output.AppendLine("```");
        output.AppendLine();

        output.AppendLine("**3. 查询结果**");
        if (response.Rows.Count == 0)
        {
            output.AppendLine("未找到匹配的数据");
        }
        else
        {
            output.AppendLine($"找到 {response.Rows.Count} 条记录：");
            output.AppendLine();

            // 显示前5行数据
            foreach (var row in response.Rows.Take(5))
            {
                var fields = string.Join(", ", row.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                output.AppendLine($"- {fields}");
            }

            if (response.Rows.Count > 5)
            {
                output.AppendLine($"... (还有 {response.Rows.Count - 5} 条记录)");
            }
        }

        return output.ToString();
    }
}
