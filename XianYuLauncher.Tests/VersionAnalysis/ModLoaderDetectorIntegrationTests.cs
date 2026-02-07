using System.IO;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using XianYuLauncher.Core.VersionAnalysis;
using XianYuLauncher.Core.VersionAnalysis.Models;

namespace XianYuLauncher.Tests.VersionAnalysis
{
    public class ModLoaderDetectorIntegrationTests
    {
        private readonly ModLoaderDetector _detector;

        public ModLoaderDetectorIntegrationTests()
        {
            _detector = new ModLoaderDetector(null);
        }

        [Fact]
        public void Detect_ShouldWorkWithLegacyFabricJson()
        {
            // 使用简化版的 JSON，仅包含检测所需的特征库
            // 真实数据提取自: 1.8.9-legacyfabric-0.18.4.json
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

            // 2. 反序列化
            var manifest = JsonConvert.DeserializeObject<MinecraftVersionManifest>(jsonContent);

            // 3. 测试
            var result = _detector.Detect(manifest);

            // 4. 验证
            result.Type.Should().Be("LegacyFabric");
            result.Version.Should().Be("0.18.4");
        }
    }
}
