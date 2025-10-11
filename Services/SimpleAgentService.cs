using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MySql.Data.MySqlClient;
using AICustomerServiceWeb.Models;

namespace AICustomerServiceWeb.Services;

/// <summary>
/// 简化版Agent服务 - 专注于准确执行和清晰响应
/// </summary>
public class SimpleAgentService
{
    private readonly Kernel _kernel;
    private readonly RAGFlowService _ragflow;
    private readonly ConversationService _conversationService;
    private readonly string _connectionString;
    private readonly ILogger<SimpleAgentService> _logger;

    public SimpleAgentService(
        Kernel kernel,
        RAGFlowService ragflow,
        ConversationService conversationService,
        IConfiguration configuration,
        ILogger<SimpleAgentService> logger)
    {
        _kernel = kernel;
        _ragflow = ragflow;
        _conversationService = conversationService;
        _connectionString = configuration.GetConnectionString("Production") ?? "";
        _logger = logger;
    }

    /// <summary>
    /// 处理用户消息 - 简单直接的处理流程
    /// </summary>
    public async Task<ChatResponse> ProcessMessageAsync(string conversationId, string userMessage)
    {
        return await ProcessMessageAsync(conversationId, userMessage, "qwen-plus");
    }

    /// <summary>
    /// 处理用户消息 - 支持模型选择
    /// </summary>
    public async Task<ChatResponse> ProcessMessageAsync(string conversationId, string userMessage, string modelId)
    {
        _logger.LogInformation($"[SimpleAgent] 处理消息: {userMessage} (模型: {modelId})");

        var response = new ChatResponse();
        var executionLog = new StringBuilder();

        try
        {
            // Step 1: 保存用户消息
            await _conversationService.SaveMessage(conversationId, "user", userMessage, EstimateTokenCount(userMessage), null);

            // Step 2: 判断是否需要查询数据库
            if (NeedsDatabaseQuery(userMessage))
            {
                _logger.LogInformation("[SimpleAgent] 识别为数据库查询");
                executionLog.AppendLine("🔍 **执行数据库查询**\n");

                // Step 3: 获取RAG上下文
                executionLog.AppendLine("**步骤1**: 检索相关信息");

                var (q2sql, q2sqlDetails) = await _ragflow.RetrieveQ2SQLExamplesWithDetails(userMessage, 5);
                executionLog.AppendLine($"  ✅ 找到 {q2sqlDetails?.RetrievedItems?.Count ?? 0} 个相似查询示例");

                var (ddl, ddlDetails) = await _ragflow.RetrieveDDLWithDetails(userMessage, 8);
                executionLog.AppendLine($"  ✅ 找到 {ddlDetails?.RetrievedItems?.Count ?? 0} 个相关表结构");

                // Step 4: 生成SQL
                executionLog.AppendLine("\n**步骤2**: 生成SQL语句");
                var sql = await GenerateSQL(userMessage, q2sql, ddl);
                executionLog.AppendLine($"```sql\n{sql}\n```");

                // Step 5: 执行SQL（关键！）
                executionLog.AppendLine("\n**步骤3**: 执行查询");
                var queryResult = await ExecuteSQL(sql);

                if (queryResult.Success)
                {
                    executionLog.AppendLine($"✅ 查询成功，返回 {queryResult.RowCount} 条记录\n");

                    // Step 6: 生成自然语言回答
                    var answer = await GenerateAnswer(userMessage, sql, queryResult.Data);

                    // 组合最终响应
                    response.ExecutionProcess = executionLog.ToString();
                    response.Answer = answer;
                    response.QueryResult = queryResult.Data;
                    response.TokenCount = EstimateTokenCount(answer);

                    // 保存执行详情
                    var executionDetails = new
                    {
                        Type = "DatabaseQuery",
                        SQL = sql,
                        RowCount = queryResult.RowCount,
                        Success = true,
                        RAGFlow = new
                        {
                            Q2SQLCount = q2sqlDetails?.RetrievedItems?.Count ?? 0,
                            DDLCount = ddlDetails?.RetrievedItems?.Count ?? 0
                        }
                    };

                    await _conversationService.SaveMessage(
                        conversationId,
                        "assistant",
                        response.ExecutionProcess + "\n" + response.Answer,
                        EstimateTokenCount(response.Answer),
                        JsonSerializer.Serialize(executionDetails));
                }
                else
                {
                    executionLog.AppendLine($"❌ 查询失败: {queryResult.Error}");
                    response.ExecutionProcess = executionLog.ToString();
                    response.Answer = $"抱歉，查询失败了。\n\n错误信息：{queryResult.Error}\n\n请检查问题描述或联系管理员。";

                    await _conversationService.SaveMessage(
                        conversationId,
                        "assistant",
                        response.Answer,
                        EstimateTokenCount(response.Answer),
                        null);
                }
            }
            else
            {
                // 普通对话
                _logger.LogInformation("[SimpleAgent] 识别为普通对话");

                var answer = await GenerateConversationalResponse(conversationId, userMessage);
                response.Answer = answer;

                await _conversationService.SaveMessage(
                    conversationId,
                    "assistant",
                    answer,
                    EstimateTokenCount(answer),
                    null);
            }

            response.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SimpleAgent] 处理失败");
            response.Success = false;
            response.Answer = $"处理您的请求时出现了错误：{ex.Message}";
        }

