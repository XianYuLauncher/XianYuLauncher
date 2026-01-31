using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Models;

namespace XianYuLauncher.Views;

public sealed partial class WorldManagementPage : Page
{
    public WorldManagementViewModel ViewModel { get; }

    public WorldManagementPage()
    {
        System.Diagnostics.Debug.WriteLine("[WorldManagementPage] 构造函数开始");
        
        try
        {
            ViewModel = App.GetService<WorldManagementViewModel>();
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
    
    private int _currentTabIndex = 0;
    private bool _isFirstNavigation = true;

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
                
                // 初始化完成后，导航到概览页并传递 ViewModel，首次导航不使用动画
                if (ContentFrame != null)
                {
                    ContentFrame.Navigate(typeof(WorldOverviewPage), ViewModel, new SuppressNavigationTransitionInfo());
                    _isFirstNavigation = false;
                }
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
        if (args.SelectedItem is NavigationViewItem item && ContentFrame != null)
        {
            var tag = item.Tag?.ToString();
            int newTabIndex = 0;
            Type pageType = typeof(WorldOverviewPage);
            
            // 确定要导航的页面和索引
            switch (tag)
            {
                case "overview":
                    newTabIndex = 0;
                    pageType = typeof(WorldOverviewPage);
                    break;
                case "datapacks":
                    newTabIndex = 1;
                    pageType = typeof(WorldDataPacksPage);
                    break;
                default:
                    return;
            }
            
            // 确定滑动方向
            var effect = newTabIndex > _currentTabIndex 
                ? SlideNavigationTransitionEffect.FromRight 
                : SlideNavigationTransitionEffect.FromLeft;
            
            // 导航到新页面，带滑动动画，并传递 ViewModel
            ContentFrame.Navigate(pageType, ViewModel, new SlideNavigationTransitionInfo { Effect = effect });
            
            _currentTabIndex = newTabIndex;
            
            // 触发延迟加载
            await ViewModel.OnSelectedTabChangedAsync(newTabIndex);
        }
    }
}
