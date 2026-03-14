using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.ViewModels;
using Windows.Storage;

namespace XianYuLauncher.Features.VersionManagement.Services;

public interface IOverviewDataService
{
    Task<(int LaunchCount, long TotalPlayTimeSeconds, DateTime? LastLaunchTime)?> LoadOverviewDataAsync(
        VersionListViewModel.VersionInfoItem? selectedVersion,
        CancellationToken cancellationToken = default);

    Task<List<SaveInfo>> LoadSavesAsync(
        VersionListViewModel.VersionInfoItem? selectedVersion,
        string? gameDir = null,
        CancellationToken cancellationToken = default);

    Task LoadSaveIconsAsync(
        IReadOnlyList<SaveInfo> saves,
        CancellationToken cancellationToken = default);

    Task<List<ScreenshotInfo>> LoadScreenshotsAsync(
        string screenshotsPath,
        CancellationToken cancellationToken = default);

    List<ScreenshotInfo> FilterScreenshots(
        IReadOnlyList<ScreenshotInfo> allScreenshots,
        string screenshotSearchText);

    bool HasSameScreenshotSnapshot(
        IEnumerable<ScreenshotInfo> currentItems,
        IEnumerable<ScreenshotInfo> sourceItems);

    Task DeleteScreenshotAsync(string filePath);

    Task CopyScreenshotAsync(string sourceFilePath, StorageFile destinationFile);

    (string? RandomScreenshotPath, bool HasRandomScreenshot) PickRandomScreenshot(IReadOnlyList<ScreenshotInfo> screenshots);
}