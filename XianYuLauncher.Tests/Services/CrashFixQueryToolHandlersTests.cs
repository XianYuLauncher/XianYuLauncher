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
        payload["selected_java_present_in_list"]!.Value<bool>().Should().BeTrue();

        var javaVersions = (JArray)payload["java_versions"]!;
        javaVersions.Should().HaveCount(2);
        javaVersions[0]!["path"]!.Value<string>().Should().Be(@"C:\Java\jdk-21\bin\javaw.exe");
        javaVersions[0]!["matches_selected_java_path"]!.Value<bool>().Should().BeTrue();

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
}