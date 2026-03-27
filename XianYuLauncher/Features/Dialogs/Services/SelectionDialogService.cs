using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Helpers;

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
        var nameBox = new TextBox { Text = request.Name, PlaceholderText = "Dialog_CustomSource_NamePlaceholder".GetLocalized(), Header = "Dialog_CustomSource_NameHeader".GetLocalized() };
        var urlBox = new TextBox { Text = request.BaseUrl, PlaceholderText = "https://mirror.example.com", Header = "Dialog_CustomSource_UrlHeader".GetLocalized() };
        var priorityBox = new NumberBox
        {
            Header = "Dialog_CustomSource_PriorityHeader".GetLocalized(),
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
                Header = "Dialog_CustomSource_TemplateHeader".GetLocalized(),
                ItemsSource = new[] { "Dialog_CustomSource_OfficialResource".GetLocalized(), "Dialog_CustomSource_CommunityResource".GetLocalized() },
                SelectedIndex = request.Template == DownloadSourceTemplateType.Official ? 0 : 1,
            };
            stackPanel.Children.Add(templateCombo);
        }
        else
        {
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"{"Dialog_CustomSource_TemplateHeader".GetLocalized()}: {(request.Template == DownloadSourceTemplateType.Official ? "Dialog_CustomSource_OfficialResource".GetLocalized() : "Dialog_CustomSource_CommunityResource".GetLocalized())}",
                Opacity = 0.7,
                Margin = new Thickness(0, 8, 0, 0),
            });
        }

        stackPanel.Children.Add(priorityBox);

        ToggleSwitch? enabledSwitch = null;
        if (request.ShowEnabledSwitch)
        {
            enabledSwitch = new ToggleSwitch { Header = "Dialog_CustomSource_EnableHeader".GetLocalized(), IsOn = request.Enabled };
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
            Header = "Dialog_AddServer_NameHeader".GetLocalized(),
            PlaceholderText = "Minecraft Server",
            Text = defaultName,
        };
        var addrInput = new TextBox
        {
            Header = "Dialog_AddServer_AddressHeader".GetLocalized(),
            PlaceholderText = "Dialog_AddServer_AddressPlaceholder".GetLocalized(),
        };

        stackPanel.Children.Add(nameInput);
        stackPanel.Children.Add(addrInput);

        var dialog = new ContentDialog
        {
            Title = "Dialog_AddServer_Title".GetLocalized(),
            PrimaryButtonText = "Dialog_AddServer_AddButton".GetLocalized(),
            CloseButtonText = "Msg_Cancel".GetLocalized(),
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