using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using Windows.ApplicationModel.Resources;

using XianYuLauncher.Features.News.ViewModels;

namespace XianYuLauncher.Features.News.Views;

public sealed partial class NewsListPage : Page
{
    private readonly ResourceLoader _resourceLoader;
    private bool _isInnerContentFrameInitialized;

    public NewsListViewModel ViewModel { get; }

    public NewsListPage()
    {
        _resourceLoader = ResourceLoader.GetForViewIndependentUse();
        ViewModel = App.GetService<NewsListViewModel>();
        InitializeComponent();
        InitializeHeader();
        EnsureInnerContentFrame();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        EnsureInnerContentFrame();
    }

    private void InitializeHeader()
    {
        NewsListPageHeader.Title = GetResourceString("NewsListPage_Title.Text", "新闻与公告");
        NewsListPageHeader.Subtitle = GetResourceString("NewsListPage_Subtitle.Text", "Minecraft Java 版更新动态");
        NewsListPageHeader.ShowBreadcrumb = false;
        NewsListPageHeader.BreadcrumbItems = null;
    }

    private void EnsureInnerContentFrame()
    {
        if (_isInnerContentFrameInitialized)
        {
            return;
        }

        NewsListInnerContentFrame.Navigate(typeof(NewsListRootPage), ViewModel);
        _isInnerContentFrameInitialized = true;
    }

    private string GetResourceString(string key, string fallback)
    {
        var value = _resourceLoader.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}