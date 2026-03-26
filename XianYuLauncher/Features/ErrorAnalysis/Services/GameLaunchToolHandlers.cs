using System.Text;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed class GetCurrentGameDirectoryToolHandler : IAgentToolHandler
{
    private readonly IFileService _fileService;

    public GetCurrentGameDirectoryToolHandler(IFileService fileService)
    {
        _fileService = fileService;
    }

    public string ToolName => "get_current_game_directory";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "返回启动器当前使用的 Minecraft 游戏根目录。若当前会话已知版本 ID，也会附带给出对应的版本目录路径示例。",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var currentGameDirectory = _fileService.GetMinecraftDataPath();
        var builder = new StringBuilder();
        builder.AppendLine($"当前游戏目录: {currentGameDirectory}");

        if (!string.IsNullOrWhiteSpace(context.VersionId))
        {
            builder.AppendLine($"当前会话版本 ID: {context.VersionId}");
            builder.AppendLine($"对应版本目录示例: {Path.Combine(currentGameDirectory, MinecraftPathConsts.Versions, context.VersionId)}");
        }

        return Task.FromResult(AgentToolExecutionResult.FromMessage(builder.ToString().TrimEnd()));
    }
}

public sealed class LaunchGameToolHandler : IAgentToolHandler
{
    public const string ToolNameValue = "launch_game";

    private readonly IVersionPathGameLaunchService _versionPathGameLaunchService;

    public LaunchGameToolHandler(IVersionPathGameLaunchService versionPathGameLaunchService)
    {
        _versionPathGameLaunchService = versionPathGameLaunchService;
    }

    public string ToolName => ToolNameValue;

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "按版本目录绝对路径启动游戏。path 的语义与 xianyulauncher://launch/?path=... 完全一致，必须传入 versions 下的具体版本目录，而不是游戏根目录。profileId 可选，填写后使用指定档案启动；留空则使用当前默认档案。",
        new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "版本目录绝对路径，例如 D:\\zm\\.minecraft\\versions\\1.21.10" },
                profileId = new { type = "string", description = "可选。档案 UUID；若提供，则使用该档案启动。" }
            },
            required = new[] { "path" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var path = arguments["path"]?.ToString() ?? string.Empty;
        var profileId = arguments["profileId"]?.ToString();

        try
        {
            var preparedLaunch = _versionPathGameLaunchService.PrepareLaunch(path);
            var proposal = new AgentActionProposal
            {
                ActionType = ToolName,
                ButtonText = $"启动 {preparedLaunch.VersionName}",
                PermissionLevel = AgentToolPermissionLevel.ConfirmationRequired,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["path"] = preparedLaunch.VersionPath
                }
            };

            if (!string.IsNullOrWhiteSpace(profileId))
            {
                proposal.Parameters["profileId"] = profileId;
            }

            var message = $"已准备启动版本 {preparedLaunch.VersionName}。实例路径：{preparedLaunch.VersionPath}。启动时会临时切换到游戏根目录 {preparedLaunch.MinecraftPath}，完成启动流程后再恢复原目录，等待用户确认。";
            return Task.FromResult(AgentToolExecutionResult.FromActionProposal(message, proposal));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AgentToolExecutionResult.FromMessage($"launch_game 无法执行: {ex.Message}"));
        }
    }
}

public sealed class LaunchGameActionHandler : IAgentActionHandler
{
    private readonly IVersionPathGameLaunchService _versionPathGameLaunchService;

    public LaunchGameActionHandler(IVersionPathGameLaunchService versionPathGameLaunchService)
    {
        _versionPathGameLaunchService = versionPathGameLaunchService;
    }

    public string ActionType => LaunchGameToolHandler.ToolNameValue;

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public async Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        if (!proposal.Parameters.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("缺少 path 参数。");
        }

        proposal.Parameters.TryGetValue("profileId", out var profileId);

        var preparedLaunch = _versionPathGameLaunchService.PrepareLaunch(path);
        var result = await _versionPathGameLaunchService.LaunchAsync(
            preparedLaunch,
            new VersionPathLaunchOptions
            {
                ProfileId = profileId,
            },
            cancellationToken: cancellationToken);
        if (result.GameProcess != null)
        {
            return $"已开始启动 {preparedLaunch.VersionName}。实例路径：{preparedLaunch.VersionPath}。启动前已临时切换到 {preparedLaunch.MinecraftPath}，现在已恢复原游戏目录。";
        }

        return $"启动 {preparedLaunch.VersionName} 失败：{result.ErrorMessage ?? "游戏未能启动，请查看日志。"}";
    }
}

public sealed class GetProfilesToolHandler : IAgentToolHandler
{
    private readonly IProfileManager _profileManager;

    public GetProfilesToolHandler(IProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public string ToolName => "get_profiles";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "返回当前启动器已保存的档案列表。每个档案包含玩家名、玩家 UUID、账户类型，以及是否为当前默认档案；其中 profileId 与 uuid 相同，可直接传给 launch_game。",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>(),
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var profiles = await _profileManager.LoadProfilesAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var orderedProfiles = profiles
            .OrderByDescending(profile => profile.IsActive)
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new
            {
                profileId = profile.Id,
                uuid = profile.Id,
                name = profile.Name,
                accountType = GetAccountType(profile),
                isActive = profile.IsActive,
            })
            .ToList();

        var payload = new
        {
            profiles = orderedProfiles,
            activeProfileId = orderedProfiles.FirstOrDefault(profile => profile.isActive)?.profileId,
        };

        return AgentToolExecutionResult.FromMessage(Newtonsoft.Json.JsonConvert.SerializeObject(payload, Newtonsoft.Json.Formatting.Indented));
    }

    private static string GetAccountType(MinecraftProfile profile)
    {
        if (profile.IsOffline)
        {
            return "offline";
        }

        return string.Equals(profile.TokenType, "external", StringComparison.OrdinalIgnoreCase)
            ? "external"
            : "microsoft";
    }
}