using Microsoft.UI.Xaml; using Microsoft.UI.Xaml.Controls; using Microsoft.UI.Xaml.Input; using Microsoft.UI.Xaml.Navigation; using XMCL2025.ViewModels;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace XMCL2025.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class 角色Page : Page
    {
        public 角色ViewModel ViewModel
        {
            get;
        }

        public 角色Page()
        {
            ViewModel = App.GetService<角色ViewModel>();
            InitializeComponent();
            
            // 订阅显示离线登录对话框的事件
            ViewModel.RequestShowOfflineLoginDialog += (sender, e) =>
            {
                ShowOfflineLoginDialog();
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // 页面导航到时时的初始化逻辑
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            // 页面导航离开时的清理逻辑
        }

        /// <summary>
        /// 角色卡片点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void ProfileCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Border border && border.Tag is MinecraftProfile profile)
            {
                ViewModel.SwitchProfileCommand.Execute(profile);
            }
        }

        /// <summary>
        /// 离线登录菜单项点击事件
        /// </summary>
        private void OfflineLoginMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 直接调用显示对话框的方法
            ShowOfflineLoginDialog();
        }

        /// <summary>
        /// 微软登录菜单项点击事件
        /// </summary>
        private async void MicrosoftLoginMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 直接调用微软登录方法
            await ViewModel.StartMicrosoftLoginCommand.ExecuteAsync(null);
        }

        /// <summary>
        /// 显示离线登录对话框
        /// </summary>
        public async void ShowOfflineLoginDialog()
        {
            // 创建一个简单的StackPanel作为对话框内容
            var stackPanel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };

            // 添加提示文本
            var textBlock = new TextBlock
            {
                Text = "请输入离线用户名",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            stackPanel.Children.Add(textBlock);

            // 添加文本框
            var textBox = new TextBox
            {
                PlaceholderText = "输入用户名",
                Width = 300,
                Margin = new Thickness(0, 4, 0, 0)
            };
            stackPanel.Children.Add(textBox);

            // 创建ContentDialog
            var dialog = new ContentDialog
            {
                Title = "离线登录",
                Content = stackPanel,
                PrimaryButtonText = "确定",
                SecondaryButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            // 显示对话框并获取结果
            var result = await dialog.ShowAsync();

            // 根据结果执行操作
            if (result == ContentDialogResult.Primary)
            {
                // 使用用户输入的用户名或默认用户名
                string username = !string.IsNullOrWhiteSpace(textBox.Text) ? textBox.Text : "Player";
                ViewModel.OfflineUsername = username;
                ViewModel.ConfirmOfflineLoginCommand.Execute(null);
            }
        }
    }
}