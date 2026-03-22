using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Features.Dialogs.ViewModels;
using XianYuLauncher.Features.Dialogs.Views;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class AnnouncementDialogService : IAnnouncementDialogService
{
    private readonly ILogger<AnnouncementDialogViewModel> _logger;
    private readonly IAnnouncementService _announcementService;
    private readonly IContentDialogHostService _dialogHostService;

    public AnnouncementDialogService(
        ILogger<AnnouncementDialogViewModel> logger,
        IAnnouncementService announcementService,
        IContentDialogHostService dialogHostService)
    {
        _logger = logger;
        _announcementService = announcementService;
        _dialogHostService = dialogHostService;
    }

    public async Task ShowAnnouncementAsync(AnnouncementInfo announcement, string closeButtonText = "知道了")
    {
        var viewModel = new AnnouncementDialogViewModel(_logger, _announcementService, announcement);
        var dialog = new ContentDialog
        {
            Title = announcement.title,
            Content = new AnnouncementDialog(viewModel),
            DefaultButton = ContentDialogButton.None,
        };

        if (announcement.buttons is null || announcement.buttons.Count == 0)
        {
            dialog.CloseButtonText = closeButtonText;
        }

        void OnCloseDialog(object? sender, EventArgs args)
        {
            dialog.Hide();
        }

        viewModel.CloseDialog += OnCloseDialog;

        try
        {
            await _dialogHostService.ShowAsync(dialog);
        }
        finally
        {
            viewModel.CloseDialog -= OnCloseDialog;
        }
    }
}