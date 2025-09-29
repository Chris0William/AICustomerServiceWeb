using System.Text;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AICustomerServiceWeb.Services;

// 检索结果详情
public class RetrievalResult
{
    public string KnowledgeBaseId { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public int RetrievedCount { get; set; }
    public List<RetrievedItem> RetrievedItems { get; set; } = new();
    public string FullResponse { get; set; } = string.Empty;
    public int ExecutionTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class RetrievedItem
{
    public string Content { get; set; } = string.Empty;
    public double Similarity { get; set; }
    public string DocumentName { get; set; } = string.Empty;
}

public class RAGFlowService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _endpoint;

    private readonly string _q2sqlKbId;
    private readonly string _ddlKbId;

    public RAGFlowService(string apiKey, string endpoint, string q2sqlKbId, string ddlKbId)
    {
        _apiKey = apiKey;
        _endpoint = endpoint;
        _q2sqlKbId = q2sqlKbId;
        _ddlKbId = ddlKbId;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<(string content, RetrievalResult details)> RetrieveQ2SQLExamplesWithDetails(string question, int topK = 5)
    {
        return await RetrieveFromKnowledgeBaseWithDetails(_q2sqlKbId, question, topK, "Q2SQL");
    }

    public async Task<(string content, RetrievalResult details)> RetrieveDDLWithDetails(string question, int topK = 3)
    {
        return await RetrieveFromKnowledgeBaseWithDetails(_ddlKbId, question, topK, "DDL+Description");
    }

    // 保留旧接口以保持兼容性
    public async Task<string> RetrieveQ2SQLExamples(string question, int topK = 5)
    {
        var (content, _) = await RetrieveQ2SQLExamplesWithDetails(question, topK);
        return content;
    }

    public async Task<string> RetrieveDDL(string question, int topK = 3)
    {
        var (content, _) = await RetrieveDDLWithDetails(question, topK);
        return content;
    }

    private async Task<(string content, RetrievalResult details)> RetrieveFromKnowledgeBaseWithDetails(string kbId, string query, int topK, string stepName)
    {
        var sw = Stopwatch.StartNew();
        var result = new RetrievalResult
        {
            KnowledgeBaseId = kbId,
            QueryText = query,
            StepName = stepName
        };

        try
        {
            var requestBody = new
            {
                question = query,
                dataset_ids = new[] { kbId },
                page = 1,
                page_size = topK,
                similarity_threshold = 0.2,
                vector_similarity_weight = 0.3
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(_endpoint, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            result.ExecutionTimeMs = (int)sw.ElapsedMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                return ("", result);
            }

            var jsonResult = JObject.Parse(responseContent);
            var code = jsonResult["code"]?.Value<int>();

            if (code != 0)
            {
                result.Success = false;
                return ("", result);
            }

            var chunks = jsonResult["data"]?["chunks"];

            if (chunks == null || !chunks.Any())
            {
                result.Success = true;
                result.RetrievedCount = 0;
                return ("", result);
            }

            var results = new StringBuilder();
            var retrievedItems = new List<RetrievedItem>();

            foreach (var chunk in chunks)
            {
                var chunkContent = chunk["content"]?.ToString() ?? "";
                var score = chunk["similarity"]?.Value<double>() ?? 0;
                var docName = chunk["document_name"]?.ToString() ?? "";

                if (!string.IsNullOrEmpty(chunkContent))
                {
                    results.AppendLine(chunkContent);
                    results.AppendLine("---");

                    retrievedItems.Add(new RetrievedItem
                    {
                        Content = chunkContent,
                        Similarity = score,
                        DocumentName = docName
                    });
                }
            }

            result.Success = true;
            result.RetrievedCount = retrievedItems.Count;
            result.RetrievedItems = retrievedItems;
            result.FullResponse = responseContent;

            return (results.ToString(), result);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.ExecutionTimeMs = (int)sw.ElapsedMilliseconds;
            return ("", result);
        }
    }

    private async Task<string> RetrieveFromKnowledgeBase(string kbId, string query, int topK)
    {
        var (content, _) = await RetrieveFromKnowledgeBaseWithDetails(kbId, query, topK, "");
        return content;
    }
}