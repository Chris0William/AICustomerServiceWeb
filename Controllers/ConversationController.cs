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
            request.Title);

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

    [HttpGet("{conversationId}/export")]
    public async Task<IActionResult> ExportConversation(string conversationId)
    {
        var conversation = await _conversationService.ExportConversation(conversationId);
        if (conversation == null)
            return NotFound();

        return Ok(conversation);
    }
}