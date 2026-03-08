using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Services;

public class OptifineVersionHelperTests
{
    [Theory]
    [InlineData("HD_U:I5", "HD_U", "I5")]
    [InlineData("HD_U_I5", "HD_U", "I5")]
    [InlineData("1.12.2-HD_U_I5", "HD_U", "I5")]
    [InlineData("1.12.2_HD_U_I5", "HD_U", "I5")]
    [InlineData("1.21.1_HD_U_J7_pre10", "HD_U", "J7_pre10")]
    public void TryParse_ShouldSupportKnownFormats(string rawVersion, string expectedType, string expectedPatch)
    {
        var success = OptifineVersionHelper.TryParse(rawVersion, out var parts);

        success.Should().BeTrue();
        parts.Type.Should().Be(expectedType);
        parts.Patch.Should().Be(expectedPatch);
    }

    [Fact]
    public void TryNormalize_ShouldStripMinecraftVersionPrefix()
    {
        var success = OptifineVersionHelper.TryNormalize("1.21.1_HD_U_J7_pre10", out var normalizedVersion);

        success.Should().BeTrue();
        normalizedVersion.Should().Be("HD_U_J7_pre10");
    }

    [Fact]
    public void TryParse_WithMinecraftVersionHint_ShouldPreserveHintInDownloadFormat()
    {
        var success = OptifineVersionHelper.TryParse("HD_U:I5", "1.12.2", out var parts);

        success.Should().BeTrue();
        parts.ToDownloadSourceFormat("1.12.2").Should().Be("1.12.2-HD_U_I5");
    }
}