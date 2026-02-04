using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.IO;

using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

// Set the URL for your privacy policy by updating SettingsPage_PrivacyTermsLink.NavigateUri in Resources.resw.
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel
    {
        get;
    }

    private int _clickCount = 0;
    
    /// <summary>
    /// 彩蛋模式设置键
    /// </summary>
    private const string EasterEggModeKey = "EasterEggMode";

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
            try
            {
                // 获取当前彩蛋模式状态
                var localSettingsService = App.GetService<ILocalSettingsService>();
                var currentMode = await localSettingsService.ReadSettingAsync<bool?>(EasterEggModeKey) ?? false;
                
                // 切换彩蛋模式
                var newMode = !currentMode;
                await localSettingsService.SaveSettingAsync(EasterEggModeKey, newMode);
                
                // 显示提示
                var dialog = new ContentDialog
                {
                    Title = newMode ? "🎉 彩蛋模式已开启" : "彩蛋模式已关闭",
                    Content = newMode 
                        ? "恭喜你发现了隐藏彩蛋！看看有什么地方不同寻常吧()" 
                        : "彩蛋模式已关闭，一切恢复正常。",
                    CloseButtonText = "好的",
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                    DefaultButton = ContentDialogButton.None
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[彩蛋模式] 切换失败: {ex.Message}");
            }
            finally
            {
                _clickCount = 0;
            }
        }
    }
    
    /// <summary>
    /// 处理游戏目录列表的双击事件
    /// </summary>
    private async void MinecraftPathListBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.SelectedMinecraftPathItem != null)
        {
            await ViewModel.SwitchMinecraftPathCommand.ExecuteAsync(ViewModel.SelectedMinecraftPathItem);
        }
    }
}
