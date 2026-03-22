using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Serilog;
using XianYuLauncher.Core.Helpers;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel
    {
        get;
    }

    private int _clickCount = 0;
    private const string EasterEggModeKey = "EasterEggMode";

    // 自动测速服务（用于事件驱动刷新缓存）
    private readonly IAutoSpeedTestService _autoSpeedTestService;
    private readonly ICommonDialogService _dialogService;
    private readonly IUiDispatcher _uiDispatcher;
    private string? _pendingProtocolSection;

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        _autoSpeedTestService = App.GetService<IAutoSpeedTestService>();
        _dialogService = App.GetService<ICommonDialogService>();
        _uiDispatcher = App.GetService<IUiDispatcher>();
        InitializeComponent();
        
        // 页面加载时刷新自定义源列表
        Loaded += SettingsPage_Loaded;
        Unloaded += SettingsPage_Unloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (!ProtocolNavigationParameterHelper.TryGetStringParameter(e.Parameter, "section", out var section))
        {
            Log.Information("[Protocol.Settings] OnNavigatedTo: no section parameter.");
            return;
        }

        _pendingProtocolSection = section;
        Log.Information("[Protocol.Settings] OnNavigatedTo: section='{Section}'.", section);

        TryApplyPendingSectionNavigation();
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        TryApplyPendingSectionNavigation();

        try
        {
            // 加载测速缓存，显示上次的测速结果（启动时已自动测速）
            if (ViewModel != null)
            {
                await ViewModel.LoadSpeedTestCacheAsync();

                _autoSpeedTestService.SpeedTestCompleted -= AutoSpeedTestService_SpeedTestCompleted;
                _autoSpeedTestService.SpeedTestCompleted += AutoSpeedTestService_SpeedTestCompleted;
            }

            if (ViewModel?.RefreshCustomSourcesCommand != null)
            {
                await ViewModel.RefreshCustomSourcesCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SettingsPage] 刷新设置页初始化数据失败");
        }
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _autoSpeedTestService.SpeedTestCompleted -= AutoSpeedTestService_SpeedTestCompleted;
        ViewModel?.Dispose();
    }

    private void AutoSpeedTestService_SpeedTestCompleted(object? sender, EventArgs e)
    {
        try
        {
            if (ViewModel?.AutoSelectFastestSource == true)
            {
                _uiDispatcher.EnqueueAsync(async () =>
                {
                    await RefreshAutoSpeedTestStateAsync();
                }).Observe("SettingsPage.AutoSpeedTestCompleted");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] 处理自动测速完成事件失败: {ex.Message}");
        }
    }

    private async Task RefreshAutoSpeedTestStateAsync()
    {
        if (ViewModel == null)
        {
            return;
        }

        await ViewModel.LoadSpeedTestCacheAsync();
        await ViewModel.ReloadDownloadSourceSettingsAsync();
    }

    private void ScrollToSection(string section)
    {
        var sectionKey = section.Trim().ToLowerInvariant();
        FrameworkElement? target = sectionKey switch
        {
            "game" => GameSectionHeader,
            "personalization" or "appearance" => PersonalizationSectionHeader,
            "network" => NetworkSectionHeader,
            "ai" or "ai-analysis" => AiSectionHeader,
            "about" => AboutSectionHeader,
            _ => null,
        };

        if (target == null)
        {
            Log.Warning("[Protocol.Settings] Unknown section='{Section}'.", section);
            return;
        }

        Log.Information("[Protocol.Settings] Scrolling to section='{Section}'.", sectionKey);
        target.StartBringIntoView(new BringIntoViewOptions
        {
            AnimationDesired = true,
            VerticalAlignmentRatio = 0.05,
        });
    }

    private void TryApplyPendingSectionNavigation()
    {
        if (string.IsNullOrWhiteSpace(_pendingProtocolSection))
        {
            return;
        }

        if (!IsLoaded)
        {
            Log.Information("[Protocol.Settings] Delay section scroll: page is not loaded.");
            return;
        }

        var section = _pendingProtocolSection;
        _pendingProtocolSection = null;

        _uiDispatcher.TryEnqueue(() =>
        {
            ScrollToSection(section!);

            // 再补一次重试，覆盖首次布局尚未稳定的场景。
            _uiDispatcher.TryEnqueue(() => ScrollToSection(section!));
        });
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
                
                await _dialogService.ShowMessageDialogAsync(
                    newMode ? "🎉 彩蛋模式已开启" : "彩蛋模式已关闭",
                    newMode
                        ? "恭喜你发现了隐藏彩蛋！看看有什么地方不同寻常吧()"
                        : "彩蛋模式已关闭，一切恢复正常。",
                    "好的");
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
