using System.IO.Compression;

namespace XianYuLauncher.Core.Helpers;

public static class ZipExtractionHelper
{
    public static void ExtractToDirectorySafely(
        string zipPath,
        string destinationDirectory,
        bool stripSingleRootDirectory,
        string entryPathDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);

        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        ExtractToDirectorySafely(archive, destinationDirectory, stripSingleRootDirectory, entryPathDescription);
    }

    internal static void ExtractToDirectorySafely(
        ZipArchive archive,
        string destinationDirectory,
        bool stripSingleRootDirectory,
        string entryPathDescription)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryPathDescription);

        Directory.CreateDirectory(destinationDirectory);

        string? rootFolderName = stripSingleRootDirectory
            ? GetSingleRootFolderName(archive.Entries)
            : null;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string? relativePath = GetRelativeEntryPath(entry.FullName, rootFolderName);
            if (string.IsNullOrEmpty(relativePath))
            {
                continue;
            }

            string destinationPath = PathContainmentHelper.GetValidatedPathUnderRoot(
                destinationDirectory,
                relativePath,
                entryPathDescription);

            if (IsDirectoryEntry(entry.FullName))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            string? destinationFolder = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static string? GetSingleRootFolderName(IReadOnlyList<ZipArchiveEntry> entries)
    {
        var normalizedEntryPaths = entries
            .Select(entry => NormalizeEntryPath(entry.FullName))
            .Where(path => !string.IsNullOrEmpty(path))
            .ToList();

        string? candidatePath = normalizedEntryPaths
            .Select(path => path.TrimEnd('/'))
            .FirstOrDefault(path => path.Contains('/', StringComparison.Ordinal));

        if (string.IsNullOrEmpty(candidatePath))
        {
            return null;
        }

        int separatorIndex = candidatePath.IndexOf('/');
        if (separatorIndex <= 0)
        {
            return null;
        }

        string rootFolderName = candidatePath[..separatorIndex];
        return normalizedEntryPaths.All(path =>
            string.Equals(path, rootFolderName, StringComparison.Ordinal)
            || string.Equals(path, rootFolderName + "/", StringComparison.Ordinal)
            || path.StartsWith(rootFolderName + "/", StringComparison.Ordinal))
            ? rootFolderName
            : null;
    }

    private static string? GetRelativeEntryPath(string entryFullName, string? rootFolderName)
    {
        string normalizedEntryPath = NormalizeEntryPath(entryFullName);
        if (string.IsNullOrEmpty(normalizedEntryPath))
        {
            return null;
        }

        if (string.IsNullOrEmpty(rootFolderName))
        {
            return normalizedEntryPath;
        }

        string rootFolderPrefix = rootFolderName + "/";
        if (string.Equals(normalizedEntryPath, rootFolderName, StringComparison.Ordinal)
            || string.Equals(normalizedEntryPath, rootFolderPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return normalizedEntryPath.StartsWith(rootFolderPrefix, StringComparison.Ordinal)
            ? normalizedEntryPath[rootFolderPrefix.Length..]
            : normalizedEntryPath;
    }

    private static string NormalizeEntryPath(string entryFullName)
    {
        return string.IsNullOrWhiteSpace(entryFullName)
            ? string.Empty
            : entryFullName.Replace('\\', '/');
    }

    private static bool IsDirectoryEntry(string entryFullName)
    {
        return entryFullName.EndsWith("/", StringComparison.Ordinal)
            || entryFullName.EndsWith("\\", StringComparison.Ordinal);
    }
}