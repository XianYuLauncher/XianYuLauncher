using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Tests.Services.DownloadSource;

public class CustomDownloadSourceCapabilityTests
{
    [Fact]
    public void OfficialTemplateCustomSource_ShouldNotSupportLegacyFabric_WhenTemplateFallsBackToOfficialUrl()
    {
        var source = new CustomDownloadSource(
            key: "custom_official",
            name: "自定义Official",
            baseUrl: "https://xx",
            template: new BmclapiTemplate());

        Assert.False(source.SupportsLegacyFabric);
    }

    [Fact]
    public void OfficialTemplateCustomSource_ShouldSupportForge_WhenTemplateUsesBaseUrl()
    {
        var source = new CustomDownloadSource(
            key: "custom_official",
            name: "自定义Official",
            baseUrl: "https://xx",
            template: new BmclapiTemplate());

        Assert.True(source.SupportsForge);
    }

    [Fact]
    public void OfficialTemplateCustomSource_ShouldSupportLegacyFabric_WhenOverrideProvided()
    {
        var source = new CustomDownloadSource(
            key: "custom_official",
            name: "自定义Official",
            baseUrl: "https://xx",
            template: new BmclapiTemplate(),
            overrides: new Dictionary<string, string>
            {
                ["legacy_fabric_versions"] = "{baseUrl}/legacyfabric/{version}"
            });

        Assert.True(source.SupportsLegacyFabric);
    }

    [Fact]
    public void DownloadSourceFactory_ShouldFallbackToOfficial_WhenLegacyFabricSourceUnsupported()
    {
        var factory = new DownloadSourceFactory();
        factory.RegisterSource(
            "custom_official",
            new CustomDownloadSource(
                key: "custom_official",
                name: "自定义Official",
                baseUrl: "https://xx",
                template: new BmclapiTemplate()));

        factory.SetLegacyFabricSource("custom_official");

        Assert.Equal("official", factory.GetLegacyFabricSourceKey());
        Assert.Equal("official", factory.GetLegacyFabricSource().Key);
    }
}
