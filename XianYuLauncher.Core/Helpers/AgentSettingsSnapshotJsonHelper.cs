using Newtonsoft.Json;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

public sealed class AgentGlobalSettingsSnapshotInput
{
    public bool AutoMemoryAllocation { get; init; } = true;

    public double InitialHeapMemory { get; init; } = 6.0;

    public double MaximumHeapMemory { get; init; } = 12.0;

    public string CustomJvmArguments { get; init; } = string.Empty;

    public string GarbageCollectorMode { get; init; } = GarbageCollectorModeHelper.Auto;

    public int WindowWidth { get; init; } = 1280;

    public int WindowHeight { get; init; } = 720;

    public string? JavaSelectionMode { get; init; }

    public string? SelectedJavaPath { get; init; }

    public IReadOnlyList<JavaVersion> KnownJavaVersions { get; init; } = [];

    public string? GameIsolationModeKey { get; init; }

    public bool LegacyEnableVersionIsolation { get; init; }

    public string? CustomGameDirectoryPath { get; init; }

    public string CurrentMinecraftPath { get; init; } = string.Empty;
}

public sealed class AgentEffectiveSettingsSnapshotInput
{
    public string VersionId { get; init; } = string.Empty;

    public string MinecraftRootPath { get; init; } = string.Empty;

    public string VersionDirectoryPath { get; init; } = string.Empty;

    public VersionConfig Config { get; init; } = new();

    public EffectiveLaunchSettings EffectiveSettings { get; init; } = new();

    public int RequiredJavaVersion { get; init; } = 8;

    public bool RequiredJavaVersionFromVersionInfo { get; init; }

    public string ResolvedGameDirectory { get; init; } = string.Empty;

    public string? GlobalGameIsolationModeKey { get; init; }

    public bool GlobalLegacyEnableVersionIsolation { get; init; }

    public string? GlobalCustomGameDirectoryPath { get; init; }
}

public static class AgentSettingsSnapshotJsonHelper
{
    public static string BuildVersionConfigSnapshotJson(
        string versionId,
        string minecraftRootPath,
        string versionDirectoryPath,
        VersionConfig config)
    {
        var usesGlobalSettingsOverall = config.UseGlobalJavaSetting
            && !config.OverrideMemory
            && !config.OverrideResolution;
        var payload = new
        {
            version_id = versionId,
            minecraft_root_path = minecraftRootPath,
            version_directory_path = versionDirectoryPath,
            minecraft_version = config.MinecraftVersion,
            modloader = new
            {
                type = string.IsNullOrWhiteSpace(config.ModLoaderType) ? "vanilla" : config.ModLoaderType,
                version = NullIfWhiteSpace(config.ModLoaderVersion),
                optifine_version = NullIfWhiteSpace(config.OptifineVersion),
                liteloader_version = NullIfWhiteSpace(config.LiteLoaderVersion)
            },
            uses_global_settings_overall = usesGlobalSettingsOverall,
            java_settings = new
            {
                follows_global = config.UseGlobalJavaSetting,
                local_java_path = NullIfWhiteSpace(config.JavaPath),
                local_java_path_configured = !string.IsNullOrWhiteSpace(config.JavaPath)
            },
            memory_settings = new
            {
                follows_global = !config.OverrideMemory,
                local_override_enabled = config.OverrideMemory,
                local_auto_manage = config.AutoMemoryAllocation,
                local_initial_memory_gb = config.InitialHeapMemory,
                local_max_memory_gb = config.MaximumHeapMemory
            },
            jvm_settings = new
            {
                follows_global = usesGlobalSettingsOverall,
                local_custom_jvm_arguments = config.CustomJvmArguments ?? string.Empty,
                local_custom_jvm_arguments_configured = !string.IsNullOrWhiteSpace(config.CustomJvmArguments),
                local_garbage_collector_mode = GarbageCollectorModeHelper.Normalize(config.GarbageCollectorMode)
            },
            resolution_settings = new
            {
                follows_global = !config.OverrideResolution,
                local_override_enabled = config.OverrideResolution,
                local_window_width = config.WindowWidth,
                local_window_height = config.WindowHeight
            },
            game_directory_settings = new
            {
                follows_global = usesGlobalSettingsOverall || string.IsNullOrWhiteSpace(config.GameDirMode),
                local_mode = NullIfWhiteSpace(config.GameDirMode),
                local_custom_path = NullIfWhiteSpace(config.GameDirCustomPath),
                local_custom_path_configured = !string.IsNullOrWhiteSpace(config.GameDirCustomPath),
                local_override_ignored_by_use_global_settings = usesGlobalSettingsOverall && !string.IsNullOrWhiteSpace(config.GameDirMode)
            }
        };

        return JsonConvert.SerializeObject(payload, Formatting.Indented);
    }

