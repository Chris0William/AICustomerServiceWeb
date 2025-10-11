using System.Data;

namespace AICustomerServiceWeb2.Core.Models;

/// <summary>
/// SQL 执行请求
/// </summary>
public class SqlExecutionRequest
{
    /// <summary>
    /// SQL 语句
    /// </summary>
    public string Sql { get; set; } = string.Empty;

    /// <summary>
    /// 参数列表
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// 超时时间(秒)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 连接字符串名称
    /// </summary>
    public string ConnectionName { get; set; } = "Production";
}

/// <summary>
/// SQL 执行响应
/// </summary>
public class SqlExecutionResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 查询结果(字典列表)
    /// </summary>
    public List<Dictionary<string, object>> Rows { get; set; } = new();

    /// <summary>
    /// 影响行数
    /// </summary>
    public int AffectedRows { get; set; }

    /// <summary>
    /// 执行耗时(毫秒)
    /// </summary>
    public long ElapsedMs { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// SQL 语句
    /// </summary>
    public string Sql { get; set; } = string.Empty;
}

/// <summary>
/// 表验证结果
/// </summary>
public class TableValidationResult
{
    /// <summary>
    /// 表是否存在
    /// </summary>
    public bool Exists { get; set; }

    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// 列信息
    /// </summary>
    public List<ColumnInfo> Columns { get; set; } = new();
}

/// <summary>
/// 列信息
/// </summary>
public class ColumnInfo
{
    /// <summary>
    /// 列名
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// 数据类型
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// 是否可为空
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// 默认值
    /// </summary>
    public string? DefaultValue { get; set; }
}

/// <summary>
/// SQL 验证结果
/// </summary>
public class SqlValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 错误消息列表
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 警告消息列表
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 提取的表名列表
    /// </summary>
    public List<string> Tables { get; set; } = new();
}
