using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed class SearchCommunityResourcesToolHandler : IAgentToolHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public SearchCommunityResourcesToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => "searchCommunityResources";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "跨 Modrinth / CurseForge 搜索社区资源。V1 支持 mod、resourcepack、shader、datapack、world、modpack 的只读搜索，其中 world 当前仅通过 CurseForge 查询。",
        new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "可选。搜索关键词，例如 Iris；省略时返回当前更热门的资源，适合先了解热度最高的内容。" },
                resource_type = new { type = "string", description = "资源类型", @enum = new[] { "mod", "resourcepack", "shader", "datapack", "world", "modpack" } },
                platforms = new
                {
                    type = "array",
                    description = "可选。搜索平台数组；省略时默认同时搜索 Modrinth 和 CurseForge。world 当前会自动收敛到 CurseForge。",
                    items = new { type = "string", @enum = new[] { "modrinth", "curseforge" } }
                },
                category_tokens = new
                {
                    type = "array",
                    description = "可选。按类别 token 过滤；建议先调用 getCommunityResourceTags 获取可用值，再把 categories[*].token 直接传进来。",
                    items = new { type = "string" }
                },
                game_version = new { type = "string", description = "可选。按 Minecraft 版本过滤，例如 1.21.1" },
                loader = new { type = "string", description = "可选。按加载器过滤，例如 fabric、forge、neoforge、quilt" },
                limit = new { type = "integer", description = "可选。每个平台最多返回多少条，默认 5，最大 10。" }
            },
            required = new[] { "resource_type" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var query = arguments["query"]?.ToString() ?? string.Empty;
        var resourceType = arguments["resource_type"]?.ToString()
            ?? arguments["resourceType"]?.ToString()
            ?? string.Empty;
        var gameVersion = arguments["game_version"]?.ToString() ?? arguments["gameVersion"]?.ToString();
        var loader = arguments["loader"]?.ToString();
        var limit = arguments["limit"]?.Value<int>() ?? 5;
        var platforms = CommunityResourceToolArgumentParser.ParseStringArray(arguments["platforms"]);
        var categoryTokensToken = arguments["category_tokens"] ?? arguments["categoryTokens"] ?? arguments["categories"];
        var categoryTokens = CommunityResourceToolArgumentParser.ParseStringArray(categoryTokensToken);
        if (platforms == null && arguments["platforms"] != null)
        {
            return AgentToolExecutionResult.FromMessage("platforms 必须是字符串数组。");
        }

        if (categoryTokens == null && categoryTokensToken != null)
        {
            return AgentToolExecutionResult.FromMessage("category_tokens 必须是字符串数组。");
        }

        var message = await _communityResourceService.SearchAsync(
            query,
            resourceType,
            platforms,
            categoryTokens,
            gameVersion,
            loader,
            limit,
            cancellationToken);
        return AgentToolExecutionResult.FromMessage(message);
    }
}

public sealed class GetCommunityResourceTagsToolHandler : IAgentToolHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public GetCommunityResourceTagsToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => "getCommunityResourceTags";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "返回社区资源搜索可用的类别 token 和加载器值。categories[*].token 可直接传给 searchCommunityResources.category_tokens。V1 支持 mod、resourcepack、shader、datapack、world、modpack，其中 world 当前仅返回 CurseForge 标签。",
        new
        {
            type = "object",
            properties = new
            {
                resource_type = new { type = "string", description = "资源类型", @enum = new[] { "mod", "resourcepack", "shader", "datapack", "world", "modpack" } },
                platforms = new
                {
                    type = "array",
                    description = "可选。查询平台数组；省略时默认同时返回 Modrinth 和 CurseForge 的标签。world 当前会自动收敛到 CurseForge。",
                    items = new { type = "string", @enum = new[] { "modrinth", "curseforge" } }
                }
            },
            required = new[] { "resource_type" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var resourceType = arguments["resource_type"]?.ToString()
            ?? arguments["resourceType"]?.ToString()
            ?? string.Empty;
        var platforms = CommunityResourceToolArgumentParser.ParseStringArray(arguments["platforms"]);
        if (platforms == null && arguments["platforms"] != null)
        {
            return AgentToolExecutionResult.FromMessage("platforms 必须是字符串数组。");
        }

        var message = await _communityResourceService.GetCommunityResourceTagsAsync(
            resourceType,
            platforms,
            cancellationToken);
        return AgentToolExecutionResult.FromMessage(message);
    }
}