    public static string BuildJavaVersionsSnapshotJson(
        bool refresh,
        string? selectionMode,
        string? selectedJavaPath,
        IReadOnlyList<JavaVersion> javaVersions,
        bool fromSettingsCache)
    {
        var normalizedSelectionMode = NormalizeJavaSelectionMode(selectionMode);
        var normalizedSelectedPath = NullIfWhiteSpace(selectedJavaPath);
        var ignoreSelectedJavaPathForChanges = ShouldIgnoreSelectedJavaPathForChanges(normalizedSelectionMode);
        var orderedJavaVersions = AgentJavaInventoryHelper.NormalizeJavaVersions(normalizedSelectedPath, javaVersions)
            .Select(javaVersion => new
            {
                java_id = javaVersion.JavaId,
                path = javaVersion.Path,
                major_version = javaVersion.MajorVersion,
                full_version = javaVersion.FullVersion,
                is_jdk = javaVersion.IsJdk,
                is_64_bit = javaVersion.Is64Bit,
                matches_selected_java_path = javaVersion.MatchesSelectedJavaPath
            })
            .ToList();
        var payload = new
        {
            refresh_requested = refresh,
            data_source = fromSettingsCache ? "settings_cache" : "runtime_scan",
            java_selection_mode = normalizedSelectionMode,
            selected_java_path = normalizedSelectedPath,
            selected_java_path_interpretation = DescribeSelectedJavaPathInterpretation(normalizedSelectionMode),
            ignore_selected_java_path_for_changes = ignoreSelectedJavaPathForChanges,
            selected_java_path_guidance = DescribeSelectedJavaPathGuidance(normalizedSelectionMode, normalizedSelectedPath),
            selected_java_present_in_list = orderedJavaVersions.Any(javaVersion => javaVersion.matches_selected_java_path),
            total_count = orderedJavaVersions.Count,
            java_versions = orderedJavaVersions
        };

        return JsonConvert.SerializeObject(payload, Formatting.Indented);
    }

    public static string BuildMinecraftPathsSnapshotJson(string currentMinecraftPath, string? pathsJson)
    {
        var normalizedPaths = AgentMinecraftPathHelper.NormalizePaths(currentMinecraftPath, pathsJson);
        var activePath = normalizedPaths.First(path => path.IsActive);
        var payload = new
        {
            current_minecraft_path = currentMinecraftPath,
            active_path_id = activePath.PathId,
            active_path = activePath.Path,
            total_count = normalizedPaths.Count,
            minecraft_paths = normalizedPaths.Select(path => new
            {
                path_id = path.PathId,
                name = path.Name,
                path = path.Path,
                is_active = path.IsActive,
            })
        };

        return JsonConvert.SerializeObject(payload, Formatting.Indented);
    }

