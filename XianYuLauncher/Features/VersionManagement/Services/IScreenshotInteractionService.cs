using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.Features.VersionManagement.Services;

public interface IScreenshotInteractionService
{
    Task<(bool IsDeleted, string StatusMessage)> DeleteScreenshotAsync(ScreenshotInfo? screenshot);

    Task<(bool IsSaved, string StatusMessage)> SaveScreenshotAsAsync(ScreenshotInfo? screenshot);
}