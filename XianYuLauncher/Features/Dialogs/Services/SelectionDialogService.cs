using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.Dialogs.Contracts;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class SelectionDialogService : ISelectionDialogService
{
    private readonly IContentDialogHostService _dialogHostService;

    public SelectionDialogService(IContentDialogHostService dialogHostService)
    {
        _dialogHostService = dialogHostService ?? throw new ArgumentNullException(nameof(dialogHostService));
    }

    public async Task<SettingsCustomSourceDialogResult?> ShowSettingsCustomSourceDialogAsync(SettingsCustomSourceDialogRequest request)
    {
        var dialog = new ContentDialog
        {
            Title = request.Title,
            PrimaryButtonText = request.PrimaryButtonText,
            CloseButtonText = request.CloseButtonText,
            DefaultButton = ContentDialogButton.Primary,
        };

        var stackPanel = new StackPanel { Spacing = 12 };
        var nameBox = new TextBox { Text = request.Name, PlaceholderText = "例如：我的镜像站", Header = "源名称" };
        var urlBox = new TextBox { Text = request.BaseUrl, PlaceholderText = "https://mirror.example.com", Header = "Base URL" };
        var priorityBox = new NumberBox
        {
            Header = "优先级（数值越大优先级越高）",
            Value = request.Priority,
            Minimum = 1,
            Maximum = 1000,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };

        stackPanel.Children.Add(nameBox);
        stackPanel.Children.Add(urlBox);

        ComboBox? templateCombo = null;
        if (request.ShowTemplateSelection)
        {
            templateCombo = new ComboBox
            {
                Header = "模板类型",
                ItemsSource = new[] { "官方资源", "社区资源" },
                SelectedIndex = request.Template == DownloadSourceTemplateType.Official ? 0 : 1,
            };
            stackPanel.Children.Add(templateCombo);
        }
        else
        {
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"模板类型: {(request.Template == DownloadSourceTemplateType.Official ? "官方资源" : "社区资源")}",
                Opacity = 0.7,
                Margin = new Thickness(0, 8, 0, 0),
            });
        }

        stackPanel.Children.Add(priorityBox);

        ToggleSwitch? enabledSwitch = null;
        if (request.ShowEnabledSwitch)
        {
            enabledSwitch = new ToggleSwitch { Header = "启用", IsOn = request.Enabled };
            stackPanel.Children.Add(enabledSwitch);
        }

        dialog.Content = stackPanel;

        var result = await _dialogHostService.ShowAsync(dialog);
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        var template = request.Template;
        if (templateCombo != null)
        {
            template = templateCombo.SelectedIndex == 0
                ? DownloadSourceTemplateType.Official
                : DownloadSourceTemplateType.Community;
        }

        return new SettingsCustomSourceDialogResult
        {
            Name = nameBox.Text?.Trim() ?? string.Empty,
            BaseUrl = urlBox.Text?.Trim() ?? string.Empty,
            Template = template,
            Priority = (int)priorityBox.Value,
            Enabled = enabledSwitch?.IsOn ?? request.Enabled,
        };
    }

    public async Task<AddServerDialogResult?> ShowAddServerDialogAsync(string defaultName = "Minecraft Server")
    {
        var stackPanel = new StackPanel { Spacing = 12 };
        var nameInput = new TextBox
        {
            Header = "服务器名称",
            PlaceholderText = "Minecraft Server",
            Text = defaultName,
        };
        var addrInput = new TextBox
        {
            Header = "服务器地址",
            PlaceholderText = "例如: 127.0.0.1",
        };

        stackPanel.Children.Add(nameInput);
        stackPanel.Children.Add(addrInput);

        var dialog = new ContentDialog
        {
            Title = "添加服务器",
            PrimaryButtonText = "添加",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = stackPanel,
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return new AddServerDialogResult
        {
            Name = nameInput.Text?.Trim() ?? string.Empty,
            Address = addrInput.Text?.Trim() ?? string.Empty,
        };
    }
}