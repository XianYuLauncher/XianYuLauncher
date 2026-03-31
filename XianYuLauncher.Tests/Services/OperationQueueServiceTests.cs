using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public class OperationQueueServiceTests
{
    private readonly Mock<ILogger<OperationQueueService>> _loggerMock = new();

    private sealed class ConcurrencyCounter
    {
        public int Current;

        public int Max;
    }

    [Fact]
    public async Task EnqueueAsync_SameScope_ShouldRunSequentially()
    {
        var service = new OperationQueueService(_loggerMock.Object);
        var counter = new ConcurrencyCounter();

        var task1 = service.EnqueueAsync(CreateRequest("scope-a", true, counter));
        var task2 = service.EnqueueAsync(CreateRequest("scope-a", true, counter));

        var results = await Task.WhenAll(task1, task2);

        counter.Max.Should().Be(1);
        results[0].Success.Should().BeTrue();
        results[1].Success.Should().BeTrue();
    }

    [Fact]
    public async Task EnqueueAsync_DifferentScope_WithAllowParallelTrue_ShouldRunConcurrently()
    {
        var service = new OperationQueueService(_loggerMock.Object);
        var counter = new ConcurrencyCounter();

        var task1 = service.EnqueueAsync(CreateRequest("scope-a", true, counter));
        var task2 = service.EnqueueAsync(CreateRequest("scope-b", true, counter));

        var results = await Task.WhenAll(task1, task2);

        counter.Max.Should().BeGreaterThan(1);
        results[0].Success.Should().BeTrue();
        results[1].Success.Should().BeTrue();
    }

    [Fact]
    public async Task EnqueueAsync_DifferentScope_WithAllowParallelFalse_ShouldRunSequentially()
    {
        var service = new OperationQueueService(_loggerMock.Object);
        var counter = new ConcurrencyCounter();

        var task1 = service.EnqueueAsync(CreateRequest("scope-a", false, counter));
        var task2 = service.EnqueueAsync(CreateRequest("scope-b", false, counter));

        var results = await Task.WhenAll(task1, task2);

        counter.Max.Should().Be(1);
        results[0].Success.Should().BeTrue();
        results[1].Success.Should().BeTrue();
    }

    [Fact]
    public async Task EnqueueBackgroundAsync_ShouldReturnTaskIdAndExposeCompletedSnapshot()
    {
        var service = new OperationQueueService(_loggerMock.Object);

        string taskId = await service.EnqueueBackgroundAsync(new OperationTaskRequest
        {
            TaskName = "background-task",
            TaskType = OperationTaskType.CommunityResourceUpdate,
            ScopeKey = "scope-background",
            AllowParallel = true,
            ExecuteAsync = async (context, token) =>
            {
                context.ReportProgress("处理中", 25);
                await Task.Delay(50, token);
                context.ReportProgress("即将完成", 90);
            }
        });

        service.TryGetSnapshot(taskId, out var queuedSnapshot).Should().BeTrue();
        queuedSnapshot.Should().NotBeNull();

        OperationTaskInfo finalSnapshot = await WaitForTerminalSnapshotAsync(service, taskId);
        finalSnapshot.State.Should().Be(OperationTaskState.Completed);
        finalSnapshot.Progress.Should().Be(100);
        finalSnapshot.StatusMessage.Should().Be("任务完成");
        service.TasksSnapshot.Should().Contain(task => task.TaskId == taskId && task.State == OperationTaskState.Completed);
    }

    [Fact]
    public async Task EnqueueBackgroundAsync_WhenTaskReportsCompletionSummary_ShouldPreserveSummaryMessage()
    {
        var service = new OperationQueueService(_loggerMock.Object);

        string taskId = await service.EnqueueBackgroundAsync(new OperationTaskRequest
        {
            TaskName = "background-summary-task",
            TaskType = OperationTaskType.CommunityResourceUpdate,
            ScopeKey = "scope-background-summary",
            AllowParallel = true,
            ExecuteAsync = (context, _) =>
            {
                context.ReportProgress("社区资源更新完成：已更新 2，失败 0。", 100);
                return Task.CompletedTask;
            }
        });

        OperationTaskInfo finalSnapshot = await WaitForTerminalSnapshotAsync(service, taskId);
        finalSnapshot.State.Should().Be(OperationTaskState.Completed);
        finalSnapshot.StatusMessage.Should().Be("社区资源更新完成：已更新 2，失败 0。");
    }

    [Fact]
    public async Task EnqueueBackgroundAsync_CallerCancellationAfterEnqueue_ShouldNotCancelRunningTask()
    {
        var service = new OperationQueueService(_loggerMock.Object);
        using CancellationTokenSource callerCts = new();
        TaskCompletionSource<bool> startedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> finishSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        string taskId = await service.EnqueueBackgroundAsync(new OperationTaskRequest
        {
            TaskName = "background-caller-cancel-task",
            TaskType = OperationTaskType.CommunityResourceUpdate,
            ScopeKey = "scope-background-caller-cancel",
            AllowParallel = true,
            ExecuteAsync = async (context, token) =>
            {
                context.ReportProgress("处理中", 25);
                startedSource.TrySetResult(true);
                await finishSource.Task.WaitAsync(token);
                context.ReportProgress("后台任务完成", 100);
            }
        }, callerCts.Token);

        await startedSource.Task.WaitAsync(TimeSpan.FromSeconds(2));
        callerCts.Cancel();
        finishSource.TrySetResult(true);

        OperationTaskInfo finalSnapshot = await WaitForTerminalSnapshotAsync(service, taskId);
        finalSnapshot.State.Should().Be(OperationTaskState.Completed);
        finalSnapshot.StatusMessage.Should().Be("后台任务完成");
    }

    [Fact]
    public async Task EnqueueAsync_WhenCallerCancellationRequested_ShouldMarkTaskCancelled()
    {
        var service = new OperationQueueService(_loggerMock.Object);
        using CancellationTokenSource callerCts = new();
        TaskCompletionSource<bool> startedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        var executionTask = service.EnqueueAsync(new OperationTaskRequest
        {
            TaskName = "cancelled-task",
            TaskType = OperationTaskType.LoaderInstall,
            ScopeKey = "scope-cancelled-task",
            AllowParallel = true,
            ExecuteAsync = async (_, token) =>
            {
                startedSource.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
        }, callerCts.Token);

        await startedSource.Task.WaitAsync(TimeSpan.FromSeconds(2));
        callerCts.Cancel();

        var result = await executionTask;

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("任务已取消");
        result.TaskInfo.State.Should().Be(OperationTaskState.Cancelled);
        result.TaskInfo.StatusMessage.Should().Be("任务已取消");
    }

    [Fact]
    public async Task EnqueueAsync_WhenExecuteThrowsOperationCanceledWithoutRequestedCancellation_ShouldMarkTaskFailed()
    {
        var service = new OperationQueueService(_loggerMock.Object);

        var result = await service.EnqueueAsync(new OperationTaskRequest
        {
            TaskName = "failed-oce-task",
            TaskType = OperationTaskType.LoaderInstall,
            ScopeKey = "scope-failed-oce-task",
            AllowParallel = true,
            ExecuteAsync = (_, _) => throw new OperationCanceledException("内部步骤取消")
        });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("内部步骤取消");
        result.TaskInfo.State.Should().Be(OperationTaskState.Failed);
        result.TaskInfo.StatusMessage.Should().Be("任务失败：内部步骤取消");
    }

    [Fact]
    public async Task EnqueueAsync_WhenTerminalSnapshotsExceedLimit_ShouldRetainLatestFive()
    {
        var service = new OperationQueueService(_loggerMock.Object);
        List<string> taskIds = [];

        for (var index = 0; index < 6; index++)
        {
            var result = await service.EnqueueAsync(new OperationTaskRequest
            {
                TaskName = $"retention-task-{index}",
                TaskType = OperationTaskType.LoaderInstall,
                ScopeKey = $"retention-scope-{index}",
                AllowParallel = true,
                ExecuteAsync = (_, _) => Task.CompletedTask
            });

            taskIds.Add(result.TaskInfo.TaskId);
            await Task.Delay(2);
        }

        service.TasksSnapshot.Should().HaveCount(5);
        service.TryGetSnapshot(taskIds[0], out _).Should().BeFalse();

        foreach (var taskId in taskIds.Skip(1))
        {
            service.TryGetSnapshot(taskId, out var snapshot).Should().BeTrue();
            snapshot!.State.Should().Be(OperationTaskState.Completed);
        }
    }

    private static OperationTaskRequest CreateRequest(string scopeKey, bool allowParallel, ConcurrencyCounter counter)
    {
        async Task ExecuteAsync(CancellationToken token)
        {
            var now = Interlocked.Increment(ref counter.Current);
            UpdateMaxConcurrency(counter, now);

            try
            {
                await Task.Delay(200, token);
            }
            finally
            {
                Interlocked.Decrement(ref counter.Current);
            }
        }

        return new OperationTaskRequest
        {
            TaskName = "test-task",
            TaskType = OperationTaskType.LoaderInstall,
            ScopeKey = scopeKey,
            AllowParallel = allowParallel,
            ExecuteAsync = (_, token) => ExecuteAsync(token)
        };
    }

    private static void UpdateMaxConcurrency(ConcurrencyCounter counter, int candidate)
    {
        while (true)
        {
            var snapshot = Volatile.Read(ref counter.Max);
            if (candidate <= snapshot)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref counter.Max, candidate, snapshot) == snapshot)
            {
                return;
            }
        }
    }

    private static async Task<OperationTaskInfo> WaitForTerminalSnapshotAsync(OperationQueueService service, string taskId)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (service.TryGetSnapshot(taskId, out OperationTaskInfo? snapshot) &&
                snapshot != null &&
                snapshot.State is OperationTaskState.Completed or OperationTaskState.Failed or OperationTaskState.Cancelled)
            {
                return snapshot;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"任务 {taskId} 未在预期时间内结束。");
    }
}
