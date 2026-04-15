using System.IO.Compression;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public sealed class PathContainmentHelperTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "path-containment-tests", Guid.NewGuid().ToString("N"));

    public PathContainmentHelperTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, true);
        }
    }

    [Fact]
    public void GetValidatedPathUnderRoot_WhenRelativePathIsSafe_ShouldResolveWithinRoot()
    {
        string resolvedPath = PathContainmentHelper.GetValidatedPathUnderRoot(
            _tempRoot,
            Path.Combine("mods", "example.jar"),
            "test path");

        resolvedPath.Should().Be(Path.Combine(_tempRoot, "mods", "example.jar"));
    }

    [Theory]
    [InlineData("..\\evil.jar")]
    [InlineData("mods\\..\\evil.jar")]
    [InlineData("/absolute/path.jar")]
    [InlineData("C:\\evil.jar")]
    public void GetValidatedPathUnderRoot_WhenRelativePathEscapesRoot_ShouldThrow(string relativePath)
    {
        Action act = () => PathContainmentHelper.GetValidatedPathUnderRoot(
            _tempRoot,
            relativePath,
            "test path");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ExtractToDirectorySafely_WhenArchiveHasSingleRootFolder_ShouldStripRootFolder()
    {
        string zipPath = Path.Combine(_tempRoot, "world.zip");
        string destinationDirectory = Path.Combine(_tempRoot, "extract");

        using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "World/level.dat", "level");
            WriteEntry(archive, "World/region/r.0.0.mca", "region");
        }

        ZipExtractionHelper.ExtractToDirectorySafely(
            zipPath,
            destinationDirectory,
            stripSingleRootDirectory: true,
            entryPathDescription: "test archive path");

        File.Exists(Path.Combine(destinationDirectory, "level.dat")).Should().BeTrue();
        File.Exists(Path.Combine(destinationDirectory, "region", "r.0.0.mca")).Should().BeTrue();
        Directory.Exists(Path.Combine(destinationDirectory, "World")).Should().BeFalse();
    }

    [Fact]
    public void ExtractToDirectorySafely_WhenEntryEscapesRoot_ShouldThrowAndNotWriteOutsideDestination()
    {
        string zipPath = Path.Combine(_tempRoot, "world-malicious.zip");
        string destinationDirectory = Path.Combine(_tempRoot, "extract-malicious");
        string escapedFilePath = Path.Combine(_tempRoot, "evil.txt");

        using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "World/level.dat", "level");
            WriteEntry(archive, "World/../../evil.txt", "evil");
        }

        Action act = () => ZipExtractionHelper.ExtractToDirectorySafely(
            zipPath,
            destinationDirectory,
            stripSingleRootDirectory: true,
            entryPathDescription: "test archive path");

        act.Should().Throw<InvalidOperationException>();
        File.Exists(escapedFilePath).Should().BeFalse();
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}