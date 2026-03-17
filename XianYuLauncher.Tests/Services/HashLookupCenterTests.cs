using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public class HashLookupCenterTests
{
    [Fact]
    public async Task GetOrFetchModrinthVersionsByHashesAsync_ShouldBatchRequestsWithinWindow()
    {
        var center = new HashLookupCenter();
        var fetchCalls = 0;

        async Task<Dictionary<string, ModrinthVersion>> FetchAsync(IReadOnlyCollection<string> hashes)
        {
            Interlocked.Increment(ref fetchCalls);
            await Task.Delay(20);
            return hashes.ToDictionary(
                hash => hash,
                hash => new ModrinthVersion { Id = hash, ProjectId = $"project-{hash}" },
                StringComparer.OrdinalIgnoreCase);
        }

        var task1 = center.GetOrFetchModrinthVersionsByHashesAsync(
            "scope-batch",
            new[] { "hash-a", "hash-b" },
            FetchAsync);

        var task2 = center.GetOrFetchModrinthVersionsByHashesAsync(
            "scope-batch",
            new[] { "hash-b", "hash-c" },
            FetchAsync);

        var results = await Task.WhenAll(task1, task2);

        Assert.Equal(1, fetchCalls);
        Assert.True(results[0].ContainsKey("hash-a"));
        Assert.True(results[0].ContainsKey("hash-b"));
        Assert.True(results[1].ContainsKey("hash-b"));
        Assert.True(results[1].ContainsKey("hash-c"));
    }

    [Fact]
    public async Task GetOrFetchModrinthVersionsByHashesAsync_ShouldRespectSuccessTtl()
    {
        var center = new HashLookupCenter();
        var fetchCalls = 0;

        Task<Dictionary<string, ModrinthVersion>> FetchAsync(IReadOnlyCollection<string> hashes)
        {
            Interlocked.Increment(ref fetchCalls);
            return Task.FromResult(hashes.ToDictionary(
                hash => hash,
                hash => new ModrinthVersion { Id = $"v-{fetchCalls}", ProjectId = "project" },
                StringComparer.OrdinalIgnoreCase));
        }

        await center.GetOrFetchModrinthVersionsByHashesAsync(
            "scope-ttl",
            new[] { "hash-ttl" },
            FetchAsync,
            successTtl: TimeSpan.FromMilliseconds(150),
            emptyTtl: TimeSpan.FromMilliseconds(80));

        await Task.Delay(50);

        await center.GetOrFetchModrinthVersionsByHashesAsync(
            "scope-ttl",
            new[] { "hash-ttl" },
            FetchAsync,
            successTtl: TimeSpan.FromMilliseconds(150),
            emptyTtl: TimeSpan.FromMilliseconds(80));

        await Task.Delay(200);

        await center.GetOrFetchModrinthVersionsByHashesAsync(
            "scope-ttl",
            new[] { "hash-ttl" },
            FetchAsync,
            successTtl: TimeSpan.FromMilliseconds(150),
            emptyTtl: TimeSpan.FromMilliseconds(80));

        Assert.Equal(2, fetchCalls);
    }

    [Fact]
    public async Task GetOrFetchModrinthVersionsByHashesAsync_ShouldPropagateRemoteException()
    {
        var center = new HashLookupCenter();

        Task<Dictionary<string, ModrinthVersion>> FetchAsync(IReadOnlyCollection<string> _)
            => throw new InvalidOperationException("boom");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            center.GetOrFetchModrinthVersionsByHashesAsync(
                "scope-error",
                new[] { "hash-error" },
                FetchAsync));

        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task GetOrFetchModrinthVersionsByHashesAsync_ShouldCancelWithoutHanging()
    {
        var center = new HashLookupCenter();
        using var cts = new CancellationTokenSource(50);

        async Task<Dictionary<string, ModrinthVersion>> FetchAsync(IReadOnlyCollection<string> _)
        {
            await Task.Delay(300);
            return new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase);
        }

        var task = center.GetOrFetchModrinthVersionsByHashesAsync(
            "scope-cancel",
            new[] { "hash-cancel" },
            FetchAsync,
            cancellationToken: cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task.WaitAsync(TimeSpan.FromSeconds(2)));
    }
}
