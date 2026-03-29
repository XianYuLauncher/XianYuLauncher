using Newtonsoft.Json.Linq;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed class SwitchMinecraftPathToolHandler : IAgentToolHandler
{
    public const string ToolNameValue = "switchMinecraftPath";

    private readonly IGameSettingsDomainService _gameSettingsDomainService;
    private readonly IAgentSettingsActionProposalService _proposalService;

    public SwitchMinecraftPathToolHandler(
        IGameSettingsDomainService gameSettingsDomainService,
        IAgentSettingsActionProposalService proposalService)
    {
        _gameSettingsDomainService = gameSettingsDomainService;
        _proposalService = proposalService;
    }

    public string ToolName => ToolNameValue;

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "切换启动器当前活动的 Minecraft 根目录。必须先调用 getMinecraftPaths，从返回的 path_id 或 path 中选择一个已保存目录；本工具只生成确认提案，不会直接写入。",
        new
        {
            type = "object",
            properties = new
            {
                pathId = new { type = "string", description = "可选。getMinecraftPaths 返回的 path_id，例如 mcdir_2。推荐优先使用。" },
                path = new { type = "string", description = "可选。已保存目录列表中的绝对路径。若与 pathId 同时提供，两者必须指向同一目录。" }
            },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var requestedPathId = ReadFirstNonEmpty(arguments, "pathId", "path_id", "minecraftPathId");
        var requestedPath = ReadFirstNonEmpty(arguments, "path", "minecraftPath", "minecraft_path");

        var currentMinecraftPath = await _gameSettingsDomainService.ResolveCurrentMinecraftPathAsync();
        var pathsJson = await _gameSettingsDomainService.LoadMinecraftPathsJsonAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (!AgentMinecraftPathHelper.TryResolveSelection(
                currentMinecraftPath,
                pathsJson,
                requestedPathId,
                requestedPath,
                out var selection,
                out var errorMessage))
        {
            return AgentToolExecutionResult.FromMessage(errorMessage);
        }

        if (selection!.TargetAlreadyActive)
        {
            return AgentToolExecutionResult.FromMessage(
                $"目标目录已经是当前活动 Minecraft 目录：{FormatPathSummary(selection.TargetPathName, selection.TargetPath)}");
        }

        var proposal = _proposalService.CreateProposal(
            ToolName,
            $"切换到 {selection.TargetPathName}",
            new AgentSettingsActionProposalPayload
            {
                Scope = AgentSettingsProposalScopes.Global,
                Changes =
                [
                    new AgentSettingsFieldChange
                    {
                        FieldKey = "current_minecraft_path",
                        DisplayName = "当前 Minecraft 根目录",
                        OldValue = FormatPathLabel(selection.CurrentPathId, selection.CurrentPathName, selection.CurrentPath),
                        NewValue = FormatPathLabel(selection.TargetPathId, selection.TargetPathName, selection.TargetPath)
                    }
                ]
            });

        proposal.Parameters["minecraft_path_id"] = selection.TargetPathId;
        proposal.Parameters["minecraft_path"] = selection.TargetPath;
        proposal.Parameters["minecraft_path_name"] = selection.TargetPathName;

        return AgentToolExecutionResult.FromActionProposal(
            $"已准备切换活动 Minecraft 目录：{FormatPathSummary(selection.CurrentPathName, selection.CurrentPath)} -> {FormatPathSummary(selection.TargetPathName, selection.TargetPath)}，等待用户确认。",
            proposal);
    }

    private static string? ReadFirstNonEmpty(JObject arguments, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = arguments[propertyName]?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string FormatPathLabel(string pathId, string pathName, string path)
    {
        return $"{pathName} [{pathId}] {path}";
    }

    private static string FormatPathSummary(string pathName, string path)
    {
        return $"{pathName} ({path})";
    }
}

public sealed class SwitchMinecraftPathActionHandler : IAgentActionHandler
{
    private readonly IGameSettingsDomainService _gameSettingsDomainService;
    private readonly ErrorAnalysisSessionState _sessionState;

    public SwitchMinecraftPathActionHandler(
        IGameSettingsDomainService gameSettingsDomainService,
        ErrorAnalysisSessionState sessionState)
    {
        _gameSettingsDomainService = gameSettingsDomainService;
        _sessionState = sessionState;
    }

    public string ActionType => SwitchMinecraftPathToolHandler.ToolNameValue;

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public async Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        proposal.Parameters.TryGetValue("minecraft_path_id", out var requestedPathId);
        proposal.Parameters.TryGetValue("minecraft_path", out var requestedPath);

        if (string.IsNullOrWhiteSpace(requestedPathId) && string.IsNullOrWhiteSpace(requestedPath))
        {
            return "缺少目标 Minecraft 目录信息。";
        }

        var currentMinecraftPath = await _gameSettingsDomainService.ResolveCurrentMinecraftPathAsync();
        var pathsJson = await _gameSettingsDomainService.LoadMinecraftPathsJsonAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (!AgentMinecraftPathHelper.TryResolveSelection(
                currentMinecraftPath,
                pathsJson,
                requestedPathId,
                requestedPath,
                out var selection,
                out var errorMessage))
        {
            return $"切换 Minecraft 目录失败：{errorMessage}";
        }

        if (selection!.TargetAlreadyActive)
        {
            _sessionState.Context.MinecraftPath = selection.TargetPath;
            return $"目标目录已经是当前活动 Minecraft 目录：{FormatPathSummary(selection.TargetPathName, selection.TargetPath)}";
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _gameSettingsDomainService.SaveMinecraftPathAsync(selection.TargetPath);
            cancellationToken.ThrowIfCancellationRequested();
            await _gameSettingsDomainService.SaveMinecraftPathsJsonAsync(selection.UpdatedPathsJson);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            try
            {
                await _gameSettingsDomainService.SaveMinecraftPathAsync(selection.CurrentPath);
            }
            catch
            {
            }

            return $"切换 Minecraft 目录失败：{ex.Message}";
        }

        _sessionState.Context.MinecraftPath = selection.TargetPath;

        var currentVersionId = _sessionState.Context.VersionId;
        if (!string.IsNullOrWhiteSpace(currentVersionId))
        {
            var targetVersionDirectory = Path.Combine(selection.TargetPath, MinecraftPathConsts.Versions, currentVersionId);
            if (!Directory.Exists(targetVersionDirectory))
            {
                _sessionState.Context.VersionId = string.Empty;
                return $"已切换活动 Minecraft 目录：{FormatPathSummary(selection.CurrentPathName, selection.CurrentPath)} -> {FormatPathSummary(selection.TargetPathName, selection.TargetPath)}。当前会话原版本 {currentVersionId} 在新目录下不存在，已清空当前版本上下文；如需继续针对新目录中的实例操作，请先调用 get_instances。";
            }
        }

        return $"已切换活动 Minecraft 目录：{FormatPathSummary(selection.CurrentPathName, selection.CurrentPath)} -> {FormatPathSummary(selection.TargetPathName, selection.TargetPath)}。";
    }

    private static string FormatPathSummary(string pathName, string path)
    {
        return $"{pathName} ({path})";
    }
}