    public static string BuildGlobalLaunchSettingsSnapshotJson(AgentGlobalSettingsSnapshotInput input)
    {
        var normalizedSelectionMode = NormalizeJavaSelectionMode(input.JavaSelectionMode);
        var normalizedSelectedPath = NullIfWhiteSpace(input.SelectedJavaPath);
        var ignoreSelectedJavaPathForChanges = ShouldIgnoreSelectedJavaPathForChanges(normalizedSelectionMode);
        var matchedSelectedJava = AgentJavaInventoryHelper.NormalizeJavaVersions(normalizedSelectedPath, input.KnownJavaVersions)
            .FirstOrDefault(javaVersion => javaVersion.MatchesSelectedJavaPath);
        var effectiveGameIsolationModeKey = ResolveEffectiveGlobalGameDirModeKey(input.GameIsolationModeKey, input.LegacyEnableVersionIsolation);

        var payload = new
        {
            java_settings = new
            {
                selection_mode = normalizedSelectionMode,
                selected_java_path = normalizedSelectedPath,
                selected_java_path_interpretation = DescribeSelectedJavaPathInterpretation(normalizedSelectionMode),
                ignore_selected_java_path_for_changes = ignoreSelectedJavaPathForChanges,
                selected_java_path_guidance = DescribeSelectedJavaPathGuidance(normalizedSelectionMode, normalizedSelectedPath),
                selected_java_present_in_known_list = matchedSelectedJava != null,
                selected_java = matchedSelectedJava == null
                    ? null
                    : new
                    {
                        path = matchedSelectedJava.Path,
                        major_version = matchedSelectedJava.MajorVersion,
                        full_version = matchedSelectedJava.FullVersion,
                        is_jdk = matchedSelectedJava.IsJdk,
                        is_64_bit = matchedSelectedJava.Is64Bit,
                    },
                known_java_versions_count = input.KnownJavaVersions.Count,
            },
            memory_settings = new
            {
                auto_manage = input.AutoMemoryAllocation,
                initial_memory_gb = input.InitialHeapMemory,
                max_memory_gb = input.MaximumHeapMemory,
            },
            jvm_settings = new
            {
                custom_jvm_arguments = input.CustomJvmArguments,
                custom_jvm_arguments_configured = !string.IsNullOrWhiteSpace(input.CustomJvmArguments),
                garbage_collector_mode = GarbageCollectorModeHelper.Normalize(input.GarbageCollectorMode),
            },
            resolution_settings = new
            {
                window_width = input.WindowWidth,
                window_height = input.WindowHeight,
            },
            game_directory_settings = new
            {
                mode_key = effectiveGameIsolationModeKey,
                mode = NormalizeGameIsolationMode(effectiveGameIsolationModeKey),
                custom_path = NullIfWhiteSpace(input.CustomGameDirectoryPath),
                custom_path_configured = !string.IsNullOrWhiteSpace(input.CustomGameDirectoryPath),
                legacy_enable_version_isolation = input.LegacyEnableVersionIsolation,
                current_minecraft_path = input.CurrentMinecraftPath,
            }
        };

        return JsonConvert.SerializeObject(payload, Formatting.Indented);
    }

    public static string BuildEffectiveLaunchSettingsSnapshotJson(AgentEffectiveSettingsSnapshotInput input)
    {
        var config = input.Config;
        var effectiveSettings = input.EffectiveSettings;
        var usesGlobalSettingsOverall = config.UseGlobalJavaSetting
            && !config.OverrideMemory
            && !config.OverrideResolution;
        var effectiveGlobalModeKey = ResolveEffectiveGlobalGameDirModeKey(input.GlobalGameIsolationModeKey, input.GlobalLegacyEnableVersionIsolation);

        var payload = new
        {
            version_id = input.VersionId,
            minecraft_root_path = input.MinecraftRootPath,
            version_directory_path = input.VersionDirectoryPath,
            uses_global_settings_overall = usesGlobalSettingsOverall,
            required_java_version = input.RequiredJavaVersion,
            required_java_version_source = input.RequiredJavaVersionFromVersionInfo ? "version_manifest" : "default_fallback",
            java_settings = new
            {
                follows_global = config.UseGlobalJavaSetting,
                local_java_path = NullIfWhiteSpace(config.JavaPath),
                effective_java_path = NullIfWhiteSpace(effectiveSettings.JavaPath),
                effective_value_source = ResolveEffectiveJavaSource(config),
            },
            memory_settings = new
            {
                follows_global = !config.OverrideMemory,
                local_override_enabled = config.OverrideMemory,
                effective_auto_manage = effectiveSettings.AutoMemoryAllocation,
                effective_initial_memory_gb = effectiveSettings.InitialHeapMemory,
                effective_max_memory_gb = effectiveSettings.MaximumHeapMemory,
                effective_value_source = config.OverrideMemory ? "local" : "global",
            },
            jvm_settings = new
            {
                follows_global = usesGlobalSettingsOverall,
                local_custom_jvm_arguments = config.CustomJvmArguments ?? string.Empty,
                local_garbage_collector_mode = GarbageCollectorModeHelper.Normalize(config.GarbageCollectorMode),
                effective_custom_jvm_arguments = effectiveSettings.CustomJvmArguments,
                effective_custom_jvm_arguments_configured = !string.IsNullOrWhiteSpace(effectiveSettings.CustomJvmArguments),
                effective_garbage_collector_mode = GarbageCollectorModeHelper.Normalize(effectiveSettings.GarbageCollectorMode),
                effective_value_source = usesGlobalSettingsOverall ? "global" : "local",
            },
            resolution_settings = new
            {
                follows_global = !config.OverrideResolution,
                local_override_enabled = config.OverrideResolution,
                effective_window_width = effectiveSettings.WindowWidth,
                effective_window_height = effectiveSettings.WindowHeight,
                effective_value_source = config.OverrideResolution ? "local" : "global",
            },
            game_directory_settings = new
            {
                follows_global = usesGlobalSettingsOverall || string.IsNullOrWhiteSpace(config.GameDirMode),
                local_mode = NullIfWhiteSpace(config.GameDirMode),
                local_custom_path = NullIfWhiteSpace(config.GameDirCustomPath),
                global_mode_key = effectiveGlobalModeKey,
                global_mode = NormalizeGameIsolationMode(effectiveGlobalModeKey),
                global_custom_path = NullIfWhiteSpace(input.GlobalCustomGameDirectoryPath),
                effective_game_directory = input.ResolvedGameDirectory,
                effective_value_source = ResolveEffectiveGameDirectorySource(usesGlobalSettingsOverall, config.GameDirMode, effectiveGlobalModeKey),
            }
        };

        return JsonConvert.SerializeObject(payload, Formatting.Indented);
    }

