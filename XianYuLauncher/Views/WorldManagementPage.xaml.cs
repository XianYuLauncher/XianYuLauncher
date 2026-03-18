using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Models;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class WorldManagementPage : Page
{
    public WorldManagementViewModel ViewModel { get; }
    private readonly IUiDispatcher _uiDispatcher;

    public WorldManagementPage()
    {
        System.Diagnostics.Debug.WriteLine("[WorldManagementPage] 构造函数开始");
        
        try
        {
            ViewModel = App.GetService<WorldManagementViewModel>();
            _uiDispatcher = App.GetService<IUiDispatcher>();
            System.Diagnostics.Debug.WriteLine("[WorldManagementPage] ViewModel 已获取");
            
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[WorldManagementPage] InitializeComponent 完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagementPage] 构造函数异常: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[WorldManagementPage] 堆栈: {ex.StackTrace}");
            throw;
        }
    }

    private string? _pendingWorldPath;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[WorldManagementPage] OnNavigatedTo 开始, 参数类型: {e.Parameter?.GetType().Name}");
        
        try
        {
            base.OnNavigatedTo(e);
            System.Diagnostics.Debug.WriteLine("[WorldManagementPage] base.OnNavigatedTo 完成");
            
            if (e.Parameter is Models.WorldManagementParameter param)
            {
                System.Diagnostics.Debug.WriteLine($"[WorldManagementPage] 保存待加载路径: {param.WorldPath} 版本: {param.VersionId}");
                _pendingWorldPath = param.WorldPath;
                ViewModel.CurrentVersionId = param.VersionId;
            }
            else if (e.Parameter is string worldPath)
            {
                System.Diagnostics.Debug.WriteLine($"[WorldManagementPage] 保存待加载路径: {worldPath}");
                _pendingWorldPath = worldPath;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[WorldManagementPage] 参数不是字符串: {e.Parameter}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagementPage] OnNavigatedTo 异常: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[WorldManagementPage] 堆栈: {ex.StackTrace}");
        }
    }
    
    private async void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[WorldManagementPage] Page_Loaded 开始");
        
        if (_pendingWorldPath != null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[WorldManagementPage] 页面已加载，开始初始化 ViewModel: {_pendingWorldPath}");
                await ViewModel.InitializeAsync(_pendingWorldPath);
                System.Diagnostics.Debug.WriteLine("[WorldManagementPage] ViewModel 初始化完成");
                _pendingWorldPath = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorldManagementPage] Page_Loaded 异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[WorldManagementPage] 堆栈: {ex.StackTrace}");
            }
        }
    }
    
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[WorldManagementPage] OnNavigatedFrom 开始");
        base.OnNavigatedFrom(e);
        ViewModel.Cleanup();
        System.Diagnostics.Debug.WriteLine("[WorldManagementPage] OnNavigatedFrom 完成");
    }

    private async void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        await Task.CompletedTask;
    }

    private async void WorldTabSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is not SelectorBarItem selectedItem)
        {
            return;
        }

        var newTabIndex = sender.Items.IndexOf(selectedItem);
        if (newTabIndex < 0)
        {
            return;
        }

        ViewModel.SelectedTabIndex = newTabIndex;
        if (newTabIndex == 1)
        {
            EnsureDataPacksPage();
        }

        await ViewModel.OnSelectedTabChangedAsync(newTabIndex);
    }

    private void CopySeedButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ViewModel.Seed))
        {
            return;
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(ViewModel.Seed);
        Clipboard.SetContent(dataPackage);

        if (sender is not Button button)
        {
            return;
        }

        var originalToolTip = ToolTipService.GetToolTip(button);
        ToolTipService.SetToolTip(button, "已复制！");

        _ = new System.Threading.Timer(_ =>
        {
            _uiDispatcher.TryEnqueue(() =>
            {
                ToolTipService.SetToolTip(button, originalToolTip);
            });
        }, null, 2000, System.Threading.Timeout.Infinite);
    }

    private void EnsureDataPacksPage()
    {
        if (DataPacksFrame.Content is WorldDataPacksPage)
        {
            return;
        }

        DataPacksFrame.Navigate(typeof(WorldDataPacksPage), ViewModel);
    }
}
