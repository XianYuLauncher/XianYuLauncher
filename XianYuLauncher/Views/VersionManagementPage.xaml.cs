using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Views;

public sealed partial class VersionManagementPage : Page
{
    public VersionManagementViewModel ViewModel { get; }

    public VersionManagementPage()
    {
        ViewModel = App.GetService<VersionManagementViewModel>();
        this.DataContext = ViewModel;
        InitializeComponent();
        
        // 立即注册ViewModel的属性变化事件，确保OnNavigatedTo时能触发
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        
        // 初始更新标题
        UpdatePageTitle();
    }
    
    /// <summary>
        /// 监听ViewModel属性变化，显示或隐藏弹窗
        /// </summary>
        private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == nameof(ViewModel.IsInstallingExtension))
                {
                    if (ViewModel.IsInstallingExtension)
                    {
                        // 关闭所有可能打开的弹窗
                        MoveModsDialog.Hide();
                        ResultDialog.Hide();
                        MoveResultDialog.Hide();
                        DownloadProgressDialog.Hide();
                        
                        // 等待足够长的时间，确保所有弹窗完全关闭
                        await Task.Delay(100);
                        
                        // 显示扩展安装进度弹窗
                        await ExtensionInstallDialog.ShowAsync();
                    }
                    else
                    {
                        // 关闭扩展安装进度弹窗
                        ExtensionInstallDialog.Hide();
                    }
                }
                else if (e.PropertyName == nameof(ViewModel.IsDownloading) && ViewModel.IsDownloading)
                {
                    // 关闭所有可能打开的弹窗
                    MoveModsDialog.Hide();
                    ResultDialog.Hide();
                    MoveResultDialog.Hide();
                    ExtensionInstallDialog.Hide();
                    
                    // 等待足够长的时间，确保所有弹窗完全关闭
                    await Task.Delay(100);
                    
                    // 显示下载进度弹窗
                    await DownloadProgressDialog.ShowAsync();
                }
                else if (e.PropertyName == nameof(ViewModel.IsResultDialogVisible) && ViewModel.IsResultDialogVisible)
                {
                    // 关闭下载进度弹窗（如果显示）
                    DownloadProgressDialog.Hide();
                    ExtensionInstallDialog.Hide();
                    
                    // 等待足够长的时间，确保弹窗完全关闭
                    await Task.Delay(100);
                    
                    // 显示结果弹窗
                    await ResultDialog.ShowAsync();
                }
                else if (e.PropertyName == nameof(ViewModel.IsMoveModsDialogVisible) && ViewModel.IsMoveModsDialogVisible)
                {
                    // 关闭所有可能打开的弹窗
                    DownloadProgressDialog.Hide();
                    ResultDialog.Hide();
                    MoveResultDialog.Hide();
                    ExtensionInstallDialog.Hide();
                    
                    // 等待足够长的时间，确保所有弹窗完全关闭
                    await Task.Delay(100);
                    
                    // 显示转移Mod到其他版本弹窗
                    await MoveModsDialog.ShowAsync();
                }
                else if (e.PropertyName == nameof(ViewModel.IsMoveResultDialogVisible) && ViewModel.IsMoveResultDialogVisible)
                {
                    // 关闭下载进度弹窗（如果显示）
                    DownloadProgressDialog.Hide();
                    ExtensionInstallDialog.Hide();
                    
                    // 等待足够长的时间，确保弹窗完全关闭
                    await Task.Delay(100);
                    
                    // 显示转移结果弹窗
                    await MoveResultDialog.ShowAsync();
                }
                else if (e.PropertyName == nameof(ViewModel.SelectedVersion))
                {
                    // 更新页面标题
                    UpdatePageTitle();
                }
            }
            catch (Exception ex)
            {
                // 处理Dialog显示异常
                System.Diagnostics.Debug.WriteLine($"显示弹窗失败: {ex.Message}");
            }
        }
    
    /// <summary>
    /// 结果弹窗确定按钮点击事件处理
    /// </summary>
    private void ResultDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 关闭结果弹窗
        ViewModel.IsResultDialogVisible = false;
    }
    
    /// <summary>
        /// 转移Mod到其他版本弹窗 - 确认按钮点击事件
        /// </summary>
        private async void MoveModsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 关闭当前弹窗
            sender.Hide();
            ViewModel.IsMoveModsDialogVisible = false;
            
            // 等待足够长的时间，确保弹窗完全关闭
            await Task.Delay(200);
            
            // 调用ViewModel的确认转移命令
            await ViewModel.ConfirmMoveModsCommand.ExecuteAsync(null);
        }
    
    /// <summary>
        /// 转移Mod到其他版本弹窗 - 取消按钮点击事件
        /// </summary>
        private void MoveModsDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 关闭弹窗
            ViewModel.IsMoveModsDialogVisible = false;
        }
        
        /// <summary>
        /// 转移Mod结果弹窗 - 确定按钮点击事件
        /// </summary>
        private void MoveResultDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 关闭转移结果弹窗
            ViewModel.IsMoveResultDialogVisible = false;
        }
    
    /// <summary>
    /// 更新页面标题
    /// </summary>
    private void UpdatePageTitle()
    {
        if (ViewModel.SelectedVersion != null)
        {
            PageTitle.Text = $"{"VersionManagerPage_Title".GetLocalized()} - {ViewModel.SelectedVersion.Name}";
            PageSubtitle.Text = $"{"VersionManagerPage_Subtitle_WithVersion".GetLocalized()} {ViewModel.SelectedVersion.Name} {"VersionManagerPage_Subtitle_Components".GetLocalized()}";
        }
        else
        {
            PageTitle.Text = "VersionManagerPage_Title".GetLocalized();
            PageSubtitle.Text = "VersionManagerPage_Subtitle_SelectVersion".GetLocalized();
        }
    }
    
    #region 拖放事件处理
    
    private void ContentArea_DragEnter(object sender, DragEventArgs e)
    {
        // 检查拖放的内容是否包含文件
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            // 显示拖放状态视觉反馈
            ShowDragDropFeedback(true);
        }
    }
    
    private void ContentArea_DragOver(object sender, DragEventArgs e)
    {
        // 检查拖放的内容是否包含文件
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }
    
    private void ContentArea_DragLeave(object sender, DragEventArgs e)
    {
        // 隐藏拖放状态视觉反馈
        ShowDragDropFeedback(false);
    }
    
    private async void ContentArea_Drop(object sender, DragEventArgs e)
    {
        // 隐藏拖放状态视觉反馈
        ShowDragDropFeedback(false);
        
        // 检查拖放的内容是否包含文件
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items != null && items.Count > 0)
            {
                // 调用ViewModel的拖放处理方法
                await ViewModel.HandleDragDropFilesAsync(items);
            }
        }
    }
    
    private void ShowDragDropFeedback(bool isVisible)
    {
        // 显示或隐藏拖放状态视觉反馈覆盖层，使用动画效果
        if (isVisible)
        {
            DragDropOverlay.Opacity = 0;
            DragDropOverlay.Visibility = Visibility.Visible;
            
            // 创建并启动淡入动画
            var fadeInStoryboard = new Storyboard();
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new QuadraticEase()
            };
            Storyboard.SetTarget(fadeInAnimation, DragDropOverlay);
            Storyboard.SetTargetProperty(fadeInAnimation, "Opacity");
            fadeInStoryboard.Children.Add(fadeInAnimation);
            fadeInStoryboard.Begin();
        }
        else
        {
            // 直接隐藏
            DragDropOverlay.Visibility = Visibility.Collapsed;
            DragDropOverlay.Opacity = 0;
        }
    }
    
    #endregion
    
    #region Mod开关事件处理
    
    /// <summary>
    /// 处理Mod启用/禁用开关的Toggled事件
    /// </summary>
    /// <param name="sender">发送事件的ToggleSwitch控件</param>
    /// <param name="e">事件参数</param>
    private async void ModToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            // 获取ToggleSwitch的IsOn值
            bool isOn = toggleSwitch.IsOn;
            
            // 直接从父级Grid获取DataContext
            if (VisualTreeHelper.GetParent(toggleSwitch) is FrameworkElement parentElement && parentElement.DataContext is ViewModels.ModInfo modInfo)
            {
                // 直接调用ViewModel的方法来处理开关状态变化，传递当前IsOn值
                await ViewModel.ToggleModEnabledAsync(modInfo, isOn);
            }
        }
    }
    
    #endregion
    
    #region 扩展Tab事件处理
    
    /// <summary>
    /// 处理加载器Expander展开事件，加载版本列表
    /// </summary>
    private async void LoaderExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] LoaderExpander_Expanding 事件被触发");
        
        // 从 Expander.Header 的 DataContext 获取 LoaderItemViewModel
        if (sender.Header is Grid headerGrid && headerGrid.DataContext is LoaderItemViewModel loader)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 从 Header.DataContext 找到 LoaderItemViewModel: {loader.Name}");
            await ViewModel.LoadLoaderVersionsAsync(loader);
        }
        // 尝试从 Tag 获取
        else if (sender.Tag is LoaderItemViewModel loaderFromTag)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 从 Tag 找到 LoaderItemViewModel: {loaderFromTag.Name}");
            await ViewModel.LoadLoaderVersionsAsync(loaderFromTag);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] 无法获取 LoaderItemViewModel！");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] sender.Header 类型: {sender.Header?.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] sender.Tag 类型: {sender.Tag?.GetType().Name}");
        }
    }
    
    /// <summary>
    /// 处理版本列表选择变化事件 - 只更新临时选择状态，并处理互斥逻辑
    /// </summary>
    private void LoaderVersionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] LoaderVersionListView_SelectionChanged 事件被触发");
        
        if (sender is ListView listView && listView.Tag is LoaderItemViewModel loader)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 选中版本: {loader.SelectedVersion}");
            
            // 如果选择了版本，需要处理互斥逻辑
            if (!string.IsNullOrEmpty(loader.SelectedVersion))
            {
                // 获取当前选择的加载器类型
                string currentLoaderType = loader.LoaderType.ToLower();
                
                // 清除其他互斥的加载器选择
                // 规则：只有 Forge 和 Optifine 可以同时选择，其他都互斥
                foreach (var otherLoader in ViewModel.AvailableLoaders)
                {
                    if (otherLoader == loader)
                        continue;
                    
                    string otherLoaderType = otherLoader.LoaderType.ToLower();
                    
                    // 检查是否需要清除
                    bool shouldClear = false;
                    
                    if (currentLoaderType == "forge" && otherLoaderType == "optifine")
                    {
                        // Forge 和 Optifine 可以共存
                        shouldClear = false;
                    }
                    else if (currentLoaderType == "optifine" && otherLoaderType == "forge")
                    {
                        // Optifine 和 Forge 可以共存
                        shouldClear = false;
                    }
                    else if (!string.IsNullOrEmpty(otherLoader.SelectedVersion))
                    {
                        // 其他情况都互斥
                        shouldClear = true;
                    }
                    
                    if (shouldClear)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 清除互斥的加载器选择: {otherLoader.Name}");
                        otherLoader.SelectedVersion = null;
                        otherLoader.IsExpanded = false;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 处理取消加载器按钮点击事件，卸载加载器
    /// </summary>
    private async void CancelLoader_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] CancelLoader_Click 事件被触发");
        
        if (sender is Button button && button.Tag is LoaderItemViewModel loader)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 准备卸载加载器: {loader.Name}");
            
            // 调用ViewModel的卸载加载器命令
            await ViewModel.UninstallLoaderCommand.ExecuteAsync(loader);
        }
    }
    
    #endregion
}