public sealed class GetCommunityResourceProjectDetailToolHandler : IAgentToolHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public GetCommunityResourceProjectDetailToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => "getCommunityResourceProjectDetail";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "获取某个社区资源项目的细粒度详情，包括更具体的类别、兼容加载器、支持版本、平台链接和可选正文内容。project_id 必填；若要读取正文，请把 include_body 设为 true。",
        new
        {
            type = "object",
            properties = new
            {
                project_id = new { type = "string", description = "资源项目 ID。CurseForge 必须是 curseforge-<id>；Modrinth 直接使用 project_id。" },
                resource_type = new { type = "string", description = "可选。辅助识别资源类型，尤其是 Modrinth datapack、world 或 modpack。", @enum = new[] { "mod", "resourcepack", "shader", "datapack", "world", "modpack" } },
                include_body = new { type = "boolean", description = "可选。是否返回项目正文内容；默认 false，只返回预览。" },
                body_max_chars = new { type = "integer", description = "可选。正文最多返回多少字符；默认 4000，最大 12000。仅在 include_body=true 时生效。" }
            },
            required = new[] { "project_id" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var projectId = arguments["project_id"]?.ToString() ?? arguments["projectId"]?.ToString() ?? string.Empty;
        var resourceType = arguments["resource_type"]?.ToString() ?? arguments["resourceType"]?.ToString();
        var includeBody = arguments["include_body"]?.Value<bool>() ?? arguments["includeBody"]?.Value<bool>() ?? false;
        var bodyMaxChars = arguments["body_max_chars"]?.Value<int>() ?? arguments["bodyMaxChars"]?.Value<int>() ?? 4000;

        var message = await _communityResourceService.GetProjectDetailAsync(
            projectId,
            resourceType,
            includeBody,
            bodyMaxChars,
            cancellationToken);
        return AgentToolExecutionResult.FromMessage(message);
    }
}

public sealed class GetCommunityResourceFilesToolHandler : IAgentToolHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public GetCommunityResourceFilesToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => "getCommunityResourceFiles";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "获取某个社区资源的候选文件列表。project_id 必填；可选传 game_version / loader 过滤，避免一次返回过多文件撑爆上下文。",
        new
        {
            type = "object",
            properties = new
            {
                project_id = new { type = "string", description = "资源项目 ID。CurseForge 必须是 curseforge-<id>；Modrinth 直接使用 project_id。" },
                resource_type = new { type = "string", description = "可选。辅助识别资源类型，尤其是 Modrinth datapack、world 或 modpack。", @enum = new[] { "mod", "resourcepack", "shader", "datapack", "world", "modpack" } },
                game_version = new { type = "string", description = "可选。按 Minecraft 版本过滤。" },
                loader = new { type = "string", description = "可选。按加载器过滤。" },
                limit = new { type = "integer", description = "可选。最多返回多少个文件，默认 20，最大 50。" }
            },
            required = new[] { "project_id" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var projectId = arguments["project_id"]?.ToString() ?? arguments["projectId"]?.ToString() ?? string.Empty;
        var resourceType = arguments["resource_type"]?.ToString() ?? arguments["resourceType"]?.ToString();
        var gameVersion = arguments["game_version"]?.ToString() ?? arguments["gameVersion"]?.ToString();
        var loader = arguments["loader"]?.ToString();
        var limit = arguments["limit"]?.Value<int>() ?? 20;

        var message = await _communityResourceService.GetProjectFilesAsync(
            projectId,
            resourceType,
            gameVersion,
            loader,
            limit,
            cancellationToken);
        return AgentToolExecutionResult.FromMessage(message);
    }
}

public sealed class GetInstancesToolHandler : IAgentToolHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public GetInstancesToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => "getInstances";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "返回当前启动器已安装的可用实例列表。结果会包含 target_version_name、version_directory_path、resolved_game_directory，以及 is_modpack / modpack_platform / modpack_project_id / modpack_version_id；安装或更新整合包前应优先先读这里。",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var message = await _communityResourceService.GetInstallableInstancesAsync(context, cancellationToken);
        return AgentToolExecutionResult.FromMessage(message);
    }
}

