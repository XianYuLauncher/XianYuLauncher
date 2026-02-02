using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

/// <summary>
/// DownloadTaskManager 单元测试
/// </summary>
public class DownloadTaskManagerTests
{
    private readonly Mock<IMinecraftVersionService> _minecraftVersionServiceMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ILogger<DownloadTaskManager>> _loggerMock;
    private readonly Mock<IDownloadManager> _downloadManagerMock;
    private readonly DownloadTaskManager _downloadTaskManager;

    public DownloadTaskManagerTests()
    {
        _minecraftVersionServiceMock = new Mock<IMinecraftVersionService>();
        _fileServiceMock = new Mock<IFileService>();
        _loggerMock = new Mock<ILogger<DownloadTaskManager>>();
        _downloadManagerMock = new Mock<IDownloadManager>();
        _downloadManagerMock = new Mock<IDownloadManager>();

        _fileServiceMock.Setup(f => f.GetMinecraftDataPath())
            .Returns(Path.Combine(Path.GetTempPath(), "minecraft_test"));

        _downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object,
            _downloadManagerMock.Object);
    }

    [Fact]
    public void CurrentTask_Initially_ShouldBeNull()
    {
        // Assert
        _downloadTaskManager.CurrentTask.Should().BeNull();
    }

    [Fact]
    public void HasActiveDownload_Initially_ShouldBeFalse()
    {
        // Assert
        _downloadTaskManager.HasActiveDownload.Should().BeFalse();
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_ShouldCreateTask()
    {
        // Arrange
        var stateChanges = new List<DownloadTaskInfo>();
        _downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(new DownloadTaskInfo
        {
            TaskId = task.TaskId,
            TaskName = task.TaskName,
            State = task.State,
            Progress = task.Progress,
            StatusMessage = task.StatusMessage
        });

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");
        await Task.Delay(50); // 等待后台任务完成

        // Assert
        stateChanges.Should().NotBeEmpty();
        var firstState = stateChanges.First();
        firstState.TaskName.Should().Be("MyVersion");
        firstState.State.Should().Be(DownloadTaskState.Downloading);
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_WhenDownloadActive_ShouldThrow()
    {
        // Arrange
        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<string>()))
            .Returns(async () => await Task.Delay(5000)); // 模拟长时间下载

        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "Version1");

        // Act & Assert
        var act = async () => await _downloadTaskManager.StartVanillaDownloadAsync("1.20.2", "Version2");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*已有下载任务正在进行中*");
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_OnComplete_ShouldUpdateState()
    {
        // Arrange
        var stateChanges = new List<DownloadTaskState>();
        _downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(task.State);

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");
        
        // 等待后台任务完成
        await Task.Delay(100);

        // Assert
        stateChanges.Should().Contain(DownloadTaskState.Downloading);
        stateChanges.Should().Contain(DownloadTaskState.Completed);
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_OnFailure_ShouldSetFailedState()
    {
        // Arrange
        DownloadTaskInfo? finalTask = null;
        _downloadTaskManager.TaskStateChanged += (_, task) => finalTask = task;

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<string>()))
            .ThrowsAsync(new Exception("下载失败"));

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");
        
        // 等待后台任务完成
        await Task.Delay(100);

        // Assert
        finalTask.Should().NotBeNull();
        finalTask!.State.Should().Be(DownloadTaskState.Failed);
        finalTask.ErrorMessage.Should().Contain("下载失败");
    }

    [Fact]
    public async Task CancelCurrentDownload_ShouldSetCancelledState()
    {
        // Arrange
        DownloadTaskInfo? finalTask = null;
        _downloadTaskManager.TaskStateChanged += (_, task) => finalTask = task;

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<string>()))
            .Returns(async () => await Task.Delay(5000));

        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");

        // Act
        _downloadTaskManager.CancelCurrentDownload();

        // Assert
        finalTask.Should().NotBeNull();
        finalTask!.State.Should().Be(DownloadTaskState.Cancelled);
    }

    [Fact]
    public void CancelCurrentDownload_WhenNoActiveDownload_ShouldNotThrow()
    {
        // Act & Assert
        var act = () => _downloadTaskManager.CancelCurrentDownload();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task TaskProgressChanged_ShouldBeRaised()
    {
        // Arrange
        var progressValues = new List<double>();
        _downloadTaskManager.TaskProgressChanged += (_, task) => progressValues.Add(task.Progress);

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<string>()))
            .Callback<string, string, Action<double>, string>((_, _, callback, _) =>
            {
                callback?.Invoke(25);
                callback?.Invoke(50);
                callback?.Invoke(75);
                callback?.Invoke(100);
            })
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");
        
        // 等待后台任务完成
        await Task.Delay(100);

        // Assert
        progressValues.Should().Contain(25);
        progressValues.Should().Contain(50);
        progressValues.Should().Contain(75);
        progressValues.Should().Contain(100);
    }

    [Fact]
    public async Task StartModLoaderDownloadAsync_ShouldCreateTask()
    {
        // Arrange
        var stateChanges = new List<DownloadTaskInfo>();
        _downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(new DownloadTaskInfo
        {
            TaskId = task.TaskId,
            TaskName = task.TaskName,
            State = task.State,
            Progress = task.Progress,
            StatusMessage = task.StatusMessage
        });

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadModLoaderVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartModLoaderDownloadAsync("1.20.1", "Fabric", "0.15.0", "MyFabricVersion");
        await Task.Delay(50); // 等待后台任务完成

        // Assert
        stateChanges.Should().NotBeEmpty();
        var firstState = stateChanges.First();
        firstState.TaskName.Should().Be("MyFabricVersion");
        firstState.State.Should().Be(DownloadTaskState.Downloading);
    }
}


