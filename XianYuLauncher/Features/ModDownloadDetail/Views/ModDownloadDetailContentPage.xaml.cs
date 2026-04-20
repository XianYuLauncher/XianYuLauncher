using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Features.ModDownloadDetail.Models;
using XianYuLauncher.Features.ModDownloadDetail.ViewModels;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Features.ModDownloadDetail.Views
{
    /// <summary>
    /// Mod 下载详情正文页，由宿主页统一承载 Header 与本地导航。
    /// </summary>
    public sealed partial class ModDownloadDetailContentPage : Page, IHostedLocalPage
    {
        private EventHandler? _closeRequested;

        public ModDownloadDetailViewModel ViewModel { get; }

        public IPageHeaderAware HeaderSource => ViewModel;

        /// <summary>
        /// 当前页面本身不会主动请求关闭；保留标准事件实现以满足接口契约并允许外部正常订阅。
        /// 如后续新增关闭行为，请在对应时机触发该事件。
        /// </summary>
        public event EventHandler? CloseRequested
        {
            add => _closeRequested += value;
            remove => _closeRequested -= value;
        }

        public event EventHandler<ModDownloadDetailNavigationRequestedEventArgs>? DetailNavigationRequested
        {
            add => ViewModel.DetailNavigationRequested += value;
            remove => ViewModel.DetailNavigationRequested -= value;
        }

        private bool _isDescriptionExpanded = false;

        public ModDownloadDetailContentPage()
        {
            ViewModel = App.GetService<ModDownloadDetailViewModel>();
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ResetDescriptionState();

            if (e.Parameter is ModDownloadDetailNavigationParameter navigationParameter)
            {
                await ViewModel.LoadModDetailsAsync(navigationParameter);
            }
            else if (e.Parameter is Tuple<XianYuLauncher.Core.Models.ModrinthProject, string> tuple)
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

        public void ResetEmbeddedVisualState()
        {
            ContentArea.Opacity = 1;
            ContentArea.Translation = default;
        }

        private void AuthorButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AuthorTextBlock.TextDecorations = Windows.UI.Text.TextDecorations.Underline;
        }

        private void AuthorButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AuthorTextBlock.TextDecorations = Windows.UI.Text.TextDecorations.None;
        }

        private void ToggleDescriptionButton_Click(object sender, RoutedEventArgs e)
        {
            _isDescriptionExpanded = !_isDescriptionExpanded;

            FullDescriptionContainer.Visibility = _isDescriptionExpanded
                ? Visibility.Visible
                : Visibility.Collapsed;

            ToggleDescriptionText.Text = _isDescriptionExpanded
                ? "ModDownloadDetailPage_CollapseDescription".GetLocalized()
                : "ModDownloadDetailPage_ViewFullDescription".GetLocalized();

            AnimateArrow(_isDescriptionExpanded);
        }

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
            sb.Completed += (_, _) =>
            {
                rotation.Angle = expand ? 180 : 0;
            };
            sb.Begin();
        }

        private void ResetDescriptionState()
        {
            _isDescriptionExpanded = false;
            FullDescriptionContainer.Visibility = Visibility.Collapsed;
            ((RotateTransform)ToggleDescriptionIcon.RenderTransform).Angle = 0;
            ToggleDescriptionText.Text = "ModDownloadDetailPage_ViewFullDescription".GetLocalized();
        }
    }
}