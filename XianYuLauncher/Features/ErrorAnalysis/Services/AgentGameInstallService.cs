using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed record InstallGameLoaderRequest(string Type, string? Version);

public sealed class PreparedGameInstallPlan
{
    public required string MinecraftVersion { get; init; }

    public required string VersionName { get; init; }

    public required IReadOnlyList<ModLoaderSelection> LoaderSelections { get; init; }

    public required string LoaderSummary { get; init; }
}

public interface IAgentGameInstallService
{
    Task<PreparedGameInstallPlan> PrepareInstallAsync(
        string minecraftVersion,
        IReadOnlyList<InstallGameLoaderRequest> loaders,
        string? requestedVersionName,
        CancellationToken cancellationToken);

    Task<string> StartInstallAsync(
        string minecraftVersion,
        IReadOnlyList<ModLoaderSelection> loaderSelections,
        string versionName,
        CancellationToken cancellationToken);
}

public sealed class AgentGameInstallService : IAgentGameInstallService
{
    private readonly IModLoaderVersionLoaderService _versionLoaderService;
    private readonly IModLoaderVersionNameService _versionNameService;
    private readonly IDownloadTaskManager _downloadTaskManager;

    private sealed class ResolvedLoader
    {
        public required string Key { get; init; }

        public required string DisplayName { get; init; }

        public required string DisplayVersion { get; init; }

        public required string InstallVersion { get; init; }

        public required string VersionNameToken { get; init; }
    }

    public AgentGameInstallService(
        IModLoaderVersionLoaderService versionLoaderService,
        IModLoaderVersionNameService versionNameService,
        IDownloadTaskManager downloadTaskManager)
    {
        _versionLoaderService = versionLoaderService;
        _versionNameService = versionNameService;
        _downloadTaskManager = downloadTaskManager;
    }

