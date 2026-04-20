using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Features.VersionList.ViewModels;
using XianYuLauncher.Models.VersionManagement;
using System.Text.Json;
using System.IO;

namespace XianYuLauncher.Features.VersionManagement.Services;

public class VersionSettingsOrchestrator : IVersionSettingsOrchestrator
{
    private const string SettingsFileName = MinecraftFileConsts.VersionConfig;

    private readonly IFileService _fileService;
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IVersionInfoService _versionInfoService;
    private readonly IVersionInfoManager _versionInfoManager;
    private readonly IModLoaderInstallerFactory _modLoaderInstallerFactory;
    private readonly FabricService _fabricService;
    private readonly LegacyFabricService _legacyFabricService;
    private readonly ForgeService _forgeService;
    private readonly NeoForgeService _neoForgeService;
    private readonly QuiltService _quiltService;
    private readonly OptifineService _optifineService;
    private readonly CleanroomService _cleanroomService;
    private readonly LiteLoaderService _liteLoaderService;

    public VersionSettingsOrchestrator(
        IFileService fileService,
        IMinecraftVersionService minecraftVersionService,
        IVersionInfoService versionInfoService,
        IVersionInfoManager versionInfoManager,
        IModLoaderInstallerFactory modLoaderInstallerFactory,
        FabricService fabricService,
        LegacyFabricService legacyFabricService,
        ForgeService forgeService,
        NeoForgeService neoForgeService,
        QuiltService quiltService,
        OptifineService optifineService,
        CleanroomService cleanroomService,
        LiteLoaderService liteLoaderService)
    {
        _fileService = fileService;
        _minecraftVersionService = minecraftVersionService;
        _versionInfoService = versionInfoService;
        _versionInfoManager = versionInfoManager;
        _modLoaderInstallerFactory = modLoaderInstallerFactory;
        _fabricService = fabricService;
        _legacyFabricService = legacyFabricService;
        _forgeService = forgeService;
        _neoForgeService = neoForgeService;
        _quiltService = quiltService;
        _optifineService = optifineService;
        _cleanroomService = cleanroomService;
        _liteLoaderService = liteLoaderService;
    }

    public async Task<VersionConfig?> LoadVersionConfigFastAsync(VersionListViewModel.VersionInfoItem selectedVersion)
    {
        return await _versionInfoService.GetFullVersionInfoAsync(selectedVersion.Name, selectedVersion.Path, preferCache: true);
    }

    public async Task<VersionConfig?> LoadVersionConfigDeepAsync(VersionListViewModel.VersionInfoItem selectedVersion)
    {
        return await _versionInfoService.GetFullVersionInfoAsync(selectedVersion.Name, selectedVersion.Path);
    }

    public async Task<string> ResolveMinecraftVersionAsync(VersionListViewModel.VersionInfoItem? selectedVersion)
    {
        if (selectedVersion == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(selectedVersion.VersionNumber))
        {
            return selectedVersion.VersionNumber;
        }

        var versionConfig = await _versionInfoService.GetFullVersionInfoAsync(selectedVersion.Name, selectedVersion.Path, preferCache: true);
        if (versionConfig != null && !string.IsNullOrWhiteSpace(versionConfig.MinecraftVersion) && !string.Equals(versionConfig.MinecraftVersion, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return versionConfig.MinecraftVersion;
        }

        return string.Empty;
    }

    public bool IsVersionBelow1_14(string version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return false;
        }

        try
        {
            var parts = version.Split('.');
            if (parts.Length < 2)
            {
                return false;
            }

            var major = int.Parse(parts[0]);
            var minor = int.Parse(parts[1]);
            return major == 1 && minor < 14;
        }
        catch
        {
            return false;
        }
    }

