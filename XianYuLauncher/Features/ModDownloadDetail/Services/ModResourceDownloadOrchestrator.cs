using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.ModDownloadDetail.Services;

public class ModResourceDownloadOrchestrator : IModResourceDownloadOrchestrator
{
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFileService _fileService;

    public ModResourceDownloadOrchestrator(
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        IDownloadTaskManager downloadTaskManager,
        ILocalSettingsService localSettingsService,
        IFileService fileService)
    {
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _downloadTaskManager = downloadTaskManager;
        _localSettingsService = localSettingsService;
        _fileService = fileService;
    }

    public string EnsureDownloadUrl(ModVersionViewModel modVersion)
    {
        try
        {
            string resolvedUrl = ModDownloadPlanningHelper.ResolveDownloadUrl(
                modVersion.DownloadUrl,
                modVersion.OriginalCurseForgeFile,
                modVersion.FileName,
                _curseForgeService.ConstructDownloadUrl);

            modVersion.DownloadUrl = resolvedUrl;
            return resolvedUrl;
        }
        catch
        {
            return modVersion.DownloadUrl ?? string.Empty;
        }
    }

    public async Task ProcessDependenciesForResourceAsync(
        string projectType,
        string minecraftPath,
        ModVersionViewModel modVersion,
        string targetDir,
        InstalledGameVersionViewModel? gameVersion,
        Action<string, double, string>? onProgress = null)
    {
        bool? downloadDependenciesSetting = await _localSettingsService.ReadSettingAsync<bool?>("DownloadDependencies");
        bool downloadDependencies = downloadDependenciesSetting ?? true;

        if (!downloadDependencies)
        {
            return;
        }

        var loaderType = gameVersion?.LoaderType?.ToLowerInvariant();
        var gameVersionId = gameVersion?.GameVersion;
        if (ModDownloadPlanningHelper.ShouldSkipDependencyProcessing(projectType, loaderType, gameVersionId))
        {
            return;
        }

        if (ModResourcePathHelper.NormalizeProjectType(projectType) != "mod")
        {
            modVersion.OriginalVersion ??= new ModrinthVersion();
            ModDownloadPlanningHelper.ApplyNonModDependencyContext(modVersion.OriginalVersion, loaderType!, gameVersionId!);
        }

        // 当用户选择「自定义安装位置」时，targetDir 不在 minecraftPath 下，依赖应下载到同一目录
        bool useTargetDirForAllDependencies = !ModDownloadPlanningHelper.IsTargetUnderMinecraftVersions(targetDir, minecraftPath);

        Func<string, Task<string>> resolveModrinthDependencyTargetAsync = async projectId =>
        {
            if (useTargetDirForAllDependencies)
            {
                return targetDir;
            }

            try
            {
                var detail = await _modrinthService.GetProjectDetailAsync(projectId);
                string dependencyProjectType = ModResourcePathHelper.NormalizeProjectType(detail?.ProjectType);
                return ModResourcePathHelper.GetDependencyTargetDir(minecraftPath, gameVersion?.OriginalVersionName, dependencyProjectType);
            }
            catch
            {
                return ModResourcePathHelper.GetDependencyTargetDir(minecraftPath, gameVersion?.OriginalVersionName, "mod");
            }
        };

        Func<CurseForgeModDetail, Task<string>> resolveCurseForgeDependencyTargetAsync = depMod =>
        {
            if (useTargetDirForAllDependencies)
            {
                return Task.FromResult(targetDir);
            }

            string dependencyProjectType = ModResourcePathHelper.MapCurseForgeClassIdToProjectType(depMod?.ClassId);
            return Task.FromResult(ModResourcePathHelper.GetDependencyTargetDir(minecraftPath, gameVersion?.OriginalVersionName, dependencyProjectType));
        };

        if (modVersion.OriginalVersion?.Dependencies != null && modVersion.OriginalVersion.Dependencies.Count > 0)
        {
            var requiredDependencies = modVersion.OriginalVersion.Dependencies
                .Where(d => d.DependencyType == "required")
                .ToList();

            if (requiredDependencies.Count == 0)
            {
                return;
            }

            await _modrinthService.ProcessDependenciesAsync(
                requiredDependencies,
                targetDir,
                modVersion.OriginalVersion,
                (fileName, progress) =>
                {
                    string statusMessage = $"正在下载前置资源: {fileName}";
                    onProgress?.Invoke(fileName, progress, statusMessage);
                    _downloadTaskManager.NotifyProgress($"前置: {fileName}", progress, statusMessage);
                },
                resolveDestinationPathAsync: resolveModrinthDependencyTargetAsync);

            return;
        }

        if (modVersion.OriginalCurseForgeFile?.Dependencies == null || modVersion.OriginalCurseForgeFile.Dependencies.Count == 0)
        {
            return;
        }

        var curseForgeDependencies = modVersion.OriginalCurseForgeFile.Dependencies
            .Where(d => d.RelationType == 3)
            .ToList();

        if (curseForgeDependencies.Count == 0)
        {
            return;
        }

        await _curseForgeService.ProcessDependenciesAsync(
            curseForgeDependencies,
            targetDir,
            modVersion.OriginalCurseForgeFile,
            (fileName, progress) =>
            {
                string statusMessage = $"正在下载前置资源: {fileName}";
                onProgress?.Invoke(fileName, progress, statusMessage);
                _downloadTaskManager.NotifyProgress($"前置: {fileName}", progress, statusMessage);
            },
            resolveDestinationPathAsync: resolveCurseForgeDependencyTargetAsync);
    }

    public async Task StartResourceDownloadAsync(
        string modName,
        string projectType,
        string modIconUrl,
        string downloadUrl,
        string savePath,
        Action initializeTeachingTip)
    {
        _downloadTaskManager.IsTeachingTipEnabled = true;
        initializeTeachingTip();
        await _downloadTaskManager.StartResourceDownloadAsync(modName, projectType, downloadUrl, savePath, modIconUrl);
    }
}