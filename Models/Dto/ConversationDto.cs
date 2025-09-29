namespace AICustomerServiceWeb.Models.Dto;

public class ConversationListDto
{
    public string ConversationId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public int MessageCount { get; set; }
    public int TotalTokens { get; set; }
    public DateTime UpdatedTime { get; set; }
}

public class ConversationDetailDto
{
    public string ConversationId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public int TotalTokens { get; set; }
    public List<MessageDto> Messages { get; set; } = new();
}

public class CreateConversationRequest
{
    public string ModelId { get; set; } = string.Empty;
    public string? Title { get; set; }
}

public class ConversationExportDto
{
    public string ConversationId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public int TotalTokens { get; set; }
    public List<MessageExportDto> Messages { get; set; } = new();
}

public class MessageExportDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public DateTime CreatedTime { get; set; }
}