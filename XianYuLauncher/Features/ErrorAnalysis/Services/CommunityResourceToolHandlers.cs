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

    public string ToolName => "search_community_resources";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "跨 Modrinth / CurseForge 搜索社区资源。V1 支持 mod、resourcepack、shader、datapack 的只读搜索。",
        new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "搜索关键词，例如 Iris" },
                resource_type = new { type = "string", description = "资源类型", @enum = new[] { "mod", "resourcepack", "shader", "datapack" } },
                platforms = new
                {
                    type = "array",
                    description = "可选。搜索平台数组；省略时默认同时搜索 Modrinth 和 CurseForge。",
                    items = new { type = "string", @enum = new[] { "modrinth", "curseforge" } }
                },
                game_version = new { type = "string", description = "可选。按 Minecraft 版本过滤，例如 1.21.1" },
                loader = new { type = "string", description = "可选。按加载器过滤，例如 fabric、forge、neoforge、quilt" },
                limit = new { type = "integer", description = "可选。每个平台最多返回多少条，默认 5，最大 10。" }
            },
            required = new[] { "query", "resource_type" }
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
        var platforms = ParseStringArray(arguments["platforms"]);
        if (platforms == null && arguments["platforms"] != null)
        {
            return AgentToolExecutionResult.FromMessage("platforms 必须是字符串数组。");
        }

        var message = await _communityResourceService.SearchAsync(
            query,
            resourceType,
            platforms,
            gameVersion,
            loader,
            limit,
            cancellationToken);
        return AgentToolExecutionResult.FromMessage(message);
    }

    private static List<string>? ParseStringArray(JToken? token)
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
}

public sealed class GetCommunityResourceFilesToolHandler : IAgentToolHandler
{
    private readonly IAgentCommunityResourceService _communityResourceService;

    public GetCommunityResourceFilesToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => "get_community_resource_files";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "获取某个社区资源的候选文件列表。project_id 必填；可选传 game_version / loader 过滤，避免一次返回过多文件撑爆上下文。",
        new
        {
            type = "object",
            properties = new
            {
                project_id = new { type = "string", description = "资源项目 ID。CurseForge 必须是 curseforge-<id>；Modrinth 直接使用 project_id。" },
                resource_type = new { type = "string", description = "可选。辅助识别资源类型，尤其是 Modrinth datapack。", @enum = new[] { "mod", "resourcepack", "shader", "datapack" } },
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

    public string ToolName => "get_instances";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "返回当前启动器已安装的可用实例列表。结果会包含 target_version_name、version_directory_path 和 resolved_game_directory；安装社区资源时优先把 target_version_name 传给 install_community_resource。",
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

public sealed class InstallCommunityResourceToolHandler : IAgentToolHandler
{
    public const string ToolNameValue = "install_community_resource";

    private readonly IAgentCommunityResourceService _communityResourceService;

    public InstallCommunityResourceToolHandler(IAgentCommunityResourceService communityResourceService)
    {
        _communityResourceService = communityResourceService;
    }

    public string ToolName => ToolNameValue;

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "安装社区资源到指定实例。V1 仅支持 mod、resourcepack、shader；datapack / world / modpack 仍超出当前 AI 安装范围。调用前建议先用 get_community_resource_files 选定 resource_file_id，并用 get_instances 获取 target_version_name。",
        new
        {
            type = "object",
            properties = new
            {
                project_id = new { type = "string", description = "资源项目 ID。CurseForge 必须是 curseforge-<id>；Modrinth 直接使用 project_id。" },
                resource_file_id = new { type = "string", description = "要安装的文件 ID。Modrinth 这里传版本 ID；CurseForge 这里传文件 ID。" },
                target_version_name = new { type = "string", description = "目标实例名，优先从 get_instances 的 target_version_name 中选择。" },
                target_version_path = new { type = "string", description = "可选。若传实例目录路径，启动器会自动推导出 target_version_name。" },
                resource_type = new { type = "string", description = "可选。辅助识别资源类型。", @enum = new[] { "mod", "resourcepack", "shader", "datapack" } },
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
            ResourceType = proposal.Parameters.TryGetValue("resource_type", out var resourceType) ? resourceType : null,
            DownloadDependencies = proposal.Parameters.TryGetValue("download_dependencies", out var downloadDependencies)
                && bool.TryParse(downloadDependencies, out var parsedDownloadDependencies)
                && parsedDownloadDependencies,
        };

        return _communityResourceService.StartInstallAsync(command, cancellationToken);
    }
}