using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public sealed class CommunityResourceInstallPlanner : ICommunityResourceInstallPlanner
{
    private const string DownloadDependenciesKey = "DownloadDependencies";

    private readonly IGameDirResolver _gameDirResolver;
    private readonly ILocalSettingsService _localSettingsService;

    public CommunityResourceInstallPlanner(
        IGameDirResolver gameDirResolver,
        ILocalSettingsService localSettingsService)
    {
        _gameDirResolver = gameDirResolver;
        _localSettingsService = localSettingsService;
    }

    public async Task<CommunityResourceInstallPlanningResult> PlanAsync(CommunityResourceInstallRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string normalizedResourceType = ModResourcePathHelper.NormalizeProjectType(request.ResourceType);
        CommunityResourceKind resourceKind = MapResourceKind(normalizedResourceType);
        if (resourceKind is CommunityResourceKind.Modpack or CommunityResourceKind.Unknown)
        {
            return CommunityResourceInstallPlanningResult.Unsupported(GetUnsupportedReason(resourceKind, normalizedResourceType));
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return CommunityResourceInstallPlanningResult.Missing(new CommunityResourceInstallRequirement
            {
                Type = CommunityResourceInstallRequirementType.FileName,
                Key = "file_name",
                Message = "缺少要安装的文件名。"
            });
        }

        if (!TryNormalizeLeafFileName(request.FileName, out var normalizedFileName))
        {
            return CommunityResourceInstallPlanningResult.Missing(new CommunityResourceInstallRequirement
            {
                Type = CommunityResourceInstallRequirementType.FileName,
                Key = "file_name",
                Message = "文件名无效：必须是单个文件名，不能包含目录或非法路径片段。"
            });
        }

        bool downloadDependencies = await ReadDownloadDependenciesAsync().ConfigureAwait(false);
        if (request.UseCustomDownloadPath)
        {
            if (string.IsNullOrWhiteSpace(request.CustomDownloadPath) || !Path.IsPathRooted(request.CustomDownloadPath))
            {
                return CommunityResourceInstallPlanningResult.Missing(new CommunityResourceInstallRequirement
                {
                    Type = CommunityResourceInstallRequirementType.CustomDownloadPath,
                    Key = "custom_download_path",
                    Message = "缺少有效的自定义下载目录。"
                });
            }

            string customDownloadPath = request.CustomDownloadPath.Trim();
            return CommunityResourceInstallPlanningResult.Ready(new CommunityResourceInstallPlan
            {
                ResourceKind = resourceKind,
                NormalizedResourceType = normalizedResourceType,
                GameDirectory = null,
                TargetVersionName = request.TargetVersionName?.Trim(),
                TargetSaveName = request.TargetSaveName?.Trim(),
                PrimaryTargetDirectory = customDownloadPath,
                DependencyTargetDirectory = customDownloadPath,
                SavePath = Path.Combine(customDownloadPath, normalizedFileName),
                UseTargetDirectoryForAllDependencies = true,
                DownloadDependencies = downloadDependencies,
                UseCustomDownloadPath = true
            });
        }

        if (string.IsNullOrWhiteSpace(request.TargetVersionName))
        {
            return CommunityResourceInstallPlanningResult.Missing(new CommunityResourceInstallRequirement
            {
                Type = CommunityResourceInstallRequirementType.TargetVersion,
                Key = "target_version_name",
                Message = "缺少目标游戏版本。"
            });
        }

        string targetVersionName = request.TargetVersionName.Trim();
        string gameDirectory = await _gameDirResolver.GetGameDirForVersionAsync(targetVersionName).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (resourceKind == CommunityResourceKind.DataPack && string.IsNullOrWhiteSpace(request.TargetSaveName))
        {
            return CommunityResourceInstallPlanningResult.Missing(new CommunityResourceInstallRequirement
            {
                Type = CommunityResourceInstallRequirementType.SaveName,
                Key = "target_save_name",
                Message = "缺少数据包目标存档名称。"
            });
        }

        string primaryTargetDirectory = BuildPrimaryTargetDirectory(resourceKind, gameDirectory, request.TargetSaveName?.Trim());
        string dependencyTargetDirectory = BuildDependencyTargetDirectory(resourceKind, gameDirectory, primaryTargetDirectory);
        return CommunityResourceInstallPlanningResult.Ready(new CommunityResourceInstallPlan
        {
            ResourceKind = resourceKind,
            NormalizedResourceType = normalizedResourceType,
            GameDirectory = gameDirectory,
            TargetVersionName = targetVersionName,
            TargetSaveName = request.TargetSaveName?.Trim(),
            PrimaryTargetDirectory = primaryTargetDirectory,
            DependencyTargetDirectory = dependencyTargetDirectory,
            SavePath = Path.Combine(primaryTargetDirectory, normalizedFileName),
            UseTargetDirectoryForAllDependencies = false,
            DownloadDependencies = downloadDependencies,
            UseCustomDownloadPath = false
        });
    }

    private static bool TryNormalizeLeafFileName(string fileName, out string normalizedFileName)
    {
        normalizedFileName = string.Empty;

        string trimmedFileName = fileName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedFileName) || Path.IsPathRooted(trimmedFileName))
        {
            return false;
        }

        string leafFileName = Path.GetFileName(trimmedFileName);
        if (!string.Equals(leafFileName, trimmedFileName, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(leafFileName) ||
            leafFileName is "." or ".." ||
            leafFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        normalizedFileName = leafFileName;
        return true;
    }

    private async Task<bool> ReadDownloadDependenciesAsync()
    {
        bool? setting = await _localSettingsService.ReadSettingAsync<bool?>(DownloadDependenciesKey).ConfigureAwait(false);
        return setting ?? true;
    }

    private static CommunityResourceKind MapResourceKind(string normalizedResourceType)
    {
        return normalizedResourceType switch
        {
            "mod" => CommunityResourceKind.Mod,
            "resourcepack" => CommunityResourceKind.ResourcePack,
            "shader" => CommunityResourceKind.Shader,
            "datapack" => CommunityResourceKind.DataPack,
            "world" => CommunityResourceKind.World,
            "modpack" => CommunityResourceKind.Modpack,
            _ => CommunityResourceKind.Unknown
        };
    }

    private static string GetUnsupportedReason(CommunityResourceKind resourceKind, string normalizedResourceType)
    {
        return resourceKind switch
        {
            CommunityResourceKind.Modpack => "整合包安装尚未接入统一静默安装规划，当前仍依赖独立安装流程。",
            _ => $"当前不支持为资源类型 {normalizedResourceType} 生成安装计划。"
        };
    }

    private static string BuildPrimaryTargetDirectory(CommunityResourceKind resourceKind, string gameDirectory, string? targetSaveName)
    {
        return resourceKind switch
        {
            CommunityResourceKind.Mod => Path.Combine(gameDirectory, MinecraftPathConsts.Mods),
            CommunityResourceKind.ResourcePack => Path.Combine(gameDirectory, MinecraftPathConsts.ResourcePacks),
            CommunityResourceKind.Shader => Path.Combine(gameDirectory, MinecraftPathConsts.ShaderPacks),
            CommunityResourceKind.DataPack => Path.Combine(gameDirectory, MinecraftPathConsts.Saves, targetSaveName!, MinecraftPathConsts.Datapacks),
            CommunityResourceKind.World => Path.Combine(gameDirectory, MinecraftPathConsts.Saves),
            _ => throw new InvalidOperationException($"不支持的资源类型: {resourceKind}")
        };
    }

    private static string BuildDependencyTargetDirectory(CommunityResourceKind resourceKind, string gameDirectory, string primaryTargetDirectory)
    {
        return resourceKind switch
        {
            CommunityResourceKind.World => Path.Combine(gameDirectory, MinecraftPathConsts.Mods),
            _ => primaryTargetDirectory,
        };
    }
}