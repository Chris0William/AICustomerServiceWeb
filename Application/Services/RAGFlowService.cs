using AICustomerServiceWeb2.Core.Interfaces;
using AICustomerServiceWeb2.Core.Models;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

namespace AICustomerServiceWeb2.Application.Services;

/// <summary>
/// RAGFlow 知识库检索服务实现
/// </summary>
public class RAGFlowService : IRAGFlowService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RAGFlowService> _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _q2sqlKbId;
    private readonly string _ddlKbId;
    private readonly string _businessRulesKbId;

    public RAGFlowService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RAGFlowService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _apiKey = configuration["RAGFlow:ApiKey"] ?? throw new InvalidOperationException("RAGFlow:ApiKey not configured");
        _baseUrl = configuration["RAGFlow:BaseUrl"] ?? throw new InvalidOperationException("RAGFlow:BaseUrl not configured");
        _q2sqlKbId = configuration["RAGFlow:Q2SQLKnowledgeBaseId"] ?? throw new InvalidOperationException("RAGFlow:Q2SQLKnowledgeBaseId not configured");
        _ddlKbId = configuration["RAGFlow:DDLKnowledgeBaseId"] ?? throw new InvalidOperationException("RAGFlow:DDLKnowledgeBaseId not configured");
        _businessRulesKbId = configuration["RAGFlow:BusinessRulesKnowledgeBaseId"] ?? throw new InvalidOperationException("RAGFlow:BusinessRulesKnowledgeBaseId not configured");

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<RAGFlowResponse> RetrieveAsync(
        RAGFlowRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("[RAGFlowService] 开始检索，知识库: {KbId}, 问题: {Question}",
                request.KnowledgeBaseId, request.Question);

            // 构建请求体
            var requestBody = new
            {
                question = request.Question,
                dataset_ids = new[] { request.KnowledgeBaseId },
                top_n = request.Limit,
                keyword_similarity_weight = request.KeywordWeight ?? 0.3,
                vector_similarity_weight = request.VectorWeight ?? 0.7
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 发送请求
            var response = await _httpClient.PostAsync(_baseUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("[RAGFlowService] 响应内容: {Response}", responseText);

            // 解析响应
            var apiResponse = JsonConvert.DeserializeObject<RAGFlowApiResponse<RAGFlowRetrievalData>>(responseText);

            if (apiResponse == null || apiResponse.Code != 0)
            {
                throw new Exception($"RAGFlow API 返回错误: {apiResponse?.Message ?? "Unknown error"}");
            }

            // 转换为标准格式
            var result = new RAGFlowResponse
            {
                Items = apiResponse.Data?.Chunks.Select(chunk => new RAGFlowItem
                {
                    Id = chunk.ChunkId,
                    Content = chunk.Content,
                    Score = chunk.Similarity,
                    Metadata = chunk.Metadata ?? new Dictionary<string, object>
                    {
                        ["doc_name"] = chunk.DocName
                    }
                }).ToList() ?? new List<RAGFlowItem>(),
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                KnowledgeBaseId = request.KnowledgeBaseId,
                Question = request.Question
            };

            _logger.LogInformation("[RAGFlowService] 检索成功，返回 {Count} 条结果，耗时 {Ms}ms",
                result.Items.Count, result.ElapsedMs);

            if (result.Items.Any())
            {
                _logger.LogDebug("[RAGFlowService] 第一条结果: {Content}",
                    result.Items[0].Content.Length > 100
                        ? result.Items[0].Content.Substring(0, 100) + "..."
                        : result.Items[0].Content);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[RAGFlowService] 检索失败，知识库: {KbId}, 问题: {Question}",
                request.KnowledgeBaseId, request.Question);

            return new RAGFlowResponse
            {
                Items = new List<RAGFlowItem>(),
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                KnowledgeBaseId = request.KnowledgeBaseId,
                Question = request.Question
            };
        }
    }

    public async Task<RAGFlowResponse> RetrieveQ2SQLExamplesAsync(
        string question,
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[RAGFlowService] 检索 Q2SQL 示例，问题: {Question}", question);

        return await RetrieveAsync(new RAGFlowRequest
        {
            Question = question,
            KnowledgeBaseId = _q2sqlKbId,
            Limit = limit,
            KeywordWeight = 0.3,
            VectorWeight = 0.7
        }, cancellationToken);
    }

    public async Task<RAGFlowResponse> RetrieveDDLSchemasAsync(
        string question,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[RAGFlowService] 检索 DDL 结构，问题: {Question}", question);

        // DDL 检索使用更高的关键词权重
        return await RetrieveAsync(new RAGFlowRequest
        {
            Question = question,
            KnowledgeBaseId = _ddlKbId,
            Limit = limit,
            KeywordWeight = 0.9,
            VectorWeight = 0.1
        }, cancellationToken);
    }

    public async Task<RAGFlowResponse> RetrieveBusinessRulesAsync(
        string question,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[RAGFlowService] 检索业务规则，问题: {Question}", question);

        return await RetrieveAsync(new RAGFlowRequest
        {
            Question = question,
            KnowledgeBaseId = _businessRulesKbId,
            Limit = limit,
            KeywordWeight = 0.3,
            VectorWeight = 0.7
        }, cancellationToken);
    }
}
