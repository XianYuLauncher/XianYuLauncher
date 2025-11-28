using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using XMCL2025.ViewModels;

namespace XMCL2025.Views;

// TODO: Set the URL for your privacy policy by updating SettingsPage_PrivacyTermsLink.NavigateUri in Resources.resw.
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel
    {
        get;
    }

    private int _clickCount = 0;

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    private async void VersionTextBlock_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _clickCount++;
        if (_clickCount >= 5)
        {
            // 设置彩蛋显示标志
            App.ShowEasterEgg = true;
            
            // 显示彩蛋内容
            var contentDialog = new ContentDialog
            {
                Title = "测试彩蛋",
                Content = "彩蛋已激活！启动页面的微软登录测试按钮和当前版本路径将在下次打开时显示。",
                CloseButtonText = "确定",
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            await contentDialog.ShowAsync();
            
            // 重置点击计数
            _clickCount = 0;
        }
    }
}
