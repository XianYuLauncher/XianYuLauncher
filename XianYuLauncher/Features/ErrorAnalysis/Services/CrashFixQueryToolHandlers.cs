using System.Text;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed class ListInstalledModsToolHandler : IAgentToolHandler
{
    private readonly IAgentToolSupportService _toolSupportService;

    public ListInstalledModsToolHandler(IAgentToolSupportService toolSupportService)
    {
        _toolSupportService = toolSupportService;
    }

    public string ToolName => "listInstalledMods";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "列出当前游戏版本已安装的所有 Mod，返回名称、版本、文件名、是否启用",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => !string.IsNullOrWhiteSpace(context.MinecraftPath);

    public Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var files = _toolSupportService.GetInstalledModFiles();
        if (files.Count == 0)
        {
            return Task.FromResult(AgentToolExecutionResult.FromMessage("mods 目录为空，没有安装任何 Mod。"));
        }

        var sb = new StringBuilder();
        sb.AppendLine($"共 {files.Count} 个 Mod 文件：");
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(file);
            var enabled = !fileName.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase);
            sb.AppendLine($"- {fileName} [{(enabled ? "启用" : "禁用")}] ");
        }

        return Task.FromResult(AgentToolExecutionResult.FromMessage(sb.ToString()));
    }
}

public sealed class GetVersionConfigToolHandler : IAgentToolHandler
{
    private readonly IVersionInfoService _versionInfoService;

    public GetVersionConfigToolHandler(IVersionInfoService versionInfoService)
    {
        _versionInfoService = versionInfoService;
    }

    public string ToolName => "getVersionConfig";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "获取当前会话实例的版本配置快照，返回 JSON，包含 uses_global_settings_overall，以及 Java/JVM/GC/内存/分辨率/版本隔离的局部配置与 follows_global 状态。",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) =>
        !string.IsNullOrWhiteSpace(context.VersionId) && !string.IsNullOrWhiteSpace(context.MinecraftPath);

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        try
        {
            var versionDirectory = Path.Combine(context.MinecraftPath, MinecraftPathConsts.Versions, context.VersionId);
            var config = await _versionInfoService.GetFullVersionInfoAsync(context.VersionId, versionDirectory, preferCache: true);
            cancellationToken.ThrowIfCancellationRequested();

            return AgentToolExecutionResult.FromMessage(
                AgentSettingsSnapshotJsonHelper.BuildVersionConfigSnapshotJson(
                    context.VersionId,
                    context.MinecraftPath,
                    versionDirectory,
                    config));
        }
        catch (Exception ex)
        {
            return AgentToolExecutionResult.FromMessage($"读取版本配置失败: {ex.Message}");
        }
    }
}

public sealed class CheckJavaVersionsToolHandler : IAgentToolHandler
{
    private readonly IJavaRuntimeService _javaRuntimeService;
    private readonly IGameSettingsDomainService _gameSettingsDomainService;

    public CheckJavaVersionsToolHandler(
        IJavaRuntimeService javaRuntimeService,
        IGameSettingsDomainService gameSettingsDomainService)
    {
        _javaRuntimeService = javaRuntimeService;
        _gameSettingsDomainService = gameSettingsDomainService;
    }

