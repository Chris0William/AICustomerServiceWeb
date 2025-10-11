namespace AICustomerServiceWeb2.Core.Models;

/// <summary>
/// 反思结果模型
/// </summary>
public class ReflectionResult
{
    /// <summary>
    /// 是否应该继续（true: 成功完成，false: 需要重试）
    /// </summary>
    public bool ShouldContinue { get; set; }

    /// <summary>
    /// 反思分析
    /// </summary>
    public string Analysis { get; set; } = string.Empty;

    /// <summary>
    /// 问题诊断
    /// </summary>
    public List<string> IssuesIdentified { get; set; } = new();

    /// <summary>
    /// 改进建议
    /// </summary>
    public List<string> Improvements { get; set; } = new();

    /// <summary>
    /// 是否需要调整计划
    /// </summary>
    public bool NeedsPlanAdjustment { get; set; }

    /// <summary>
    /// 调整后的计划（如果需要）
    /// </summary>
    public ExecutionPlan? AdjustedPlan { get; set; }

    /// <summary>
    /// 置信度评分（0-100）
    /// </summary>
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// 反思时间戳
    /// </summary>
    public DateTime ReflectionTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 当前重试次数
    /// </summary>
    public int CurrentRetryCount { get; set; }
}
