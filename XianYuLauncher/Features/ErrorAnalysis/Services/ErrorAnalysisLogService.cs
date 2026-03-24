using System.Text;
using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public class ErrorAnalysisLogService : IErrorAnalysisLogService
{
    private const int LogUpdateIntervalMs = 100;

    private readonly ErrorAnalysisSessionState _sessionState;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly object _updateGate = new();

    private DateTime _lastLogUpdateTime = DateTime.MinValue;
    private bool _isUpdateScheduled;

    public ErrorAnalysisLogService(ErrorAnalysisSessionState sessionState, IUiDispatcher uiDispatcher)
    {
        _sessionState = sessionState;
        _uiDispatcher = uiDispatcher;
    }

    public void SetLogData(string launchCommand, IReadOnlyList<string> gameOutput, IReadOnlyList<string> gameError)
    {
        _sessionState.Context.LaunchCommand = launchCommand;
        _sessionState.ReplaceGameOutput(gameOutput);
        _sessionState.ReplaceGameError(gameError);

        ResetUpdateState();
        ReplaceDisplayedLogs(BuildBufferedLogLines(gameOutput, gameError));
        RefreshFullLog();
    }

    public void InitializeRealTimeLogs()
    {
        _sessionState.ReplaceGameOutput(Array.Empty<string>());
        _sessionState.ReplaceGameError(Array.Empty<string>());

        ResetUpdateState();
        ReplaceDisplayedLogs(BuildWaitingLogLines());
        RefreshFullLog();
    }

    public void AddGameOutputLog(string logLine)
    {
        lock (_sessionState.Context.GameOutput)
        {
            _sessionState.Context.GameOutput.Add(logLine);
        }

        AddLogLineToUi(logLine);
        ScheduleLogUpdate();
    }

    public void AddGameErrorLog(string logLine)
    {
        lock (_sessionState.Context.GameError)
        {
            _sessionState.Context.GameError.Add(logLine);
        }

        AddLogLineToUi(logLine);
        ScheduleLogUpdate();
    }

    private void ResetUpdateState()
    {
        lock (_updateGate)
        {
            _lastLogUpdateTime = DateTime.MinValue;
            _isUpdateScheduled = false;
        }
    }

    private static List<string> BuildBufferedLogLines(IReadOnlyList<string> gameOutput, IReadOnlyList<string> gameError)
    {
        List<string> lines =
        [
            "=== 实时游戏日志 ===",
            $"日志开始时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            string.Empty,
            "=== 游戏输出日志 ==="
        ];

        foreach (var line in gameOutput)
        {
            lines.Add(line);
        }

        lines.Add(string.Empty);
        lines.Add("=== 游戏错误日志 ===");

        foreach (var line in gameError)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static List<string> BuildWaitingLogLines()
    {
        return
        [
            "=== 实时游戏日志 ===",
            $"日志开始时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            string.Empty,
            "等待游戏输出..."
        ];
    }

    private void ReplaceDisplayedLogs(IReadOnlyList<string> lines)
    {
        void Update()
        {
            _sessionState.LogLines.Clear();
            foreach (var line in lines)
            {
                _sessionState.LogLines.Add(line);
            }
        }

        if (_uiDispatcher.HasThreadAccess)
        {
            Update();
            return;
        }

        _uiDispatcher.TryEnqueue(Update);
    }

    private void AddLogLineToUi(string line)
    {
        if (_uiDispatcher.HasThreadAccess)
        {
            _sessionState.LogLines.Add(line);
            return;
        }

        _uiDispatcher.TryEnqueue(() =>
        {
            _sessionState.LogLines.Add(line);
        });
    }

    private void ScheduleLogUpdate()
    {
        int? delay = null;

        lock (_updateGate)
        {
            var now = DateTime.Now;
            var timeSinceLastUpdate = now - _lastLogUpdateTime;
            if (timeSinceLastUpdate.TotalMilliseconds >= LogUpdateIntervalMs)
            {
                _lastLogUpdateTime = now;
            }
            else if (!_isUpdateScheduled)
            {
                _isUpdateScheduled = true;
                delay = Math.Max(0, LogUpdateIntervalMs - (int)timeSinceLastUpdate.TotalMilliseconds);
            }
            else
            {
                return;
            }
        }

        if (delay == null)
        {
            RefreshFullLog();
            return;
        }

        _ = Task.Delay(delay.Value).ContinueWith(_ =>
        {
            bool shouldRefresh = false;

            lock (_updateGate)
            {
                if (_isUpdateScheduled)
                {
                    _lastLogUpdateTime = DateTime.Now;
                    _isUpdateScheduled = false;
                    shouldRefresh = true;
                }
            }

            if (shouldRefresh)
            {
                RefreshFullLog();
            }
        }, TaskScheduler.Default);
    }

    private void RefreshFullLog()
    {
        var (outputList, errorList) = _sessionState.CreateLogSnapshot();
        var fullLog = BuildFullLog(outputList, errorList, out var crashReason);
        _sessionState.Context.OriginalLog = fullLog;

        void Update()
        {
            _sessionState.CrashReason = crashReason;
            _sessionState.FullLog = fullLog;
        }

        if (_uiDispatcher.HasThreadAccess)
        {
            Update();
            return;
        }

        _uiDispatcher.TryEnqueue(Update);
    }

    private static string BuildFullLog(IReadOnlyList<string> gameOutput, IReadOnlyList<string> gameError, out string crashReason)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== 实时游戏日志 ===");
        sb.AppendLine(string.Format("日志开始时间: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));
        sb.AppendLine();

        if (gameOutput.Count == 0 && gameError.Count == 0)
        {
            crashReason = string.Empty;
            sb.AppendLine("等待游戏输出...");
        }
        else
        {
            crashReason = AnalyzeCrash(gameOutput, gameError);
            sb.AppendLine(string.Format("崩溃分析: {0}", crashReason));
            sb.AppendLine();
        }

        sb.AppendLine("=== 游戏输出日志 ===");
        foreach (var line in gameOutput)
        {
            sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine("=== 游戏错误日志 ===");
        foreach (var line in gameError)
        {
            sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine("实时日志持续更新中...");
        return sb.ToString();
    }

    private static string AnalyzeCrash(IReadOnlyList<string> gameOutput, IReadOnlyList<string> gameError)
    {
        if (IsNormalStartup(gameOutput, gameError))
        {
            return "游戏正在启动中";
        }

        if (ContainsKeyword(gameError, "Manually triggered debug crash") || ContainsKeyword(gameOutput, "Manually triggered debug crash"))
        {
            return "玩家手动触发崩溃";
        }

        if (ContainsKeyword(gameError, "UnsupportedClassVersionError") ||
            ContainsKeyword(gameOutput, "UnsupportedClassVersionError") ||
            ContainsKeyword(gameError, "java.lang.UnsupportedClassVersionError") ||
            ContainsKeyword(gameOutput, "java.lang.UnsupportedClassVersionError") ||
            ContainsKeyword(gameError, "class file version") ||
            ContainsKeyword(gameOutput, "class file version") ||
            ContainsKeyword(gameError, "compiled by a more recent version") ||
            ContainsKeyword(gameOutput, "compiled by a more recent version"))
        {
            return "Java 版本不匹配 - 需要更高版本的 Java";
        }

        if (ContainsKeyword(gameError, "[Fatal Error]") || ContainsKeyword(gameOutput, "[Fatal Error]"))
        {
            return "致命错误导致崩溃";
        }

        if (ContainsKeywordWithContext(gameError, "java.lang.Exception", "[ERROR]", "[FATAL]") ||
            ContainsKeywordWithContext(gameOutput, "java.lang.Exception", "[ERROR]", "[FATAL]"))
        {
            return "Java异常导致崩溃";
        }

        if (ContainsKeyword(gameError, "OutOfMemoryError") || ContainsKeyword(gameOutput, "OutOfMemoryError"))
        {
            return "内存不足导致崩溃";
        }

        if (ContainsKeyword(gameError, "玩家崩溃") || ContainsKeyword(gameOutput, "玩家崩溃"))
        {
            return "玩家手动触发崩溃";
        }

        if (ContainsKeyword(gameError, "[ERROR]") ||
            ContainsKeyword(gameOutput, "[ERROR]") ||
            ContainsKeyword(gameError, "[FATAL]") ||
            ContainsKeyword(gameOutput, "[FATAL]"))
        {
            return "错误日志中存在错误信息";
        }

        return "未知崩溃原因";
    }

    private static bool IsNormalStartup(IReadOnlyList<string> gameOutput, IReadOnlyList<string> gameError)
    {
        List<string> allLogs = [];
        allLogs.AddRange(gameOutput);
        allLogs.AddRange(gameError);

        bool hasLoadingMessage = ContainsKeyword(allLogs, "Loading Minecraft") ||
                                 ContainsKeyword(allLogs, "Loading mods") ||
                                 ContainsKeyword(allLogs, "Datafixer optimizations");

        bool hasOnlyWarnings = (ContainsKeyword(allLogs, "[WARN]") || ContainsKeyword(allLogs, "[INFO]")) &&
                               !ContainsKeyword(allLogs, "[ERROR]") &&
                               !ContainsKeyword(allLogs, "[FATAL]");

        bool hasMixinWarnings = ContainsKeyword(allLogs, "Reference map") ||
                                ContainsKeyword(allLogs, "@Mixin target") ||
                                ContainsKeyword(allLogs, "Force-disabling mixin");

        return hasLoadingMessage && hasOnlyWarnings && hasMixinWarnings;
    }

    private static bool ContainsKeywordWithContext(IReadOnlyList<string> lines, string keyword, params string[] contextKeywords)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var contextKeyword in contextKeywords)
            {
                if (lines[i].Contains(contextKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsKeyword(IReadOnlyList<string> lines, string keyword)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}