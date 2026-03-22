using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using XianYuLauncher.Features.Dialogs.ViewModels;

namespace XianYuLauncher.Features.Dialogs.Views;

public sealed partial class AnnouncementDialog : UserControl
{
    public AnnouncementDialogViewModel ViewModel { get; }

    public AnnouncementDialog(AnnouncementDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        SetTypeIndicatorColor();

        if (ViewModel.HasCustomXaml)
        {
            LoadCustomXaml();
        }
    }

    private void SetTypeIndicatorColor()
    {
        var color = ViewModel.Announcement.type switch
        {
            "warning" => Windows.UI.Color.FromArgb(255, 247, 99, 12),
            "error" => Windows.UI.Color.FromArgb(255, 196, 43, 28),
            "success" => Windows.UI.Color.FromArgb(255, 16, 137, 62),
            _ => Windows.UI.Color.FromArgb(255, 138, 138, 138)
        };

        TypeIndicator.Color = color;
    }

    private void LoadCustomXaml()
    {
        try
        {
            if (string.IsNullOrEmpty(ViewModel.Announcement.custom_xaml))
            {
                return;
            }

            var xamlContent = XamlReader.Load(ViewModel.Announcement.custom_xaml) as UIElement;
            if (xamlContent != null)
            {
                CustomXamlContent.Content = xamlContent;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载自定义 XAML 失败: {ex.Message}");
            ViewModel.HasCustomXaml = false;
        }
    }

    private async void AnnouncementButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is XianYuLauncher.Core.Models.AnnouncementButton announcementButton)
        {
            await ViewModel.ExecuteButtonAsync(announcementButton);
        }
    }
}