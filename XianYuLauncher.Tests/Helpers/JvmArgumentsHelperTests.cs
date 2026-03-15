using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public class JvmArgumentsHelperTests
{
    [Fact]
    public void MergeAndDeduplicateArguments_ShouldKeepLastValue_ForSameSemanticKey()
    {
        var launcherArgs = new List<string>
        {
            "-Xms1G",
            "-Xmx2G",
            "-XX:+UseG1GC",
            "-Dfoo=1",
            "-Dbar=launcher"
        };

        var customArgs = "-Xms3G -Xmx6G -XX:+UseZGC -Dfoo=2";

        var result = JvmArgumentsHelper.MergeAndDeduplicateArguments(launcherArgs, customArgs);

        result.Should().Contain("-Xms3G");
        result.Should().Contain("-Xmx6G");
        result.Should().Contain("-XX:+UseZGC");
        result.Should().Contain("-Dfoo=2");
        result.Should().Contain("-Dbar=launcher");

        result.Should().NotContain("-Xms1G");
        result.Should().NotContain("-Xmx2G");
        result.Should().NotContain("-XX:+UseG1GC");
        result.Should().NotContain("-Dfoo=1");
    }

    [Fact]
    public void MergeAndDeduplicateArguments_ShouldKeepNonOverrideDuplicates_Unchanged()
    {
        var launcherArgs = new List<string>
        {
            "--add-opens",
            "java.base/java.util.jar=cpw.mods.securejarhandler",
            "--add-opens",
            "java.base/java.lang.invoke=cpw.mods.securejarhandler"
        };

        var result = JvmArgumentsHelper.MergeAndDeduplicateArguments(launcherArgs, null);

        // 非覆盖域参数不做全局去重，必须保持原样
        result.Should().Equal(launcherArgs);
    }

    /// <summary>
    /// 复现 Forge 1.20.1 崩溃问题：version.json 的 jvm 参数中 --add-opens / --add-exports
    /// 各出现两次，去重逻辑不能把相同标志视为同一 key，需按"标志+值"配对去重。
    /// </summary>
    [Fact]
    public void MergeAndDeduplicateArguments_ShouldPreserveDistinctPairedFlags()
    {
        // 摘自 Forge 1.20.1 version.json arguments.jvm
        var launcherArgs = new List<string>
        {
            "--add-modules",
            "ALL-MODULE-PATH",
            "--add-opens",
            "java.base/java.util.jar=cpw.mods.securejarhandler",
            "--add-opens",
            "java.base/java.lang.invoke=cpw.mods.securejarhandler",
            "--add-exports",
            "java.base/sun.security.util=cpw.mods.securejarhandler",
            "--add-exports",
            "jdk.naming.dns/com.sun.jndi.dns=java.naming"
        };

        var result = JvmArgumentsHelper.MergeAndDeduplicateArguments(launcherArgs, null);

        // 所有标志及其对应的值均应保留
        result.Should().ContainInOrder("--add-modules", "ALL-MODULE-PATH");
        result.Should().ContainInOrder("--add-opens", "java.base/java.util.jar=cpw.mods.securejarhandler");
        result.Should().ContainInOrder("--add-opens", "java.base/java.lang.invoke=cpw.mods.securejarhandler");
        result.Should().ContainInOrder("--add-exports", "java.base/sun.security.util=cpw.mods.securejarhandler");
        result.Should().ContainInOrder("--add-exports", "jdk.naming.dns/com.sun.jndi.dns=java.naming");

        // 值不能作为孤立 token 出现：必须和对应标志相邻
        ContainsAdjacentPair(result, "--add-modules", "ALL-MODULE-PATH").Should().BeTrue();
        ContainsAdjacentPair(result, "--add-opens", "java.base/java.util.jar=cpw.mods.securejarhandler").Should().BeTrue();
        ContainsAdjacentPair(result, "--add-opens", "java.base/java.lang.invoke=cpw.mods.securejarhandler").Should().BeTrue();
        ContainsAdjacentPair(result, "--add-exports", "java.base/sun.security.util=cpw.mods.securejarhandler").Should().BeTrue();
        ContainsAdjacentPair(result, "--add-exports", "jdk.naming.dns/com.sun.jndi.dns=java.naming").Should().BeTrue();

        // 5 对参数，每对 2 个 token，共 10 个（5 * 2 = launcherArgs.Count）
        result.Should().HaveCount(launcherArgs.Count);
    }

    [Fact]
    public void MergeAndDeduplicateArguments_ShouldNotDeduplicateIdenticalPairedFlagsAcrossSources()
    {
        // 同一 --add-opens 成对参数来自 launcher + 自定义参数时，不做全局去重
        var launcherArgs = new List<string>
        {
            "--add-opens",
            "java.base/java.util.jar=cpw.mods.securejarhandler",
        };

        var customArgs = "--add-opens java.base/java.util.jar=cpw.mods.securejarhandler";

        var result = JvmArgumentsHelper.MergeAndDeduplicateArguments(launcherArgs, customArgs);

        result.Should().HaveCount(4);
        result.Should().Equal(
            "--add-opens",
            "java.base/java.util.jar=cpw.mods.securejarhandler",
            "--add-opens",
            "java.base/java.util.jar=cpw.mods.securejarhandler");
    }

    [Fact]
    public void MergeAndDeduplicateArguments_ShouldKeepLastOverrideWithinCustomArgs()
    {
        var launcherArgs = new List<string>
        {
            "-Xmx2G",
            "-XX:+UseG1GC",
            "-Dfoo=1"
        };

        var customArgs = "-Xmx4G -Xmx6G -XX:+UseZGC -XX:+UseG1GC -Dfoo=2 -Dfoo=3";

        var result = JvmArgumentsHelper.MergeAndDeduplicateArguments(launcherArgs, customArgs);

        result.Should().Contain("-Xmx6G");
        result.Should().Contain("-XX:+UseG1GC");
        result.Should().Contain("-Dfoo=3");

        result.Should().NotContain("-Xmx2G");
        result.Should().NotContain("-Xmx4G");
        result.Should().NotContain("-XX:+UseZGC");
        result.Should().NotContain("-Dfoo=1");
        result.Should().NotContain("-Dfoo=2");
    }

    [Fact]
    public void MergeAndDeduplicateArguments_ShouldKeepLastOverrideWithinLauncherArgs_WhenCustomArgsIsEmpty()
    {
        var launcherArgs = new List<string>
        {
            "-Xmx2G",
            "-Xmx4G",
            "-XX:+UseZGC",
            "-XX:+UseG1GC",
            "-Dfoo=1",
            "-Dfoo=2"
        };

        var result = JvmArgumentsHelper.MergeAndDeduplicateArguments(launcherArgs, null);

        result.Should().Contain("-Xmx4G");
        result.Should().Contain("-XX:+UseG1GC");
        result.Should().Contain("-Dfoo=2");

        result.Should().NotContain("-Xmx2G");
        result.Should().NotContain("-XX:+UseZGC");
        result.Should().NotContain("-Dfoo=1");
    }

    [Fact]
    public void MergeAndDeduplicateArguments_ShouldTreatDashDWithoutEqualsAsOverrideKey()
    {
        var launcherArgs = new List<string>
        {
            "-Dfoo=1"
        };

        var customArgs = "-Dfoo";

        var result = JvmArgumentsHelper.MergeAndDeduplicateArguments(launcherArgs, customArgs);

        result.Should().ContainSingle(item => item == "-Dfoo");
        result.Should().NotContain("-Dfoo=1");
    }

    private static bool ContainsAdjacentPair(List<string> args, string flag, string value)
    {
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (args[i] == flag && args[i + 1] == value)
            {
                return true;
            }
        }

        return false;
    }
}
