using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class AnnouncementDialog : UserControl
{
    public AnnouncementDialogViewModel ViewModel { get; }

    public AnnouncementDialog(AnnouncementDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();
        
        // 设置类型指示器颜色
        SetTypeIndicatorColor();
        
        // 如果有自定义 XAML，加载它
        if (ViewModel.HasCustomXaml)
        {
            LoadCustomXaml();
        }
    }

    /// <summary>
    /// 设置公告类型指示器颜色
    /// </summary>
    private void SetTypeIndicatorColor()
    {
        var color = ViewModel.Announcement.type switch
        {
            "warning" => Windows.UI.Color.FromArgb(255, 247, 99, 12),   // Fluent 橙色
            "error" => Windows.UI.Color.FromArgb(255, 196, 43, 28),     // Fluent 红色（降低饱和度）
            "success" => Windows.UI.Color.FromArgb(255, 16, 137, 62),   // Fluent 绿色（降低饱和度）
            _ => Windows.UI.Color.FromArgb(255, 138, 138, 138)          // 浅灰色（info）
        };
        
        TypeIndicator.Color = color;
    }

    /// <summary>
    /// 加载自定义 XAML 内容
    /// </summary>
    private void LoadCustomXaml()
    {
        try
        {
            if (string.IsNullOrEmpty(ViewModel.Announcement.custom_xaml))
            {
                return;
            }

            // 解析 XAML 字符串
            var xamlContent = XamlReader.Load(ViewModel.Announcement.custom_xaml) as UIElement;
            
            if (xamlContent != null)
            {
                CustomXamlContent.Content = xamlContent;
            }
        }
        catch (Exception ex)
        {
            // 如果自定义 XAML 加载失败，回退到默认内容
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
