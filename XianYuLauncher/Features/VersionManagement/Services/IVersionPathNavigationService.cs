using XianYuLauncher.Features.VersionList.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.Services;

public interface IVersionPathNavigationService
{
    string GetVersionSpecificPath(string gameDir, VersionListViewModel.VersionInfoItem? selectedVersion, string folderType);

    string GetVersionSpecificFilePath(string gameDir, VersionListViewModel.VersionInfoItem? selectedVersion, string fileName);

    Task<(bool Success, string StatusMessage)> OpenFolderAsync(string folderPath);
}