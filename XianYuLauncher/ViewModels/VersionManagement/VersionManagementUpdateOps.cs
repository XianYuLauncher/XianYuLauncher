using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.ViewModels;

internal static class VersionManagementUpdateOps
{
    public static async Task<string> ResolveGameVersionAsync(
        VersionListViewModel.VersionInfoItem? selectedVersion,
        IVersionInfoService? versionInfoService,
        string defaultVersion = "1.19.2")
    {
        var gameVersion = selectedVersion?.VersionNumber ?? defaultVersion;

        if (versionInfoService == null || selectedVersion == null)
        {
            return gameVersion;
        }

        var versionDirectory = selectedVersion.Path;
        var versionConfig = await versionInfoService.GetFullVersionInfoAsync(selectedVersion.Name, versionDirectory);
        if (!string.IsNullOrEmpty(versionConfig?.MinecraftVersion))
        {
            gameVersion = versionConfig.MinecraftVersion;
        }

        return gameVersion;
    }

    public static (List<string> Hashes, Dictionary<string, string> FilePathMap) BuildHashIndex<T>(
        IEnumerable<T> items,
        Func<T, string> filePathSelector,
        Func<string, string> calculateSha1,
        Func<T, bool>? shouldSkip = null,
        Action<T>? onSkipped = null,
        Action<T, string>? onHashed = null,
        Action<T, Exception>? onHashFailed = null)
    {
        List<string> hashes = new();
        Dictionary<string, string> filePathMap = new();

        foreach (T item in items)
        {
            if (shouldSkip?.Invoke(item) == true)
            {
                onSkipped?.Invoke(item);
                continue;
            }

            var filePath = filePathSelector(item);
            try
            {
                var sha1Hash = calculateSha1(filePath);
                hashes.Add(sha1Hash);
                filePathMap[sha1Hash] = filePath;
                onHashed?.Invoke(item, sha1Hash);
            }
            catch (Exception exception)
            {
                onHashFailed?.Invoke(item, exception);
            }
        }

        return (hashes, filePathMap);
    }
}
