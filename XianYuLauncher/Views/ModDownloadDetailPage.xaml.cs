using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.Helpers;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views
{
    /// <summary>
    /// Mod下载详情页面 — 所有弹窗已迁移至 DialogService
    /// </summary>
    public sealed partial class ModDownloadDetailPage : Page
    {
        public ModDownloadDetailViewModel ViewModel { get; }

        // 描述展开/收起状态
        private bool _isDescriptionExpanded = false;

        public ModDownloadDetailPage()
        {
            ViewModel = App.GetService<ModDownloadDetailViewModel>();
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 重置描述展开状态
            ResetDescriptionState();

            if (e.Parameter is Tuple<XianYuLauncher.Core.Models.ModrinthProject, string> tuple)
            {
                await ViewModel.LoadModDetailsAsync(tuple.Item1, tuple.Item2);
            }
            else if (e.Parameter is XianYuLauncher.Core.Models.ModrinthProject mod)
            {
                await ViewModel.LoadModDetailsAsync(mod, null);
            }
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
            if (sender is Button button && button.Tag is GameVersionViewModel viewModel)
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

        /// <summary>
        /// 展开/收起完整描述
        /// </summary>
        private void ToggleDescriptionButton_Click(object sender, RoutedEventArgs e)
        {
            _isDescriptionExpanded = !_isDescriptionExpanded;

            // Composition 动画由 Implicit.ShowAnimations / HideAnimations 自动触发
            FullDescriptionContainer.Visibility = _isDescriptionExpanded 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            // 更新按钮文本
            ToggleDescriptionText.Text = _isDescriptionExpanded
                ? "ModDownloadDetailPage_CollapseDescription".GetLocalized()
                : "ModDownloadDetailPage_ViewFullDescription".GetLocalized();

            // 箭头旋转动画（Storyboard，但 RotateTransform 不是依赖属性动画，很轻量）
            AnimateArrow(_isDescriptionExpanded);
        }

        /// <summary>
        /// 箭头旋转动画
        /// </summary>
        private void AnimateArrow(bool expand)
        {
            var rotation = (RotateTransform)ToggleDescriptionIcon.RenderTransform;
            var from = expand ? 0d : 180d;
            var to = expand ? 180d : 360d;

            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new PowerEase { Power = 3, EasingMode = EasingMode.EaseOut },
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(animation, rotation);
            Storyboard.SetTargetProperty(animation, "Angle");

            var sb = new Storyboard();
            sb.Children.Add(animation);
            sb.Completed += (s, args) =>
            {
                rotation.Angle = expand ? 180 : 0;
            };
            sb.Begin();
        }

        /// <summary>
        /// 重置描述展开状态（导航时调用）
        /// </summary>
        private void ResetDescriptionState()
        {
            _isDescriptionExpanded = false;
            FullDescriptionContainer.Visibility = Visibility.Collapsed;
            ((RotateTransform)ToggleDescriptionIcon.RenderTransform).Angle = 0;
            ToggleDescriptionText.Text = "ModDownloadDetailPage_ViewFullDescription".GetLocalized();
        }
    }
}
