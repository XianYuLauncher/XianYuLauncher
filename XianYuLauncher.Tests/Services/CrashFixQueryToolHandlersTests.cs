using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Services;

public class CrashFixQueryToolHandlersTests
{
    [Fact]
    public Task GetVersionConfigToolHandler_ReturnsStructuredJsonWithDomainFlags()
    {
        const string versionId = "1.21.1-Fabric";
        const string minecraftRootPath = @"D:\Games\.minecraft";
        var expectedVersionDirectory = Path.Combine(minecraftRootPath, MinecraftPathConsts.Versions, versionId);
        var payload = JObject.Parse(AgentSettingsSnapshotJsonHelper.BuildVersionConfigSnapshotJson(
            versionId,
            minecraftRootPath,
            expectedVersionDirectory,
            new VersionConfig
            {
                MinecraftVersion = "1.21.1",
                ModLoaderType = "fabric",
                ModLoaderVersion = "0.16.10",
                ModpackPlatform = "modrinth",
                ModpackProjectId = "fancy-pack",
                ModpackVersionId = "version-42",
                UseGlobalJavaSetting = false,
                JavaPath = @"C:\Java\jdk-21\bin\javaw.exe",
                OverrideMemory = true,
                AutoMemoryAllocation = false,
                InitialHeapMemory = 4,
                MaximumHeapMemory = 10,
                CustomJvmArguments = "-Dfoo=bar",
                GarbageCollectorMode = "ZGC",
                OverrideResolution = false,
                WindowWidth = 1600,
                WindowHeight = 900,
                GameDirMode = "Custom",
                GameDirCustomPath = @"E:\MinecraftData\InstanceA"
            }));

        payload["version_id"]!.Value<string>().Should().Be(versionId);
        payload["version_directory_path"]!.Value<string>().Should().Be(expectedVersionDirectory);
    payload["is_modpack"]!.Value<bool>().Should().BeTrue();
    payload["modpack_platform"]!.Value<string>().Should().Be("modrinth");
    payload["modpack_project_id"]!.Value<string>().Should().Be("fancy-pack");
    payload["modpack_version_id"]!.Value<string>().Should().Be("version-42");
        payload["uses_global_settings_overall"]!.Value<bool>().Should().BeFalse();
        payload["java_settings"]!["follows_global"]!.Value<bool>().Should().BeFalse();
        payload["memory_settings"]!["follows_global"]!.Value<bool>().Should().BeFalse();
        payload["jvm_settings"]!["follows_global"]!.Value<bool>().Should().BeFalse();
        payload["resolution_settings"]!["follows_global"]!.Value<bool>().Should().BeTrue();
        payload["game_directory_settings"]!["follows_global"]!.Value<bool>().Should().BeFalse();
        payload["game_directory_settings"]!["local_override_ignored_by_use_global_settings"]!.Value<bool>().Should().BeFalse();

        return Task.CompletedTask;
    }

    [Fact]
    public Task GetVersionConfigToolHandler_FlagsIgnoredGameDirectoryOverrideWhenOverallGlobalEnabled()
    {
        var payload = JObject.Parse(AgentSettingsSnapshotJsonHelper.BuildVersionConfigSnapshotJson(
            "1.20.4",
            @"C:\Minecraft",
            @"C:\Minecraft\versions\1.20.4",
            new VersionConfig
            {
                MinecraftVersion = "1.20.4",
                ModLoaderType = string.Empty,
                UseGlobalJavaSetting = true,
                OverrideMemory = false,
                OverrideResolution = false,
                CustomJvmArguments = "-Xmx4G",
                GarbageCollectorMode = "G1GC",
                GameDirMode = "Custom",
                GameDirCustomPath = @"F:\IgnoredLocalDir"
            }));

        payload["uses_global_settings_overall"]!.Value<bool>().Should().BeTrue();
        payload["is_modpack"]!.Value<bool>().Should().BeFalse();
        payload["modpack_platform"]!.Type.Should().Be(JTokenType.Null);
        payload["modpack_project_id"]!.Type.Should().Be(JTokenType.Null);
        payload["modpack_version_id"]!.Type.Should().Be(JTokenType.Null);
        payload["jvm_settings"]!["follows_global"]!.Value<bool>().Should().BeTrue();
        payload["game_directory_settings"]!["follows_global"]!.Value<bool>().Should().BeTrue();
        payload["game_directory_settings"]!["local_override_ignored_by_use_global_settings"]!.Value<bool>().Should().BeTrue();

        return Task.CompletedTask;
    }

