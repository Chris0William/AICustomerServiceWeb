using AICustomerServiceWeb2.Core.Agent;
using AICustomerServiceWeb2.Core.Models;
using AICustomerServiceWeb2.Core.Tools;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using System.Text;

namespace AICustomerServiceWeb2.Application.Services;

/// <summary>
/// 任务规划器实现
/// </summary>
public class Planner : IPlanner
{
    private readonly Kernel _kernel;
    private readonly IEnumerable<IAgentTool> _tools;
    private readonly ILogger<Planner> _logger;

    public Planner(
        Kernel kernel,
        IEnumerable<IAgentTool> tools,
        ILogger<Planner> logger)
    {
        _kernel = kernel;
        _tools = tools;
        _logger = logger;
    }

    public async Task<ExecutionPlan> CreatePlanAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"[Planner] 开始规划任务：{request.UserMessage}");

        var prompt = BuildPlanningPrompt(request, isRetry: false);

        try
        {
            var response = await _kernel.InvokePromptAsync(
                prompt,
                cancellationToken: cancellationToken);

            var planJson = response.ToString();
            var plan = ParsePlan(planJson);

            _logger.LogInformation($"[Planner] 计划创建成功，包含 {plan.Steps.Count} 个步骤");

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Planner] 计划创建失败");
            throw;
        }
    }

    public async Task<ExecutionPlan> AdjustPlanAsync(
        ExecutionPlan originalPlan,
        ExecutionResult executionResult,
        ReflectionResult reflection,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"[Planner] 开始调整计划，原因：{reflection.Analysis}");

        var prompt = BuildAdjustmentPrompt(originalPlan, executionResult, reflection);

        try
        {
            var response = await _kernel.InvokePromptAsync(
                prompt,
                cancellationToken: cancellationToken);

            var planJson = response.ToString();
            var adjustedPlan = ParsePlan(planJson);
            adjustedPlan.IsRetry = true;
            adjustedPlan.RetryReason = reflection.Analysis;

            _logger.LogInformation($"[Planner] 计划调整成功，包含 {adjustedPlan.Steps.Count} 个步骤");

            return adjustedPlan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Planner] 计划调整失败");
            throw;
        }
    }

    private string BuildPlanningPrompt(AgentRequest request, bool isRetry)
    {
        var sb = new StringBuilder();

        sb.AppendLine("你是一个任务规划专家。请分析用户请求，制定详细的执行计划。");
        sb.AppendLine();
        sb.AppendLine("**用户请求：**");
        sb.AppendLine(request.UserMessage);
        sb.AppendLine();
        sb.AppendLine("**可用工具：**");

        foreach (var tool in _tools)
        {
            sb.AppendLine($"- {tool.Name}: {tool.Description}");
            sb.AppendLine($"  参数Schema: {tool.ParametersSchema}");
        }

        sb.AppendLine();
        sb.AppendLine("**请按照以下JSON格式输出执行计划：**");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"requestAnalysis\": \"对请求的分析\",");
        sb.AppendLine("  \"steps\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"stepNumber\": 1,");
        sb.AppendLine("      \"description\": \"步骤描述\",");
        sb.AppendLine("      \"toolName\": \"工具名称\",");
        sb.AppendLine("      \"toolParameters\": \"{\\\"param1\\\": \\\"value1\\\"}\",");
        sb.AppendLine("      \"dependencies\": []");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"expectedOutcome\": \"预期结果描述\"");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**规划要求：**");
        sb.AppendLine("1. 步骤要具体明确，按执行顺序编号");
        sb.AppendLine("2. 如果步骤之间有依赖关系，在dependencies中标注");
        sb.AppendLine("3. toolParameters必须是有效的JSON字符串");
        sb.AppendLine("4. 对于数据库查询请求，需要先检索知识库再生成SQL");
        sb.AppendLine("5. 只输出JSON，不要包含其他解释性文字");

        return sb.ToString();
    }

    private string BuildAdjustmentPrompt(
        ExecutionPlan originalPlan,
        ExecutionResult executionResult,
        ReflectionResult reflection)
    {
        var sb = new StringBuilder();

        sb.AppendLine("你是一个任务规划专家。根据执行失败的结果和反思分析，调整执行计划。");
        sb.AppendLine();
        sb.AppendLine("**原始计划：**");
        sb.AppendLine(JsonConvert.SerializeObject(originalPlan, Formatting.Indented));
        sb.AppendLine();
        sb.AppendLine("**执行结果：**");
        sb.AppendLine($"成功: {executionResult.Success}");
        sb.AppendLine($"错误: {executionResult.ErrorMessage}");
        sb.AppendLine();
        sb.AppendLine("**反思分析：**");
        sb.AppendLine(reflection.Analysis);
        sb.AppendLine();
        sb.AppendLine("**识别的问题：**");
        foreach (var issue in reflection.IssuesIdentified)
        {
            sb.AppendLine($"- {issue}");
        }
        sb.AppendLine();
        sb.AppendLine("**改进建议：**");
        foreach (var improvement in reflection.Improvements)
        {
            sb.AppendLine($"- {improvement}");
        }
        sb.AppendLine();
        sb.AppendLine("**请输出调整后的执行计划（JSON格式，同原始格式）：**");

        return sb.ToString();
    }

    private ExecutionPlan ParsePlan(string planJson)
    {
        try
        {
            // 提取JSON内容（去除markdown代码块标记）
            var json = planJson.Trim();
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

            var planData = JsonConvert.DeserializeObject<dynamic>(json);

            var plan = new ExecutionPlan
            {
                RequestAnalysis = planData.requestAnalysis ?? planData.RequestAnalysis ?? "",
                ExpectedOutcome = planData.expectedOutcome ?? planData.ExpectedOutcome ?? ""
            };

            // 兼容小写 steps 和大写 Steps
            var stepsArray = planData.steps ?? planData.Steps;

            if (stepsArray == null)
            {
                _logger.LogInformation("[Planner] 计划不包含任何步骤，返回空计划");
                return plan;
            }

            foreach (var step in stepsArray)
            {
                // 兼容不同的字段命名格式
                int stepNumber;
                if (step.stepNumber != null)
                    stepNumber = (int)step.stepNumber;
                else if (step.StepNumber != null)
                    stepNumber = (int)step.StepNumber;
                else if (step.StepId != null)
                    stepNumber = (int)step.StepId;
                else
                    stepNumber = plan.Steps.Count + 1;

                string description = step.description ?? step.Description ?? "";
                string toolName = step.toolName ?? step.ToolName ?? step.Action ?? "";

                // toolParameters 可能是对象或字符串
                string toolParameters = "{}";
                var parameters = step.toolParameters ?? step.ToolParameters ?? step.Parameters;
                if (parameters != null)
                {
                    if (parameters is string)
                        toolParameters = parameters;
                    else
                        toolParameters = JsonConvert.SerializeObject(parameters);
                }

                var executionStep = new ExecutionStep
                {
                    StepNumber = stepNumber,
                    Description = description,
                    ToolName = toolName,
                    ToolParameters = toolParameters,
                };

                var dependencies = step.dependencies ?? step.Dependencies;
                if (dependencies != null)
                {
                    foreach (var dep in dependencies)
                    {
                        executionStep.Dependencies.Add((int)dep);
                    }
                }

                plan.Steps.Add(executionStep);
            }

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[Planner] 解析计划失败，原始内容：{planJson}");
            throw new InvalidOperationException("无法解析执行计划", ex);
        }
    }
}