        return response;
    }

    /// <summary>
    /// 判断是否需要查询数据库
    /// </summary>
    private bool NeedsDatabaseQuery(string message)
    {
        var keywords = new[]
        {
            "多少", "几个", "数量", "统计", "查询", "列出", "显示",
            "用户", "员工", "部门", "设备", "任务", "公司", "承包商",
            "有什么", "有哪些", "都有谁", "列表", "清单"
        };

        var lowerMessage = message.ToLower();
        return keywords.Any(k => message.Contains(k));
    }

    /// <summary>
    /// 生成SQL - 简洁直接的提示词
    /// </summary>
    private async Task<string> GenerateSQL(string question, string examples, string ddl)
    {
        var prompt = $@"你是MySQL专家。根据用户问题生成SQL。

参考示例：
{examples}

表结构：
{ddl}

用户问题：{question}

重要规则：
1. 使用反引号包裹表名和字段名
2. 加上 IsDeleted=0 条件
3. 默认 LIMIT 20
4. 只返回SQL，不要解释

SQL：";

        var response = await _kernel.InvokePromptAsync(prompt);
        var sql = response.ToString().Trim();

        // 清理SQL
        if (sql.StartsWith("```sql"))
        {
            sql = sql.Substring(6);
        }
        if (sql.EndsWith("```"))
        {
            sql = sql.Substring(0, sql.Length - 3);
        }

        return sql.Trim();
    }

    /// <summary>
    /// 执行SQL - 真正执行！
    /// </summary>
    private async Task<QueryResult> ExecuteSQL(string sql)
    {
        var result = new QueryResult();

        try
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var data = new StringBuilder();
            var columns = new List<string>();

            // 获取列名
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            data.AppendLine("| " + string.Join(" | ", columns) + " |");
            data.AppendLine("|" + string.Join("|", columns.Select(_ => "---")) + "|");

            // 读取数据
            int rowCount = 0;
            while (await reader.ReadAsync() && rowCount < 20)
            {
                var values = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    values.Add(value?.ToString() ?? "NULL");
                }
                data.AppendLine("| " + string.Join(" | ", values) + " |");
                rowCount++;
            }

            result.Success = true;
            result.Data = data.ToString();
            result.RowCount = rowCount;

            _logger.LogInformation($"[SimpleAgent] SQL执行成功，返回 {rowCount} 行");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "[SimpleAgent] SQL执行失败");
        }

        return result;
    }

    /// <summary>
    /// 生成自然语言答案 - 简洁直接
    /// </summary>
    private async Task<string> GenerateAnswer(string question, string sql, string queryResult)
    {
        var prompt = $@"根据数据库查询结果回答用户问题。要求简洁、准确、直接。

用户问题：{question}

查询结果：
{queryResult}

请用1-2句话直接回答用户的问题。如果是数量统计，直接说数字。
不要说废话，不要写报告，就像人工客服一样简单直接地回答。

回答：";

        var response = await _kernel.InvokePromptAsync(prompt);
        return response.ToString();
    }

    /// <summary>
    /// 生成普通对话响应
    /// </summary>
    private async Task<string> GenerateConversationalResponse(string conversationId, string message)
    {
        var chatHistory = new ChatHistory("你是一个智能客服助手。请用简洁、友好的方式回答用户问题。");

        // 添加历史消息
        var messages = await _conversationService.GetMessages(conversationId);
        var recentMessages = messages.TakeLast(6).ToList();

        foreach (var msg in recentMessages)
        {
            if (msg.Role == "user")
                chatHistory.AddUserMessage(msg.Content);
            else
                chatHistory.AddAssistantMessage(CleanContent(msg.Content));
        }

        chatHistory.AddUserMessage(message);

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var response = await chatService.GetChatMessageContentAsync(chatHistory);

        return response.Content ?? "抱歉，我没有理解您的问题。";
    }

    private string CleanContent(string content)
    {
        // 移除执行过程，只保留答案
        if (content.Contains("**执行数据库查询**"))
        {
            var idx = content.LastIndexOf("\n\n");
            if (idx > 0)
            {
                return content.Substring(idx + 2);
            }
        }
        return content;
    }

    private int EstimateTokenCount(string text)
    {
        return text.Length / 2;
    }

    private class QueryResult
    {
        public bool Success { get; set; }
        public string Data { get; set; } = "";
        public int RowCount { get; set; }
        public string? Error { get; set; }
    }
}

public class ChatResponse
{
    public bool Success { get; set; }
    public string? ExecutionProcess { get; set; }
    public string Answer { get; set; } = "";
    public string? QueryResult { get; set; }
    public int TokenCount { get; set; }
    public string? ErrorMessage { get; set; }
}