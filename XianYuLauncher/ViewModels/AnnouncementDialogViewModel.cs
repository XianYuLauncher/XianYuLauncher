using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.ViewModels;

/// <summary>
/// 云控公告弹窗 ViewModel
/// </summary>
public partial class AnnouncementDialogViewModel : ObservableObject
{
    private readonly ILogger<AnnouncementDialogViewModel> _logger;
    private readonly IAnnouncementService _announcementService;
    
    [ObservableProperty]
    private AnnouncementInfo _announcement;
    
    [ObservableProperty]
    private bool _hasCustomXaml;

    public bool HasButtons => Announcement.buttons != null && Announcement.buttons.Count > 0;
    
    /// <summary>
    /// 关闭对话框事件
    /// </summary>
    public event EventHandler? CloseDialog;
    
    public AnnouncementDialogViewModel(
        ILogger<AnnouncementDialogViewModel> logger,
        IAnnouncementService announcementService,
        AnnouncementInfo announcement)
    {
        _logger = logger;
        _announcementService = announcementService;
        _announcement = announcement;
        _hasCustomXaml = !string.IsNullOrEmpty(announcement.custom_xaml);
    }
    
    /// <summary>
    /// 关闭对话框并标记为已读
    /// </summary>
    public async Task CloseAndMarkAsReadAsync()
    {
        await _announcementService.MarkAnnouncementAsReadAsync(Announcement.id);
        CloseDialog?.Invoke(this, EventArgs.Empty);
    }

    public async Task ExecuteButtonAsync(AnnouncementButton button)
    {
        var action = button.action?.Trim().ToLowerInvariant() ?? "close";

        switch (action)
        {
            case "open_url":
                if (!string.IsNullOrWhiteSpace(button.action_param))
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(button.action_param));
                }
                break;
            case "exit_app":
                CloseDialog?.Invoke(this, EventArgs.Empty);
                App.MainWindow.Close();
                Application.Current.Exit();
                break;
            case "close":
            default:
                await CloseAndMarkAsReadAsync();
                break;
        }
    }
}
