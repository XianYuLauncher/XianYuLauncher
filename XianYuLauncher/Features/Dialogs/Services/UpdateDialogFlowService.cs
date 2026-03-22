using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Features.Dialogs.ViewModels;
using XianYuLauncher.Features.Dialogs.Views;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class UpdateDialogFlowService : IUpdateDialogFlowService
{
    private readonly ILogger<UpdateDialogViewModel> _logger;
    private readonly UpdateService _updateService;
    private readonly IContentDialogHostService _dialogHostService;

    public UpdateDialogFlowService(
        ILogger<UpdateDialogViewModel> logger,
        UpdateService updateService,
        IContentDialogHostService dialogHostService)
    {
        _logger = logger;
        _updateService = updateService;
        _dialogHostService = dialogHostService;
    }

    public async Task<bool> ShowUpdateInstallFlowAsync(
        UpdateInfo updateInfo,
        string title,
        string primaryButtonText,
        string? closeButtonText = "取消")
    {
        var viewModel = new UpdateDialogViewModel(_logger, _updateService, updateInfo);
        var updateDialog = new ContentDialog
        {
            Title = title,
            Content = new UpdateDialog(viewModel),
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await _dialogHostService.ShowAsync(updateDialog);
        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        var downloadDialog = new ContentDialog
        {
            Title = title,
            Content = new DownloadProgressDialog(viewModel),
            IsPrimaryButtonEnabled = false,
            CloseButtonText = closeButtonText ?? "取消",
            DefaultButton = ContentDialogButton.None,
        };

        void OnCloseDialog(object? sender, bool args)
        {
            downloadDialog.Hide();
        }

        downloadDialog.CloseButtonClick += (_, _) => viewModel.CancelCommand.Execute(null);
        viewModel.CloseDialog += OnCloseDialog;

        try
        {
            _ = viewModel.UpdateCommand.ExecuteAsync(null);
            await _dialogHostService.ShowAsync(downloadDialog);
            return true;
        }
        finally
        {
            viewModel.CloseDialog -= OnCloseDialog;
        }
    }
}