    public string ToolName => "checkJavaVersions";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "列出启动器当前可见的 Java 版本清单。默认优先返回已缓存列表；refresh=true 时重新扫描。返回 JSON，包含 java_selection_mode、selected_java_path 和 java_versions。",
        new
        {
            type = "object",
            properties = new
            {
                refresh = new { type = "boolean", description = "可选。true 表示重新扫描本机 Java 版本；默认 false 优先读取已缓存列表。" }
            },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        try
        {
            var refresh = arguments["refresh"]?.Value<bool>()
                ?? arguments["forceRefresh"]?.Value<bool>()
                ?? false;
            var selectionMode = await _gameSettingsDomainService.LoadJavaSelectionModeAsync();
            var selectedJavaPath = NullIfWhiteSpace(await _gameSettingsDomainService.LoadJavaPathAsync());

            IReadOnlyList<JavaVersion> javaVersions;
            var cachedJavaVersions = refresh ? null : await _gameSettingsDomainService.LoadJavaVersionsAsync();
            if (!refresh && cachedJavaVersions is { Count: > 0 })
            {
                javaVersions = cachedJavaVersions;
            }
            else
            {
                javaVersions = await _javaRuntimeService.DetectJavaVersionsAsync(refresh);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return AgentToolExecutionResult.FromMessage(
                AgentSettingsSnapshotJsonHelper.BuildJavaVersionsSnapshotJson(
                    refresh,
                    selectionMode,
                    selectedJavaPath,
                    javaVersions,
                    !refresh && cachedJavaVersions is { Count: > 0 }));
        }
        catch (Exception ex)
        {
            return AgentToolExecutionResult.FromMessage($"检测 Java 版本失败: {ex.Message}");
        }
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public sealed class GetMinecraftPathsToolHandler : IAgentToolHandler
{
    private readonly IGameSettingsDomainService _gameSettingsDomainService;

    public GetMinecraftPathsToolHandler(IGameSettingsDomainService gameSettingsDomainService)
    {
        _gameSettingsDomainService = gameSettingsDomainService;
    }

    public string ToolName => "getMinecraftPaths";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "返回启动器已保存的 Minecraft 根目录列表和当前活动目录。结果包含短 ID，可供后续目录切换工具使用。",
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
        try
        {
            var currentMinecraftPath = await _gameSettingsDomainService.ResolveCurrentMinecraftPathAsync();
            var pathsJson = await _gameSettingsDomainService.LoadMinecraftPathsJsonAsync();
            cancellationToken.ThrowIfCancellationRequested();

            return AgentToolExecutionResult.FromMessage(
                AgentSettingsSnapshotJsonHelper.BuildMinecraftPathsSnapshotJson(currentMinecraftPath, pathsJson));
        }
        catch (Exception ex)
        {
            return AgentToolExecutionResult.FromMessage($"读取 Minecraft 目录列表失败: {ex.Message}");
        }
    }
}

public sealed class GetGlobalLaunchSettingsToolHandler : IAgentToolHandler
{
    private readonly IGameSettingsDomainService _gameSettingsDomainService;

    public GetGlobalLaunchSettingsToolHandler(IGameSettingsDomainService gameSettingsDomainService)
    {
        _gameSettingsDomainService = gameSettingsDomainService;
    }

    public string ToolName => "getGlobalLaunchSettings";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "返回启动器当前全局启动设置的 JSON 快照，包含 Java 选择方式、当前选中的 Java、全局 JVM/GC、内存、分辨率，以及当前全局游戏目录模式。",
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
        try
        {
            var globalLaunchSettingsTask = _gameSettingsDomainService.LoadGlobalLaunchSettingsAsync();
            var javaSelectionModeTask = _gameSettingsDomainService.LoadJavaSelectionModeAsync();
            var selectedJavaPathTask = _gameSettingsDomainService.LoadJavaPathAsync();
            var knownJavaVersionsTask = _gameSettingsDomainService.LoadJavaVersionsAsync();
            var gameIsolationModeTask = _gameSettingsDomainService.LoadGameIsolationModeAsync();
            var legacyVersionIsolationTask = _gameSettingsDomainService.LoadEnableVersionIsolationAsync();
            var customGameDirectoryPathTask = _gameSettingsDomainService.LoadCustomGameDirectoryAsync();
            var currentMinecraftPathTask = _gameSettingsDomainService.ResolveCurrentMinecraftPathAsync();

            await Task.WhenAll(
                globalLaunchSettingsTask,
                javaSelectionModeTask,
                selectedJavaPathTask,
                knownJavaVersionsTask,
                gameIsolationModeTask,
                legacyVersionIsolationTask,
                customGameDirectoryPathTask,
                currentMinecraftPathTask);
            cancellationToken.ThrowIfCancellationRequested();

            var globalLaunchSettings = await globalLaunchSettingsTask;
            return AgentToolExecutionResult.FromMessage(
                AgentSettingsSnapshotJsonHelper.BuildGlobalLaunchSettingsSnapshotJson(new AgentGlobalSettingsSnapshotInput
                {
                    AutoMemoryAllocation = globalLaunchSettings.AutoMemoryAllocation,
                    InitialHeapMemory = globalLaunchSettings.InitialHeapMemory,
                    MaximumHeapMemory = globalLaunchSettings.MaximumHeapMemory,
                    CustomJvmArguments = globalLaunchSettings.CustomJvmArguments,
                    GarbageCollectorMode = globalLaunchSettings.GarbageCollectorMode,
                    WindowWidth = globalLaunchSettings.WindowWidth,
                    WindowHeight = globalLaunchSettings.WindowHeight,
                    JavaSelectionMode = await javaSelectionModeTask,
                    SelectedJavaPath = await selectedJavaPathTask,
                    KnownJavaVersions = await knownJavaVersionsTask ?? [],
                    GameIsolationModeKey = await gameIsolationModeTask,
                    LegacyEnableVersionIsolation = await legacyVersionIsolationTask,
                    CustomGameDirectoryPath = await customGameDirectoryPathTask,
                    CurrentMinecraftPath = await currentMinecraftPathTask,
                }));
        }
        catch (Exception ex)
        {
            return AgentToolExecutionResult.FromMessage($"读取全局启动设置失败: {ex.Message}");
        }
    }
}

public sealed class GetEffectiveLaunchSettingsToolHandler : IAgentToolHandler
{
    private readonly IVersionInfoService _versionInfoService;
    private readonly IVersionInfoManager _versionInfoManager;
    private readonly ILaunchSettingsResolver _launchSettingsResolver;
    private readonly IGameDirResolver _gameDirResolver;
    private readonly IGameSettingsDomainService _gameSettingsDomainService;