    [Fact]
    public Task BuildJavaVersionsSnapshotJson_UsesCachedVersionsAndMarksSelectedJava()
    {
        var payload = JObject.Parse(AgentSettingsSnapshotJsonHelper.BuildJavaVersionsSnapshotJson(
            refresh: false,
            selectionMode: "Manual",
            selectedJavaPath: @"C:\Java\jdk-21\bin\javaw.exe",
            javaVersions:
            [
                new() { Path = @"C:\Java\jre-8\bin\javaw.exe", FullVersion = "1.8.0_401", MajorVersion = 8, IsJDK = false, Is64Bit = true },
                new() { Path = @"C:\Java\jdk-21\bin\javaw.exe", FullVersion = "21.0.4", MajorVersion = 21, IsJDK = true, Is64Bit = true }
            ],
            fromSettingsCache: true));

        payload["data_source"]!.Value<string>().Should().Be("settings_cache");
        payload["java_selection_mode"]!.Value<string>().Should().Be("manual");
        payload["selected_java_path_interpretation"]!.Value<string>().Should().Be("manual_selection");
        payload["ignore_selected_java_path_for_changes"]!.Value<bool>().Should().BeFalse();
        payload["selected_java_present_in_list"]!.Value<bool>().Should().BeTrue();

        var javaVersions = (JArray)payload["java_versions"]!;
        javaVersions.Should().HaveCount(2);
        javaVersions[0]!["path"]!.Value<string>().Should().Be(@"C:\Java\jdk-21\bin\javaw.exe");
        javaVersions[0]!["matches_selected_java_path"]!.Value<bool>().Should().BeTrue();

        return Task.CompletedTask;
    }

    [Fact]
    public Task BuildJavaVersionsSnapshotJson_AutoModeMarksSelectedPathAsInformational()
    {
        var payload = JObject.Parse(AgentSettingsSnapshotJsonHelper.BuildJavaVersionsSnapshotJson(
            refresh: false,
            selectionMode: "Auto",
            selectedJavaPath: @"C:\Java\jdk-21\bin\javaw.exe",
            javaVersions:
            [
                new() { Path = @"C:\Java\jdk-21\bin\javaw.exe", FullVersion = "21.0.4", MajorVersion = 21, IsJDK = true, Is64Bit = true }
            ],
            fromSettingsCache: true));

        payload["java_selection_mode"]!.Value<string>().Should().Be("auto");
        payload["selected_java_path_interpretation"]!.Value<string>().Should().Be("auto_detected_result");
        payload["ignore_selected_java_path_for_changes"]!.Value<bool>().Should().BeTrue();
        payload["selected_java_path_guidance"]!.Value<string>().Should().Contain("不代表用户手动选择");
        payload["selected_java_path_guidance"]!.Value<string>().Should().Contain("应忽略该路径");

        return Task.CompletedTask;
    }

    [Fact]
    public Task BuildMinecraftPathsSnapshotJson_ParsesNestedJsonAndMarksCurrentPathAsActive()
    {
        var currentPath = @"E:\Games\.minecraft";
        var nestedJson = JsonConvert.SerializeObject(
            "[{\"Name\":\"主目录\",\"Path\":\"D:\\\\Games\\\\.minecraft\",\"IsActive\":false},{\"Name\":\"备用目录\",\"Path\":\"E:\\\\Games\\\\.minecraft\",\"IsActive\":false}]");
        var payload = JObject.Parse(AgentSettingsSnapshotJsonHelper.BuildMinecraftPathsSnapshotJson(currentPath, nestedJson));

        payload["current_minecraft_path"]!.Value<string>().Should().Be(currentPath);
        payload["active_path"]!.Value<string>().Should().Be(currentPath);

        var paths = (JArray)payload["minecraft_paths"]!;
        paths.Should().HaveCount(2);
        paths.Single(path => string.Equals(path!["path"]!.Value<string>(), currentPath, StringComparison.OrdinalIgnoreCase))!["is_active"]!
            .Value<bool>()
            .Should()
            .BeTrue();

        return Task.CompletedTask;
    }

    [Fact]
    public Task BuildGlobalLaunchSettingsSnapshotJson_UsesLegacyGameDirFallbackAndSelectedJavaDetails()
    {
        var payload = JObject.Parse(AgentSettingsSnapshotJsonHelper.BuildGlobalLaunchSettingsSnapshotJson(new AgentGlobalSettingsSnapshotInput
        {
            AutoMemoryAllocation = true,
            InitialHeapMemory = 6,
            MaximumHeapMemory = 12,
            CustomJvmArguments = "-Dglobal=true",
            GarbageCollectorMode = "G1GC",
            WindowWidth = 1920,
            WindowHeight = 1080,
            JavaSelectionMode = "Manual",
            SelectedJavaPath = @"C:\Java\jdk-21\bin\javaw.exe",
            KnownJavaVersions =
            [
                new JavaVersion { Path = @"C:\Java\jdk-21\bin\javaw.exe", FullVersion = "21.0.4", MajorVersion = 21, IsJDK = true, Is64Bit = true }
            ],
            GameIsolationModeKey = null,
            LegacyEnableVersionIsolation = true,
            CustomGameDirectoryPath = @"D:\CustomGameDir",
            CurrentMinecraftPath = @"D:\Games\.minecraft"
        }));

        payload["java_settings"]!["selection_mode"]!.Value<string>().Should().Be("manual");
        payload["java_settings"]!["selected_java_path_interpretation"]!.Value<string>().Should().Be("manual_selection");
        payload["java_settings"]!["ignore_selected_java_path_for_changes"]!.Value<bool>().Should().BeFalse();
        payload["java_settings"]!["selected_java_present_in_known_list"]!.Value<bool>().Should().BeTrue();
        payload["java_settings"]!["selected_java"]!["major_version"]!.Value<int>().Should().Be(21);
        payload["game_directory_settings"]!["mode_key"]!.Value<string>().Should().Be("VersionIsolation");
        payload["game_directory_settings"]!["mode"]!.Value<string>().Should().Be("version_isolation");
        payload["game_directory_settings"]!["legacy_enable_version_isolation"]!.Value<bool>().Should().BeTrue();

        return Task.CompletedTask;
    }

