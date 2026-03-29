using System.Globalization;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed class PatchGlobalLaunchSettingsToolHandler : IAgentToolHandler
{
    public const string ToolNameValue = "patchGlobalLaunchSettings";

    private readonly IAgentSettingsWriteService _settingsWriteService;

    public PatchGlobalLaunchSettingsToolHandler(IAgentSettingsWriteService settingsWriteService)
    {
        _settingsWriteService = settingsWriteService;
    }

    public string ToolName => ToolNameValue;

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "修改全局启动设置的确认式 patch 工具。调用前应先读取 getGlobalLaunchSettings；若涉及 Java，再先读取 checkJavaVersions。优先使用 selected_java_id 而不是 selected_java_path。本工具只生成确认提案，不会直接写入。当前支持字段：java_selection_mode、selected_java_id、selected_java_path、clear_selected_java、custom_jvm_arguments、garbage_collector_mode、auto_memory_allocation、initial_heap_memory_gb、maximum_heap_memory_gb、window_width、window_height、game_directory_mode、custom_game_directory_path。",
        new
        {
            type = "object",
            properties = new
            {
                java_selection_mode = new { type = "string", description = "可选。全局 Java 选择方式：auto 或 manual。" , @enum = new[] { "auto", "manual" } },
                selected_java_id = new { type = "string", description = "可选。checkJavaVersions 返回的 java_id，例如 java_1。" },
                selected_java_path = new { type = "string", description = "可选。绝对 Java 路径；优先推荐使用 selected_java_id。" },
                clear_selected_java = new { type = "boolean", description = "可选。清空当前保存的手动 Java 选择。若当前为 manual，工具会自动切回 auto。" },
                custom_jvm_arguments = new { type = "string", description = "可选。全局自定义 JVM 参数；传空字符串表示清空。" },
                garbage_collector_mode = new { type = "string", description = "可选。全局垃圾回收器模式。", @enum = new[] { "Auto", "G1GC", "ZGC", "ParallelGC", "SerialGC" } },
                auto_memory_allocation = new { type = "boolean", description = "可选。全局是否自动分配内存。true 为自动管理，false 为手动指定。" },
                initial_heap_memory_gb = new { type = "number", description = "可选。全局初始堆内存，单位 GB，范围 1-64；仅当 auto_memory_allocation=false 时允许修改。" },
                maximum_heap_memory_gb = new { type = "number", description = "可选。全局最大堆内存，单位 GB，范围 1-64；仅当 auto_memory_allocation=false 时允许修改。" },
                window_width = new { type = "integer", description = "可选。全局窗口宽度，范围 800-4096。" },
                window_height = new { type = "integer", description = "可选。全局窗口高度，范围 600-2160。" },
                game_directory_mode = new { type = "string", description = "可选。全局游戏目录模式。", @enum = new[] { "default", "version_isolation", "custom" } },
                custom_game_directory_path = new { type = "string", description = "可选。全局自定义游戏目录绝对路径；仅当 game_directory_mode=custom 时允许设置。传空字符串可清空已保存的自定义目录。" }
            },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        return _settingsWriteService.PrepareGlobalLaunchSettingsPatchAsync(new AgentGlobalLaunchSettingsPatchRequest
        {
            JavaSelectionMode = ReadFirstNonEmpty(arguments, "java_selection_mode", "javaSelectionMode", "selection_mode", "selectionMode"),
            SelectedJavaId = ReadFirstNonEmpty(arguments, "selected_java_id", "selectedJavaId", "java_id", "javaId"),
            SelectedJavaPath = ReadFirstNonEmpty(arguments, "selected_java_path", "selectedJavaPath", "java_path", "javaPath"),
            ClearSelectedJava = ReadBoolean(arguments, "clear_selected_java", "clearSelectedJava"),
            HasCustomJvmArguments = TryReadString(arguments, out var customJvmArguments, "custom_jvm_arguments", "customJvmArguments"),
            CustomJvmArguments = customJvmArguments,
            GarbageCollectorMode = ReadFirstNonEmpty(arguments, "garbage_collector_mode", "garbageCollectorMode", "gc_mode", "gcMode"),
            AutoMemoryAllocation = ReadNullableBoolean(arguments, "auto_memory_allocation", "autoMemoryAllocation"),
            InitialHeapMemoryGb = ReadNullableDouble(arguments, "initial_heap_memory_gb", "initialHeapMemoryGb"),
            MaximumHeapMemoryGb = ReadNullableDouble(arguments, "maximum_heap_memory_gb", "maximumHeapMemoryGb"),
            WindowWidth = ReadNullableInt32(arguments, "window_width", "windowWidth"),
            WindowHeight = ReadNullableInt32(arguments, "window_height", "windowHeight"),
            GameDirectoryMode = ReadFirstNonEmpty(arguments, "game_directory_mode", "gameDirectoryMode", "game_isolation_mode", "gameIsolationMode", "mode_key", "modeKey"),
            HasCustomGameDirectoryPath = TryReadString(arguments, out var customGameDirectoryPath, "custom_game_directory_path", "customGameDirectoryPath", "game_directory_custom_path", "gameDirectoryCustomPath", "custom_path", "customPath"),
            CustomGameDirectoryPath = customGameDirectoryPath,
        }, cancellationToken);
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

    private static bool ReadBoolean(JObject arguments, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (arguments.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var token)
                && token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }
        }

        return false;
    }

    private static bool? ReadNullableBoolean(JObject arguments, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (arguments.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var token)
                && token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }
        }

        return null;
    }

    private static bool TryReadString(JObject arguments, out string? value, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (arguments.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var token)
                && token.Type != JTokenType.Null)
            {
                value = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
                return true;
            }
        }

        value = null;
        return false;
    }

    private static double? ReadNullableDouble(JObject arguments, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!arguments.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var token)
                || token.Type == JTokenType.Null)
            {
                continue;
            }

            if (token.Type is JTokenType.Float or JTokenType.Integer)
            {
                return token.Value<double>();
            }

            if (double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue)
                || double.TryParse(token.ToString(), out invariantValue))
            {
                return invariantValue;
            }
        }

        return null;
    }

    private static int? ReadNullableInt32(JObject arguments, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!arguments.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var token)
                || token.Type == JTokenType.Null)
            {
                continue;
            }

            if (token.Type == JTokenType.Integer)
            {
                return token.Value<int>();
            }

            if (int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var invariantValue)
                || int.TryParse(token.ToString(), out invariantValue))
            {
                return invariantValue;
            }
        }

        return null;
    }
}

