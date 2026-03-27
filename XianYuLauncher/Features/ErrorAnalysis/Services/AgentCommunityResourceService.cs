using Newtonsoft.Json;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.Features.ModDownloadDetail.Services;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public interface IAgentCommunityResourceService
{
    Task<string> SearchAsync(
        string query,
        string resourceType,
        IReadOnlyList<string>? platforms,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken);

    Task<string> GetProjectFilesAsync(
        string projectId,
        string? resourceType,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken);

    Task<string> GetInstallableInstancesAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken);

    Task<AgentCommunityResourceInstallPreparation> PrepareInstallAsync(
        AgentCommunityResourceInstallCommand command,
        CancellationToken cancellationToken);

    Task<string> StartInstallAsync(
        AgentCommunityResourceInstallCommand command,
        CancellationToken cancellationToken);
}

public sealed class AgentCommunityResourceInstallCommand
{
    public string ProjectId { get; init; } = string.Empty;

    public string ResourceFileId { get; init; } = string.Empty;

    public string? TargetVersionName { get; init; }

    public string? TargetVersionPath { get; init; }

    public string? ResourceType { get; init; }

    public bool DownloadDependencies { get; init; }
}

public sealed class AgentCommunityResourceInstallPreparation
{
    public required string Message { get; init; }

    public bool IsReadyForConfirmation { get; init; }

    public string ButtonText { get; init; } = string.Empty;

    public Dictionary<string, string> ProposalParameters { get; init; } = [];
}

