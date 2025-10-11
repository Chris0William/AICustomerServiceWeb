using AICustomerServiceWeb2.Core.Models;

namespace AICustomerServiceWeb2.Core.Interfaces;

/// <summary>
/// 对话管理服务接口
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// 创建新对话
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话ID</returns>
    Task<string> CreateConversationAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取对话详情
    /// </summary>
    /// <param name="conversationId">对话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话信息</returns>
    Task<Conversation?> GetConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取对话消息列表
    /// </summary>
    /// <param name="conversationId">对话ID</param>
    /// <param name="limit">返回数量限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息列表</returns>
    Task<List<Message>> GetMessagesAsync(
        string conversationId,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存消息
    /// </summary>
    /// <param name="request">保存请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息ID</returns>
    Task<int> SaveMessageAsync(
        SaveMessageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有对话列表
    /// </summary>
    /// <param name="limit">返回数量限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话列表</returns>
    Task<List<Conversation>> GetAllConversationsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除对话
    /// </summary>
    /// <param name="conversationId">对话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新对话标题
    /// </summary>
    /// <param name="conversationId">对话ID</param>
    /// <param name="title">新标题</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateConversationTitleAsync(
        string conversationId,
        string title,
        CancellationToken cancellationToken = default);
}
