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
        "修改全局启动设置的 patch 工具。当前 Phase 5.4 支持 Java、JVM/GC 与内存字段：java_selection_mode、selected_java_id、selected_java_path、clear_selected_java、custom_jvm_arguments、garbage_collector_mode、auto_memory_allocation、initial_heap_memory_gb、maximum_heap_memory_gb。调用前建议先用 getGlobalLaunchSettings 和 checkJavaVersions。",
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
                maximum_heap_memory_gb = new { type = "number", description = "可选。全局最大堆内存，单位 GB，范围 1-64；仅当 auto_memory_allocation=false 时允许修改。" }
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
        "修改实例启动设置的 patch 工具。当前 Phase 5.4 支持 Java、JVM/GC 与内存字段：use_global_java_setting、java_id、java_path、custom_jvm_arguments、garbage_collector_mode、override_memory、auto_memory_allocation、initial_heap_memory_gb、maximum_heap_memory_gb。实例定位优先使用 get_instances 返回的 target_version_name，必要时再传 target_version_path。",
        new
        {
            type = "object",
            properties = new
            {
                target_version_name = new { type = "string", description = "目标实例名，优先使用 get_instances 返回的 target_version_name。" },
                target_version_path = new { type = "string", description = "可选。get_instances 返回的 version_directory_path；若提供，启动器会校验它与 target_version_name 是否一致。" },
                use_global_java_setting = new { type = "boolean", description = "可选。true 表示该实例改为跟随全局 Java；false 表示改为版本独立 Java。" },
                java_id = new { type = "string", description = "可选。checkJavaVersions 返回的 java_id，例如 java_2。设置后会自动切换为实例独立 Java。" },
                java_path = new { type = "string", description = "可选。实例独立 Java 的绝对路径。优先推荐使用 java_id。" },
                custom_jvm_arguments = new { type = "string", description = "可选。实例自定义 JVM 参数；传空字符串表示清空。若实例当前仍跟随全局，保存后会在脱离全局时生效。" },
                garbage_collector_mode = new { type = "string", description = "可选。实例垃圾回收器模式。若实例当前仍跟随全局，保存后会在脱离全局时生效。", @enum = new[] { "Auto", "G1GC", "ZGC", "ParallelGC", "SerialGC" } },
                override_memory = new { type = "boolean", description = "可选。true 表示实例使用独立内存设置；false 表示实例内存改为跟随全局。" },
                auto_memory_allocation = new { type = "boolean", description = "可选。实例独立内存是否自动分配。仅当 override_memory=true 时允许修改。" },
                initial_heap_memory_gb = new { type = "number", description = "可选。实例初始堆内存，单位 GB，范围 1-64；仅当 override_memory=true 且 auto_memory_allocation=false 时允许修改。" },
                maximum_heap_memory_gb = new { type = "number", description = "可选。实例最大堆内存，单位 GB，范围 1-64；仅当 override_memory=true 且 auto_memory_allocation=false 时允许修改。" }
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
}

public sealed class AgentInstanceLaunchSettingsPatchRequest
{
    public string? TargetVersionName { get; init; }

    public string? TargetVersionPath { get; init; }

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
}