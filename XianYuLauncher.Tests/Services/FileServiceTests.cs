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

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}