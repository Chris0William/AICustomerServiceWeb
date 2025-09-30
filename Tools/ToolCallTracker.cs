using System.Collections.Concurrent;

namespace AICustomerServiceWeb.Tools;

/// <summary>
/// 工具调用跟踪器，防止无限循环调用
/// </summary>
public class ToolCallTracker
{
    private readonly ConcurrentDictionary<string, int> _callCounts = new();
    private readonly int _maxCallsPerTool = 3;
    private readonly int _maxTotalCalls = 20;
    private int _totalCalls = 0;

    public bool CanCallTool(string toolName, string parameters)
    {
        // 检查总调用次数
        if (_totalCalls >= _maxTotalCalls)
        {
            Console.WriteLine($"[ToolCallTracker] Total calls limit reached: {_totalCalls}/{_maxTotalCalls}");
            return false;
        }

        // 生成唯一键（工具名+参数）
        var key = $"{toolName}:{parameters}";

        // 获取当前调用次数
        var currentCount = _callCounts.GetOrAdd(key, 0);

        if (currentCount >= _maxCallsPerTool)
        {
            Console.WriteLine($"[ToolCallTracker] Tool {toolName} with params '{parameters}' exceeded limit: {currentCount}/{_maxCallsPerTool}");
            return false;
        }

        // 增加计数
        _callCounts[key] = currentCount + 1;
        _totalCalls++;

        Console.WriteLine($"[ToolCallTracker] Tool {toolName} call #{currentCount + 1}, Total: {_totalCalls}");
        return true;
    }

    public void Reset()
    {
        _callCounts.Clear();
        _totalCalls = 0;
        Console.WriteLine("[ToolCallTracker] Reset all counters");
    }
}