using Microsoft.AspNetCore.Mvc;
using AICustomerServiceWeb.Models.Dto;
using AICustomerServiceWeb.Services;

namespace AICustomerServiceWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly AIService _aiService;
    private readonly ConversationService _conversationService;

    public ChatController(AIService aiService, ConversationService conversationService)
    {
        _aiService = aiService;
        _conversationService = conversationService;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        try
        {
            var conversation = await _conversationService.GetConversationDetail(request.ConversationId);
            if (conversation == null)
                return NotFound(new { message = "会话不存在" });

            var (response, tokenCount, messageId) = await _aiService.ChatAsync(request.ConversationId, request.Message);

            var conversationDetail = await _conversationService.GetConversationDetail(request.ConversationId);

            return Ok(new ChatResponse
            {
                ConversationId = request.ConversationId,
                Response = response,
                TokenCount = tokenCount,
                TotalTokens = conversationDetail?.TotalTokens ?? 0,
                MessageId = messageId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"处理失败: {ex.Message}" });
        }
    }
}