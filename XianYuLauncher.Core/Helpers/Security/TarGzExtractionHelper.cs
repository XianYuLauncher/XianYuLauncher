using System.Formats.Tar;
using System.IO.Compression;

namespace XianYuLauncher.Core.Helpers;

public static class TarGzExtractionHelper
{
    public static async Task ExtractToDirectoryAsync(
        string tarGzPath,
        string destinationDirectory,
        string entryPathDescription,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tarGzPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryPathDescription);

        Directory.CreateDirectory(destinationDirectory);

        await using FileStream fileStream = new(tarGzPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using GZipStream gzipStream = new(fileStream, CompressionMode.Decompress, leaveOpen: false);
        await using TarReader tarReader = new(gzipStream, leaveOpen: false);

        TarEntry? entry;
        while ((entry = await tarReader.GetNextEntryAsync(copyData: false, cancellationToken)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            switch (entry.EntryType)
            {
                case TarEntryType.Directory:
                    ExtractDirectory(destinationDirectory, entry.Name, entryPathDescription);
                    break;
                case TarEntryType.RegularFile:
                case TarEntryType.V7RegularFile:
                case TarEntryType.ContiguousFile:
                    await ExtractFileAsync(destinationDirectory, entry, entryPathDescription, cancellationToken);
                    break;
                case TarEntryType.SymbolicLink:
                case TarEntryType.HardLink:
                    throw new InvalidOperationException($"{entryPathDescription} 无效：不支持链接条目");
                default:
                    throw new InvalidOperationException($"{entryPathDescription} 无效：不支持条目类型 {entry.EntryType}");
            }
        }
    }

    private static void ExtractDirectory(string destinationDirectory, string entryName, string entryPathDescription)
    {
        string directoryPath = PathContainmentHelper.GetValidatedPathUnderRoot(
            destinationDirectory,
            entryName,
            entryPathDescription);

        Directory.CreateDirectory(directoryPath);
    }

    private static async Task ExtractFileAsync(
        string destinationDirectory,
        TarEntry entry,
        string entryPathDescription,
        CancellationToken cancellationToken)
    {
        string destinationPath = PathContainmentHelper.GetValidatedPathUnderRoot(
            destinationDirectory,
            entry.Name,
            entryPathDescription);

        string? targetDirectory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException($"无法确定解压目标目录: {destinationPath}");
        }

        Directory.CreateDirectory(targetDirectory);

        await using FileStream destinationStream = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using Stream? sourceStream = entry.DataStream;

        if (sourceStream is null)
        {
            if (entry.Length != 0)
            {
                throw new InvalidOperationException($"{entryPathDescription} 无效：文件条目缺少数据流");
            }

            return;
        }

        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
    }
}