/// <summary>
/// 资源下载功能测试
/// </summary>
public class DownloadTaskManagerResourceDownloadTests
{
    private readonly Mock<IMinecraftVersionService> _minecraftVersionServiceMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ILogger<DownloadTaskManager>> _loggerMock;
    private readonly string _tempDirectory;

    public DownloadTaskManagerResourceDownloadTests()
    {
        _minecraftVersionServiceMock = new Mock<IMinecraftVersionService>();
        _fileServiceMock = new Mock<IFileService>();
        _loggerMock = new Mock<ILogger<DownloadTaskManager>>();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"resource_download_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _fileServiceMock.Setup(f => f.GetMinecraftDataPath())
            .Returns(_tempDirectory);
    }

    /// <summary>
    /// 测试资源下载任务创建
    /// Property 1: 资源下载任务创建
    /// Validates: Requirements 1.2, 2.1, 2.2
    /// </summary>
    [Fact]
    public async Task StartResourceDownloadAsync_ShouldCreateTaskWithCorrectState()
    {
        // Arrange
        var downloadManagerMock = new Mock<IDownloadManager>();
        var downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object, 
            _fileServiceMock.Object, 
            _loggerMock.Object, 
            downloadManagerMock.Object);

        var stateChanges = new List<DownloadTaskInfo>();
        downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(new DownloadTaskInfo
        {
            TaskId = task.TaskId,
            TaskName = task.TaskName,
            VersionName = task.VersionName,
            State = task.State,
            Progress = task.Progress,
            StatusMessage = task.StatusMessage
        });

