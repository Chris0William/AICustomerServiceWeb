using AICustomerServiceWeb2.Core.Agent;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace AICustomerServiceWeb2.Application.Services;

/// <summary>
/// 请求分类器实现 - 快速判断请求类型和处理策略
/// </summary>
public class RequestClassifier : IRequestClassifier
{
    private readonly Kernel _kernel;
    private readonly ILogger<RequestClassifier> _logger;

    // 简单会话关键词
    private static readonly HashSet<string> ConversationKeywords = new()
    {
        "你好", "您好", "hi", "hello", "在吗", "在么",
        "你是谁", "你是什么", "你能做什么", "怎么用", "如何使用",
        "谢谢", "感谢", "再见", "拜拜", "bye"
    };

    // 数据库查询关键词
    private static readonly HashSet<string> QueryKeywords = new()
    {
        "查询", "查看", "显示", "列出", "统计", "计算", "汇总",
        "有多少", "多少个", "总数", "数量", "几个", "count",
        "所有", "全部", "最新", "最近", "top", "排行", "排名"
    };

    // 复杂查询标志
    private static readonly HashSet<string> ComplexIndicators = new()
    {
        "关联", "连接", "join", "每个", "各个", "分组", "group",
        "对比", "比较", "趋势", "环比", "同比", "排行榜",
        "详细", "明细", "分析", "报表"
    };

