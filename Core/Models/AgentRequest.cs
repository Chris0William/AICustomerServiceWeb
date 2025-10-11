namespace AICustomerServiceWeb2.Core.Models;

/// <summary>
/// Agent请求模型
/// </summary>
public class AgentRequest
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// 用户消息
    /// </summary>
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>
    /// AI模型ID
    /// </summary>
    public string ModelId { get; set; } = "qwen-plus";

    /// <summary>
    /// 历史消息上下文（可选）
    /// </summary>
    public List<ConversationMessage>? ContextMessages { get; set; }

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// 请求时间戳
    /// </summary>
    public DateTime RequestTime { get; set; } = DateTime.Now;
}

/// <summary>
/// 会话消息模型
/// </summary>
public class ConversationMessage
{
    /// <summary>
    /// 角色：user/assistant/system
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;
}
