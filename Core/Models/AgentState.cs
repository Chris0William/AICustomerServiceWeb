namespace AICustomerServiceWeb2.Core.Models;

/// <summary>
/// Agent状态枚举
/// </summary>
public enum AgentState
{
    /// <summary>
    /// 初始状态：等待输入
    /// </summary>
    Idle,

    /// <summary>
    /// 规划中：分析请求并制定执行计划
    /// </summary>
    Planning,

    /// <summary>
    /// 执行中：执行工具调用
    /// </summary>
    Executing,

    /// <summary>
    /// 反思中：分析执行结果，判断是否需要调整
    /// </summary>
    Reflecting,

    /// <summary>
    /// 成功：任务完成
    /// </summary>
    Succeeded,

    /// <summary>
    /// 失败：任务失败（重试次数耗尽）
    /// </summary>
    Failed
}
