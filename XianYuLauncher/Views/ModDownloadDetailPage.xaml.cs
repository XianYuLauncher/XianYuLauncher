using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using XianYuLauncher.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace XianYuLauncher.Views
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

        private SemaphoreSlim _dialogSemaphore = new SemaphoreSlim(1, 1);

        private async Task ShowDialogSafeAsync(ContentDialog dialog)
        {
            try
            {
                // 等待上一个弹窗完全关闭
                await _dialogSemaphore.WaitAsync();
                
                // 增加一个小延迟，确保WinUI内部状态已重置
                // 许多COM异常是因为上一个弹窗的关闭动画还没完全结束就开始显示下一个
                await Task.Delay(50);
                
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                // 忽略特定错误，避免崩溃
                System.Diagnostics.Debug.WriteLine($"[ModDownloadDetailPage] 弹窗显示异常: {ex.Message}");
            }
            finally
            {
                _dialogSemaphore.Release();
            }
        }

        // 处理ViewModel属性变化
        private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsDownloadDialogOpen))
            {
                if (ViewModel.IsDownloadDialogOpen)
                {
                    await ShowDialogSafeAsync(DownloadDialog);
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
                    await ShowDialogSafeAsync(VersionSelectionDialog);
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
                    await ShowDialogSafeAsync(ModpackInstallDialog);
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
                    await ShowDialogSafeAsync(SaveSelectionDialog);
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
                    await ShowDialogSafeAsync(DownloadProgressDialog);
                }
                else
                {
                    DownloadProgressDialog.Hide();
                }
            }
            else if (e.PropertyName == nameof(ViewModel.IsQuickInstallGameVersionDialogOpen))
            {
                if (ViewModel.IsQuickInstallGameVersionDialogOpen)
                {
                    await ShowDialogSafeAsync(QuickInstallGameVersionDialog);
                }
                else
                {
                    QuickInstallGameVersionDialog.Hide();
                }
            }
            else if (e.PropertyName == nameof(ViewModel.IsQuickInstallModVersionDialogOpen))
            {
                if (ViewModel.IsQuickInstallModVersionDialogOpen)
                {
                    await QuickInstallModVersionDialog.ShowAsync();
                }
                else
                {
                    QuickInstallModVersionDialog.Hide();
                }
            }
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
            
            // 清理ViewModel资源
            ViewModel.OnNavigatedFrom();
        }

        private void BackButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // 根据当前资源类型设置返回的Tab索引
            ResourceDownloadPage.TargetTabIndex = ViewModel.ProjectType switch
            {
                "mod" => 1,           // Mod下载
                "shader" => 2,        // 光影下载
                "resourcepack" => 3,  // 资源包下载
                "datapack" => 4,      // 数据包下载
                "modpack" => 5,       // 整合包下载
                "world" => 6,         // 世界下载
                _ => 0                // 默认：版本下载
            };
            
            // 返回上一页
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void GameVersionButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // 切换游戏版本的展开状态
            if (sender is Button button && button.Tag is XianYuLauncher.ViewModels.GameVersionViewModel viewModel)
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
        
        // 下载进度弹窗 - 后台下载按钮点击事件
        private void DownloadProgressDialog_BackgroundButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 启动后台下载
            ViewModel.StartBackgroundDownload();
        }
        
        // 一键安装 - 游戏版本选择弹窗 - 下一步按钮点击事件
        private async void QuickInstallGameVersionDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ViewModel.SelectedQuickInstallVersion == null)
            {
                args.Cancel = true;
                await ViewModel.ShowMessageAsync("请选择一个游戏版本");
                return;
            }
            
            ViewModel.IsQuickInstallGameVersionDialogOpen = false;
            await Task.Delay(100);
            await ViewModel.ShowQuickInstallModVersionSelectionAsync();
        }
        
        // 一键安装 - 游戏版本选择弹窗 - 取消按钮点击事件
        private void QuickInstallGameVersionDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.IsQuickInstallGameVersionDialogOpen = false;
        }
        
        // 一键安装 - Mod版本选择弹窗 - 安装按钮点击事件
        private async void QuickInstallModVersionDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ViewModel.SelectedQuickInstallModVersion == null)
            {
                args.Cancel = true;
                await ViewModel.ShowMessageAsync("请选择一个Mod版本");
                return;
            }
            
            ViewModel.IsQuickInstallModVersionDialogOpen = false;
            await Task.Delay(100);
            await ViewModel.DownloadModVersionToGameAsync(ViewModel.SelectedQuickInstallModVersion, ViewModel.SelectedQuickInstallVersion);
        }
        
        // 一键安装 - Mod版本选择弹窗 - 取消按钮点击事件
        private void QuickInstallModVersionDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.IsQuickInstallModVersionDialogOpen = false;
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
