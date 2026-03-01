using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class ScreenshotInteractionService : IScreenshotInteractionService
{
    private readonly IOverviewDataService _overviewDataService;
    private readonly IDialogService _dialogService;

    public ScreenshotInteractionService(
        IOverviewDataService overviewDataService,
        IDialogService dialogService)
    {
        _overviewDataService = overviewDataService;
        _dialogService = dialogService;
    }

    public async Task<(bool IsDeleted, string StatusMessage)> DeleteScreenshotAsync(ScreenshotInfo? screenshot)
    {
        if (screenshot == null)
        {
            return (false, string.Empty);
        }

        try
        {
            var confirmed = await _dialogService.ShowConfirmationDialogAsync(
                "确认删除",
                $"确定要删除截图 '{screenshot.Name}' 吗？此操作不可恢复。",
                "确定删除",
                "取消");
            if (!confirmed)
            {
                return (false, string.Empty);
            }

            if (File.Exists(screenshot.FilePath))
            {
                await _overviewDataService.DeleteScreenshotAsync(screenshot.FilePath);
            }

            return (true, $"已删除截图: {screenshot.Name}");
        }
        catch (Exception ex)
        {
            return (false, $"删除截图失败：{ex.Message}");
        }
    }

    public async Task<(bool IsSaved, string StatusMessage)> SaveScreenshotAsAsync(ScreenshotInfo? screenshot)
    {
        if (screenshot == null)
        {
            return (false, string.Empty);
        }

        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            picker.FileTypeChoices.Add("PNG图片", new List<string> { ".png" });
            picker.SuggestedFileName = screenshot.Name;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file == null)
            {
                return (false, string.Empty);
            }

            await _overviewDataService.CopyScreenshotAsync(screenshot.FilePath, file);
            return (true, $"截图已保存至: {file.Path}");
        }
        catch (Exception ex)
        {
            return (false, $"保存截图失败：{ex.Message}");
        }
    }
}