public sealed class CheckModpackUpdateToolHandler : IAgentToolHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public CheckModpackUpdateToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => "checkModpackUpdate";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "检查指定实例是否存在整合包更新。仅适用于已安装的整合包实例；结果会返回 latest_version、default_update_candidate，以及可供 updateModpack 使用的 candidates[*].source_version_id。",
        new
        {
            type = "object",
            properties = new
            {
                target_version_name = new { type = "string", description = "目标实例名，优先使用 getInstances 返回的 target_version_name。" },
                target_version_path = new { type = "string", description = "可选。getInstances 返回的 version_directory_path；若提供，启动器会自动推导目标实例。" },
                include_candidates = new { type = "boolean", description = "可选。是否返回候选版本列表；默认 true。" },
                candidate_limit = new { type = "integer", description = "可选。候选版本最多返回多少项；默认 10，最大 20。" }
            },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        string message = await _communityResourceService.CheckModpackUpdateAsync(
            new AgentModpackUpdateCheckCommand
            {
                TargetVersionName = arguments["target_version_name"]?.ToString() ?? arguments["targetVersionName"]?.ToString(),
                TargetVersionPath = arguments["target_version_path"]?.ToString() ?? arguments["targetVersionPath"]?.ToString(),
                IncludeCandidates = arguments["include_candidates"]?.Value<bool>() ?? arguments["includeCandidates"]?.Value<bool>() ?? true,
                CandidateLimit = arguments["candidate_limit"]?.Value<int>() ?? arguments["candidateLimit"]?.Value<int>() ?? 10,
            },
            cancellationToken);

        return AgentToolExecutionResult.FromMessage(message);
    }
}

public sealed class UpdateModpackToolHandler : IAgentToolHandler
{
    public const string ToolNameValue = "updateModpack";

    private readonly IAgentCommunityResourceService _communityResourceService;

    public UpdateModpackToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => ToolNameValue;

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "确认式整合包更新工具。仅适用于已安装的整合包实例。默认更新到最新可用版本；若要指定目标版本，只能传 checkModpackUpdate 返回的 source_version_id。",
        new
        {
            type = "object",
            properties = new
            {
                target_version_name = new { type = "string", description = "目标实例名，优先使用 getInstances 返回的 target_version_name。" },
                target_version_path = new { type = "string", description = "可选。getInstances 返回的 version_directory_path；若提供，启动器会自动推导目标实例。" },
                source_version_id = new { type = "string", description = "可选。目标整合包版本的稳定 ID；只能使用 checkModpackUpdate 返回的 candidates[*].source_version_id。省略时默认更新到最新可用版本。" }
            },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        AgentModpackUpdatePreparation preparation = await _communityResourceService.PrepareModpackUpdateAsync(
            new AgentModpackUpdateCommand
            {
                TargetVersionName = arguments["target_version_name"]?.ToString() ?? arguments["targetVersionName"]?.ToString(),
                TargetVersionPath = arguments["target_version_path"]?.ToString() ?? arguments["targetVersionPath"]?.ToString(),
                SourceVersionId = arguments["source_version_id"]?.ToString() ?? arguments["sourceVersionId"]?.ToString(),
            },
            cancellationToken);

        if (!preparation.IsReadyForConfirmation)
        {
            return AgentToolExecutionResult.FromMessage(preparation.Message);
        }

        AgentActionProposal proposal = new()
        {
            ActionType = ToolName,
            ButtonText = preparation.ButtonText,
            DisplayMessage = preparation.Message,
            PermissionLevel = AgentToolPermissionLevel.ConfirmationRequired,
            Parameters = new Dictionary<string, string>(preparation.ProposalParameters, StringComparer.OrdinalIgnoreCase)
        };

        return AgentToolExecutionResult.FromActionProposal(preparation.Message, proposal);
    }
}

public sealed class InstallCommunityResourceToolHandler : IAgentToolHandler
{
    public const string ToolNameValue = "installCommunityResource";

    private readonly IAgentCommunityResourceService _communityResourceService;

