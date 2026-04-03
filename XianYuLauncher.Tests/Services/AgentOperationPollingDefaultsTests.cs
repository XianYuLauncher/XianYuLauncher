using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class AgentOperationPollingDefaultsTests
{
    [Fact]
    public void NormalizeSleepSeconds_ShouldUseDefaultWhenNull()
    {
        int result = AgentOperationPollingDefaults.NormalizeSleepSeconds(null);

        result.Should().Be(AgentOperationPollingDefaults.DefaultSuggestedPollDelaySeconds);
    }

    [Fact]
    public void NormalizeSleepSeconds_ShouldClampBelowMinimum()
    {
        int result = AgentOperationPollingDefaults.NormalizeSleepSeconds(0);

        result.Should().Be(AgentOperationPollingDefaults.MinSleepSeconds);
    }

    [Fact]
    public void NormalizeSleepSeconds_ShouldClampAboveMaximum()
    {
        int result = AgentOperationPollingDefaults.NormalizeSleepSeconds(999);

        result.Should().Be(AgentOperationPollingDefaults.MaxSleepSeconds);
    }

    [Fact]
    public void NormalizeSleepSeconds_ShouldKeepInRangeValue()
    {
        int result = AgentOperationPollingDefaults.NormalizeSleepSeconds(12);

        result.Should().Be(12);
    }
}