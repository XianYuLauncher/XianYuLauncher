using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.IntegrationTests
{
    /// <summary>
    /// 测速服务集成测试
    /// 注意：这些测试需要网络连接，实际测试 TCP 连接速度
    /// </summary>
    [Trait("Category", "Integration")]
    public class SpeedTestServiceIntegrationTests
    {
        /// <summary>
        /// 测试游戏资源源测速
        /// 验证：能够成功测速并返回结果
        /// </summary>
        [Fact(Skip = "网络集成测试，需要外网连接")]
        public async Task TestGameSources_Success()
        {
            // Arrange
            var downloadSourceFactory = new DownloadSourceFactory();
            var mockLogger = new Mock<ILogger<SpeedTestService>>();

            var speedTestService = new SpeedTestService(downloadSourceFactory, mockLogger.Object);

            // Act
            var results = await speedTestService.TestGameSourcesAsync();

            // Assert
            Assert.NotNull(results);
            Assert.NotEmpty(results);

            // 至少应该有一个成功的测速结果
            var successfulResults = results.FindAll(r => r.IsSuccess);
            Assert.NotEmpty(successfulResults);

            // 验证结果包含延迟信息
            foreach (var result in successfulResults)
            {
                Assert.True(result.LatencyMs > 0, "延迟应该大于0");
                Assert.False(string.IsNullOrEmpty(result.SourceKey), "源键不应为空");
            }

            // 输出测速结果供查看
            foreach (var result in results)
            {
                Console.WriteLine($"  {result.SourceKey}: {result.LatencyMs}ms ({(result.IsSuccess ? "成功" : "失败")})");
            }
        }

        /// <summary>
        /// 测试社区资源源测速
        /// 验证：能够成功测速 Modrinth/MCIM 等社区源
        /// </summary>
        [Fact(Skip = "网络集成测试，需要外网连接")]
        public async Task TestCommunitySources_Success()
        {
            // Arrange
            var downloadSourceFactory = new DownloadSourceFactory();
            var mockLogger = new Mock<ILogger<SpeedTestService>>();

            var speedTestService = new SpeedTestService(downloadSourceFactory, mockLogger.Object);

            // Act
            var results = await speedTestService.TestCommunitySourcesAsync();

            // Assert
            Assert.NotNull(results);
            Assert.NotEmpty(results);

            // 至少应该有一个成功的测速结果
            var successfulResults = results.FindAll(r => r.IsSuccess);
            Assert.NotEmpty(successfulResults);

            // 验证结果包含延迟信息
            foreach (var result in successfulResults)
            {
                Assert.True(result.LatencyMs > 0, "延迟应该大于0");
            }

            // 输出测速结果供查看
            foreach (var result in results)
            {
                Console.WriteLine($"  {result.SourceKey}: {result.LatencyMs}ms ({(result.IsSuccess ? "成功" : "失败")})");
            }
        }

        /// <summary>
        /// 测试获取最快游戏源
        /// 验证：能够正确返回延迟最低的源
        /// </summary>
        [Fact(Skip = "网络集成测试，需要外网连接")]
        public async Task GetFastestGameSourceKey_ReturnsFastest()
        {
            // Arrange
            var downloadSourceFactory = new DownloadSourceFactory();
            var mockLogger = new Mock<ILogger<SpeedTestService>>();

            var speedTestService = new SpeedTestService(downloadSourceFactory, mockLogger.Object);

            // Act
            var fastestKey = await speedTestService.GetFastestGameSourceKeyAsync();

            // Assert
            Assert.False(string.IsNullOrEmpty(fastestKey), "应该返回最快的源键");

            // 验证返回的键是有效的（存在于下载源工厂中）
            var sources = downloadSourceFactory.GetAllSources();
            Assert.True(sources.ContainsKey(fastestKey!), $"返回的源键 {fastestKey} 应该存在于下载源工厂中");

            Console.WriteLine($"最快游戏源: {fastestKey}");
        }

        /// <summary>
        /// 测试获取最快社区源
        /// 验证：能够正确返回延迟最低的社区源
        /// </summary>
        [Fact(Skip = "网络集成测试，需要外网连接")]
        public async Task GetFastestCommunitySourceKey_ReturnsFastest()
        {
            // Arrange
            var downloadSourceFactory = new DownloadSourceFactory();
            var mockLogger = new Mock<ILogger<SpeedTestService>>();

            var speedTestService = new SpeedTestService(downloadSourceFactory, mockLogger.Object);

            // Act
            var fastestKey = await speedTestService.GetFastestCommunitySourceKeyAsync();

            // Assert
            Assert.False(string.IsNullOrEmpty(fastestKey), "应该返回最快的社区源键");

            Console.WriteLine($"最快社区源: {fastestKey}");
        }

        /// <summary>
        /// 测试测速结果排序
        /// 验证：测速结果按延迟从低到高排序
        /// </summary>
        [Fact(Skip = "网络集成测试，需要外网连接")]
        public async Task TestGameSources_ResultsOrderedByLatency()
        {
            // Arrange
            var downloadSourceFactory = new DownloadSourceFactory();
            var mockLogger = new Mock<ILogger<SpeedTestService>>();

            var speedTestService = new SpeedTestService(downloadSourceFactory, mockLogger.Object);

            // Act
            var results = await speedTestService.TestGameSourcesAsync();

            // Assert
            // 筛选成功的测速结果并验证排序
            var successfulResults = results.FindAll(r => r.IsSuccess);
            if (successfulResults.Count > 1)
            {
                for (int i = 0; i < successfulResults.Count - 1; i++)
                {
                    Assert.True(successfulResults[i].LatencyMs <= successfulResults[i + 1].LatencyMs,
                        $"结果应该按延迟排序，但 {successfulResults[i].SourceKey}({successfulResults[i].LatencyMs}ms) > {successfulResults[i + 1].SourceKey}({successfulResults[i + 1].LatencyMs}ms)");
                }
            }
        }

        /// <summary>
        /// 测试缓存保存和加载
        /// 验证：缓存能够正确保存和加载
        /// </summary>
        [Fact(Skip = "网络集成测试，需要外网连接")]
        public async Task SaveAndLoadCache_WorksCorrectly()
        {
            // Arrange
            var downloadSourceFactory = new DownloadSourceFactory();
            var mockLogger = new Mock<ILogger<SpeedTestService>>();

            var speedTestService = new SpeedTestService(downloadSourceFactory, mockLogger.Object);

            // 创建测试缓存
            var testCache = new Core.Models.SpeedTestCache
            {
                LastUpdated = DateTime.UtcNow,
                GameSources = new Dictionary<string, Core.Models.SpeedTestResult>
                {
                    ["bmclapi"] = new Core.Models.SpeedTestResult
                    {
                        SourceKey = "bmclapi",
                        SourceName = "BMCLAPI",
                        LatencyMs = 50,
                        IsSuccess = true
                    }
                },
                CommunitySources = new Dictionary<string, Core.Models.SpeedTestResult>
                {
                    ["mcim"] = new Core.Models.SpeedTestResult
                    {
                        SourceKey = "mcim",
                        SourceName = "MCIM",
                        LatencyMs = 60,
                        IsSuccess = true
                    }
                }
            };

            // Act
            await speedTestService.SaveCacheAsync(testCache);
            var loadedCache = await speedTestService.LoadCacheAsync();

            // Assert
            Assert.NotNull(loadedCache);
            Assert.NotNull(loadedCache.GameSources);
            Assert.True(loadedCache.GameSources.ContainsKey("bmclapi"));
            Assert.Equal(50, loadedCache.GameSources["bmclapi"].LatencyMs);
        }
    }
}
