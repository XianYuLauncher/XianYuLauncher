using FluentAssertions;

using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Helpers;

public class AppEnvironmentTests
{
    [Fact]
    public void GetUnpackagedAppDataDirectoryName_StableSideLoad_ReturnsStableDirectory()
    {
        string directoryName = AppEnvironment.GetUnpackagedAppDataDirectoryName(DistributionChannel.SideLoad);

        directoryName.Should().Be("XianYuLauncher");
    }

    [Fact]
    public void GetUnpackagedAppDataDirectoryName_DevSideLoad_ReturnsDevDirectory()
    {
        string directoryName = AppEnvironment.GetUnpackagedAppDataDirectoryName(DistributionChannel.DevSideLoad);

        directoryName.Should().Be("XianYuLauncher.Dev");
    }

    [Fact]
    public void ResolveUnpackagedAppDataPath_DevSideLoad_UsesIsolatedDirectory()
    {
        string path = AppEnvironment.ResolveUnpackagedAppDataPath(@"C:\Users\pc\AppData\Local", DistributionChannel.DevSideLoad);

        path.Should().Be(@"C:\Users\pc\AppData\Local\XianYuLauncher.Dev");
    }
}

public class AppLanguageCodesTests
{
    [Theory]
    [InlineData("zh-tw", "zh-TW")]
    [InlineData("zh-cn", "zh-CN")]
    [InlineData("en-us", "en-US")]
    public void Normalize_ReturnsCanonicalLanguageCodes(string input, string expected)
    {
        AppLanguageCodes.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("zh-CN", true)]
    [InlineData("zh-TW", true)]
    [InlineData("en-US", false)]
    public void IsChinese_RecognizesChineseLocales(string language, bool expected)
    {
        AppLanguageCodes.IsChinese(language).Should().Be(expected);
    }

    [Theory]
    [InlineData("zh-CN", "Simplified Chinese")]
    [InlineData("zh-TW", "Traditional Chinese")]
    [InlineData("en-US", "English")]
    public void GetAiPromptLanguageName_ReturnsExpectedPrompt(string language, string expected)
    {
        AppLanguageCodes.GetAiPromptLanguageName(language).Should().Be(expected);
    }
}