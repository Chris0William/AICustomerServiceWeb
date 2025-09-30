using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;
using AICustomerServiceWeb.Tools;
using AICustomerServiceWeb.Models;

namespace AICustomerServiceWeb.Services;

public class AIService
{
    private readonly Kernel _kernel;
    private readonly ConversationService _conversationService;
    private readonly ExecutionContext _executionContext;
    private readonly string _systemPrompt;
    private readonly int _maxTokens;
    private readonly double _temperature;

    public AIService(
        Kernel kernel,
        ConversationService conversationService,
        ExecutionContext executionContext,
        string systemPrompt,
        int maxTokens,
        double temperature)
    {
        _kernel = kernel;
        _conversationService = conversationService;
        _executionContext = executionContext;
        _systemPrompt = systemPrompt;
        _maxTokens = maxTokens;
        _temperature = temperature;
    }

    public async Task<(string response, int tokenCount, long messageId)> ChatAsync(string conversationId, string userMessage)
    {
        var chatHistory = new ChatHistory(_systemPrompt);

        // 获取历史消息，但限制数量防止上下文过长
        var messages = await _conversationService.GetMessages(conversationId);
        foreach (var msg in messages)
        {
            if (msg.Role == "user")
            {
                chatHistory.AddUserMessage(msg.Content);
            }
            else if (msg.Role == "assistant")
            {
                // 保留完整的历史消息，包括执行过程
                chatHistory.AddAssistantMessage(msg.Content);
            }
        }

        // ReAct模式：让AI自主决策，不再强制调用特定工具
        // AI会根据系统提示词中的ReAct workflow自行判断和调用工具
        chatHistory.AddUserMessage(userMessage);

        var userTokenCount = EstimateTokenCount(userMessage);
        var messageId = await _conversationService.SaveMessage(conversationId, "user", userMessage, userTokenCount, null);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = _maxTokens,
            Temperature = _temperature
        };

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var response = await chatService.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            _kernel);

        var answer = response.Content ?? "抱歉，我无法理解您的问题。";

        // 工具执行后才获取ExecutionContext状态
        Console.WriteLine("[AIService] ========== Checking ExecutionContext State ==========");
        var lastExecution = _executionContext.LastDatabaseExecution;
        Console.WriteLine($"[AIService] LastDatabaseExecution: {(lastExecution != null ? lastExecution.Length + " chars" : "NULL")}");

        if (!string.IsNullOrEmpty(lastExecution))
        {
            answer = lastExecution + "\n\n" + answer;
        }

        var assistantTokenCount = EstimateTokenCount(answer);

        // 在工具执行后获取ExecutionDetails
        var currentDetails = _executionContext.CurrentExecutionDetails;
        Console.WriteLine($"[AIService] CurrentExecutionDetails: {(currentDetails != null ? "Has Value" : "NULL")}");

        if (lastExecution != null && currentDetails == null)
        {
            Console.WriteLine("[AIService] WARNING: LastDatabaseExecution has value but CurrentExecutionDetails is NULL!");
            Console.WriteLine("[AIService] This indicates ExecutionDetails was not properly set or was cleared.");
        }

        // 直接从ExecutionContext获取ExecutionDetails
        string? executionDetailsJson = null;
        if (currentDetails != null)
        {
            executionDetailsJson = JsonSerializer.Serialize(currentDetails,
                new JsonSerializerOptions { WriteIndented = true });

            Console.WriteLine($"[AIService] ExecutionDetails to save: {executionDetailsJson.Length} characters");
            Console.WriteLine($"[AIService] RAGFlow steps count: {currentDetails.RAGFlowSteps.Count}");
        }
        else
        {
            Console.WriteLine("[AIService] No ExecutionDetails found in ExecutionContext");

            // 检测是否存在伪造的执行过程
            if (answer.Contains("🔍 **数据库查询执行过程**") ||
                answer.Contains("**步骤1**:") ||
                answer.Contains("**步骤4**: 执行SQL查询"))
            {
                Console.WriteLine("[AIService] ⚠️ WARNING: Response contains execution process but no ExecutionDetails!");
                Console.WriteLine("[AIService] This indicates LLM fabricated the execution process instead of calling DatabaseTool!");

                // 添加警告到响应
                answer = "⚠️ **警告**: 检测到异常响应，执行过程可能不准确。\n\n" + answer;
            }
        }

        var assistantMessageId = await _conversationService.SaveMessage(conversationId, "assistant", answer, assistantTokenCount, executionDetailsJson);

        // 清理上下文（可选，因为请求结束后会自动清理）
        _executionContext.Clear();

        if (messages.Count == 0)
        {
            var title = await GenerateTitle(userMessage);
            await _conversationService.UpdateConversationTitle(conversationId, title);
        }

        return (answer, userTokenCount + assistantTokenCount, assistantMessageId);
    }

    private async Task<string> GenerateTitle(string firstMessage)
    {
        var prompt = $"请用5个字以内总结这个问题：{firstMessage}\n只返回标题，不要其他内容。";
        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            var title = result.ToString().Trim().Trim('"', '\'');
            return string.IsNullOrEmpty(title) || title.Length > 50 ? firstMessage.Substring(0, Math.Min(20, firstMessage.Length)) : title;
        }
        catch
        {
            return firstMessage.Substring(0, Math.Min(20, firstMessage.Length));
        }
    }

    private bool ContainsDataQueryKeywords(string text)
    {
        // 数据查询关键词
        string[] dataKeywords = new[] {
            "多少", "几个", "数量", "统计", "总数",
            "查询", "显示", "列出", "查看", "获取",
            "用户", "部门", "设备", "任务", "班组",
            "人员", "承包商", "公司", "员工", "内部人员",
            "友商", "承包", "系统里", "数据库"
        };

        var lowerText = text.ToLower();
        foreach (var keyword in dataKeywords)
        {
            if (text.Contains(keyword))
            {
                return true;
            }
        }

        return false;
    }

    private int EstimateTokenCount(string text)
    {
        return text.Length / 2;
    }
}