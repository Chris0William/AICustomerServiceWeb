using Microsoft.AspNetCore.Mvc;
using AICustomerServiceWeb.Services;
using System.Text;
using Newtonsoft.Json;

namespace AICustomerServiceWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatStreamController : ControllerBase
{
    private readonly SimpleAgentService _agentService;
    private readonly ConversationService _conversationService;
    private readonly ILogger<ChatStreamController> _logger;

    public ChatStreamController(
        SimpleAgentService agentService,
        ConversationService conversationService,
        ILogger<ChatStreamController> logger)
    {
        _agentService = agentService;
        _conversationService = conversationService;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task SendStreamingMessage([FromBody] StreamChatRequest request)
    {
        // 设置SSE响应头
        Response.ContentType = "text/event-stream";
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("X-Accel-Buffering", "no");

        try
        {
            // 确保会话存在
            var conversation = await _conversationService.GetConversationDetail(request.ConversationId);
            if (conversation == null)
            {
                await WriteSSE("error", "会话不存在");
                return;
            }

            // 检查是否需要生成标题（在处理消息之前）
            bool shouldGenerateTitle = conversation.Title == "新会话" &&
                                      (conversation.Messages?.Count(m => m.Role == "user") ?? 0) == 0;

            // 获取响应（SimpleAgentService会负责保存用户和助手消息）
            var response = await _agentService.ProcessMessageAsync(
                request.ConversationId,
                request.Message,
                request.ModelId ?? "qwen-plus"
            );

            // 模拟流式输出
            if (response.Success)
            {
                // 发送执行过程
                if (!string.IsNullOrEmpty(response.ExecutionProcess))
                {
                    await WriteSSE("process", response.ExecutionProcess);
                    await Task.Delay(100);
                }

                // 发送查询结果
                if (!string.IsNullOrEmpty(response.QueryResult))
                {
                    await WriteSSE("result", response.QueryResult);
                    await Task.Delay(100);
                }

                // 流式发送回答（逐字输出效果）
                if (!string.IsNullOrEmpty(response.Answer))
                {
                    var words = response.Answer.ToCharArray();
                    var buffer = new StringBuilder();

                    foreach (var word in words)
                    {
                        buffer.Append(word);

                        // 每10个字符或遇到标点符号时发送
                        if (buffer.Length >= 10 || IsPunctuation(word))
                        {
                            await WriteSSE("content", buffer.ToString());
                            buffer.Clear();
                            await Task.Delay(30); // 模拟打字效果
                        }
                    }

                    // 发送剩余内容
                    if (buffer.Length > 0)
                    {
                        await WriteSSE("content", buffer.ToString());
                    }
                }

                // 生成标题（如果需要）
                if (shouldGenerateTitle)
                {
                    var title = request.Message.Length > 30
                        ? request.Message.Substring(0, 30) + "..."
                        : request.Message;
                    await _conversationService.UpdateConversationTitle(request.ConversationId, title);
                    _logger.LogInformation($"[ChatStream] 自动生成标题: {title}");
                }
            }
            else
            {
                await WriteSSE("error", response.ErrorMessage ?? "处理失败");
            }

            // 发送完成信号
            await WriteSSE("done", "[DONE]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式响应失败");
            await WriteSSE("error", "系统错误：" + ex.Message);
        }
    }

    private async Task WriteSSE(string eventType, string data)
    {
        // 前端期望的格式: data: {"content": "xxx"} 或 data: {"type": "xxx", "data": "xxx"}
        var jsonData = new { type = eventType, content = data };
        var message = $"data: {JsonConvert.SerializeObject(jsonData)}\n\n";
        var bytes = Encoding.UTF8.GetBytes(message);
        await Response.Body.WriteAsync(bytes, 0, bytes.Length);
        await Response.Body.FlushAsync();
    }

    private bool IsPunctuation(char c)
    {
        return "，。！？；：,.!?;:".Contains(c);
    }
}

public class StreamChatRequest
{
    public string ConversationId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ModelId { get; set; }
}