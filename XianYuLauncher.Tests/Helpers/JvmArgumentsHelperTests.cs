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
    public void MergeAndDeduplicateArguments_ShouldDeduplicateExactDuplicates_AndPreserveLastOccurrenceOrder()
    {
        var launcherArgs = new List<string>
        {
            "-Dfoo=1",
            "-Dfoo=1",
            "-Xmx2G",
            "-Xmx2G",
            "-XX:+UseG1GC"
        };

        var result = JvmArgumentsHelper.MergeAndDeduplicateArguments(launcherArgs, null);

        result.Should().Equal(new[]
        {
            "-Dfoo=1",
            "-Xmx2G",
            "-XX:+UseG1GC"
        });
    }
}
