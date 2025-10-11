namespace AICustomerServiceWeb2.Core.Models;

/// <summary>
/// RAGFlow 检索请求
/// </summary>
public class RAGFlowRequest
{
    /// <summary>
    /// 查询问题
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// 知识库ID
    /// </summary>
    public string KnowledgeBaseId { get; set; } = string.Empty;

    /// <summary>
    /// 返回结果数量限制
    /// </summary>
    public int Limit { get; set; } = 10;

    /// <summary>
    /// 关键词权重 (0-1)
    /// </summary>
    public double? KeywordWeight { get; set; }

    /// <summary>
    /// 向量权重 (0-1)
    /// </summary>
    public double? VectorWeight { get; set; }
}

/// <summary>
/// RAGFlow 检索响应
/// </summary>
public class RAGFlowResponse
{
    /// <summary>
    /// 检索到的项目列表
    /// </summary>
    public List<RAGFlowItem> Items { get; set; } = new();

    /// <summary>
    /// 总耗时(毫秒)
    /// </summary>
    public long ElapsedMs { get; set; }

    /// <summary>
    /// 知识库ID
    /// </summary>
    public string KnowledgeBaseId { get; set; } = string.Empty;

    /// <summary>
    /// 查询问题
    /// </summary>
    public string Question { get; set; } = string.Empty;
}

/// <summary>
/// RAGFlow 检索项
/// </summary>
public class RAGFlowItem
{
    /// <summary>
    /// 文档ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 文档内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 相似度评分
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// RAGFlow API 响应包装
/// </summary>
public class RAGFlowApiResponse<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
}

/// <summary>
/// RAGFlow 检索数据
/// </summary>
public class RAGFlowRetrievalData
{
    public List<RAGFlowChunk> Chunks { get; set; } = new();
    public int Total { get; set; }
}

/// <summary>
/// RAGFlow 文档块
/// </summary>
public class RAGFlowChunk
{
    public string Content { get; set; } = string.Empty;
    public string DocName { get; set; } = string.Empty;
    public string ChunkId { get; set; } = string.Empty;
    public double Similarity { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
