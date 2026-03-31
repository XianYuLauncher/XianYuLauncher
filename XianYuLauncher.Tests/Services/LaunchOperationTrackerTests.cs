using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class LaunchOperationTrackerTests
{
    [Fact]
    public void CreateOperation_ShouldCreateLaunchingSnapshot()
    {
        var tracker = new LaunchOperationTracker();

        var operationId = tracker.CreateOperation("1.21.10", @"D:\\.minecraft\\versions\\1.21.10");

        tracker.TryGetSnapshot(operationId, out var snapshot).Should().BeTrue();
        snapshot!.State.Should().Be("launching");
        snapshot.StatusMessage.Should().Be("正在启动 1.21.10...");
        snapshot.IsTerminal.Should().BeFalse();
        snapshot.OperationKind.Should().Be("launchGame");
        snapshot.TaskName.Should().Be("启动 1.21.10");
        snapshot.VersionName.Should().Be("1.21.10");
    }

    [Fact]
    public void CompleteOperation_ShouldMarkSnapshotAsCompleted()
    {
        var tracker = new LaunchOperationTracker();
        var operationId = tracker.CreateOperation("1.21.10", @"D:\\.minecraft\\versions\\1.21.10");

        tracker.CompleteOperation(operationId);

        tracker.TryGetSnapshot(operationId, out var snapshot).Should().BeTrue();
        snapshot!.State.Should().Be("completed");
        snapshot.StatusMessage.Should().Be("游戏进程已成功启动。");
        snapshot.IsTerminal.Should().BeTrue();
        snapshot.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void FailOperation_ShouldMarkSnapshotAsFailed()
    {
        var tracker = new LaunchOperationTracker();
        var operationId = tracker.CreateOperation("1.21.10", @"D:\\.minecraft\\versions\\1.21.10");

        tracker.FailOperation(operationId, "账户登录已过期，请重新登录。");

        tracker.TryGetSnapshot(operationId, out var snapshot).Should().BeTrue();
        snapshot!.State.Should().Be("failed");
        snapshot.StatusMessage.Should().Be("启动失败: 账户登录已过期，请重新登录。");
        snapshot.IsTerminal.Should().BeTrue();
        snapshot.ErrorMessage.Should().Be("账户登录已过期，请重新登录。");
    }

    [Fact]
    public void CancelOperation_ShouldMarkSnapshotAsCancelled()
    {
        var tracker = new LaunchOperationTracker();
        var operationId = tracker.CreateOperation("1.21.10", @"D:\\.minecraft\\versions\\1.21.10");

        tracker.CancelOperation(operationId);

        tracker.TryGetSnapshot(operationId, out var snapshot).Should().BeTrue();
        snapshot!.State.Should().Be("cancelled");
        snapshot.StatusMessage.Should().Be("启动已取消。");
        snapshot.IsTerminal.Should().BeTrue();
        snapshot.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void TryGetSnapshot_ShouldTreatOperationIdAsCaseInsensitive()
    {
        var tracker = new LaunchOperationTracker();
        var operationId = tracker.CreateOperation("1.21.10", @"D:\\.minecraft\\versions\\1.21.10");

        var found = tracker.TryGetSnapshot(operationId.ToUpperInvariant(), out var snapshot);

        found.Should().BeTrue();
        snapshot.Should().NotBeNull();
        snapshot!.OperationId.Should().Be(operationId);
    }

    [Fact]
    public void CompleteOperation_WhenTerminalOperationsExceedLimit_ShouldRetainLatestFiveAndKeepActiveOperation()
    {
        var tracker = new LaunchOperationTracker();
        var activeOperationId = tracker.CreateOperation("active", @"D:\\.minecraft\\versions\\active");
        List<string> completedOperationIds = [];

        for (var index = 0; index < 6; index++)
        {
            var operationId = tracker.CreateOperation($"1.21.{index}", $@"D:\\.minecraft\\versions\\1.21.{index}");
            tracker.CompleteOperation(operationId);
            completedOperationIds.Add(operationId);
            Thread.Sleep(2);
        }

        tracker.TryGetSnapshot(activeOperationId, out var activeSnapshot).Should().BeTrue();
        activeSnapshot.Should().NotBeNull();
        activeSnapshot!.IsTerminal.Should().BeFalse();

        tracker.TryGetSnapshot(completedOperationIds[0], out _).Should().BeFalse();
        foreach (var operationId in completedOperationIds.Skip(1))
        {
            tracker.TryGetSnapshot(operationId, out var snapshot).Should().BeTrue();
            snapshot!.IsTerminal.Should().BeTrue();
        }
    }
}