    public InstallCommunityResourceToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => ToolNameValue;

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "安装社区资源到指定实例。V1 支持 mod、resourcepack、shader、world、datapack；其中 datapack 必须额外提供 target_world_resource_id。整合包请改用 installModpack。调用前建议先用 getCommunityResourceFiles 选定 resource_file_id，并用 getInstances 获取 target_version_name 或 version_directory_path；安装 datapack 前再用 getInstanceCommunityResources(resource_types=[\"world\"]) 选择目标世界，并直接复用 world 项的 resource_instance_id，不要自行拼 world_path 或仅凭 world_name 推断。",
        new
        {
            type = "object",
            properties = new
            {
                project_id = new { type = "string", description = "资源项目 ID。CurseForge 必须是 curseforge-<id>；Modrinth 直接使用 project_id。" },
                resource_file_id = new { type = "string", description = "要安装的文件 ID。Modrinth 这里传版本 ID；CurseForge 这里传文件 ID。" },
                target_version_name = new { type = "string", description = "目标实例名，优先从 getInstances 的 target_version_name 中选择。" },
                target_version_path = new { type = "string", description = "可选。若传实例目录路径，启动器会自动推导出 target_version_name。" },
                resource_type = new { type = "string", description = "可选。辅助识别资源类型。", @enum = new[] { "mod", "resourcepack", "shader", "datapack", "world" } },
                target_world_resource_id = new { type = "string", description = "可选。安装 datapack 时必填；值必须来自 getInstanceCommunityResources(resource_types=[\"world\"]) 返回的 world 项 resource_instance_id，不要改传 world_name 或 world_path。" },
                download_dependencies = new { type = "boolean", description = "可选。是否自动下载前置依赖；默认 false。" }
            },
            required = new[] { "project_id", "resource_file_id" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var command = new AgentCommunityResourceInstallCommand
        {
            ProjectId = arguments["project_id"]?.ToString() ?? arguments["projectId"]?.ToString() ?? string.Empty,
            ResourceFileId = arguments["resource_file_id"]?.ToString() ?? arguments["resourceFileId"]?.ToString() ?? string.Empty,
            TargetVersionName = arguments["target_version_name"]?.ToString() ?? arguments["targetVersionName"]?.ToString(),
            TargetVersionPath = arguments["target_version_path"]?.ToString() ?? arguments["targetVersionPath"]?.ToString(),
            ResourceType = arguments["resource_type"]?.ToString() ?? arguments["resourceType"]?.ToString(),
            TargetWorldResourceId = arguments["target_world_resource_id"]?.ToString() ?? arguments["targetWorldResourceId"]?.ToString(),
            DownloadDependencies = arguments["download_dependencies"]?.Value<bool>() ?? arguments["downloadDependencies"]?.Value<bool>() ?? false,
        };

        var preparation = await _communityResourceService.PrepareInstallAsync(command, cancellationToken);
        if (!preparation.IsReadyForConfirmation)
        {
            return AgentToolExecutionResult.FromMessage(preparation.Message);
        }

        var proposal = new AgentActionProposal
        {
            ActionType = ToolName,
            ButtonText = preparation.ButtonText,
            DisplayMessage = preparation.Message,
            PermissionLevel = AgentToolPermissionLevel.ConfirmationRequired,
            Parameters = new Dictionary<string, string>(preparation.ProposalParameters, StringComparer.OrdinalIgnoreCase)
        };

        return AgentToolExecutionResult.FromActionProposal(preparation.Message, proposal);
    }
}

public sealed class InstallModpackToolHandler : IAgentToolHandler
{
    public const string ToolNameValue = "installModpack";

    private readonly IAgentCommunityResourceService _communityResourceService;

