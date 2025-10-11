using AICustomerServiceWeb2.Core.Agent;
using AICustomerServiceWeb2.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

namespace AICustomerServiceWeb2.Presentation.Controllers;

/// <summary>
/// Agent API控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IReActAgent _agent;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IReActAgent agent,
        ILogger<AgentController> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    /// <summary>
    /// 处理Agent请求（SSE流式响应）
    /// </summary>
    [HttpPost("chat")]
    public async Task ChatStream([FromBody] ChatRequest request)
    {
        // 设置SSE响应头
        Response.ContentType = "text/event-stream";
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("X-Accel-Buffering", "no");

        try
        {
            var agentRequest = new AgentRequest
            {
                ConversationId = request.ConversationId,
                UserMessage = request.Message,
                ModelId = request.ModelId ?? "qwen-plus",
                MaxRetries = 2
            };

            // 流式处理，实时推送状态
            var response = await _agent.ProcessStreamAsync(
                agentRequest,
                async (state, data) =>
                {
                    await SendStateUpdate(state, data);
                },
                HttpContext.RequestAborted
            );

            // 发送最终答案
            await SendSSE("answer", response.Answer);

            // 发送完成信号
            await SendSSE("done", new
            {
                success = response.Success,
                finalState = response.FinalState.ToString(),
                totalTimeMs = response.TotalTimeMs,
                retryCount = response.Thought.RetryCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AgentController] 处理请求失败");
            await SendSSE("error", new { message = ex.Message });
        }
    }

    /// <summary>
    /// 发送状态更新
    /// </summary>
    private async Task SendStateUpdate(AgentState state, object? data)
    {
        switch (state)
        {
            case AgentState.Planning:
                await SendSSE("state", new { state = "planning", message = "正在规划任务..." });
                break;

            case AgentState.Executing:
                if (data is ExecutionResult executionResult)
                {
                    await SendSSE("state", new { state = "executing", message = "正在执行计划..." });

                    // 发送执行过程
                    foreach (var stepOutput in executionResult.StepOutputs)
                    {
                        await SendSSE("step", new
                        {
                            stepNumber = stepOutput.StepNumber,
                            toolName = stepOutput.ToolName,
                            success = stepOutput.Success,
                            output = stepOutput.ToolOutput
                        });
                    }
                }
                break;

            case AgentState.Reflecting:
                if (data is ReflectionResult reflection)
                {
                    await SendSSE("state", new { state = "reflecting", message = "正在反思执行结果..." });
                    await SendSSE("reflection", new
                    {
                        analysis = reflection.Analysis,
                        confidenceScore = reflection.ConfidenceScore,
                        needsAdjustment = reflection.NeedsPlanAdjustment
                    });
                }
                break;

            case AgentState.Succeeded:
                await SendSSE("state", new { state = "succeeded", message = "任务成功完成" });
                break;

            case AgentState.Failed:
                await SendSSE("state", new { state = "failed", message = "任务执行失败" });
                break;
        }
    }

    /// <summary>
    /// 发送SSE消息
    /// </summary>
    private async Task SendSSE(string eventType, object data)
    {
        var jsonData = new { type = eventType, data = data };
        var message = $"data: {JsonConvert.SerializeObject(jsonData)}\n\n";
        var bytes = Encoding.UTF8.GetBytes(message);

        await Response.Body.WriteAsync(bytes, 0, bytes.Length);
        await Response.Body.FlushAsync();
    }
}

/// <summary>
/// 聊天请求模型
/// </summary>
public class ChatRequest
{
    public string ConversationId { get; set; } = Guid.NewGuid().ToString();
    public string Message { get; set; } = string.Empty;
    public string? ModelId { get; set; }
}
