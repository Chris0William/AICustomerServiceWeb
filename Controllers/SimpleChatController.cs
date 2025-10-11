using Microsoft.AspNetCore.Mvc;
using AICustomerServiceWeb.Services;

namespace AICustomerServiceWeb.Controllers;

/// <summary>
/// 简化版聊天控制器 - 专注于准确性和实用性
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class SimpleChatController : ControllerBase
{
    private readonly SimpleAgentService _agentService;
    private readonly ConversationService _conversationService;
    private readonly ILogger<SimpleChatController> _logger;

    public SimpleChatController(
        SimpleAgentService agentService,
        ConversationService conversationService,
        ILogger<SimpleChatController> logger)
    {
        _agentService = agentService;
        _conversationService = conversationService;
        _logger = logger;
    }

    /// <summary>
    /// 发送消息并获取响应
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            _logger.LogInformation($"[SimpleChat] 收到消息: {request.Message}");

            // 确保有会话ID
            if (string.IsNullOrEmpty(request.ConversationId))
            {
                request.ConversationId = await _conversationService.CreateConversation(
                    "qwen-plus", "qwen-plus", "新会话");
            }

            // 处理消息
            var response = await _agentService.ProcessMessageAsync(
                request.ConversationId,
                request.Message);

            // 检查是否需要生成标题（会话的第一条用户消息）
            var conversation = await _conversationService.GetConversationDetail(request.ConversationId);
            var userMessageCount = conversation?.Messages?.Count(m => m.Role == "user") ?? 0;
            if (conversation != null && userMessageCount == 1 && conversation.Title == "新会话")
            {
                // 使用用户的第一个问题作为标题（截取前30个字符）
                var title = request.Message.Length > 30
                    ? request.Message.Substring(0, 30) + "..."
                    : request.Message;
                await _conversationService.UpdateConversationTitle(request.ConversationId, title);
                _logger.LogInformation($"[SimpleChat] 自动生成标题: {title}");
            }

            _logger.LogInformation($"[SimpleChat] 响应成功");

            return Ok(new
            {
                success = response.Success,
                conversationId = request.ConversationId,
                executionProcess = response.ExecutionProcess,
                answer = response.Answer,
                queryResult = response.QueryResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SimpleChat] 处理失败");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// 创建新会话
    /// </summary>
    [HttpPost("new-conversation")]
    public async Task<IActionResult> CreateConversation()
    {
        try
        {
            var conversationId = await _conversationService.CreateConversation(
                "qwen-plus", "qwen-plus", "新会话");

            return Ok(new { conversationId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建会话失败");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public class ChatRequest
    {
        public string ConversationId { get; set; } = "";
        public string Message { get; set; } = "";
    }
}