    public InstallModpackToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => ToolNameValue;

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "安装整合包并创建新的实例。调用前建议先用 getCommunityResourceFiles 选定 resource_file_id。target_version_name 必须是新的实例名，允许空格，不会自动追加 loader 或 Minecraft 版本后缀。",
        new
        {
            type = "object",
            properties = new
            {
                project_id = new { type = "string", description = "整合包项目 ID。CurseForge 必须是 curseforge-<id>；Modrinth 直接使用 project_id。" },
                resource_file_id = new { type = "string", description = "要安装的整合包文件 ID。Modrinth 这里传版本 ID；CurseForge 这里传文件 ID。" },
                target_version_name = new { type = "string", description = "新的实例名。会直接用于 versions/<name> 目录，必须唯一。" }
            },
            required = new[] { "project_id", "resource_file_id", "target_version_name" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var command = new AgentCommunityResourceInstallCommand
        {
            ProjectId = arguments["project_id"]?.ToString() ?? arguments["projectId"]?.ToString() ?? string.Empty,
            ResourceFileId = arguments["resource_file_id"]?.ToString() ?? arguments["resourceFileId"]?.ToString() ?? string.Empty,
            TargetVersionName = arguments["target_version_name"]?.ToString() ?? arguments["targetVersionName"]?.ToString(),
        };

        var preparation = await _communityResourceService.PrepareModpackInstallAsync(command, cancellationToken);
        if (!preparation.IsReadyForConfirmation)
        {
            return AgentToolExecutionResult.FromMessage(preparation.Message);
        }

        var proposal = new AgentActionProposal
        {
            ActionType = ToolName,
            ButtonText = preparation.ButtonText,
            DisplayMessage = preparation.Message,
            PermissionLevel = AgentToolPermissionLevel.ConfirmationRequired,
            Parameters = new Dictionary<string, string>(preparation.ProposalParameters, StringComparer.OrdinalIgnoreCase)
        };

        return AgentToolExecutionResult.FromActionProposal(preparation.Message, proposal);
    }
}

public sealed class GetInstanceCommunityResourcesToolHandler : IAgentToolHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public GetInstanceCommunityResourcesToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => "getInstanceCommunityResources";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "返回指定实例内已安装的社区资源清单。支持 mod、shader、resourcepack、world、datapack 五类资源，并保留稳定的 resource_instance_id。安装 datapack 时，应该先读取这里的 world 项，再把该 world 的 resource_instance_id 传给 installCommunityResource.target_world_resource_id；world_name 仅用于展示。调用前建议先用 getInstances 获取 target_version_name。",
        new
        {
            type = "object",
            properties = new
            {
                target_version_name = new { type = "string", description = "目标实例名，优先使用 getInstances 返回的 target_version_name。" },
                target_version_path = new { type = "string", description = "可选。getInstances 返回的 version_directory_path；若提供，启动器会自动推导目标实例。" },
                resource_types = new
                {
                    type = "array",
                    description = "可选。按资源类型过滤；省略时返回实例内全部五类资源。若要安装 datapack，建议显式传 [\"world\"] 只读取世界列表。",
                    items = new { type = "string", @enum = new[] { "mod", "shader", "resourcepack", "world", "datapack" } }
                }
            },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        List<string>? resourceTypes = CommunityResourceToolArgumentParser.ParseStringArray(arguments["resource_types"]);
        if (resourceTypes == null && arguments["resource_types"] != null)
        {
            return AgentToolExecutionResult.FromMessage("resource_types 必须是字符串数组。\n可选值：mod、shader、resourcepack、world、datapack。");
        }

        string message = await _communityResourceService.GetInstanceCommunityResourcesAsync(
            new AgentInstanceCommunityResourceInventoryCommand
            {
                TargetVersionName = arguments["target_version_name"]?.ToString() ?? arguments["targetVersionName"]?.ToString(),
                TargetVersionPath = arguments["target_version_path"]?.ToString() ?? arguments["targetVersionPath"]?.ToString(),
                ResourceTypes = resourceTypes,
            },
            cancellationToken);

        return AgentToolExecutionResult.FromMessage(message);
    }
}

public sealed class CheckInstanceCommunityResourceUpdatesToolHandler : IAgentToolHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public CheckInstanceCommunityResourceUpdatesToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => "checkInstanceCommunityResourceUpdates";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "批量检查指定实例内社区资源的更新状态。首版仅对 mod、shader、resourcepack 执行更新检查；world/datapack 会返回 unsupported_reason。可传 resource_instance_ids 缩小范围，或显式传 check_scope=all_installed 做全量检查。",
        new
        {
            type = "object",
            properties = new
            {
                target_version_name = new { type = "string", description = "目标实例名，优先使用 getInstances 返回的 target_version_name。" },
                target_version_path = new { type = "string", description = "可选。getInstances 返回的 version_directory_path；若提供，启动器会自动推导目标实例。" },
                resource_instance_ids = new
                {
                    type = "array",
                    description = "可选。仅检查这些已安装资源；值必须来自 getInstanceCommunityResources 返回的 resource_instance_id。",
                    items = new { type = "string" }
                },
                check_scope = new { type = "string", description = "可选。传 all_installed 表示检查实例内全部已安装资源。", @enum = new[] { "all_installed" } }
            },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        List<string>? resourceInstanceIds = CommunityResourceToolArgumentParser.ParseStringArray(arguments["resource_instance_ids"]);
        if (resourceInstanceIds == null && arguments["resource_instance_ids"] != null)
        {
            return AgentToolExecutionResult.FromMessage("resource_instance_ids 必须是字符串数组。每一项都应来自 getInstanceCommunityResources 的 resource_instance_id。");
        }

        string message = await _communityResourceService.CheckInstanceCommunityResourceUpdatesAsync(
            new AgentInstanceCommunityResourceUpdateCheckCommand
            {
                TargetVersionName = arguments["target_version_name"]?.ToString() ?? arguments["targetVersionName"]?.ToString(),
                TargetVersionPath = arguments["target_version_path"]?.ToString() ?? arguments["targetVersionPath"]?.ToString(),
                ResourceInstanceIds = resourceInstanceIds,
                CheckScope = arguments["check_scope"]?.ToString() ?? arguments["checkScope"]?.ToString(),
            },
            cancellationToken);

        return AgentToolExecutionResult.FromMessage(message);
    }
}

