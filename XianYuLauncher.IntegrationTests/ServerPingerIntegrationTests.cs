using System.Threading.Tasks;
using Xunit;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.IntegrationTests
{
    /// <summary>
    /// 服务器 Ping 集成测试
    /// 注意：这些测试需要网络连接，可能会比较慢
    /// </summary>
    public class ServerPingerIntegrationTests
    {
        [Fact(Skip = "需要本地服务器运行")]
        public async Task TestPinger_Localhost()
        {
            // 需要本地运行 Minecraft 服务器才能测试
            var (icon, motd, online, max, ping) = await ServerStatusFetcher.PingerAsync("localhost", 25565);
            
            Assert.True(ping >= 0, $"本地服务器 Ping 失败: {motd}");
        }
    }
}