    private static string NormalizeJavaSelectionMode(string? rawValue)
    {
        return string.Equals(rawValue, "Manual", StringComparison.OrdinalIgnoreCase) ? "manual" : "auto";
    }

    private static bool ShouldIgnoreSelectedJavaPathForChanges(string normalizedSelectionMode)
    {
        return string.Equals(normalizedSelectionMode, "auto", StringComparison.Ordinal);
    }

    private static string DescribeSelectedJavaPathInterpretation(string normalizedSelectionMode)
    {
        return ShouldIgnoreSelectedJavaPathForChanges(normalizedSelectionMode)
            ? "auto_detected_result"
            : "manual_selection";
    }

    private static string DescribeSelectedJavaPathGuidance(string normalizedSelectionMode, string? selectedJavaPath)
    {
        if (ShouldIgnoreSelectedJavaPathForChanges(normalizedSelectionMode))
        {
            return string.IsNullOrWhiteSpace(selectedJavaPath)
                ? "java_selection_mode=auto。当前没有暴露可复用的 selected_java_path；修改 Java 设置时应忽略路径，直接使用 java_selection_mode，或先调用 checkJavaVersions 后使用 java_id。"
                : "java_selection_mode=auto。selected_java_path 仅表示当前自动匹配结果，不代表用户手动选择；修改 Java 设置时应忽略该路径，直接使用 java_selection_mode，或先调用 checkJavaVersions 后使用 java_id。";
        }

        return string.IsNullOrWhiteSpace(selectedJavaPath)
            ? "java_selection_mode=manual，但当前尚未保存有效的 selected_java_path。"
            : "java_selection_mode=manual。selected_java_path 表示用户当前保存的手动 Java 选择。";
    }

    private static string NormalizeGameIsolationMode(string? modeKey)
    {
        if (string.Equals(modeKey, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            return "custom";
        }

        if (string.Equals(modeKey, "VersionIsolation", StringComparison.OrdinalIgnoreCase))
        {
            return "version_isolation";
        }

        return "default";
    }

    private static string ResolveEffectiveGlobalGameDirModeKey(string? rawModeKey, bool legacyEnableVersionIsolation)
    {
        if (!string.IsNullOrWhiteSpace(rawModeKey))
        {
            return rawModeKey;
        }

        return legacyEnableVersionIsolation ? "VersionIsolation" : "Default";
    }

    private static string ResolveEffectiveJavaSource(VersionConfig config)
    {
        if (!config.UseGlobalJavaSetting && !string.IsNullOrWhiteSpace(config.JavaPath))
        {
            return "local";
        }

        if (!config.UseGlobalJavaSetting)
        {
            return "global_fallback";
        }

        return "global";
    }

    private static string ResolveEffectiveGameDirectorySource(bool usesGlobalSettingsOverall, string? localMode, string globalModeKey)
    {
        if (usesGlobalSettingsOverall || string.IsNullOrWhiteSpace(localMode))
        {
            return $"global_{NormalizeGameIsolationMode(globalModeKey)}";
        }

        return $"local_{NormalizeGameIsolationMode(localMode)}";
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

}