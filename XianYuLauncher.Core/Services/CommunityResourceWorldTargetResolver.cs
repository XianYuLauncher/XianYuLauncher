using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public sealed class CommunityResourceWorldTargetResolver : ICommunityResourceWorldTargetResolver
{
    private static readonly string[] WorldOnlyResourceTypes = ["world"];

    private readonly ICommunityResourceInventoryService _inventoryService;

    public CommunityResourceWorldTargetResolver(ICommunityResourceInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task<CommunityResourceWorldTargetResolutionResult> ResolveAsync(
        CommunityResourceWorldTargetResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TargetVersionName))
        {
            throw new ArgumentException("TargetVersionName 不能为空。", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ResolvedGameDirectory))
        {
            throw new ArgumentException("ResolvedGameDirectory 不能为空。", nameof(request));
        }

        string targetVersionName = request.TargetVersionName.Trim();
        string resolvedGameDirectory = Path.GetFullPath(request.ResolvedGameDirectory.Trim());
        string? targetWorldResourceId = NormalizeText(request.TargetWorldResourceId);

        CommunityResourceInventoryResult inventory = await _inventoryService.ListAsync(
            new CommunityResourceInventoryRequest
            {
                TargetVersionName = targetVersionName,
                ResolvedGameDirectory = resolvedGameDirectory,
                ResourceTypes = WorldOnlyResourceTypes,
            },
            cancellationToken).ConfigureAwait(false);

        List<CommunityResourceWorldTargetDescriptor> availableWorlds = inventory.Resources
            .Where(item => string.Equals(item.ResourceType, "world", StringComparison.OrdinalIgnoreCase))
            .Select(CreateDescriptor)
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.TargetSaveName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(targetWorldResourceId))
        {
            return new CommunityResourceWorldTargetResolutionResult
            {
                Status = CommunityResourceWorldTargetResolutionStatus.MissingWorldResourceId,
                AvailableWorlds = availableWorlds,
            };
        }

        if (!targetWorldResourceId.StartsWith("world:", StringComparison.OrdinalIgnoreCase))
        {
            return new CommunityResourceWorldTargetResolutionResult
            {
                Status = CommunityResourceWorldTargetResolutionStatus.InvalidWorldResourceId,
                RequestedTargetWorldResourceId = targetWorldResourceId,
                AvailableWorlds = availableWorlds,
            };
        }

        CommunityResourceWorldTargetDescriptor? resolvedWorld = availableWorlds.FirstOrDefault(item =>
            string.Equals(item.ResourceInstanceId, targetWorldResourceId, StringComparison.OrdinalIgnoreCase));

        if (resolvedWorld == null)
        {
            return new CommunityResourceWorldTargetResolutionResult
            {
                Status = CommunityResourceWorldTargetResolutionStatus.WorldNotFound,
                RequestedTargetWorldResourceId = targetWorldResourceId,
                AvailableWorlds = availableWorlds,
            };
        }

        return new CommunityResourceWorldTargetResolutionResult
        {
            Status = CommunityResourceWorldTargetResolutionStatus.Resolved,
            RequestedTargetWorldResourceId = targetWorldResourceId,
            ResolvedWorld = resolvedWorld,
            AvailableWorlds = availableWorlds,
        };
    }

    private static CommunityResourceWorldTargetDescriptor CreateDescriptor(CommunityResourceInventoryItem item)
    {
        return new CommunityResourceWorldTargetDescriptor
        {
            ResourceInstanceId = item.ResourceInstanceId,
            DisplayName = item.DisplayName,
            WorldName = NormalizeText(item.WorldName) ?? item.DisplayName,
            FilePath = item.FilePath,
            RelativePath = item.RelativePath,
            TargetSaveName = ExtractTargetSaveName(item),
        };
    }

    private static string ExtractTargetSaveName(CommunityResourceInventoryItem item)
    {
        string? relativePathSaveName = TryExtractTargetSaveNameFromRelativePath(item.RelativePath);
        if (!string.IsNullOrWhiteSpace(relativePathSaveName))
        {
            return relativePathSaveName;
        }

        string normalizedFilePath = item.FilePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? fileName = Path.GetFileName(normalizedFilePath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        throw new InvalidOperationException($"世界资源 {item.ResourceInstanceId} 缺少可用的存档目录名。");
    }

    private static string? TryExtractTargetSaveNameFromRelativePath(string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 || !segments[0].Equals(MinecraftPathConsts.Saves, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return NormalizeText(segments[1]);
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}