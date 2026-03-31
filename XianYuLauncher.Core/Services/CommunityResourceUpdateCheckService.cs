using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public sealed class CommunityResourceUpdateCheckService : ICommunityResourceUpdateCheckService
{
    private static readonly string[] InventoryResourceTypes =
    {
        "mod",
        "shader",
        "resourcepack",
        "world",
        "datapack",
    };

    private static readonly HashSet<string> InventoryResourceTypeSet = new(StringComparer.OrdinalIgnoreCase)
    {
        "mod",
        "shader",
        "resourcepack",
        "world",
        "datapack",
    };

    private static readonly HashSet<string> SupportedResourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "mod",
        "shader",
        "resourcepack",
    };

    private readonly ICommunityResourceInventoryService _inventoryService;
    private readonly IGameDirResolver _gameDirResolver;
    private readonly IFileService _fileService;
    private readonly IVersionInfoService _versionInfoService;
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;

    public CommunityResourceUpdateCheckService(
        ICommunityResourceInventoryService inventoryService,
        IGameDirResolver gameDirResolver,
        IFileService fileService,
        IVersionInfoService versionInfoService,
        ModrinthService modrinthService,
        CurseForgeService curseForgeService)
    {
        _inventoryService = inventoryService;
        _gameDirResolver = gameDirResolver;
        _fileService = fileService;
        _versionInfoService = versionInfoService;
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
    }

    public async Task<CommunityResourceUpdateCheckResult> CheckAsync(
        CommunityResourceUpdateCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetVersionName);

        string resolvedGameDirectory = await ResolveGameDirectoryAsync(request, cancellationToken);
        VersionConfig versionConfig = await ResolveVersionConfigAsync(request.TargetVersionName, cancellationToken);
        string minecraftVersion = ResolveMinecraftVersion(versionConfig, request.TargetVersionName);
        string modLoaderType = DetermineModLoaderType(versionConfig, request.TargetVersionName);
        IReadOnlyCollection<string> requestedResourceTypes = ResolveRequestedResourceTypes(request.ResourceTypes);

        CommunityResourceInventoryResult inventoryResult = await _inventoryService.ListAsync(
            new CommunityResourceInventoryRequest
            {
                TargetVersionName = request.TargetVersionName,
                ResolvedGameDirectory = resolvedGameDirectory,
                ResourceTypes = requestedResourceTypes,
            },
            cancellationToken);

        HashSet<string>? requestedIds = NormalizeRequestedIds(request.ResourceInstanceIds);
        List<CommunityResourceInventoryItem> resources = inventoryResult.Resources
            .Where(item => requestedIds == null || requestedIds.Contains(item.ResourceInstanceId))
            .ToList();

        List<CommunityResourceUpdateCheckItem> items = resources
            .Select(CreateBaseItem)
            .ToList();

        Dictionary<string, CommunityResourceUpdateCheckItem> itemsById = items
            .ToDictionary(item => item.ResourceInstanceId, StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, CommunityResourceInventoryItem> group in resources
                     .Where(IsSupportedFileResource)
                     .GroupBy(item => item.ResourceType, StringComparer.OrdinalIgnoreCase))
        {
            List<CommunityResourceInventoryItem> unresolved = await ApplyModrinthResultsAsync(
                group.ToList(),
                itemsById,
                minecraftVersion,
                modLoaderType,
                cancellationToken);

            if (unresolved.Count > 0)
            {
                await ApplyCurseForgeResultsAsync(
                    unresolved,
                    itemsById,
                    minecraftVersion,
                    GetCurseForgeLoader(group.Key, modLoaderType),
                    cancellationToken);
            }
        }

        return new CommunityResourceUpdateCheckResult
        {
            TargetVersionName = request.TargetVersionName,
            ResolvedGameDirectory = resolvedGameDirectory,
            MinecraftVersion = minecraftVersion,
            ModLoaderType = modLoaderType,
            CheckedAt = DateTimeOffset.UtcNow,
            Items = items,
        };
    }

    private static IReadOnlyCollection<string> ResolveRequestedResourceTypes(IReadOnlyCollection<string>? resourceTypes)
    {
        if (resourceTypes == null || resourceTypes.Count == 0)
        {
            return InventoryResourceTypes;
        }

        HashSet<string> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach (string resourceType in resourceTypes)
        {
            string? value = NormalizeRequestedResourceType(resourceType);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!InventoryResourceTypeSet.Contains(value))
            {
                throw new ArgumentException($"不支持的资源类型: {resourceType}", nameof(resourceTypes));
            }

            normalized.Add(value);
        }

        return normalized.Count == 0 ? InventoryResourceTypes : normalized.ToList();
    }

    private static string? NormalizeRequestedResourceType(string? resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            return null;
        }

        return ModResourcePathHelper.NormalizeProjectType(resourceType.Trim().ToLowerInvariant());
    }

    private async Task<string> ResolveGameDirectoryAsync(
        CommunityResourceUpdateCheckRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string resolvedGameDirectory = request.ResolvedGameDirectory ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedGameDirectory))
        {
            resolvedGameDirectory = await _gameDirResolver.GetGameDirForVersionAsync(request.TargetVersionName);
        }

        if (string.IsNullOrWhiteSpace(resolvedGameDirectory))
        {
            throw new InvalidOperationException($"无法解析实例的游戏目录: {request.TargetVersionName}");
        }

        return Path.GetFullPath(resolvedGameDirectory);
    }

    private async Task<VersionConfig> ResolveVersionConfigAsync(
        string targetVersionName,
        CancellationToken cancellationToken)
    {
        VersionConfig merged = _versionInfoService.ExtractVersionConfigFromName(targetVersionName) ?? new VersionConfig();
        string versionDirectory = Path.Combine(_fileService.GetMinecraftDataPath(), MinecraftPathConsts.Versions, targetVersionName);

        if (!Directory.Exists(versionDirectory))
        {
            return merged;
        }

        cancellationToken.ThrowIfCancellationRequested();

        VersionConfig? fullVersionInfo = await _versionInfoService.GetFullVersionInfoAsync(
            targetVersionName,
            versionDirectory,
            preferCache: true);

        if (!string.IsNullOrWhiteSpace(fullVersionInfo.MinecraftVersion))
        {
            merged.MinecraftVersion = fullVersionInfo.MinecraftVersion;
        }

        if (!string.IsNullOrWhiteSpace(fullVersionInfo.ModLoaderType))
        {
            merged.ModLoaderType = fullVersionInfo.ModLoaderType;
        }

        if (!string.IsNullOrWhiteSpace(fullVersionInfo.ModLoaderVersion))
        {
            merged.ModLoaderVersion = fullVersionInfo.ModLoaderVersion;
        }

        return merged;
    }

    private async Task<List<CommunityResourceInventoryItem>> ApplyModrinthResultsAsync(
        IReadOnlyList<CommunityResourceInventoryItem> resources,
        IReadOnlyDictionary<string, CommunityResourceUpdateCheckItem> itemsById,
        string minecraftVersion,
        string modLoaderType,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> hashByResourceId = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> distinctHashes = new(StringComparer.OrdinalIgnoreCase);

        foreach (CommunityResourceInventoryItem resource in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string hash = await ComputeSha1Async(resource.FilePath, cancellationToken);
                hashByResourceId[resource.ResourceInstanceId] = hash;
                distinctHashes.Add(hash);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is CryptographicException)
            {
                System.Diagnostics.Debug.WriteLine($"[CommunityResourceUpdateCheck] 计算 SHA1 失败: {resource.FilePath}, {ex.Message}");
            }
        }

        if (distinctHashes.Count == 0)
        {
            return resources.ToList();
        }

        string[] modrinthLoaders = GetModrinthLoaders(resources[0].ResourceType, modLoaderType);
        string[] gameVersions = new[] { minecraftVersion };

        Dictionary<string, ModrinthVersion> currentVersions = await _modrinthService.GetVersionFilesByHashesAsync(
            distinctHashes.ToList(),
            "sha1")
            ?? new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, ModrinthVersion> latestVersions = await _modrinthService.UpdateVersionFilesAsync(
            distinctHashes.ToList(),
            modrinthLoaders,
            gameVersions,
            "sha1")
            ?? new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase);

        List<CommunityResourceInventoryItem> unresolved = new();

        foreach (CommunityResourceInventoryItem resource in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!hashByResourceId.TryGetValue(resource.ResourceInstanceId, out string? hash) ||
                !itemsById.TryGetValue(resource.ResourceInstanceId, out CommunityResourceUpdateCheckItem? item))
            {
                unresolved.Add(resource);
                continue;
            }

            bool identified = false;

            if (currentVersions.TryGetValue(hash, out ModrinthVersion? currentVersion))
            {
                ApplyModrinthIdentity(item, currentVersion);
                item.CurrentVersion = BuildModrinthVersionDisplay(currentVersion, resource.CurrentVersionHint);
                identified = true;
            }

            if (latestVersions.TryGetValue(hash, out ModrinthVersion? latestVersion) &&
                latestVersion?.Files != null &&
                latestVersion.Files.Count > 0)
            {
                ApplyModrinthIdentity(item, latestVersion);
                item.LatestVersion = BuildModrinthVersionDisplay(latestVersion, item.CurrentVersion ?? resource.CurrentVersionHint);
                item.LatestResourceFileId = latestVersion.Id;

                ModrinthVersionFile primaryFile = latestVersion.Files.FirstOrDefault(file => file.Primary) ?? latestVersion.Files[0];
                bool hasUpdate = true;
                if (primaryFile.Hashes.TryGetValue("sha1", out string? remoteSha1) && !string.IsNullOrWhiteSpace(remoteSha1))
                {
                    hasUpdate = !hash.Equals(remoteSha1, StringComparison.OrdinalIgnoreCase);
                }

                item.HasUpdate = hasUpdate;
                item.Status = hasUpdate ? "update_available" : "up_to_date";
                if (!hasUpdate)
                {
                    item.LatestVersion ??= item.CurrentVersion;
                }

                identified = true;
            }
            else if (identified)
            {
                item.HasUpdate = false;
                item.Status = "up_to_date";
                item.LatestVersion = item.CurrentVersion;
                item.LatestResourceFileId ??= currentVersion?.Id;
            }

            if (!identified)
            {
                unresolved.Add(resource);
            }
        }

        return unresolved;
    }

    private async Task ApplyCurseForgeResultsAsync(
        IReadOnlyList<CommunityResourceInventoryItem> resources,
        IReadOnlyDictionary<string, CommunityResourceUpdateCheckItem> itemsById,
        string minecraftVersion,
        string? modLoaderType,
        CancellationToken cancellationToken)
    {
        Dictionary<uint, List<CommunityResourceInventoryItem>> resourcesByFingerprint = new();

        foreach (CommunityResourceInventoryItem resource in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                uint fingerprint = CurseForgeFingerprintHelper.ComputeFingerprint(resource.FilePath);
                if (!resourcesByFingerprint.TryGetValue(fingerprint, out List<CommunityResourceInventoryItem>? matchedResources))
                {
                    matchedResources = new List<CommunityResourceInventoryItem>();
                    resourcesByFingerprint[fingerprint] = matchedResources;
                }

                matchedResources.Add(resource);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[CommunityResourceUpdateCheck] 计算 Fingerprint 失败: {resource.FilePath}, {ex.Message}");
            }
        }

        if (resourcesByFingerprint.Count == 0)
        {
            return;
        }

        CurseForgeFingerprintMatchesResult matchResult = await _curseForgeService.GetFingerprintMatchesAsync(
            resourcesByFingerprint.Keys.ToList());

        Dictionary<uint, CurseForgeFingerprintMatch> exactMatches = (matchResult.ExactMatches ?? new List<CurseForgeFingerprintMatch>())
            .Where(match => match?.File != null)
            .GroupBy(match => (uint)match.File.FileFingerprint)
            .ToDictionary(group => group.Key, group => group.First());

        foreach ((uint fingerprint, List<CommunityResourceInventoryItem> matchedResources) in resourcesByFingerprint)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!exactMatches.TryGetValue(fingerprint, out CurseForgeFingerprintMatch? match) || match.File == null)
            {
                continue;
            }

            CurseForgeFile? latestFile = SelectLatestCurseForgeFile(match, minecraftVersion, modLoaderType);

            foreach (CommunityResourceInventoryItem resource in matchedResources)
            {
                if (!itemsById.TryGetValue(resource.ResourceInstanceId, out CommunityResourceUpdateCheckItem? item))
                {
                    continue;
                }

                item.Provider = "CurseForge";
                item.Source ??= "CurseForge";
                item.ProjectId ??= match.Id.ToString(CultureInfo.InvariantCulture);
                item.CurrentVersion = BuildCurseForgeVersionDisplay(match.File, resource.CurrentVersionHint);

                if (latestFile == null)
                {
                    item.HasUpdate = false;
                    item.Status = "up_to_date";
                    item.LatestVersion = item.CurrentVersion;
                    item.LatestResourceFileId = match.File.Id.ToString(CultureInfo.InvariantCulture);
                    continue;
                }

                item.LatestVersion = BuildCurseForgeVersionDisplay(latestFile, item.CurrentVersion);
                item.LatestResourceFileId = latestFile.Id.ToString(CultureInfo.InvariantCulture);
                item.HasUpdate = latestFile.FileFingerprint != match.File.FileFingerprint;
                item.Status = item.HasUpdate ? "update_available" : "up_to_date";
            }
        }
    }

    private static CommunityResourceUpdateCheckItem CreateBaseItem(CommunityResourceInventoryItem resource)
    {
        CommunityResourceUpdateCheckItem item = new()
        {
            ResourceInstanceId = resource.ResourceInstanceId,
            ResourceType = resource.ResourceType,
            DisplayName = resource.DisplayName,
            FilePath = resource.FilePath,
            RelativePath = resource.RelativePath,
            IsDirectory = resource.IsDirectory,
            WorldName = resource.WorldName,
            PackFormat = resource.PackFormat,
            Source = resource.Source,
            ProjectId = resource.ProjectId,
            Provider = resource.Source,
            CurrentVersion = resource.CurrentVersionHint,
            Status = "not_identified",
        };

        if (resource.ResourceType.Equals("world", StringComparison.OrdinalIgnoreCase) ||
            resource.ResourceType.Equals("datapack", StringComparison.OrdinalIgnoreCase))
        {
            item.Status = "unsupported";
            item.UnsupportedReason = resource.UpdateUnsupportedReason ?? "该资源类型当前不支持统一更新检查。";
            return item;
        }

        if (resource.IsDirectory &&
            (resource.ResourceType.Equals("shader", StringComparison.OrdinalIgnoreCase) ||
             resource.ResourceType.Equals("resourcepack", StringComparison.OrdinalIgnoreCase)))
        {
            item.Status = "unsupported";
            item.UnsupportedReason = "目录型资源当前不支持统一更新检查。";
            return item;
        }

        return item;
    }

    private static bool IsSupportedFileResource(CommunityResourceInventoryItem resource)
    {
        return SupportedResourceTypes.Contains(resource.ResourceType) &&
               !resource.IsDirectory &&
               File.Exists(resource.FilePath);
    }

    private static HashSet<string>? NormalizeRequestedIds(IReadOnlyCollection<string>? resourceInstanceIds)
    {
        if (resourceInstanceIds == null || resourceInstanceIds.Count == 0)
        {
            return null;
        }

        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        foreach (string resourceInstanceId in resourceInstanceIds)
        {
            if (!string.IsNullOrWhiteSpace(resourceInstanceId))
            {
                ids.Add(resourceInstanceId.Trim());
            }
        }

        return ids.Count == 0 ? null : ids;
    }

    private static string ResolveMinecraftVersion(VersionConfig versionConfig, string targetVersionName)
    {
        if (!string.IsNullOrWhiteSpace(versionConfig.MinecraftVersion))
        {
            return versionConfig.MinecraftVersion;
        }

        return targetVersionName;
    }

    private static string DetermineModLoaderType(VersionConfig? versionConfig, string versionName)
    {
        if (versionConfig != null && !string.IsNullOrEmpty(versionConfig.ModLoaderType))
        {
            if (versionConfig.ModLoaderType.Equals("LegacyFabric", StringComparison.OrdinalIgnoreCase))
            {
                return "LegacyFabric";
            }

            if (versionConfig.ModLoaderType.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
            {
                return "NeoForge";
            }

            if (versionConfig.ModLoaderType.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase))
            {
                return "LiteLoader";
            }

            return versionConfig.ModLoaderType.ToLowerInvariant();
        }

        if (string.IsNullOrEmpty(versionName))
        {
            return "fabric";
        }

        if (versionName.Contains("legacyfabric", StringComparison.OrdinalIgnoreCase) ||
            versionName.Contains("legacy-fabric", StringComparison.OrdinalIgnoreCase))
        {
            return "LegacyFabric";
        }

        if (versionName.Contains("fabric", StringComparison.OrdinalIgnoreCase))
        {
            return "fabric";
        }

        if (versionName.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
        {
            return "neoforge";
        }

        if (versionName.Contains("forge", StringComparison.OrdinalIgnoreCase))
        {
            return "forge";
        }

        if (versionName.Contains("quilt", StringComparison.OrdinalIgnoreCase))
        {
            return "quilt";
        }

        if (versionName.Contains("liteloader", StringComparison.OrdinalIgnoreCase))
        {
            return "LiteLoader";
        }

        return "fabric";
    }

    private static string[] GetModrinthLoaders(string resourceType, string modLoaderType)
    {
        if (resourceType.Equals("shader", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "iris", "optifine", "minecraft" };
        }

        if (resourceType.Equals("resourcepack", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "minecraft" };
        }

        return new[] { NormalizeLoader(modLoaderType) };
    }

    private static string? GetCurseForgeLoader(string resourceType, string modLoaderType)
    {
        if (resourceType.Equals("mod", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeLoader(modLoaderType);
        }

        return null;
    }

    private static string NormalizeLoader(string modLoaderType)
    {
        return string.IsNullOrWhiteSpace(modLoaderType)
            ? "fabric"
            : modLoaderType.Trim().ToLowerInvariant();
    }

    private static void ApplyModrinthIdentity(CommunityResourceUpdateCheckItem item, ModrinthVersion version)
    {
        item.Provider = "Modrinth";
        item.Source ??= "Modrinth";
        if (!string.IsNullOrWhiteSpace(version.ProjectId))
        {
            item.ProjectId ??= version.ProjectId;
        }
    }

    private static CurseForgeFile? SelectLatestCurseForgeFile(
        CurseForgeFingerprintMatch match,
        string minecraftVersion,
        string? modLoaderType)
    {
        List<CurseForgeFile> compatibleFiles = (match.LatestFiles ?? new List<CurseForgeFile>())
            .Where(file => file.GameVersions != null &&
                           file.GameVersions.Contains(minecraftVersion, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (!string.IsNullOrWhiteSpace(modLoaderType) && compatibleFiles.Count > 0)
        {
            List<CurseForgeFile> loaderCompatibleFiles = compatibleFiles
                .Where(file => file.GameVersions != null &&
                               file.GameVersions.Any(version => version.Equals(modLoaderType, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (loaderCompatibleFiles.Count > 0)
            {
                compatibleFiles = loaderCompatibleFiles;
            }
        }

        return compatibleFiles
            .OrderByDescending(file => file.FileDate)
            .FirstOrDefault();
    }

    private static string BuildModrinthVersionDisplay(ModrinthVersion? version, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(version?.VersionNumber))
        {
            return version.VersionNumber;
        }

        if (!string.IsNullOrWhiteSpace(version?.Name))
        {
            return version.Name;
        }

        return fallback ?? string.Empty;
    }

    private static string BuildCurseForgeVersionDisplay(CurseForgeFile? file, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(file?.DisplayName))
        {
            return file.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(file?.FileName))
        {
            return file.FileName;
        }

        return fallback ?? string.Empty;
    }

    private static async Task<string> ComputeSha1Async(string filePath, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(filePath);
        using SHA1 sha1 = SHA1.Create();
        byte[] hashBytes = await sha1.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}