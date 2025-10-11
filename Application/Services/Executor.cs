using AICustomerServiceWeb2.Core.Agent;
using AICustomerServiceWeb2.Core.Models;
using AICustomerServiceWeb2.Core.Tools;
using System.Diagnostics;

namespace AICustomerServiceWeb2.Application.Services;

/// <summary>
/// 执行器实现
/// </summary>
public class Executor : IExecutor
{
    private readonly IEnumerable<IAgentTool> _tools;
    private readonly ILogger<Executor> _logger;
    private readonly Dictionary<string, IAgentTool> _toolRegistry;

    public Executor(
        IEnumerable<IAgentTool> tools,
        ILogger<Executor> logger)
    {
        _tools = tools;
        _logger = logger;
        _toolRegistry = tools.ToDictionary(t => t.Name, t => t);
    }

    public async Task<ExecutionResult> ExecutePlanAsync(
        ExecutionPlan plan,
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"[Executor] 开始执行计划：{plan.PlanId}，包含 {plan.Steps.Count} 个步骤");

        var result = new ExecutionResult
        {
            Plan = plan,
            StartTime = DateTime.Now
        };

        var stopwatch = Stopwatch.StartNew();
        var executedSteps = new Dictionary<int, StepOutput>();

        try
        {
            // 按步骤顺序执行
            foreach (var step in plan.Steps.OrderBy(s => s.StepNumber))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 检查依赖是否已完成
                if (!CheckDependencies(step, executedSteps))
                {
                    _logger.LogWarning($"[Executor] 步骤 {step.StepNumber} 的依赖未满足，跳过执行");
                    step.Status = StepStatus.Skipped;
                    continue;
                }

                // 执行步骤
                var stepOutput = await ExecuteStepAsync(step, request, executedSteps, cancellationToken);
                executedSteps[step.StepNumber] = stepOutput;
                result.StepOutputs.Add(stepOutput);

                // 更新步骤状态
                step.Status = stepOutput.Success ? StepStatus.Succeeded : StepStatus.Failed;
                step.Result = stepOutput.ToolOutput;
                step.Error = stepOutput.Error;

                _logger.LogInformation(
                    $"[Executor] 步骤 {step.StepNumber} 执行{(stepOutput.Success ? "成功" : "失败")}，" +
                    $"耗时 {stepOutput.ExecutionTimeMs}ms");

                // 如果步骤失败，记录但继续执行（由Reflector决定是否重试）
                if (!stepOutput.Success)
                {
                    _logger.LogWarning($"[Executor] 步骤 {step.StepNumber} 执行失败：{stepOutput.Error}");
                }
            }

            stopwatch.Stop();

            // 判断整体是否成功（至少有一个步骤成功）
            var hasSuccess = result.StepOutputs.Any(s => s.Success);
            result.Success = hasSuccess;
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.EndTime = DateTime.Now;

            // 生成最终输出
            result.FinalOutput = GenerateFinalOutput(result.StepOutputs);

            if (!result.Success)
            {
                result.ErrorMessage = "所有步骤执行失败";
            }

            _logger.LogInformation($"[Executor] 计划执行完成，总耗时 {result.ExecutionTimeMs}ms");

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "[Executor] 计划执行异常");

            result.Success = false;
            result.ErrorMessage = $"执行异常：{ex.Message}";
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.EndTime = DateTime.Now;

            return result;
        }
    }

    private async Task<StepOutput> ExecuteStepAsync(
        ExecutionStep step,
        AgentRequest request,
        Dictionary<int, StepOutput> previousSteps,
        CancellationToken cancellationToken)
    {
        var stepStopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation($"[Executor] 开始执行步骤 {step.StepNumber}: {step.Description}");

            // 查找工具
            if (!_toolRegistry.TryGetValue(step.ToolName, out var tool))
            {
                return new StepOutput
                {
                    StepNumber = step.StepNumber,
                    ToolName = step.ToolName,
                    ToolInput = step.ToolParameters,
                    Success = false,
                    Error = $"工具 '{step.ToolName}' 未找到",
                    ExecutionTimeMs = stepStopwatch.ElapsedMilliseconds
                };
            }

            // 验证参数
            var validation = await tool.ValidateParametersAsync(step.ToolParameters);
            if (!validation.IsValid)
            {
                return new StepOutput
                {
                    StepNumber = step.StepNumber,
                    ToolName = step.ToolName,
                    ToolInput = step.ToolParameters,
                    Success = false,
                    Error = $"参数验证失败：{string.Join(", ", validation.Errors)}",
                    ExecutionTimeMs = stepStopwatch.ElapsedMilliseconds
                };
            }

            // 构建工具上下文
            var context = new ToolContext
            {
                ConversationId = request.ConversationId,
                UserMessage = request.UserMessage,
                CurrentStepNumber = step.StepNumber,
                PreviousStepOutputs = previousSteps
            };

            // 执行工具
            step.Status = StepStatus.Running;
            var toolResult = await tool.ExecuteAsync(step.ToolParameters, context, cancellationToken);

            stepStopwatch.Stop();

            return new StepOutput
            {
                StepNumber = step.StepNumber,
                ToolName = step.ToolName,
                ToolInput = step.ToolParameters,
                ToolOutput = toolResult.Output,
                Success = toolResult.Success,
                Error = toolResult.Error,
                ExecutionTimeMs = stepStopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stepStopwatch.Stop();

            _logger.LogError(ex, $"[Executor] 步骤 {step.StepNumber} 执行异常");

            return new StepOutput
            {
                StepNumber = step.StepNumber,
                ToolName = step.ToolName,
                ToolInput = step.ToolParameters,
                Success = false,
                Error = $"执行异常：{ex.Message}",
                ExecutionTimeMs = stepStopwatch.ElapsedMilliseconds
            };
        }
    }

    private bool CheckDependencies(ExecutionStep step, Dictionary<int, StepOutput> executedSteps)
    {
        if (step.Dependencies == null || step.Dependencies.Count == 0)
        {
            return true;
        }

        foreach (var depStepNumber in step.Dependencies)
        {
            if (!executedSteps.ContainsKey(depStepNumber))
            {
                _logger.LogWarning($"[Executor] 依赖步骤 {depStepNumber} 尚未执行");
                return false;
            }

            if (!executedSteps[depStepNumber].Success)
            {
                _logger.LogWarning($"[Executor] 依赖步骤 {depStepNumber} 执行失败");
                return false;
            }
        }

        return true;
    }

    private string GenerateFinalOutput(List<StepOutput> stepOutputs)
    {
        if (!stepOutputs.Any())
        {
            return "未执行任何步骤";
        }

        var successfulSteps = stepOutputs.Where(s => s.Success).ToList();

        if (!successfulSteps.Any())
        {
            return "所有步骤执行失败";
        }

        // 整合所有成功步骤的输出
        return string.Join("\n\n", successfulSteps.Select(s =>
            $"**步骤 {s.StepNumber} ({s.ToolName}) 结果：**\n{s.ToolOutput}"));
    }
}
