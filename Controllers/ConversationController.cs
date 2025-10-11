using Microsoft.AspNetCore.Mvc;
using AICustomerServiceWeb.Models.Dto;
using AICustomerServiceWeb.Services;

namespace AICustomerServiceWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversationController : ControllerBase
{
    private readonly ConversationService _conversationService;

    public ConversationController(ConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request)
    {
        var conversationId = await _conversationService.CreateConversation(
            request.ModelId,
            request.ModelId,
            request.Title ?? "新会话");

        return Ok(new { conversationId });
    }

    [HttpGet]
    public async Task<IActionResult> GetConversationList()
    {
        var conversations = await _conversationService.GetConversationList();
        return Ok(conversations);
    }

    [HttpGet("{conversationId}")]
    public async Task<IActionResult> GetConversationDetail(string conversationId)
    {
        var conversation = await _conversationService.GetConversationDetail(conversationId);
        if (conversation == null)
            return NotFound();

        return Ok(conversation);
    }

    [HttpGet("{conversationId}/messages")]
    public async Task<IActionResult> GetConversationMessages(string conversationId)
    {
        var messages = await _conversationService.GetMessages(conversationId);
        return Ok(messages);
    }

    [HttpGet("{conversationId}/export")]
    public async Task<IActionResult> ExportConversation(string conversationId)
    {
        var conversation = await _conversationService.ExportConversation(conversationId);
        if (conversation == null)
            return NotFound();

        return Ok(conversation);
    }

    [HttpDelete("{conversationId}")]
    public async Task<IActionResult> DeleteConversation(string conversationId)
    {
        // 软删除会话（将IsDeleted设为1）
        var sql = "UPDATE ai_conversation SET IsDeleted = 1 WHERE ConversationId = @ConversationId";
        // TODO: 调用ConversationService实现
        return Ok(new { success = true });
    }
}