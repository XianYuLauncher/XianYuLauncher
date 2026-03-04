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
}
