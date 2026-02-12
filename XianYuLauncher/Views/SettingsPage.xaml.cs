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
    
    // 防止 ToggleSwitch 事件递归触发的标志
    private bool _isTogglingSwitch = false;

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
        
        // 页面加载时刷新自定义源列表
        Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[SettingsPage] 页面加载，开始刷新自定义源列表");
        try
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] ViewModel 是否为 null: {ViewModel == null}");
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] RefreshCustomSourcesCommand 是否为 null: {ViewModel?.RefreshCustomSourcesCommand == null}");
            
            if (ViewModel?.RefreshCustomSourcesCommand != null)
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] 开始执行 RefreshCustomSourcesCommand");
                await ViewModel.RefreshCustomSourcesCommand.ExecuteAsync(null);
                System.Diagnostics.Debug.WriteLine("[SettingsPage] RefreshCustomSourcesCommand 执行完成");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] ViewModel 或 Command 为 null，无法刷新");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] 刷新自定义源列表失败: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] 堆栈跟踪: {ex.StackTrace}");
        }
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

    private void OpenCustomSourceConfigFolder_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenCustomSourceConfigFileCommand.Execute(null);
    }

    private async void OpenSourceLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CommunityToolkit.WinUI.Controls.SettingsCard card && card.Tag is string url)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
        }
    }

    private async void CustomSourceToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        // 防止递归触发
        if (_isTogglingSwitch)
        {
            System.Diagnostics.Debug.WriteLine("[SettingsPage] ToggleSwitch_Toggled 被阻止（递归保护）");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine("[SettingsPage] ToggleSwitch_Toggled 事件触发");
        
        if (sender is ToggleSwitch toggle && toggle.Tag is Models.CustomSourceViewModel source)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] 源: {source.Name}, Key: {source.Key}, 新状态: {toggle.IsOn}");
            
            try
            {
                _isTogglingSwitch = true;
                
                // 先记录当前状态
                var newState = toggle.IsOn;
                
                // 调用命令保存状态
                var result = await ViewModel.ToggleCustomSourceWithResultAsync(source.Key, newState);
                
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] 切换结果: {result}");
                
                // 如果失败，恢复原状态
                if (!result)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] 切换失败，恢复原状态");
                    toggle.IsOn = !newState;
                }
            }
            finally
            {
                _isTogglingSwitch = false;
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] ToggleSwitch 或 Tag 为空！sender type: {sender?.GetType().Name}, Tag type: {(sender as ToggleSwitch)?.Tag?.GetType().Name}");
        }
    }

    private async void EditCustomSource_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[SettingsPage] EditCustomSource_Click 事件触发");
        
        if (sender is Button button && button.Tag is Models.CustomSourceViewModel source)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] 编辑源: {source.Name}");
            await ViewModel.EditCustomSourceCommand.ExecuteAsync(source);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] 按钮或 Tag 为空！");
        }
    }

    private async void DeleteCustomSource_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[SettingsPage] DeleteCustomSource_Click 事件触发");
        
        if (sender is Button button && button.Tag is Models.CustomSourceViewModel source)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] 删除源: {source.Name}");
            await ViewModel.DeleteCustomSourceCommand.ExecuteAsync(source);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] 按钮或 Tag 为空！");
        }
    }
}
