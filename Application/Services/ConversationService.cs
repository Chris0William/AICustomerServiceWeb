using AICustomerServiceWeb2.Core.Interfaces;
using AICustomerServiceWeb2.Core.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace AICustomerServiceWeb2.Application.Services;

/// <summary>
/// 对话管理服务实现
/// </summary>
public class ConversationService : IConversationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConversationService> _logger;
    private readonly string _connectionString;

    public ConversationService(
        IConfiguration configuration,
        ILogger<ConversationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("AICustomerService")
            ?? throw new InvalidOperationException("AICustomerService connection string not configured");
    }

    public async Task<string> CreateConversationAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var conversationId = Guid.NewGuid().ToString();
            var now = DateTime.Now;

            _logger.LogInformation("[ConversationService] 创建对话: {ConversationId}", conversationId);

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                INSERT INTO ai_conversation (
                    ConversationId, Title, ModelId, ModelName,
                    MessageCount, TotalTokens, CreatedTime, UpdatedTime
                )
                VALUES (
                    @ConversationId, @Title, @ModelId, @ModelName,
                    0, 0, @CreatedTime, @UpdatedTime
                )";

            await connection.ExecuteAsync(sql, new
            {
                ConversationId = conversationId,
                request.Title,
                request.ModelId,
                request.ModelName,
                CreatedTime = now,
                UpdatedTime = now
            });

            _logger.LogInformation("[ConversationService] 对话创建成功: {ConversationId}", conversationId);

            return conversationId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConversationService] 创建对话失败");
            throw;
        }
    }

    public async Task<Conversation?> GetConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[ConversationService] 获取对话: {ConversationId}", conversationId);

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT
                    ConversationId, Title, ModelId, ModelName,
                    MessageCount, TotalTokens, CreatedTime, UpdatedTime
                FROM ai_conversation
                WHERE ConversationId = @ConversationId";

            var conversation = await connection.QueryFirstOrDefaultAsync<Conversation>(
                sql,
                new { ConversationId = conversationId }
            );

            if (conversation != null)
            {
                conversation.Messages = await GetMessagesAsync(conversationId, 20, cancellationToken);
            }

            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConversationService] 获取对话失败: {ConversationId}", conversationId);
            return null;
        }
    }

    public async Task<List<Message>> GetMessagesAsync(
        string conversationId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[ConversationService] 获取消息列表: {ConversationId}, Limit: {Limit}",
                conversationId, limit);

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT
                    Id, ConversationId, Role, Content,
                    ExecutionDetails, TokenCount, CreatedTime
                FROM ai_message
                WHERE ConversationId = @ConversationId
                ORDER BY CreatedTime ASC
                LIMIT @Limit";

            var messages = await connection.QueryAsync<Message>(
                sql,
                new { ConversationId = conversationId, Limit = limit }
            );

            var messageList = messages.ToList();

            _logger.LogInformation("[ConversationService] 获取到 {Count} 条消息", messageList.Count);

            return messageList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConversationService] 获取消息列表失败: {ConversationId}", conversationId);
            return new List<Message>();
        }
    }

    public async Task<int> SaveMessageAsync(
        SaveMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[ConversationService] 保存消息: {ConversationId}, Role: {Role}, ExecutionDetails: {HasDetails}",
                request.ConversationId, request.Role, request.ExecutionDetails != null ? "Yes" : "No");

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // 保存消息
            var insertSql = @"
                INSERT INTO ai_message (
                    ConversationId, Role, Content, ExecutionDetails,
                    TokenCount, CreatedTime
                )
                VALUES (
                    @ConversationId, @Role, @Content, @ExecutionDetails,
                    @TokenCount, @CreatedTime
                );
                SELECT LAST_INSERT_ID();";

            var messageId = await connection.ExecuteScalarAsync<int>(insertSql, new
            {
                request.ConversationId,
                request.Role,
                request.Content,
                request.ExecutionDetails,
                request.TokenCount,
                CreatedTime = DateTime.Now
            });

            // 更新对话统计
            var updateSql = @"
                UPDATE ai_conversation
                SET
                    MessageCount = MessageCount + 1,
                    TotalTokens = TotalTokens + @TokenCount,
                    UpdatedTime = @UpdatedTime
                WHERE ConversationId = @ConversationId";

            await connection.ExecuteAsync(updateSql, new
            {
                request.ConversationId,
                request.TokenCount,
                UpdatedTime = DateTime.Now
            });

            _logger.LogInformation("[ConversationService] 消息保存成功: MessageId={MessageId}", messageId);

            return messageId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConversationService] 保存消息失败: {ConversationId}", request.ConversationId);
            throw;
        }
    }

    public async Task<List<Conversation>> GetAllConversationsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[ConversationService] 获取所有对话, Limit: {Limit}", limit);

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT
                    ConversationId, Title, ModelId, ModelName,
                    MessageCount, TotalTokens, CreatedTime, UpdatedTime
                FROM ai_conversation
                ORDER BY UpdatedTime DESC
                LIMIT @Limit";

            var conversations = await connection.QueryAsync<Conversation>(
                sql,
                new { Limit = limit }
            );

            var conversationList = conversations.ToList();

            _logger.LogInformation("[ConversationService] 获取到 {Count} 个对话", conversationList.Count);

            return conversationList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConversationService] 获取对话列表失败");
            return new List<Conversation>();
        }
    }

    public async Task<bool> DeleteConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[ConversationService] 删除对话: {ConversationId}", conversationId);

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // 删除消息
            var deleteMessagesSql = @"
                DELETE FROM ai_message
                WHERE ConversationId = @ConversationId";

            await connection.ExecuteAsync(deleteMessagesSql, new { ConversationId = conversationId });

            // 删除对话
            var deleteConversationSql = @"
                DELETE FROM ai_conversation
                WHERE ConversationId = @ConversationId";

            var affectedRows = await connection.ExecuteAsync(
                deleteConversationSql,
                new { ConversationId = conversationId }
            );

            _logger.LogInformation("[ConversationService] 对话删除成功: {ConversationId}", conversationId);

            return affectedRows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConversationService] 删除对话失败: {ConversationId}", conversationId);
            return false;
        }
    }

    public async Task<bool> UpdateConversationTitleAsync(
        string conversationId,
        string title,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[ConversationService] 更新对话标题: {ConversationId}, Title: {Title}",
                conversationId, title);

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                UPDATE ai_conversation
                SET
                    Title = @Title,
                    UpdatedTime = @UpdatedTime
                WHERE ConversationId = @ConversationId";

            var affectedRows = await connection.ExecuteAsync(sql, new
            {
                ConversationId = conversationId,
                Title = title,
                UpdatedTime = DateTime.Now
            });

            return affectedRows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConversationService] 更新对话标题失败: {ConversationId}", conversationId);
            return false;
        }
    }
}
