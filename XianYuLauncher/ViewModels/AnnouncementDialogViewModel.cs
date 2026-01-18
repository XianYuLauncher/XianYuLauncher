using CommunityToolkit.Mvvm.ComponentModel;
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
}
