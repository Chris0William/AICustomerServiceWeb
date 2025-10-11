namespace AICustomerServiceWeb2.Core.Models;

/// <summary>
/// 对话信息
/// </summary>
public class Conversation
{
    /// <summary>
    /// 对话ID
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// 对话标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 模型ID
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// 模型名称
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// 消息数量
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// 总Token数
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedTime { get; set; }

    /// <summary>
    /// 消息列表
    /// </summary>
    public List<Message> Messages { get; set; } = new();
}

/// <summary>
/// 消息信息
/// </summary>
public class Message
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 对话ID
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// 角色 (user/assistant)
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 执行详情 (JSON格式)
    /// </summary>
    public string? ExecutionDetails { get; set; }

    /// <summary>
    /// Token数量
    /// </summary>
    public int TokenCount { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 保存消息请求
/// </summary>
public class SaveMessageRequest
{
    /// <summary>
    /// 对话ID
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// 角色
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 执行详情
    /// </summary>
    public string? ExecutionDetails { get; set; }

    /// <summary>
    /// Token数量
    /// </summary>
    public int TokenCount { get; set; }
}

/// <summary>
/// 创建对话请求
/// </summary>
public class CreateConversationRequest
{
    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 模型ID
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// 模型名称
    /// </summary>
    public string ModelName { get; set; } = string.Empty;
}
