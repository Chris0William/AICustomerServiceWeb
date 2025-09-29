namespace AICustomerServiceWeb.Models;

/// <summary>
/// 数据库查询结果包装类
/// </summary>
public class DatabaseQueryResult
{
    /// <summary>
    /// 查询结果文本（包含执行过程和数据）
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 执行详情
    /// </summary>
    public ExecutionDetails? ExecutionDetails { get; set; }
}