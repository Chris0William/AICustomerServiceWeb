using AICustomerServiceWeb2.Core.Models;

namespace AICustomerServiceWeb2.Core.Agent;

/// <summary>
/// 任务规划器接口
/// </summary>
public interface IPlanner
{
    /// <summary>
    /// 创建执行计划
    /// </summary>
    /// <param name="request">Agent请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行计划</returns>
    Task<ExecutionPlan> CreatePlanAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 调整计划（根据反思结果）
    /// </summary>
    /// <param name="originalPlan">原始计划</param>
    /// <param name="executionResult">执行结果</param>
    /// <param name="reflection">反思结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调整后的计划</returns>
    Task<ExecutionPlan> AdjustPlanAsync(
        ExecutionPlan originalPlan,
        ExecutionResult executionResult,
        ReflectionResult reflection,
        CancellationToken cancellationToken = default);
}