public sealed class UpdateInstanceCommunityResourcesToolHandler : IAgentToolHandler
{
    public const string ToolNameValue = "updateInstanceCommunityResources";

    private readonly IAgentCommunityResourceService _communityResourceService;

    public UpdateInstanceCommunityResourcesToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => ToolNameValue;

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "确认式批量更新工具。支持对实例内社区资源执行显式批量更新，或传 selection_mode=all_updatable 让启动器更新全部可更新项。调用前建议先用 getInstanceCommunityResources / checkInstanceCommunityResourceUpdates 确定 resource_instance_id。",
        new
        {
            type = "object",
            properties = new
            {
                target_version_name = new { type = "string", description = "目标实例名，优先使用 getInstances 返回的 target_version_name。" },
                target_version_path = new { type = "string", description = "可选。getInstances 返回的 version_directory_path；若提供，启动器会自动推导目标实例。" },
                resource_instance_ids = new
                {
                    type = "array",
                    description = "可选。显式指定要更新的已安装资源；值必须来自 getInstanceCommunityResources 的 resource_instance_id。",
                    items = new { type = "string" }
                },
                selection_mode = new { type = "string", description = "可选。传 all_updatable 表示更新全部可更新项。", @enum = new[] { "all_updatable" } }
            },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        List<string>? resourceInstanceIds = CommunityResourceToolArgumentParser.ParseStringArray(arguments["resource_instance_ids"]);
        if (resourceInstanceIds == null && arguments["resource_instance_ids"] != null)
        {
            return AgentToolExecutionResult.FromMessage("resource_instance_ids 必须是字符串数组。每一项都应来自 getInstanceCommunityResources 的 resource_instance_id。");
        }

        AgentCommunityResourceUpdatePreparation preparation = await _communityResourceService.PrepareUpdateAsync(
            new AgentInstanceCommunityResourceUpdateCommand
            {
                TargetVersionName = arguments["target_version_name"]?.ToString() ?? arguments["targetVersionName"]?.ToString(),
                TargetVersionPath = arguments["target_version_path"]?.ToString() ?? arguments["targetVersionPath"]?.ToString(),
                ResourceInstanceIds = resourceInstanceIds,
                SelectionMode = arguments["selection_mode"]?.ToString() ?? arguments["selectionMode"]?.ToString(),
            },
            cancellationToken);

        if (!preparation.IsReadyForConfirmation)
        {
            return AgentToolExecutionResult.FromMessage(preparation.Message);
        }

        AgentActionProposal proposal = new()
        {
            ActionType = ToolName,
            ButtonText = preparation.ButtonText,
            DisplayMessage = preparation.Message,
            PermissionLevel = AgentToolPermissionLevel.ConfirmationRequired,
            Parameters = new Dictionary<string, string>(preparation.ProposalParameters, StringComparer.OrdinalIgnoreCase)
        };

        return AgentToolExecutionResult.FromActionProposal(preparation.Message, proposal);
    }
}

