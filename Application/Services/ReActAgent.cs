using AICustomerServiceWeb2.Core.Agent;
using AICustomerServiceWeb2.Core.Models;
using System.Diagnostics;

namespace AICustomerServiceWeb2.Application.Services;

/// <summary>
/// ReAct Agent实现 - 动态流程控制版本
/// </summary>
public class ReActAgent : IReActAgent
{
    private readonly IRequestClassifier _classifier;
    private readonly IPlanner _planner;
    private readonly IExecutor _executor;
    private readonly IReflector _reflector;
    private readonly ILogger<ReActAgent> _logger;

    public ReActAgent(
        IRequestClassifier classifier,
        IPlanner planner,
        IExecutor executor,
        IReflector reflector,
        ILogger<ReActAgent> logger)
    {
        _classifier = classifier;
        _planner = planner;
        _executor = executor;
        _reflector = reflector;
        _logger = logger;
    }

    public async Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ProcessStreamAsync(request, null, cancellationToken);
    }

    public async Task<AgentResponse> ProcessStreamAsync(
        AgentRequest request,
        Func<AgentState, object?, Task>? onStateChange,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"[ReActAgent] 开始处理请求：{request.UserMessage}");

        var stopwatch = Stopwatch.StartNew();
        var response = new AgentResponse
        {
            Thought = new AgentThought()
        };

        var currentState = AgentState.Idle;
        var retryCount = 0;

        try
        {
            // **新增**: 第一步 - 分类请求
            _logger.LogInformation("[ReActAgent] 开始分类请求");
            var classification = await _classifier.ClassifyAsync(
                request.UserMessage,
                context: null,
                cancellationToken);

            _logger.LogInformation(
                "[ReActAgent] 请求分类: {Type}, 策略: {Strategy}, 置信度: {Confidence:F2}",
                classification.Type, classification.Strategy, classification.Confidence);

            // 根据分类选择处理流程
            switch (classification.Strategy)
            {
                case ProcessingStrategy.DirectResponse:
                    return await HandleDirectResponse(request, classification, response, stopwatch, onStateChange);

                case ProcessingStrategy.SimplifiedFlow:
                    return await HandleSimplifiedFlow(request, response, stopwatch, onStateChange, cancellationToken);

                case ProcessingStrategy.FullFlow:
                default:
                    // 继续执行原有的完整流程
                    break;
            }

            // 完整流程: Planning → Executing → Reflecting
            // 状态：Idle → Planning
            await TransitionState(
                AgentState.Idle,
                AgentState.Planning,
                "开始规划任务",
                response,
                onStateChange);

            // Planning 阶段
            var plan = await _planner.CreatePlanAsync(request, cancellationToken);
            response.Thought.InitialPlan = plan;

            _logger.LogInformation($"[ReActAgent] 计划创建完成，包含 {plan.Steps.Count} 个步骤");

            // ReAct 循环：Execute → Reflect → (Adjust Plan) → Execute ...
            while (retryCount <= request.MaxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 状态：Planning → Executing
                await TransitionState(
                    AgentState.Planning,
                    AgentState.Executing,
                    $"开始执行计划（尝试 {retryCount + 1}/{request.MaxRetries + 1}）",
                    response,
                    onStateChange);

                // Executing 阶段
                var executionResult = await _executor.ExecutePlanAsync(plan, request, cancellationToken);
                response.Thought.ExecutionResult = executionResult;

                _logger.LogInformation(
                    $"[ReActAgent] 执行完成，成功：{executionResult.Success}，" +
                    $"耗时：{executionResult.ExecutionTimeMs}ms");

                // 推送执行结果（如果有回调）
                if (onStateChange != null)
                {
                    await onStateChange(AgentState.Executing, executionResult);
                }

                // 状态：Executing → Reflecting
                await TransitionState(
                    AgentState.Executing,
                    AgentState.Reflecting,
                    "分析执行结果",
                    response,
                    onStateChange);

                // Reflecting 阶段
                var reflection = await _reflector.ReflectAsync(
                    plan,
                    executionResult,
                    request,
                    retryCount,
                    cancellationToken);

                response.Thought.Reflections.Add(reflection);

                _logger.LogInformation(
                    $"[ReActAgent] 反思完成，置信度：{reflection.ConfidenceScore}%，" +
                    $"是否继续：{reflection.ShouldContinue}");

                // 推送反思结果
                if (onStateChange != null)
                {
                    await onStateChange(AgentState.Reflecting, reflection);
                }

                // 决策：成功 OR 失败 OR 重试
                if (reflection.ShouldContinue)
                {
                    // 成功或达到最大重试次数
                    if (executionResult.Success && reflection.ConfidenceScore >= 70)
                    {
                        await TransitionState(
                            AgentState.Reflecting,
                            AgentState.Succeeded,
                            "任务成功完成",
                            response,
                            onStateChange);

                        response.Success = true;
                        response.FinalState = AgentState.Succeeded;
                        response.Answer = GenerateSuccessAnswer(executionResult, reflection);
                    }
                    else
                    {
                        await TransitionState(
                            AgentState.Reflecting,
                            AgentState.Failed,
                            "任务失败",
                            response,
                            onStateChange);

                        response.Success = false;
                        response.FinalState = AgentState.Failed;
                        response.Answer = GenerateFailureAnswer(executionResult, reflection);
                        response.ErrorMessage = executionResult.ErrorMessage ?? "任务执行失败";
                    }

                    break;
                }
                else if (reflection.NeedsPlanAdjustment)
                {
                    // 需要调整计划并重试
                    retryCount++;
                    response.Thought.RetryCount = retryCount;

                    _logger.LogInformation($"[ReActAgent] 开始第 {retryCount} 次重试");

                    await TransitionState(
                        AgentState.Reflecting,
                        AgentState.Planning,
                        $"调整计划（重试 {retryCount}/{request.MaxRetries}）",
                        response,
                        onStateChange);

                    // 调整计划
                    plan = await _planner.AdjustPlanAsync(plan, executionResult, reflection, cancellationToken);

                    _logger.LogInformation($"[ReActAgent] 计划调整完成");
                }
                else
                {
                    // 不需要调整，直接重试
                    retryCount++;
                    response.Thought.RetryCount = retryCount;
                }
            }

            stopwatch.Stop();
            response.TotalTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                $"[ReActAgent] 请求处理完成，状态：{response.FinalState}，" +
                $"总耗时：{response.TotalTimeMs}ms");

            return response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[ReActAgent] 请求被取消");

            response.Success = false;
            response.FinalState = AgentState.Failed;
            response.ErrorMessage = "请求被取消";
            response.Answer = "任务执行被中断";

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ReActAgent] 请求处理异常");

            stopwatch.Stop();

            response.Success = false;
            response.FinalState = AgentState.Failed;
            response.ErrorMessage = $"系统错误：{ex.Message}";
            response.Answer = "抱歉，系统处理请求时发生错误";
            response.TotalTimeMs = stopwatch.ElapsedMilliseconds;

            return response;
        }
    }

    private async Task TransitionState(
        AgentState fromState,
        AgentState toState,
        string reason,
        AgentResponse response,
        Func<AgentState, object?, Task>? onStateChange)
    {
        var transition = new StateTransition
        {
            FromState = fromState,
            ToState = toState,
            Reason = reason
        };

        response.Thought.StateHistory.Add(transition);

        _logger.LogInformation($"[ReActAgent] 状态转换：{fromState} → {toState}，原因：{reason}");

        // 推送状态变化
        if (onStateChange != null)
        {
            await onStateChange(toState, transition);
        }
    }

    private string GenerateSuccessAnswer(ExecutionResult result, ReflectionResult reflection)
    {
        // 对于会话型请求(0步骤计划),FinalOutput 为空,使用反思分析作为主要回复
        if (string.IsNullOrEmpty(result.FinalOutput))
        {
            // 会话型请求,返回友好的回复
            return "你好!我是AI智能客服助手,很高兴为您服务。有什么我可以帮助您的吗?";
        }

        // 对于任务型请求,返回执行结果
        return $"{result.FinalOutput}\n\n**分析：** {reflection.Analysis}";
    }

    private string GenerateFailureAnswer(ExecutionResult result, ReflectionResult reflection)
    {
        var answer = $"抱歉，未能完成您的请求。\n\n";

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            answer += $"**错误信息：** {result.ErrorMessage}\n\n";
        }

        if (reflection.IssuesIdentified.Any())
        {
            answer += $"**发现的问题：**\n";
            foreach (var issue in reflection.IssuesIdentified)
            {
                answer += $"- {issue}\n";
            }
        }

        return answer;
    }

    /// <summary>
    /// 处理直接响应(会话型请求) - 最快路径
    /// </summary>
    private async Task<AgentResponse> HandleDirectResponse(
        AgentRequest request,
        RequestClassification classification,
        AgentResponse response,
        Stopwatch stopwatch,
        Func<AgentState, object?, Task>? onStateChange)
    {
        _logger.LogInformation("[ReActAgent] 使用直接响应策略");

        await TransitionState(
            AgentState.Idle,
            AgentState.Succeeded,
            "会话型请求,直接响应",
            response,
            onStateChange);

        stopwatch.Stop();

        response.Success = true;
        response.FinalState = AgentState.Succeeded;
        response.TotalTimeMs = stopwatch.ElapsedMilliseconds;

        // 根据意图生成友好回复
        response.Answer = classification.Intent switch
        {
            "greeting_or_chat" => "你好!我是AI智能客服助手,很高兴为您服务。\n\n我可以帮您:\n- 查询业务数据\n- 统计分析报表\n- 回答业务问题\n\n请问有什么可以帮到您?",
            "question_or_chat" => "我是基于 RAGFlow 和 Semantic Kernel 构建的智能客服系统,可以通过自然语言理解您的需求并查询数据库。有什么我可以帮您的吗?",
            _ => "你好!有什么我可以帮助您的吗?"
        };

        _logger.LogInformation("[ReActAgent] 直接响应完成,耗时: {Ms}ms", response.TotalTimeMs);
        return response;
    }

    /// <summary>
    /// 处理简化流程(简单查询) - Planning + Executing,跳过 Reflecting
    /// </summary>
    private async Task<AgentResponse> HandleSimplifiedFlow(
        AgentRequest request,
        AgentResponse response,
        Stopwatch stopwatch,
        Func<AgentState, object?, Task>? onStateChange,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ReActAgent] 使用简化流程策略");

        // Planning
        await TransitionState(
            AgentState.Idle,
            AgentState.Planning,
            "规划查询任务",
            response,
            onStateChange);

        var plan = await _planner.CreatePlanAsync(request, cancellationToken);
        response.Thought.InitialPlan = plan;

        // Executing
        await TransitionState(
            AgentState.Planning,
            AgentState.Executing,
            "执行查询计划",
            response,
            onStateChange);

        var executionResult = await _executor.ExecutePlanAsync(plan, request, cancellationToken);
        response.Thought.ExecutionResult = executionResult;

        if (onStateChange != null)
        {
            await onStateChange(AgentState.Executing, executionResult);
        }

        // 直接判断成功/失败,跳过 Reflecting
        stopwatch.Stop();
        response.TotalTimeMs = stopwatch.ElapsedMilliseconds;

        if (executionResult.Success)
        {
            await TransitionState(
                AgentState.Executing,
                AgentState.Succeeded,
                "查询成功完成",
                response,
                onStateChange);

            response.Success = true;
            response.FinalState = AgentState.Succeeded;
            response.Answer = executionResult.FinalOutput;
        }
        else
        {
            await TransitionState(
                AgentState.Executing,
                AgentState.Failed,
                "查询执行失败",
                response,
                onStateChange);

            response.Success = false;
            response.FinalState = AgentState.Failed;
            response.Answer = $"抱歉,查询失败。\n\n{executionResult.ErrorMessage}";
            response.ErrorMessage = executionResult.ErrorMessage;
        }

        _logger.LogInformation("[ReActAgent] 简化流程完成,耗时: {Ms}ms", response.TotalTimeMs);
        return response;
    }
}
