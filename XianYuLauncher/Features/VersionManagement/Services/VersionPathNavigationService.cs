using Windows.Storage;
using Windows.System;
using XianYuLauncher.Features.VersionList.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class VersionPathNavigationService : IVersionPathNavigationService
{
    public string GetVersionSpecificPath(string gameDir, VersionListViewModel.VersionInfoItem? selectedVersion, string folderType)
    {
        if (selectedVersion == null)
        {
            return Path.Combine(gameDir, folderType);
        }

        return folderType switch
        {
            "versions" => selectedVersion.Path,
            _ => Path.Combine(gameDir, folderType)
        };
    }

    public string GetVersionSpecificFilePath(string gameDir, VersionListViewModel.VersionInfoItem? selectedVersion, string fileName)
    {
        return Path.Combine(gameDir, fileName);
    }

    public async Task<(bool Success, string StatusMessage)> OpenFolderAsync(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
            await Launcher.LaunchFolderAsync(folder);
            return (true, $"已打开文件夹: {Path.GetFileName(folderPath)}");
        }
        catch (Exception ex)
        {
            return (false, $"打开文件夹失败：{ex.Message}");
        }
    }
}