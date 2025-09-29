namespace AICustomerServiceWeb.Models.Entities;

public class AiConversation
{
    public long Id { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public string Title { get; set; } = "新会话";
    public int TotalTokens { get; set; }
    public int MessageCount { get; set; }
    public byte Status { get; set; } = 1;
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public byte IsDeleted { get; set; }
}