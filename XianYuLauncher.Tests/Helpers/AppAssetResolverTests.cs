using System.IO;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public class AppAssetResolverTests
{
    [Fact]
    public void NormalizePath_ShouldStripLegacyMsAppxPrefix()
    {
        var normalized = AppAssetResolver.NormalizePath("ms-appx:///Assets/Placeholder.png");

        normalized.Should().Be(AppAssetResolver.PlaceholderAssetPath);
    }

    [Theory]
    [InlineData("ms-appx:///Assets/Placeholder.png")]
    [InlineData("Assets/Placeholder.png")]
    public void IsAppAssetPath_ShouldRecognizeLogicalAssetPath(string path)
    {
        var result = AppAssetResolver.IsAppAssetPath(path);

        result.Should().BeTrue();
    }

    [Fact]
    public void TryGetLoaderIconAssetPath_ShouldNormalizeLoaderAliases()
    {
        var foundLegacyFabric = AppAssetResolver.TryGetLoaderIconAssetPath("Legacy Fabric", out var legacyFabricIcon);
        var foundLiteLoader = AppAssetResolver.TryGetLoaderIconAssetPath("LiteLoader", out var liteLoaderIcon);

        foundLegacyFabric.Should().BeTrue();
        legacyFabricIcon.Should().Be(AppAssetResolver.LegacyFabricIconAssetPath);
        foundLiteLoader.Should().BeTrue();
        liteLoaderIcon.Should().Be(AppAssetResolver.LiteLoaderIconAssetPath);
    }

    [Fact]
    public void ToUri_ShouldResolveLogicalAssetPathForCurrentDeploymentMode()
    {
        var uri = AppAssetResolver.ToUri(AppAssetResolver.DefaultAvatarAssetPath);

        if (AppEnvironment.IsMSIX)
        {
            uri.Scheme.Should().Be("ms-appx");
            uri.AbsoluteUri.Should().Be("ms-appx:///Assets/Icons/Avatars/Steve.png");
            return;
        }

        uri.IsFile.Should().BeTrue();
        uri.LocalPath.Should().EndWith(Path.Combine("Assets", "Icons", "Avatars", "Steve.png"));
    }

    [Fact]
    public void NormalizeOrDefault_ShouldConvertLegacyDefaultIconToLogicalAssetPath()
    {
        var normalized = VersionIconPathHelper.NormalizeOrDefault("ms-appx:///Assets/Icons/Download_Options/Vanilla/icon_128x128.png");

        normalized.Should().Be(VersionIconPathHelper.DefaultIconPath);
    }

    [Theory]
    [InlineData("https://example.com/icon.png")]
    [InlineData("missing/icon.png")]
    public void NormalizeOrDefault_ShouldFallbackToDefaultIconForUnsupportedPath(string iconPath)
    {
        var normalized = VersionIconPathHelper.NormalizeOrDefault(iconPath);

        normalized.Should().Be(VersionIconPathHelper.DefaultIconPath);
    }
}