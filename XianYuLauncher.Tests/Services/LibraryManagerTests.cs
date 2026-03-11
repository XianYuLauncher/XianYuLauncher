using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public class LibraryManagerTests : IDisposable
{
    private readonly Mock<IDownloadManager> _mockDownloadManager;
    private readonly Mock<ILogger<LibraryManager>> _mockLogger;
    private readonly LibraryManager _libraryManager;
    private readonly string _testDirectory;

    public LibraryManagerTests()
    {
        _mockDownloadManager = new Mock<IDownloadManager>();
        _mockLogger = new Mock<ILogger<LibraryManager>>();
        _libraryManager = new LibraryManager(_mockDownloadManager.Object, _mockLogger.Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LibraryManagerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region GetLibraryPath 测试

    [Fact]
    public void GetLibraryPath_SimpleLibrary_ReturnsCorrectPath()
    {
        // Arrange
        var libraryName = "com.google.code.gson:gson:2.8.9";
        var librariesDir = _testDirectory;

        // Act
        var result = _libraryManager.GetLibraryPath(libraryName, librariesDir);

        // Assert
        var expected = Path.Combine(librariesDir, "com", "google", "code", "gson", "gson", "2.8.9", "gson-2.8.9.jar");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetLibraryPath_WithClassifier_ReturnsCorrectPath()
    {
        // Arrange
        var libraryName = "org.lwjgl:lwjgl:3.3.1";
        var librariesDir = _testDirectory;
        var classifier = "natives-windows";

        // Act
        var result = _libraryManager.GetLibraryPath(libraryName, librariesDir, classifier);

        // Assert
        var expected = Path.Combine(librariesDir, "org", "lwjgl", "lwjgl", "3.3.1", "lwjgl-3.3.1-natives-windows.jar");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetLibraryPath_WithEmbeddedClassifier_ReturnsCorrectPath()
    {
        // Arrange
        var libraryName = "com.mojang:jtracy:1.0.36:natives-windows";
        var librariesDir = _testDirectory;

        // Act
        var result = _libraryManager.GetLibraryPath(libraryName, librariesDir);

        // Assert
        var expected = Path.Combine(librariesDir, "com", "mojang", "jtracy", "1.0.36", "jtracy-1.0.36-natives-windows.jar");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetLibraryPath_WithExtension_ReturnsCorrectPath()
    {
        // Arrange
        var libraryName = "net.neoforged:neoform:1.20.4@zip";
        var librariesDir = _testDirectory;

        // Act
        var result = _libraryManager.GetLibraryPath(libraryName, librariesDir);

        // Assert
        var expected = Path.Combine(librariesDir, "net", "neoforged", "neoform", "1.20.4", "neoform-1.20.4.zip");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetLibraryPath_InvalidFormat_ThrowsArgumentException()
    {
        // Arrange
        var libraryName = "invalid-library-name";
        var librariesDir = _testDirectory;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _libraryManager.GetLibraryPath(libraryName, librariesDir));
    }

    #endregion


    #region IsLibraryApplicable 测试

    [Fact]
    public void IsLibraryApplicable_NoRules_ReturnsTrue()
    {
        // Arrange
        var library = new Library { Name = "test:library:1.0" };

        // Act
        var result = _libraryManager.IsLibraryApplicable(library);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLibraryApplicable_EmptyRules_ReturnsTrue()
    {
        // Arrange
        var library = new Library 
        { 
            Name = "test:library:1.0",
            Rules = Array.Empty<LibraryRules>()
        };

        // Act
        var result = _libraryManager.IsLibraryApplicable(library);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLibraryApplicable_AllowWindowsRule_OnWindows_ReturnsTrue()
    {
        // Arrange
        var library = new Library
        {
            Name = "test:library:1.0",
            Rules = new[]
            {
                new LibraryRules
                {
                    Action = "allow",
                    Os = new LibraryOs { Name = "windows" }
                }
            }
        };

        // Act
        var result = _libraryManager.IsLibraryApplicable(library);

        // Assert - 在Windows上应该返回true
        // 注意：这个测试假设在Windows上运行
        if (OperatingSystem.IsWindows())
        {
            Assert.True(result);
        }
    }

    [Fact]
    public void IsLibraryApplicable_DisallowWindowsRule_OnWindows_ReturnsFalse()
    {
        // Arrange
        var library = new Library
        {
            Name = "test:library:1.0",
            Rules = new[]
            {
                new LibraryRules { Action = "allow" },
                new LibraryRules
                {
                    Action = "disallow",
                    Os = new LibraryOs { Name = "windows" }
                }
            }
        };

        // Act
        var result = _libraryManager.IsLibraryApplicable(library);

        // Assert - 在Windows上应该返回false
        if (OperatingSystem.IsWindows())
        {
            Assert.False(result);
        }
    }

    /// <summary>
    /// Issue #83: 当库仅有 allow+os:linux 规则时，在 Windows 上应排除（不下载 natives-linux）
    /// </summary>
    [Fact]
    public void IsLibraryApplicable_AllowLinuxOnly_OnWindows_ReturnsFalse()
    {
        // Arrange: 模拟 org.lwjgl:lwjgl:3.4.1:natives-linux 的规则
        var library = new Library
        {
            Name = "org.lwjgl:lwjgl:3.4.1:natives-linux",
            Rules = new[]
            {
                new LibraryRules
                {
                    Action = "allow",
                    Os = new LibraryOs { Name = "linux" }
                }
            }
        };

        // Act
        var result = _libraryManager.IsLibraryApplicable(library);

        // Assert: 在 Windows 上应返回 false（不适用，不应下载）
        if (OperatingSystem.IsWindows())
        {
            Assert.False(result);
        }
    }

    #endregion

    #region IsLibraryDownloaded 测试

    [Fact]
    public void IsLibraryDownloaded_FileExists_ReturnsTrue()
    {
        // Arrange
        var libraryName = "test:library:1.0";
        var librariesDir = _testDirectory;
        var libraryPath = _libraryManager.GetLibraryPath(libraryName, librariesDir);
        
        // 创建测试文件
        Directory.CreateDirectory(Path.GetDirectoryName(libraryPath)!);
        File.WriteAllText(libraryPath, "test content");

        var library = new Library
        {
            Name = libraryName,
            Downloads = new LibraryDownloads
            {
                Artifact = new DownloadFile
                {
                    Url = "https://example.com/library.jar"
                    // 没有SHA1，所以只检查文件存在
                }
            }
        };

        // Act
        var result = _libraryManager.IsLibraryDownloaded(library, librariesDir);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLibraryDownloaded_FileNotExists_ReturnsFalse()
    {
        // Arrange
        var library = new Library
        {
            Name = "test:nonexistent:1.0",
            Downloads = new LibraryDownloads
            {
                Artifact = new DownloadFile
                {
                    Url = "https://example.com/library.jar"
                }
            }
        };

        // Act
        var result = _libraryManager.IsLibraryDownloaded(library, _testDirectory);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLibraryDownloaded_NotApplicable_ReturnsTrue()
    {
        // Arrange - 创建一个只在Linux上适用的库（在Windows上不适用）
        var library = new Library
        {
            Name = "test:linux-only:1.0",
            Rules = new[]
            {
                // 默认禁止所有
                new LibraryRules { Action = "disallow" },
                // 只在Linux上允许
                new LibraryRules
                {
                    Action = "allow",
                    Os = new LibraryOs { Name = "linux" }
                }
            },
            Downloads = new LibraryDownloads
            {
                Artifact = new DownloadFile
                {
                    Url = "https://example.com/library.jar"
                }
            }
        };

        // Act
        var result = _libraryManager.IsLibraryDownloaded(library, _testDirectory);

        // Assert - 在Windows上，不适用的库应该返回true（因为不需要下载）
        if (OperatingSystem.IsWindows())
        {
            Assert.True(result);
        }
    }

    #endregion

    #region GetMissingLibraries 测试

    [Fact]
    public void GetMissingLibraries_AllExist_ReturnsEmpty()
    {
        // Arrange
        var libraryName = "test:existing:1.0";
        var libraryPath = _libraryManager.GetLibraryPath(libraryName, _testDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(libraryPath)!);
        File.WriteAllText(libraryPath, "test content");

        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library>
            {
                new Library
                {
                    Name = libraryName,
                    Downloads = new LibraryDownloads
                    {
                        Artifact = new DownloadFile { Url = "https://example.com/library.jar" }
                    }
                }
            }
        };

        // Act
        var result = _libraryManager.GetMissingLibraries(versionInfo, _testDirectory);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetMissingLibraries_SomeMissing_ReturnsMissingOnly()
    {
        // Arrange
        var existingLibraryName = "test:existing:1.0";
        var missingLibraryName = "test:missing:1.0";
        
        var existingPath = _libraryManager.GetLibraryPath(existingLibraryName, _testDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(existingPath)!);
        File.WriteAllText(existingPath, "test content");

        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library>
            {
                new Library
                {
                    Name = existingLibraryName,
                    Downloads = new LibraryDownloads
                    {
                        Artifact = new DownloadFile { Url = "https://example.com/existing.jar" }
                    }
                },
                new Library
                {
                    Name = missingLibraryName,
                    Downloads = new LibraryDownloads
                    {
                        Artifact = new DownloadFile { Url = "https://example.com/missing.jar" }
                    }
                }
            }
        };

        // Act
        var result = new List<Library>(_libraryManager.GetMissingLibraries(versionInfo, _testDirectory));

        // Assert
        Assert.Single(result);
        Assert.Equal(missingLibraryName, result[0].Name);
    }

    [Fact]
    public void GetMissingLibraries_NullVersionInfo_ReturnsEmpty()
    {
        // Act
        var result = _libraryManager.GetMissingLibraries(null!, _testDirectory);

        // Assert
        Assert.Empty(result);
    }

    #endregion


    #region DownloadLibrariesAsync 测试

    [Fact]
    public async Task DownloadLibrariesAsync_NoLibraries_CompletesImmediately()
    {
        // Arrange
        var versionInfo = new VersionInfo { Libraries = null };
        double? reportedProgress = null;

        // Act
        await _libraryManager.DownloadLibrariesAsync(
            versionInfo, 
            _testDirectory, 
            p => reportedProgress = p);

        // Assert
        Assert.Equal(100, reportedProgress);
        _mockDownloadManager.Verify(
            m => m.DownloadFilesAsync(
                It.IsAny<IEnumerable<DownloadTask>>(),
                It.IsAny<int>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DownloadLibrariesAsync_AllExist_SkipsDownload()
    {
        // Arrange
        var libraryName = "test:existing:1.0";
        var libraryPath = _libraryManager.GetLibraryPath(libraryName, _testDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(libraryPath)!);
        File.WriteAllText(libraryPath, "test content");

        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library>
            {
                new Library
                {
                    Name = libraryName,
                    Downloads = new LibraryDownloads
                    {
                        Artifact = new DownloadFile { Url = "https://example.com/library.jar" }
                    }
                }
            }
        };

        double? reportedProgress = null;

        // Act
        await _libraryManager.DownloadLibrariesAsync(
            versionInfo, 
            _testDirectory, 
            p => reportedProgress = p);

        // Assert
        Assert.Equal(100, reportedProgress);
        _mockDownloadManager.Verify(
            m => m.DownloadFilesAsync(
                It.IsAny<IEnumerable<DownloadTask>>(),
                It.IsAny<int>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DownloadLibrariesAsync_MissingLibraries_CallsDownloadManager()
    {
        // Arrange
        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library>
            {
                new Library
                {
                    Name = "test:missing:1.0",
                    Downloads = new LibraryDownloads
                    {
                        Artifact = new DownloadFile 
                        { 
                            Url = "https://example.com/library.jar",
                            Sha1 = "abc123"
                        }
                    }
                }
            }
        };

        _mockDownloadManager
            .Setup(m => m.DownloadFilesAsync(
                It.IsAny<IEnumerable<DownloadTask>>(),
                It.IsAny<int>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadResult>
            {
                DownloadResult.Succeeded("test.jar", "https://example.com/library.jar")
            });

        // Act
        await _libraryManager.DownloadLibrariesAsync(versionInfo, _testDirectory);

        // Assert
        _mockDownloadManager.Verify(
            m => m.DownloadFilesAsync(
                It.Is<IEnumerable<DownloadTask>>(tasks => 
                    tasks.Any(t => t.Url == "https://example.com/library.jar")),
                4, // 默认并发数
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadLibrariesAsync_Cancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library>
            {
                new Library
                {
                    Name = "test:library:1.0",
                    Downloads = new LibraryDownloads
                    {
                        Artifact = new DownloadFile { Url = "https://example.com/library.jar" }
                    }
                }
            }
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _libraryManager.DownloadLibrariesAsync(versionInfo, _testDirectory, cancellationToken: cts.Token));
    }

    /// <summary>
    /// Issue #83: 不应下载其他 OS 的原生库。验证当库包含 Windows/Linux/Osx 三类原生库时，
    /// 仅下载当前平台对应的原生库，不下载其他平台的。
    /// </summary>
    [Fact]
    public async Task DownloadLibrariesAsync_NativeLibraryWithAllPlatforms_DownloadsOnlyCurrentPlatform()
    {
        // Arrange: 库包含全平台 Classifiers
        const string windowsUrl = "https://example.com/natives-windows.jar";
        const string linuxUrl = "https://example.com/natives-linux.jar";
        const string osxUrl = "https://example.com/natives-osx.jar";

        var currentOs = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
            : "windows";
        var expectedUrl = currentOs switch { "windows" => windowsUrl, "linux" => linuxUrl, _ => osxUrl };
        var otherOsUrls = new[] { windowsUrl, linuxUrl, osxUrl }.Where(u => u != expectedUrl).ToList();

        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library>
            {
                new Library
                {
                    Name = "org.lwjgl:lwjgl-platform:3.3.1",
                    Natives = new LibraryNative
                    {
                        Windows = "natives-windows",
                        Linux = "natives-linux",
                        Osx = "natives-osx"
                    },
                    Downloads = new LibraryDownloads
                    {
                        Classifiers = new Dictionary<string, DownloadFile>
                        {
                            ["natives-windows"] = new DownloadFile { Url = windowsUrl, Sha1 = "a" },
                            ["natives-linux"] = new DownloadFile { Url = linuxUrl, Sha1 = "b" },
                            ["natives-osx"] = new DownloadFile { Url = osxUrl, Sha1 = "c" }
                        }
                    }
                }
            }
        };

        IEnumerable<DownloadTask>? capturedTasks = null;
        _mockDownloadManager
            .Setup(m => m.DownloadFilesAsync(
                It.IsAny<IEnumerable<DownloadTask>>(),
                It.IsAny<int>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DownloadTask>, int, Action<DownloadProgressStatus>, CancellationToken>(
                (tasks, _, _, _) => capturedTasks = tasks.ToList())
            .ReturnsAsync(new List<DownloadResult>
            {
                DownloadResult.Succeeded(Path.Combine(_testDirectory, "natives.jar"), expectedUrl)
            });

        // Act
        await _libraryManager.DownloadLibrariesAsync(versionInfo, _testDirectory);

        // Assert: 仅应下载当前 OS 的原生库，不应包含其他 OS 的 URL
        Assert.NotNull(capturedTasks);
        var taskList = capturedTasks.ToList();
        var urls = taskList.Select(t => t.Url).ToList();

        Assert.Contains(expectedUrl, urls);
        foreach (var otherUrl in otherOsUrls)
        {
            Assert.DoesNotContain(otherUrl, urls);
        }
        Assert.Single(taskList);
    }

    /// <summary>
    /// 与官启一致：当前 OS 下全架构 natives 均需下载（如 natives-windows, natives-windows-x64, natives-windows-arm64）
    /// </summary>
    [Fact]
    public async Task DownloadLibrariesAsync_NativeLibraryWithAllArchs_DownloadsAllForCurrentOs()
    {
        const string nativesWindowsUrl = "https://example.com/natives-windows.jar";
        const string nativesX64Url = "https://example.com/natives-windows-x64.jar";
        const string nativesArm64Url = "https://example.com/natives-windows-arm64.jar";
        const string nativesLinuxUrl = "https://example.com/natives-linux.jar";

        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library>
            {
                new Library
                {
                    Name = "org.lwjgl:lwjgl:3.4.1",
                    Natives = new LibraryNative { Windows = "natives-windows", Linux = "natives-linux", Osx = "natives-osx" },
                    Downloads = new LibraryDownloads
                    {
                        Classifiers = new Dictionary<string, DownloadFile>
                        {
                            ["natives-windows"] = new DownloadFile { Url = nativesWindowsUrl, Sha1 = "a" },
                            ["natives-windows-x64"] = new DownloadFile { Url = nativesX64Url, Sha1 = "b" },
                            ["natives-windows-arm64"] = new DownloadFile { Url = nativesArm64Url, Sha1 = "c" },
                            ["natives-linux"] = new DownloadFile { Url = nativesLinuxUrl, Sha1 = "d" }
                        }
                    }
                }
            }
        };

        IEnumerable<DownloadTask>? capturedTasks = null;
        _mockDownloadManager
            .Setup(m => m.DownloadFilesAsync(
                It.IsAny<IEnumerable<DownloadTask>>(),
                It.IsAny<int>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DownloadTask>, int, Action<DownloadProgressStatus>, CancellationToken>(
                (tasks, _, _, _) => capturedTasks = tasks.ToList())
            .ReturnsAsync(new List<DownloadResult>());

        await _libraryManager.DownloadLibrariesAsync(versionInfo, _testDirectory);

        Assert.NotNull(capturedTasks);
        var urls = capturedTasks.Select(t => t.Url).ToList();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Contains(nativesWindowsUrl, urls);
            Assert.Contains(nativesX64Url, urls);
            Assert.Contains(nativesArm64Url, urls);
            Assert.DoesNotContain(nativesLinuxUrl, urls);
            Assert.Equal(3, urls.Count);
        }
    }

    #endregion

    #region ExtractNativeLibrariesAsync 测试

    [Fact]
    public async Task ExtractNativeLibrariesAsync_NoLibraries_CompletesWithoutError()
    {
        // Arrange
        var versionInfo = new VersionInfo { Libraries = null };
        var nativesDir = Path.Combine(_testDirectory, "natives");

        // Act
        await _libraryManager.ExtractNativeLibrariesAsync(versionInfo, _testDirectory, nativesDir);

        // Assert - 应该创建natives目录
        Assert.True(Directory.Exists(nativesDir));
    }

    [Fact]
    public async Task ExtractNativeLibrariesAsync_EmptyLibraries_CompletesWithoutError()
    {
        // Arrange
        var versionInfo = new VersionInfo { Libraries = new List<Library>() };
        var nativesDir = Path.Combine(_testDirectory, "natives");

        // Act
        await _libraryManager.ExtractNativeLibrariesAsync(versionInfo, _testDirectory, nativesDir);

        // Assert
        Assert.True(Directory.Exists(nativesDir));
    }

    #endregion
}
