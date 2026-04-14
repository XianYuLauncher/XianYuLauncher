using FluentAssertions;

using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Helpers;

public class AppEnvironmentTests
{
    [Fact]
    public void GetUnpackagedAppDataDirectoryName_StableSideLoad_ReturnsStableDirectory()
    {
        string directoryName = AppEnvironment.GetUnpackagedAppDataDirectoryName(DistributionChannel.SideLoad);

        directoryName.Should().Be("XianYuLauncher");
    }

    [Fact]
    public void GetUnpackagedAppDataDirectoryName_DevSideLoad_ReturnsDevDirectory()
    {
        string directoryName = AppEnvironment.GetUnpackagedAppDataDirectoryName(DistributionChannel.DevSideLoad);

        directoryName.Should().Be("XianYuLauncher.Dev");
    }

    [Fact]
    public void ResolveUnpackagedAppDataPath_DevSideLoad_UsesIsolatedDirectory()
    {
        string path = AppEnvironment.ResolveUnpackagedAppDataPath(@"C:\Users\pc\AppData\Local", DistributionChannel.DevSideLoad);

        path.Should().Be(@"C:\Users\pc\AppData\Local\XianYuLauncher.Dev");
    }
}