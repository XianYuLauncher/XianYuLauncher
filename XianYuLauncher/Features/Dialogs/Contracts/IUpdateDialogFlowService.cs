using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface IUpdateDialogFlowService
{
    Task<bool> ShowUpdatePreviewAsync(
        UpdateInfo updateInfo,
        string title,
        string primaryButtonText,
        string? closeButtonText = null);

    Task<bool> ShowUpdateInstallFlowAsync(
        UpdateInfo updateInfo,
        string title,
        string primaryButtonText,
        string? closeButtonText = null);
}