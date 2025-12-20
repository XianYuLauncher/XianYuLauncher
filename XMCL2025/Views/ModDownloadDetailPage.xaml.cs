using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using XMCL2025.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace XMCL2025.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ModDownloadDetailPage : Page
    {
        public ModDownloadDetailViewModel ViewModel { get; }

        public ModDownloadDetailPage()
        {
            ViewModel = App.GetService<ModDownloadDetailViewModel>();
            this.InitializeComponent();
            // 监听ViewModel的IsDownloadDialogOpen属性变化
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        // 处理ViewModel属性变化
        private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsDownloadDialogOpen))
            {
                if (ViewModel.IsDownloadDialogOpen)
                {
                    await DownloadDialog.ShowAsync();
                }
                else
                {
                    DownloadDialog.Hide();
                }
            }
            else if (e.PropertyName == nameof(ViewModel.IsVersionSelectionDialogOpen))
            {
                if (ViewModel.IsVersionSelectionDialogOpen)
                {
                    await VersionSelectionDialog.ShowAsync();
                }
                else
                {
                    VersionSelectionDialog.Hide();
                }
            }
            else if (e.PropertyName == nameof(ViewModel.IsModpackInstallDialogOpen))
            {
                if (ViewModel.IsModpackInstallDialogOpen)
                {
                    await ModpackInstallDialog.ShowAsync();
                }
                else
                {
                    ModpackInstallDialog.Hide();
                }
            }
            else if (e.PropertyName == nameof(ViewModel.IsSaveSelectionDialogOpen))
            {
                if (ViewModel.IsSaveSelectionDialogOpen)
                {
                    await SaveSelectionDialog.ShowAsync();
                }
                else
                {
                    SaveSelectionDialog.Hide();
                }
            }
            else if (e.PropertyName == nameof(ViewModel.IsDownloadProgressDialogOpen))
            {
                if (ViewModel.IsDownloadProgressDialogOpen)
                {
                    await DownloadProgressDialog.ShowAsync();
                }
                else
                {
                    DownloadProgressDialog.Hide();
                }
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 接收从搜索页面传递的完整Mod对象
            if (e.Parameter is XMCL2025.Core.Models.ModrinthProject mod)
            {
                await ViewModel.LoadModDetailsAsync(mod);
            }
            // 兼容旧的导航方式（仅传递ID）
            else if (e.Parameter is string modId)
            {
                await ViewModel.LoadModDetailsAsync(modId);
            }
        }

        private void BackButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // 返回上一页
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void GameVersionButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // 切换游戏版本的展开状态
            if (sender is Button button && button.Tag is XMCL2025.ViewModels.GameVersionViewModel viewModel)
            {
                viewModel.IsExpanded = !viewModel.IsExpanded;
            }
        }

        // 下载弹窗 - 选择版本按钮点击事件
        private async void DownloadDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 关闭下载对话框
            args.Cancel = true; // 取消默认关闭行为
            sender.Hide(); // 显式隐藏对话框
            ViewModel.IsDownloadDialogOpen = false;
            
            // 使用Task.Delay确保对话框完全关闭后再打开新对话框
            await Task.Delay(100);
            await ViewModel.DownloadToSelectedVersionCommand.ExecuteAsync(null);
        }

        // 下载弹窗 - 自定义位置按钮点击事件
        private async void DownloadDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 先关闭下载对话框
            args.Cancel = true; // 取消默认关闭行为
            sender.Hide(); // 显式隐藏对话框
            ViewModel.IsDownloadDialogOpen = false;
            
            // 使用Task.Delay确保对话框完全关闭后再打开文件选择器
            await Task.Delay(100);
            
            // 执行自定义位置下载逻辑
            await HandleCustomLocationDownload();
        }
        
        // 处理自定义位置下载的方法
        private async Task HandleCustomLocationDownload()
        {
            if (ViewModel.SelectedModVersion == null)
            {
                await ViewModel.ShowMessageAsync("请先选择要下载的Mod版本");
                return;
            }
            
            // 打开文件保存对话框
            var filePicker = new FileSavePicker();
            
            // 设置文件选择器的起始位置
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, windowHandle);
            
            // 设置默认文件名（从API获取）
            filePicker.SuggestedFileName = ViewModel.SelectedModVersion.FileName;
            
            // 设置文件类型
            filePicker.FileTypeChoices.Add("Mod文件", new[] { ".jar" });
            
            // 显示文件选择器
            var file = await filePicker.PickSaveFileAsync();
            
            if (file != null)
            {
                // 获取选择的文件夹路径（不包含文件名）
                string folderPath = Path.GetDirectoryName(file.Path);
                
                // 设置自定义下载路径
                ViewModel.SetCustomDownloadPath(folderPath);
                
                // 开始下载
                await ViewModel.DownloadModAsync(ViewModel.SelectedModVersion);
            }
        }

        // 下载弹窗 - 取消按钮点击事件
        private void DownloadDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.CancelDownloadCommand.Execute(null);
            ViewModel.IsDownloadDialogOpen = false;
        }

        private async void VersionSelectionDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.IsVersionSelectionDialogOpen = false;
            await ViewModel.ConfirmDownloadAsync();
        }

        private void VersionSelectionDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.IsVersionSelectionDialogOpen = false;
        }

        // 存档选择弹窗 - 确认按钮点击事件
        private async void SaveSelectionDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.IsSaveSelectionDialogOpen = false;
            // 继续下载流程，调用ViewModel中的方法完成下载
            await ViewModel.CompleteDatapackDownloadAsync();
        }

        // 存档选择弹窗 - 取消按钮点击事件
        private void SaveSelectionDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.IsSaveSelectionDialogOpen = false;
            ViewModel.SelectedSaveName = null;
        }

        // 整合包安装弹窗 - 取消按钮点击事件
        private void ModpackInstallDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.CancelInstallCommand.Execute(null);
        }
        
        // 下载进度弹窗 - 取消按钮点击事件
        private void DownloadProgressDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.CancelDownloadCommand.Execute(null);
        }
    }
}