    public GetEffectiveLaunchSettingsToolHandler(
        IVersionInfoService versionInfoService,
        IVersionInfoManager versionInfoManager,
        ILaunchSettingsResolver launchSettingsResolver,
        IGameDirResolver gameDirResolver,
        IGameSettingsDomainService gameSettingsDomainService)
    {
        _versionInfoService = versionInfoService;
        _versionInfoManager = versionInfoManager;
        _launchSettingsResolver = launchSettingsResolver;
        _gameDirResolver = gameDirResolver;
        _gameSettingsDomainService = gameSettingsDomainService;
    }

    public string ToolName => "getEffectiveLaunchSettings";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "返回当前会话实例的最终生效启动设置 JSON 快照，包含 required_java_version、最终 Java/内存/JVM/GC/分辨率，以及最终生效的游戏目录。",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) =>
        !string.IsNullOrWhiteSpace(context.VersionId) && !string.IsNullOrWhiteSpace(context.MinecraftPath);

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        try
        {
            var versionDirectory = Path.Combine(context.MinecraftPath, MinecraftPathConsts.Versions, context.VersionId);
            var versionConfigTask = _versionInfoService.GetFullVersionInfoAsync(context.VersionId, versionDirectory, preferCache: true);
            var versionInfoTask = GetVersionInfoWithFallbackAsync(context, cancellationToken);
            var resolvedGameDirectoryTask = _gameDirResolver.GetGameDirForVersionAsync(context.VersionId);
            var globalGameIsolationModeTask = _gameSettingsDomainService.LoadGameIsolationModeAsync();
            var globalLegacyVersionIsolationTask = _gameSettingsDomainService.LoadEnableVersionIsolationAsync();
            var globalCustomGameDirectoryTask = _gameSettingsDomainService.LoadCustomGameDirectoryAsync();

            await Task.WhenAll(
                versionConfigTask,
                versionInfoTask,
                resolvedGameDirectoryTask,
                globalGameIsolationModeTask,
                globalLegacyVersionIsolationTask,
                globalCustomGameDirectoryTask);
            cancellationToken.ThrowIfCancellationRequested();

            var versionConfig = await versionConfigTask;
            var versionInfo = await versionInfoTask;
            var requiredJavaVersion = versionInfo?.JavaVersion?.MajorVersion ?? 8;
            var effectiveSettings = await _launchSettingsResolver.ResolveAsync(versionConfig, requiredJavaVersion);
            cancellationToken.ThrowIfCancellationRequested();

            return AgentToolExecutionResult.FromMessage(
                AgentSettingsSnapshotJsonHelper.BuildEffectiveLaunchSettingsSnapshotJson(new AgentEffectiveSettingsSnapshotInput
                {
                    VersionId = context.VersionId,
                    MinecraftRootPath = context.MinecraftPath,
                    VersionDirectoryPath = versionDirectory,
                    Config = versionConfig,
                    EffectiveSettings = effectiveSettings,
                    RequiredJavaVersion = requiredJavaVersion,
                    RequiredJavaVersionFromVersionInfo = versionInfo?.JavaVersion?.MajorVersion is > 0,
                    ResolvedGameDirectory = await resolvedGameDirectoryTask,
                    GlobalGameIsolationModeKey = await globalGameIsolationModeTask,
                    GlobalLegacyEnableVersionIsolation = await globalLegacyVersionIsolationTask,
                    GlobalCustomGameDirectoryPath = await globalCustomGameDirectoryTask,
                }));
        }
        catch (Exception ex)
        {
            return AgentToolExecutionResult.FromMessage($"读取生效启动设置失败: {ex.Message}");
        }
    }

    private async Task<VersionInfo?> GetVersionInfoWithFallbackAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await _versionInfoManager.GetVersionInfoAsync(
                context.VersionId,
                context.MinecraftPath,
                allowNetwork: false,
                cancellationToken: cancellationToken);
        }
        catch
        {
            return await _versionInfoManager.GetVersionInfoAsync(
                context.VersionId,
                context.MinecraftPath,
                allowNetwork: true,
                cancellationToken: cancellationToken);
        }
    }
}

