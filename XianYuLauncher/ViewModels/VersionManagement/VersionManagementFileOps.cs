using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace XianYuLauncher.ViewModels;

internal static class VersionManagementFileOps
{
    public static string? GetLocalIconPath(string launcherCachePath, string filePath, string resourceType)
    {
        try
        {
            string iconDirectory = Path.Combine(launcherCachePath, "icons", resourceType);
            Directory.CreateDirectory(iconDirectory);

            string fileName = Path.GetFileName(filePath);
            string normalizedName = fileName.EndsWith(".disabled", StringComparison.Ordinal)
                ? fileName.Substring(0, fileName.Length - ".disabled".Length)
                : fileName;

            string fileBaseName = Path.GetFileNameWithoutExtension(normalizedName);

            string[] iconFiles = Directory.GetFiles(iconDirectory, $"*_{fileBaseName}_icon.png");
            foreach (string iconFile in iconFiles)
            {
                if (IsValidIconFile(iconFile))
                {
                    return iconFile;
                }

                TryDelete(iconFile);
            }

            string modrinthIcon = Path.Combine(iconDirectory, $"modrinth_{fileBaseName}_icon.png");
            if (File.Exists(modrinthIcon))
            {
                if (IsValidIconFile(modrinthIcon))
                {
                    return modrinthIcon;
                }

                TryDelete(modrinthIcon);
            }

            string curseForgeIcon = Path.Combine(iconDirectory, $"curseforge_{fileBaseName}_icon.png");
            if (File.Exists(curseForgeIcon))
            {
                if (IsValidIconFile(curseForgeIcon))
                {
                    return curseForgeIcon;
                }

                TryDelete(curseForgeIcon);
            }

            bool isResourcePack = resourceType == "resourcepack"
                                  && File.Exists(filePath)
                                  && (filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                      || filePath.EndsWith(".mcpack", StringComparison.OrdinalIgnoreCase));

            if (isResourcePack)
            {
                return ExtractResourcePackIcon(filePath, iconDirectory, fileBaseName);
            }
        }
        catch
        {
        }

        return null;
    }

    public static string CalculateSha1(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        byte[] hashBytes = SHA1.HashData(stream);
        return Convert.ToHexStringLower(hashBytes);
    }

    public static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        DirectoryInfo sourceInfo = new(sourceDirectory);
        if (!sourceInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");
        }

        Directory.CreateDirectory(destinationDirectory);

        foreach (FileInfo fileInfo in sourceInfo.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDirectory, fileInfo.Name);
            fileInfo.CopyTo(targetFilePath, true);
        }

        foreach (DirectoryInfo directoryInfo in sourceInfo.GetDirectories())
        {
            string childTargetDirectory = Path.Combine(destinationDirectory, directoryInfo.Name);
            CopyDirectory(directoryInfo.FullName, childTargetDirectory);
        }
    }

    private static bool IsValidIconFile(string iconPath)
    {
        try
        {
            if (!File.Exists(iconPath))
            {
                return false;
            }

            FileInfo fileInfo = new(iconPath);
            return fileInfo.Length > 100;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractResourcePackIcon(string zipFilePath, string iconDirectory, string fileBaseName)
    {
        try
        {
            string cachedIconPath = Path.Combine(iconDirectory, $"local_{fileBaseName}_icon.png");
            if (File.Exists(cachedIconPath))
            {
                return cachedIconPath;
            }

            using ZipArchive zipArchive = ZipFile.OpenRead(zipFilePath);
            ZipArchiveEntry? packIconEntry = zipArchive.GetEntry("pack.png");
            if (packIconEntry == null)
            {
                return null;
            }

            using Stream entryStream = packIconEntry.Open();
            using FileStream fileStream = File.Create(cachedIconPath);
            entryStream.CopyTo(fileStream);
            return cachedIconPath;
        }
        catch
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
