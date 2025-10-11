using AICustomerServiceWeb2.Core.Models;

namespace AICustomerServiceWeb2.Core.Interfaces;

/// <summary>
/// RAGFlow 知识库检索服务接口
/// </summary>
public interface IRAGFlowService
{
    /// <summary>
    /// 从知识库检索相关内容
    /// </summary>
    /// <param name="request">检索请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检索响应</returns>
    Task<RAGFlowResponse> RetrieveAsync(
        RAGFlowRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从 Q2SQL 知识库检索示例
    /// </summary>
    /// <param name="question">用户问题</param>
    /// <param name="limit">返回数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检索响应</returns>
    Task<RAGFlowResponse> RetrieveQ2SQLExamplesAsync(
        string question,
        int limit = 8,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从 DDL 知识库检索表结构
    /// </summary>
    /// <param name="question">用户问题</param>
    /// <param name="limit">返回数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检索响应</returns>
    Task<RAGFlowResponse> RetrieveDDLSchemasAsync(
        string question,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从业务规则知识库检索规则
    /// </summary>
    /// <param name="question">用户问题</param>
    /// <param name="limit">返回数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检索响应</returns>
    Task<RAGFlowResponse> RetrieveBusinessRulesAsync(
        string question,
        int limit = 5,
        CancellationToken cancellationToken = default);
}
