using Newtonsoft.Json;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.Features.ModDownloadDetail.Services;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public interface IAgentCommunityResourceService
{
    Task<string> SearchAsync(
        string query,
        string resourceType,
        IReadOnlyList<string>? platforms,
        IReadOnlyList<string>? categoryTokens,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken);

    Task<string> GetCommunityResourceTagsAsync(
        string resourceType,
        IReadOnlyList<string>? platforms,
        CancellationToken cancellationToken);

    Task<string> GetProjectDetailAsync(
        string projectId,
        string? resourceType,
        bool includeBody,
        int bodyMaxChars,
        CancellationToken cancellationToken);

    Task<string> GetProjectFilesAsync(
        string projectId,
        string? resourceType,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken);

    Task<string> GetInstallableInstancesAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken);

    Task<string> GetInstanceCommunityResourcesAsync(
        AgentInstanceCommunityResourceInventoryCommand command,
        CancellationToken cancellationToken);

    Task<string> CheckInstanceCommunityResourceUpdatesAsync(
        AgentInstanceCommunityResourceUpdateCheckCommand command,
        CancellationToken cancellationToken);

    Task<AgentCommunityResourceUpdatePreparation> PrepareUpdateAsync(
        AgentInstanceCommunityResourceUpdateCommand command,
        CancellationToken cancellationToken);

    Task<string> StartUpdateAsync(
        AgentInstanceCommunityResourceUpdateCommand command,
        CancellationToken cancellationToken);

    Task<AgentCommunityResourceInstallPreparation> PrepareInstallAsync(
        AgentCommunityResourceInstallCommand command,
        CancellationToken cancellationToken);

    Task<string> StartInstallAsync(
        AgentCommunityResourceInstallCommand command,
        CancellationToken cancellationToken);
}

public sealed class AgentInstanceCommunityResourceInventoryCommand
{
    public string? TargetVersionName { get; init; }

    public string? TargetVersionPath { get; init; }

    public IReadOnlyCollection<string>? ResourceTypes { get; init; }
}

public sealed class AgentInstanceCommunityResourceUpdateCheckCommand
{
    public string? TargetVersionName { get; init; }

    public string? TargetVersionPath { get; init; }

    public IReadOnlyCollection<string>? ResourceInstanceIds { get; init; }

    public string? CheckScope { get; init; }
}

public sealed class AgentInstanceCommunityResourceUpdateCommand
{
    public string? TargetVersionName { get; init; }

    public string? TargetVersionPath { get; init; }

    public IReadOnlyCollection<string>? ResourceInstanceIds { get; init; }

    public string? SelectionMode { get; init; }
}

public sealed class AgentCommunityResourceUpdatePreparation
{
    public required string Message { get; init; }

    public bool IsReadyForConfirmation { get; init; }

    public string ButtonText { get; init; } = string.Empty;

    public Dictionary<string, string> ProposalParameters { get; init; } = [];
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
    private readonly ICommunityResourceInventoryService _communityResourceInventoryService;
    private readonly ICommunityResourceUpdateCheckService _communityResourceUpdateCheckService;
    private readonly ICommunityResourceUpdateService _communityResourceUpdateService;
    private readonly ICommunityResourceInstallPlanner _communityResourceInstallPlanner;
    private readonly ICommunityResourceInstallService _communityResourceInstallService;
    private readonly ITranslationService _translationService;
    private readonly ICommunityResourceFilterMetadataService _communityResourceFilterMetadataService;

    public AgentCommunityResourceService(
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        IMinecraftVersionService minecraftVersionService,
        IVersionInfoService versionInfoService,
        IFileService fileService,
        IGameDirResolver gameDirResolver,
        ICommunityResourceInventoryService communityResourceInventoryService,
        ICommunityResourceUpdateCheckService communityResourceUpdateCheckService,
        ICommunityResourceUpdateService communityResourceUpdateService,
        ICommunityResourceInstallPlanner communityResourceInstallPlanner,
        ICommunityResourceInstallService communityResourceInstallService,
        ITranslationService translationService,
        ICommunityResourceFilterMetadataService communityResourceFilterMetadataService)
    {
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _minecraftVersionService = minecraftVersionService;
        _versionInfoService = versionInfoService;
        _fileService = fileService;
        _gameDirResolver = gameDirResolver;
        _communityResourceInventoryService = communityResourceInventoryService;
        _communityResourceUpdateCheckService = communityResourceUpdateCheckService;
        _communityResourceUpdateService = communityResourceUpdateService;
        _communityResourceInstallPlanner = communityResourceInstallPlanner;
        _communityResourceInstallService = communityResourceInstallService;
        _translationService = translationService;
        _communityResourceFilterMetadataService = communityResourceFilterMetadataService;
    }

