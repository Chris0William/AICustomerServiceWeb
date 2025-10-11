using AICustomerServiceWeb2.Core.Models;

namespace AICustomerServiceWeb2.Core.Agent;

/// <summary>
/// ReAct Agent接口
/// </summary>
public interface IReActAgent
{
    /// <summary>
    /// 处理请求（完整的ReAct循环）
    /// </summary>
    /// <param name="request">Agent请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Agent响应</returns>
    Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式处理请求（支持SSE推送中间状态）
    /// </summary>
    /// <param name="request">Agent请求</param>
    /// <param name="onStateChange">状态变化回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Agent响应</returns>
    Task<AgentResponse> ProcessStreamAsync(
        AgentRequest request,
        Func<AgentState, object?, Task> onStateChange,
        CancellationToken cancellationToken = default);
}
