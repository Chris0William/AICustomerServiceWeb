using AICustomerServiceWeb2.Core.Models;

namespace AICustomerServiceWeb2.Core.Agent;

/// <summary>
/// 执行器接口
/// </summary>
public interface IExecutor
{
    /// <summary>
    /// 执行计划
    /// </summary>
    /// <param name="plan">执行计划</param>
    /// <param name="context">工具上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<ExecutionResult> ExecutePlanAsync(
        ExecutionPlan plan,
        AgentRequest request,
        CancellationToken cancellationToken = default);
}
