using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public sealed class TarGzExtractionHelperTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "tar-gz-extraction-tests", Guid.NewGuid().ToString("N"));

    public TarGzExtractionHelperTests()
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
    public async Task ExtractToDirectoryAsync_WhenArchiveContainsRegularFiles_ShouldExtractFiles()
    {
        string archivePath = Path.Combine(_tempRoot, "terracotta.tar.gz");
        string destinationDirectory = Path.Combine(_tempRoot, "extract");

        CreateTarGzArchive(archivePath, writer =>
        {
            writer.WriteEntry(new PaxTarEntry(TarEntryType.Directory, "bin"));
            writer.WriteEntry(CreateRegularFileEntry("bin/terracotta.exe", "exe"));
            writer.WriteEntry(CreateRegularFileEntry("VCRUNTIME140.DLL", "dll"));
        });

        await TarGzExtractionHelper.ExtractToDirectoryAsync(
            archivePath,
            destinationDirectory,
            "test archive path");

        File.ReadAllText(Path.Combine(destinationDirectory, "bin", "terracotta.exe")).Should().Be("exe");
        File.ReadAllText(Path.Combine(destinationDirectory, "VCRUNTIME140.DLL")).Should().Be("dll");
    }

    [Fact]
    public async Task ExtractToDirectoryAsync_WhenArchiveContainsEmptyFile_ShouldCreateEmptyFile()
    {
        string archivePath = Path.Combine(_tempRoot, "empty-file.tar.gz");
        string destinationDirectory = Path.Combine(_tempRoot, "extract-empty-file");

        CreateTarGzArchive(archivePath, writer =>
        {
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "empty.txt"));
        });

        await TarGzExtractionHelper.ExtractToDirectoryAsync(
            archivePath,
            destinationDirectory,
            "test archive path");

        string extractedFilePath = Path.Combine(destinationDirectory, "empty.txt");
        File.Exists(extractedFilePath).Should().BeTrue();
        new FileInfo(extractedFilePath).Length.Should().Be(0);
    }

    [Fact]
    public async Task ExtractToDirectoryAsync_WhenEntryEscapesDestination_ShouldThrowAndNotWriteOutsideRoot()
    {
        string archivePath = Path.Combine(_tempRoot, "malicious.tar.gz");
        string destinationDirectory = Path.Combine(_tempRoot, "extract-malicious");
        string escapedFilePath = Path.Combine(_tempRoot, "evil.txt");

        CreateTarGzArchive(archivePath, writer =>
        {
            writer.WriteEntry(CreateRegularFileEntry("bin/terracotta.exe", "safe"));
            writer.WriteEntry(CreateRegularFileEntry("../evil.txt", "evil"));
        });

        Func<Task> act = () => TarGzExtractionHelper.ExtractToDirectoryAsync(
            archivePath,
            destinationDirectory,
            "test archive path");

        await act.Should().ThrowAsync<InvalidOperationException>();
        File.Exists(escapedFilePath).Should().BeFalse();
    }

    [Fact]
    public async Task ExtractToDirectoryAsync_WhenArchiveContainsSymbolicLink_ShouldThrow()
    {
        string archivePath = Path.Combine(_tempRoot, "symbolic-link.tar.gz");
        string destinationDirectory = Path.Combine(_tempRoot, "extract-symbolic-link");

        CreateTarGzArchive(archivePath, writer =>
        {
            var entry = new PaxTarEntry(TarEntryType.SymbolicLink, "link")
            {
                LinkName = "../outside"
            };

            writer.WriteEntry(entry);
        });

        Func<Task> act = () => TarGzExtractionHelper.ExtractToDirectoryAsync(
            archivePath,
            destinationDirectory,
            "test archive path");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*不支持链接条目*");
    }

    [Fact]
    public async Task ExtractFileAsync_WhenEntryHasLengthButNoDataStream_ShouldThrowWithoutLeavingPartialFile()
    {
        string destinationDirectory = Path.Combine(_tempRoot, "extract-broken");
        string brokenFilePath = Path.Combine(destinationDirectory, "broken.txt");
        var entry = new PaxTarEntry(TarEntryType.RegularFile, "broken.txt");

        SetEntryLength(entry, 5);

        Func<Task> act = () => InvokeExtractFileAsync(destinationDirectory, entry, "test archive path");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*缺少数据流*");
        File.Exists(brokenFilePath).Should().BeFalse();
    }

    private static void CreateTarGzArchive(string archivePath, Action<TarWriter> writeEntries)
    {
        using FileStream fileStream = File.Create(archivePath);
        using GZipStream gzipStream = new(fileStream, CompressionLevel.SmallestSize);
        using TarWriter writer = new(gzipStream, TarEntryFormat.Pax, leaveOpen: false);

        writeEntries(writer);
    }

    private static TarEntry CreateRegularFileEntry(string entryName, string content)
    {
        return new PaxTarEntry(TarEntryType.RegularFile, entryName)
        {
            DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content))
        };
    }

    private static void SetEntryLength(TarEntry entry, long length)
    {
        FieldInfo headerField = typeof(TarEntry).GetField("_header", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object header = headerField.GetValue(entry)!;
        FieldInfo sizeField = header.GetType().GetField("_size", BindingFlags.Instance | BindingFlags.NonPublic)!;

        sizeField.SetValue(header, length);
    }

    private static async Task InvokeExtractFileAsync(string destinationDirectory, TarEntry entry, string entryPathDescription)
    {
        MethodInfo method = typeof(TarGzExtractionHelper).GetMethod("ExtractFileAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? result;

        try
        {
            result = method.Invoke(null, [destinationDirectory, entry, entryPathDescription, CancellationToken.None]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        await (Task)result!;
    }
}