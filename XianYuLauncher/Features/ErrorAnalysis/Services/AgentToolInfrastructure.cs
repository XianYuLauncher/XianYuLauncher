using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed class AgentToolExecutionResult
{
    private AgentToolExecutionResult(string message, AgentActionProposal? actionProposal)
    {
        Message = message;
        ActionProposal = actionProposal;
    }

    public string Message { get; }

    public AgentActionProposal? ActionProposal { get; }

    public static AgentToolExecutionResult FromMessage(string message)
    {
        return new AgentToolExecutionResult(message, null);
    }

    public static AgentToolExecutionResult FromActionProposal(string message, AgentActionProposal actionProposal)
    {
        return new AgentToolExecutionResult(message, actionProposal);
    }
}

public interface IAgentToolHandler
{
    string ToolName { get; }

    AiToolDefinition ToolDefinition { get; }

    AgentToolPermissionLevel PermissionLevel { get; }

    bool IsAvailable(ErrorAnalysisSessionContext context);

    Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken);
}

public interface IAgentActionHandler
{
    string ActionType { get; }

    AgentToolPermissionLevel PermissionLevel { get; }

    Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken);
}

public interface IAgentToolDispatcher
{
    IReadOnlyList<AiToolDefinition> GetAvailableTools();

    Task<AgentToolExecutionResult> ExecuteAsync(ToolCallInfo toolCall, CancellationToken cancellationToken);
}

public interface IAgentActionExecutor
{
    Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken);
}

public interface IAgentToolSupportService
{
    IReadOnlyList<string> GetInstalledModFiles();

    Task<string?> FindModFileByIdAsync(string modId);

    Task<string?> TryGetFabricModIdAsync(string jarPath);

    Task<string> GetCurrentLoaderTypeAsync(CancellationToken cancellationToken = default);

    JavaVersion? SelectBestJava(IReadOnlyCollection<JavaVersion> javaVersions, int requiredMajorVersion);
}

public sealed class AgentToolSupportService : IAgentToolSupportService
{
    private readonly ErrorAnalysisSessionState _sessionState;
    private readonly IVersionInfoService _versionInfoService;

    public AgentToolSupportService(
        ErrorAnalysisSessionState sessionState,
        IVersionInfoService versionInfoService)
    {
        _sessionState = sessionState;
        _versionInfoService = versionInfoService;
    }

    public IReadOnlyList<string> GetInstalledModFiles()
    {
        var minecraftPath = _sessionState.Context.MinecraftPath;
        var versionId = _sessionState.Context.VersionId;
        if (string.IsNullOrWhiteSpace(minecraftPath))
        {
            return [];
        }

        List<string> candidateFiles = [];
        var globalModsPath = Path.Combine(minecraftPath, MinecraftPathConsts.Mods);
        candidateFiles.AddRange(TryGetModFiles(globalModsPath));

        if (!string.IsNullOrWhiteSpace(versionId))
        {
            var versionModsPath = Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionId, MinecraftPathConsts.Mods);
            candidateFiles.AddRange(TryGetModFiles(versionModsPath));
        }

        return candidateFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<string?> FindModFileByIdAsync(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return null;
        }

