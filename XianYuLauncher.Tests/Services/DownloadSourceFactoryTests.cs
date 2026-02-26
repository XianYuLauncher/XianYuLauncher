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
        Assert.Equal(2, sources.Count); // official, bmclapi
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
        Assert.Equal(2, sources.Count); // official, mcim
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
        Assert.Equal(2, sources.Count); // official, mcim
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
}
