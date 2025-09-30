namespace AICustomerServiceWeb.Models.Dto;

public class MessageDto
{
    public long Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ExecutionDetails { get; set; }
    public int TokenCount { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class ChatRequest
{
    public string ConversationId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string ConversationId { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public int TotalTokens { get; set; }
    public long MessageId { get; set; }
    public string? ExecutionDetails { get; set; }
}

public class ModelInfo
{
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}