    public bool IsLoaderInstalled(string loaderType, VersionListViewModel.VersionInfoItem? selectedVersion)
    {
        if (selectedVersion == null)
        {
            return false;
        }

        var versionName = selectedVersion.Name.ToLowerInvariant();
        return versionName.Contains(loaderType, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<string>> GetLoaderVersionsAsync(string loaderType, string minecraftVersion)
    {
        return loaderType.ToLowerInvariant() switch
        {
            "fabric" => await GetFabricVersionsAsync(minecraftVersion),
            "legacyfabric" => await GetLegacyFabricVersionsAsync(minecraftVersion),
            "forge" => await GetForgeVersionsAsync(minecraftVersion),
            "neoforge" => await GetNeoForgeVersionsAsync(minecraftVersion),
            "quilt" => await GetQuiltVersionsAsync(minecraftVersion),
            "optifine" => await GetOptifineVersionsAsync(minecraftVersion),
            "cleanroom" => await GetCleanroomVersionsAsync(minecraftVersion),
            "liteloader" => await GetLiteLoaderVersionsAsync(minecraftVersion),
            _ => new List<string>()
        };
    }

    public async Task SaveVersionSettingsAsync(VersionListViewModel.VersionInfoItem selectedVersion, VersionSettings inputSettings)
    {
        var settingsFilePath = GetSettingsFilePath(selectedVersion);
        VersionSettings settings;

        if (File.Exists(settingsFilePath))
        {
            var existingJson = await File.ReadAllTextAsync(settingsFilePath);
            settings = JsonSerializer.Deserialize<VersionSettings>(existingJson) ?? new VersionSettings();
        }
        else
        {
            settings = new VersionSettings();

            var versionConfig = await _versionInfoService.GetFullVersionInfoAsync(selectedVersion.Name, selectedVersion.Path);
            if (versionConfig != null)
            {
                settings.ModLoaderType = versionConfig.ModLoaderType ?? "vanilla";
                settings.MinecraftVersion = versionConfig.MinecraftVersion ?? selectedVersion.Name;
                settings.ModLoaderVersion = versionConfig.ModLoaderVersion ?? string.Empty;
                settings.CreatedAt = versionConfig.CreatedAt;
                settings.ModpackPlatform = versionConfig.ModpackPlatform;
                settings.ModpackProjectId = versionConfig.ModpackProjectId;
                settings.ModpackVersionId = versionConfig.ModpackVersionId;
            }
            else
            {
                ParseVersionNameToSettings(settings, selectedVersion.Name);
            }

            settings.CreatedAt = DateTime.Now;
        }

        settings.OverrideMemory = inputSettings.OverrideMemory;
        settings.AutoMemoryAllocation = inputSettings.AutoMemoryAllocation;
        settings.InitialHeapMemory = inputSettings.InitialHeapMemory;
        settings.MaximumHeapMemory = inputSettings.MaximumHeapMemory;
        settings.JavaPath = inputSettings.JavaPath;
        settings.CustomJvmArguments = inputSettings.CustomJvmArguments;
        settings.GarbageCollectorMode = GarbageCollectorModeHelper.Normalize(inputSettings.GarbageCollectorMode);
        settings.OverrideResolution = inputSettings.OverrideResolution;
        settings.WindowWidth = inputSettings.WindowWidth;
        settings.WindowHeight = inputSettings.WindowHeight;
        settings.UseGlobalJavaSetting = inputSettings.UseGlobalJavaSetting;
        settings.GameDirMode = inputSettings.GameDirMode;
        settings.GameDirCustomPath = inputSettings.GameDirCustomPath;

        var jsonContent = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

        if (!Directory.Exists(selectedVersion.Path))
        {
            Directory.CreateDirectory(selectedVersion.Path);
        }

        await File.WriteAllTextAsync(settingsFilePath, jsonContent);
    }

    public async Task<ExtensionInstallResult> InstallExtensionsAsync(
        VersionListViewModel.VersionInfoItem selectedVersion,
        IReadOnlyList<LoaderSelection> selectedLoaders,
        ExtensionInstallOptions options,
        Action<string, double>? onProgress = null)
    {
        var minecraftVersion = await ResolveMinecraftVersionAsync(selectedVersion);
        var minecraftDirectory = _fileService.GetMinecraftDataPath();
        var versionDirectory = selectedVersion.Path;
        var versionId = selectedVersion.Name;

        var installPlan = BuildLoaderInstallPlan(selectedLoaders);
        var primaryLoader = installPlan.PrimaryLoader;
        var optifineLoader = installPlan.OptifineLoader;
        var liteLoaderLoader = installPlan.LiteLoaderLoader;

        var needsReinstall = await CheckNeedsReinstallAsync(
            versionId,
            versionDirectory,
            primaryLoader,
            optifineLoader,
            liteLoaderLoader);

        var totalSteps = 2;
        if (primaryLoader != null) totalSteps++;
        if (optifineLoader != null) totalSteps++;
        if (liteLoaderLoader != null) totalSteps++;
        var currentStep = 0;

        if (needsReinstall)
        {
            if (installPlan.UseMultiModLoaderInstall)
            {
                onProgress?.Invoke("正在安装加载器组合...", 0);
                await _minecraftVersionService.DownloadMultiModLoaderVersionAsync(
                    minecraftVersion,
                    installPlan.MultiModLoaderSelections,
                    minecraftDirectory,
                    status => onProgress?.Invoke("正在安装加载器组合...", status.Percent),
                    customVersionName: versionId);
                currentStep = totalSteps - 1;
            }
            else
            {
                onProgress?.Invoke("正在下载原版版本信息...", currentStep / (double)totalSteps * 100);

                var originalVersionJsonContent = await GetOriginalVersionJsonContentAsync(
                    minecraftVersion,
                    minecraftDirectory);

                var versionJsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
                await File.WriteAllTextAsync(versionJsonPath, originalVersionJsonContent);
                currentStep++;
                onProgress?.Invoke("正在下载原版版本信息...", currentStep / (double)totalSteps * 100);

                if (primaryLoader != null)
                {
                    await InstallLoaderAsync(
                        primaryLoader,
                        minecraftVersion,
                        minecraftDirectory,
                        versionId,
                        totalSteps,
                        currentStep,
                        onProgress);
                    currentStep++;
                }

                if (optifineLoader != null)
                {
                    await InstallLoaderAsync(
                        optifineLoader,
                        minecraftVersion,
                        minecraftDirectory,
                        versionId,
                        totalSteps,
                        currentStep,
                        onProgress,
                        forceLoaderType: "optifine");
                    currentStep++;
                }

                if (liteLoaderLoader != null)
                {
                    await InstallLoaderAsync(
                        liteLoaderLoader,
                        minecraftVersion,
                        minecraftDirectory,
                        versionId,
                        totalSteps,
                        currentStep,
                        onProgress,
                        forceLoaderType: "liteloader");
                    currentStep++;
                }
            }
        }

        onProgress?.Invoke("正在保存配置...", currentStep / (double)totalSteps * 100);
        var config = await SaveExtensionConfigAsync(
            selectedVersion,
            minecraftVersion,
            primaryLoader,
            optifineLoader,
            liteLoaderLoader,
            options);

        onProgress?.Invoke("安装完成！", 100);

        return new ExtensionInstallResult
        {
            SavedConfig = config,
            NeedsReinstall = needsReinstall,
            SelectedLoaders = selectedLoaders.ToList()
        };
    }

    public async Task<bool> NeedsExtensionReinstallAsync(
        VersionListViewModel.VersionInfoItem selectedVersion,
        IReadOnlyList<LoaderSelection> selectedLoaders)
    {
        var versionDirectory = selectedVersion.Path;
        var versionId = selectedVersion.Name;

        var installPlan = BuildLoaderInstallPlan(selectedLoaders);

        return await CheckNeedsReinstallAsync(
            versionId,
            versionDirectory,
            installPlan.PrimaryLoader,
            installPlan.OptifineLoader,
            installPlan.LiteLoaderLoader);
    }

    public void ParseVersionNameToSettings(VersionSettings settings, string versionName)
    {
        var lowerVersionName = versionName.ToLowerInvariant();

        if (lowerVersionName.Contains("fabric", StringComparison.OrdinalIgnoreCase))
        {
            settings.ModLoaderType = "fabric";
            TryParseLoaderVersion(settings, versionName, "fabric");
            return;
        }

        if (lowerVersionName.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
        {
            settings.ModLoaderType = "neoforge";
            TryParseLoaderVersion(settings, versionName, "neoforge");
            return;
        }

        if (lowerVersionName.Contains("forge", StringComparison.OrdinalIgnoreCase))
        {
            settings.ModLoaderType = "forge";
            TryParseLoaderVersion(settings, versionName, "forge");
            return;
        }

        if (lowerVersionName.Contains("quilt", StringComparison.OrdinalIgnoreCase))
        {
            settings.ModLoaderType = "quilt";
            TryParseLoaderVersion(settings, versionName, "quilt");
            return;
        }

        if (lowerVersionName.Contains("cleanroom", StringComparison.OrdinalIgnoreCase))
        {
            settings.ModLoaderType = "cleanroom";
            TryParseLoaderVersion(settings, versionName, "cleanroom");
            return;
        }

        if (lowerVersionName.Contains("optifine", StringComparison.OrdinalIgnoreCase))
        {
            settings.ModLoaderType = "optifine";
            TryParseLoaderVersion(settings, versionName, "optifine");
            return;
        }

        if (lowerVersionName.Contains("liteloader", StringComparison.OrdinalIgnoreCase))
        {
            settings.ModLoaderType = "LiteLoader";
            var parts = versionName.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                settings.MinecraftVersion = parts[0];
                for (var index = 1; index < parts.Length; index++)
                {
                    if (parts[index].Contains("liteloader", StringComparison.OrdinalIgnoreCase) && index + 1 < parts.Length)
                    {
                        settings.LiteLoaderVersion = parts[index + 1];
                        break;
                    }
                }
            }
            return;
        }

        settings.ModLoaderType = "vanilla";
        settings.MinecraftVersion = versionName;
    }

    private async Task<List<string>> GetFabricVersionsAsync(string minecraftVersion)
    {
        var fabricVersions = await _fabricService.GetFabricLoaderVersionsAsync(minecraftVersion);
        return fabricVersions.Select(version => version.Loader.Version).ToList();
    }

    private async Task<List<string>> GetLegacyFabricVersionsAsync(string minecraftVersion)
    {
        var legacyFabricVersions = await _legacyFabricService.GetLegacyFabricLoaderVersionsAsync(minecraftVersion);
        return legacyFabricVersions.Select(version => version.Loader.Version).ToList();
    }

    private async Task<List<string>> GetForgeVersionsAsync(string minecraftVersion)
    {
        return await _forgeService.GetForgeVersionsAsync(minecraftVersion);
    }

    private async Task<List<string>> GetNeoForgeVersionsAsync(string minecraftVersion)
    {
        return await _neoForgeService.GetNeoForgeVersionsAsync(minecraftVersion);
    }

    private async Task<List<string>> GetQuiltVersionsAsync(string minecraftVersion)
    {
        var quiltVersions = await _quiltService.GetQuiltLoaderVersionsAsync(minecraftVersion);
        return quiltVersions.Select(version => version.Loader.Version).ToList();
    }

    private async Task<List<string>> GetOptifineVersionsAsync(string minecraftVersion)
    {
        var optifineVersions = await _optifineService.GetOptifineVersionsAsync(minecraftVersion);
        return optifineVersions.Select(version => $"{version.Type}_{version.Patch}").ToList();
    }

    private async Task<List<string>> GetCleanroomVersionsAsync(string minecraftVersion)
    {
        return await _cleanroomService.GetCleanroomVersionsAsync(minecraftVersion);
    }

    private async Task<List<string>> GetLiteLoaderVersionsAsync(string minecraftVersion)
    {
        var artifacts = await _liteLoaderService.GetLiteLoaderArtifactsAsync(minecraftVersion);
        return artifacts
            .Select(artifact => artifact.Version)
            .Where(version => !string.IsNullOrEmpty(version))
            .ToList()!;
    }

    private static void TryParseLoaderVersion(VersionSettings settings, string versionName, string loaderToken)
    {
        var parts = versionName.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return;
        }

        settings.MinecraftVersion = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            if (parts[index].Contains(loaderToken, StringComparison.OrdinalIgnoreCase) && index + 1 < parts.Length)
            {
                settings.ModLoaderVersion = parts[index + 1];
                break;
            }
        }
    }