public sealed class PatchGlobalLaunchSettingsActionHandler : IAgentActionHandler
{
    private readonly IAgentSettingsWriteService _settingsWriteService;

    public PatchGlobalLaunchSettingsActionHandler(IAgentSettingsWriteService settingsWriteService)
    {
        _settingsWriteService = settingsWriteService;
    }

    public string ActionType => PatchGlobalLaunchSettingsToolHandler.ToolNameValue;

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        return _settingsWriteService.ExecuteGlobalLaunchSettingsPatchAsync(proposal, cancellationToken);
    }
}

public sealed class PatchInstanceLaunchSettingsToolHandler : IAgentToolHandler
{
    public const string ToolNameValue = "patchInstanceLaunchSettings";

    private readonly IAgentSettingsWriteService _settingsWriteService;

    public PatchInstanceLaunchSettingsToolHandler(IAgentSettingsWriteService settingsWriteService)
    {
        _settingsWriteService = settingsWriteService;
    }

    public string ToolName => ToolNameValue;

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "修改实例启动设置的确认式 patch 工具。调用前应先用 get_instances 确认实例，再读取 getVersionConfig；只有需要解释最终生效值时才补 getEffectiveLaunchSettings。优先使用 target_version_name 和 java_id，不要优先传绝对路径。本工具只生成确认提案，不会直接写入。实例设置遵循 UI 的扁平语义：use_global_settings_overall 是唯一总开关；设置 Java/JVM/GC/内存/分辨率/游戏目录等实例字段时，若当前仍使用全局设置，工具会自动关闭该总开关。当前支持字段：use_global_settings_overall、java_id、java_path、custom_jvm_arguments、garbage_collector_mode、auto_memory_allocation、initial_heap_memory_gb、maximum_heap_memory_gb、window_width、window_height、game_directory_mode、custom_game_directory_path。",
        new
        {
            type = "object",
            properties = new
            {
                target_version_name = new { type = "string", description = "目标实例名，优先使用 get_instances 返回的 target_version_name。" },
                target_version_path = new { type = "string", description = "可选。get_instances 返回的 version_directory_path；若提供，启动器会校验它与 target_version_name 是否一致。" },
                use_global_settings_overall = new { type = "boolean", description = "可选。对应 UI 的“使用全局设置”总开关。true 表示整套实例启动设置改为跟随全局；false 表示切到实例本地设置。" },
                java_id = new { type = "string", description = "可选。checkJavaVersions 返回的 java_id，例如 java_2。设置该字段时，若当前仍使用全局设置，工具会自动关闭 use_global_settings_overall。" },
                java_path = new { type = "string", description = "可选。实例 Java 的绝对路径。优先推荐使用 java_id。设置该字段时，若当前仍使用全局设置，工具会自动关闭 use_global_settings_overall。" },
                custom_jvm_arguments = new { type = "string", description = "可选。实例自定义 JVM 参数；传空字符串表示清空。设置该字段时，若当前仍使用全局设置，工具会自动关闭 use_global_settings_overall。" },
                garbage_collector_mode = new { type = "string", description = "可选。实例垃圾回收器模式。设置该字段时，若当前仍使用全局设置，工具会自动关闭 use_global_settings_overall。", @enum = new[] { "Auto", "G1GC", "ZGC", "ParallelGC", "SerialGC" } },
                auto_memory_allocation = new { type = "boolean", description = "可选。实例本地内存是否自动分配。设置该字段时，若当前仍使用全局设置，工具会自动关闭 use_global_settings_overall。" },
                initial_heap_memory_gb = new { type = "number", description = "可选。实例初始堆内存，单位 GB，范围 1-64。设置该字段时，若当前仍使用全局设置，工具会自动关闭 use_global_settings_overall；当 auto_memory_allocation=true 时不允许修改。" },
                maximum_heap_memory_gb = new { type = "number", description = "可选。实例最大堆内存，单位 GB，范围 1-64。设置该字段时，若当前仍使用全局设置，工具会自动关闭 use_global_settings_overall；当 auto_memory_allocation=true 时不允许修改。" },
                window_width = new { type = "integer", description = "可选。实例窗口宽度，范围 800-4096。设置该字段时，若当前仍使用全局设置，工具会自动关闭 use_global_settings_overall。" },
                window_height = new { type = "integer", description = "可选。实例窗口高度，范围 600-2160。设置该字段时，若当前仍使用全局设置，工具会自动关闭 use_global_settings_overall。" },
                game_directory_mode = new { type = "string", description = "可选。实例游戏目录模式。follow_global 表示该分项跟随全局，其余表示保存实例级目录模式。设置该字段时，若当前仍使用全局设置，工具会自动关闭 use_global_settings_overall。", @enum = new[] { "follow_global", "default", "version_isolation", "custom" } },
                custom_game_directory_path = new { type = "string", description = "可选。实例自定义游戏目录绝对路径；仅当 game_directory_mode=custom 时允许设置。传空字符串可清空已保存的自定义目录。" }
            },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        return _settingsWriteService.PrepareInstanceLaunchSettingsPatchAsync(new AgentInstanceLaunchSettingsPatchRequest
        {
            TargetVersionName = ReadFirstNonEmpty(arguments, "target_version_name", "targetVersionName"),
            TargetVersionPath = ReadFirstNonEmpty(arguments, "target_version_path", "targetVersionPath", "version_directory_path", "versionDirectoryPath"),
            UseGlobalSettingsOverall = ReadNullableBoolean(arguments, "use_global_settings_overall", "uses_global_settings_overall", "useGlobalSettingsOverall", "usesGlobalSettingsOverall"),
            UseGlobalJavaSetting = ReadNullableBoolean(arguments, "use_global_java_setting", "useGlobalJavaSetting"),
            JavaId = ReadFirstNonEmpty(arguments, "java_id", "javaId", "selected_java_id", "selectedJavaId"),
            JavaPath = ReadFirstNonEmpty(arguments, "java_path", "javaPath", "selected_java_path", "selectedJavaPath"),
            HasCustomJvmArguments = TryReadString(arguments, out var customJvmArguments, "custom_jvm_arguments", "customJvmArguments"),
            CustomJvmArguments = customJvmArguments,
            GarbageCollectorMode = ReadFirstNonEmpty(arguments, "garbage_collector_mode", "garbageCollectorMode", "gc_mode", "gcMode"),
            OverrideMemory = ReadNullableBoolean(arguments, "override_memory", "overrideMemory"),
            AutoMemoryAllocation = ReadNullableBoolean(arguments, "auto_memory_allocation", "autoMemoryAllocation"),
            InitialHeapMemoryGb = ReadNullableDouble(arguments, "initial_heap_memory_gb", "initialHeapMemoryGb"),
            MaximumHeapMemoryGb = ReadNullableDouble(arguments, "maximum_heap_memory_gb", "maximumHeapMemoryGb"),
            OverrideResolution = ReadNullableBoolean(arguments, "override_resolution", "overrideResolution"),
            WindowWidth = ReadNullableInt32(arguments, "window_width", "windowWidth"),
            WindowHeight = ReadNullableInt32(arguments, "window_height", "windowHeight"),
            GameDirectoryMode = ReadFirstNonEmpty(arguments, "game_directory_mode", "gameDirectoryMode", "game_isolation_mode", "gameIsolationMode", "mode_key", "modeKey", "local_mode", "localMode"),
            HasCustomGameDirectoryPath = TryReadString(arguments, out var customGameDirectoryPath, "custom_game_directory_path", "customGameDirectoryPath", "game_directory_custom_path", "gameDirectoryCustomPath", "local_custom_path", "localCustomPath", "custom_path", "customPath"),
            CustomGameDirectoryPath = customGameDirectoryPath,
        }, cancellationToken);
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

    private static bool? ReadNullableBoolean(JObject arguments, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (arguments.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var token)
                && token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }
        }

