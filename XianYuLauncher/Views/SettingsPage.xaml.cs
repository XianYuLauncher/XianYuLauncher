using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel
    {
        get;
    }

    private int _clickCount = 0;
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
                var localSettingsService = App.GetService<ILocalSettingsService>();
                var currentMode = await localSettingsService.ReadSettingAsync<bool?>(EasterEggModeKey) ?? false;
                
                var newMode = !currentMode;
                await localSettingsService.SaveSettingAsync(EasterEggModeKey, newMode);
                
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
    
    private async void MinecraftPathListBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.SelectedMinecraftPathItem != null)
        {
            await ViewModel.SwitchMinecraftPathCommand.ExecuteAsync(ViewModel.SelectedMinecraftPathItem);
        }
    }

    private async void OpenLogDirectory_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.OpenLogDirectoryCommand.ExecuteAsync(null);
    }

    private async void OpenSourceLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CommunityToolkit.WinUI.Controls.SettingsCard card && card.Tag is string url)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
        }
    }
}
