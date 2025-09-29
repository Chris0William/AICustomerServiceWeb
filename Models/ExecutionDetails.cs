namespace AICustomerServiceWeb.Models;

/// <summary>
/// 执行详情，存储在 ai_message 表的 ExecutionDetails 字段中
/// </summary>
public class ExecutionDetails
{
    /// <summary>
    /// RAGFlow 检索步骤
    /// </summary>
    public List<RAGFlowStep> RAGFlowSteps { get; set; } = new();

    /// <summary>
    /// 生成的SQL语句
    /// </summary>
    public string? GeneratedSQL { get; set; }

    /// <summary>
    /// SQL执行结果行数
    /// </summary>
    public int? ResultRowCount { get; set; }

    /// <summary>
    /// 总执行时间(毫秒)
    /// </summary>
    public int TotalExecutionTime { get; set; }

    /// <summary>
    /// 执行状态
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// 错误信息(如果有)
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// RAGFlow 检索步骤
/// </summary>
public class RAGFlowStep
{
    /// <summary>
    /// 步骤编号
    /// </summary>
    public int StepNumber { get; set; }

    /// <summary>
    /// 步骤名称
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// 知识库ID
    /// </summary>
    public string KnowledgeBaseId { get; set; } = string.Empty;

    /// <summary>
    /// 查询内容
    /// </summary>
    public string QueryText { get; set; } = string.Empty;

    /// <summary>
    /// 检索到的条目数
    /// </summary>
    public int RetrievedCount { get; set; }

    /// <summary>
    /// 检索到的内容
    /// </summary>
    public List<RetrievedItem> RetrievedItems { get; set; } = new();

    /// <summary>
    /// 执行时间(毫秒)
    /// </summary>
    public int ExecutionTimeMs { get; set; }
}

/// <summary>
/// 检索到的单个条目
/// </summary>
public class RetrievedItem
{
    public string Content { get; set; } = string.Empty;
    public double Similarity { get; set; }
    public string DocumentName { get; set; } = string.Empty;
}