    private static string GetSettingsFilePath(VersionListViewModel.VersionInfoItem selectedVersion)
    {
        return Path.Combine(selectedVersion.Path, SettingsFileName);
    }

    private async Task<bool> CheckNeedsReinstallAsync(
        string versionId,
        string versionDirectory,
        LoaderSelection? primaryLoader,
        LoaderSelection? optifineLoader,
        LoaderSelection? liteLoaderLoader)
    {
        try
        {
            var currentConfig = await _versionInfoService.GetFullVersionInfoAsync(versionId, versionDirectory);

            var targetType = primaryLoader?.LoaderType.ToLowerInvariant() ?? "vanilla";
            var targetVersion = primaryLoader?.SelectedVersion ?? string.Empty;
            var targetOptifine = optifineLoader?.SelectedVersion
                ?? (primaryLoader?.LoaderType.Equals("optifine", StringComparison.OrdinalIgnoreCase) == true
                    ? primaryLoader.SelectedVersion
                    : null);
            var targetLiteLoader = liteLoaderLoader?.SelectedVersion
                ?? (primaryLoader?.LoaderType.Equals("liteloader", StringComparison.OrdinalIgnoreCase) == true
                    ? primaryLoader.SelectedVersion
                    : null);

            var currentType = string.IsNullOrEmpty(currentConfig.ModLoaderType)
                ? "vanilla"
                : currentConfig.ModLoaderType.ToLowerInvariant();
            var currentVersion = currentConfig.ModLoaderVersion ?? string.Empty;
            var currentOptifine = currentConfig.OptifineVersion;
            var currentLiteLoader = currentConfig.LiteLoaderVersion;

            var isLoaderSame =
                string.Equals(targetType, currentType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(targetVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
            var isOptifineSame = string.Equals(targetOptifine ?? string.Empty, currentOptifine ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            var isLiteLoaderSame = string.Equals(targetLiteLoader ?? string.Empty, currentLiteLoader ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            return !(isLoaderSame && isOptifineSame && isLiteLoaderSame);
        }
        catch
        {
            return true;
        }
    }

    private async Task InstallLoaderAsync(
        LoaderSelection loader,
        string minecraftVersion,
        string minecraftDirectory,
        string versionId,
        int totalSteps,
        int currentStep,
        Action<string, double>? onProgress,
        string? forceLoaderType = null)
    {
        onProgress?.Invoke($"正在安装 {loader.Name} {loader.SelectedVersion}...", currentStep / (double)totalSteps * 100);

        var installer = _modLoaderInstallerFactory.GetInstaller(forceLoaderType ?? loader.LoaderType);
        var installOptions = new ModLoaderInstallOptions
        {
            SkipJarDownload = true,
            CustomVersionName = versionId,
            OverwriteExisting = true
        };

        var stepStartProgress = currentStep / (double)totalSteps * 100;
        var stepEndProgress = (currentStep + 1) / (double)totalSteps * 100;

        await installer.InstallAsync(
            minecraftVersion,
            loader.SelectedVersion,
            minecraftDirectory,
            installOptions,
            status =>
            {
                var mapped = stepStartProgress + (status.Percent / 100.0) * (stepEndProgress - stepStartProgress);
                onProgress?.Invoke($"正在安装 {loader.Name} {loader.SelectedVersion}...", mapped);
            });
    }

    private async Task<string> GetOriginalVersionJsonContentAsync(string minecraftVersion, string minecraftDirectory)
    {
        var preferLocal = await ShouldPreferLocalBaseVersionJsonAsync(minecraftVersion, minecraftDirectory);

        return await _versionInfoManager.GetVersionInfoJsonAsync(
            minecraftVersion,
            minecraftDirectory,
            allowNetwork: true,
            preferLocal: preferLocal);
    }

    private async Task<bool> ShouldPreferLocalBaseVersionJsonAsync(string minecraftVersion, string minecraftDirectory)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion) || string.IsNullOrWhiteSpace(minecraftDirectory))
        {
            return true;
        }

        var baseVersionDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Versions, minecraftVersion);
        var baseVersionJsonPath = Path.Combine(baseVersionDirectory, $"{minecraftVersion}.json");

        if (!File.Exists(baseVersionJsonPath))
        {
            return true;
        }

        try
        {
            var versionConfig = await _versionInfoService.GetFullVersionInfoAsync(minecraftVersion, baseVersionDirectory);
            return IsVanillaBaseVersion(versionConfig);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsVanillaBaseVersion(VersionConfig? versionConfig)
    {
        if (versionConfig == null)
        {
            return false;
        }

        var modLoaderType = versionConfig.ModLoaderType;
        var hasPrimaryLoader = !string.IsNullOrWhiteSpace(modLoaderType)
            && !string.Equals(modLoaderType, "vanilla", StringComparison.OrdinalIgnoreCase);
        var hasAddonLoader = !string.IsNullOrWhiteSpace(versionConfig.OptifineVersion)
            || !string.IsNullOrWhiteSpace(versionConfig.LiteLoaderVersion);

        return !hasPrimaryLoader && !hasAddonLoader;
    }

    private static LoaderInstallPlan BuildLoaderInstallPlan(IReadOnlyList<LoaderSelection> selectedLoaders)
    {
        var explicitPrimaryLoader = selectedLoaders.FirstOrDefault(loader =>
            !loader.LoaderType.Equals("optifine", StringComparison.OrdinalIgnoreCase)
            && !loader.LoaderType.Equals("liteloader", StringComparison.OrdinalIgnoreCase));
        var optifineLoader = selectedLoaders.FirstOrDefault(loader =>
            loader.LoaderType.Equals("optifine", StringComparison.OrdinalIgnoreCase));
        var liteLoaderLoader = selectedLoaders.FirstOrDefault(loader =>
            loader.LoaderType.Equals("liteloader", StringComparison.OrdinalIgnoreCase));

        var primaryLoader = explicitPrimaryLoader;
        var addonOptifineLoader = optifineLoader;
        var addonLiteLoader = liteLoaderLoader;

        var isLiteLoaderOptifineOnlyCombination = explicitPrimaryLoader == null
            && liteLoaderLoader != null
            && optifineLoader != null;

        if (primaryLoader == null)
        {
            if (liteLoaderLoader != null)
            {
                primaryLoader = liteLoaderLoader;
                addonLiteLoader = null;
            }
            else if (optifineLoader != null)
            {
                primaryLoader = optifineLoader;
                addonOptifineLoader = null;
            }
        }

        var multiModLoaderSelections = new List<ModLoaderSelection>();
        if (isLiteLoaderOptifineOnlyCombination && liteLoaderLoader != null && optifineLoader != null)
        {
            multiModLoaderSelections.Add(new ModLoaderSelection
            {
                Type = "LiteLoader",
                Version = liteLoaderLoader.SelectedVersion,
                InstallOrder = 1,
                IsAddon = false
            });

            multiModLoaderSelections.Add(new ModLoaderSelection
            {
                Type = "OptiFine",
                Version = optifineLoader.SelectedVersion,
                InstallOrder = 2,
                IsAddon = true
            });
        }

        return new LoaderInstallPlan(
            primaryLoader,
            addonOptifineLoader,
            addonLiteLoader,
            isLiteLoaderOptifineOnlyCombination,
            multiModLoaderSelections);
    }

    private static async Task<VersionConfig> SaveExtensionConfigAsync(
        VersionListViewModel.VersionInfoItem selectedVersion,
        string minecraftVersion,
        LoaderSelection? primaryLoader,
        LoaderSelection? optifineLoader,
        LoaderSelection? liteLoaderLoader,
        ExtensionInstallOptions options)
    {
        var settingsFilePath = GetSettingsFilePath(selectedVersion);
        VersionConfig config;

        if (File.Exists(settingsFilePath))
        {
            var existingJson = await File.ReadAllTextAsync(settingsFilePath);
            config = JsonSerializer.Deserialize<VersionConfig>(existingJson) ?? new VersionConfig();
        }
        else
        {
            config = new VersionConfig
            {
                CreatedAt = DateTime.Now
            };
        }

        config.MinecraftVersion = minecraftVersion;
        config.ModLoaderType = primaryLoader?.LoaderType?.ToLowerInvariant() ?? "vanilla";
        config.ModLoaderVersion = primaryLoader?.SelectedVersion ?? string.Empty;
        config.OptifineVersion = optifineLoader?.SelectedVersion
            ?? (primaryLoader?.LoaderType.Equals("optifine", StringComparison.OrdinalIgnoreCase) == true
                ? primaryLoader.SelectedVersion
                : null);
        config.LiteLoaderVersion = liteLoaderLoader?.SelectedVersion
            ?? (primaryLoader?.LoaderType.Equals("liteloader", StringComparison.OrdinalIgnoreCase) == true
                ? primaryLoader.SelectedVersion
                : null);
        config.OverrideMemory = options.OverrideMemory;
        config.AutoMemoryAllocation = options.AutoMemoryAllocation;
        config.InitialHeapMemory = options.InitialHeapMemory;
        config.MaximumHeapMemory = options.MaximumHeapMemory;
        config.JavaPath = options.JavaPath;
        config.UseGlobalJavaSetting = options.UseGlobalJavaSetting;
        config.OverrideResolution = options.OverrideResolution;
        config.WindowWidth = options.WindowWidth;
        config.WindowHeight = options.WindowHeight;

        var jsonContent = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(settingsFilePath, jsonContent);

        return config;
    }

    private sealed record LoaderInstallPlan(
        LoaderSelection? PrimaryLoader,
        LoaderSelection? OptifineLoader,
        LoaderSelection? LiteLoaderLoader,
        bool UseMultiModLoaderInstall,
        IReadOnlyList<ModLoaderSelection> MultiModLoaderSelections);
}