    public async Task<PreparedGameInstallPlan> PrepareInstallAsync(
        string minecraftVersion,
        IReadOnlyList<InstallGameLoaderRequest> loaders,
        string? requestedVersionName,
        CancellationToken cancellationToken)
    {
        var normalizedMinecraftVersion = minecraftVersion?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedMinecraftVersion))
        {
            throw new InvalidOperationException("请提供 Minecraft 版本号。");
        }

        var resolvedLoaders = await ResolveLoadersAsync(normalizedMinecraftVersion, loaders, cancellationToken);
        var loaderSelections = BuildLoaderSelections(resolvedLoaders);
        var finalVersionName = string.IsNullOrWhiteSpace(requestedVersionName)
            ? GenerateVersionName(normalizedMinecraftVersion, resolvedLoaders)
            : requestedVersionName.Trim();

        var validationResult = _versionNameService.ValidateVersionName(finalVersionName);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(validationResult.ErrorMessage);
        }

        return new PreparedGameInstallPlan
        {
            MinecraftVersion = normalizedMinecraftVersion,
            VersionName = finalVersionName,
            LoaderSelections = loaderSelections,
            LoaderSummary = BuildLoaderSummary(resolvedLoaders)
        };
    }

    public async Task<string> StartInstallAsync(
        string minecraftVersion,
        IReadOnlyList<ModLoaderSelection> loaderSelections,
        string versionName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (loaderSelections.Count == 0)
        {
            return await _downloadTaskManager.StartVanillaDownloadWithTaskIdAsync(
                minecraftVersion,
                versionName,
                showInTeachingTip: true);
        }

        return await _downloadTaskManager.StartMultiModLoaderDownloadWithTaskIdAsync(
            minecraftVersion,
            loaderSelections,
            versionName,
            showInTeachingTip: true);
    }

    private async Task<List<ResolvedLoader>> ResolveLoadersAsync(
        string minecraftVersion,
        IReadOnlyList<InstallGameLoaderRequest> loaders,
        CancellationToken cancellationToken)
    {
        if (loaders.Count == 0)
        {
            return [];
        }

        var normalizedKeys = new List<string>(loaders.Count);
        HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);

        foreach (var loader in loaders)
        {
            if (string.IsNullOrWhiteSpace(loader.Type))
            {
                throw new InvalidOperationException("loaders 数组中的 type 不能为空。");
            }

            var loaderKey = NormalizeLoaderKey(loader.Type);
            if (string.IsNullOrWhiteSpace(loaderKey))
            {
                throw new InvalidOperationException($"不支持的加载器类型: {loader.Type}。支持的类型: forge, fabric, neoforge, quilt, cleanroom, legacyfabric, optifine, liteloader。");
            }

            if (!seenKeys.Add(loaderKey))
            {
                throw new InvalidOperationException($"loaders 中重复指定了 {GetDisplayLoaderName(loaderKey)}。");
            }

            normalizedKeys.Add(loaderKey);
        }

        if (normalizedKeys.Count(IsExplicitPrimaryLoader) > 1)
        {
            throw new InvalidOperationException("V1 目前只支持 1 个基础加载器，再附加 OptiFine 和/或 LiteLoader；暂不支持 Forge + Fabric 这类多基础加载器组合。");
        }

        List<ResolvedLoader> resolvedLoaders = [];
        for (int index = 0; index < loaders.Count; index++)
        {
            var loader = loaders[index];
            var loaderKey = normalizedKeys[index];
            var displayName = GetDisplayLoaderName(loaderKey);
            var availableVersions = await _versionLoaderService.LoadVersionsAsync(loaderKey, minecraftVersion, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (availableVersions.Count == 0)
            {
                throw new InvalidOperationException($"未找到与 Minecraft {minecraftVersion} 兼容的 {displayName} 版本。");
            }

            var selectedVersion = SelectVersion(loaderKey, loader.Version, availableVersions);
            if (selectedVersion == null)
            {
                throw new InvalidOperationException($"未找到与 Minecraft {minecraftVersion} 兼容的 {displayName} 版本 {loader.Version}。可以省略 version，让系统自动选择当前兼容列表中的第一项。");
            }

            resolvedLoaders.Add(CreateResolvedLoader(loaderKey, minecraftVersion, selectedVersion));
        }

        return resolvedLoaders;
    }

    private IReadOnlyList<ModLoaderSelection> BuildLoaderSelections(IReadOnlyList<ResolvedLoader> resolvedLoaders)
    {
        var explicitPrimaryLoader = resolvedLoaders.FirstOrDefault(loader => IsExplicitPrimaryLoader(loader.Key));
        var optifineLoader = resolvedLoaders.FirstOrDefault(loader => loader.Key == "optifine");
        var liteLoader = resolvedLoaders.FirstOrDefault(loader => loader.Key == "liteloader");
        var isLiteLoaderOptifineOnlyCombination = explicitPrimaryLoader == null
            && liteLoader != null
            && optifineLoader != null;

        List<ModLoaderSelection> selections = [];

        if (explicitPrimaryLoader != null)
        {
            selections.Add(new ModLoaderSelection
            {
                Type = explicitPrimaryLoader.DisplayName,
                Version = explicitPrimaryLoader.InstallVersion,
                InstallOrder = 1,
                IsAddon = false
            });
        }

        if (isLiteLoaderOptifineOnlyCombination && liteLoader != null)
        {
            selections.Add(new ModLoaderSelection
            {
                Type = liteLoader.DisplayName,
                Version = liteLoader.InstallVersion,
                InstallOrder = 1,
                IsAddon = false
            });
        }

        if (optifineLoader != null)
        {
            var isAddon = explicitPrimaryLoader != null || isLiteLoaderOptifineOnlyCombination;
            selections.Add(new ModLoaderSelection
            {
                Type = optifineLoader.DisplayName,
                Version = optifineLoader.InstallVersion,
                InstallOrder = isAddon ? 2 : 1,
                IsAddon = isAddon
            });
        }

        if (liteLoader != null && !isLiteLoaderOptifineOnlyCombination)
        {
            var isAddon = explicitPrimaryLoader != null || optifineLoader != null;
            selections.Add(new ModLoaderSelection
            {
                Type = liteLoader.DisplayName,
                Version = liteLoader.InstallVersion,
                InstallOrder = isAddon ? 3 : 1,
                IsAddon = isAddon
            });
        }

        return selections;
    }

    private string GenerateVersionName(string minecraftVersion, IReadOnlyList<ResolvedLoader> resolvedLoaders)
    {
        var explicitPrimaryLoader = resolvedLoaders.FirstOrDefault(loader => IsExplicitPrimaryLoader(loader.Key));
        var optifineLoader = resolvedLoaders.FirstOrDefault(loader => loader.Key == "optifine");
        var liteLoader = resolvedLoaders.FirstOrDefault(loader => loader.Key == "liteloader");

        return _versionNameService.GenerateVersionName(
            minecraftVersion,
            explicitPrimaryLoader?.DisplayName,
            explicitPrimaryLoader?.DisplayVersion,
            optifineLoader != null,
            optifineLoader?.VersionNameToken,
            liteLoader != null,
            liteLoader?.VersionNameToken);
    }

    private static string BuildLoaderSummary(IReadOnlyList<ResolvedLoader> resolvedLoaders)
    {
        if (resolvedLoaders.Count == 0)
        {
            return "原版";
        }

        var explicitPrimaryLoader = resolvedLoaders.FirstOrDefault(loader => IsExplicitPrimaryLoader(loader.Key));
        var optifineLoader = resolvedLoaders.FirstOrDefault(loader => loader.Key == "optifine");
        var liteLoader = resolvedLoaders.FirstOrDefault(loader => loader.Key == "liteloader");

        List<string> parts = [];

        if (explicitPrimaryLoader != null)
        {
            parts.Add($"{explicitPrimaryLoader.DisplayName} {explicitPrimaryLoader.VersionNameToken}");
        }

        if (explicitPrimaryLoader == null && liteLoader != null && optifineLoader != null)
        {
            parts.Add($"{liteLoader.DisplayName} {liteLoader.VersionNameToken}");
            parts.Add($"{optifineLoader.DisplayName} {optifineLoader.VersionNameToken}");
            return string.Join(" + ", parts);
        }

        if (explicitPrimaryLoader == null && optifineLoader != null && liteLoader == null)
        {
            parts.Add($"{optifineLoader.DisplayName} {optifineLoader.VersionNameToken}");
        }

        if (explicitPrimaryLoader == null && liteLoader != null && optifineLoader == null)
        {
            parts.Add($"{liteLoader.DisplayName} {liteLoader.VersionNameToken}");
        }

        if (explicitPrimaryLoader != null && optifineLoader != null)
        {
            parts.Add($"{optifineLoader.DisplayName} {optifineLoader.VersionNameToken}");
        }

        if (liteLoader != null && !(explicitPrimaryLoader == null && optifineLoader == null))
        {
            parts.Add($"{liteLoader.DisplayName} {liteLoader.VersionNameToken}");
        }

        return string.Join(" + ", parts);
    }

    private static ResolvedLoader CreateResolvedLoader(string loaderKey, string minecraftVersion, string selectedVersion)
    {
        if (loaderKey == "optifine")
        {
            if (!OptifineVersionHelper.TryParse(selectedVersion, minecraftVersion, out var parts))
            {
                throw new InvalidOperationException($"无法解析 OptiFine 版本: {selectedVersion}");
            }

            return new ResolvedLoader
            {
                Key = loaderKey,
                DisplayName = GetDisplayLoaderName(loaderKey),
                DisplayVersion = parts.ToUnderscoreFormat(),
                InstallVersion = parts.ToColonFormat(),
                VersionNameToken = parts.ToUnderscoreFormat()
            };
        }

        return new ResolvedLoader
        {
            Key = loaderKey,
            DisplayName = GetDisplayLoaderName(loaderKey),
            DisplayVersion = selectedVersion,
            InstallVersion = selectedVersion,
            VersionNameToken = selectedVersion
        };
    }

    private static string? SelectVersion(string loaderKey, string? requestedVersion, IReadOnlyList<string> availableVersions)
    {
        if (availableVersions.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(requestedVersion))
        {
            return availableVersions[0];
        }

        var normalizedRequestedVersion = NormalizeRequestedVersion(loaderKey, requestedVersion);
        return availableVersions.FirstOrDefault(version =>
            string.Equals(version, normalizedRequestedVersion, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeRequestedVersion(string loaderKey, string requestedVersion)
    {
        var normalized = requestedVersion.Trim();
        if (loaderKey == "optifine" && OptifineVersionHelper.TryNormalize(normalized, out var normalizedOptifineVersion))
        {
            return normalizedOptifineVersion;
        }

        return normalized;
    }

    private static string NormalizeLoaderKey(string loaderType)
    {
        return loaderType.Trim().ToLowerInvariant() switch
        {
            "forge" => "forge",
            "fabric" => "fabric",
            "legacyfabric" or "legacy-fabric" or "legacy_fabric" => "legacyfabric",
            "neoforge" or "neo-forge" or "neo_forge" => "neoforge",
            "quilt" => "quilt",
            "cleanroom" => "cleanroom",
            "optifine" or "opti-fine" or "opti_fine" => "optifine",
            "liteloader" or "lite-loader" or "lite_loader" => "liteloader",
            _ => string.Empty
        };
    }

    private static string GetDisplayLoaderName(string loaderKey)
    {
        return loaderKey switch
        {
            "forge" => "Forge",
            "fabric" => "Fabric",
            "legacyfabric" => "LegacyFabric",
            "neoforge" => "NeoForge",
            "quilt" => "Quilt",
            "cleanroom" => "Cleanroom",
            "optifine" => "OptiFine",
            "liteloader" => "LiteLoader",
            _ => loaderKey
        };
    }

    private static bool IsExplicitPrimaryLoader(string loaderKey)
    {
        return loaderKey is not "optifine" and not "liteloader";
    }

}