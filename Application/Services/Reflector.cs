using AICustomerServiceWeb2.Core.Agent;
using AICustomerServiceWeb2.Core.Models;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using System.Text;

namespace AICustomerServiceWeb2.Application.Services;

/// <summary>
/// 反思器实现
/// </summary>
public class Reflector : IReflector
{
    private readonly Kernel _kernel;
    private readonly ILogger<Reflector> _logger;
    private const int MIN_CONFIDENCE_SCORE = 70; // 最低置信度阈值

    public Reflector(
        Kernel kernel,
        ILogger<Reflector> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<ReflectionResult> ReflectAsync(
        ExecutionPlan plan,
        ExecutionResult result,
        AgentRequest request,
        int currentRetryCount,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"[Reflector] 开始反思，重试次数：{currentRetryCount}/{request.MaxRetries}");

        var prompt = BuildReflectionPrompt(plan, result, request, currentRetryCount);

        try
        {
            var response = await _kernel.InvokePromptAsync(
                prompt,
                cancellationToken: cancellationToken);

            var reflectionJson = response.ToString();
            var reflection = ParseReflection(reflectionJson);
            reflection.CurrentRetryCount = currentRetryCount;

            // 决策逻辑
            if (result.Success && reflection.ConfidenceScore >= MIN_CONFIDENCE_SCORE)
            {
                reflection.ShouldContinue = true;
                reflection.NeedsPlanAdjustment = false;
                _logger.LogInformation($"[Reflector] 任务成功完成，置信度：{reflection.ConfidenceScore}%");
            }
            else if (currentRetryCount >= request.MaxRetries)
            {
                reflection.ShouldContinue = true; // 停止重试
                reflection.NeedsPlanAdjustment = false;
                _logger.LogWarning($"[Reflector] 达到最大重试次数，终止执行");
            }
            else
            {
                reflection.ShouldContinue = false; // 需要重试
                reflection.NeedsPlanAdjustment = true;
                _logger.LogInformation($"[Reflector] 需要调整计划并重试");
            }

            return reflection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Reflector] 反思失败");

            // 返回保守的反思结果
            return new ReflectionResult
            {
                ShouldContinue = currentRetryCount >= request.MaxRetries,
                Analysis = $"反思过程异常：{ex.Message}",
                IssuesIdentified = new List<string> { "反思器执行异常" },
                NeedsPlanAdjustment = false,
                ConfidenceScore = 0,
                CurrentRetryCount = currentRetryCount
            };
        }
    }

    private string BuildReflectionPrompt(
        ExecutionPlan plan,
        ExecutionResult result,
        AgentRequest request,
        int currentRetryCount)
    {
        var sb = new StringBuilder();

        sb.AppendLine("你是一个任务反思专家。请分析任务执行情况，判断是否成功完成，是否需要调整计划。");
        sb.AppendLine();
        sb.AppendLine("**用户请求：**");
        sb.AppendLine(request.UserMessage);
        sb.AppendLine();
        sb.AppendLine("**执行计划：**");
        sb.AppendLine($"分析：{plan.RequestAnalysis}");
        sb.AppendLine($"预期结果：{plan.ExpectedOutcome}");
        sb.AppendLine($"步骤数：{plan.Steps.Count}");
        sb.AppendLine();
        sb.AppendLine("**执行结果：**");
        sb.AppendLine($"整体成功：{result.Success}");
        sb.AppendLine($"执行耗时：{result.ExecutionTimeMs}ms");
        sb.AppendLine($"最终输出：{result.FinalOutput}");

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            sb.AppendLine($"错误信息：{result.ErrorMessage}");
        }

        sb.AppendLine();
        sb.AppendLine("**步骤执行详情：**");
        foreach (var stepOutput in result.StepOutputs)
        {
            sb.AppendLine($"- 步骤 {stepOutput.StepNumber} ({stepOutput.ToolName}):");
            sb.AppendLine($"  成功：{stepOutput.Success}");
            sb.AppendLine($"  输出：{stepOutput.ToolOutput?.Substring(0, Math.Min(200, stepOutput.ToolOutput?.Length ?? 0))}...");
            if (!string.IsNullOrEmpty(stepOutput.Error))
            {
                sb.AppendLine($"  错误：{stepOutput.Error}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"**当前重试次数：** {currentRetryCount}");
        sb.AppendLine();
        sb.AppendLine("**请按照以下JSON格式输出反思结果：**");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"analysis\": \"对执行结果的整体分析\",");
        sb.AppendLine("  \"issuesIdentified\": [\"问题1\", \"问题2\"],");
        sb.AppendLine("  \"improvements\": [\"改进建议1\", \"改进建议2\"],");
        sb.AppendLine("  \"confidenceScore\": 85");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**反思要求：**");
        sb.AppendLine("1. analysis: 分析任务是否真正完成了用户请求");
        sb.AppendLine("2. issuesIdentified: 列出发现的所有问题（如果有）");
        sb.AppendLine("3. improvements: 提出具体的改进建议");
        sb.AppendLine($"4. confidenceScore: 0-100的置信度评分（>={MIN_CONFIDENCE_SCORE}表示成功）");
        sb.AppendLine("5. 只输出JSON，不要包含其他解释性文字");

        return sb.ToString();
    }

    private ReflectionResult ParseReflection(string reflectionJson)
    {
        try
        {
            // 提取JSON内容
            var json = reflectionJson.Trim();
            if (json.Contains("```json"))
            {
                var startIndex = json.IndexOf("```json") + 7;
                var endIndex = json.LastIndexOf("```");
                json = json.Substring(startIndex, endIndex - startIndex).Trim();
            }
            else if (json.Contains("```"))
            {
                var startIndex = json.IndexOf("```") + 3;
                var endIndex = json.LastIndexOf("```");
                json = json.Substring(startIndex, endIndex - startIndex).Trim();
            }

            var reflectionData = JsonConvert.DeserializeObject<dynamic>(json);

            var reflection = new ReflectionResult
            {
                Analysis = reflectionData.analysis ?? "",
                ConfidenceScore = reflectionData.confidenceScore ?? 0
            };

            if (reflectionData.issuesIdentified != null)
            {
                foreach (var issue in reflectionData.issuesIdentified)
                {
                    reflection.IssuesIdentified.Add(issue.ToString());
                }
            }

            if (reflectionData.improvements != null)
            {
                foreach (var improvement in reflectionData.improvements)
                {
                    reflection.Improvements.Add(improvement.ToString());
                }
            }

            return reflection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[Reflector] 解析反思结果失败，原始内容：{reflectionJson}");

            return new ReflectionResult
            {
                Analysis = "反思结果解析失败",
                ConfidenceScore = 0,
                IssuesIdentified = new List<string> { "无法解析反思输出" }
            };
        }
    }
}
