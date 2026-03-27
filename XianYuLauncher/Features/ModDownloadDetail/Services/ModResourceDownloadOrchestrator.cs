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

    public string EnsureDownloadUrl(CommunityResourceInstallDescriptor descriptor)
    {
        try
        {
            string resolvedUrl = ModDownloadPlanningHelper.ResolveDownloadUrl(
                descriptor.DownloadUrl,
                descriptor.OriginalCurseForgeFile,
                descriptor.FileName,
                _curseForgeService.ConstructDownloadUrl);

            descriptor.DownloadUrl = resolvedUrl;
            return resolvedUrl;
        }
        catch
        {
            return descriptor.DownloadUrl ?? string.Empty;
        }
    }

    public async Task ProcessDependenciesForResourceAsync(
        string projectType,
        string gameDir,
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
        string minecraftPath = _fileService.GetMinecraftDataPath();
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
                return ModResourcePathHelper.GetDependencyTargetDir(gameDir, dependencyProjectType);
            }
            catch
            {
                return ModResourcePathHelper.GetDependencyTargetDir(gameDir, "mod");
            }
        };

        Func<CurseForgeModDetail, Task<string>> resolveCurseForgeDependencyTargetAsync = depMod =>
        {
            if (useTargetDirForAllDependencies)
            {
                return Task.FromResult(targetDir);
            }

            string dependencyProjectType = ModResourcePathHelper.MapCurseForgeClassIdToProjectType(depMod?.ClassId);
            return Task.FromResult(ModResourcePathHelper.GetDependencyTargetDir(gameDir, dependencyProjectType));
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
            },
            resolveDestinationPathAsync: resolveCurseForgeDependencyTargetAsync);
    }

    public async Task<IReadOnlyList<ResourceDependency>> BuildDependenciesAsync(
        CommunityResourceInstallPlan installPlan,
        CommunityResourceInstallDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installPlan);
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!installPlan.DownloadDependencies)
        {
            return [];
        }

        string normalizedProjectType = installPlan.NormalizedResourceType;
        string? loaderType = descriptor.TargetLoaderType?.ToLowerInvariant();
        string? gameVersionId = descriptor.TargetGameVersion;
        if (ModDownloadPlanningHelper.ShouldSkipDependencyProcessing(normalizedProjectType, loaderType, gameVersionId))
        {
            return [];
        }

        List<ResourceDependency> dependencies = [];
        HashSet<string> seenSavePaths = new(StringComparer.OrdinalIgnoreCase);

        var modrinthVersion = descriptor.OriginalVersion;
        if (ModResourcePathHelper.NormalizeProjectType(normalizedProjectType) != "mod" &&
            modrinthVersion != null &&
            !string.IsNullOrWhiteSpace(loaderType) &&
            !string.IsNullOrWhiteSpace(gameVersionId))
        {
            ModDownloadPlanningHelper.ApplyNonModDependencyContext(modrinthVersion, loaderType, gameVersionId);
        }

        if (modrinthVersion?.Dependencies is { Count: > 0 })
        {
            var requiredDependencies = modrinthVersion.Dependencies
                .Where(dependency => dependency.DependencyType == "required")
                .ToList();

            foreach (var dependency in requiredDependencies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dependencyVersion = await ResolveModrinthDependencyVersionAsync(dependency, modrinthVersion).ConfigureAwait(false);
                var dependencyFile = dependencyVersion?.Files.FirstOrDefault(file => file.Primary) ?? dependencyVersion?.Files.FirstOrDefault();
                if (dependencyVersion == null || dependencyFile == null)
                {
                    continue;
                }

                string dependencyTargetDir = await ResolveModrinthDependencyTargetDirAsync(installPlan, dependencyVersion.ProjectId).ConfigureAwait(false);
                string savePath = Path.Combine(dependencyTargetDir, dependencyFile.Filename);
                if (!seenSavePaths.Add(savePath))
                {
                    continue;
                }

                dependencies.Add(new ResourceDependency
                {
                    Name = dependencyFile.Filename,
                    DownloadUrl = dependencyFile.Url.ToString(),
                    SavePath = savePath
                });
            }

            return dependencies;
        }

        if (descriptor.OriginalCurseForgeFile?.Dependencies == null || descriptor.OriginalCurseForgeFile.Dependencies.Count == 0)
        {
            return dependencies;
        }

        foreach (var dependency in descriptor.OriginalCurseForgeFile.Dependencies.Where(item => item.RelationType == 3))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dependencyResolution = await ResolveCurseForgeDependencyAsync(dependency, descriptor.OriginalCurseForgeFile).ConfigureAwait(false);
            if (dependencyResolution.File == null || dependencyResolution.ModDetail == null)
            {
                continue;
            }

            string dependencyTargetDir = ResolveCurseForgeDependencyTargetDir(installPlan, dependencyResolution.ModDetail);
            string fileName = dependencyResolution.File.FileName;
            string downloadUrl = string.IsNullOrWhiteSpace(dependencyResolution.File.DownloadUrl)
                ? _curseForgeService.ConstructDownloadUrl(dependencyResolution.File.Id, fileName)
                : dependencyResolution.File.DownloadUrl;
            string savePath = Path.Combine(dependencyTargetDir, fileName);
            if (!seenSavePaths.Add(savePath))
            {
                continue;
            }

            dependencies.Add(new ResourceDependency
            {
                Name = dependencyResolution.ModDetail.Name,
                DownloadUrl = downloadUrl,
                SavePath = savePath
            });
        }

        return dependencies;
    }

    public async Task StartResourceDownloadAsync(
        string modName,
        string projectType,
        string modIconUrl,
        string downloadUrl,
        string savePath,
        bool showInTeachingTip = false,
        string? teachingTipGroupKey = null,
        CommunityResourceProvider communityResourceProvider = CommunityResourceProvider.Unknown)
    {
        await _downloadTaskManager.StartResourceDownloadAsync(
            modName,
            projectType,
            downloadUrl,
            savePath,
            modIconUrl,
            showInTeachingTip: showInTeachingTip,
            teachingTipGroupKey: teachingTipGroupKey,
            communityResourceProvider: communityResourceProvider);
    }

    public Task<string> StartResourceDownloadWithTaskIdAsync(
        string resourceName,
        CommunityResourceInstallPlan installPlan,
        CommunityResourceInstallDescriptor descriptor,
        IEnumerable<ResourceDependency>? dependencies = null,
        bool showInTeachingTip = false,
        string? teachingTipGroupKey = null)
    {
        return _downloadTaskManager.StartResourceDownloadWithTaskIdAsync(
            resourceName,
            installPlan.NormalizedResourceType,
            descriptor.DownloadUrl,
            installPlan.SavePath,
            descriptor.ResourceIconUrl,
            dependencies,
            showInTeachingTip: showInTeachingTip,
            teachingTipGroupKey: teachingTipGroupKey,
            communityResourceProvider: descriptor.CommunityResourceProvider);
    }

    private async Task<ModrinthVersion?> ResolveModrinthDependencyVersionAsync(Dependency dependency, ModrinthVersion currentVersion)
    {
        if (!string.IsNullOrWhiteSpace(dependency.VersionId))
        {
            return await _modrinthService.GetVersionByIdAsync(dependency.VersionId);
        }

        if (string.IsNullOrWhiteSpace(dependency.ProjectId))
        {
            return null;
        }

        var compatibleVersions = currentVersion != null
            ? await _modrinthService.GetProjectVersionsAsync(
                dependency.ProjectId,
                currentVersion.Loaders,
                currentVersion.GameVersions)
            : await _modrinthService.GetProjectVersionsAsync(dependency.ProjectId);

        return compatibleVersions
            .OrderByDescending(version => version.DatePublished)
            .FirstOrDefault();
    }

    private async Task<string> ResolveModrinthDependencyTargetDirAsync(CommunityResourceInstallPlan installPlan, string? projectId)
    {
        if (installPlan.UseTargetDirectoryForAllDependencies || string.IsNullOrWhiteSpace(installPlan.GameDirectory))
        {
            return installPlan.DependencyTargetDirectory;
        }

        try
        {
            var detail = await _modrinthService.GetProjectDetailAsync(projectId!);
            string dependencyProjectType = ModResourcePathHelper.NormalizeProjectType(detail?.ProjectType);
            return ModResourcePathHelper.GetDependencyTargetDir(installPlan.GameDirectory, dependencyProjectType);
        }
        catch
        {
            return ModResourcePathHelper.GetDependencyTargetDir(installPlan.GameDirectory, "mod");
        }
    }

    private async Task<(CurseForgeModDetail? ModDetail, CurseForgeFile? File)> ResolveCurseForgeDependencyAsync(CurseForgeDependency dependency, CurseForgeFile? currentFile)
    {
        var depMod = await _curseForgeService.GetModDetailAsync(dependency.ModId);
        if (depMod == null)
        {
            return (null, null);
        }

        CurseForgeFile? depFile = null;
        if (currentFile?.GameVersions is { Count: > 0 })
        {
            var gameVersions = currentFile.GameVersions
                .Where(version => !version.Equals("forge", StringComparison.OrdinalIgnoreCase) &&
                                  !version.Equals("fabric", StringComparison.OrdinalIgnoreCase) &&
                                  !version.Equals("quilt", StringComparison.OrdinalIgnoreCase) &&
                                  !version.Equals("neoforge", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var loaders = currentFile.GameVersions
                .Where(version => version.Equals("forge", StringComparison.OrdinalIgnoreCase) ||
                                  version.Equals("fabric", StringComparison.OrdinalIgnoreCase) ||
                                  version.Equals("quilt", StringComparison.OrdinalIgnoreCase) ||
                                  version.Equals("neoforge", StringComparison.OrdinalIgnoreCase))
                .ToList();

            depFile = depMod.LatestFiles
                .Where(file => file.GameVersions != null &&
                               gameVersions.Any(gameVersion => file.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase)) &&
                               loaders.Any(loader => file.GameVersions.Contains(loader, StringComparer.OrdinalIgnoreCase)))
                .OrderByDescending(file => file.FileDate)
                .FirstOrDefault();

            depFile ??= depMod.LatestFiles
                .Where(file => file.GameVersions != null &&
                               gameVersions.Any(gameVersion => file.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase)))
                .OrderByDescending(file => file.FileDate)
                .FirstOrDefault();
        }

        depFile ??= depMod.LatestFiles.OrderByDescending(file => file.FileDate).FirstOrDefault();
        return (depMod, depFile);
    }

    private static string ResolveCurseForgeDependencyTargetDir(CommunityResourceInstallPlan installPlan, CurseForgeModDetail depMod)
    {
        if (installPlan.UseTargetDirectoryForAllDependencies || string.IsNullOrWhiteSpace(installPlan.GameDirectory))
        {
            return installPlan.DependencyTargetDirectory;
        }

        string dependencyProjectType = ModResourcePathHelper.MapCurseForgeClassIdToProjectType(depMod.ClassId);
        return ModResourcePathHelper.GetDependencyTargetDir(installPlan.GameDirectory, dependencyProjectType);
    }
}
