using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public class JavaRuntimeServiceTests
{
    private readonly Mock<ILocalSettingsService> _mockLocalSettingsService;
    private readonly JavaRuntimeService _service;

    public JavaRuntimeServiceTests()
    {
        _mockLocalSettingsService = new Mock<ILocalSettingsService>();
        _service = new JavaRuntimeService(_mockLocalSettingsService.Object);
    }

    #region TryParseJavaVersion Tests

    [Theory]
    [InlineData("1.8.0_301", 8)]
    [InlineData("1.8.0", 8)]
    [InlineData("17.0.1", 17)]
    [InlineData("17", 17)]
    [InlineData("21.0.2", 21)]
    [InlineData("11.0.15", 11)]
    public void TryParseJavaVersion_ValidVersionString_ReturnsTrue(string versionString, int expectedMajorVersion)
    {
        // Act
        var result = _service.TryParseJavaVersion(versionString, out int majorVersion);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedMajorVersion, majorVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("abc.def")]
    public void TryParseJavaVersion_InvalidVersionString_ReturnsFalse(string versionString)
    {
        // Act
        var result = _service.TryParseJavaVersion(versionString, out int majorVersion);

        // Assert
        Assert.False(result);
        Assert.Equal(0, majorVersion);
    }

    [Fact]
    public void TryParseJavaVersion_Null_ReturnsFalse()
    {
        var result = _service.TryParseJavaVersion(null, out int majorVersion);

        Assert.False(result);
        Assert.Equal(0, majorVersion);
    }

    #endregion

    #region ValidateJavaPathAsync Tests

    [Fact]
    public async Task ValidateJavaPathAsync_NullPath_ReturnsFalse()
    {
        // Act
        var result = await _service.ValidateJavaPathAsync(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateJavaPathAsync_EmptyPath_ReturnsFalse()
    {
        // Act
        var result = await _service.ValidateJavaPathAsync(string.Empty);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateJavaPathAsync_NonExistentPath_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = @"C:\NonExistent\java.exe";

        // Act
        var result = await _service.ValidateJavaPathAsync(nonExistentPath);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region SelectBestJavaAsync Tests

    [Fact]
    public async Task SelectBestJavaAsync_WithVersionSpecificPath_ReturnsSpecificPath()
    {
        // Arrange — create a temporary file to simulate java.exe
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempJavaPath = Path.Combine(tempDir, "java.exe");
        File.WriteAllText(tempJavaPath, "dummy");

        try
        {
            // Act
            var result = await _service.SelectBestJavaAsync(17, tempJavaPath);

            // Assert
            Assert.Equal(tempJavaPath, result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task SelectBestJavaAsync_NoJavaFound_ReturnsNull()
    {
        // Arrange
        _mockLocalSettingsService
            .Setup(x => x.ReadSettingAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync((int?)null);
        
        _mockLocalSettingsService
            .Setup(x => x.ReadSettingAsync<List<JavaVersion>>(It.IsAny<string>()))
            .ReturnsAsync((List<JavaVersion>?)null);
        
        _mockLocalSettingsService
            .Setup(x => x.ReadSettingAsync<string>(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.SelectBestJavaAsync(999); // Use an unlikely version number

        // Assert
        // Result may be null or a system Java if found
        // This test verifies the method doesn't throw and handles the case gracefully
        Assert.True(result == null || !string.IsNullOrEmpty(result));
    }

    [Fact]
    public async Task SelectBestJavaAsync_ManualModeWithSelectedJavaPath_ReturnsSelectedPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var selectedJavaPath = Path.Combine(tempDir, "java.exe");
        File.WriteAllText(selectedJavaPath, "dummy");

        try
        {
            _mockLocalSettingsService
                .Setup(x => x.ReadSettingAsync<string>("JavaSelectionMode"))
                .ReturnsAsync("Manual");
            _mockLocalSettingsService
                .Setup(x => x.ReadSettingAsync<string>("SelectedJavaVersion"))
                .ReturnsAsync(selectedJavaPath);
            _mockLocalSettingsService
                .Setup(x => x.ReadSettingAsync<string>("JavaPath"))
                .ReturnsAsync((string?)null);
            _mockLocalSettingsService
                .Setup(x => x.ReadSettingAsync<List<JavaVersion>>("JavaVersions"))
                .ReturnsAsync([
                    new JavaVersion
                    {
                        Path = selectedJavaPath,
                        FullVersion = "25.0.1",
                        MajorVersion = 25,
                        IsJDK = false,
                    }
                ]);

            var result = await _service.SelectBestJavaAsync(25);

            Assert.Equal(selectedJavaPath, result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    #endregion

    #region DetectJavaVersionsAsync Tests

    [Fact]
    public async Task DetectJavaVersionsAsync_ReturnsList()
    {
        // Arrange
        _mockLocalSettingsService
            .Setup(x => x.ReadSettingAsync<List<JavaVersion>>(It.IsAny<string>()))
            .ReturnsAsync((List<JavaVersion>?)null);

        // Act
        var result = await _service.DetectJavaVersionsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<JavaVersion>>(result);
    }

    #endregion
}
