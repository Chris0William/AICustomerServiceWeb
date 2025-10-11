namespace AICustomerServiceWeb2.Core.Models;

/// <summary>
/// 执行结果模型
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// 是否执行成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 执行计划
    /// </summary>
    public ExecutionPlan Plan { get; set; } = new();

    /// <summary>
    /// 所有步骤的执行输出
    /// </summary>
    public List<StepOutput> StepOutputs { get; set; } = new();

    /// <summary>
    /// 最终输出（整合后的结果）
    /// </summary>
    public string FinalOutput { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 执行开始时间
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 执行结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 步骤输出模型
/// </summary>
public class StepOutput
{
    /// <summary>
    /// 步骤序号
    /// </summary>
    public int StepNumber { get; set; }

    /// <summary>
    /// 工具名称
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// 工具输入参数
    /// </summary>
    public string ToolInput { get; set; } = string.Empty;

    /// <summary>
    /// 工具输出结果
    /// </summary>
    public string ToolOutput { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }
}
