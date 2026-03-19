using Newtonsoft.Json;
using XianYuLauncher.Core.VersionAnalysis;
using XianYuLauncher.Core.VersionAnalysis.Models;

namespace XianYuLauncher.IntegrationTests.VersionAnalysis;

[Trait("Category", "Integration")]
public class ModLoaderDetectorIntegrationTests
{
    private readonly ModLoaderDetector _detector = new(null);

    [Fact]
    public void Detect_ShouldWorkWithLegacyFabricJson()
    {
        string jsonContent = """
        {
          "id": "1.8.9-legacyfabric-0.18.4",
          "libraries": [
            {
              "name": "net.fabricmc:fabric-loader:0.18.4",
              "url": "https://maven.fabricmc.net/"
            },
            {
              "name": "net.legacyfabric:intermediary:1.8.9", 
              "url": "https://maven.legacyfabric.net/"
            }
          ]
        }
        """;

        var manifest = JsonConvert.DeserializeObject<MinecraftVersionManifest>(jsonContent);

        var result = _detector.Detect(manifest);

        Assert.Equal("LegacyFabric", result.Type);
        Assert.Equal("0.18.4", result.Version);
    }
}