internal sealed class AgentCommunityResourceService : IAgentCommunityResourceService
{
    private static readonly HashSet<string> CurseForgeLoaderTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "forge",
        "neoforge",
        "fabric",
        "quilt",
        "optifine",
        "iris",
        "legacyfabric"
    };

    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IVersionInfoService _versionInfoService;
    private readonly IFileService _fileService;
    private readonly IGameDirResolver _gameDirResolver;
    private readonly ICommunityResourceInstallPlanner _communityResourceInstallPlanner;
    private readonly ICommunityResourceInstallService _communityResourceInstallService;
    private readonly ITranslationService _translationService;

    public AgentCommunityResourceService(
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        IMinecraftVersionService minecraftVersionService,
        IVersionInfoService versionInfoService,
        IFileService fileService,
        IGameDirResolver gameDirResolver,
        ICommunityResourceInstallPlanner communityResourceInstallPlanner,
        ICommunityResourceInstallService communityResourceInstallService,
        ITranslationService translationService)
    {
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _minecraftVersionService = minecraftVersionService;
        _versionInfoService = versionInfoService;
        _fileService = fileService;
        _gameDirResolver = gameDirResolver;
        _communityResourceInstallPlanner = communityResourceInstallPlanner;
        _communityResourceInstallService = communityResourceInstallService;
        _translationService = translationService;
    }

    public async Task<string> SearchAsync(
        string query,
        string resourceType,
        IReadOnlyList<string>? platforms,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return SerializePayload(new
            {
                status = "invalid_request",
                message = "query 不能为空。"
            });
        }

        var normalizedResourceType = NormalizeQueryableResourceType(resourceType);
        if (normalizedResourceType == null)
        {
            return SerializePayload(new
            {
                status = "invalid_request",
                message = "resource_type 仅支持 mod、resourcepack、shader、datapack。"
            });
        }

        var normalizedPlatforms = NormalizePlatforms(platforms);
        if (normalizedPlatforms.Count == 0)
        {
            normalizedPlatforms = ["modrinth", "curseforge"];
        }

        var normalizedLoader = NormalizeLoader(loader);
        var normalizedGameVersion = NormalizeText(gameVersion);
        var effectiveLimit = Math.Clamp(limit, 1, 10);
        var effectiveQuery = _translationService.GetEnglishKeywordForSearch(query.Trim());
        var results = new List<object>();

        foreach (var platform in normalizedPlatforms)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (platform)
            {
                case "modrinth":
                    results.AddRange(await SearchModrinthAsync(
                        effectiveQuery,
                        normalizedResourceType,
                        normalizedGameVersion,
                        normalizedLoader,
                        effectiveLimit,
                        cancellationToken));
                    break;
                case "curseforge":
                    results.AddRange(await SearchCurseForgeAsync(
                        effectiveQuery,
                        normalizedResourceType,
                        normalizedGameVersion,
                        normalizedLoader,
                        effectiveLimit,
                        cancellationToken));
                    break;
            }
        }

        return SerializePayload(new
        {
            status = "ok",
            query = query.Trim(),
            effective_query = effectiveQuery,
            resource_type = normalizedResourceType,
            platforms = normalizedPlatforms,
            total_returned = results.Count,
            results
        });
    }

    public async Task<string> GetProjectFilesAsync(
        string projectId,
        string? resourceType,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return SerializePayload(new
            {
                status = "invalid_request",
                message = "project_id 不能为空。"
            });
        }

        var identity = TryParseProjectIdentity(projectId);
        if (identity == null)
        {
            return SerializePayload(new
            {
                status = "invalid_request",
                message = "project_id 无法识别，CurseForge 项目请传 curseforge-<id>。"
            });
        }

        var normalizedResourceType = NormalizeOptionalQueryableResourceType(resourceType);
        var normalizedLoader = NormalizeLoader(loader);
        var normalizedGameVersion = NormalizeText(gameVersion);
        var effectiveLimit = Math.Clamp(limit, 1, 50);

        return identity.Provider switch
        {
            CommunityResourceProvider.Modrinth => await GetModrinthProjectFilesAsync(
                identity.RawProjectId,
                normalizedResourceType,
                normalizedGameVersion,
                normalizedLoader,
                effectiveLimit,
                cancellationToken),
            CommunityResourceProvider.CurseForge => await GetCurseForgeProjectFilesAsync(
                identity.RawProjectId,
                normalizedResourceType,
                normalizedGameVersion,
                normalizedLoader,
                effectiveLimit,
                cancellationToken),
            _ => SerializePayload(new
            {
                status = "invalid_request",
                message = "暂不支持的 project_id 平台。"
            })
        };
    }

    public async Task<string> GetInstallableInstancesAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken)
    {
        var minecraftPath = _fileService.GetMinecraftDataPath();
        var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var instances = new List<object>();
        foreach (var versionName in installedVersions.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var versionDirectoryPath = Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionName);
            string resolvedGameDirectory;
            try
            {
                resolvedGameDirectory = await _gameDirResolver.GetGameDirForVersionAsync(versionName);
            }
            catch
            {
                resolvedGameDirectory = string.Empty;
            }

            string? minecraftVersion = null;
            string? loaderType = null;
            try
            {
                var config = await _versionInfoService.GetFullVersionInfoAsync(versionName, versionDirectoryPath, preferCache: true);
                minecraftVersion = NormalizeText(config?.MinecraftVersion);
                loaderType = NormalizeLoader(config?.ModLoaderType);
            }
            catch
            {
            }

            instances.Add(new
            {
                target_version_name = versionName,
                version_directory_path = versionDirectoryPath,
                resolved_game_directory = resolvedGameDirectory,
                minecraft_version = minecraftVersion,
                loader = loaderType,
                is_current_session_version = string.Equals(context.VersionId, versionName, StringComparison.OrdinalIgnoreCase)
            });
        }

        return SerializePayload(new
        {
            status = "ok",
            total_count = instances.Count,
            current_session_version = context.VersionId,
            instances
        });
    }

    public async Task<AgentCommunityResourceInstallPreparation> PrepareInstallAsync(
        AgentCommunityResourceInstallCommand command,
        CancellationToken cancellationToken)
    {
        var executionContext = await BuildInstallExecutionContextAsync(command, cancellationToken);
        if (!executionContext.IsReady)
        {
            return new AgentCommunityResourceInstallPreparation
            {
                Message = executionContext.Message
            };
        }

        return new AgentCommunityResourceInstallPreparation
        {
            Message = executionContext.Message,
            IsReadyForConfirmation = true,
            ButtonText = executionContext.ButtonText,
            ProposalParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["project_id"] = executionContext.Command.ProjectId,
                ["resource_file_id"] = executionContext.Command.ResourceFileId,
                ["target_version_name"] = executionContext.TargetVersionName,
                ["download_dependencies"] = executionContext.Command.DownloadDependencies.ToString(),
            }
        };
    }

    public async Task<string> StartInstallAsync(
        AgentCommunityResourceInstallCommand command,
        CancellationToken cancellationToken)
    {
        var executionContext = await BuildInstallExecutionContextAsync(command, cancellationToken);
        if (!executionContext.IsReady || executionContext.Plan == null || executionContext.Descriptor == null)
        {
            return executionContext.Message;
        }

        var operationId = await _communityResourceInstallService.StartInstallAsync(
            executionContext.Plan,
            executionContext.Descriptor,
            showInTeachingTip: true,
            teachingTipGroupKey: "launcher-ai-community-resource",
            cancellationToken: cancellationToken);

        return SerializePayload(new
        {
            status = "started",
            operation_id = operationId,
            project_id = executionContext.Command.ProjectId,
            resource_file_id = executionContext.Command.ResourceFileId,
            resource_type = executionContext.ResourceType,
            resource_name = executionContext.ResourceName,
            target_version_name = executionContext.TargetVersionName,
            download_dependencies = executionContext.Command.DownloadDependencies,
            message = $"已开始安装 {executionContext.ResourceName} 到 {executionContext.TargetVersionName}。可继续使用 get_operation_status 查询下载状态。"
        });
    }

    private async Task<List<object>> SearchModrinthAsync(
        string query,
        string resourceType,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken)
    {
        var facets = new List<List<string>>();
        if (!string.IsNullOrWhiteSpace(loader))
        {
            facets.Add([ $"categories:{loader}" ]);
        }

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            facets.Add([ $"versions:{gameVersion}" ]);
        }

        var result = await _modrinthService.SearchModsAsync(
            query,
            facets,
            index: "relevance",
            offset: 0,
            limit: limit,
            projectType: resourceType);
        cancellationToken.ThrowIfCancellationRequested();

        return result.Hits
            .Take(limit)
            .Select(project => (object)new
            {
                platform = "modrinth",
                project_id = project.ProjectId,
                resource_type = resourceType,
                title = project.Title,
                slug = project.Slug,
                summary = project.Description,
                author = project.Author,
                supported_loaders = project.Categories
                    .Where(category => IsLikelyLoaderToken(category) || string.Equals(resourceType, "datapack", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                game_versions = ModrinthVersionPresentationHelper.SelectRepresentativeGameVersions(project.Versions, 8),
                downloads = project.Downloads,
                icon_url = project.IconUrl?.ToString()
            })
            .ToList();
    }

    private async Task<List<object>> SearchCurseForgeAsync(
        string query,
        string resourceType,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken)
    {
        var loaderType = MapCurseForgeLoaderType(loader);
        var classId = MapCurseForgeClassId(resourceType);

        CurseForgeSearchResult result = classId == 6
            ? await _curseForgeService.SearchModsAsync(query, gameVersion, loaderType, null, 0, limit)
            : await _curseForgeService.SearchResourcesAsync(classId, query, gameVersion, loaderType, null, 0, limit);
        cancellationToken.ThrowIfCancellationRequested();

        return result.Data
            .Take(limit)
            .Select(project => (object)new
            {
                platform = "curseforge",
                project_id = $"curseforge-{project.Id}",
                resource_type = ModResourcePathHelper.MapCurseForgeClassIdToProjectType(project.ClassId),
                title = project.Name,
                slug = project.Slug,
                summary = project.Summary,
                author = project.Authors?.FirstOrDefault()?.Name,
                supported_loaders = ModDetailLoadHelper.BuildCurseForgeSupportedLoaders(project.LatestFilesIndexes),
                game_versions = project.LatestFilesIndexes
                    .Select(index => index.GameVersion)
                    .Where(version => !string.IsNullOrWhiteSpace(version))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToList(),
                downloads = project.DownloadCount,
                icon_url = project.Logo?.Url
            })
            .ToList();
    }

    private async Task<string> GetModrinthProjectFilesAsync(
        string rawProjectId,
        string? resourceType,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken)
    {
        var detail = await _modrinthService.GetProjectDetailAsync(rawProjectId);
        if (detail == null)
        {
            return SerializePayload(new
            {
                status = "not_found",
                message = "未找到对应的 Modrinth 项目。"
            });
        }

        var loaders = string.IsNullOrWhiteSpace(loader) ? null : new List<string> { loader };
        var gameVersions = string.IsNullOrWhiteSpace(gameVersion) ? null : new List<string> { gameVersion };
        var versions = await _modrinthService.GetProjectVersionsAsync(rawProjectId, loaders, gameVersions);
        cancellationToken.ThrowIfCancellationRequested();

        var detectedResourceType = DetectModrinthResourceType(resourceType, detail.ProjectType, versions);
        versions = ModDetailLoadHelper.FilterModrinthVersionsBySourceType(versions, detectedResourceType).ToList();

        var files = versions
            .Select(version =>
            {
                var file = version.Files.FirstOrDefault(candidate => candidate.Primary) ?? version.Files.FirstOrDefault();
                if (file == null)
                {
                    return null;
                }

                return new
                {
                    resource_file_id = version.Id,
                    version_id = version.Id,
                    version_number = version.VersionNumber,
                    name = version.Name,
                    file_name = file.Filename,
                    size = file.Size,
                    downloads = version.Downloads,
                    version_type = version.VersionType,
                    published_at = version.DatePublished,
                    game_versions = version.GameVersions,
                    loaders = version.Loaders
                };
            })
            .Where(item => item != null)
            .Take(limit)
            .ToList();

        return SerializePayload(new
        {
            status = "ok",
            platform = "modrinth",
            project = new
            {
                project_id = detail.Id,
                resource_type = detectedResourceType,
                title = detail.Title,
                slug = detail.Slug,
                summary = detail.Description,
                author = detail.Author,
                supported_loaders = ModDetailLoadHelper.BuildModrinthSupportedLoaders(
                    detail.Loaders,
                    detail.Categories,
                    detectedResourceType,
                    detectedResourceType is "mod" or "datapack" ? detectedResourceType : null),
                game_versions = ModrinthVersionPresentationHelper.SelectRepresentativeGameVersions(detail.GameVersions, 10),
                downloads = detail.Downloads,
                icon_url = detail.IconUrl?.ToString()
            },
            total_matching_files = versions.Count,
            returned_files = files.Count,
            truncated = versions.Count > files.Count,
            filters = new
            {
                resource_type = detectedResourceType,
                game_version = gameVersion,
                loader
            },
            files
        });
    }

    private async Task<string> GetCurseForgeProjectFilesAsync(
        string rawProjectId,
        string? resourceType,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(rawProjectId, out var modId))
        {
            return SerializePayload(new
            {
                status = "invalid_request",
                message = "CurseForge project_id 必须是 curseforge-<数字 id>。"
            });
        }

        var detail = await _curseForgeService.GetModDetailAsync(modId);
        var detectedResourceType = ModDetailLoadHelper.ResolveCurseForgeProjectType(detail.ClassId, resourceType);
        var loaderType = MapCurseForgeLoaderType(loader);

        var allFiles = new List<CurseForgeFile>();
        const int pageSize = 50;
        for (var index = 0; ; index += pageSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _curseForgeService.GetModFilesAsync(modId, gameVersion, loaderType, index, pageSize);
            if (page.Count == 0)
            {
                break;
            }

            allFiles.AddRange(page);
            if (page.Count < pageSize)
            {
                break;
            }
        }

        var files = allFiles
            .Select(file => new
            {
                resource_file_id = file.Id.ToString(),
                file_id = file.Id,
                display_name = file.DisplayName,
                file_name = file.FileName,
                size = file.FileLength,
                downloads = file.DownloadCount,
                release_type = MapCurseForgeReleaseType(file.ReleaseType),
                published_at = file.FileDate,
                game_versions = file.GameVersions
                    .Where(version => !IsLikelyLoaderToken(version))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                loaders = file.GameVersions
                    .Where(IsLikelyLoaderToken)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .Take(limit)
            .ToList();

        return SerializePayload(new
        {
            status = "ok",
            platform = "curseforge",
            project = new
            {
                project_id = $"curseforge-{detail.Id}",
                resource_type = detectedResourceType,
                title = detail.Name,
                slug = detail.Slug,
                summary = detail.Summary,
                author = detail.Authors?.FirstOrDefault()?.Name,
                supported_loaders = ModDetailLoadHelper.BuildCurseForgeSupportedLoaders(detail.LatestFilesIndexes),
                game_versions = detail.LatestFilesIndexes
                    .Select(index => index.GameVersion)
                    .Where(version => !string.IsNullOrWhiteSpace(version))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(10)
                    .ToList(),
                downloads = detail.DownloadCount,
                icon_url = detail.Logo?.Url
            },
            total_matching_files = allFiles.Count,
            returned_files = files.Count,
            truncated = allFiles.Count > files.Count,
            filters = new
            {
                resource_type = detectedResourceType,
                game_version = gameVersion,
                loader
            },
            files
        });
    }

    private async Task<CommunityResourceInstallExecutionContext> BuildInstallExecutionContextAsync(
        AgentCommunityResourceInstallCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ProjectId))
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = "project_id 不能为空。"
            }));
        }

        if (string.IsNullOrWhiteSpace(command.ResourceFileId))
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = "resource_file_id 不能为空。"
            }));
        }

        var identity = TryParseProjectIdentity(command.ProjectId);
        if (identity == null)
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = "project_id 无法识别，CurseForge 项目请传 curseforge-<id>。"
            }));
        }

        var targetVersionName = ResolveTargetVersionName(command.TargetVersionName, command.TargetVersionPath);
        if (string.IsNullOrWhiteSpace(targetVersionName))
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "missing_requirements",
                missing_requirements = new[]
                {
                    new
                    {
                        field = "target_version_name",
                        message = "必须提供 target_version_name，或提供 target_version_path 让启动器推导目标实例。"
                    }
                }
            }));
        }

        var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
        cancellationToken.ThrowIfCancellationRequested();
        if (!installedVersions.Any(version => string.Equals(version, targetVersionName, StringComparison.OrdinalIgnoreCase)))
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = $"目标实例 {targetVersionName} 不存在，请先调用 get_instances 获取可用实例。",
                available_instances = installedVersions.OrderBy(version => version, StringComparer.OrdinalIgnoreCase).ToList()
            }));
        }

        var versionDirectory = Path.Combine(_fileService.GetMinecraftDataPath(), MinecraftPathConsts.Versions, targetVersionName);
        var targetVersionConfig = await _versionInfoService.GetFullVersionInfoAsync(targetVersionName, versionDirectory, preferCache: true);
        cancellationToken.ThrowIfCancellationRequested();

        return identity.Provider switch
        {
            CommunityResourceProvider.Modrinth => await BuildModrinthInstallContextAsync(
                command,
                identity.RawProjectId,
                targetVersionName,
                targetVersionConfig,
                cancellationToken),
            CommunityResourceProvider.CurseForge => await BuildCurseForgeInstallContextAsync(
                command,
                identity.RawProjectId,
                targetVersionName,
                targetVersionConfig,
                cancellationToken),
            _ => CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = "暂不支持的资源平台。"
            }))
        };
    }

    private async Task<CommunityResourceInstallExecutionContext> BuildModrinthInstallContextAsync(
        AgentCommunityResourceInstallCommand command,
        string rawProjectId,
        string targetVersionName,
        VersionConfig targetVersionConfig,
        CancellationToken cancellationToken)
    {
        var detail = await _modrinthService.GetProjectDetailAsync(rawProjectId);
        if (detail == null)
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "not_found",
                message = "未找到对应的 Modrinth 项目。"
            }));
        }

        var version = await _modrinthService.GetVersionByIdAsync(command.ResourceFileId);
        if (version == null)
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "not_found",
                message = $"未找到 Modrinth 版本 {command.ResourceFileId}。"
            }));
        }

        if (!string.Equals(version.ProjectId, detail.Id, StringComparison.OrdinalIgnoreCase))
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = "resource_file_id 不属于当前 project_id。"
            }));
        }

        var primaryFile = version.Files.FirstOrDefault(file => file.Primary) ?? version.Files.FirstOrDefault();
        if (primaryFile == null)
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = "当前 Modrinth 版本没有可下载的主文件。"
            }));
        }

        var resourceType = DetectModrinthResourceType(command.ResourceType, detail.ProjectType, [version]);
        if (!IsAiInstallSupportedResourceType(resourceType))
        {
            return BuildOutOfScopeContext(resourceType);
        }

        var planningResult = await _communityResourceInstallPlanner.PlanAsync(new CommunityResourceInstallRequest
        {
            ResourceType = resourceType,
            FileName = primaryFile.Filename,
            TargetVersionName = targetVersionName,
            UseCustomDownloadPath = false,
        }, cancellationToken);

        var planContext = CreatePlanContext(command, targetVersionName, detail.Title, resourceType, planningResult);
        if (planContext.Plan == null)
        {
            return planContext;
        }

        var descriptor = new CommunityResourceInstallDescriptor
        {
            ResourceName = detail.Title,
            ResourceIconUrl = detail.IconUrl?.ToString() ?? string.Empty,
            FileName = primaryFile.Filename,
            DownloadUrl = primaryFile.Url?.ToString() ?? string.Empty,
            CommunityResourceProvider = CommunityResourceProvider.Modrinth,
            OriginalVersion = version,
            TargetLoaderType = NormalizeLoader(targetVersionConfig.ModLoaderType),
            TargetGameVersion = NormalizeText(targetVersionConfig.MinecraftVersion),
        };

        return CreateReadyContext(command, targetVersionName, detail.Title, resourceType, planContext.Plan, descriptor);
    }

    private async Task<CommunityResourceInstallExecutionContext> BuildCurseForgeInstallContextAsync(
        AgentCommunityResourceInstallCommand command,
        string rawProjectId,
        string targetVersionName,
        VersionConfig targetVersionConfig,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(rawProjectId, out var modId))
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = "CurseForge project_id 必须是 curseforge-<数字 id>。"
            }));
        }

        if (!int.TryParse(command.ResourceFileId, out var fileId))
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = "CurseForge 的 resource_file_id 必须是数字文件 ID。"
            }));
        }

        var detail = await _curseForgeService.GetModDetailAsync(modId);
        var file = await _curseForgeService.GetFileAsync(modId, fileId);
        cancellationToken.ThrowIfCancellationRequested();
        if (file == null)
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "not_found",
                message = $"未找到 CurseForge 文件 {fileId}。"
            }));
        }

        var resourceType = ModDetailLoadHelper.ResolveCurseForgeProjectType(detail.ClassId, command.ResourceType);
        if (!IsAiInstallSupportedResourceType(resourceType))
        {
            return BuildOutOfScopeContext(resourceType);
        }

        var planningResult = await _communityResourceInstallPlanner.PlanAsync(new CommunityResourceInstallRequest
        {
            ResourceType = resourceType,
            FileName = file.FileName,
            TargetVersionName = targetVersionName,
            UseCustomDownloadPath = false,
        }, cancellationToken);

        var planContext = CreatePlanContext(command, targetVersionName, detail.Name, resourceType, planningResult);
        if (planContext.Plan == null)
        {
            return planContext;
        }

        var descriptor = new CommunityResourceInstallDescriptor
        {
            ResourceName = detail.Name,
            ResourceIconUrl = detail.Logo?.Url ?? string.Empty,
            FileName = file.FileName,
            DownloadUrl = file.DownloadUrl ?? string.Empty,
            CommunityResourceProvider = CommunityResourceProvider.CurseForge,
            OriginalCurseForgeFile = file,
            TargetLoaderType = NormalizeLoader(targetVersionConfig.ModLoaderType),
            TargetGameVersion = NormalizeText(targetVersionConfig.MinecraftVersion),
        };

        return CreateReadyContext(command, targetVersionName, detail.Name, resourceType, planContext.Plan, descriptor);
    }

    private CommunityResourceInstallExecutionContext CreatePlanContext(
        AgentCommunityResourceInstallCommand command,
        string targetVersionName,
        string resourceName,
        string resourceType,
        CommunityResourceInstallPlanningResult planningResult)
    {
        if (!string.IsNullOrWhiteSpace(planningResult.UnsupportedReason))
        {
            return BuildOutOfScopeContext(resourceType, planningResult.UnsupportedReason);
        }

        if (planningResult.MissingRequirements.Count > 0)
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "missing_requirements",
                missing_requirements = planningResult.MissingRequirements.Select(requirement => new
                {
                    requirement_type = requirement.Type.ToString(),
                    field = requirement.Key,
                    requirement.Message,
                }).ToList()
            }));
        }

        if (planningResult.Plan == null)
        {
            return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = "安装规划失败，未生成有效安装计划。"
            }));
        }

        var overriddenPlan = new CommunityResourceInstallPlan
        {
            ResourceKind = planningResult.Plan.ResourceKind,
            NormalizedResourceType = planningResult.Plan.NormalizedResourceType,
            GameDirectory = planningResult.Plan.GameDirectory,
            TargetVersionName = planningResult.Plan.TargetVersionName,
            TargetSaveName = planningResult.Plan.TargetSaveName,
            PrimaryTargetDirectory = planningResult.Plan.PrimaryTargetDirectory,
            DependencyTargetDirectory = planningResult.Plan.DependencyTargetDirectory,
            SavePath = planningResult.Plan.SavePath,
            UseTargetDirectoryForAllDependencies = planningResult.Plan.UseTargetDirectoryForAllDependencies,
            DownloadDependencies = command.DownloadDependencies,
            UseCustomDownloadPath = planningResult.Plan.UseCustomDownloadPath,
        };

        return CreateReadyContext(command, targetVersionName, resourceName, resourceType, overriddenPlan, descriptor: null);
    }

    private CommunityResourceInstallExecutionContext CreateReadyContext(
        AgentCommunityResourceInstallCommand command,
        string targetVersionName,
        string resourceName,
        string resourceType,
        CommunityResourceInstallPlan plan,
        CommunityResourceInstallDescriptor? descriptor)
    {
        return new CommunityResourceInstallExecutionContext
        {
            Command = command,
            IsReady = descriptor != null,
            Message = SerializePayload(new
            {
                status = descriptor == null ? "ready" : "ready_for_confirmation",
                project_id = command.ProjectId,
                resource_file_id = command.ResourceFileId,
                resource_type = resourceType,
                resource_name = resourceName,
                target_version_name = targetVersionName,
                download_dependencies = command.DownloadDependencies,
                plan = new
                {
                    target_directory = plan.PrimaryTargetDirectory,
                    dependency_target_directory = plan.DependencyTargetDirectory,
                    save_path = plan.SavePath,
                },
                message = $"已准备安装 {resourceName} 到 {targetVersionName}。等待用户确认。"
            }),
            ButtonText = $"安装 {resourceName}",
            TargetVersionName = targetVersionName,
            ResourceName = resourceName,
            ResourceType = resourceType,
            Plan = plan,
            Descriptor = descriptor,
        };
    }

    private static CommunityResourceInstallExecutionContext BuildOutOfScopeContext(string resourceType, string? reason = null)
    {
        var message = resourceType switch
        {
            "datapack" => "数据包安装需要目标世界/存档选择，当前 AI V1 还没有 get_worlds 之类的补参工具，因此暂不支持直接安装。",
            "world" => "世界安装仍属于二期/Phase 5 范围，当前 AI V1 不支持直接安装。",
            "modpack" => "整合包安装/更新仍属于二期/Phase 5 范围，当前 AI V1 不支持直接安装。",
            _ => reason ?? "当前资源类型超出 AI V1 安装范围。"
        };

        return CommunityResourceInstallExecutionContext.FromMessage(SerializePayload(new
        {
            status = "out_of_scope",
            resource_type = resourceType,
            message
        }));
    }

    private static string ResolveTargetVersionName(string? targetVersionName, string? targetVersionPath)
    {
        var normalizedTargetVersionName = NormalizeText(targetVersionName);
        if (!string.IsNullOrWhiteSpace(normalizedTargetVersionName))
        {
            return normalizedTargetVersionName;
        }

        var normalizedTargetVersionPath = NormalizeText(targetVersionPath);
        if (string.IsNullOrWhiteSpace(normalizedTargetVersionPath))
        {
            return string.Empty;
        }

        return Path.GetFileName(normalizedTargetVersionPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static ParsedProjectIdentity? TryParseProjectIdentity(string projectId)
    {
        var normalized = NormalizeText(projectId);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.StartsWith("curseforge-", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedProjectIdentity(CommunityResourceProvider.CurseForge, normalized["curseforge-".Length..]);
        }

        return new ParsedProjectIdentity(CommunityResourceProvider.Modrinth, normalized);
    }

    private static string? NormalizeQueryableResourceType(string? resourceType)
    {
        var normalized = NormalizeOptionalQueryableResourceType(resourceType);
        return normalized is "mod" or "resourcepack" or "shader" or "datapack" ? normalized : null;
    }

    private static string? NormalizeOptionalQueryableResourceType(string? resourceType)
    {
        var normalized = NormalizeText(resourceType);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        normalized = ModResourcePathHelper.NormalizeProjectType(normalized);
        return normalized switch
        {
            "mod" or "resourcepack" or "shader" or "datapack" => normalized,
            _ => null
        };
    }

    private static bool IsAiInstallSupportedResourceType(string resourceType) =>
        resourceType is "mod" or "resourcepack" or "shader";

    private static List<string> NormalizePlatforms(IReadOnlyList<string>? platforms)
    {
        var normalizedPlatforms = new List<string>();
        if (platforms == null)
        {
            return normalizedPlatforms;
        }

        foreach (var platform in platforms)
        {
            var normalized = NormalizeText(platform)?.ToLowerInvariant();
            if (normalized is not ("modrinth" or "curseforge"))
            {
                continue;
            }

            if (!normalizedPlatforms.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                normalizedPlatforms.Add(normalized);
            }
        }

        return normalizedPlatforms;
    }

    private static int MapCurseForgeClassId(string resourceType) =>
        resourceType switch
        {
            "resourcepack" => 12,
            "shader" => 6552,
            "datapack" => 6945,
            _ => 6,
        };

    private static int? MapCurseForgeLoaderType(string? loader) =>
        NormalizeLoader(loader) switch
        {
            "forge" => 1,
            "liteloader" => 3,
            "fabric" => 4,
            "quilt" => 5,
            "neoforge" => 6,
            _ => null,
        };

    private static string DetectModrinthResourceType(string? requestedResourceType, string? projectType, IEnumerable<ModrinthVersion>? versions)
    {
        var normalizedRequested = NormalizeOptionalQueryableResourceType(requestedResourceType);
        if (!string.IsNullOrWhiteSpace(normalizedRequested))
        {
            return normalizedRequested;
        }

        var versionList = versions?.ToList() ?? [];
        if (versionList.Any(version => version.Loaders.Any(loader => string.Equals(loader, "datapack", StringComparison.OrdinalIgnoreCase))))
        {
            return "datapack";
        }

        return ModResourcePathHelper.NormalizeProjectType(projectType);
    }

    private static bool IsLikelyLoaderToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return CurseForgeLoaderTokens.Contains(value.Trim());
    }

    private static string MapCurseForgeReleaseType(int releaseType) =>
        releaseType switch
        {
            1 => "release",
            2 => "beta",
            3 => "alpha",
            _ => "unknown",
        };

    private static string NormalizeLoader(string? loader)
    {
        var normalized = NormalizeText(loader)?.ToLowerInvariant();
        return normalized is "vanilla" or "auto" ? string.Empty : normalized ?? string.Empty;
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string SerializePayload(object payload)
    {
        return JsonConvert.SerializeObject(payload, Formatting.Indented);
    }

    private sealed record ParsedProjectIdentity(CommunityResourceProvider Provider, string RawProjectId);

    private sealed class CommunityResourceInstallExecutionContext
    {
        public required AgentCommunityResourceInstallCommand Command { get; init; }

        public bool IsReady { get; init; }

        public required string Message { get; init; }

        public string ButtonText { get; init; } = string.Empty;

        public string TargetVersionName { get; init; } = string.Empty;

        public string ResourceName { get; init; } = string.Empty;

        public string ResourceType { get; init; } = string.Empty;

        public CommunityResourceInstallPlan? Plan { get; init; }

        public CommunityResourceInstallDescriptor? Descriptor { get; init; }

        public static CommunityResourceInstallExecutionContext FromMessage(string message)
        {
            return new CommunityResourceInstallExecutionContext
            {
                Command = new AgentCommunityResourceInstallCommand(),
                Message = message
            };
        }
    }
}