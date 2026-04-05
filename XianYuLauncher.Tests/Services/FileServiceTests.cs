using FluentAssertions;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class FileServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileService _fileService = new();

    public FileServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "file-service-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void Read_WhenJsonIsInvalid_ShouldReturnDefault()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "invalid.json"), "{ not-valid-json }");

        var result = _fileService.Read<LauncherAIWorkspaceStorageModel>(_testDirectory, "invalid.json");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_WhenJsonIsInvalid_ShouldReturnDefault()
    {
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "invalid-async.json"), "{ not-valid-json }");

        var result = await _fileService.ReadAsync<LauncherAIWorkspaceStorageModel>(_testDirectory, "invalid-async.json");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Save_WhenConcurrentWritesTargetSameFile_ShouldPersistValidJson()
    {
        TaskCompletionSource<bool> startSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task[] tasks = Enumerable.Range(0, 8)
            .Select(index => Task.Run(async () =>
            {
                await startSignal.Task;

                for (int iteration = 0; iteration < 10; iteration++)
                {
                    _fileService.Save(_testDirectory, "shared.json", new Dictionary<string, int>
                    {
                        ["Index"] = index,
                        ["Iteration"] = iteration,
                    });
                }
            }))
            .ToArray();

        startSignal.SetResult(true);
        await Task.WhenAll(tasks);

        Dictionary<string, int>? result = await _fileService.ReadAsync<Dictionary<string, int>>(_testDirectory, "shared.json");

        result.Should().NotBeNull();
        result.Should().ContainKey("Index");
        result.Should().ContainKey("Iteration");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}