        return null;
    }

    private static bool TryReadString(JObject arguments, out string? value, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (arguments.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var token)
                && token.Type != JTokenType.Null)
            {
                value = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
                return true;
            }
        }

        value = null;
        return false;
    }

    private static double? ReadNullableDouble(JObject arguments, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!arguments.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var token)
                || token.Type == JTokenType.Null)
            {
                continue;
            }

            if (token.Type is JTokenType.Float or JTokenType.Integer)
            {
                return token.Value<double>();
            }

            if (double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue)
                || double.TryParse(token.ToString(), out invariantValue))
            {
                return invariantValue;
            }
        }

        return null;
    }

    private static int? ReadNullableInt32(JObject arguments, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!arguments.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var token)
                || token.Type == JTokenType.Null)
            {
                continue;
            }

            if (token.Type == JTokenType.Integer)
            {
                return token.Value<int>();
            }

            if (int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var invariantValue)
                || int.TryParse(token.ToString(), out invariantValue))
            {
                return invariantValue;
            }
        }

        return null;
    }
}

public sealed class PatchInstanceLaunchSettingsActionHandler : IAgentActionHandler
{
    private readonly IAgentSettingsWriteService _settingsWriteService;

    public PatchInstanceLaunchSettingsActionHandler(IAgentSettingsWriteService settingsWriteService)
    {
        _settingsWriteService = settingsWriteService;
    }

