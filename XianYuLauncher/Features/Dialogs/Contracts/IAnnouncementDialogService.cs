using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface IAnnouncementDialogService
{
    Task ShowAnnouncementAsync(AnnouncementInfo announcement, string? closeButtonText = null);
}