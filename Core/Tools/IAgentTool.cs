using AICustomerServiceWeb2.Core.Models;

namespace AICustomerServiceWeb2.Core.Tools;

/// <summary>
/// Agent工具接口
/// </summary>
public interface IAgentTool
{
    /// <summary>
    /// 工具名称（唯一标识）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 工具描述（用于LLM理解）
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 工具参数Schema（JSON Schema格式）
    /// </summary>
    string ParametersSchema { get; }

    /// <summary>
    /// 执行工具
    /// </summary>
    /// <param name="parameters">工具参数（JSON格式）</param>
    /// <param name="context">执行上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工具执行结果</returns>
    Task<ToolResult> ExecuteAsync(
        string parameters,
        ToolContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证参数是否有效
    /// </summary>
    /// <param name="parameters">工具参数（JSON格式）</param>
    /// <returns>验证结果</returns>
    Task<ValidationResult> ValidateParametersAsync(string parameters);
}

/// <summary>
/// 工具执行结果
/// </summary>
public class ToolResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 输出内容
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 元数据（如检索详情、SQL等）
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ToolResult CreateSuccess(string output, Dictionary<string, object>? metadata = null)
    {
        return new ToolResult
        {
            Success = true,
            Output = output,
            Metadata = metadata ?? new()
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ToolResult CreateFailure(string error)
    {
        return new ToolResult
        {
            Success = false,
            Error = error
        };
    }
}

/// <summary>
/// 工具执行上下文
/// </summary>
public class ToolContext
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// 用户消息
    /// </summary>
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>
    /// 当前步骤序号
    /// </summary>
    public int CurrentStepNumber { get; set; }

    /// <summary>
    /// 历史执行结果（前序步骤）
    /// </summary>
    public Dictionary<int, StepOutput> PreviousStepOutputs { get; set; } = new();

    /// <summary>
    /// 额外上下文数据
    /// </summary>
    public Dictionary<string, object> AdditionalContext { get; set; } = new();
}

/// <summary>
/// 参数验证结果
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 验证错误列表
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 创建有效结果
    /// </summary>
    public static ValidationResult Valid()
    {
        return new ValidationResult { IsValid = true };
    }

    /// <summary>
    /// 创建无效结果
    /// </summary>
    public static ValidationResult Invalid(params string[] errors)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = errors.ToList()
        };
    }
}
