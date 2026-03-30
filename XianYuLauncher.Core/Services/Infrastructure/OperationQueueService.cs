using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 通用操作队列服务实现。
/// </summary>
public class OperationQueueService : IOperationQueueService
{
    private readonly ILogger<OperationQueueService> _logger;
    private readonly SemaphoreSlim _globalConcurrencyGate;
    private readonly Dictionary<string, ScopeLockEntry> _scopeLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OperationTaskInfo> _taskSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ManagedOperationTask> _managedTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();
    private int _activeOperationCount;

    private sealed class ScopeLockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int RefCount;
    }

    private sealed class ManagedOperationTask
    {
        public ManagedOperationTask(OperationTaskInfo info, CancellationTokenSource cancellationTokenSource)
        {
            Info = info;
            CancellationTokenSource = cancellationTokenSource;
        }

        public OperationTaskInfo Info { get; }

        public CancellationTokenSource CancellationTokenSource { get; }
    }

    public OperationQueueService(ILogger<OperationQueueService> logger)
    {
        _logger = logger;
        _globalConcurrencyGate = new SemaphoreSlim(1, 1);
    }

    public bool HasActiveOperation => Volatile.Read(ref _activeOperationCount) > 0;

    public IReadOnlyList<OperationTaskInfo> TasksSnapshot
    {
        get
        {
            lock (_syncRoot)
            {
                return _taskSnapshots.Values
                    .OrderBy(task => task.CreatedAtUtc)
                    .Select(Clone)
                    .ToList();
            }
        }
    }

    public event EventHandler<OperationTaskInfo>? TaskStateChanged;
    public event EventHandler<OperationTaskInfo>? TaskProgressChanged;

    public async Task<OperationExecutionResult> EnqueueAsync(OperationTaskRequest request, CancellationToken cancellationToken = default)
    {
        ManagedOperationTask managedTask = CreateManagedTask(request, cancellationToken);

        try
        {
            return await ExecuteTaskAsync(request, managedTask);
        }
        finally
        {
            CleanupManagedTask(managedTask.Info.TaskId);
        }
    }

    public Task<string> EnqueueBackgroundAsync(OperationTaskRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ManagedOperationTask managedTask = CreateManagedTask(request, CancellationToken.None, linkCallerCancellation: false);

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteTaskAsync(request, managedTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "后台操作任务异常: {TaskName}({TaskId})", managedTask.Info.TaskName, managedTask.Info.TaskId);
            }
            finally
            {
                CleanupManagedTask(managedTask.Info.TaskId);
            }
        }, CancellationToken.None);

        return Task.FromResult(managedTask.Info.TaskId);
    }

    public bool TryGetSnapshot(string taskId, out OperationTaskInfo? taskInfo)
    {
        string normalizedTaskId = taskId?.Trim() ?? string.Empty;
        lock (_syncRoot)
        {
            if (_taskSnapshots.TryGetValue(normalizedTaskId, out OperationTaskInfo? snapshot))
            {
                taskInfo = Clone(snapshot);
                return true;
            }
        }

        taskInfo = null;
        return false;
    }

    public void CancelTask(string taskId)
    {
        string normalizedTaskId = taskId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTaskId))
        {
            return;
        }

        ManagedOperationTask? managedTask;
        lock (_syncRoot)
        {
            if (!_managedTasks.TryGetValue(normalizedTaskId, out managedTask))
            {
                return;
            }

            if (managedTask.Info.State is OperationTaskState.Completed or OperationTaskState.Failed or OperationTaskState.Cancelled)
            {
                return;
            }
        }

        try
        {
            managedTask.CancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task<OperationExecutionResult> ExecuteTaskAsync(
        OperationTaskRequest request,
        ManagedOperationTask managedTask)
    {
        ArgumentNullException.ThrowIfNull(request);

        OperationTaskInfo taskInfo = managedTask.Info;
        CancellationToken cancellationToken = managedTask.CancellationTokenSource.Token;

        var globalLockAcquired = false;
        ScopeLockEntry? scopeLockEntry = null;
        var scopeLockAcquired = false;

        Interlocked.Increment(ref _activeOperationCount);

        try
        {
            if (!request.AllowParallel)
            {
                await _globalConcurrencyGate.WaitAsync(cancellationToken);
                globalLockAcquired = true;
            }

            scopeLockEntry = AcquireScopeLock(taskInfo.ScopeKey);
            await scopeLockEntry.Semaphore.WaitAsync(cancellationToken);
            scopeLockAcquired = true;

            UpdateTaskState(taskInfo, OperationTaskState.Running, "任务开始执行", null);

            var executionContext = new OperationTaskExecutionContext(taskInfo.TaskId, (status, progress) =>
            {
                taskInfo.StatusMessage = string.IsNullOrWhiteSpace(status) ? taskInfo.StatusMessage : status;
                taskInfo.Progress = Math.Clamp(progress, 0, 100);
                taskInfo.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
                StoreSnapshot(taskInfo);
                TaskProgressChanged?.Invoke(this, Clone(taskInfo));
            });

            await request.ExecuteAsync(executionContext, cancellationToken);

            UpdateTaskState(taskInfo, OperationTaskState.Completed, ResolveCompletedStatusMessage(taskInfo), null);
            taskInfo.Progress = 100;
            taskInfo.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
            StoreSnapshot(taskInfo);
            TaskProgressChanged?.Invoke(this, Clone(taskInfo));

            return new OperationExecutionResult
            {
                TaskInfo = Clone(taskInfo),
                Success = true
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("操作任务已取消: {TaskName}({TaskId})", taskInfo.TaskName, taskInfo.TaskId);
            UpdateTaskState(taskInfo, OperationTaskState.Cancelled, ResolveCancelledStatusMessage(taskInfo), null);
            return new OperationExecutionResult
            {
                TaskInfo = Clone(taskInfo),
                Success = false,
                ErrorMessage = "任务已取消"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "操作任务执行失败: {TaskName}({TaskId})", taskInfo.TaskName, taskInfo.TaskId);
            UpdateTaskState(taskInfo, OperationTaskState.Failed, $"任务失败：{ex.Message}", ex.Message);
            return new OperationExecutionResult
            {
                TaskInfo = Clone(taskInfo),
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            if (scopeLockAcquired)
            {
                scopeLockEntry?.Semaphore.Release();
            }

            if (scopeLockEntry != null)
            {
                ReleaseScopeLock(taskInfo.ScopeKey, scopeLockEntry);
            }

            if (globalLockAcquired)
            {
                _globalConcurrencyGate.Release();
            }

            Interlocked.Decrement(ref _activeOperationCount);
        }
    }

    private ManagedOperationTask CreateManagedTask(
        OperationTaskRequest request,
        CancellationToken cancellationToken,
        bool linkCallerCancellation = true)
    {
        ArgumentNullException.ThrowIfNull(request);

        string normalizedScopeKey = string.IsNullOrWhiteSpace(request.ScopeKey)
            ? "global"
            : request.ScopeKey.Trim();

        OperationTaskInfo taskInfo = new()
        {
            TaskId = Guid.NewGuid().ToString("N"),
            TaskName = request.TaskName,
            TaskType = request.TaskType,
            ScopeKey = normalizedScopeKey,
            State = OperationTaskState.Queued,
            Progress = 0,
            StatusMessage = "已加入任务队列"
        };

        CancellationTokenSource cancellationTokenSource = linkCallerCancellation
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : new CancellationTokenSource();

        ManagedOperationTask managedTask = new(taskInfo, cancellationTokenSource);

        lock (_syncRoot)
        {
            _managedTasks[taskInfo.TaskId] = managedTask;
        }

        UpdateTaskState(taskInfo, OperationTaskState.Queued, taskInfo.StatusMessage, null);
        return managedTask;
    }

    private void CleanupManagedTask(string taskId)
    {
        ManagedOperationTask? managedTask = null;

        lock (_syncRoot)
        {
            if (_managedTasks.Remove(taskId, out var removedTask))
            {
                managedTask = removedTask;
            }
        }

        managedTask?.CancellationTokenSource.Dispose();
    }

    private ScopeLockEntry AcquireScopeLock(string scopeKey)
    {
        lock (_syncRoot)
        {
            if (_scopeLocks.TryGetValue(scopeKey, out var existingLock))
            {
                existingLock.RefCount++;
                return existingLock;
            }

            var newLock = new ScopeLockEntry
            {
                RefCount = 1
            };

            _scopeLocks[scopeKey] = newLock;
            return newLock;
        }
    }

    private void ReleaseScopeLock(string scopeKey, ScopeLockEntry entry)
    {
        lock (_syncRoot)
        {
            if (!_scopeLocks.TryGetValue(scopeKey, out var existingLock) || !ReferenceEquals(existingLock, entry))
            {
                return;
            }

            existingLock.RefCount--;
            if (existingLock.RefCount <= 0)
            {
                _scopeLocks.Remove(scopeKey);
                existingLock.Semaphore.Dispose();
            }
        }
    }

    private void UpdateTaskState(OperationTaskInfo taskInfo, OperationTaskState newState, string statusMessage, string? errorMessage)
    {
        taskInfo.State = newState;
        taskInfo.StatusMessage = statusMessage;
        taskInfo.ErrorMessage = errorMessage;
        taskInfo.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        StoreSnapshot(taskInfo);
        TaskStateChanged?.Invoke(this, Clone(taskInfo));
    }

    private void StoreSnapshot(OperationTaskInfo taskInfo)
    {
        lock (_syncRoot)
        {
            _taskSnapshots[taskInfo.TaskId] = Clone(taskInfo);
        }
    }

    private static string ResolveCompletedStatusMessage(OperationTaskInfo taskInfo)
    {
        if (taskInfo.Progress >= 100 &&
            !string.IsNullOrWhiteSpace(taskInfo.StatusMessage) &&
            !string.Equals(taskInfo.StatusMessage, "已加入任务队列", StringComparison.Ordinal) &&
            !string.Equals(taskInfo.StatusMessage, "任务开始执行", StringComparison.Ordinal))
        {
            return taskInfo.StatusMessage;
        }

        return "任务完成";
    }

    private static string ResolveCancelledStatusMessage(OperationTaskInfo taskInfo)
    {
        if (!string.IsNullOrWhiteSpace(taskInfo.StatusMessage) &&
            !string.Equals(taskInfo.StatusMessage, "已加入任务队列", StringComparison.Ordinal) &&
            !string.Equals(taskInfo.StatusMessage, "任务开始执行", StringComparison.Ordinal))
        {
            return taskInfo.StatusMessage;
        }

        return "任务已取消";
    }

    private static OperationTaskInfo Clone(OperationTaskInfo source)
    {
        return new OperationTaskInfo
        {
            TaskId = source.TaskId,
            TaskName = source.TaskName,
            TaskType = source.TaskType,
            ScopeKey = source.ScopeKey,
            State = source.State,
            Progress = source.Progress,
            StatusMessage = source.StatusMessage,
            ErrorMessage = source.ErrorMessage,
            CreatedAtUtc = source.CreatedAtUtc,
            LastUpdatedAtUtc = source.LastUpdatedAtUtc
        };
    }
}
