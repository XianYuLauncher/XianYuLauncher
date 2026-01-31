using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Contracts.Services;
using Microsoft.Extensions.Logging.Abstractions;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests
{
    public class LibraryManagerTests
    {
        [Fact]
        public void GetLibraryPath_NeoForgeMergetool_ShouldNotHaveDoubleExtension()
        {
            // Arrange
            var mockLocalSettings = new Mock<ILocalSettingsService>();
            mockLocalSettings.Setup(x => x.ReadSettingAsync<string>(It.IsAny<string>()))
                .ReturnsAsync((string)null);
                
            var downloadManager = new DownloadManager(new NullLogger<DownloadManager>(), mockLocalSettings.Object);
            var logger = new NullLogger<LibraryManager>();
            var libraryManager = new LibraryManager(downloadManager, logger);

            string libraryName = "net.neoforged:mergetool:2.0.0:api@jar";
            string librariesDirectory = @"E:\libraries";

            // Act
            string path = libraryManager.GetLibraryPath(libraryName, librariesDirectory);

            // Assert
            // Expected: ...\mergetool-2.0.0-api.jar
            // On Windows, separators are backslashes
            string expectedEndsWith = Path.Combine("net", "neoforged", "mergetool", "2.0.0", "mergetool-2.0.0-api.jar");
            
            Assert.EndsWith(expectedEndsWith, path);
            Assert.DoesNotContain(".jar.jar", path);
        }
    }
}
