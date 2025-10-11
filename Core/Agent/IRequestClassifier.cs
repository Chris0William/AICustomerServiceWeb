namespace AICustomerServiceWeb2.Core.Agent;

/// <summary>
/// 请求分类器接口 - 决定处理流程
/// </summary>
public interface IRequestClassifier
{
    /// <summary>
    /// 分类用户请求
    /// </summary>
    Task<RequestClassification> ClassifyAsync(
        string userMessage,
        List<ConversationMessage>? context = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 请求分类结果
/// </summary>
public class RequestClassification
{
    /// <summary>
    /// 请求类型
    /// </summary>
    public RequestType Type { get; set; }

    /// <summary>
    /// 置信度 (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// 建议的处理流程
    /// </summary>
    public ProcessingStrategy Strategy { get; set; }

    /// <summary>
    /// 分类原因
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 提取的意图
    /// </summary>
    public string? Intent { get; set; }

    /// <summary>
    /// 是否需要工具调用
    /// </summary>
    public bool RequiresToolCall => Type != RequestType.SimpleConversation;

    /// <summary>
    /// 是否需要反思
    /// </summary>
    public bool RequiresReflection => Type == RequestType.ComplexQuery;
}

/// <summary>
/// 请求类型
/// </summary>
public enum RequestType
{
    /// <summary>
    /// 简单会话 (问候、闲聊、说明)
    /// </summary>
    SimpleConversation,

    /// <summary>
    /// 简单查询 (单表查询、基础统计)
    /// </summary>
    SimpleQuery,

    /// <summary>
    /// 复杂查询 (多表关联、复杂聚合、业务逻辑)
    /// </summary>
    ComplexQuery
}

/// <summary>
/// 处理策略
/// </summary>
public enum ProcessingStrategy
{
    /// <summary>
    /// 直接响应 - 不需要规划和工具
    /// </summary>
    DirectResponse,

    /// <summary>
    /// 简化流程 - 规划 + 执行,跳过反思
    /// </summary>
    SimplifiedFlow,

    /// <summary>
    /// 完整流程 - 规划 + 执行 + 反思
    /// </summary>
    FullFlow
}

/// <summary>
/// 会话消息 (用于上下文)
/// </summary>
public class ConversationMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
