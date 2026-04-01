using System.Text.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed class InstallGameToolHandler : IAgentToolHandler
{
    public const string ToolNameValue = "installGame";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAgentGameInstallService _gameInstallService;

    public InstallGameToolHandler(IAgentGameInstallService gameInstallService)
    {
        _gameInstallService = gameInstallService;
    }

    public string ToolName => ToolNameValue;

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "安装原版或带加载器组合的游戏版本。loaders 留空表示原版，version 省略时自动选择当前兼容列表中的第一项。",
        new
        {
            type = "object",
            properties = new
            {
                minecraft_version = new { type = "string", description = "Minecraft 版本号，例如 1.20.1" },
                version_name = new { type = "string", description = "目标版本名称，可选；留空时按现有安装页规则自动生成" },
                loaders = new
                {
                    type = "array",
                    description = "加载器数组。留空或省略表示安装原版。V1 支持 1 个基础加载器，再附加 OptiFine 和/或 LiteLoader。",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new
                            {
                                type = "string",
                                description = "加载器类型",
                                @enum = new[] { "forge", "fabric", "neoforge", "quilt", "cleanroom", "legacyfabric", "optifine", "liteloader" }
                            },
                            version = new { type = "string", description = "加载器版本，可选；省略时自动选择当前兼容列表中的第一项" }
                        },
                        required = new[] { "type" }
                    }
                }
            },
            required = new[] { "minecraft_version" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var minecraftVersion = arguments["minecraft_version"]?.ToString()
            ?? arguments["minecraftVersion"]?.ToString()
            ?? string.Empty;
        var requestedVersionName = arguments["version_name"]?.ToString()
            ?? arguments["versionName"]?.ToString();

        List<InstallGameLoaderRequest> loaders;
        try
        {
            loaders = ParseLoaders(arguments["loaders"]);
        }
        catch (Exception ex)
        {
            return AgentToolExecutionResult.FromMessage($"installGame 参数无效: {ex.Message}");
        }

        try
        {
            var preparedPlan = await _gameInstallService.PrepareInstallAsync(
                minecraftVersion,
                loaders,
                requestedVersionName,
                cancellationToken);

            var proposal = new AgentActionProposal
            {
                ActionType = ToolName,
                ButtonText = $"安装 {preparedPlan.VersionName}",
                PermissionLevel = AgentToolPermissionLevel.ConfirmationRequired,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["minecraft_version"] = preparedPlan.MinecraftVersion,
                    ["version_name"] = preparedPlan.VersionName,
                    ["loader_summary"] = preparedPlan.LoaderSummary,
                    ["loader_selections"] = JsonSerializer.Serialize(preparedPlan.LoaderSelections, SerializerOptions)
                }
            };

            var message = preparedPlan.LoaderSelections.Count == 0
                ? $"已准备安装原版 Minecraft {preparedPlan.MinecraftVersion}，目标版本名为 {preparedPlan.VersionName}，等待用户确认。"
                : $"已准备安装 Minecraft {preparedPlan.MinecraftVersion}，加载器：{preparedPlan.LoaderSummary}，目标版本名为 {preparedPlan.VersionName}，等待用户确认。";

            return AgentToolExecutionResult.FromActionProposal(message, proposal);
        }
        catch (Exception ex)
        {
            return AgentToolExecutionResult.FromMessage($"installGame 无法执行: {ex.Message}");
        }
    }

    private static List<InstallGameLoaderRequest> ParseLoaders(JToken? loadersToken)
    {
        if (loadersToken == null || loadersToken.Type == JTokenType.Null)
        {
            return [];
        }

        if (loadersToken is not JArray loaderArray)
        {
            throw new InvalidOperationException("loaders 必须是数组。");
        }

        List<InstallGameLoaderRequest> loaders = [];
        foreach (var item in loaderArray)
        {
            if (item is not JObject loaderObject)
            {
                throw new InvalidOperationException("loaders 数组中的每一项都必须是对象。");
            }

            loaders.Add(new InstallGameLoaderRequest(
                loaderObject["type"]?.ToString() ?? string.Empty,
                loaderObject["version"]?.ToString()));
        }

        return loaders;
    }
}

public sealed class InstallGameActionHandler : IAgentActionHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAgentGameInstallService _gameInstallService;

    public InstallGameActionHandler(IAgentGameInstallService gameInstallService)
    {
        _gameInstallService = gameInstallService;
    }

    public string ActionType => InstallGameToolHandler.ToolNameValue;

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public async Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        if (!proposal.Parameters.TryGetValue("minecraft_version", out var minecraftVersion) || string.IsNullOrWhiteSpace(minecraftVersion))
        {
            throw new InvalidOperationException("缺少 minecraft_version 参数。");
        }

        if (!proposal.Parameters.TryGetValue("version_name", out var versionName) || string.IsNullOrWhiteSpace(versionName))
        {
            throw new InvalidOperationException("缺少 version_name 参数。");
        }

        proposal.Parameters.TryGetValue("loader_summary", out var loaderSummary);
        proposal.Parameters.TryGetValue("loader_selections", out var loaderSelectionsJson);

        var loaderSelections = string.IsNullOrWhiteSpace(loaderSelectionsJson)
            ? []
            : JsonSerializer.Deserialize<List<ModLoaderSelection>>(loaderSelectionsJson, SerializerOptions) ?? [];

        var operationId = await _gameInstallService.StartInstallAsync(
            minecraftVersion,
            loaderSelections,
            versionName,
            cancellationToken);

        var message = loaderSelections.Count == 0
            ? $"已开始安装原版 Minecraft {minecraftVersion}，版本名：{versionName}。\noperation_id: {operationId}\n可继续使用 getOperationStatus 查询进度，下载队列 TeachingTip 也会同步显示。"
            : $"已开始安装 Minecraft {minecraftVersion}，加载器：{loaderSummary}，版本名：{versionName}。\noperation_id: {operationId}\n可继续使用 getOperationStatus 查询进度，下载队列 TeachingTip 也会同步显示。";

        return message;
    }
}

public sealed class GetOperationStatusToolHandler : IAgentToolHandler
{
    private readonly IAgentOperationStatusService _operationStatusService;

    public GetOperationStatusToolHandler(IAgentOperationStatusService operationStatusService)
    {
        _operationStatusService = operationStatusService;
    }

    public string ToolName => "getOperationStatus";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "根据 operation_id 查询 installGame、installCommunityResource（含世界安装）、installModpack、updateModpack、updateInstanceCommunityResources 或 launchGame 返回的任务状态。",
        new
        {
            type = "object",
            properties = new
            {
                operation_id = new { type = "string", description = "installGame、installCommunityResource（含世界安装）、installModpack、updateModpack、updateInstanceCommunityResources 或 launchGame 执行后返回的 operation_id" }
            },
            required = new[] { "operation_id" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var operationId = arguments["operation_id"]?.ToString()
            ?? arguments["operationId"]?.ToString()
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return Task.FromResult(AgentToolExecutionResult.FromMessage("请提供 operation_id。"));
        }

        return Task.FromResult(AgentToolExecutionResult.FromMessage(_operationStatusService.GetOperationStatusMessage(operationId)));
    }
}