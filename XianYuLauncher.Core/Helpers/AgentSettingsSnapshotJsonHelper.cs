using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

public static class AgentSettingsSnapshotJsonHelper
{
    private const string CurrentPathFallbackName = "当前活动目录";

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
        var normalizedSelectedPath = NullIfWhiteSpace(selectedJavaPath);
        var orderedJavaVersions = javaVersions
            .OrderByDescending(javaVersion => IsSelectedJavaPath(normalizedSelectedPath, javaVersion.Path))
            .ThenByDescending(javaVersion => javaVersion.MajorVersion)
            .ThenByDescending(javaVersion => javaVersion.IsJDK)
            .ThenBy(javaVersion => javaVersion.FullVersion, StringComparer.OrdinalIgnoreCase)
            .ThenBy(javaVersion => javaVersion.Path, StringComparer.OrdinalIgnoreCase)
            .Select((javaVersion, index) => new
            {
                java_id = $"java_{index + 1}",
                path = javaVersion.Path,
                major_version = javaVersion.MajorVersion,
                full_version = javaVersion.FullVersion,
                is_jdk = javaVersion.IsJDK,
                is_64_bit = javaVersion.Is64Bit,
                matches_selected_java_path = IsSelectedJavaPath(normalizedSelectedPath, javaVersion.Path)
            })
            .ToList();
        var payload = new
        {
            refresh_requested = refresh,
            data_source = fromSettingsCache ? "settings_cache" : "runtime_scan",
            java_selection_mode = NormalizeJavaSelectionMode(selectionMode),
            selected_java_path = normalizedSelectedPath,
            selected_java_present_in_list = orderedJavaVersions.Any(javaVersion => javaVersion.matches_selected_java_path),
            total_count = orderedJavaVersions.Count,
            java_versions = orderedJavaVersions
        };

        return JsonConvert.SerializeObject(payload, Formatting.Indented);
    }

    public static string BuildMinecraftPathsSnapshotJson(string currentMinecraftPath, string? pathsJson)
    {
        var storedPaths = TryDeserializeMinecraftPaths(pathsJson);
        var normalizedPaths = NormalizeMinecraftPaths(storedPaths, currentMinecraftPath);
        var activePath = normalizedPaths.First(path => path.is_active);
        var payload = new
        {
            current_minecraft_path = currentMinecraftPath,
            active_path_id = activePath.path_id,
            active_path = activePath.path,
            total_count = normalizedPaths.Count,
            minecraft_paths = normalizedPaths
        };

        return JsonConvert.SerializeObject(payload, Formatting.Indented);
    }

    private static string NormalizeJavaSelectionMode(string? rawValue)
    {
        return string.Equals(rawValue, "Manual", StringComparison.OrdinalIgnoreCase) ? "manual" : "auto";
    }

    private static bool IsSelectedJavaPath(string? selectedJavaPath, string javaPath)
    {
        return !string.IsNullOrWhiteSpace(selectedJavaPath)
            && string.Equals(selectedJavaPath, javaPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static List<MinecraftPathSnapshot> NormalizeMinecraftPaths(
        IReadOnlyList<StoredMinecraftPath> storedPaths,
        string currentMinecraftPath)
    {
        var paths = storedPaths
            .Where(path => !string.IsNullOrWhiteSpace(path.Path))
            .ToList();

        if (paths.Count == 0)
        {
            paths.Add(new StoredMinecraftPath
            {
                Name = CurrentPathFallbackName,
                Path = currentMinecraftPath,
                IsActive = true
            });
        }

        var activePath = paths.FirstOrDefault(path => path.IsActive)
            ?? paths.FirstOrDefault(path => string.Equals(path.Path, currentMinecraftPath, StringComparison.OrdinalIgnoreCase));

        if (activePath is null)
        {
            paths.Insert(0, new StoredMinecraftPath
            {
                Name = CurrentPathFallbackName,
                Path = currentMinecraftPath,
                IsActive = true
            });
            activePath = paths[0];
        }

        return paths
            .Select((path, index) => new MinecraftPathSnapshot(
                $"mcdir_{index + 1}",
                string.IsNullOrWhiteSpace(path.Name) ? $"Minecraft 目录 {index + 1}" : path.Name,
                path.Path,
                string.Equals(path.Path, activePath.Path, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static IReadOnlyList<StoredMinecraftPath> TryDeserializeMinecraftPaths(string? pathsJson)
    {
        if (string.IsNullOrWhiteSpace(pathsJson))
        {
            return [];
        }

        try
        {
            var token = JToken.Parse(pathsJson);
            if (token.Type == JTokenType.String)
            {
                var nestedJson = token.Value<string>();
                if (string.IsNullOrWhiteSpace(nestedJson))
                {
                    return [];
                }

                token = JToken.Parse(nestedJson);
            }

            return token.Type == JTokenType.Array
                ? token.ToObject<List<StoredMinecraftPath>>() ?? []
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class StoredMinecraftPath
    {
        public string Name { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }

    private sealed record MinecraftPathSnapshot(string path_id, string name, string path, bool is_active);
}