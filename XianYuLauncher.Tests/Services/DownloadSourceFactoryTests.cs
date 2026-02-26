using System.Linq;
using Xunit;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Tests.Services;

public class DownloadSourceFactoryTests
{
    #region Supports* 属性测试

    [Fact]
    public void OfficialDownloadSource_SupportsGameResources_ReturnsTrue()
    {
        // Arrange
        var factory = new DownloadSourceFactory();
        var source = factory.GetSource("official");

        // Act & Assert
        Assert.True(source.SupportsGameResources);
        Assert.True(source.SupportsModrinth);
        Assert.True(source.SupportsCurseForge);
    }

    [Fact]
    public void BmclapiDownloadSource_SupportsGameResources_ReturnsTrue()
    {
        // Arrange
        var factory = new DownloadSourceFactory();
        var source = factory.GetSource("bmclapi");

        // Act & Assert
        Assert.True(source.SupportsGameResources);
        Assert.False(source.SupportsModrinth);
        Assert.False(source.SupportsCurseForge);
    }

    [Fact]
    public void McimDownloadSource_SupportsGameResources_ReturnsFalse()
    {
        // Arrange
        var factory = new DownloadSourceFactory();
        var source = factory.GetSource("mcim");

        // Act & Assert
        Assert.False(source.SupportsGameResources);
        Assert.True(source.SupportsModrinth);
        Assert.True(source.SupportsCurseForge);
    }

    #endregion

    #region GetSourcesFor* 方法测试

    [Fact]
    public void GetSourcesForGameResources_ReturnsOnlyGameResourcesSources()
    {
        // Arrange
        var factory = new DownloadSourceFactory();
        var sources = factory.GetSourcesForGameResources();

        // Act & Assert
        Assert.All(sources, s => Assert.True(s.SupportsGameResources));
        Assert.Contains(sources, s => s.Key == "official");
        Assert.Contains(sources, s => s.Key == "bmclapi");
        Assert.DoesNotContain(sources, s => s.Key == "mcim");
    }

    [Fact]
    public void GetSourcesForModrinth_ReturnsOnlyModrinthSources()
    {
        // Arrange
        var factory = new DownloadSourceFactory();
        var sources = factory.GetSourcesForModrinth();

        // Act & Assert
        Assert.All(sources, s => Assert.True(s.SupportsModrinth));
        Assert.Contains(sources, s => s.Key == "official");
        Assert.Contains(sources, s => s.Key == "mcim");
        Assert.DoesNotContain(sources, s => s.Key == "bmclapi");
    }

    [Fact]
    public void GetSourcesForCurseForge_ReturnsOnlyCurseForgeSources()
    {
        // Arrange
        var factory = new DownloadSourceFactory();
        var sources = factory.GetSourcesForCurseForge();

        // Act & Assert
        Assert.All(sources, s => Assert.True(s.SupportsCurseForge));
        Assert.Contains(sources, s => s.Key == "official");
        Assert.Contains(sources, s => s.Key == "mcim");
        Assert.DoesNotContain(sources, s => s.Key == "bmclapi");
    }

    #endregion

    #region GetDisplayName 方法测试

    [Fact]
    public void GetDisplayName_Official_ReturnsChineseName()
    {
        // Arrange
        var factory = new DownloadSourceFactory();

        // Act
        var displayName = factory.GetDisplayName("official");

        // Assert
        Assert.Equal("官方源", displayName);
    }

    [Fact]
    public void GetDisplayName_Bmclapi_ReturnsChineseName()
    {
        // Arrange
        var factory = new DownloadSourceFactory();

        // Act
        var displayName = factory.GetDisplayName("bmclapi");

        // Assert
        Assert.Equal("BMCLAPI 镜像", displayName);
    }

    [Fact]
    public void GetDisplayName_Mcim_ReturnsChineseName()
    {
        // Arrange
        var factory = new DownloadSourceFactory();

        // Act
        var displayName = factory.GetDisplayName("mcim");

        // Assert
        Assert.Equal("MCIM 镜像", displayName);
    }

    [Fact]
    public void GetDisplayName_UnknownKey_ReturnsKey()
    {
        // Arrange
        var factory = new DownloadSourceFactory();

        // Act
        var displayName = factory.GetDisplayName("unknown");

        // Assert
        Assert.Equal("unknown", displayName);
    }

    #endregion

    #region CustomDownloadSource Supports* 测试

    [Fact]
    public void CustomOfficialTemplateSource_ParticipatesInGameResourcesOnly()
    {
        // Arrange
        var factory = new DownloadSourceFactory();
        var customOfficial = new CustomDownloadSource(
            key: "custom-official",
            name: "自定义官方镜像",
            baseUrl: "https://example.com",
            template: new BmclapiTemplate());

        factory.RegisterSource("custom-official", customOfficial);

        // Act
        var gameSources = factory.GetSourcesForGameResources();
        var modrinthSources = factory.GetSourcesForModrinth();
        var curseForgeSources = factory.GetSourcesForCurseForge();

        // Assert - official 模板自定义源应参与游戏资源过滤
        Assert.Contains(gameSources, s => s.Key == "custom-official" && s.SupportsGameResources);
        // Assert - official 模板自定义源不应参与 Modrinth/CurseForge 过滤
        Assert.DoesNotContain(modrinthSources, s => s.Key == "custom-official");
        Assert.DoesNotContain(curseForgeSources, s => s.Key == "custom-official");
    }

    [Fact]
    public void CustomCommunityTemplateSource_ExcludedFromGameResourcesButIncludedInModPlatforms()
    {
        // Arrange
        var factory = new DownloadSourceFactory();
        var customCommunity = new CustomDownloadSource(
            key: "custom-community",
            name: "自定义社区镜像",
            baseUrl: "https://example.com",
            template: new McimTemplate());

        factory.RegisterSource("custom-community", customCommunity);

        // Act
        var gameSources = factory.GetSourcesForGameResources();
        var modrinthSources = factory.GetSourcesForModrinth();
        var curseForgeSources = factory.GetSourcesForCurseForge();

        // Assert - community 模板不应参与游戏资源过滤
        Assert.DoesNotContain(gameSources, s => s.Key == "custom-community");
        // Assert - 但应参与 Modrinth/CurseForge 过滤
        Assert.Contains(modrinthSources, s => s.Key == "custom-community" && s.SupportsModrinth);
        Assert.Contains(curseForgeSources, s => s.Key == "custom-community" && s.SupportsCurseForge);
    }

    #endregion
}
