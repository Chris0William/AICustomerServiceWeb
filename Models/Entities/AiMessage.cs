namespace AICustomerServiceWeb.Models.Entities;

public class AiMessage
{
    public long Id { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public DateTime CreatedTime { get; set; }
    public byte IsDeleted { get; set; }
}