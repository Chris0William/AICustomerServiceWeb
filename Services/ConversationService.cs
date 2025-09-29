using Dapper;
using MySql.Data.MySqlClient;
using AICustomerServiceWeb.Models.Entities;
using AICustomerServiceWeb.Models.Dto;

namespace AICustomerServiceWeb.Services;

public class ConversationService
{
    private readonly string _connectionString;
    private readonly int _maxContextMessages;

    public ConversationService(string connectionString, int maxContextMessages)
    {
        _connectionString = connectionString;
        _maxContextMessages = maxContextMessages;
    }

    public async Task<string> CreateConversation(string modelId, string? modelName, string? title = null)
    {
        var conversationId = Guid.NewGuid().ToString();
        var sql = @"INSERT INTO ai_conversation
            (ConversationId, ModelId, ModelName, Title, CreatedTime, UpdatedTime)
            VALUES (@ConversationId, @ModelId, @ModelName, @Title, NOW(), NOW())";

        using var conn = new MySqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new
        {
            ConversationId = conversationId,
            ModelId = modelId,
            ModelName = modelName,
            Title = title ?? "新会话"
        });

        return conversationId;
    }

    public async Task<List<ConversationListDto>> GetConversationList()
    {
        var sql = @"SELECT ConversationId, Title, ModelId, ModelName, MessageCount, TotalTokens, UpdatedTime
            FROM ai_conversation
            WHERE IsDeleted = 0
            ORDER BY UpdatedTime DESC
            LIMIT 50";

        using var conn = new MySqlConnection(_connectionString);
        var result = await conn.QueryAsync<ConversationListDto>(sql);
        return result.ToList();
    }

    public async Task<ConversationDetailDto?> GetConversationDetail(string conversationId)
    {
        var sql = @"SELECT ConversationId, Title, ModelId, ModelName, TotalTokens
            FROM ai_conversation
            WHERE ConversationId = @ConversationId AND IsDeleted = 0";

        using var conn = new MySqlConnection(_connectionString);
        var conversation = await conn.QueryFirstOrDefaultAsync<ConversationDetailDto>(sql, new { ConversationId = conversationId });

        if (conversation == null) return null;

        conversation.Messages = await GetMessages(conversationId);
        return conversation;
    }

    public async Task<List<MessageDto>> GetMessages(string conversationId, int? limit = null)
    {
        var actualLimit = limit ?? _maxContextMessages;
        var sql = $@"SELECT Id, Role, Content, ExecutionDetails, TokenCount, CreatedTime
            FROM ai_message
            WHERE ConversationId = @ConversationId AND IsDeleted = 0
            ORDER BY CreatedTime DESC
            LIMIT {actualLimit}";

        using var conn = new MySqlConnection(_connectionString);
        var messages = await conn.QueryAsync<MessageDto>(sql, new { ConversationId = conversationId });
        return messages.Reverse().ToList();
    }

    public async Task<long> SaveMessage(string conversationId, string role, string content, int tokenCount, string? executionDetails)
    {
        Console.WriteLine($"[ConversationService] Saving message with ExecutionDetails: {(executionDetails != null ? executionDetails.Length + " chars" : "null")}");

        var sql = @"INSERT INTO ai_message
            (ConversationId, Role, Content, ExecutionDetails, TokenCount, CreatedTime)
            VALUES (@ConversationId, @Role, @Content, @ExecutionDetails, @TokenCount, NOW());
            SELECT LAST_INSERT_ID();";

        using var conn = new MySqlConnection(_connectionString);
        var messageId = await conn.QueryFirstAsync<long>(sql, new
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            ExecutionDetails = executionDetails,
            TokenCount = tokenCount
        });

        await UpdateConversationStats(conversationId, tokenCount);
        return messageId;
    }

    private async Task UpdateConversationStats(string conversationId, int tokenCount)
    {
        var sql = @"UPDATE ai_conversation
            SET MessageCount = MessageCount + 1,
                TotalTokens = TotalTokens + @TokenCount,
                UpdatedTime = NOW()
            WHERE ConversationId = @ConversationId";

        using var conn = new MySqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new
        {
            ConversationId = conversationId,
            TokenCount = tokenCount
        });
    }

    public async Task UpdateConversationTitle(string conversationId, string title)
    {
        var sql = @"UPDATE ai_conversation
            SET Title = @Title, UpdatedTime = NOW()
            WHERE ConversationId = @ConversationId";

        using var conn = new MySqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new
        {
            ConversationId = conversationId,
            Title = title
        });
    }

    public async Task<ConversationExportDto?> ExportConversation(string conversationId)
    {
        var sql = @"SELECT ConversationId, Title, ModelId, CreatedTime, TotalTokens
            FROM ai_conversation
            WHERE ConversationId = @ConversationId AND IsDeleted = 0";

        using var conn = new MySqlConnection(_connectionString);
        var conversation = await conn.QueryFirstOrDefaultAsync<ConversationExportDto>(sql, new { ConversationId = conversationId });

        if (conversation == null) return null;

        var messageSql = @"SELECT Role, Content, TokenCount, CreatedTime
            FROM ai_message
            WHERE ConversationId = @ConversationId AND IsDeleted = 0
            ORDER BY CreatedTime ASC";

        var messages = await conn.QueryAsync<MessageExportDto>(messageSql, new { ConversationId = conversationId });
        conversation.Messages = messages.ToList();

        return conversation;
    }
}