namespace AICustomerServiceWeb2.Core.Models;

/// <summary>
/// 执行计划模型
/// </summary>
public class ExecutionPlan
{
    /// <summary>
    /// 计划ID
    /// </summary>
    public string PlanId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 请求分析
    /// </summary>
    public string RequestAnalysis { get; set; } = string.Empty;

    /// <summary>
    /// 执行步骤列表
    /// </summary>
    public List<ExecutionStep> Steps { get; set; } = new();

    /// <summary>
    /// 预期结果描述
    /// </summary>
    public string ExpectedOutcome { get; set; } = string.Empty;

    /// <summary>
    /// 计划创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 是否为重试计划
    /// </summary>
    public bool IsRetry { get; set; } = false;

    /// <summary>
    /// 重试原因（如果是重试）
    /// </summary>
    public string? RetryReason { get; set; }
}

/// <summary>
/// 执行步骤模型
/// </summary>
public class ExecutionStep
{
    /// <summary>
    /// 步骤序号
    /// </summary>
    public int StepNumber { get; set; }

    /// <summary>
    /// 步骤描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 工具名称
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// 工具参数（JSON格式）
    /// </summary>
    public string ToolParameters { get; set; } = "{}";

    /// <summary>
    /// 依赖的步骤序号列表
    /// </summary>
    public List<int> Dependencies { get; set; } = new();

    /// <summary>
    /// 步骤状态
    /// </summary>
    public StepStatus Status { get; set; } = StepStatus.Pending;

    /// <summary>
    /// 执行结果
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// 步骤状态枚举
/// </summary>
public enum StepStatus
{
    /// <summary>
    /// 等待执行
    /// </summary>
    Pending,

    /// <summary>
    /// 执行中
    /// </summary>
    Running,

    /// <summary>
    /// 成功
    /// </summary>
    Succeeded,

    /// <summary>
    /// 失败
    /// </summary>
    Failed,

    /// <summary>
    /// 跳过
    /// </summary>
    Skipped
}
