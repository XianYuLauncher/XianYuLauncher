using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models.VersionManagement;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Canvas;
using System.IO.Compression;
using Windows.Foundation;

namespace XianYuLauncher.Views;

public sealed partial class VersionManagementPage : Page
{
    public VersionManagementViewModel ViewModel { get; }
    
    // 标记页面是否正在卸载
    private bool _isUnloading = false;

    public VersionManagementPage()
    {
        ViewModel = App.GetService<VersionManagementViewModel>();
        this.DataContext = ViewModel;
        InitializeComponent();
        
        // 立即注册ViewModel的属性变化事件，确保OnNavigatedTo时能触发
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        
        // 注册页面卸载事件，快速清理资源
        this.Unloaded += VersionManagementPage_Unloaded;
        
        // 初始更新标题
        UpdatePageTitle();
    }
    
    /// <summary>
    /// 页面卸载时快速清理资源
    /// </summary>
    private void VersionManagementPage_Unloaded(object sender, RoutedEventArgs e)
    {
        // 立即标记为卸载中，阻止所有异步操作
        _isUnloading = true;
        
        // 极速清理策略：在后台线程清理，不阻塞UI
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                // 清理 Canvas 资源
                foreach (var bitmap in _previewBitmaps.Values)
                {
                    try { bitmap?.Dispose(); } catch { }
                }
                _previewBitmaps.Clear();
                _canvasControls.Clear();
                _currentPreviewPack = null;
            }
            catch
            {
                // 完全忽略清理异常
            }
        });
        
        // 取消注册事件，防止内存泄漏
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        this.Unloaded -= VersionManagementPage_Unloaded;
    }
    
    /// <summary>
        /// 监听ViewModel属性变化，显示或隐藏弹窗
        /// </summary>
        private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 如果页面正在卸载，直接返回
            if (_isUnloading)
                return;
                
            try
            {
                if (e.PropertyName == nameof(ViewModel.IsInstallingExtension))
                {
                    if (ViewModel.IsInstallingExtension)
                    {
                        // 关闭所有可能打开的弹窗
                        MoveResourcesDialog.Hide();
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
                    MoveResourcesDialog.Hide();
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
                else if (e.PropertyName == nameof(ViewModel.IsMoveResourcesDialogVisible) && ViewModel.IsMoveResourcesDialogVisible)
                {
                    // 关闭所有可能打开的弹窗
                    DownloadProgressDialog.Hide();
                    ResultDialog.Hide();
                    MoveResultDialog.Hide();
                    ExtensionInstallDialog.Hide();
                    
                    // 等待足够长的时间，确保所有弹窗完全关闭
                    await Task.Delay(100);
                    
                    // 显示转移资源到其他版本弹窗
                    await MoveResourcesDialog.ShowAsync();
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
    /// <summary>
    /// 转移资源到其他版本弹窗 - 确定按钮点击事件
    /// </summary>
    private async void MoveResourcesDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 关闭当前弹窗
        sender.Hide();
        ViewModel.IsMoveResourcesDialogVisible = false;
        
        // 等待足够长的时间，确保弹窗完全关闭
        await Task.Delay(200);
        
        // 调用ViewModel的确认转移命令
        await ViewModel.ConfirmMoveResourcesCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// 转移资源到其他版本弹窗 - 取消按钮点击事件
    /// </summary>
    private void MoveResourcesDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 关闭弹窗
        ViewModel.IsMoveResourcesDialogVisible = false;
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
            if (VisualTreeHelper.GetParent(toggleSwitch) is FrameworkElement parentElement && parentElement.DataContext is Models.VersionManagement.ModInfo modInfo)
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
    
    #region 资源包预览
    
    // 存储当前预览的资源包和加载的纹理
    private ResourcePackInfo _currentPreviewPack;
    private Dictionary<string, CanvasBitmap> _previewBitmaps = new Dictionary<string, CanvasBitmap>();
    private Dictionary<ResourcePackInfo, CanvasControl> _canvasControls = new Dictionary<ResourcePackInfo, CanvasControl>(); // 存储每个资源包对应的 Canvas
    
    // 预定义的常用方块纹理列表（优先查找这些）
    private static readonly string[] PreferredBlockTextures = new[]
    {
        "grass_block_side.png",
        "dirt.png",
        "stone.png",
        "oak_log.png",
        "oak_planks.png",
        "cobblestone.png",
        "sand.png",
        "gravel.png",
        "oak_leaves.png",
        "glass.png",
        "bricks.png",
        "iron_block.png",
        "gold_block.png",
        "diamond_block.png",
        "emerald_block.png",
        "netherrack.png",
        "soul_sand.png",
        "glowstone.png",
        "obsidian.png",
        "bedrock.png",
        "water_still.png",
        "lava_still.png",
        "crafting_table_front.png",
        "furnace_front.png",
        "bookshelf.png",
        "tnt_side.png",
        "coal_ore.png",
        "iron_ore.png",
        "gold_ore.png",
        "diamond_ore.png",
        "redstone_ore.png",
        "lapis_ore.png"
    };
    
    /// <summary>
    /// 资源包列表项点击事件 - 显示预览
    /// </summary>
    private async void ResourcePackItem_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid && grid.DataContext is ResourcePackInfo resourcePack)
        {
            System.Diagnostics.Debug.WriteLine($"[预览] 点击资源包: {resourcePack.Name}");
            
            // 如果已经打开，则关闭
            if (resourcePack.IsPreviewOpen)
            {
                System.Diagnostics.Debug.WriteLine($"[预览] 关闭预览: {resourcePack.Name}");
                resourcePack.IsPreviewOpen = false;
                _currentPreviewPack = null;
                
                // 释放 CanvasBitmap 资源
                foreach (var bitmap in _previewBitmaps.Values)
                {
                    bitmap?.Dispose();
                }
                _previewBitmaps.Clear();
                System.Diagnostics.Debug.WriteLine($"[预览] 已释放 {_previewBitmaps.Count} 个 Bitmap");
                return;
            }
            
            // 关闭其他预览
            foreach (var pack in ViewModel.ResourcePacks)
            {
                if (pack != resourcePack)
                {
                    pack.IsPreviewOpen = false;
                }
            }
            
            // 释放旧的 CanvasBitmap 资源（重要！避免文件被占用）
            System.Diagnostics.Debug.WriteLine($"[预览] 释放旧的 Bitmap，当前数量: {_previewBitmaps.Count}");
            foreach (var bitmap in _previewBitmaps.Values)
            {
                bitmap?.Dispose();
            }
            _previewBitmaps.Clear();
            
            // 打开当前预览
            _currentPreviewPack = resourcePack;
            resourcePack.IsPreviewOpen = true;
            resourcePack.IsLoadingPreview = true;
            
            System.Diagnostics.Debug.WriteLine($"[预览] 开始加载纹理: {resourcePack.Name}, 已有纹理数: {resourcePack.PreviewTextures.Count}");
            
            // 异步加载纹理
            await LoadResourcePackPreviewTexturesAsync(resourcePack);
            
            System.Diagnostics.Debug.WriteLine($"[预览] 纹理加载完成: {resourcePack.Name}, 纹理数: {resourcePack.PreviewTextures.Count}");
            resourcePack.IsLoadingPreview = false;
        }
    }
    
    /// <summary>
    /// Canvas Loaded 事件 - 保存 Canvas 引用并设置当前预览包
    /// </summary>
    private async void PreviewCanvas_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is CanvasControl canvas && canvas.Tag is ResourcePackInfo resourcePack)
        {
            System.Diagnostics.Debug.WriteLine($"[预览] Canvas Loaded: {resourcePack.Name}");
            _canvasControls[resourcePack] = canvas;
            _currentPreviewPack = resourcePack;
            
            // 如果 Bitmap 已经被释放但纹理列表还在，需要重新加载
            if (_previewBitmaps.Count == 0 && resourcePack.PreviewTextures.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[预览] Canvas Loaded 检测到需要重新加载 Bitmap");
                await ReloadBitmapsAsync(canvas, resourcePack);
            }
        }
    }
    
    /// <summary>
    /// 重新加载 Bitmap（当 Canvas 已存在但 Bitmap 被释放时）
    /// </summary>
    private async Task ReloadBitmapsAsync(CanvasControl canvas, ResourcePackInfo resourcePack)
    {
        System.Diagnostics.Debug.WriteLine($"[预览] ReloadBitmaps 开始: {resourcePack.Name}, 纹理数: {resourcePack.PreviewTextures.Count}");
        
        _previewBitmaps.Clear();
        
        var texturePathsCopy = resourcePack.PreviewTextures.ToList();
        int loadedCount = 0;
        
        foreach (var texturePath in texturePathsCopy)
        {
            try
            {
                if (File.Exists(texturePath))
                {
                    var bitmap = await CanvasBitmap.LoadAsync(canvas, texturePath);
                    _previewBitmaps[texturePath] = bitmap;
                    loadedCount++;
                    System.Diagnostics.Debug.WriteLine($"[预览] 重新加载纹理成功: {Path.GetFileName(texturePath)}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[预览] 重新加载纹理失败: {ex.Message}");
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"[预览] ReloadBitmaps 完成: 成功加载 {loadedCount}/{texturePathsCopy.Count} 个纹理");
        canvas.Invalidate();
    }
    
    /// <summary>
    /// 从资源包中提取 4 个方块纹理（优先使用预定义列表）
    /// </summary>
    private async Task LoadResourcePackPreviewTexturesAsync(ResourcePackInfo resourcePack)
    {
        await Task.Run(() =>
        {
            try
            {
                resourcePack.PreviewTextures.Clear();
                
                if (!File.Exists(resourcePack.FilePath))
                    return;
                
                using (var zip = ZipFile.OpenRead(resourcePack.FilePath))
                {
                    var selectedTextures = new List<ZipArchiveEntry>();
                    
                    // 1. 优先从预定义列表中查找存在的纹理
                    foreach (var textureName in PreferredBlockTextures)
                    {
                        var entry = zip.GetEntry($"assets/minecraft/textures/block/{textureName}");
                        if (entry != null)
                        {
                            selectedTextures.Add(entry);
                            if (selectedTextures.Count >= 4)
                                break;
                        }
                    }
                    
                    // 2. 如果预定义列表不够 4 个，再随机选择其他纹理
                    if (selectedTextures.Count < 4)
                    {
                        var allBlockTextures = zip.Entries
                            .Where(e => e.FullName.StartsWith("assets/minecraft/textures/block/", StringComparison.OrdinalIgnoreCase) &&
                                       e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                                       !e.FullName.Contains("_n.png") && // 排除法线贴图
                                       !e.FullName.Contains("_s.png") && // 排除高光贴图
                                       !e.FullName.Contains("_e.png") && // 排除自发光贴图
                                       !selectedTextures.Contains(e))
                            .ToList();
                        
                        if (allBlockTextures.Count > 0)
                        {
                            var random = new Random();
                            var additionalTextures = allBlockTextures
                                .OrderBy(x => random.Next())
                                .Take(4 - selectedTextures.Count);
                            
                            selectedTextures.AddRange(additionalTextures);
                        }
                    }
                    
                    if (selectedTextures.Count == 0)
                        return;
                    
                    // 提取到临时目录（每个资源包使用独立的子目录）
                    string tempDir = Path.Combine(Path.GetTempPath(), "XianYuLauncher_Preview", Path.GetFileNameWithoutExtension(resourcePack.FileName));
                    Directory.CreateDirectory(tempDir);
                    
                    // 不删除旧文件，直接覆盖（避免文件被占用的问题）
                    // 临时目录会在系统重启时自动清理
                    
                    foreach (var texture in selectedTextures)
                    {
                        string tempPath = Path.Combine(tempDir, Path.GetFileName(texture.FullName));
                        texture.ExtractToFile(tempPath, true);
                        resourcePack.PreviewTextures.Add(tempPath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载资源包预览失败: {ex.Message}");
            }
        });
    }
    
    /// <summary>
    /// Win2D Canvas 创建资源事件
    /// </summary>
    private async void PreviewCanvas_CreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine($"[预览] CreateResources 被调用");
        
        // 从 Canvas 的 Tag 获取当前资源包
        if (sender.Tag is not ResourcePackInfo resourcePack || resourcePack.PreviewTextures.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine($"[预览] CreateResources 退出: Tag={sender.Tag?.GetType().Name}, PreviewTextures={((sender.Tag as ResourcePackInfo)?.PreviewTextures.Count ?? 0)}");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[预览] CreateResources 开始加载: {resourcePack.Name}, 纹理数: {resourcePack.PreviewTextures.Count}");
        
        // 设置为当前预览包
        _currentPreviewPack = resourcePack;
        
        // 加载纹理到 CanvasBitmap
        _previewBitmaps.Clear();
        
        // 创建列表副本以避免并发修改异常
        var texturePathsCopy = resourcePack.PreviewTextures.ToList();
        
        int loadedCount = 0;
        foreach (var texturePath in texturePathsCopy)
        {
            try
            {
                if (File.Exists(texturePath))
                {
                    var bitmap = await CanvasBitmap.LoadAsync(sender, texturePath);
                    _previewBitmaps[texturePath] = bitmap;
                    loadedCount++;
                    System.Diagnostics.Debug.WriteLine($"[预览] 加载纹理成功: {Path.GetFileName(texturePath)}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[预览] 纹理文件不存在: {texturePath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[预览] 加载纹理失败: {ex.Message}");
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"[预览] CreateResources 完成: {resourcePack.Name}, 成功加载 {loadedCount}/{texturePathsCopy.Count} 个纹理");
        sender.Invalidate();
    }
    
    /// <summary>
    /// Win2D Canvas 绘制事件 - 绘制 2x2 网格
    /// </summary>
    private void PreviewCanvas_Draw(CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine($"[预览] Draw 被调用, Bitmap数量: {_previewBitmaps.Count}");
        
        if (_previewBitmaps.Count == 0)
            return;
        
        var session = args.DrawingSession;
        
        // 设置最近邻插值，保持像素艺术风格（Minecraft 纹理都是低分辨率像素）
        session.Antialiasing = Microsoft.Graphics.Canvas.CanvasAntialiasing.Aliased;
        
        // 画布尺寸 340x340 (正方形)
        float canvasWidth = 340;
        float canvasHeight = 340;
        
        // 绘制透明背景（完全透明，让 TeachingTip 的背景透出来）
        session.Clear(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        
        // 全局向左偏移 23 像素，补偿 TeachingTip 的右偏移
        float globalOffsetX = -23;
        
        // 内边距
        float padding = 20;
        float drawableWidth = canvasWidth - padding * 2;   // 300
        float drawableHeight = canvasHeight - padding * 2; // 300
        
        // 2行2列网格
        int rows = 2;
        int cols = 2;
        float cellWidth = drawableWidth / cols;   // 150
        float cellHeight = drawableHeight / rows; // 150
        
        // 绘制纹理
        int index = 0;
        foreach (var kvp in _previewBitmaps)
        {
            if (index >= 4) break;
            
            int row = index / cols;
            int col = index % cols;
            
            float x = padding + col * cellWidth;
            float y = padding + row * cellHeight;
            
            // 绘制纹理（保持纵横比，居中）
            var bitmap = kvp.Value;
            float scale = (float)Math.Min(cellWidth / bitmap.Size.Width, cellHeight / bitmap.Size.Height);
            float drawWidth = (float)(bitmap.Size.Width * scale);
            float drawHeight = (float)(bitmap.Size.Height * scale);
            
            // 确保坐标对齐到整数像素，避免亚像素渲染，并应用全局偏移
            float drawX = (float)Math.Round(x + (cellWidth - drawWidth) / 2 + globalOffsetX);
            float drawY = (float)Math.Round(y + (cellHeight - drawHeight) / 2);
            drawWidth = (float)Math.Round(drawWidth);
            drawHeight = (float)Math.Round(drawHeight);
            
            // 使用最近邻插值绘制图像
            session.DrawImage(bitmap, new Rect(drawX, drawY, drawWidth, drawHeight), 
                bitmap.Bounds, 1.0f, Microsoft.Graphics.Canvas.CanvasImageInterpolation.NearestNeighbor);
            
            index++;
        }
        
        System.Diagnostics.Debug.WriteLine($"[预览] Draw 完成, 绘制了 {index} 个纹理");
    }
    
    #endregion
    
    #region 地图管理相关事件
    
    /// <summary>
    /// 地图列表项点击事件
    /// </summary>
    private void MapListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MapInfo map)
        {
            ViewModel.ShowMapDetailCommand.Execute(map);
        }
    }
    
    #endregion
}
