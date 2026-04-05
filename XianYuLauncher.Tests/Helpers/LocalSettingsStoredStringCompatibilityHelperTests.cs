using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public class LocalSettingsStoredStringCompatibilityHelperTests
{
    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("en-US", "en-US")]
    [InlineData("\"zh-CN\"", "zh-CN")]
    [InlineData("\"en-US\"", "en-US")]
    public void UnwrapStoredString_ShouldReturnExpectedValue(string rawValue, string expected)
    {
        var result = LocalSettingsStoredStringCompatibilityHelper.UnwrapStoredString(rawValue);

        result.Should().Be(expected);
    }

    [Fact]
    public void UnwrapStoredString_ShouldReturnRawValue_WhenJsonStringIsInvalid()
    {
        const string rawValue = "\"\\uZZZZ\"";

        var result = LocalSettingsStoredStringCompatibilityHelper.UnwrapStoredString(rawValue);

        result.Should().Be(rawValue);
    }
}