    public RequestClassifier(
        Kernel kernel,
        ILogger<RequestClassifier> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<RequestClassification> ClassifyAsync(
        string userMessage,
        List<ConversationMessage>? context = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 1. 快速规则判断 (优先,避免 LLM 调用)
            var ruleBasedResult = ClassifyByRules(userMessage);
            if (ruleBasedResult.Confidence > 0.8)
            {
                stopwatch.Stop();
                _logger.LogInformation(
                    "[RequestClassifier] 规则分类: {Type}, 置信度: {Confidence:F2}, 耗时: {Ms}ms",
                    ruleBasedResult.Type, ruleBasedResult.Confidence, stopwatch.ElapsedMilliseconds);
                return ruleBasedResult;
            }

            // 2. 模糊情况,使用 LLM 判断
            var llmResult = await ClassifyByLLMAsync(userMessage, context, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "[RequestClassifier] LLM分类: {Type}, 置信度: {Confidence:F2}, 耗时: {Ms}ms",
                llmResult.Type, llmResult.Confidence, stopwatch.ElapsedMilliseconds);

            return llmResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RequestClassifier] 分类失败");

            // 默认使用完整流程(保守策略)
            return new RequestClassification
            {
                Type = RequestType.ComplexQuery,
                Strategy = ProcessingStrategy.FullFlow,
                Confidence = 0.5,
                Reason = $"分类失败,使用默认策略: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 基于规则的快速分类
    /// </summary>
    private RequestClassification ClassifyByRules(string message)
    {
        var normalized = message.ToLower().Trim();

        // 规则1: 非常短的消息,大概率是会话
        if (normalized.Length <= 10 && ConversationKeywords.Any(k => normalized.Contains(k)))
        {
            return new RequestClassification
            {
                Type = RequestType.SimpleConversation,
                Strategy = ProcessingStrategy.DirectResponse,
                Confidence = 0.95,
                Reason = "匹配会话关键词",
                Intent = "greeting_or_chat"
            };
        }

        // 规则2: 包含复杂查询标志
        if (ComplexIndicators.Any(indicator => normalized.Contains(indicator)))
        {
            return new RequestClassification
            {
                Type = RequestType.ComplexQuery,
                Strategy = ProcessingStrategy.FullFlow,
                Confidence = 0.85,
                Reason = "包含复杂查询标志",
                Intent = "complex_database_query"
            };
        }

        // 规则3: 包含查询关键词但不复杂
        if (QueryKeywords.Any(keyword => normalized.Contains(keyword)))
        {
            return new RequestClassification
            {
                Type = RequestType.SimpleQuery,
                Strategy = ProcessingStrategy.SimplifiedFlow,
                Confidence = 0.8,
                Reason = "包含查询关键词",
                Intent = "simple_database_query"
            };
        }

        // 规则4: 疑问句但不包含查询词,可能是会话
        if (Regex.IsMatch(normalized, @"[?？吗呢啊嘛]$"))
        {
            return new RequestClassification
            {
                Type = RequestType.SimpleConversation,
                Strategy = ProcessingStrategy.DirectResponse,
                Confidence = 0.7,
                Reason = "疑问句但无查询意图",
                Intent = "question_or_chat"
            };
        }

        // 置信度不足,需要 LLM 判断
        return new RequestClassification
        {
            Type = RequestType.SimpleQuery, // 默认假设
            Strategy = ProcessingStrategy.SimplifiedFlow,
            Confidence = 0.5,
            Reason = "规则无法明确判断"
        };
    }

    /// <summary>
    /// 使用 LLM 进行精确分类
    /// </summary>
    private async Task<RequestClassification> ClassifyByLLMAsync(
        string message,
        List<ConversationMessage>? context,
        CancellationToken cancellationToken)
    {
        var prompt = BuildClassificationPrompt(message, context);

        var response = await _kernel.InvokePromptAsync(
            prompt,
            cancellationToken: cancellationToken);

        return ParseClassificationResult(response.ToString(), message);
    }

    private string BuildClassificationPrompt(string message, List<ConversationMessage>? context)
    {
        var prompt = @"你是一个智能请求分类器。分析用户消息,判断其类型。

**用户消息**: " + message;

        if (context != null && context.Any())
        {
            prompt += "\n\n**对话上下文**:\n";
            foreach (var msg in context.TakeLast(3))
            {
                prompt += $"- {msg.Role}: {msg.Content}\n";
            }
        }

        prompt += @"

**分类标准**:
1. SimpleConversation: 问候、闲聊、询问系统功能、感谢告别
2. SimpleQuery: 单表查询、简单统计(COUNT/SUM)、列表展示
3. ComplexQuery: 多表关联、分组聚合、复杂业务逻辑、趋势分析

**输出JSON**:
```json
{
  ""type"": ""SimpleConversation | SimpleQuery | ComplexQuery"",
  ""confidence"": 0.9,
  ""reason"": ""简短说明"",
  ""intent"": ""提取的具体意图""
}
```

只输出JSON,不要其他文字。";

        return prompt;
    }

    private RequestClassification ParseClassificationResult(string response, string originalMessage)
    {
        try
        {
            // 提取JSON
            var json = response.Trim();
            if (json.Contains("```json"))
            {
                var startIndex = json.IndexOf("```json") + 7;
                var endIndex = json.LastIndexOf("```");
                json = json.Substring(startIndex, endIndex - startIndex).Trim();
            }
            else if (json.Contains("```"))
            {
                var startIndex = json.IndexOf("```") + 3;
                var endIndex = json.LastIndexOf("```");
                json = json.Substring(startIndex, endIndex - startIndex).Trim();
            }

            var data = JsonConvert.DeserializeObject<dynamic>(json)!;

            var typeStr = (string)data.type;
            var type = Enum.Parse<RequestType>(typeStr);

            var strategy = type switch
            {
                RequestType.SimpleConversation => ProcessingStrategy.DirectResponse,
                RequestType.SimpleQuery => ProcessingStrategy.SimplifiedFlow,
                RequestType.ComplexQuery => ProcessingStrategy.FullFlow,
                _ => ProcessingStrategy.FullFlow
            };

            return new RequestClassification
            {
                Type = type,
                Strategy = strategy,
                Confidence = (double)data.confidence,
                Reason = (string)data.reason,
                Intent = (string?)data.intent
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RequestClassifier] 解析LLM响应失败: {Response}", response);

            // 解析失败,回退到规则判断
            return ClassifyByRules(originalMessage);
        }
    }
}
