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
    private readonly string _systemPrompt;
    private readonly int _maxTokens;
    private readonly double _temperature;

    public AIService(
        Kernel kernel,
        ConversationService conversationService,
        string systemPrompt,
        int maxTokens,
        double temperature)
    {
        _kernel = kernel;
        _conversationService = conversationService;
        _systemPrompt = systemPrompt;
        _maxTokens = maxTokens;
        _temperature = temperature;
    }

    public async Task<(string response, int tokenCount, long messageId)> ChatAsync(string conversationId, string userMessage)
    {
        var chatHistory = new ChatHistory(_systemPrompt);

        var messages = await _conversationService.GetMessages(conversationId);
        foreach (var msg in messages)
        {
            if (msg.Role == "user")
                chatHistory.AddUserMessage(msg.Content);
            else if (msg.Role == "assistant")
                chatHistory.AddAssistantMessage(msg.Content);
        }

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

        // 诊断：同时检查两个值
        Console.WriteLine("[AIService] ========== Checking ExecutionContext State ==========");
        var lastExecution = ExecutionContext.LastDatabaseExecution;
        var currentDetails = ExecutionContext.CurrentExecutionDetails;
        Console.WriteLine($"[AIService] LastDatabaseExecution: {(lastExecution != null ? lastExecution.Length + " chars" : "NULL")}");
        Console.WriteLine($"[AIService] CurrentExecutionDetails: {(currentDetails != null ? "Has Value" : "NULL")}");

        if (lastExecution != null && currentDetails == null)
        {
            Console.WriteLine("[AIService] WARNING: LastDatabaseExecution has value but CurrentExecutionDetails is NULL!");
            Console.WriteLine("[AIService] This indicates ExecutionDetails was not properly set or was cleared.");
        }

        if (!string.IsNullOrEmpty(lastExecution))
        {
            answer = lastExecution + "\n\n" + answer;
        }

        var assistantTokenCount = EstimateTokenCount(answer);

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
        }

        var assistantMessageId = await _conversationService.SaveMessage(conversationId, "assistant", answer, assistantTokenCount, executionDetailsJson);

        // 清理上下文
        ExecutionContext.Clear();

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

    private int EstimateTokenCount(string text)
    {
        return text.Length / 2;
    }
}