public sealed class InstallCommunityResourceActionHandler : IAgentActionHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public InstallCommunityResourceActionHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ActionType => InstallCommunityResourceToolHandler.ToolNameValue;

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        var command = new AgentCommunityResourceInstallCommand
        {
            ProjectId = proposal.Parameters.TryGetValue("project_id", out var projectId) ? projectId : string.Empty,
            ResourceFileId = proposal.Parameters.TryGetValue("resource_file_id", out var resourceFileId) ? resourceFileId : string.Empty,
            TargetVersionName = proposal.Parameters.TryGetValue("target_version_name", out var targetVersionName) ? targetVersionName : null,
            TargetVersionPath = proposal.Parameters.TryGetValue("target_version_path", out var targetVersionPath) ? targetVersionPath : null,
            ResourceType = proposal.Parameters.TryGetValue("resource_type", out var resourceType) ? resourceType : null,
            TargetWorldResourceId = proposal.Parameters.TryGetValue("target_world_resource_id", out var targetWorldResourceId) ? targetWorldResourceId : null,
            DownloadDependencies = proposal.Parameters.TryGetValue("download_dependencies", out var downloadDependencies)
                && bool.TryParse(downloadDependencies, out var parsedDownloadDependencies)
                && parsedDownloadDependencies,
        };

        return _communityResourceService.StartInstallAsync(command, cancellationToken);
    }
}

public sealed class InstallModpackActionHandler : IAgentActionHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public InstallModpackActionHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ActionType => InstallModpackToolHandler.ToolNameValue;

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        var command = new AgentCommunityResourceInstallCommand
        {
            ProjectId = proposal.Parameters.TryGetValue("project_id", out var projectId) ? projectId : string.Empty,
            ResourceFileId = proposal.Parameters.TryGetValue("resource_file_id", out var resourceFileId) ? resourceFileId : string.Empty,
            TargetVersionName = proposal.Parameters.TryGetValue("target_version_name", out var targetVersionName) ? targetVersionName : null,
        };

        return _communityResourceService.StartModpackInstallAsync(command, cancellationToken);
    }
}

public sealed class UpdateModpackActionHandler : IAgentActionHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public UpdateModpackActionHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ActionType => UpdateModpackToolHandler.ToolNameValue;

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        AgentModpackUpdateCommand command = new()
        {
            TargetVersionName = proposal.Parameters.TryGetValue("target_version_name", out string? targetVersionName) ? targetVersionName : null,
            SourceVersionId = proposal.Parameters.TryGetValue("source_version_id", out string? sourceVersionId) ? sourceVersionId : null,
        };

        return _communityResourceService.StartModpackUpdateAsync(command, cancellationToken);
    }
}

public sealed class UpdateInstanceCommunityResourcesActionHandler : IAgentActionHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public UpdateInstanceCommunityResourcesActionHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ActionType => UpdateInstanceCommunityResourcesToolHandler.ToolNameValue;

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<string>? resourceInstanceIds = CommunityResourceToolArgumentParser.ParseSerializedStringArray(
            proposal.Parameters.TryGetValue("resource_instance_ids", out string? serializedIds) ? serializedIds : null);

        AgentInstanceCommunityResourceUpdateCommand command = new()
        {
            TargetVersionName = proposal.Parameters.TryGetValue("target_version_name", out string? targetVersionName) ? targetVersionName : null,
            ResourceInstanceIds = resourceInstanceIds,
            SelectionMode = proposal.Parameters.TryGetValue("selection_mode", out string? selectionMode) ? selectionMode : null,
        };

        return _communityResourceService.StartUpdateAsync(command, cancellationToken);
    }
}

internal static class CommunityResourceToolArgumentParser
{
    public static List<string>? ParseStringArray(JToken? token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token is not JArray array)
        {
            return null;
        }

        return array
            .Select(item => item.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    public static IReadOnlyCollection<string>? ParseSerializedStringArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            List<string>? values = JsonConvert.DeserializeObject<List<string>>(raw);
            return values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return null;
        }
    }
}