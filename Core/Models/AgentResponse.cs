namespace AICustomerServiceWeb2.Core.Models;

/// <summary>
/// Agent响应模型
/// </summary>
public class AgentResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 最终状态
    /// </summary>
    public AgentState FinalState { get; set; }

    /// <summary>
    /// 思考过程（Thought）
    /// </summary>
    public AgentThought Thought { get; set; } = new();

    /// <summary>
    /// 最终答案
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 总耗时（毫秒）
    /// </summary>
    public long TotalTimeMs { get; set; }

    /// <summary>
    /// Token消耗统计
    /// </summary>
    public TokenUsage TokenUsage { get; set; } = new();

    /// <summary>
    /// 响应时间戳
    /// </summary>
    public DateTime ResponseTime { get; set; } = DateTime.Now;
}

/// <summary>
/// Agent思考过程模型
/// </summary>
public class AgentThought
{
    /// <summary>
    /// 执行计划
    /// </summary>
    public ExecutionPlan? InitialPlan { get; set; }

    /// <summary>
    /// 执行结果
    /// </summary>
    public ExecutionResult? ExecutionResult { get; set; }

    /// <summary>
    /// 反思记录
    /// </summary>
    public List<ReflectionResult> Reflections { get; set; } = new();

    /// <summary>
    /// 状态转换历史
    /// </summary>
    public List<StateTransition> StateHistory { get; set; } = new();

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }
}

/// <summary>
/// 状态转换记录
/// </summary>
public class StateTransition
{
    /// <summary>
    /// 从状态
    /// </summary>
    public AgentState FromState { get; set; }

    /// <summary>
    /// 到状态
    /// </summary>
    public AgentState ToState { get; set; }

    /// <summary>
    /// 转换原因
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 转换时间
    /// </summary>
    public DateTime TransitionTime { get; set; } = DateTime.Now;
}

/// <summary>
/// Token使用统计
/// </summary>
public class TokenUsage
{
    /// <summary>
    /// 输入Token数
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// 输出Token数
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    /// 总Token数
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;
}