    public async Task<string> SearchAsync(
        string query,
        string resourceType,
        IReadOnlyList<string>? platforms,
        IReadOnlyList<string>? categoryTokens,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken)
    {
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
        var normalizedCategoryTokens = NormalizeCategoryTokens(categoryTokens);
        var normalizedGameVersion = NormalizeText(gameVersion);
        var normalizedQuery = NormalizeText(query) ?? string.Empty;
        var effectiveLimit = Math.Clamp(limit, 1, 10);
        var effectiveQuery = string.IsNullOrEmpty(normalizedQuery)
            ? string.Empty
            : _translationService.GetEnglishKeywordForSearch(normalizedQuery);
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
                        normalizedCategoryTokens,
                        normalizedGameVersion,
                        normalizedLoader,
                        effectiveLimit,
                        cancellationToken));
                    break;
                case "curseforge":
                    results.AddRange(await SearchCurseForgeAsync(
                        effectiveQuery,
                        normalizedResourceType,
                        normalizedCategoryTokens,
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
            query = normalizedQuery,
            effective_query = effectiveQuery,
            query_provided = !string.IsNullOrEmpty(normalizedQuery),
            resource_type = normalizedResourceType,
            platforms = normalizedPlatforms,
            category_tokens = normalizedCategoryTokens,
            total_returned = results.Count,
            results
        });
    }

    public async Task<string> GetCommunityResourceTagsAsync(
        string resourceType,
        IReadOnlyList<string>? platforms,
        CancellationToken cancellationToken)
    {
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

        var metadata = await _communityResourceFilterMetadataService.GetFilterMetadataAsync(
            normalizedResourceType,
            normalizedPlatforms,
            includeAllCategory: false,
            cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var categories = metadata.Categories
            .Select(category => new
            {
                token = category.Tag,
                display_name = category.DisplayName,
                source = category.Source,
                category_id = category.Id
            })
            .ToList();

        return SerializePayload(new
        {
            status = "ok",
            resource_type = normalizedResourceType,
            platforms = metadata.Platforms,
            category_count = categories.Count,
            loader_count = metadata.Loaders.Count,
            categories,
            loaders = metadata.Loaders,
            usage = new
            {
                search_argument = "category_tokens",
                search_loader_argument = "loader",
                note = "将 categories[*].token 直接传给 searchCommunityResources.category_tokens。非数字 token 会作用于 Modrinth，纯数字 token 会作用于 CurseForge；这与资源下载页当前筛选行为保持一致。"
            }
        });
    }

    public async Task<string> GetProjectDetailAsync(
        string projectId,
        string? resourceType,
        bool includeBody,
        int bodyMaxChars,
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
        var effectiveBodyMaxChars = Math.Clamp(bodyMaxChars <= 0 ? 4000 : bodyMaxChars, 500, 12000);

        return identity.Provider switch
        {
            CommunityResourceProvider.Modrinth => await GetModrinthProjectDetailAsync(
                identity.RawProjectId,
                normalizedResourceType,
                includeBody,
                effectiveBodyMaxChars,
                cancellationToken),
            CommunityResourceProvider.CurseForge => await GetCurseForgeProjectDetailAsync(
                identity.RawProjectId,
                normalizedResourceType,
                includeBody,
                effectiveBodyMaxChars,
                cancellationToken),
            _ => SerializePayload(new
            {
                status = "invalid_request",
                message = "暂不支持的资源平台。"
            })
        };
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

    public async Task<string> GetInstanceCommunityResourcesAsync(
        AgentInstanceCommunityResourceInventoryCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        ResolvedTargetVersionContext targetVersion = await ResolveTargetVersionAsync(
            command.TargetVersionName,
            command.TargetVersionPath,
            cancellationToken);
        if (!targetVersion.IsReady)
        {
            return targetVersion.Message;
        }

        string? invalidResourceType;
        IReadOnlyCollection<string>? resourceTypes = NormalizeInventoryResourceTypes(command.ResourceTypes, out invalidResourceType);
        if (!string.IsNullOrWhiteSpace(invalidResourceType))
        {
            return SerializePayload(new
            {
                status = "invalid_request",
                message = $"resource_types 包含不支持的类型: {invalidResourceType}。仅支持 mod、shader、resourcepack、world、datapack。"
            });
        }

        CommunityResourceInventoryResult result = await _communityResourceInventoryService.ListAsync(
            new CommunityResourceInventoryRequest
            {
                TargetVersionName = targetVersion.TargetVersionName,
                ResolvedGameDirectory = targetVersion.ResolvedGameDirectory,
                ResourceTypes = resourceTypes,
            },
            cancellationToken);

        return SerializePayload(new
        {
            status = "ok",
            target_version_name = result.TargetVersionName,
            version_directory_path = targetVersion.VersionDirectoryPath,
            resolved_game_directory = result.ResolvedGameDirectory,
            total_count = result.Resources.Count,
            requested_resource_types = resourceTypes,
            resources = result.Resources.Select(SerializeInventoryItem).ToList()
        });
    }

    public async Task<string> CheckInstanceCommunityResourceUpdatesAsync(
        AgentInstanceCommunityResourceUpdateCheckCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        ResolvedTargetVersionContext targetVersion = await ResolveTargetVersionAsync(
            command.TargetVersionName,
            command.TargetVersionPath,
            cancellationToken);
        if (!targetVersion.IsReady)
        {
            return targetVersion.Message;
        }

        HashSet<string>? requestedIds = NormalizeStringSet(command.ResourceInstanceIds);
        string effectiveScope = ResolveEffectiveCheckScope(command.CheckScope, requestedIds);
        if (requestedIds == null && !string.Equals(effectiveScope, "all_installed", StringComparison.OrdinalIgnoreCase))
        {
            return SerializePayload(new
            {
                status = "missing_requirements",
                missing_requirements = new[]
                {
                    new
                    {
                        field = "resource_instance_ids",
                        message = "请提供 resource_instance_ids，或显式传 check_scope=all_installed。"
                    }
                }
            });
        }

        CommunityResourceUpdateCheckResult result = await _communityResourceUpdateCheckService.CheckAsync(
            new CommunityResourceUpdateCheckRequest
            {
                TargetVersionName = targetVersion.TargetVersionName,
                ResolvedGameDirectory = targetVersion.ResolvedGameDirectory,
                ResourceInstanceIds = requestedIds,
            },
            cancellationToken);

        UpdateCheckSummary summary = BuildUpdateCheckSummary(result.Items, requestedIds);

        return SerializePayload(new
        {
            status = "ok",
            check_scope = requestedIds == null ? "all_installed" : "selected",
            target_version_name = result.TargetVersionName,
            version_directory_path = targetVersion.VersionDirectoryPath,
            resolved_game_directory = result.ResolvedGameDirectory,
            minecraft_version = result.MinecraftVersion,
            loader = NormalizeLoader(result.ModLoaderType),
            checked_at = result.CheckedAt,
            total_count = result.Items.Count,
            summary = new
            {
                update_available = summary.UpdateAvailable,
                up_to_date = summary.UpToDate,
                unsupported = summary.Unsupported,
                not_identified = summary.NotIdentified,
                failed = summary.Failed,
                missing = summary.Missing,
            },
            items = result.Items.Select(SerializeUpdateCheckItem).ToList()
        });
    }

    public async Task<AgentCommunityResourceUpdatePreparation> PrepareUpdateAsync(
        AgentInstanceCommunityResourceUpdateCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        UpdatePreparationContext context = await BuildUpdatePreparationContextAsync(command, cancellationToken);
        if (!context.IsReady)
        {
            return new AgentCommunityResourceUpdatePreparation
            {
                Message = context.Message
            };
        }

        Dictionary<string, string> proposalParameters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["target_version_name"] = context.TargetVersionName,
            ["selection_mode"] = context.SelectionMode,
        };

        if (context.ResourceInstanceIds != null && context.ResourceInstanceIds.Count > 0)
        {
            proposalParameters["resource_instance_ids"] = JsonConvert.SerializeObject(
                context.ResourceInstanceIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList(),
                Formatting.None);
        }

        return new AgentCommunityResourceUpdatePreparation
        {
            Message = context.Message,
            IsReadyForConfirmation = true,
            ButtonText = context.ButtonText,
            ProposalParameters = proposalParameters,
        };
    }

    public async Task<string> StartUpdateAsync(
        AgentInstanceCommunityResourceUpdateCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        ResolvedTargetVersionContext targetVersion = await ResolveTargetVersionAsync(
            command.TargetVersionName,
            command.TargetVersionPath,
            cancellationToken);
        if (!targetVersion.IsReady)
        {
            return targetVersion.Message;
        }

        string? selectionMode = NormalizeSelectionMode(command.SelectionMode, command.ResourceInstanceIds);
        if (selectionMode == null)
        {
            return SerializePayload(new
            {
                status = "missing_requirements",
                missing_requirements = new[]
                {
                    new
                    {
                        field = "selection_mode",
                        message = "请提供 resource_instance_ids，或传 selection_mode=all_updatable。"
                    }
                }
            });
        }

        HashSet<string>? requestedIds = string.Equals(selectionMode, CommunityResourceUpdateRequest.ExplicitSelectionMode, StringComparison.Ordinal)
            ? NormalizeStringSet(command.ResourceInstanceIds)
            : null;

        if (string.Equals(selectionMode, CommunityResourceUpdateRequest.ExplicitSelectionMode, StringComparison.Ordinal) &&
            requestedIds == null)
        {
            return SerializePayload(new
            {
                status = "missing_requirements",
                missing_requirements = new[]
                {
                    new
                    {
                        field = "resource_instance_ids",
                        message = "显式更新至少需要一个 resource_instance_id。"
                    }
                }
            });
        }

        string operationId = await _communityResourceUpdateService.StartUpdateAsync(
            new CommunityResourceUpdateRequest
            {
                TargetVersionName = targetVersion.TargetVersionName,
                ResolvedGameDirectory = targetVersion.ResolvedGameDirectory,
                ResourceInstanceIds = requestedIds,
                SelectionMode = selectionMode,
            },
            cancellationToken);

        return SerializePayload(new
        {
            status = "started",
            operation_id = operationId,
            target_version_name = targetVersion.TargetVersionName,
            version_directory_path = targetVersion.VersionDirectoryPath,
            resolved_game_directory = targetVersion.ResolvedGameDirectory,
            selection_mode = selectionMode,
            resource_instance_ids = requestedIds,
            message = $"已开始在 {targetVersion.TargetVersionName} 中批量更新社区资源。可继续使用 getOperationStatus 查询进度。"
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
            teachingTipGroupKey: CreateInstallTeachingTipGroupKey(),
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
            message = $"已开始安装 {executionContext.ResourceName} 到 {executionContext.TargetVersionName}。可继续使用 getOperationStatus 查询下载状态。"
        });
    }

    private static string CreateInstallTeachingTipGroupKey()
    {
        return $"launcher-ai-community-resource-{Guid.NewGuid():N}";
    }

    private async Task<List<object>> SearchModrinthAsync(
        string query,
        string resourceType,
        IReadOnlyList<string> categoryTokens,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken)
    {
        var facets = new List<List<string>>();
        var modrinthCategoryTokens = GetModrinthCategoryTokens(categoryTokens);

        if (modrinthCategoryTokens.Count > 0)
        {
            facets.Add(modrinthCategoryTokens.Select(tag => $"categories:{tag}").ToList());
        }

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
        IReadOnlyList<string> categoryTokens,
        string? gameVersion,
        string? loader,
        int limit,
        CancellationToken cancellationToken)
    {
        var loaderType = MapCurseForgeLoaderType(loader);
        var classId = MapCurseForgeClassId(resourceType);
        var categoryIds = GetCurseForgeCategoryIds(categoryTokens);
        var projects = await SearchCurseForgeProjectsAsync(classId, query, gameVersion, loaderType, categoryIds, limit, cancellationToken);

        return projects
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

    private async Task<string> GetModrinthProjectDetailAsync(
        string rawProjectId,
        string? requestedResourceType,
        bool includeBody,
        int bodyMaxChars,
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

        cancellationToken.ThrowIfCancellationRequested();

        var detectedResourceType = DetectModrinthProjectDetailResourceType(requestedResourceType, detail);
        var supportedLoaders = ModDetailLoadHelper.BuildModrinthSupportedLoaders(
            detail.Loaders,
            detail.Categories,
            detectedResourceType,
            detectedResourceType is "mod" or "datapack" ? detectedResourceType : null);
        var gameVersions = ModrinthVersionPresentationHelper.SelectRepresentativeGameVersions(detail.GameVersions, 20);
        var body = CreateProjectDetailBody(detail.Body, includeBody, bodyMaxChars);

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
                downloads = detail.Downloads,
                followers = detail.Followers,
                license = detail.License?.Name,
                icon_url = detail.IconUrl?.ToString(),
                project_url = ModResourcePathHelper.GenerateModrinthUrl(detectedResourceType, detail.Slug),
                published_at = detail.Published,
                updated_at = detail.Updated,
                status = detail.Status,
                client_side = NormalizeText(detail.ClientSide),
                server_side = NormalizeText(detail.ServerSide),
                team_id = detail.Team
            },
            categories = BuildModrinthCategoryDetails(detail.Categories),
            additional_categories = BuildModrinthCategoryDetails(detail.AdditionalCategories),
            supported_loaders = supportedLoaders,
            game_versions = gameVersions,
            links = new
            {
                issues_url = detail.IssuesUrl?.ToString(),
                source_url = detail.SourceUrl?.ToString(),
                wiki_url = detail.WikiUrl?.ToString(),
                discord_url = detail.DiscordUrl?.ToString()
            },
            body = new
            {
                has_body = body.HasBody,
                preview = body.Preview,
                preview_truncated = body.PreviewTruncated,
                content = body.Content,
                content_included = includeBody && body.HasBody,
                content_truncated = body.ContentTruncated,
                original_length = body.OriginalLength
            }
        });
    }

    private async Task<string> GetCurseForgeProjectDetailAsync(
        string rawProjectId,
        string? requestedResourceType,
        bool includeBody,
        int bodyMaxChars,
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
        cancellationToken.ThrowIfCancellationRequested();

        var detectedResourceType = ModDetailLoadHelper.ResolveCurseForgeProjectType(detail.ClassId, requestedResourceType);
        var supportedLoaders = ModDetailLoadHelper.BuildCurseForgeSupportedLoaders(detail.LatestFilesIndexes);
        var gameVersions = ModrinthVersionPresentationHelper.SelectRepresentativeGameVersions(
            detail.LatestFilesIndexes
                .Select(index => index.GameVersion)
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .ToList(),
            20);
        var body = CreateProjectDetailBody(detail.Description, includeBody, bodyMaxChars);

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
                downloads = detail.DownloadCount,
                followers = (int?)null,
                license = (string?)null,
                icon_url = detail.Logo?.Url,
                project_url = NormalizeText(detail.Links?.WebsiteUrl),
                published_at = detail.DateReleased == default ? null : detail.DateReleased.ToString("O"),
                updated_at = detail.DateModified == default ? null : detail.DateModified.ToString("O"),
                status = detail.Status.ToString(),
                client_side = (string?)null,
                server_side = (string?)null,
                team_id = (string?)null,
                allow_mod_distribution = detail.AllowModDistribution,
                game_popularity_rank = detail.GamePopularityRank
            },
            categories = BuildCurseForgeCategoryDetails(detail.Categories, detail.PrimaryCategoryId),
            additional_categories = Array.Empty<object>(),
            supported_loaders = supportedLoaders,
            game_versions = gameVersions,
            links = new
            {
                issues_url = NormalizeText(detail.Links?.IssuesUrl),
                source_url = NormalizeText(detail.Links?.SourceUrl),
                wiki_url = NormalizeText(detail.Links?.WikiUrl),
                discord_url = (string?)null
            },
            authors = detail.Authors?
                .Where(author => !string.IsNullOrWhiteSpace(author.Name))
                .Select(author => new
                {
                    name = author.Name,
                    url = NormalizeText(author.Url)
                })
                .ToList(),
            body = new
            {
                has_body = body.HasBody,
                preview = body.Preview,
                preview_truncated = body.PreviewTruncated,
                content = body.Content,
                content_included = includeBody && body.HasBody,
                content_truncated = body.ContentTruncated,
                original_length = body.OriginalLength
            }
        });
    }

    private async Task<List<CurseForgeMod>> SearchCurseForgeProjectsAsync(
        int classId,
        string query,
        string? gameVersion,
        int? loaderType,
        IReadOnlyList<int> categoryIds,
        int limit,
        CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(4);
        var effectiveCategoryIds = categoryIds.Count > 0 ? categoryIds : [0];
        var results = new List<CurseForgeMod>();
        var deduplicated = new HashSet<int>();

        async Task<List<CurseForgeMod>> RunSearchAsync(int categoryId)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var effectiveCategoryId = categoryId == 0 ? (int?)null : categoryId;
                var result = classId == 6
                    ? await _curseForgeService.SearchModsAsync(query, gameVersion, loaderType, effectiveCategoryId, 0, limit)
                    : await _curseForgeService.SearchResourcesAsync(classId, query, gameVersion, loaderType, effectiveCategoryId, 0, limit);
                cancellationToken.ThrowIfCancellationRequested();
                return result.Data;
            }
            finally
            {
                semaphore.Release();
            }
        }

        var searchResults = await Task.WhenAll(effectiveCategoryIds.Select(RunSearchAsync));
        foreach (var batch in searchResults)
        {
            foreach (var project in batch)
            {
                if (deduplicated.Add(project.Id))
                {
                    results.Add(project);
                }
            }
        }

        return results;
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
                message = $"目标实例 {targetVersionName} 不存在，请先调用 getInstances 获取可用实例。",
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

    private async Task<ResolvedTargetVersionContext> ResolveTargetVersionAsync(
        string? targetVersionName,
        string? targetVersionPath,
        CancellationToken cancellationToken)
    {
        string? normalizedTargetVersionName = NormalizeText(targetVersionName);
        string derivedTargetVersionName = ResolveTargetVersionName(null, targetVersionPath);

        if (!string.IsNullOrWhiteSpace(normalizedTargetVersionName) &&
            !string.IsNullOrWhiteSpace(derivedTargetVersionName) &&
            !string.Equals(normalizedTargetVersionName, derivedTargetVersionName, StringComparison.OrdinalIgnoreCase))
        {
            return ResolvedTargetVersionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = "target_version_name 与 target_version_path 指向的实例不一致。请优先使用 getInstances 返回的 target_version_name / version_directory_path。"
            }));
        }

        string effectiveTargetVersionName = string.IsNullOrWhiteSpace(normalizedTargetVersionName)
            ? derivedTargetVersionName
            : normalizedTargetVersionName;

        if (string.IsNullOrWhiteSpace(effectiveTargetVersionName))
        {
            return ResolvedTargetVersionContext.FromMessage(SerializePayload(new
            {
                status = "missing_requirements",
                missing_requirements = new[]
                {
                    new
                    {
                        field = "target_version_name",
                        message = "必须提供 target_version_name，或提供 target_version_path 让启动器推导目标实例。建议先调用 getInstances。"
                    }
                }
            }));
        }

        IReadOnlyList<string> installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (!installedVersions.Any(version => string.Equals(version, effectiveTargetVersionName, StringComparison.OrdinalIgnoreCase)))
        {
            return ResolvedTargetVersionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = $"目标实例 {effectiveTargetVersionName} 不存在，请先调用 getInstances 获取可用实例。",
                available_instances = installedVersions.OrderBy(version => version, StringComparer.OrdinalIgnoreCase).ToList()
            }));
        }

        string versionDirectoryPath = Path.Combine(_fileService.GetMinecraftDataPath(), MinecraftPathConsts.Versions, effectiveTargetVersionName);

        string resolvedGameDirectory;
        try
        {
            resolvedGameDirectory = await _gameDirResolver.GetGameDirForVersionAsync(effectiveTargetVersionName);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception ex)
        {
            return ResolvedTargetVersionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = $"无法解析目标实例 {effectiveTargetVersionName} 的游戏目录：{ex.Message}"
            }));
        }

        if (string.IsNullOrWhiteSpace(resolvedGameDirectory))
        {
            return ResolvedTargetVersionContext.FromMessage(SerializePayload(new
            {
                status = "invalid_request",
                message = $"无法解析目标实例 {effectiveTargetVersionName} 的游戏目录。"
            }));
        }

        return new ResolvedTargetVersionContext
        {
            IsReady = true,
            TargetVersionName = effectiveTargetVersionName,
            VersionDirectoryPath = versionDirectoryPath,
            ResolvedGameDirectory = resolvedGameDirectory,
        };
    }

    private async Task<UpdatePreparationContext> BuildUpdatePreparationContextAsync(
        AgentInstanceCommunityResourceUpdateCommand command,
        CancellationToken cancellationToken)
    {
        ResolvedTargetVersionContext targetVersion = await ResolveTargetVersionAsync(
            command.TargetVersionName,
            command.TargetVersionPath,
            cancellationToken);
        if (!targetVersion.IsReady)
        {
            return UpdatePreparationContext.FromMessage(targetVersion.Message);
        }

        string? selectionMode = NormalizeSelectionMode(command.SelectionMode, command.ResourceInstanceIds);
        if (selectionMode == null)
        {
            return UpdatePreparationContext.FromMessage(SerializePayload(new
            {
                status = "missing_requirements",
                missing_requirements = new[]
                {
                    new
                    {
                        field = "selection_mode",
                        message = "请提供 resource_instance_ids，或传 selection_mode=all_updatable。"
                    }
                }
            }));
        }

        HashSet<string>? requestedIds = string.Equals(selectionMode, CommunityResourceUpdateRequest.ExplicitSelectionMode, StringComparison.Ordinal)
            ? NormalizeStringSet(command.ResourceInstanceIds)
            : null;

        if (string.Equals(selectionMode, CommunityResourceUpdateRequest.ExplicitSelectionMode, StringComparison.Ordinal) &&
            requestedIds == null)
        {
            return UpdatePreparationContext.FromMessage(SerializePayload(new
            {
                status = "missing_requirements",
                missing_requirements = new[]
                {
                    new
                    {
                        field = "resource_instance_ids",
                        message = "显式更新至少需要一个 resource_instance_id。"
                    }
                }
            }));
        }

        CommunityResourceUpdateCheckResult checkResult = await _communityResourceUpdateCheckService.CheckAsync(
            new CommunityResourceUpdateCheckRequest
            {
                TargetVersionName = targetVersion.TargetVersionName,
                ResolvedGameDirectory = targetVersion.ResolvedGameDirectory,
                ResourceInstanceIds = requestedIds,
            },
            cancellationToken);

        List<CommunityResourceUpdateCheckItem> selectedItems = SelectUpdateItems(checkResult.Items, selectionMode, requestedIds);
        List<CommunityResourceUpdateCheckItem> updateCandidates = selectedItems
            .Where(item => string.Equals(item.Status, "update_available", StringComparison.OrdinalIgnoreCase))
            .ToList();
        UpdateCheckSummary summary = BuildUpdateCheckSummary(selectedItems, requestedIds);

        if (updateCandidates.Count == 0)
        {
            return UpdatePreparationContext.FromMessage(SerializePayload(new
            {
                status = "no_updates",
                target_version_name = targetVersion.TargetVersionName,
                resolved_game_directory = targetVersion.ResolvedGameDirectory,
                selection_mode = selectionMode,
                summary = new
                {
                    update_available = summary.UpdateAvailable,
                    up_to_date = summary.UpToDate,
                    unsupported = summary.Unsupported,
                    not_identified = summary.NotIdentified,
                    failed = summary.Failed,
                    missing = summary.Missing,
                },
                selected_items = selectedItems.Select(SerializeUpdateCheckItem).ToList(),
                message = string.Equals(selectionMode, CommunityResourceUpdateRequest.AllUpdatableSelectionMode, StringComparison.Ordinal)
                    ? "当前实例没有可更新的社区资源。"
                    : "所选资源里没有可直接执行的更新项。"
            }));
        }

        string buttonText = updateCandidates.Count == 1
            ? $"更新 {updateCandidates[0].DisplayName}"
            : $"更新 {updateCandidates.Count} 项资源";

        return new UpdatePreparationContext
        {
            IsReady = true,
            Message = SerializePayload(new
            {
                status = "ready_for_confirmation",
                target_version_name = targetVersion.TargetVersionName,
                version_directory_path = targetVersion.VersionDirectoryPath,
                resolved_game_directory = targetVersion.ResolvedGameDirectory,
                selection_mode = selectionMode,
                summary = new
                {
                    update_available = summary.UpdateAvailable,
                    up_to_date = summary.UpToDate,
                    unsupported = summary.Unsupported,
                    not_identified = summary.NotIdentified,
                    failed = summary.Failed,
                    missing = summary.Missing,
                },
                update_candidates = updateCandidates.Select(SerializeUpdateCandidate).ToList(),
                skipped_items = selectedItems
                    .Where(item => !string.Equals(item.Status, "update_available", StringComparison.OrdinalIgnoreCase))
                    .Select(SerializeUpdateCheckItem)
                    .ToList(),
                message = $"已准备在 {targetVersion.TargetVersionName} 中更新 {updateCandidates.Count} 项社区资源。等待用户确认。"
            }),
            ButtonText = buttonText,
            TargetVersionName = targetVersion.TargetVersionName,
            SelectionMode = selectionMode,
            ResourceInstanceIds = requestedIds,
        };
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

    private static object SerializeInventoryItem(CommunityResourceInventoryItem item)
    {
        return new
        {
            resource_type = item.ResourceType,
            resource_instance_id = item.ResourceInstanceId,
            display_name = item.DisplayName,
            file_path = item.FilePath,
            relative_path = item.RelativePath,
            is_directory = item.IsDirectory,
            source = item.Source,
            project_id = item.ProjectId,
            description = item.Description,
            current_version_hint = item.CurrentVersionHint,
            world_name = item.WorldName,
            pack_format = item.PackFormat,
            update_support = item.UpdateSupport,
            unsupported_reason = item.UpdateUnsupportedReason,
        };
    }

    private static object SerializeUpdateCheckItem(CommunityResourceUpdateCheckItem item)
    {
        return new
        {
            resource_type = item.ResourceType,
            resource_instance_id = item.ResourceInstanceId,
            display_name = item.DisplayName,
            file_path = item.FilePath,
            relative_path = item.RelativePath,
            is_directory = item.IsDirectory,
            world_name = item.WorldName,
            pack_format = item.PackFormat,
            source = item.Source,
            project_id = item.ProjectId,
            status = NormalizeText(item.Status)?.ToLowerInvariant() ?? string.Empty,
            has_update = item.HasUpdate,
            current_version = item.CurrentVersion,
            latest_version = item.LatestVersion,
            provider = item.Provider,
            latest_resource_file_id = item.LatestResourceFileId,
            unsupported_reason = item.UnsupportedReason,
        };
    }

    private static object SerializeUpdateCandidate(CommunityResourceUpdateCheckItem item)
    {
        return new
        {
            resource_type = item.ResourceType,
            resource_instance_id = item.ResourceInstanceId,
            display_name = item.DisplayName,
            current_version = item.CurrentVersion,
            latest_version = item.LatestVersion,
            provider = item.Provider,
            project_id = item.ProjectId,
            latest_resource_file_id = item.LatestResourceFileId,
        };
    }

    private static IReadOnlyCollection<string>? NormalizeInventoryResourceTypes(
        IReadOnlyCollection<string>? resourceTypes,
        out string? invalidResourceType)
    {
        invalidResourceType = null;
        if (resourceTypes == null || resourceTypes.Count == 0)
        {
            return null;
        }

        HashSet<string> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach (string resourceType in resourceTypes)
        {
            string? value = NormalizeText(resourceType)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            value = ModResourcePathHelper.NormalizeProjectType(value);
            if (value is not ("mod" or "shader" or "resourcepack" or "world" or "datapack"))
            {
                invalidResourceType = resourceType;
                return null;
            }

            normalized.Add(value);
        }

        return normalized.Count == 0 ? null : normalized.ToList();
    }

    private static HashSet<string>? NormalizeStringSet(IReadOnlyCollection<string>? values)
    {
        if (values == null || values.Count == 0)
        {
            return null;
        }

        HashSet<string> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach (string value in values)
        {
            string? text = NormalizeText(value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                normalized.Add(text);
            }
        }

        return normalized.Count == 0 ? null : normalized;
    }

    private static string ResolveEffectiveCheckScope(string? checkScope, HashSet<string>? requestedIds)
    {
        if (requestedIds != null)
        {
            return "selected";
        }

        string? normalized = NormalizeText(checkScope)?.ToLowerInvariant();
        return normalized == "all_installed" ? normalized : string.Empty;
    }

    private static string? NormalizeSelectionMode(string? selectionMode, IReadOnlyCollection<string>? resourceInstanceIds)
    {
        HashSet<string>? normalizedIds = NormalizeStringSet(resourceInstanceIds);
        string? normalizedSelectionMode = NormalizeText(selectionMode)?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedSelectionMode))
        {
            return normalizedIds == null
                ? null
                : CommunityResourceUpdateRequest.ExplicitSelectionMode;
        }

        return normalizedSelectionMode switch
        {
            CommunityResourceUpdateRequest.ExplicitSelectionMode => CommunityResourceUpdateRequest.ExplicitSelectionMode,
            CommunityResourceUpdateRequest.AllUpdatableSelectionMode => CommunityResourceUpdateRequest.AllUpdatableSelectionMode,
            _ => null,
        };
    }

    private static List<CommunityResourceUpdateCheckItem> SelectUpdateItems(
        IReadOnlyList<CommunityResourceUpdateCheckItem> items,
        string selectionMode,
        HashSet<string>? requestedIds)
    {
        if (string.Equals(selectionMode, CommunityResourceUpdateRequest.AllUpdatableSelectionMode, StringComparison.Ordinal))
        {
            return items
                .Where(item => string.Equals(item.Status, "update_available", StringComparison.OrdinalIgnoreCase))
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

    private static UpdateCheckSummary BuildUpdateCheckSummary(
        IReadOnlyList<CommunityResourceUpdateCheckItem> items,
        HashSet<string>? requestedIds)
    {
        UpdateCheckSummary summary = new();
        HashSet<string> matchedIds = new(StringComparer.OrdinalIgnoreCase);

        foreach (CommunityResourceUpdateCheckItem item in items)
        {
            matchedIds.Add(item.ResourceInstanceId);

            switch (NormalizeText(item.Status)?.ToLowerInvariant())
            {
                case "update_available":
                    summary.UpdateAvailable++;
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
        }

        if (requestedIds != null)
        {
            summary.Missing = requestedIds.Count(id => !matchedIds.Contains(id));
        }

        return summary;
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

    private static List<string> NormalizeCategoryTokens(IReadOnlyList<string>? categoryTokens)
    {
        var normalizedTokens = new List<string>();
        if (categoryTokens == null)
        {
            return normalizedTokens;
        }

        foreach (var categoryToken in categoryTokens)
        {
            var normalized = NormalizeText(categoryToken);
            if (string.IsNullOrWhiteSpace(normalized)
                || string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!normalizedTokens.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                normalizedTokens.Add(normalized);
            }
        }

        return normalizedTokens;
    }

    private static List<string> GetModrinthCategoryTokens(IReadOnlyList<string> categoryTokens) =>
        categoryTokens
            .Where(token => !int.TryParse(token, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<int> GetCurseForgeCategoryIds(IReadOnlyList<string> categoryTokens) =>
        categoryTokens
            .Select(token => int.TryParse(token, out var categoryId) ? categoryId : (int?)null)
            .Where(categoryId => categoryId.HasValue)
            .Select(categoryId => categoryId!.Value)
            .Distinct()
            .ToList();

    private static string DetectModrinthProjectDetailResourceType(string? requestedResourceType, ModrinthProjectDetail detail)
    {
        var normalizedRequested = NormalizeOptionalQueryableResourceType(requestedResourceType);
        if (!string.IsNullOrWhiteSpace(normalizedRequested))
        {
            return normalizedRequested;
        }

        if ((detail.Loaders?.Any(loader => string.Equals(loader, "datapack", StringComparison.OrdinalIgnoreCase)) ?? false)
            || (detail.Categories?.Any(category => string.Equals(category, "datapack", StringComparison.OrdinalIgnoreCase)) ?? false))
        {
            return "datapack";
        }

        return ModResourcePathHelper.NormalizeProjectType(detail.ProjectType);
    }

    private static List<object> BuildModrinthCategoryDetails(IEnumerable<string>? categories) =>
        (categories ?? [])
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(category => (object)new
            {
                token = category,
                display_name = CategoryLocalizationHelper.GetModrinthCategoryName(category),
                source = "modrinth"
            })
            .ToList();

    private static List<object> BuildCurseForgeCategoryDetails(IEnumerable<CurseForgeCategory>? categories, int primaryCategoryId)
    {
        var categoryList = (categories ?? [])
            .Where(category => category != null)
            .ToList();
        var filteredCategories = categoryList
            .Where(category => !(category.IsClass ?? false))
            .ToList();

        if (filteredCategories.Count == 0)
        {
            filteredCategories = categoryList;
        }

        return filteredCategories
            .Where(category => !string.IsNullOrWhiteSpace(category.Name))
            .GroupBy(category => category.Id)
            .Select(group => group.First())
            .Select(category => (object)new
            {
                token = category.Id.ToString(),
                display_name = CategoryLocalizationHelper.GetLocalizedCategoryName(category.Name),
                raw_name = category.Name,
                category_id = category.Id,
                slug = category.Slug,
                source = "curseforge",
                is_primary = category.Id == primaryCategoryId
            })
            .ToList();
    }

    private static ProjectDetailBody CreateProjectDetailBody(string? rawBody, bool includeBody, int bodyMaxChars)
    {
        var normalizedBody = ModDescriptionMarkdownHelper.Preprocess(rawBody ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedBody))
        {
            return new ProjectDetailBody();
        }

        var preview = TruncateText(normalizedBody, 600, out var previewTruncated);
        string? content = null;
        var contentTruncated = false;
        if (includeBody)
        {
            content = TruncateText(normalizedBody, bodyMaxChars, out contentTruncated);
        }

        return new ProjectDetailBody
        {
            HasBody = true,
            Preview = preview,
            PreviewTruncated = previewTruncated,
            Content = content,
            ContentTruncated = contentTruncated,
            OriginalLength = normalizedBody.Length
        };
    }

    private static string TruncateText(string value, int maxChars, out bool truncated)
    {
        if (value.Length <= maxChars)
        {
            truncated = false;
            return value;
        }

        truncated = true;
        return value[..Math.Max(0, maxChars)].TrimEnd() + "...";
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

    private sealed class ProjectDetailBody
    {
        public bool HasBody { get; init; }

        public string Preview { get; init; } = string.Empty;

        public bool PreviewTruncated { get; init; }

        public string? Content { get; init; }

        public bool ContentTruncated { get; init; }

        public int OriginalLength { get; init; }
    }

    private sealed record ParsedProjectIdentity(CommunityResourceProvider Provider, string RawProjectId);

    private sealed class ResolvedTargetVersionContext
    {
        public bool IsReady { get; init; }

        public string Message { get; init; } = string.Empty;

        public string TargetVersionName { get; init; } = string.Empty;

        public string VersionDirectoryPath { get; init; } = string.Empty;

        public string ResolvedGameDirectory { get; init; } = string.Empty;

        public static ResolvedTargetVersionContext FromMessage(string message)
        {
            return new ResolvedTargetVersionContext
            {
                Message = message
            };
        }
    }

    private sealed class UpdatePreparationContext
    {
        public bool IsReady { get; init; }

        public string Message { get; init; } = string.Empty;

        public string ButtonText { get; init; } = string.Empty;

        public string TargetVersionName { get; init; } = string.Empty;

        public string SelectionMode { get; init; } = string.Empty;

        public HashSet<string>? ResourceInstanceIds { get; init; }

        public static UpdatePreparationContext FromMessage(string message)
        {
            return new UpdatePreparationContext
            {
                Message = message
            };
        }
    }

    private sealed class UpdateCheckSummary
    {
        public int UpdateAvailable { get; set; }

        public int UpToDate { get; set; }

        public int Unsupported { get; set; }

        public int NotIdentified { get; set; }

        public int Failed { get; set; }

        public int Missing { get; set; }
    }

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