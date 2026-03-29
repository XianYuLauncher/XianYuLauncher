using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Helpers;

public class AgentJavaInventoryHelperTests
{
    [Fact]
    public void NormalizeJavaVersions_AssignsStableJavaIdsAndSelectedFirst()
    {
        var inventory = AgentJavaInventoryHelper.NormalizeJavaVersions(
            @"C:\Java\jdk-21\bin\javaw.exe",
            [
                new JavaVersion { Path = @"C:\Java\jre-8\bin\javaw.exe", FullVersion = "1.8.0_401", MajorVersion = 8, IsJDK = false, Is64Bit = true },
                new JavaVersion { Path = @"C:\Java\jdk-21\bin\javaw.exe", FullVersion = "21.0.4", MajorVersion = 21, IsJDK = true, Is64Bit = true },
                new JavaVersion { Path = @"C:\Java\jdk-17\bin\javaw.exe", FullVersion = "17.0.11", MajorVersion = 17, IsJDK = true, Is64Bit = true }
            ]);

        inventory.Select(entry => entry.JavaId).Should().Equal("java_1", "java_2", "java_3");
        inventory[0].Path.Should().Be(@"C:\Java\jdk-21\bin\javaw.exe");
        inventory[0].MatchesSelectedJavaPath.Should().BeTrue();
    }

    [Fact]
    public void TryResolveJava_ByJavaId_ReturnsMatchingEntry()
    {
        var inventory = AgentJavaInventoryHelper.NormalizeJavaVersions(
            null,
            [
                new JavaVersion { Path = @"C:\Java\jdk-21\bin\javaw.exe", FullVersion = "21.0.4", MajorVersion = 21, IsJDK = true, Is64Bit = true },
                new JavaVersion { Path = @"C:\Java\jdk-17\bin\javaw.exe", FullVersion = "17.0.11", MajorVersion = 17, IsJDK = true, Is64Bit = true }
            ]);

        var success = AgentJavaInventoryHelper.TryResolveJava("java_2", null, inventory, out var javaEntry, out var errorMessage);

        success.Should().BeTrue(errorMessage);
        javaEntry.Should().NotBeNull();
        javaEntry!.Path.Should().Be(@"C:\Java\jdk-17\bin\javaw.exe");
    }

    [Fact]
    public void TryResolveJava_WhenJavaIdAndPathMismatch_ReturnsValidationError()
    {
        var inventory = AgentJavaInventoryHelper.NormalizeJavaVersions(
            null,
            [
                new JavaVersion { Path = @"C:\Java\jdk-21\bin\javaw.exe", FullVersion = "21.0.4", MajorVersion = 21, IsJDK = true, Is64Bit = true },
                new JavaVersion { Path = @"C:\Java\jdk-17\bin\javaw.exe", FullVersion = "17.0.11", MajorVersion = 17, IsJDK = true, Is64Bit = true }
            ]);

        var success = AgentJavaInventoryHelper.TryResolveJava(
            "java_1",
            @"C:\Java\jdk-17\bin\javaw.exe",
            inventory,
            out _ ,
            out var errorMessage);

        success.Should().BeFalse();
        errorMessage.Should().Contain("指向不同 Java");
        errorMessage.Should().Contain("java_id / selected_java_id");
        errorMessage.Should().Contain("java_path / selected_java_path");
    }

    [Fact]
    public void TryResolveJava_WhenNoSelectorProvided_ReturnsGenericParameterGuidance()
    {
        var inventory = AgentJavaInventoryHelper.NormalizeJavaVersions(
            null,
            [
                new JavaVersion { Path = @"C:\Java\jdk-21\bin\javaw.exe", FullVersion = "21.0.4", MajorVersion = 21, IsJDK = true, Is64Bit = true }
            ]);

        var success = AgentJavaInventoryHelper.TryResolveJava(null, null, inventory, out _, out var errorMessage);

        success.Should().BeFalse();
        errorMessage.Should().Contain("java_id / selected_java_id");
        errorMessage.Should().Contain("java_path / selected_java_path");
    }
}