using System.Collections.Concurrent;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public sealed class HashLookupCenter : IHashLookupCenter
{
    private readonly ConcurrentDictionary<string, CacheEntry<ModrinthVersion?>> _modrinthHashCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ModrinthBatchScopeState> _modrinthBatchStates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CacheEntry<ModrinthProjectDetail?>> _modrinthProjectCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<ModrinthProjectDetail?>> _modrinthProjectInFlight = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, CacheEntry<CurseForgeFingerprintMatch?>> _curseForgeExactCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CacheEntry<bool>> _curseForgeUnmatchedCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CurseForgeBatchScopeState> _curseForgeBatchStates = new(StringComparer.Ordinal);

    private static readonly TimeSpan DefaultSuccessTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultEmptyTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BatchWindow = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);
    private const int CleanupTriggerThreshold = 256;

    private readonly object _cleanupSyncRoot = new();
    private DateTimeOffset _lastCleanupAt = DateTimeOffset.UtcNow;
    private int _cleanupCounter;

    public async Task<Dictionary<string, ModrinthVersion>> GetOrFetchModrinthVersionsByHashesAsync(
        string scope,
        IReadOnlyCollection<string> hashes,
        Func<IReadOnlyCollection<string>, Task<Dictionary<string, ModrinthVersion>>> fetchBatchAsync,
        TimeSpan? successTtl = null,
        TimeSpan? emptyTtl = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = hashes
            .Where(hash => !string.IsNullOrWhiteSpace(hash))
            .Select(hash => hash.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(hash => hash, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            return new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase);
        }

        TryCleanupExpiredCaches();

        var state = _modrinthBatchStates.GetOrAdd(scope, _ => new ModrinthBatchScopeState());
        var request = new ModrinthRequest(normalized);

        lock (state.SyncRoot)
        {
            state.PendingRequests.Add(request);
            if (!state.IsBatchScheduled)
            {
                state.IsBatchScheduled = true;
                _ = ProcessModrinthBatchAsync(scope, state, fetchBatchAsync, successTtl, emptyTtl);
            }
        }

        using (cancellationToken.Register(() => request.TrySetCanceled(cancellationToken)))
        {
            return await request.Task;
        }
    }

    public async Task<CurseForgeFingerprintMatchesResult> GetOrFetchCurseForgeMatchesByFingerprintsAsync(
        string scope,
        IReadOnlyCollection<uint> fingerprints,
        Func<IReadOnlyCollection<uint>, Task<CurseForgeFingerprintMatchesResult>> fetchBatchAsync,
        TimeSpan? successTtl = null,
        TimeSpan? emptyTtl = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = fingerprints
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        if (normalized.Count == 0)
        {
            return CreateEmptyCurseForgeResult();
        }

        TryCleanupExpiredCaches();

        var state = _curseForgeBatchStates.GetOrAdd(scope, _ => new CurseForgeBatchScopeState());
        var request = new CurseForgeRequest(normalized);

        lock (state.SyncRoot)
        {
            state.PendingRequests.Add(request);
            if (!state.IsBatchScheduled)
            {
                state.IsBatchScheduled = true;
                _ = ProcessCurseForgeBatchAsync(scope, state, fetchBatchAsync, successTtl, emptyTtl);
            }
        }

        using (cancellationToken.Register(() => request.TrySetCanceled(cancellationToken)))
        {
            return await request.Task;
        }
    }

    public async Task<ModrinthProjectDetail?> GetOrFetchModrinthProjectDetailAsync(
        string scope,
        string projectIdOrSlug,
        Func<string, Task<ModrinthProjectDetail?>> fetchAsync,
        TimeSpan? successTtl = null,
        TimeSpan? emptyTtl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectIdOrSlug))
        {
            return null;
        }

        var normalizedProject = projectIdOrSlug.Trim();
        var key = BuildPerItemKey(scope, normalizedProject);
        var successDuration = successTtl ?? DefaultSuccessTtl;
        var emptyDuration = emptyTtl ?? DefaultEmptyTtl;

        TryCleanupExpiredCaches();

        if (TryGetModrinthProjectCache(key, out var cached))
        {
            System.Diagnostics.Debug.WriteLine($"[HashLookupCenter][ModrinthProject] 缓存命中: scope={scope}, project={normalizedProject}");
            return cached;
        }

        System.Diagnostics.Debug.WriteLine($"[HashLookupCenter][ModrinthProject] 进入去重: scope={scope}, project={normalizedProject}");
        var inFlightTask = _modrinthProjectInFlight.GetOrAdd(key, _ => FetchModrinthProjectWithCacheAsync(
            key,
            scope,
            normalizedProject,
            fetchAsync,
            successDuration,
            emptyDuration));

        try
        {
            var result = await inFlightTask.WaitAsync(cancellationToken);
            return result;
        }
        finally
        {
            _modrinthProjectInFlight.TryRemove(key, out _);
        }
    }

    private async Task ProcessModrinthBatchAsync(
        string scope,
        ModrinthBatchScopeState state,
        Func<IReadOnlyCollection<string>, Task<Dictionary<string, ModrinthVersion>>> fetchBatchAsync,
        TimeSpan? successTtl,
        TimeSpan? emptyTtl)
    {
        await Task.Delay(BatchWindow);

        List<ModrinthRequest> requests;
        lock (state.SyncRoot)
        {
            requests = state.PendingRequests.ToList();
            state.PendingRequests.Clear();
            state.IsBatchScheduled = false;
        }

        if (requests.Count == 0)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[HashLookupCenter][Modrinth] 批次开始: scope={scope}, 请求数={requests.Count}");

        var successDuration = successTtl ?? DefaultSuccessTtl;
        var emptyDuration = emptyTtl ?? DefaultEmptyTtl;

        var unionHashes = requests
            .SelectMany(request => request.Hashes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missingHashes = unionHashes
            .Where(hash => !TryGetModrinthCache(scope, hash, out _))
            .ToList();

        var modrinthCacheHit = unionHashes.Count - missingHashes.Count;
        System.Diagnostics.Debug.WriteLine($"[HashLookupCenter][Modrinth] 聚合完成: scope={scope}, union={unionHashes.Count}, 缓存命中={modrinthCacheHit}, 待远端={missingHashes.Count}");

        Dictionary<string, ModrinthVersion> fetched = new(StringComparer.OrdinalIgnoreCase);
        if (missingHashes.Count > 0)
        {
            try
            {
                fetched = await fetchBatchAsync(missingHashes)
                    ?? new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase);
                System.Diagnostics.Debug.WriteLine($"[HashLookupCenter][Modrinth] 远端返回: scope={scope}, 请求hash={missingHashes.Count}, 命中版本={fetched.Count}");
            }
            catch (Exception ex)
            {
                foreach (var request in requests)
                {
                    request.TrySetException(ex);
                }

                return;
            }
        }

        foreach (var hash in missingHashes)
        {
            if (fetched.TryGetValue(hash, out var value) && value != null)
            {
                SetModrinthCache(scope, hash, value, successDuration);
            }
            else
            {
                SetModrinthCache(scope, hash, null, emptyDuration);
            }
        }

        foreach (var request in requests)
        {
            var result = new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase);
            foreach (var hash in request.Hashes)
            {
                if (TryGetModrinthCache(scope, hash, out var cached) && cached != null)
                {
                    result[hash] = cached;
                }
            }

            request.TrySetResult(result);
        }

        System.Diagnostics.Debug.WriteLine($"[HashLookupCenter][Modrinth] 批次结束: scope={scope}, 回填请求数={requests.Count}");
    }

    private async Task ProcessCurseForgeBatchAsync(
        string scope,
        CurseForgeBatchScopeState state,
        Func<IReadOnlyCollection<uint>, Task<CurseForgeFingerprintMatchesResult>> fetchBatchAsync,
        TimeSpan? successTtl,
        TimeSpan? emptyTtl)
    {
        await Task.Delay(BatchWindow);

        List<CurseForgeRequest> requests;
        lock (state.SyncRoot)
        {
            requests = state.PendingRequests.ToList();
            state.PendingRequests.Clear();
            state.IsBatchScheduled = false;
        }

        if (requests.Count == 0)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[HashLookupCenter][CurseForge] 批次开始: scope={scope}, 请求数={requests.Count}");

        var successDuration = successTtl ?? DefaultSuccessTtl;
        var emptyDuration = emptyTtl ?? DefaultEmptyTtl;

        var unionFingerprints = requests
            .SelectMany(request => request.Fingerprints)
            .Distinct()
            .ToList();

        var missingFingerprints = unionFingerprints
            .Where(fingerprint => !TryGetCurseForgeExact(scope, fingerprint, out _)
                                  && !TryGetCurseForgeUnmatched(scope, fingerprint, out _))
            .ToList();

        var curseForgeCacheHit = unionFingerprints.Count - missingFingerprints.Count;
        System.Diagnostics.Debug.WriteLine($"[HashLookupCenter][CurseForge] 聚合完成: scope={scope}, union={unionFingerprints.Count}, 缓存命中={curseForgeCacheHit}, 待远端={missingFingerprints.Count}");

        CurseForgeFingerprintMatchesResult fetched = CreateEmptyCurseForgeResult();
        if (missingFingerprints.Count > 0)
        {
            try
            {
                fetched = NormalizeCurseForgeResult(await fetchBatchAsync(missingFingerprints));
                System.Diagnostics.Debug.WriteLine($"[HashLookupCenter][CurseForge] 远端返回: scope={scope}, 请求fingerprint={missingFingerprints.Count}, 精确命中={fetched.ExactMatches.Count}, 未匹配={fetched.UnmatchedFingerprints.Count}");
            }
            catch (Exception ex)
            {
                foreach (var request in requests)
                {
                    request.TrySetException(ex);
                }

                return;
            }
        }

        var exactMatchesByFingerprint = fetched.ExactMatches
            .Where(match => match?.File != null)
            .GroupBy(match => (uint)Math.Clamp(match.File.FileFingerprint, 0, uint.MaxValue))
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var fingerprint in missingFingerprints)
        {
            if (exactMatchesByFingerprint.TryGetValue(fingerprint, out var exactMatch) && exactMatch != null)
            {
                SetCurseForgeExact(scope, fingerprint, exactMatch, successDuration);
                continue;
            }

            if (fetched.UnmatchedFingerprints.Contains(fingerprint))
            {
                SetCurseForgeUnmatched(scope, fingerprint, true, emptyDuration);
            }
        }

        foreach (var request in requests)
        {
            var response = CreateEmptyCurseForgeResult();

            foreach (var fingerprint in request.Fingerprints)
            {
                if (TryGetCurseForgeExact(scope, fingerprint, out var exact) && exact != null)
                {
                    response.ExactFingerprints.Add(fingerprint);
                    response.ExactMatches.Add(exact);
                    continue;
                }

                if (TryGetCurseForgeUnmatched(scope, fingerprint, out var isUnmatched) && isUnmatched)
                {
                    response.UnmatchedFingerprints.Add(fingerprint);
                }
            }

            request.TrySetResult(response);
        }

        System.Diagnostics.Debug.WriteLine($"[HashLookupCenter][CurseForge] 批次结束: scope={scope}, 回填请求数={requests.Count}");
    }

    private bool TryGetModrinthCache(string scope, string hash, out ModrinthVersion? value)
    {
        var key = BuildPerItemKey(scope, hash);
        var now = DateTimeOffset.UtcNow;

        if (_modrinthHashCache.TryGetValue(key, out var entry) && entry.ExpiresAt > now)
        {
            value = entry.Value;
            return true;
        }

        _modrinthHashCache.TryRemove(key, out _);
        value = null;
        return false;
    }

    private void SetModrinthCache(string scope, string hash, ModrinthVersion? value, TimeSpan ttl)
    {
        _modrinthHashCache[BuildPerItemKey(scope, hash)] = new CacheEntry<ModrinthVersion?>(value, DateTimeOffset.UtcNow.Add(ttl));
    }

    private bool TryGetCurseForgeExact(string scope, uint fingerprint, out CurseForgeFingerprintMatch? value)
    {
        var key = BuildPerItemKey(scope, fingerprint.ToString());
        var now = DateTimeOffset.UtcNow;

        if (_curseForgeExactCache.TryGetValue(key, out var entry) && entry.ExpiresAt > now)
        {
            value = entry.Value;
            return true;
        }

        _curseForgeExactCache.TryRemove(key, out _);
        value = null;
        return false;
    }

    private void SetCurseForgeExact(string scope, uint fingerprint, CurseForgeFingerprintMatch? value, TimeSpan ttl)
    {
        _curseForgeExactCache[BuildPerItemKey(scope, fingerprint.ToString())] =
            new CacheEntry<CurseForgeFingerprintMatch?>(value, DateTimeOffset.UtcNow.Add(ttl));
    }

    private bool TryGetCurseForgeUnmatched(string scope, uint fingerprint, out bool value)
    {
        var key = BuildPerItemKey(scope, fingerprint.ToString());
        var now = DateTimeOffset.UtcNow;

        if (_curseForgeUnmatchedCache.TryGetValue(key, out var entry) && entry.ExpiresAt > now)
        {
            value = entry.Value;
            return true;
        }

        _curseForgeUnmatchedCache.TryRemove(key, out _);
        value = false;
        return false;
    }

    private void SetCurseForgeUnmatched(string scope, uint fingerprint, bool value, TimeSpan ttl)
    {
        _curseForgeUnmatchedCache[BuildPerItemKey(scope, fingerprint.ToString())] =
            new CacheEntry<bool>(value, DateTimeOffset.UtcNow.Add(ttl));
    }

    private static string BuildPerItemKey(string scope, string item)
    {
        return $"{scope}|{item}";
    }

    private void TryCleanupExpiredCaches()
    {
        if (Interlocked.Increment(ref _cleanupCounter) < CleanupTriggerThreshold)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        lock (_cleanupSyncRoot)
        {
            if (_cleanupCounter < CleanupTriggerThreshold)
            {
                return;
            }

            if (now - _lastCleanupAt < CleanupInterval)
            {
                return;
            }

            _cleanupCounter = 0;
            _lastCleanupAt = now;
        }

        RemoveExpiredEntries(_modrinthHashCache, now);
        RemoveExpiredEntries(_modrinthProjectCache, now);
        RemoveExpiredEntries(_curseForgeExactCache, now);
        RemoveExpiredEntries(_curseForgeUnmatchedCache, now);
    }

    private static void RemoveExpiredEntries<TValue>(ConcurrentDictionary<string, CacheEntry<TValue>> cache, DateTimeOffset now)
    {
        foreach (var kv in cache)
        {
            if (kv.Value.ExpiresAt <= now)
            {
                cache.TryRemove(kv.Key, out _);
            }
        }
    }

    private bool TryGetModrinthProjectCache(string key, out ModrinthProjectDetail? value)
    {
        var now = DateTimeOffset.UtcNow;
        if (_modrinthProjectCache.TryGetValue(key, out var entry) && entry.ExpiresAt > now)
        {
            value = entry.Value;
            return true;
        }

        _modrinthProjectCache.TryRemove(key, out _);
        value = null;
        return false;
    }

    private async Task<ModrinthProjectDetail?> FetchModrinthProjectWithCacheAsync(
        string cacheKey,
        string scope,
        string projectIdOrSlug,
        Func<string, Task<ModrinthProjectDetail?>> fetchAsync,
        TimeSpan successTtl,
        TimeSpan emptyTtl)
    {
        try
        {
            var result = await fetchAsync(projectIdOrSlug);
            var ttl = result == null ? emptyTtl : successTtl;
            _modrinthProjectCache[cacheKey] = new CacheEntry<ModrinthProjectDetail?>(result, DateTimeOffset.UtcNow.Add(ttl));
            System.Diagnostics.Debug.WriteLine($"[HashLookupCenter][ModrinthProject] 远端返回: scope={scope}, project={projectIdOrSlug}, 有结果={result != null}");
            return result;
        }
        catch
        {
            throw;
        }
    }

    private static CurseForgeFingerprintMatchesResult CreateEmptyCurseForgeResult()
    {
        return new CurseForgeFingerprintMatchesResult
        {
            ExactMatches = new List<CurseForgeFingerprintMatch>(),
            ExactFingerprints = new List<uint>(),
            UnmatchedFingerprints = new List<uint>(),
            PartialMatches = new List<CurseForgeFingerprintMatch>(),
            PartialMatchFingerprints = new Dictionary<string, List<uint>>(),
            InstalledFingerprints = new List<uint>()
        };
    }

    private static CurseForgeFingerprintMatchesResult NormalizeCurseForgeResult(CurseForgeFingerprintMatchesResult? result)
    {
        if (result == null)
        {
            return CreateEmptyCurseForgeResult();
        }

        result.ExactMatches ??= new List<CurseForgeFingerprintMatch>();
        result.ExactFingerprints ??= new List<uint>();
        result.UnmatchedFingerprints ??= new List<uint>();
        result.PartialMatches ??= new List<CurseForgeFingerprintMatch>();
        result.PartialMatchFingerprints ??= new Dictionary<string, List<uint>>();
        result.InstalledFingerprints ??= new List<uint>();
        return result;
    }

    private sealed record CacheEntry<TValue>(TValue Value, DateTimeOffset ExpiresAt);

    private sealed class ModrinthBatchScopeState
    {
        public object SyncRoot { get; } = new();
        public List<ModrinthRequest> PendingRequests { get; } = new();
        public bool IsBatchScheduled { get; set; }
    }

    private sealed class CurseForgeBatchScopeState
    {
        public object SyncRoot { get; } = new();
        public List<CurseForgeRequest> PendingRequests { get; } = new();
        public bool IsBatchScheduled { get; set; }
    }

    private sealed class ModrinthRequest
    {
        private readonly TaskCompletionSource<Dictionary<string, ModrinthVersion>> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<string> Hashes { get; }
        public Task<Dictionary<string, ModrinthVersion>> Task => _tcs.Task;

        public ModrinthRequest(IReadOnlyList<string> hashes)
        {
            Hashes = hashes;
        }

        public void TrySetResult(Dictionary<string, ModrinthVersion> result) => _tcs.TrySetResult(result);
        public void TrySetException(Exception ex) => _tcs.TrySetException(ex);
        public void TrySetCanceled(CancellationToken token) => _tcs.TrySetCanceled(token);
    }

    private sealed class CurseForgeRequest
    {
        private readonly TaskCompletionSource<CurseForgeFingerprintMatchesResult> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<uint> Fingerprints { get; }
        public Task<CurseForgeFingerprintMatchesResult> Task => _tcs.Task;

        public CurseForgeRequest(IReadOnlyList<uint> fingerprints)
        {
            Fingerprints = fingerprints;
        }

        public void TrySetResult(CurseForgeFingerprintMatchesResult result) => _tcs.TrySetResult(result);
        public void TrySetException(Exception ex) => _tcs.TrySetException(ex);
        public void TrySetCanceled(CancellationToken token) => _tcs.TrySetCanceled(token);
    }
}
