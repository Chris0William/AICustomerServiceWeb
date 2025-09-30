using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using MySql.Data.MySqlClient;
using AICustomerServiceWeb.Services;
using AICustomerServiceWeb.Models;

namespace AICustomerServiceWeb.Tools;

/// <summary>
/// SQL执行工具，专注于SQL生成和执行
/// 配合RAGFlowTool使用，实现ReAct模式
/// </summary>
public class SQLTool
{
    private readonly string _connectionString;
    private readonly Services.ExecutionContext _executionContext;
    private readonly Kernel _kernel;
    private readonly ToolCallTracker _tracker;

    public SQLTool(string connectionString, Services.ExecutionContext executionContext, Kernel kernel, ToolCallTracker tracker)
    {
        _connectionString = connectionString;
        _executionContext = executionContext;
        _kernel = kernel;
        _tracker = tracker;
    }

    [KernelFunction]
    [Description(@"生成SQL语句（不执行）。
基于提供的问题、DDL、业务规则等信息生成SQL。

使用场景：
- 已经通过RAGFlowTool获取了必要信息
- 需要生成SQL但暂不执行
- 想要验证SQL语法

输入格式：
问题：用户的查询需求
DDL：相关表结构（可选）
业务规则：业务逻辑说明（可选）
Q2SQL示例：参考SQL（可选）")]
    public async Task<string> GenerateSQL(
        [Description("用户的查询问题")] string question,
        [Description("相关的DDL结构（可选）")] string ddl = "",
        [Description("业务规则（可选）")] string businessRules = "",
        [Description("Q2SQL示例（可选）")] string examples = "")
    {
        Console.WriteLine($"[SQLTool] GenerateSQL called for: {question}");

        // 确保ExecutionDetails已初始化（仅在第一次创建）
        _executionContext.CurrentExecutionDetails ??= new ExecutionDetails();

        var prompt = BuildSQLGenerationPrompt(question, ddl, businessRules, examples);
        var response = await _kernel.InvokePromptAsync(prompt);
        var sql = ExtractSQL(response.ToString());

        Console.WriteLine($"[SQLTool] Generated SQL: {sql}");

        // 记录生成的SQL到ExecutionDetails
        _executionContext.CurrentExecutionDetails.GeneratedSQL = sql;

        return sql;
    }

    [KernelFunction]
    [Description(@"执行SQL查询并返回结果。
直接执行提供的SQL语句。

使用场景：
- 已有SQL语句需要执行
- 验证SQL是否正确
- 获取查询结果

注意：
- 自动验证表是否存在
- 返回格式化的查询结果
- 记录执行详情")]
    public async Task<string> ExecuteSQL(
        [Description("要执行的SQL语句")] string sql)
    {
        Console.WriteLine($"[SQLTool] ExecuteSQL called");
        Console.WriteLine($"[SQLTool] SQL: {sql}");

        // 检查是否超过调用限制
        if (!_tracker.CanCallTool("ExecuteSQL", sql))
        {
            return "错误：SQL执行次数已达到限制。请检查之前的执行结果或修改SQL语句。";
        }

        var processLog = new StringBuilder();
        processLog.AppendLine("🔍 **SQL执行过程**");
        processLog.AppendLine();

        // 初始化ExecutionDetails
        if (_executionContext.CurrentExecutionDetails == null)
        {
            _executionContext.CurrentExecutionDetails = new ExecutionDetails();
        }
        _executionContext.CurrentExecutionDetails.GeneratedSQL = sql;

        try
        {
            // 验证表存在性
            processLog.AppendLine("**验证表存在性...**");
            var validation = await ValidateTablesInSQL(sql);
            if (!validation.IsValid)
            {
                processLog.AppendLine($"❌ 表验证失败: {validation.ErrorMessage}");
                _executionContext.CurrentExecutionDetails.Status = "Failed";
                _executionContext.CurrentExecutionDetails.ErrorMessage = validation.ErrorMessage;
                _executionContext.LastDatabaseExecution = processLog.ToString();
                return $"SQL执行失败：{validation.ErrorMessage}\n不存在的表：{string.Join(", ", validation.MissingTables)}";
            }
            processLog.AppendLine("✅ 表验证通过");

            // 执行SQL
            processLog.AppendLine();
            processLog.AppendLine("**执行SQL查询...**");
            processLog.AppendLine("```sql");
            processLog.AppendLine(sql);
            processLog.AppendLine("```");

            var result = await ExecuteSQLInternal(sql);
            processLog.AppendLine("✅ 查询执行成功");
            processLog.AppendLine();
            processLog.AppendLine(result);

            // 记录成功
            _executionContext.CurrentExecutionDetails.Status = "Success";
            _executionContext.CurrentExecutionDetails.ResultRowCount = ExtractRowCount(result);
            _executionContext.LastDatabaseExecution = processLog.ToString();

            Console.WriteLine($"[SQLTool] Execution successful, rows: {_executionContext.CurrentExecutionDetails.ResultRowCount}");

            return result;
        }
        catch (MySqlException ex)
        {
            processLog.AppendLine($"❌ SQL执行失败: {ex.Message}");
            _executionContext.CurrentExecutionDetails.Status = "Failed";
            _executionContext.CurrentExecutionDetails.ErrorMessage = ex.Message;
            _executionContext.LastDatabaseExecution = processLog.ToString();

            Console.WriteLine($"[SQLTool] Execution failed: {ex.Message}");
            return $"SQL执行失败：{ex.Message}";
        }
    }

    [KernelFunction]
    [Description(@"验证SQL中的表是否存在。
检查SQL语句引用的所有表是否在数据库中存在。

使用场景：
- 执行SQL前的预检查
- 验证生成的SQL是否有效
- 调试表名错误")]
    public async Task<string> ValidateTables(
        [Description("要验证的SQL语句")] string sql)
    {
        Console.WriteLine($"[SQLTool] ValidateTables called");

        var validation = await ValidateTablesInSQL(sql);
        if (validation.IsValid)
        {
            return "✅ 所有表都存在";
        }
        else
        {
            return $"❌ 以下表不存在：{string.Join(", ", validation.MissingTables)}";
        }
    }

    private string BuildSQLGenerationPrompt(string question, string ddl, string businessRules, string examples)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("你是一个MySQL SQL专家。请根据用户问题生成准确的查询语句。");
        prompt.AppendLine();

        if (!string.IsNullOrEmpty(examples))
        {
            prompt.AppendLine("=== 参考SQL示例 ===");
            prompt.AppendLine(examples);
            prompt.AppendLine();
        }

        if (!string.IsNullOrEmpty(ddl))
        {
            prompt.AppendLine("=== 表结构 ===");
            prompt.AppendLine(ddl);
            prompt.AppendLine();
        }

        if (!string.IsNullOrEmpty(businessRules))
        {
            prompt.AppendLine("=== 业务规则 ===");
            prompt.AppendLine(businessRules);
            prompt.AppendLine();
        }

        prompt.AppendLine($"用户问题：{question}");
        prompt.AppendLine();
        prompt.AppendLine("要求：");
        prompt.AppendLine("1. 只返回SQL语句，不要解释");
        prompt.AppendLine("2. 使用反引号包裹表名和字段名");
        prompt.AppendLine("3. 考虑 IsDeleted=0 筛选");
        prompt.AppendLine("4. 添加合理的LIMIT（默认20）");
        prompt.AppendLine("5. 只使用提供的表和字段");
        prompt.AppendLine();
        prompt.AppendLine("SQL：");

        return prompt.ToString();
    }

    private string ExtractSQL(string response)
    {
        var sql = response.Trim();

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

        return sql;
    }

    private async Task<string> ExecuteSQLInternal(string sql)
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new MySqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        var result = new StringBuilder();

        // 构建表头
        var columns = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        result.AppendLine("| " + string.Join(" | ", columns) + " |");
        result.AppendLine("|" + string.Join("|", columns.Select(_ => "---")) + "|");

        // 读取数据
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

    private class TableValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> MissingTables { get; set; } = new List<string>();
        public string ErrorMessage { get; set; } = "";
    }

    private async Task<TableValidationResult> ValidateTablesInSQL(string sql)
    {
        var result = new TableValidationResult { IsValid = true };
        var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 提取表名
        var fromMatches = System.Text.RegularExpressions.Regex.Matches(sql, @"FROM\s+`?(\w+)`?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in fromMatches)
        {
            if (match.Groups.Count > 1)
                tableNames.Add(match.Groups[1].Value);
        }

        var joinMatches = System.Text.RegularExpressions.Regex.Matches(sql, @"JOIN\s+`?(\w+)`?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in joinMatches)
        {
            if (match.Groups.Count > 1)
                tableNames.Add(match.Groups[1].Value);
        }

        // 验证表存在性
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();

        foreach (var tableName in tableNames)
        {
            using var cmd = new MySqlCommand($"SHOW TABLES LIKE '{tableName}'", conn);
            var exists = await cmd.ExecuteScalarAsync();

            if (exists == null)
            {
                result.IsValid = false;
                result.MissingTables.Add(tableName);
            }
        }

        if (!result.IsValid)
        {
            result.ErrorMessage = $"表 {string.Join(", ", result.MissingTables)} 不存在";
        }

        return result;
    }

    private int? ExtractRowCount(string result)
    {
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