    [Fact]
    public Task BuildGlobalLaunchSettingsSnapshotJson_AutoModeExplainsSelectedJavaPathShouldBeIgnored()
    {
        var payload = JObject.Parse(AgentSettingsSnapshotJsonHelper.BuildGlobalLaunchSettingsSnapshotJson(new AgentGlobalSettingsSnapshotInput
        {
            JavaSelectionMode = "Auto",
            SelectedJavaPath = @"C:\Java\jdk-21\bin\javaw.exe",
            KnownJavaVersions =
            [
                new JavaVersion { Path = @"C:\Java\jdk-21\bin\javaw.exe", FullVersion = "21.0.4", MajorVersion = 21, IsJDK = true, Is64Bit = true }
            ],
            CurrentMinecraftPath = @"D:\Games\.minecraft"
        }));

        payload["java_settings"]!["selection_mode"]!.Value<string>().Should().Be("auto");
        payload["java_settings"]!["selected_java_path_interpretation"]!.Value<string>().Should().Be("auto_detected_result");
        payload["java_settings"]!["ignore_selected_java_path_for_changes"]!.Value<bool>().Should().BeTrue();
        payload["java_settings"]!["selected_java_path_guidance"]!.Value<string>().Should().Contain("不代表用户手动选择");
        payload["java_settings"]!["selected_java_path_guidance"]!.Value<string>().Should().Contain("应忽略该路径");

        return Task.CompletedTask;
    }

    [Fact]
    public Task BuildEffectiveLaunchSettingsSnapshotJson_ReportsEffectiveSources()
    {
        var payload = JObject.Parse(AgentSettingsSnapshotJsonHelper.BuildEffectiveLaunchSettingsSnapshotJson(new AgentEffectiveSettingsSnapshotInput
        {
            VersionId = "1.21.1-Fabric",
            MinecraftRootPath = @"D:\Games\.minecraft",
            VersionDirectoryPath = @"D:\Games\.minecraft\versions\1.21.1-Fabric",
            Config = new VersionConfig
            {
                UseGlobalJavaSetting = false,
                JavaPath = @"C:\Java\jdk-21\bin\javaw.exe",
                OverrideMemory = false,
                OverrideResolution = true,
                CustomJvmArguments = "-Dlocal=true",
                GarbageCollectorMode = "ZGC",
                WindowWidth = 1720,
                WindowHeight = 980,
                GameDirMode = "Custom",
                GameDirCustomPath = @"E:\Instances\GameDir"
            },
            EffectiveSettings = new EffectiveLaunchSettings
            {
                AutoMemoryAllocation = true,
                InitialHeapMemory = 6,
                MaximumHeapMemory = 12,
                JavaPath = @"C:\Java\jdk-21\bin\javaw.exe",
                WindowWidth = 1720,
                WindowHeight = 980,
                CustomJvmArguments = "-Dlocal=true",
                GarbageCollectorMode = "ZGC"
            },
            RequiredJavaVersion = 21,
            RequiredJavaVersionFromVersionInfo = true,
            ResolvedGameDirectory = @"E:\Instances\GameDir",
            GlobalGameIsolationModeKey = "VersionIsolation",
            GlobalLegacyEnableVersionIsolation = true,
            GlobalCustomGameDirectoryPath = @"D:\GlobalCustomDir"
        }));

        payload["required_java_version"]!.Value<int>().Should().Be(21);
        payload["required_java_version_source"]!.Value<string>().Should().Be("version_manifest");
        payload["java_settings"]!["effective_value_source"]!.Value<string>().Should().Be("local");
        payload["memory_settings"]!["effective_value_source"]!.Value<string>().Should().Be("global");
        payload["resolution_settings"]!["effective_value_source"]!.Value<string>().Should().Be("local");
        payload["game_directory_settings"]!["effective_value_source"]!.Value<string>().Should().Be("local_custom");
        payload["game_directory_settings"]!["effective_game_directory"]!.Value<string>().Should().Be(@"E:\Instances\GameDir");

        return Task.CompletedTask;
    }
}