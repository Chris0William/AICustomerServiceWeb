using System.ComponentModel;
using System.Linq;
using Microsoft.SemanticKernel;
using AICustomerServiceWeb.Services;

namespace AICustomerServiceWeb.Tools;

/// <summary>
/// RAGFlow知识库查询工具，提供独立的DDL、业务规则、Q2SQL示例查询功能
/// 让AI能够自主决定何时查询什么内容
/// </summary>
public class RAGFlowTool
{
    private readonly RAGFlowService _ragflow;
    private readonly Services.ExecutionContext _executionContext;
    private readonly ToolCallTracker _tracker;
    private readonly Dictionary<string, string> _ddlCache = new();
    private readonly Dictionary<string, string> _rulesCache = new();

    public RAGFlowTool(RAGFlowService ragflow, Services.ExecutionContext executionContext, ToolCallTracker tracker)
    {
        _ragflow = ragflow;
        _executionContext = executionContext;
        _tracker = tracker;
    }

    [KernelFunction]
    [Description(@"获取指定表的DDL结构和字段描述。
用于理解表结构、字段类型、关联关系。

使用场景：
- 需要了解某个表的结构时
- SQL执行失败需要确认字段名时
- 业务规则提到了新表需要了解结构时

示例：
- 获取sys_emp表结构
- 获取oms_worktype表结构
- 获取多个表：sys_emp,oms_worktype")]
    public async Task<string> GetTableDDL(
        [Description("表名，多个表用逗号分隔，如：sys_emp,oms_worktype")] string tableNames)
    {
        Console.WriteLine($"[RAGFlowTool] GetTableDDL called for: {tableNames}");

        // 检查是否超过调用限制
        if (!_tracker.CanCallTool("GetTableDDL", tableNames))
        {
            return "错误：该工具调用次数已达到限制。请使用已获取的信息或尝试其他方法。";
        }

        // 确保ExecutionDetails已初始化（仅在第一次创建）
        _executionContext.CurrentExecutionDetails ??= new Models.ExecutionDetails();

        var tables = tableNames.Split(',').Select(t => t.Trim()).ToList();
        var result = new List<string>();

        foreach (var table in tables)
        {
            // 检查缓存
            if (_ddlCache.ContainsKey(table))
            {
                Console.WriteLine($"[RAGFlowTool] Using cached DDL for {table}");
                result.Add(_ddlCache[table]);
                continue;
            }

            // 查询DDL
            var query = $"{table} 表结构 DDL CREATE TABLE";
            var (ddlContent, ddlDetails) = await _ragflow.RetrieveDDLWithDetails(query, 5);

            // 记录到ExecutionContext
            if (_executionContext.CurrentExecutionDetails != null && ddlDetails != null)
            {
                var step = new Models.RAGFlowStep
                {
                    StepNumber = _executionContext.CurrentExecutionDetails.RAGFlowSteps.Count + 1,
                    StepName = "DDL检索",
                    KnowledgeBaseId = ddlDetails.KnowledgeBaseId,
                    QueryText = query,
                    RetrievedCount = ddlDetails.RetrievedCount,
                    RetrievedItems = ddlDetails.RetrievedItems?.Select(item => new Models.RetrievedItem
                    {
                        Content = item.Content,
                        Similarity = item.Similarity,
                        DocumentName = item.DocumentName
                    }).ToList() ?? new List<Models.RetrievedItem>(),
                    ExecutionTimeMs = ddlDetails.ExecutionTimeMs
                };
                _executionContext.CurrentExecutionDetails.RAGFlowSteps.Add(step);
            }

            if (!string.IsNullOrEmpty(ddlContent))
            {
                _ddlCache[table] = ddlContent;
                result.Add($"=== {table} 表结构 ===\n{ddlContent}");
                Console.WriteLine($"[RAGFlowTool] Retrieved DDL for {table}: {ddlDetails?.RetrievedItems?.Count ?? 0} items");
            }
            else
            {
                result.Add($"=== {table} 表结构 ===\n未找到表 {table} 的DDL信息");
                Console.WriteLine($"[RAGFlowTool] No DDL found for {table}");
            }
        }

        return string.Join("\n\n", result);
    }

    [KernelFunction]
    [Description(@"获取业务规则和领域知识。
用于理解业务逻辑、数据关系、特殊规则。

使用场景：
- 理解业务概念（如：什么是高危作业人员）
- 了解表之间的业务关系
- 获取特定业务的计算规则

示例查询：
- 高危作业人员
- 承包商管理
- 班组人员统计")]
    public async Task<string> GetBusinessRules(
        [Description("业务相关的关键词或问题")] string query)
    {
        Console.WriteLine($"[RAGFlowTool] GetBusinessRules called for: {query}");

        // 检查是否超过调用限制
        if (!_tracker.CanCallTool("GetBusinessRules", query))
        {
            return "错误：该工具调用次数已达到限制。请使用已获取的信息或尝试其他方法。";
        }

        // 确保ExecutionDetails已初始化（仅在第一次创建）
        _executionContext.CurrentExecutionDetails ??= new Models.ExecutionDetails();

        // 检查缓存
        if (_rulesCache.ContainsKey(query))
        {
            Console.WriteLine($"[RAGFlowTool] Using cached rules for {query}");
            return _rulesCache[query];
        }

        var (rulesContent, rulesDetails) = await _ragflow.RetrieveBusinessRulesWithDetails(query, 5);

        // 记录到ExecutionContext
        if (_executionContext.CurrentExecutionDetails != null && rulesDetails != null)
        {
            var step = new Models.RAGFlowStep
            {
                StepNumber = _executionContext.CurrentExecutionDetails.RAGFlowSteps.Count + 1,
                StepName = "业务规则检索",
                KnowledgeBaseId = rulesDetails.KnowledgeBaseId,
                QueryText = query,
                RetrievedCount = rulesDetails.RetrievedCount,
                RetrievedItems = rulesDetails.RetrievedItems?.Select(item => new Models.RetrievedItem
                {
                    Content = item.Content,
                    Similarity = item.Similarity,
                    DocumentName = item.DocumentName
                }).ToList() ?? new List<Models.RetrievedItem>(),
                ExecutionTimeMs = rulesDetails.ExecutionTimeMs
            };
            _executionContext.CurrentExecutionDetails.RAGFlowSteps.Add(step);
        }

        var result = "";
        if (!string.IsNullOrEmpty(rulesContent))
        {
            result = $"=== 业务规则 ===\n{rulesContent}";
            _rulesCache[query] = result;
            Console.WriteLine($"[RAGFlowTool] Retrieved {rulesDetails?.RetrievedItems?.Count ?? 0} business rules");
        }
        else
        {
            result = "未找到相关业务规则";
            Console.WriteLine($"[RAGFlowTool] No business rules found for: {query}");
        }

        return result;
    }

    [KernelFunction]
    [Description(@"获取类似问题的SQL示例（Q2SQL）。
用于参考类似查询的SQL写法。

使用场景：
- 需要参考类似查询的SQL语法
- 学习特定业务的查询模式
- 了解复杂查询的写法

示例：
- 统计人员数量
- 高危作业查询
- 班组信息统计")]
    public async Task<string> GetQ2SQLExamples(
        [Description("查询相关的关键词")] string query)
    {
        Console.WriteLine($"[RAGFlowTool] GetQ2SQLExamples called for: {query}");

        var (examplesContent, examplesDetails) = await _ragflow.RetrieveQ2SQLExamplesWithDetails(query, 5);

        // 记录到ExecutionContext
        if (_executionContext.CurrentExecutionDetails != null && examplesDetails != null)
        {
            var step = new Models.RAGFlowStep
            {
                StepNumber = _executionContext.CurrentExecutionDetails.RAGFlowSteps.Count + 1,
                StepName = "Q2SQL示例检索",
                KnowledgeBaseId = examplesDetails.KnowledgeBaseId,
                QueryText = query,
                RetrievedCount = examplesDetails.RetrievedCount,
                RetrievedItems = examplesDetails.RetrievedItems?.Select(item => new Models.RetrievedItem
                {
                    Content = item.Content,
                    Similarity = item.Similarity,
                    DocumentName = item.DocumentName
                }).ToList() ?? new List<Models.RetrievedItem>(),
                ExecutionTimeMs = examplesDetails.ExecutionTimeMs
            };
            _executionContext.CurrentExecutionDetails.RAGFlowSteps.Add(step);
        }

        if (!string.IsNullOrEmpty(examplesContent))
        {
            Console.WriteLine($"[RAGFlowTool] Retrieved {examplesDetails?.RetrievedItems?.Count ?? 0} Q2SQL examples");
            return $"=== Q2SQL示例 ===\n{examplesContent}";
        }
        else
        {
            Console.WriteLine($"[RAGFlowTool] No Q2SQL examples found for: {query}");
            return "未找到相关SQL示例";
        }
    }

    [KernelFunction]
    [Description(@"搜索包含指定关键词的表名。
用于发现相关的数据表。

使用场景：
- 不确定表名时搜索相关表
- 寻找特定业务领域的表
- 探索数据库结构

示例：
- 搜索emp相关表
- 搜索work相关表
- 搜索provider相关表")]
    public async Task<string> SearchTables(
        [Description("表名关键词")] string keyword)
    {
        Console.WriteLine($"[RAGFlowTool] SearchTables called for: {keyword}");

        // 检查是否超过调用限制
        if (!_tracker.CanCallTool("SearchTables", keyword))
        {
            return "错误：该工具调用次数已达到限制。请使用已获取的信息或尝试其他方法。";
        }

        // 搜索DDL知识库中包含关键词的表
        var query = $"CREATE TABLE {keyword}";
        var (ddlContent, ddlDetails) = await _ragflow.RetrieveDDLWithDetails(query, 10);

        var tables = new HashSet<string>();

        if (ddlDetails?.RetrievedItems != null)
        {
            foreach (var item in ddlDetails.RetrievedItems)
            {
                // 从DDL内容中提取表名
                var tableMatch = System.Text.RegularExpressions.Regex.Match(
                    item.Content,
                    @"CREATE\s+TABLE\s+`?(\w+)`?",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (tableMatch.Success)
                {
                    tables.Add(tableMatch.Groups[1].Value);
                }
            }
        }

        if (tables.Any())
        {
            return $"找到包含 '{keyword}' 的相关表：\n" + string.Join("\n", tables.Select(t => $"- {t}"));
        }
        else
        {
            return $"未找到包含 '{keyword}' 的表";
        }
    }

    /// <summary>
    /// 清除缓存，用于新的查询会话
    /// </summary>
    public void ClearCache()
    {
        _ddlCache.Clear();
        _rulesCache.Clear();
        Console.WriteLine("[RAGFlowTool] Cache cleared");
    }
}