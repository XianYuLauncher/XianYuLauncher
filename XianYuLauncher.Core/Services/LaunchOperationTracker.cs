namespace XianYuLauncher.Core.Services;

public interface ILaunchOperationTracker
{
    string CreateOperation(string versionName, string versionPath);

    void CompleteOperation(string operationId);

    void FailOperation(string operationId, string errorMessage);

    void CancelOperation(string operationId);

    bool TryGetSnapshot(string operationId, out AgentOperationSnapshot? snapshot);
}

public sealed class LaunchOperationTracker : ILaunchOperationTracker
{
    private const int MaxRetainedTerminalOperations = 5;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, LaunchOperationEntry> _operations = new(StringComparer.OrdinalIgnoreCase);

    private sealed class LaunchOperationEntry
    {
        public required string OperationId { get; init; }

        public required string VersionName { get; init; }

        public required string VersionPath { get; init; }

        public required string State { get; set; }

        public required string StatusMessage { get; set; }

        public string? ErrorMessage { get; set; }

        public bool IsTerminal { get; set; }

        public DateTimeOffset LastUpdatedAtUtc { get; set; }
    }

    public string CreateOperation(string versionName, string versionPath)
    {
        var normalizedVersionName = string.IsNullOrWhiteSpace(versionName) ? "未知版本" : versionName.Trim();
        var operationId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        var entry = new LaunchOperationEntry
        {
            OperationId = operationId,
            VersionName = normalizedVersionName,
            VersionPath = versionPath?.Trim() ?? string.Empty,
            State = "launching",
            StatusMessage = $"正在启动 {normalizedVersionName}...",
            LastUpdatedAtUtc = now
        };

        lock (_lock)
        {
            _operations[operationId] = entry;
            PruneTerminalOperations_NoLock();
        }

        return operationId;
    }

    public void CompleteOperation(string operationId)
    {
        UpdateOperation(operationId, entry =>
        {
            entry.State = "completed";
            entry.StatusMessage = "游戏进程已成功启动。";
            entry.ErrorMessage = null;
            entry.IsTerminal = true;
        });
    }

    public void FailOperation(string operationId, string errorMessage)
    {
        var normalizedErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "游戏未能启动，请查看日志。" : errorMessage.Trim();

        UpdateOperation(operationId, entry =>
        {
            entry.State = "failed";
            entry.StatusMessage = $"启动失败: {normalizedErrorMessage}";
            entry.ErrorMessage = normalizedErrorMessage;
            entry.IsTerminal = true;
        });
    }

    public void CancelOperation(string operationId)
    {
        UpdateOperation(operationId, entry =>
        {
            entry.State = "cancelled";
            entry.StatusMessage = "启动已取消。";
            entry.ErrorMessage = null;
            entry.IsTerminal = true;
        });
    }

    public bool TryGetSnapshot(string operationId, out AgentOperationSnapshot? snapshot)
    {
        var normalizedOperationId = operationId?.Trim() ?? string.Empty;
        lock (_lock)
        {
            if (_operations.TryGetValue(normalizedOperationId, out var entry))
            {
                snapshot = CreateSnapshot(entry);
                return true;
            }
        }

        snapshot = null;
        return false;
    }

    private void UpdateOperation(string operationId, Action<LaunchOperationEntry> updateAction)
    {
        var normalizedOperationId = operationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedOperationId))
        {
            return;
        }

        lock (_lock)
        {
            if (!_operations.TryGetValue(normalizedOperationId, out var entry))
            {
                return;
            }

            updateAction(entry);
            entry.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
            PruneTerminalOperations_NoLock();
        }
    }

    private void PruneTerminalOperations_NoLock()
    {
        var operationIdsToRemove = _operations.Values
            .Where(entry => entry.IsTerminal)
            .OrderByDescending(entry => entry.LastUpdatedAtUtc)
            .Skip(MaxRetainedTerminalOperations)
            .Select(entry => entry.OperationId)
            .ToList();

        foreach (var operationId in operationIdsToRemove)
        {
            _operations.Remove(operationId);
        }
    }

    private static AgentOperationSnapshot CreateSnapshot(LaunchOperationEntry entry)
    {
        return new AgentOperationSnapshot
        {
            OperationId = entry.OperationId,
            State = entry.State,
            StatusMessage = entry.StatusMessage,
            IsTerminal = entry.IsTerminal,
            OperationKind = "launchGame",
            TaskName = $"启动 {entry.VersionName}",
            VersionName = entry.VersionName,
            ErrorMessage = entry.ErrorMessage
        };
    }
}