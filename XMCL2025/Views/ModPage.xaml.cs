using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using XMCL2025.Contracts.Services;
using XMCL2025.ViewModels;
using XMCL2025.Core.Models;

namespace XMCL2025.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ModPage : Page
    {
        public ModViewModel ViewModel
        {
            get;
        }

        private readonly INavigationService _navigationService;

        public ModPage()
        {
            ViewModel = App.GetService<ModViewModel>();
            _navigationService = App.GetService<INavigationService>();
            InitializeComponent();
            // 页面加载时自动执行一次搜索
            ViewModel.SearchModsCommand.Execute(null);
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ViewModel.SearchModsCommand.Execute(null);
        }

        // 由于现在直接在XAML中命名了ScrollViewer并附加了事件，不再需要这些方法
        // 移除了ModListView_Loaded和FindScrollViewer方法

        private void ModListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                // 计算当前滚动位置是否接近底部（距离底部100像素以内）
                var verticalOffset = scrollViewer.VerticalOffset;
                var scrollableHeight = scrollViewer.ScrollableHeight;
                var viewportHeight = scrollViewer.ViewportHeight;
                var shouldLoadMore = !ViewModel.IsLoadingMore && ViewModel.HasMoreResults && (verticalOffset + viewportHeight >= scrollableHeight - 100);

                if (shouldLoadMore)
                {
                    ViewModel.LoadMoreModsCommand.Execute(null);
                }
            }
        }

        private void LoaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 触发搜索命令
            ViewModel.SearchModsCommand.Execute(null);
        }

        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 触发搜索命令
            ViewModel.SearchModsCommand.Execute(null);
        }

        /// <summary>
        /// mod列表项点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void ModItem_Tapped(object sender, RoutedEventArgs e)
        {
            // 获取被点击的ModrinthProject对象
            if (sender is Grid grid && grid.DataContext is ModrinthProject mod)
            {
                // 导航到Mod下载详情页，并传递Mod的ID作为参数
                _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, mod.ProjectId);
            }
        }


    }
}