    public string ActionType => PatchInstanceLaunchSettingsToolHandler.ToolNameValue;

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        return _settingsWriteService.ExecuteInstanceLaunchSettingsPatchAsync(proposal, cancellationToken);
    }
}

public sealed class AgentGlobalLaunchSettingsPatchRequest
{
    public string? JavaSelectionMode { get; init; }

    public string? SelectedJavaId { get; init; }

    public string? SelectedJavaPath { get; init; }

    public bool ClearSelectedJava { get; init; }

    public bool HasCustomJvmArguments { get; init; }

    public string? CustomJvmArguments { get; init; }

    public string? GarbageCollectorMode { get; init; }

    public bool? AutoMemoryAllocation { get; init; }

    public double? InitialHeapMemoryGb { get; init; }

    public double? MaximumHeapMemoryGb { get; init; }

    public int? WindowWidth { get; init; }

    public int? WindowHeight { get; init; }

    public string? GameDirectoryMode { get; init; }

    public bool HasCustomGameDirectoryPath { get; init; }

    public string? CustomGameDirectoryPath { get; init; }
}

public sealed class AgentInstanceLaunchSettingsPatchRequest
{
    public string? TargetVersionName { get; init; }

    public string? TargetVersionPath { get; init; }

    public bool? UseGlobalSettingsOverall { get; init; }

    public bool? UseGlobalJavaSetting { get; init; }

    public string? JavaId { get; init; }

    public string? JavaPath { get; init; }

    public bool HasCustomJvmArguments { get; init; }

    public string? CustomJvmArguments { get; init; }

    public string? GarbageCollectorMode { get; init; }

    public bool? OverrideMemory { get; init; }

    public bool? AutoMemoryAllocation { get; init; }

    public double? InitialHeapMemoryGb { get; init; }

    public double? MaximumHeapMemoryGb { get; init; }

    public bool? OverrideResolution { get; init; }

    public int? WindowWidth { get; init; }

    public int? WindowHeight { get; init; }

    public string? GameDirectoryMode { get; init; }

    public bool HasCustomGameDirectoryPath { get; init; }

    public string? CustomGameDirectoryPath { get; init; }
}