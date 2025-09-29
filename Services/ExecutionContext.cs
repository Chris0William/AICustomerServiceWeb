using AICustomerServiceWeb.Models;

namespace AICustomerServiceWeb.Services;

public static class ExecutionContext
{
    [ThreadStatic]
    private static string? _lastDatabaseExecution;

    [ThreadStatic]
    private static ExecutionDetails? _currentExecutionDetails;

    public static string? LastDatabaseExecution
    {
        get => _lastDatabaseExecution;
        set => _lastDatabaseExecution = value;
    }

    public static ExecutionDetails? CurrentExecutionDetails
    {
        get => _currentExecutionDetails;
        set => _currentExecutionDetails = value;
    }

    public static void Clear()
    {
        _lastDatabaseExecution = null;
        _currentExecutionDetails = null;
    }
}