        var savePath = Path.Combine(_tempDirectory, "test_mod.jar");

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod",
            "mod",
            "https://example.com/test.jar",
            savePath);
        
        await Task.Delay(100); // 等待后台任务

        // Assert
        stateChanges.Should().NotBeEmpty();
        var firstState = stateChanges.First();
        firstState.TaskName.Should().Be("Test Mod");
        firstState.VersionName.Should().Be("mod");
        firstState.State.Should().Be(DownloadTaskState.Downloading);
        firstState.Progress.Should().Be(0);
    }

    /// <summary>
    /// 测试下载完成通知
    /// Property 3: 下载完成通知
    /// Validates: Requirements 1.5, 5.1
    /// </summary>
    [Fact]
    public async Task StartResourceDownloadAsync_OnComplete_ShouldRaiseTaskStateChangedWithCompleted()
    {
        // Arrange
        var downloadManagerMock = new Mock<IDownloadManager>();
        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        var stateChanges = new List<DownloadTaskState>();
        downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(task.State);

        var savePath = Path.Combine(_tempDirectory, "test_mod_complete.jar");

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod",
            "mod",
            "https://example.com/test.jar",
            savePath);
        
        await Task.Delay(200); // 等待后台任务完成

        // Assert
        stateChanges.Should().Contain(DownloadTaskState.Downloading);
        stateChanges.Should().Contain(DownloadTaskState.Completed);
    }

    /// <summary>
    /// 测试下载失败通知
    /// Property 3: 下载完成通知（失败情况）
    /// Validates: Requirements 2.5, 5.2
    /// </summary>
    [Fact]
    public async Task StartResourceDownloadAsync_OnFailure_ShouldRaiseTaskStateChangedWithFailed()
    {
        // Arrange
        var downloadManagerMock = new Mock<IDownloadManager>();
        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        DownloadTaskInfo? finalTask = null;
        downloadTaskManager.TaskStateChanged += (_, task) => finalTask = task;

        var savePath = Path.Combine(_tempDirectory, "test_mod_fail.jar");

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod",
            "mod",
            "https://example.com/notfound.jar",
            savePath);
        
        await Task.Delay(200); // 等待后台任务完成

        // Assert
        finalTask.Should().NotBeNull();
        finalTask!.State.Should().Be(DownloadTaskState.Failed);
        finalTask.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// 测试已有下载时启动新下载应抛出异常
    /// Validates: Requirements 1.2
    /// </summary>
    [Fact]
    public async Task StartResourceDownloadAsync_WhenDownloadActive_ShouldThrow()
    {
        // Arrange
        var downloadManagerMock = new Mock<IDownloadManager>();
        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        var savePath1 = Path.Combine(_tempDirectory, "test_mod1.jar");
        var savePath2 = Path.Combine(_tempDirectory, "test_mod2.jar");

        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod 1",
            "mod",
            "https://example.com/test1.jar",
            savePath1);

        // Act & Assert
        var act = async () => await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod 2",
            "mod",
            "https://example.com/test2.jar",
            savePath2);
        
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*已有下载任务正在进行中*");
    }

    /// <summary>
    /// 测试进度更新事件
    /// Validates: Requirements 2.3
    /// </summary>
    [Fact]
    public async Task StartResourceDownloadAsync_ShouldRaiseProgressEvents()
    {
        // Arrange
        var content = new byte[1000];
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new ByteArrayContent(content)
            {
                Headers = { ContentLength = content.Length }
            }
        });
        var downloadManagerMock = new Mock<IDownloadManager>();
        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        var progressValues = new List<double>();
        downloadTaskManager.TaskProgressChanged += (_, task) => progressValues.Add(task.Progress);

        var savePath = Path.Combine(_tempDirectory, "test_mod_progress.jar");

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod",
            "mod",
            "https://example.com/test.jar",
            savePath);
        
        await Task.Delay(200); // 等待后台任务完成

        // Assert
        progressValues.Should().NotBeEmpty();
        progressValues.Should().Contain(p => p >= 0 && p <= 100);
    }

    /// <summary>
    /// Mock HTTP 消息处理器
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    /// <summary>
    /// 慢速流，用于模拟长时间下载
    /// </summary>
    private class SlowStream : Stream
    {
        private readonly int _delayMs;
        private int _position;
        private readonly int _length = 10000;

        public SlowStream(int delayMs)
        {
            _delayMs = delayMs;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _position; set => _position = (int)value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Thread.Sleep(_delayMs);
            var bytesToRead = Math.Min(count, _length - _position);
            _position += bytesToRead;
            return bytesToRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}


/// <summary>
/// 世界下载功能测试
/// </summary>
public class DownloadTaskManagerWorldDownloadTests : IDisposable
{
    private readonly Mock<IMinecraftVersionService> _minecraftVersionServiceMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ILogger<DownloadTaskManager>> _loggerMock;
    private readonly string _tempDirectory;

    public DownloadTaskManagerWorldDownloadTests()
    {
        _minecraftVersionServiceMock = new Mock<IMinecraftVersionService>();
        _fileServiceMock = new Mock<IFileService>();
        _loggerMock = new Mock<ILogger<DownloadTaskManager>>();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"world_download_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _fileServiceMock.Setup(f => f.GetMinecraftDataPath())
            .Returns(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch { }
    }

    /// <summary>
    /// 创建测试用的zip文件内容
    /// </summary>
    private byte[] CreateTestZipContent(string worldFolderName = "TestWorld")
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            // 创建世界文件夹结构
            var levelDat = archive.CreateEntry($"{worldFolderName}/level.dat");
            using (var writer = new StreamWriter(levelDat.Open()))
            {
                writer.Write("test level data");
            }

            var regionFile = archive.CreateEntry($"{worldFolderName}/region/r.0.0.mca");
            using (var writer = new StreamWriter(regionFile.Open()))
            {
                writer.Write("test region data");
            }
        }
        return memoryStream.ToArray();
    }

    /// <summary>
    /// 测试世界下载任务创建
    /// </summary>
    [Fact]
    public async Task StartWorldDownloadAsync_ShouldCreateTaskWithCorrectState()
    {
        // Arrange
        var zipContent = CreateTestZipContent();
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new ByteArrayContent(zipContent)
            {
                Headers = { ContentLength = zipContent.Length }
            }
        });
        var downloadManagerMock = new Mock<IDownloadManager>();
        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        var stateChanges = new List<DownloadTaskInfo>();
        downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(new DownloadTaskInfo
        {
            TaskId = task.TaskId,
            TaskName = task.TaskName,
            VersionName = task.VersionName,
            State = task.State,
            Progress = task.Progress,
            StatusMessage = task.StatusMessage
        });

        var savesDir = Path.Combine(_tempDirectory, "saves");

        // Act
        await downloadTaskManager.StartWorldDownloadAsync(
            "Test World",
            "https://example.com/world.zip",
            savesDir,
            "TestWorld.zip");
        
        await Task.Delay(100); // 等待后台任务

        // Assert
        stateChanges.Should().NotBeEmpty();
        var firstState = stateChanges.First();
        firstState.TaskName.Should().Be("Test World");
        firstState.VersionName.Should().Be("world");
        firstState.State.Should().Be(DownloadTaskState.Downloading);
    }

    /// <summary>
    /// 测试世界下载完成后状态更新
    /// </summary>
    [Fact]
    public async Task StartWorldDownloadAsync_OnComplete_ShouldSetCompletedState()
    {
        // Arrange
        var zipContent = CreateTestZipContent();
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new ByteArrayContent(zipContent)
            {
                Headers = { ContentLength = zipContent.Length }
            }
        });
        var downloadManagerMock = new Mock<IDownloadManager>();
        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        var stateChanges = new List<DownloadTaskState>();
        downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(task.State);

        var savesDir = Path.Combine(_tempDirectory, "saves");

        // Act
        await downloadTaskManager.StartWorldDownloadAsync(
            "Test World",
            "https://example.com/world.zip",
            savesDir,
            "TestWorld.zip");
        
        await Task.Delay(500); // 等待后台任务完成

        // Assert
        stateChanges.Should().Contain(DownloadTaskState.Downloading);
        stateChanges.Should().Contain(DownloadTaskState.Completed);
    }

    /// <summary>
    /// 测试世界下载失败时状态更新
    /// </summary>
    [Fact]
    public async Task StartWorldDownloadAsync_OnFailure_ShouldSetFailedState()
    {
        // Arrange
        var downloadManagerMock = new Mock<IDownloadManager>();
        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        DownloadTaskInfo? finalTask = null;
        downloadTaskManager.TaskStateChanged += (_, task) => finalTask = task;

        var savesDir = Path.Combine(_tempDirectory, "saves");

        // Act
        await downloadTaskManager.StartWorldDownloadAsync(
            "Test World",
            "https://example.com/notfound.zip",
            savesDir,
            "TestWorld.zip");
        
        await Task.Delay(200); // 等待后台任务完成

        // Assert
        finalTask.Should().NotBeNull();
        finalTask!.State.Should().Be(DownloadTaskState.Failed);
        finalTask.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// 测试已有下载时启动世界下载应抛出异常
    /// </summary>
    [Fact]
    public async Task StartWorldDownloadAsync_WhenDownloadActive_ShouldThrow()
    {
        // Arrange
        var downloadManagerMock = new Mock<IDownloadManager>();
        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        var savesDir = Path.Combine(_tempDirectory, "saves");

        await downloadTaskManager.StartWorldDownloadAsync(
            "Test World 1",
            "https://example.com/world1.zip",
            savesDir,
            "TestWorld1.zip");

        // Act & Assert
        var act = async () => await downloadTaskManager.StartWorldDownloadAsync(
            "Test World 2",
            "https://example.com/world2.zip",
            savesDir,
            "TestWorld2.zip");
        
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*已有下载任务正在进行中*");
    }

    /// <summary>
    /// 测试世界下载进度更新（下载阶段0-70%，解压阶段70-100%）
    /// </summary>
    [Fact]
    public async Task StartWorldDownloadAsync_ShouldRaiseProgressEvents()
    {
        // Arrange
        var zipContent = CreateTestZipContent();
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new ByteArrayContent(zipContent)
            {
                Headers = { ContentLength = zipContent.Length }
            }
        });
        var downloadManagerMock = new Mock<IDownloadManager>();
        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        var progressValues = new List<double>();
        downloadTaskManager.TaskProgressChanged += (_, task) => progressValues.Add(task.Progress);

        var savesDir = Path.Combine(_tempDirectory, "saves");

        // Act
        await downloadTaskManager.StartWorldDownloadAsync(
            "Test World",
            "https://example.com/world.zip",
            savesDir,
            "TestWorld.zip");
        
        await Task.Delay(500); // 等待后台任务完成

        // Assert
        progressValues.Should().NotBeEmpty();
        // 应该有下载阶段的进度（0-70%）和解压阶段的进度（70-100%）
        progressValues.Should().Contain(p => p >= 0 && p <= 70); // 下载阶段
        progressValues.Should().Contain(p => p >= 70 && p <= 100); // 解压阶段
    }

    /// <summary>
    /// Mock HTTP 消息处理器
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    /// <summary>
    /// 慢速流，用于模拟长时间下载
    /// </summary>
    private class SlowStream : Stream
    {
        private readonly int _delayMs;
        private int _position;
        private readonly int _length = 10000;

        public SlowStream(int delayMs)
        {
            _delayMs = delayMs;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _position; set => _position = (int)value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Thread.Sleep(_delayMs);
            var bytesToRead = Math.Min(count, _length - _position);
            _position += bytesToRead;
            return bytesToRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
