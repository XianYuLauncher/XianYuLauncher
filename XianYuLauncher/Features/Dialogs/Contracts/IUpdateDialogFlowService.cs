using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface IUpdateDialogFlowService
{
    Task<bool> ShowUpdateInstallFlowAsync(
        UpdateInfo updateInfo,
        string title,
        string primaryButtonText,
        string? closeButtonText = "取消");
}