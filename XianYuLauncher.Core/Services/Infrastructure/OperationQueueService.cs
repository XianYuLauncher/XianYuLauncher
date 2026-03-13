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
    private readonly object _syncRoot = new();
    private int _activeOperationCount;

    private sealed class ScopeLockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int RefCount;
    }

    public OperationQueueService(ILogger<OperationQueueService> logger)
    {
        _logger = logger;
        _globalConcurrencyGate = new SemaphoreSlim(1, 1);
    }

    public bool HasActiveOperation => Volatile.Read(ref _activeOperationCount) > 0;

    public event EventHandler<OperationTaskInfo>? TaskStateChanged;
    public event EventHandler<OperationTaskInfo>? TaskProgressChanged;

    public async Task<OperationExecutionResult> EnqueueAsync(OperationTaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedScopeKey = string.IsNullOrWhiteSpace(request.ScopeKey)
            ? "global"
            : request.ScopeKey.Trim();

        var taskInfo = new OperationTaskInfo
        {
            TaskId = Guid.NewGuid().ToString(),
            TaskName = request.TaskName,
            TaskType = request.TaskType,
            ScopeKey = normalizedScopeKey,
            State = OperationTaskState.Queued,
            Progress = 0,
            StatusMessage = "已加入任务队列"
        };

        UpdateTaskState(taskInfo, OperationTaskState.Queued, taskInfo.StatusMessage, null);

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

            var executionContext = new OperationTaskExecutionContext((status, progress) =>
            {
                taskInfo.StatusMessage = string.IsNullOrWhiteSpace(status) ? taskInfo.StatusMessage : status;
                taskInfo.Progress = Math.Clamp(progress, 0, 100);
                taskInfo.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
                TaskProgressChanged?.Invoke(this, Clone(taskInfo));
            });

            await request.ExecuteAsync(executionContext, cancellationToken);

            UpdateTaskState(taskInfo, OperationTaskState.Completed, "任务完成", null);
            taskInfo.Progress = 100;
            taskInfo.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
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
            UpdateTaskState(taskInfo, OperationTaskState.Cancelled, "任务已取消", null);
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
        TaskStateChanged?.Invoke(this, Clone(taskInfo));
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
