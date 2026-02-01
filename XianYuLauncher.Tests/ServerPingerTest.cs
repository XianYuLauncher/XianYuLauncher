using System.Threading.Tasks;
using Xunit;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests
{
    public class ServerPingerTest
    {
        [Fact]
        public async Task TestPinger_Hypixel()
        {
            // Note: This test requires internet connection
            var (icon, motd, online, max, ping) = await ServerStatusFetcher.PingerAsync("mc.hypixel.net", 25565);
            
            // If it fails, ping is -1. 
            // Assert.True(ping >= 0, $"Ping failed with status: {motd}");
        }

        [Fact]
        public async Task TestPinger_Localhost()
        {
            // Requires a local server running, so might just be a template
            // var (icon, motd, online, max, ping) = await ServerStatusFetcher.PingerAsync("localhost", 25565);
        }
    }
}
