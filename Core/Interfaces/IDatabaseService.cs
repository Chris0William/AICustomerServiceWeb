using AICustomerServiceWeb2.Core.Models;

namespace AICustomerServiceWeb2.Core.Interfaces;

/// <summary>
/// 数据库服务接口
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// 执行 SQL 查询
    /// </summary>
    /// <param name="request">SQL 执行请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行响应</returns>
    Task<SqlExecutionResponse> ExecuteQueryAsync(
        SqlExecutionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行 SQL 命令(INSERT/UPDATE/DELETE)
    /// </summary>
    /// <param name="request">SQL 执行请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行响应</returns>
    Task<SqlExecutionResponse> ExecuteNonQueryAsync(
        SqlExecutionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证表是否存在
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="connectionName">连接名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>验证结果</returns>
    Task<TableValidationResult> ValidateTableAsync(
        string tableName,
        string connectionName = "Production",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证 SQL 语句
    /// </summary>
    /// <param name="sql">SQL 语句</param>
    /// <param name="connectionName">连接名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>验证结果</returns>
    Task<SqlValidationResult> ValidateSqlAsync(
        string sql,
        string connectionName = "Production",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取数据库中的所有表名
    /// </summary>
    /// <param name="connectionName">连接名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表名列表</returns>
    Task<List<string>> GetAllTablesAsync(
        string connectionName = "Production",
        CancellationToken cancellationToken = default);
}
