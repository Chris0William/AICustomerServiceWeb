using AICustomerServiceWeb.Models;

namespace AICustomerServiceWeb.Services;

/// <summary>
/// 简单的执行上下文，用于在工具和服务之间传递数据
/// </summary>
public static class ExecutionContext
{
    private static string? _lastDatabaseExecution;
    private static ExecutionDetails? _currentExecutionDetails;
    private static readonly object _lock = new();

    public static string? LastDatabaseExecution
    {
        get
        {
            lock (_lock)
            {
                Console.WriteLine($"[ExecutionContext] Getting LastDatabaseExecution: {(_lastDatabaseExecution != null ? "Has Value" : "NULL")}");
                return _lastDatabaseExecution;
            }
        }
        set
        {
            lock (_lock)
            {
                Console.WriteLine($"[ExecutionContext] Setting LastDatabaseExecution: {(value != null ? value.Length + " chars" : "NULL")}");
                _lastDatabaseExecution = value;
            }
        }
    }

    public static ExecutionDetails? CurrentExecutionDetails
    {
        get
        {
            lock (_lock)
            {
                Console.WriteLine($"[ExecutionContext] Getting CurrentExecutionDetails: {(_currentExecutionDetails != null ? "Has Value" : "NULL")}");
                if (_currentExecutionDetails != null)
                {
                    Console.WriteLine($"[ExecutionContext]   - RAGFlow steps: {_currentExecutionDetails.RAGFlowSteps.Count}");
                }
                return _currentExecutionDetails;
            }
        }
        set
        {
            lock (_lock)
            {
                Console.WriteLine($"[ExecutionContext] Setting CurrentExecutionDetails: {(value != null ? "Has Value" : "NULL")}");
                if (value != null)
                {
                    Console.WriteLine($"[ExecutionContext]   - RAGFlow steps: {value.RAGFlowSteps.Count}");
                    Console.WriteLine($"[ExecutionContext]   - SQL: {value.GeneratedSQL?.Length ?? 0} chars");
                }
                _currentExecutionDetails = value;
            }
        }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            Console.WriteLine("[ExecutionContext] Clear() called - clearing all data");
            _lastDatabaseExecution = null;
            _currentExecutionDetails = null;
        }
    }
}