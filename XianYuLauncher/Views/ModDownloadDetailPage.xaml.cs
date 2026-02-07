using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views
{
    /// <summary>
    /// Mod下载详情页面 — 所有弹窗已迁移至 DialogService
    /// </summary>
    public sealed partial class ModDownloadDetailPage : Page
    {
        public ModDownloadDetailViewModel ViewModel { get; }

        public ModDownloadDetailPage()
        {
            ViewModel = App.GetService<ModDownloadDetailViewModel>();
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 接收从搜索页面传递的完整Mod对象和来源类型
            if (e.Parameter is Tuple<XianYuLauncher.Core.Models.ModrinthProject, string> tuple)
            {
                await ViewModel.LoadModDetailsAsync(tuple.Item1, tuple.Item2);
            }
            // 兼容旧的导航方式（仅传递Mod对象）
            else if (e.Parameter is XianYuLauncher.Core.Models.ModrinthProject mod)
            {
                await ViewModel.LoadModDetailsAsync(mod, null);
            }
            // 兼容旧的导航方式（仅传递ID）
            else if (e.Parameter is string modId)
            {
                await ViewModel.LoadModDetailsAsync(modId);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.OnNavigatedFrom();
        }

        private void BackButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // 根据当前资源类型设置返回的Tab索引
            ResourceDownloadPage.TargetTabIndex = ViewModel.ProjectType switch
            {
                "mod" => 1,
                "shader" => 2,
                "resourcepack" => 3,
                "datapack" => 4,
                "modpack" => 5,
                "world" => 6,
                _ => 0
            };

            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void GameVersionButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is XianYuLauncher.ViewModels.GameVersionViewModel viewModel)
            {
                viewModel.IsExpanded = !viewModel.IsExpanded;
            }
        }

        private void AuthorButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AuthorTextBlock.TextDecorations = Windows.UI.Text.TextDecorations.Underline;
        }

        private void AuthorButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AuthorTextBlock.TextDecorations = Windows.UI.Text.TextDecorations.None;
        }
    }
}
