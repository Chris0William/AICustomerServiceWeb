using AICustomerServiceWeb2.Core.Models;
using AICustomerServiceWeb2.Core.Tools;
using Newtonsoft.Json;
using System.Diagnostics;

namespace AICustomerServiceWeb2.Infrastructure.Tools;

/// <summary>
/// 数据库查询工具（示例实现）
/// </summary>
public class DatabaseTool : IAgentTool
{
    private readonly ILogger<DatabaseTool> _logger;
    // 实际项目中这里应该注入RAGFlowService和DatabaseService

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

    public DatabaseTool(ILogger<DatabaseTool> logger)
    {
        _logger = logger;
    }

    public async Task<ToolResult> ExecuteAsync(
        string parameters,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation($"[DatabaseTool] 开始执行，参数：{parameters}");

            // 解析参数
            var param = JsonConvert.DeserializeObject<dynamic>(parameters);
            string question = param?.question ?? context.UserMessage;

            // TODO: 实际实现应该包括：
            // 1. 从RAGFlow检索Q2SQL示例
            // 2. 从RAGFlow检索DDL schema
            // 3. 从RAGFlow检索业务规则
            // 4. 使用LLM生成SQL
            // 5. 验证SQL
            // 6. 执行SQL
            // 7. 格式化结果

            // 这里是示例输出
            await Task.Delay(500, cancellationToken); // 模拟处理时间

            var result = new
            {
                ragflow_steps = new[]
                {
                    new { kb = "Q2SQL示例库", count = 8, time_ms = 150 },
                    new { kb = "DDL结构库", count = 10, time_ms = 200 },
                    new { kb = "业务规则库", count = 5, time_ms = 120 }
                },
                sql = "SELECT * FROM sys_emp WHERE IsDeleted=0 LIMIT 20",
                query_result = new[]
                {
                    new { id = 1, name = "张三", dept = "技术部" },
                    new { id = 2, name = "李四", dept = "市场部" }
                }
            };

            stopwatch.Stop();

            var output = $@"
🔍 **数据库查询执行过程**

**1. RAGFlow知识库检索**
- Q2SQL示例库：检索到 8 条相似示例
- DDL结构库：检索到 10 个相关表结构
- 业务规则库：检索到 5 条业务规则

**2. SQL生成**
```sql
{result.sql}
```

**3. 查询结果**
找到 2 条记录：
- 张三（技术部）
- 李四（市场部）
";

            return new ToolResult
            {
                Success = true,
                Output = output,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Metadata = new Dictionary<string, object>
                {
                    ["sql"] = result.sql,
                    ["ragflow_steps"] = result.ragflow_steps,
                    ["row_count"] = 2
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DatabaseTool] 执行失败");

            stopwatch.Stop();

            return new ToolResult
            {
                Success = false,
                Error = $"数据库查询失败：{ex.Message}",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
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

            // 可以验证question字段是否存在
            // if (string.IsNullOrWhiteSpace((string)param.question))
            // {
            //     return Task.FromResult(ValidationResult.Invalid("question字段不能为空"));
            // }

            return Task.FromResult(ValidationResult.Valid());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ValidationResult.Invalid($"参数格式错误：{ex.Message}"));
        }
    }
}
