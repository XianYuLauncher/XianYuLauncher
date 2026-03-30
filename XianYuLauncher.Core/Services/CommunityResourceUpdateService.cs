using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public sealed class CommunityResourceUpdateService : ICommunityResourceUpdateService
{
    private readonly ICommunityResourceUpdateCheckService _updateCheckService;
    private readonly IOperationQueueService _operationQueueService;
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly FallbackDownloadManager _fallbackDownloadManager;

    public CommunityResourceUpdateService(
        ICommunityResourceUpdateCheckService updateCheckService,
        IOperationQueueService operationQueueService,
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        FallbackDownloadManager fallbackDownloadManager)
    {
        _updateCheckService = updateCheckService;
        _operationQueueService = operationQueueService;
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _fallbackDownloadManager = fallbackDownloadManager;
    }

    public Task<string> StartUpdateAsync(
        CommunityResourceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetVersionName);

        string selectionMode = NormalizeSelectionMode(request.SelectionMode);
        if (selectionMode == CommunityResourceUpdateRequest.ExplicitSelectionMode &&
            !HasAnyRequestedIds(request.ResourceInstanceIds))
        {
            throw new InvalidOperationException("显式更新至少需要一个 resource_instance_id。");
        }

        OperationTaskRequest operationRequest = new()
        {
            TaskName = $"更新社区资源 ({request.TargetVersionName.Trim()})",
            TaskType = OperationTaskType.CommunityResourceUpdate,
            ScopeKey = BuildScopeKey(request.TargetVersionName),
            AllowParallel = true,
            ExecuteAsync = (context, token) => ExecuteUpdateAsync(request, selectionMode, context, token),
        };

        return _operationQueueService.EnqueueBackgroundAsync(operationRequest, cancellationToken);
    }

    private async Task ExecuteUpdateAsync(
        CommunityResourceUpdateRequest request,
        string selectionMode,
        OperationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        HashSet<string>? requestedIds = NormalizeRequestedIds(request.ResourceInstanceIds);
        CommunityResourceUpdateCheckResult checkResult = await _updateCheckService.CheckAsync(
            new CommunityResourceUpdateCheckRequest
            {
                TargetVersionName = request.TargetVersionName,
                ResolvedGameDirectory = request.ResolvedGameDirectory,
                ResourceInstanceIds = selectionMode == CommunityResourceUpdateRequest.AllUpdatableSelectionMode ? null : requestedIds,
            },
            cancellationToken);

        List<CommunityResourceUpdateCheckItem> selectedItems = SelectItems(checkResult.Items, selectionMode, requestedIds);
        UpdateExecutionSummary summary = new()
        {
            Missing = CalculateMissingCount(requestedIds, selectedItems),
        };

        if (selectedItems.Count == 0)
        {
            context.ReportProgress(BuildSummaryMessage(summary), 100);
            return;
        }

        for (int index = 0; index < selectedItems.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CommunityResourceUpdateCheckItem item = selectedItems[index];
            string normalizedStatus = NormalizeStatus(item.Status);

            switch (normalizedStatus)
            {
                case "update_available":
                    bool updated = await TryUpdateItemAsync(item, index, selectedItems.Count, context, cancellationToken);
                    if (updated)
                    {
                        summary.Updated++;
                    }
                    else
                    {
                        summary.Failed++;
                    }
                    break;
                case "up_to_date":
                    summary.UpToDate++;
                    break;
                case "unsupported":
                    summary.Unsupported++;
                    break;
                case "not_identified":
                    summary.NotIdentified++;
                    break;
                default:
                    summary.Failed++;
                    break;
            }

            context.ReportProgress(
                BuildProgressMessage(summary, item.DisplayName, index + 1, selectedItems.Count),
                ComputeProgress(index + 1, selectedItems.Count));
        }

        context.ReportProgress(BuildSummaryMessage(summary), 100);
    }

    private async Task<bool> TryUpdateItemAsync(
        CommunityResourceUpdateCheckItem item,
        int completedCount,
        int totalCount,
        OperationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        DownloadCandidate? candidate = await ResolveDownloadCandidateAsync(item, cancellationToken);
        if (candidate == null)
        {
            return false;
        }

        string targetDirectory = Path.GetDirectoryName(item.FilePath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return false;
        }

        Directory.CreateDirectory(targetDirectory);

        string tempFilePath = Path.Combine(targetDirectory, $"{candidate.FileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            FallbackDownloadResult downloadResult = await _fallbackDownloadManager.DownloadFileForCommunityWithStatusAsync(
                NormalizeCommunityDownloadUrl(candidate.DownloadUrl, candidate.Provider),
                tempFilePath,
                GetCommunityFallbackResourceType(candidate.Provider),
                status => context.ReportProgress(
                    $"正在下载 {item.DisplayName} ({completedCount + 1}/{totalCount})",
                    ComputeProgress(completedCount, totalCount, status.Percent)),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!downloadResult.Success)
            {
                SafeDelete(tempFilePath);
                return false;
            }

            if (!await ValidateDownloadedFileAsync(tempFilePath, candidate.ExpectedSha1, cancellationToken))
            {
                SafeDelete(tempFilePath);
                return false;
            }

            string finalPath = BuildFinalPath(item, candidate.FileName);
            ReplaceDownloadedFile(item, tempFilePath, finalPath);
            return true;
        }
        catch (OperationCanceledException)
        {
            SafeDelete(tempFilePath);
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CommunityResourceUpdate] 更新失败: {item.ResourceInstanceId}, {ex.Message}");
            SafeDelete(tempFilePath);
            return false;
        }
    }

    private async Task<DownloadCandidate?> ResolveDownloadCandidateAsync(
        CommunityResourceUpdateCheckItem item,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommunityResourceProvider provider = ResolveProvider(item.Provider ?? item.Source);
        if (provider == CommunityResourceProvider.Unknown)
        {
            return null;
        }

        if (provider == CommunityResourceProvider.Modrinth)
        {
            if (string.IsNullOrWhiteSpace(item.LatestResourceFileId))
            {
                return null;
            }

            ModrinthVersion? version = await _modrinthService.GetVersionByIdAsync(item.LatestResourceFileId);
            ModrinthVersionFile? file = version?.Files.FirstOrDefault(candidate => candidate.Primary) ?? version?.Files.FirstOrDefault();
            if (file == null || string.IsNullOrWhiteSpace(file.Filename) || file.Url == null)
            {
                return null;
            }

            file.Hashes.TryGetValue("sha1", out string? sha1);
            return new DownloadCandidate
            {
                Provider = provider,
                DownloadUrl = file.Url.ToString(),
                FileName = file.Filename,
                ExpectedSha1 = sha1,
            };
        }

        if (!int.TryParse(item.ProjectId, out int modId) ||
            !int.TryParse(item.LatestResourceFileId, out int fileId))
        {
            return null;
        }

        CurseForgeFile? curseForgeFile = await _curseForgeService.GetFileAsync(modId, fileId);
        if (curseForgeFile == null || string.IsNullOrWhiteSpace(curseForgeFile.FileName))
        {
            return null;
        }

        string downloadUrl = string.IsNullOrWhiteSpace(curseForgeFile.DownloadUrl)
            ? _curseForgeService.ConstructDownloadUrl(curseForgeFile.Id, curseForgeFile.FileName)
            : curseForgeFile.DownloadUrl;

        string? expectedSha1 = curseForgeFile.Hashes?
            .FirstOrDefault(hash => hash.Algo == 1)?.Value;

        return new DownloadCandidate
        {
            Provider = provider,
            DownloadUrl = downloadUrl,
            FileName = curseForgeFile.FileName,
            ExpectedSha1 = expectedSha1,
        };
    }

    private static List<CommunityResourceUpdateCheckItem> SelectItems(
        IReadOnlyList<CommunityResourceUpdateCheckItem> items,
        string selectionMode,
        HashSet<string>? requestedIds)
    {
        if (selectionMode == CommunityResourceUpdateRequest.AllUpdatableSelectionMode)
        {
            return items
                .Where(item => NormalizeStatus(item.Status) == "update_available")
                .ToList();
        }

        if (requestedIds == null || requestedIds.Count == 0)
        {
            return [];
        }

        return items
            .Where(item => requestedIds.Contains(item.ResourceInstanceId))
            .ToList();
    }

    private static int CalculateMissingCount(
        HashSet<string>? requestedIds,
        IReadOnlyList<CommunityResourceUpdateCheckItem> selectedItems)
    {
        if (requestedIds == null || requestedIds.Count == 0)
        {
            return 0;
        }

        HashSet<string> selectedIds = selectedItems
            .Select(item => item.ResourceInstanceId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return requestedIds.Count(id => !selectedIds.Contains(id));
    }

    private static string BuildScopeKey(string targetVersionName)
    {
        return $"version:{targetVersionName.Trim()}";
    }

    private static string NormalizeSelectionMode(string? selectionMode)
    {
        string normalized = string.IsNullOrWhiteSpace(selectionMode)
            ? CommunityResourceUpdateRequest.ExplicitSelectionMode
            : selectionMode.Trim().ToLowerInvariant();

        return normalized switch
        {
            CommunityResourceUpdateRequest.ExplicitSelectionMode => CommunityResourceUpdateRequest.ExplicitSelectionMode,
            CommunityResourceUpdateRequest.AllUpdatableSelectionMode => CommunityResourceUpdateRequest.AllUpdatableSelectionMode,
            _ => throw new InvalidOperationException($"不支持的选择模式: {selectionMode}")
        };
    }

    private static bool HasAnyRequestedIds(IReadOnlyCollection<string>? resourceInstanceIds)
    {
        return resourceInstanceIds != null && resourceInstanceIds.Any(id => !string.IsNullOrWhiteSpace(id));
    }

    private static HashSet<string>? NormalizeRequestedIds(IReadOnlyCollection<string>? resourceInstanceIds)
    {
        if (resourceInstanceIds == null || resourceInstanceIds.Count == 0)
        {
            return null;
        }

        HashSet<string> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach (string resourceInstanceId in resourceInstanceIds)
        {
            if (!string.IsNullOrWhiteSpace(resourceInstanceId))
            {
                normalized.Add(resourceInstanceId.Trim());
            }
        }

        return normalized.Count == 0 ? null : normalized;
    }

    private static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? string.Empty
            : status.Trim().ToLowerInvariant();
    }

    private static string BuildProgressMessage(UpdateExecutionSummary summary, string displayName, int completedItems, int totalItems)
    {
        return $"已处理 {completedItems}/{totalItems} 项，当前资源：{displayName}。已更新 {summary.Updated}，失败 {summary.Failed}，已是最新 {summary.UpToDate}。";
    }

    private static string BuildSummaryMessage(UpdateExecutionSummary summary)
    {
        return $"社区资源更新完成：已更新 {summary.Updated}，失败 {summary.Failed}，已是最新 {summary.UpToDate}，不支持 {summary.Unsupported}，未识别 {summary.NotIdentified}，未找到 {summary.Missing}。";
    }

    private static double ComputeProgress(int completedItems, int totalItems, double itemProgress = 0)
    {
        if (totalItems <= 0)
        {
            return 100;
        }

        double completedProgress = completedItems / (double)totalItems * 100;
        double partialProgress = Math.Clamp(itemProgress, 0, 100) / totalItems;
        return Math.Min(99.9, completedProgress + partialProgress);
    }

    private static CommunityResourceProvider ResolveProvider(string? provider)
    {
        if (string.Equals(provider, "Modrinth", StringComparison.OrdinalIgnoreCase))
        {
            return CommunityResourceProvider.Modrinth;
        }

        if (string.Equals(provider, "CurseForge", StringComparison.OrdinalIgnoreCase))
        {
            return CommunityResourceProvider.CurseForge;
        }

        return CommunityResourceProvider.Unknown;
    }

    private static string GetCommunityFallbackResourceType(CommunityResourceProvider provider)
    {
        return provider switch
        {
            CommunityResourceProvider.Modrinth => "modrinth_cdn",
            CommunityResourceProvider.CurseForge => "curseforge_cdn",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "未知的社区资源提供方")
        };
    }

    private static string NormalizeCommunityDownloadUrl(string url, CommunityResourceProvider provider)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        return provider switch
        {
            CommunityResourceProvider.Modrinth when url.Contains("https://mod.mcimirror.top", StringComparison.OrdinalIgnoreCase)
                => url.Replace("https://mod.mcimirror.top", "https://cdn.modrinth.com", StringComparison.OrdinalIgnoreCase),
            CommunityResourceProvider.CurseForge when url.Contains("https://mod.mcimirror.top", StringComparison.OrdinalIgnoreCase)
                => url.Replace("https://mod.mcimirror.top", "https://edge.forgecdn.net", StringComparison.OrdinalIgnoreCase),
            _ => url
        };
    }

    private static string BuildFinalPath(CommunityResourceUpdateCheckItem item, string downloadedFileName)
    {
        string fileName = downloadedFileName;
        if (item.ResourceType.Equals("mod", StringComparison.OrdinalIgnoreCase) &&
            item.FilePath.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase) &&
            !fileName.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            fileName += FileExtensionConsts.Disabled;
        }

        string targetDirectory = Path.GetDirectoryName(item.FilePath) ?? string.Empty;
        return Path.Combine(targetDirectory, fileName);
    }

    private static void ReplaceDownloadedFile(CommunityResourceUpdateCheckItem item, string tempFilePath, string finalPath)
    {
        if (item.ResourceType.Equals("shader", StringComparison.OrdinalIgnoreCase))
        {
            SafeDelete($"{item.FilePath}.txt");
        }

        SafeDelete(item.FilePath);
        if (!string.Equals(item.FilePath, finalPath, StringComparison.OrdinalIgnoreCase))
        {
            SafeDelete(finalPath);
        }

        File.Move(tempFilePath, finalPath);
    }

    private static async Task<bool> ValidateDownloadedFileAsync(
        string filePath,
        string? expectedSha1,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expectedSha1))
        {
            return true;
        }

        await using FileStream stream = File.OpenRead(filePath);
        using SHA1 sha1 = SHA1.Create();
        byte[] hash = await sha1.ComputeHashAsync(stream, cancellationToken);
        string actualSha1 = Convert.ToHexString(hash).ToLowerInvariant();
        return actualSha1.Equals(expectedSha1.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static void SafeDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private sealed class DownloadCandidate
    {
        public required CommunityResourceProvider Provider { get; init; }

        public required string DownloadUrl { get; init; }

        public required string FileName { get; init; }

        public string? ExpectedSha1 { get; init; }
    }

    private sealed class UpdateExecutionSummary
    {
        public int Updated { get; set; }

        public int Failed { get; set; }

        public int UpToDate { get; set; }

        public int Unsupported { get; set; }

        public int NotIdentified { get; set; }

        public int Missing { get; set; }
    }
}