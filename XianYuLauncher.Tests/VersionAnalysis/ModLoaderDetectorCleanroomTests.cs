using System.IO;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using XianYuLauncher.Core.VersionAnalysis;
using XianYuLauncher.Core.VersionAnalysis.Models;

namespace XianYuLauncher.Tests.VersionAnalysis
{
    public class ModLoaderDetectorCleanroomTests
    {
        private readonly ModLoaderDetector _detector;

        public ModLoaderDetectorCleanroomTests()
        {
            _detector = new ModLoaderDetector(null);
        }

        [Fact]
        public void Detect_ShouldWorkWithCleanroomJson()
        {
            // 模拟 Cleanroom 的 JSON 结构
            // 关键特征: "com.cleanroommc:cleanroom"
            string jsonContent = """
            {
              "id": "1.12.2-cleanroom-0.4.2-alpha",
              "libraries": [
                {
                  "name": "com.cleanroommc:cleanroom:0.4.2-alpha",
                  "url": "https://maven.cleanroommc.com/"
                }
              ]
            }
            """;

            var manifest = JsonConvert.DeserializeObject<MinecraftVersionManifest>(jsonContent);
            var result = _detector.Detect(manifest);

            result.Type.Should().Be("cleanroom");
            result.Version.Should().Be("0.4.2-alpha");
        }
    }
}
