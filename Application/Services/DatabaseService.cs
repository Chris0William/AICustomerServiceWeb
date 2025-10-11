using AICustomerServiceWeb2.Core.Interfaces;
using AICustomerServiceWeb2.Core.Models;
using Dapper;
using MySql.Data.MySqlClient;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AICustomerServiceWeb2.Application.Services;

/// <summary>
/// 数据库服务实现
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(
        IConfiguration configuration,
        ILogger<DatabaseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<SqlExecutionResponse> ExecuteQueryAsync(
        SqlExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("[DatabaseService] 执行查询: {Sql}", request.Sql);

            var connectionString = GetConnectionString(request.ConnectionName);
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var results = await connection.QueryAsync(
                request.Sql,
                request.Parameters,
                commandTimeout: request.TimeoutSeconds
            );

            var rows = results.Select(row =>
            {
                var dict = new Dictionary<string, object>();
                var rowDict = (IDictionary<string, object>)row;
                foreach (var kvp in rowDict)
                {
                    dict[kvp.Key] = kvp.Value ?? DBNull.Value;
                }
                return dict;
            }).ToList();

            stopwatch.Stop();

            _logger.LogInformation("[DatabaseService] 查询成功，返回 {Count} 行，耗时 {Ms}ms",
                rows.Count, stopwatch.ElapsedMilliseconds);

            return new SqlExecutionResponse
            {
                Success = true,
                Rows = rows,
                AffectedRows = rows.Count,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Sql = request.Sql
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[DatabaseService] 查询失败: {Sql}", request.Sql);

            return new SqlExecutionResponse
            {
                Success = false,
                Rows = new List<Dictionary<string, object>>(),
                AffectedRows = 0,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
                Sql = request.Sql
            };
        }
    }

    public async Task<SqlExecutionResponse> ExecuteNonQueryAsync(
        SqlExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("[DatabaseService] 执行命令: {Sql}", request.Sql);

            var connectionString = GetConnectionString(request.ConnectionName);
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = await connection.ExecuteAsync(
                request.Sql,
                request.Parameters,
                commandTimeout: request.TimeoutSeconds
            );

            stopwatch.Stop();

            _logger.LogInformation("[DatabaseService] 命令成功，影响 {Count} 行，耗时 {Ms}ms",
                affectedRows, stopwatch.ElapsedMilliseconds);

            return new SqlExecutionResponse
            {
                Success = true,
                Rows = new List<Dictionary<string, object>>(),
                AffectedRows = affectedRows,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Sql = request.Sql
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[DatabaseService] 命令失败: {Sql}", request.Sql);

            return new SqlExecutionResponse
            {
                Success = false,
                Rows = new List<Dictionary<string, object>>(),
                AffectedRows = 0,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
                Sql = request.Sql
            };
        }
    }

    public async Task<TableValidationResult> ValidateTableAsync(
        string tableName,
        string connectionName = "Production",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[DatabaseService] 验证表: {TableName}", tableName);

            var connectionString = GetConnectionString(connectionName);
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // 查询表是否存在
            var database = connection.Database;
            var existsSql = @"
                SELECT COUNT(*)
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = @Database
                AND TABLE_NAME = @TableName";

            var exists = await connection.ExecuteScalarAsync<int>(
                existsSql,
                new { Database = database, TableName = tableName }
            ) > 0;

            if (!exists)
            {
                return new TableValidationResult
                {
                    Exists = false,
                    TableName = tableName,
                    Columns = new List<ColumnInfo>()
                };
            }

            // 获取列信息
            var columnsSql = @"
                SELECT
                    COLUMN_NAME as ColumnName,
                    DATA_TYPE as DataType,
                    IS_NULLABLE as IsNullable,
                    COLUMN_DEFAULT as DefaultValue
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = @Database
                AND TABLE_NAME = @TableName
                ORDER BY ORDINAL_POSITION";

            var columns = await connection.QueryAsync<ColumnInfo>(
                columnsSql,
                new { Database = database, TableName = tableName }
            );

            _logger.LogInformation("[DatabaseService] 表 {TableName} 存在，包含 {Count} 列",
                tableName, columns.Count());

            return new TableValidationResult
            {
                Exists = true,
                TableName = tableName,
                Columns = columns.ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DatabaseService] 验证表失败: {TableName}", tableName);

            return new TableValidationResult
            {
                Exists = false,
                TableName = tableName,
                Columns = new List<ColumnInfo>()
            };
        }
    }

    public async Task<SqlValidationResult> ValidateSqlAsync(
        string sql,
        string connectionName = "Production",
        CancellationToken cancellationToken = default)
    {
        var result = new SqlValidationResult
        {
            IsValid = true,
            Errors = new List<string>(),
            Warnings = new List<string>(),
            Tables = ExtractTableNames(sql)
        };

        try
        {
            _logger.LogInformation("[DatabaseService] 验证 SQL: {Sql}", sql);

            // 基本语法检查
            if (string.IsNullOrWhiteSpace(sql))
            {
                result.IsValid = false;
                result.Errors.Add("SQL 语句不能为空");
                return result;
            }

            // 检查危险操作
            var dangerousKeywords = new[] { "DROP", "TRUNCATE", "ALTER", "CREATE" };
            foreach (var keyword in dangerousKeywords)
            {
                if (Regex.IsMatch(sql, $@"\b{keyword}\b", RegexOptions.IgnoreCase))
                {
                    result.Warnings.Add($"SQL 包含危险操作: {keyword}");
                }
            }

            // 检查是否包含 WHERE 子句 (UPDATE/DELETE 时)
            if (Regex.IsMatch(sql, @"\b(UPDATE|DELETE)\b", RegexOptions.IgnoreCase) &&
                !Regex.IsMatch(sql, @"\bWHERE\b", RegexOptions.IgnoreCase))
            {
                result.Warnings.Add("UPDATE/DELETE 语句缺少 WHERE 子句，可能影响所有行");
            }

            // 验证表是否存在
            foreach (var tableName in result.Tables)
            {
                var tableValidation = await ValidateTableAsync(tableName, connectionName, cancellationToken);
                if (!tableValidation.Exists)
                {
                    result.IsValid = false;
                    result.Errors.Add($"表 '{tableName}' 不存在");
                }
            }

            _logger.LogInformation("[DatabaseService] SQL 验证完成，有效: {IsValid}", result.IsValid);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DatabaseService] SQL 验证失败");

            result.IsValid = false;
            result.Errors.Add($"验证异常: {ex.Message}");
            return result;
        }
    }

    public async Task<List<string>> GetAllTablesAsync(
        string connectionName = "Production",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[DatabaseService] 获取所有表");

            var connectionString = GetConnectionString(connectionName);
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var database = connection.Database;
            var sql = @"
                SELECT TABLE_NAME
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = @Database
                AND TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_NAME";

            var tables = await connection.QueryAsync<string>(
                sql,
                new { Database = database }
            );

            var tableList = tables.ToList();

            _logger.LogInformation("[DatabaseService] 获取到 {Count} 个表", tableList.Count);

            return tableList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DatabaseService] 获取表列表失败");
            return new List<string>();
        }
    }

    /// <summary>
    /// 获取连接字符串
    /// </summary>
    private string GetConnectionString(string connectionName)
    {
        var connectionString = _configuration.GetConnectionString(connectionName);
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"连接字符串 '{connectionName}' 未配置");
        }
        return connectionString;
    }

    /// <summary>
    /// 从 SQL 中提取表名
    /// </summary>
    private List<string> ExtractTableNames(string sql)
    {
        var tables = new List<string>();

        // 匹配 FROM 和 JOIN 后面的表名
        var pattern = @"\b(?:FROM|JOIN)\s+([`\w]+)";
        var matches = Regex.Matches(sql, pattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var tableName = match.Groups[1].Value.Trim('`');
            if (!tables.Contains(tableName))
            {
                tables.Add(tableName);
            }
        }

        return tables;
    }
}
