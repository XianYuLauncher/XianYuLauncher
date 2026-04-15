namespace XianYuLauncher.Core.Helpers;

public static class PathContainmentHelper
{
    public static string GetValidatedPathUnderRoot(string rootDirectory, string relativePath, string pathDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string trimmedPath = relativePath.Trim();
        if (Path.IsPathRooted(trimmedPath)
            || trimmedPath.StartsWith(Path.DirectorySeparatorChar)
            || trimmedPath.StartsWith(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException($"{pathDescription} 无效：禁止使用绝对路径");
        }

        string[] segments = trimmedPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException($"{pathDescription} 无效：包含非法路径段");
        }

        if (segments.Any(segment => segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new InvalidOperationException($"{pathDescription} 无效：包含非法字符");
        }

        string normalizedRelativePath = Path.Combine(segments);
        string normalizedRootDirectory = EnsureTrailingDirectorySeparator(Path.GetFullPath(rootDirectory));
        string fullPath = Path.GetFullPath(Path.Combine(normalizedRootDirectory, normalizedRelativePath));

        if (!fullPath.StartsWith(normalizedRootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{pathDescription} 无效：超出实例目录范围");
        }

        return fullPath;
    }

    public static string SanitizeExternalFileName(string? fileName, string fallbackFileName, string fileDescription)
    {
        string candidateFileName = string.IsNullOrWhiteSpace(fileName)
            ? fallbackFileName
            : Path.GetFileName(fileName.Trim());

        if (string.IsNullOrWhiteSpace(candidateFileName) || candidateFileName is "." or "..")
        {
            throw new InvalidOperationException($"{fileDescription} 无效：无法解析文件名");
        }

        if (candidateFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException($"{fileDescription} 无效：包含非法字符");
        }

        return candidateFileName;
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}