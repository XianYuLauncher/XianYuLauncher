using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public class ProtocolHelpersTests
{
    [Theory]
    [InlineData("\\\\server\\share\\instance", true)]
    [InlineData("//server/share/instance", true)]
    [InlineData("C:\\Games\\.minecraft\\versions\\1.20.1", false)]
    [InlineData("D:/Games/.minecraft/versions/1.20.1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsUncPath_ShouldMatchExpected(string? path, bool expected)
    {
        var result = ProtocolPathSecurityHelper.IsUncPath(path);

        result.Should().Be(expected);
    }

    [Fact]
    public void ParseQueryString_ShouldDecodeAndUseCaseInsensitiveKeys()
    {
        var query = "?path=C%3A%5CGames%5CInstance&Server=mc.example.com&port=25565&map=My%20World";

        var result = ProtocolQueryStringHelper.ParseQueryString(query);

        result["path"].Should().Be("C:\\Games\\Instance");
        result["server"].Should().Be("mc.example.com");
        result["PORT"].Should().Be("25565");
        result["map"].Should().Be("My World");
    }

    [Fact]
    public void ParseQueryString_ShouldIgnoreMalformedPairs_AndKeepEmptyValue()
    {
        var query = "?valid=1&novalue&empty=&another=ok";

        var result = ProtocolQueryStringHelper.ParseQueryString(query);

        result.Should().ContainKey("valid");
        result.Should().ContainKey("empty");
        result["empty"].Should().BeEmpty();
        result.Should().ContainKey("another");
        result.Should().NotContainKey("novalue");
    }

    [Theory]
    [InlineData("?path=%5C%5Cevil.example.com%5Cshare%5Cinstance")]
    [InlineData("?path=%2F%2Fevil.example.com%2Fshare%2Finstance")]
    [InlineData("?path=%5C%5C10.0.0.8%5CC%24%5CUsers%5CAdministrator%5CDesktop%5Csecret.config")]
    [InlineData("?path=\\\\10.0.0.8\\C$\\Users\\Administrator\\Desktop\\secret.config")]
    public void ParseQueryString_ThenIsUncPath_ShouldBlockEncodedUncPath(string query)
    {
        var result = ProtocolQueryStringHelper.ParseQueryString(query);

        result.Should().ContainKey("path");
        ProtocolPathSecurityHelper.IsUncPath(result["path"]).Should().BeTrue();
    }

    [Theory]
    [InlineData("?path=C%3A%5CGames%5CInstance&server=evil.example.com")]
    [InlineData("?path=D%3A%2FGames%2FInstance&server=198.51.100.9")]
    public void ParseQueryString_ShouldNotTreatServerHostAsUncPath(string query)
    {
        var result = ProtocolQueryStringHelper.ParseQueryString(query);

        result.Should().ContainKey("path");
        ProtocolPathSecurityHelper.IsUncPath(result["path"]).Should().BeFalse();
        result.Should().ContainKey("server");
    }
}
