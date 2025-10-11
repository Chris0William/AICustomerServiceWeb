using AICustomerServiceWeb2.Core.Models;

namespace AICustomerServiceWeb2.Core.Agent;

/// <summary>
/// 反思器接口
/// </summary>
public interface IReflector
{
    /// <summary>
    /// 反思执行结果
    /// </summary>
    /// <param name="plan">执行计划</param>
    /// <param name="result">执行结果</param>
    /// <param name="request">原始请求</param>
    /// <param name="currentRetryCount">当前重试次数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反思结果</returns>
    Task<ReflectionResult> ReflectAsync(
        ExecutionPlan plan,
        ExecutionResult result,
        AgentRequest request,
        int currentRetryCount,
        CancellationToken cancellationToken = default);
}