        var normalizedModId = modId.Trim();
        foreach (var file in GetInstalledModFiles())
        {
            var fileName = Path.GetFileName(file);
            if (!string.IsNullOrWhiteSpace(fileName) &&
                fileName.Contains(normalizedModId, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }

            var fabricId = await TryGetFabricModIdAsync(file);
            if (!string.IsNullOrWhiteSpace(fabricId) &&
                string.Equals(fabricId, normalizedModId, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> TryGetModFiles(string modsPath)
    {
        if (!Directory.Exists(modsPath))
        {
            return [];
        }

        try
        {
            return Directory.GetFiles(modsPath, "*.jar*");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AgentToolSupportService] 枚举 Mod 目录失败: {modsPath}, {ex}");
            return [];
        }
    }

    public async Task<string?> TryGetFabricModIdAsync(string jarPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(jarPath);
            var entry = archive.GetEntry("fabric.mod.json");
            if (entry == null)
            {
                return null;
            }

            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var obj = JObject.Parse(json);
            return obj["id"]?.ToString();
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> GetCurrentLoaderTypeAsync(CancellationToken cancellationToken = default)
    {
        var versionId = _sessionState.Context.VersionId;
        var minecraftPath = _sessionState.Context.MinecraftPath;
        if (string.IsNullOrWhiteSpace(versionId) || string.IsNullOrWhiteSpace(minecraftPath))
        {
            return string.Empty;
        }

        try
        {
            var versionDirectory = Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionId);
            var config = await _versionInfoService.GetFullVersionInfoAsync(versionId, versionDirectory, preferCache: true);
            cancellationToken.ThrowIfCancellationRequested();
            return config?.ModLoaderType?.Trim().ToLowerInvariant() ?? string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取版本加载器失败: {ex.Message}");
            return string.Empty;
        }
    }

    public JavaVersion? SelectBestJava(IReadOnlyCollection<JavaVersion> javaVersions, int requiredMajorVersion)
    {
        if (javaVersions == null || javaVersions.Count == 0)
        {
            return null;
        }

        var candidates = javaVersions
            .Where(j =>
                !string.IsNullOrWhiteSpace(j.Path) &&
                File.Exists(j.Path) &&
                j.MajorVersion > 0 &&
                j.MajorVersion >= requiredMajorVersion)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .OrderByDescending(j => j.MajorVersion == requiredMajorVersion)
            .ThenByDescending(j => j.IsJDK)
            .ThenBy(j => j.MajorVersion - requiredMajorVersion)
            .FirstOrDefault();
    }
}

public sealed class AgentToolDispatcher : IAgentToolDispatcher
{
    private readonly IEnumerable<IAgentToolHandler> _handlers;
    private readonly ErrorAnalysisSessionState _sessionState;

    public AgentToolDispatcher(IEnumerable<IAgentToolHandler> handlers, ErrorAnalysisSessionState sessionState)
    {
        _handlers = handlers;
        _sessionState = sessionState;
    }

    public IReadOnlyList<AiToolDefinition> GetAvailableTools()
    {
        return _handlers
            .Where(h => h.IsAvailable(_sessionState.Context))
            .Select(h => h.ToolDefinition)
            .ToList();
    }

    public async Task<AgentToolExecutionResult> ExecuteAsync(ToolCallInfo toolCall, CancellationToken cancellationToken)
    {
        try
        {
            var handler = _handlers.FirstOrDefault(h => string.Equals(h.ToolName, toolCall.FunctionName, StringComparison.OrdinalIgnoreCase));
            if (handler == null)
            {
                return AgentToolExecutionResult.FromMessage($"未知工具: {toolCall.FunctionName}");
            }

            if (!handler.IsAvailable(_sessionState.Context))
            {
                return AgentToolExecutionResult.FromMessage($"工具当前不可用: {toolCall.FunctionName}");
            }

            JObject args;
            try
            {
                args = string.IsNullOrWhiteSpace(toolCall.Arguments)
                    ? new JObject()
                    : JObject.Parse(toolCall.Arguments);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI Tool Error] {toolCall.FunctionName} 参数解析失败: {ex}");
                return AgentToolExecutionResult.FromMessage($"工具参数格式无效: {toolCall.FunctionName}。请重试。\n原始参数需要是合法 JSON。");
            }

            return await handler.ExecuteAsync(_sessionState.Context, args, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AI Tool Error] {toolCall.FunctionName}: {ex}");
            return AgentToolExecutionResult.FromMessage("工具执行失败，请重试或查看日志。");
        }
    }
}

public sealed class AgentActionExecutor : IAgentActionExecutor
{
    private readonly IEnumerable<IAgentActionHandler> _handlers;

    public AgentActionExecutor(IEnumerable<IAgentActionHandler> handlers)
    {
        _handlers = handlers;
    }

    public async Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        var handler = _handlers.FirstOrDefault(h => string.Equals(h.ActionType, proposal.ActionType, StringComparison.OrdinalIgnoreCase));
        if (handler == null)
        {
            return $"未找到可执行的动作处理器: {proposal.ActionType}";
        }

        return await handler.ExecuteAsync(proposal, cancellationToken);
    }
}