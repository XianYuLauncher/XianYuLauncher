using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.Services;

public interface IVersionPathNavigationService
{
    string GetVersionSpecificPath(string minecraftPath, VersionListViewModel.VersionInfoItem? selectedVersion, string folderType);

    Task<string> GetVersionSpecificFilePathAsync(string minecraftPath, VersionListViewModel.VersionInfoItem? selectedVersion, string fileName);

    Task<(bool Success, string StatusMessage)> OpenFolderAsync(string folderPath);
}