public sealed class SearchKnowledgeBaseToolHandler : IAgentToolHandler
{
    private readonly ICrashAnalyzer _crashAnalyzer;

    public SearchKnowledgeBaseToolHandler(ICrashAnalyzer crashAnalyzer)
    {
        _crashAnalyzer = crashAnalyzer;
    }

    public string ToolName => "searchKnowledgeBase";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "在内置错误知识库中搜索匹配的错误规则，返回匹配的错误标题、分析和修复建议",
        new
        {
            type = "object",
            properties = new
            {
                keyword = new { type = "string", description = "搜索关键词，如错误类名或关键日志片段" }
            },
            required = new[] { "keyword" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var keyword = arguments["keyword"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return AgentToolExecutionResult.FromMessage("请提供搜索关键词。");
        }

        try
        {
            var fakeOutput = new List<string> { keyword };
            var result = await _crashAnalyzer.AnalyzeCrashAsync(0, fakeOutput, fakeOutput);
            cancellationToken.ThrowIfCancellationRequested();

            if (result.Type == CrashType.Unknown)
            {
                return AgentToolExecutionResult.FromMessage($"知识库中未找到与 \"{keyword}\" 匹配的错误规则。");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"匹配到错误: {result.Title}");
            sb.AppendLine($"类型: {result.Type}");
            sb.AppendLine($"分析: {result.Analysis}");
            if (result.Suggestions.Count > 0)
            {
                sb.AppendLine("建议:");
                foreach (var suggestion in result.Suggestions)
                {
                    sb.AppendLine($"  - {suggestion}");
                }
            }

            return AgentToolExecutionResult.FromMessage(sb.ToString());
        }
        catch (Exception ex)
        {
            return AgentToolExecutionResult.FromMessage($"搜索知识库失败: {ex.Message}");
        }
    }
}

public sealed class ReadModInfoToolHandler : IAgentToolHandler
{
    private readonly IAgentToolSupportService _toolSupportService;

    public ReadModInfoToolHandler(IAgentToolSupportService toolSupportService)
    {
        _toolSupportService = toolSupportService;
    }

    public string ToolName => "readModInfo";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "读取指定 Mod 文件的元数据（fabric.mod.json 或 mods.toml），返回 modId、名称、版本、依赖列表",
        new
        {
            type = "object",
            properties = new
            {
                fileName = new { type = "string", description = "Mod 的 jar 文件名" }
            },
            required = new[] { "fileName" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => !string.IsNullOrWhiteSpace(context.MinecraftPath);

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var fileName = arguments["fileName"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return AgentToolExecutionResult.FromMessage("请提供 Mod 文件名。");
        }

        fileName = Path.GetFileName(fileName);
        var filePath = await _toolSupportService.FindModFileByIdAsync(fileName);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return AgentToolExecutionResult.FromMessage($"未找到 Mod 文件: {fileName}");
        }

        try
        {
            var modId = await _toolSupportService.TryGetFabricModIdAsync(filePath);
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(modId))
            {
                return AgentToolExecutionResult.FromMessage($"Mod文件: {fileName}\nFabric Mod ID: {modId}\n(完整元数据需解压 jar 读取 fabric.mod.json)");
            }

            return AgentToolExecutionResult.FromMessage($"Mod文件: {fileName}\n无法解析元数据（可能不是 Fabric mod，或 jar 格式异常）");
        }
        catch (Exception ex)
        {
            return AgentToolExecutionResult.FromMessage($"读取 Mod 信息失败: {ex.Message}");
        }
    }
}