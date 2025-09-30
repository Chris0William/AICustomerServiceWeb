using AICustomerServiceWeb.Models;
using Microsoft.AspNetCore.Http;

namespace AICustomerServiceWeb.Services;

/// <summary>
/// 执行上下文，使用HttpContext.Items存储请求级别的数据
/// </summary>
public class ExecutionContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string LastExecutionKey = "LastDatabaseExecution";
    private const string ExecutionDetailsKey = "CurrentExecutionDetails";

    public ExecutionContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? LastDatabaseExecution
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            var value = context.Items[LastExecutionKey] as string;
            Console.WriteLine($"[ExecutionContext] Getting LastDatabaseExecution: {(value != null ? "Has Value" : "NULL")}");
            return value;
        }
        set
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            Console.WriteLine($"[ExecutionContext] Setting LastDatabaseExecution: {(value != null ? value.Length + " chars" : "NULL")}");
            context.Items[LastExecutionKey] = value;
        }
    }

    public ExecutionDetails? CurrentExecutionDetails
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            var value = context.Items[ExecutionDetailsKey] as ExecutionDetails;
            // 注释掉频繁的日志输出，避免性能问题
            // Console.WriteLine($"[ExecutionContext] Getting CurrentExecutionDetails: {(value != null ? "Has Value" : "NULL")}");
            // if (value != null)
            // {
            //     Console.WriteLine($"[ExecutionContext]   - RAGFlow steps: {value.RAGFlowSteps.Count}");
            // }
            return value;
        }
        set
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            Console.WriteLine($"[ExecutionContext] Setting CurrentExecutionDetails: {(value != null ? "Has Value" : "NULL")}");
            if (value != null)
            {
                Console.WriteLine($"[ExecutionContext]   - RAGFlow steps: {value.RAGFlowSteps.Count}");
                Console.WriteLine($"[ExecutionContext]   - SQL: {value.GeneratedSQL?.Length ?? 0} chars");
            }
            context.Items[ExecutionDetailsKey] = value;
        }
    }

    public void Clear()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return;

        Console.WriteLine("[ExecutionContext] Clear() called - clearing all data");
        context.Items.Remove(LastExecutionKey);
        context.Items.Remove(ExecutionDetailsKey);
    }
}