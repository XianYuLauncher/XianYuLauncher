using Windows.Storage;
using Windows.System;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class VersionPathNavigationService : IVersionPathNavigationService
{
    private readonly ILocalSettingsService _localSettingsService;

    public VersionPathNavigationService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public string GetVersionSpecificPath(string minecraftPath, VersionListViewModel.VersionInfoItem? selectedVersion, string folderType)
    {
        if (selectedVersion == null)
        {
            return Path.Combine(minecraftPath, folderType);
        }

        return folderType switch
        {
            "mods" => Path.Combine(selectedVersion.Path, folderType),
            "shaderpacks" => Path.Combine(selectedVersion.Path, folderType),
            "resourcepacks" => Path.Combine(selectedVersion.Path, folderType),
            "datapacks" => Path.Combine(selectedVersion.Path, folderType),
            "saves" => Path.Combine(selectedVersion.Path, folderType),
            "versions" => selectedVersion.Path,
            _ => Path.Combine(selectedVersion.Path, folderType)
        };
    }

    public async Task<string> GetVersionSpecificFilePathAsync(string minecraftPath, VersionListViewModel.VersionInfoItem? selectedVersion, string fileName)
    {
        var enableVersionIsolation = (await _localSettingsService.ReadSettingAsync<bool?>("EnableVersionIsolation")) ?? true;

        if (enableVersionIsolation && !string.IsNullOrEmpty(selectedVersion?.Path))
        {
            return Path.Combine(selectedVersion.Path, fileName);
        }

        return Path.Combine(minecraftPath, fileName);
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