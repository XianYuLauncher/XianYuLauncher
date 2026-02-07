using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using XianYuLauncher.Core.VersionAnalysis;
using XianYuLauncher.Core.VersionAnalysis.Models;

namespace XianYuLauncher.Tests.VersionAnalysis
{
    public class ModLoaderDetectorTests
    {
        // 被测试对象
        private readonly ModLoaderDetector _detector;

        public ModLoaderDetectorTests()
        {
            // 初始化，不需要 Mock Logger 也可以（允许传 null）
            _detector = new ModLoaderDetector(null);
        }

        [Fact]
        public void Detect_ShouldReturnVanilla_WhenManifestIsNull()
        {
            // Act
            var result = _detector.Detect(null);

            // Assert
            result.Type.Should().Be("vanilla");
            result.Version.Should().BeEmpty();
        }

        [Fact]
        public void Detect_ShouldReturnVanilla_WhenLibrariesAreEmpty()
        {
            // Arrange
            var manifest = new MinecraftVersionManifest
            {
                Id = "1.20.1",
                Libraries = new List<Library>()
            };

            // Act
            var result = _detector.Detect(manifest);

            // Assert
            result.Type.Should().Be("vanilla");
        }

        [Fact]
        public void Detect_ShouldDetectFabric_WhenFabricLoaderLibraryExists()
        {
            // Arrange: 模拟一个包含 Fabric Loader 的 Manifest
            // 真实库名示例: net.fabricmc:fabric-loader:0.14.21
            var manifest = new MinecraftVersionManifest
            {
                Libraries = new List<Library>
                {
                    new Library { Name = "org.ow2.asm:asm:9.3" }, // 干扰项
                    new Library { Name = "net.fabricmc:fabric-loader:0.14.21" },
                    new Library { Name = "net.fabricmc:intermediary:1.20.1" }
                }
            };

            // Act
            var result = _detector.Detect(manifest);

            // Assert
            result.Type.Should().Be("fabric");
            result.Version.Should().Be("0.14.21");
        }

        [Fact]
        public void Detect_ShouldDetectForge_FromArguments_NewForge()
        {
            // Arrange: 新版 Forge 通常在 arguments 中指定版本
            // 示例参数: --fml.forgeVersion 47.1.0
            var manifest = new MinecraftVersionManifest
            {
                Libraries = new List<Library>(), // 库列表可以是空的，因为优先检查参数
                Arguments = new Arguments
                {
                    Game = new List<object>
                    {
                        "--username", "${auth_player_name}",
                        "--fml.forgeVersion", "47.1.0" 
                    }
                }
            };

            // Act
            var result = _detector.Detect(manifest);

            // Assert
            result.Type.Should().Be("forge");
            result.Version.Should().Be("47.1.0");
        }

        [Fact]
        public void Detect_ShouldDetectForge_FromLibraries_OldForge()
        {
            // Arrange: 以前的 Forge 只能通过库名判断
            // 示例库名: net.minecraftforge:forge:1.12.2-14.23.5.2854
            var manifest = new MinecraftVersionManifest
            {
                 Libraries = new List<Library>
                 {
                     new Library { Name = "net.minecraftforge:forge:1.12.2-14.23.5.2854" }
                 }
            };

            // Act
            var result = _detector.Detect(manifest);

            // Assert
            result.Type.Should().Be("forge");
            // 这里的 CleanForgeVersion 逻辑会把 1.12.2-14... 剥离，只保留 Loader 版本
            result.Version.Should().Be("14.23.5.2854");
        }

        [Fact]
        public void Detect_ShouldDetectNeoForge_FromLibraries()
        {
            // Arrange
            // 示例库名: net.neoforged:neoforge:20.1.0-beta
            var manifest = new MinecraftVersionManifest
            {
                Libraries = new List<Library>
                {
                    new Library { Name = "net.neoforged:neoforge:20.1.0-beta" }
                }
            };

            // Act
            var result = _detector.Detect(manifest);

            // Assert
            result.Type.Should().Be("neoforge");
            result.Version.Should().Be("20.1.0-beta");
        }
    }
}
