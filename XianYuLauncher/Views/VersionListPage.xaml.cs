using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Windows.Storage;
using System;
using System.IO;
using System.IO.Compression;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class VersionListPage : Page
{
    private readonly INavigationService _navigationService;
    private bool _isExportCancelled = false;
    private bool _isCompleteVersionDialogOpen = false; // 用于跟踪版本补全弹窗状态
    private bool _isRenameDialogOpen = false; // 用于跟踪重命名弹窗状态
    
    // 动态创建的弹窗引用
    private ContentDialog? _loadingDialog;
    private ContentDialog? _completeVersionDialog;
    
    // 弹窗内的控件引用
    private ProgressRing? _loadingProgressRing;
    private TextBlock? _loadingStatusText;
    private ProgressBar? _loadingProgressBar;
    private TextBlock? _loadingProgressText;
    
    private TextBlock? _completeVersionNameText;
    private TextBlock? _completeVersionStageText;
    private TextBlock? _completeVersionCurrentFileText;
    private ProgressBar? _completeVersionProgressBar;
    private TextBlock? _completeVersionProgressText;

    public VersionListPage()
    {
        this.DataContext = App.GetService<VersionListViewModel>();
        _navigationService = App.GetService<INavigationService>();
        InitializeComponent();
        
        // 添加ItemClick事件处理
        VersionsListView.ItemClick += VersionsListView_ItemClick;
        
        // 订阅导出整合包事件
        if (this.DataContext is VersionListViewModel viewModel)
        {
            viewModel.ExportModpackRequested += OnExportModpackRequested;
            // 订阅ResourceDirectories集合变化事件
            viewModel.ResourceDirectories.CollectionChanged += ResourceDirectories_CollectionChanged;
            // 订阅版本补全事件
            viewModel.CompleteVersionRequested += OnCompleteVersionRequested;
            viewModel.CompleteVersionProgressUpdated += OnCompleteVersionProgressUpdated;
            viewModel.CompleteVersionCompleted += OnCompleteVersionCompleted;
            // 监听属性变化以显示弹窗
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
        
    }
    
    /// <summary>
    /// ViewModel属性变化事件处理
    /// </summary>
    private async void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not VersionListViewModel viewModel)
            return;
        
        // 处理重命名弹窗显示/隐藏
        if (e.PropertyName == nameof(viewModel.IsRenameDialogVisible))
        {
            if (viewModel.IsRenameDialogVisible && !_isRenameDialogOpen)
            {
                try
                {
                    _isRenameDialogOpen = true;
                    
                    // 动态创建ContentDialog（参考官方示例）
                    var dialog = new ContentDialog
                    {
                        XamlRoot = this.XamlRoot,
                        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                        Title = "重命名版本",
                        PrimaryButtonText = "确定",
                        CloseButtonText = "取消",
                        DefaultButton = ContentDialogButton.Primary,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    
                    // 创建内容
                    var contentStack = new StackPanel { Spacing = 12, Width = 400 };
                    
                    // 说明文字
                    var instructionText = new TextBlock
                    {
                        Text = "请输入新的版本名称：",
                        FontSize = 14
                    };
                    contentStack.Children.Add(instructionText);
                    
                    // 输入框
                    var nameTextBox = new TextBox
                    {
                        PlaceholderText = "新版本名称",
                        Text = viewModel.NewVersionName,
                        MaxLength = 100
                    };
                    // 双向绑定
                    nameTextBox.TextChanged += (s, args) => viewModel.NewVersionName = nameTextBox.Text;
                    contentStack.Children.Add(nameTextBox);
                    
                    dialog.Content = contentStack;
                    
                    // 显示弹窗
                    var result = await dialog.ShowAsync();
                    
                    if (result == ContentDialogResult.Primary)
                    {
                        // 执行重命名
                        var (success, message) = await viewModel.ExecuteRenameVersionAsync();
                        
                        if (!success)
                        {
                            // 显示错误消息
                            var errorDialog = new ContentDialog
                            {
                                XamlRoot = this.XamlRoot,
                                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                                Title = "重命名失败",
                                Content = message,
                                CloseButtonText = "确定",
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            
                            await errorDialog.ShowAsync();
                        }
                    }
                    
                    _isRenameDialogOpen = false;
                    // 弹窗关闭后重置状态
                    viewModel.IsRenameDialogVisible = false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"显示重命名弹窗失败: {ex.Message}");
                    _isRenameDialogOpen = false;
                    viewModel.IsRenameDialogVisible = false;
                }
            }
        }
    }
    
    /// <summary>
    /// 处理版本补全请求事件，动态创建并打开版本补全弹窗
    /// </summary>
    private async void OnCompleteVersionRequested(object? sender, VersionListViewModel.VersionInfoItem e)
    {
        if (_isCompleteVersionDialogOpen) return;
        
        _isCompleteVersionDialogOpen = true;
        
        // 动态创建ContentDialog
        _completeVersionDialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = "版本补全",
            CloseButtonText = "关闭",
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            DefaultButton = ContentDialogButton.None
        };
        
        var mainStack = new StackPanel { Spacing = 16, Width = 400 };
        
        // 版本信息卡片
        var versionCard = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12)
        };
        
        var cardGrid = new Grid();
        cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        
        var versionIcon = new FontIcon
        {
            Glyph = "\uE74C",
            FontSize = 24,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        Grid.SetColumn(versionIcon, 0);
        
        _completeVersionNameText = new TextBlock
        {
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = e.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(_completeVersionNameText, 1);
        
        cardGrid.Children.Add(versionIcon);
        cardGrid.Children.Add(_completeVersionNameText);
        versionCard.Child = cardGrid;
        mainStack.Children.Add(versionCard);
        
        // 状态区域
        var statusStack = new StackPanel { Spacing = 8 };
        
        var stageGrid = new Grid();
        stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        
        var stageIcon = new FontIcon
        {
            Glyph = "\uE896",
            FontSize = 14,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 8, 0)
        };
        Grid.SetColumn(stageIcon, 0);
        
        _completeVersionStageText = new TextBlock
        {
            FontSize = 14,
            Text = "正在检查依赖...",
            TextWrapping = TextWrapping.WrapWholeWords
        };
        Grid.SetColumn(_completeVersionStageText, 1);
        
        stageGrid.Children.Add(stageIcon);
        stageGrid.Children.Add(_completeVersionStageText);
        statusStack.Children.Add(stageGrid);
        
        _completeVersionCurrentFileText = new TextBlock
        {
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            Text = "",
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(22, 0, 0, 0)
        };
        statusStack.Children.Add(_completeVersionCurrentFileText);
        mainStack.Children.Add(statusStack);
        
        // 进度区域
        var progressStack = new StackPanel { Spacing = 8 };
        
        _completeVersionProgressBar = new ProgressBar
        {
            Value = 0,
            Minimum = 0,
            Maximum = 100,
            Height = 6,
            CornerRadius = new CornerRadius(3)
        };
        progressStack.Children.Add(_completeVersionProgressBar);
        
        _completeVersionProgressText = new TextBlock
        {
            FontSize = 13,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Text = "0%",
            HorizontalTextAlignment = TextAlignment.Center
        };
        progressStack.Children.Add(_completeVersionProgressText);
        mainStack.Children.Add(progressStack);
        
        _completeVersionDialog.Content = mainStack;
        
        // 显示弹窗（非阻塞）
        _ = _completeVersionDialog.ShowAsync();
    }
    
    /// <summary>
    /// 处理版本补全进度更新事件
    /// </summary>
    private void OnCompleteVersionProgressUpdated(object? sender, (double Progress, string Stage, string CurrentFile) e)
    {
        if (!_isCompleteVersionDialogOpen) return;
        
        // 在 UI 线程更新
        DispatcherQueue.TryEnqueue(() =>
        {
            if (e.Progress >= 0 && _completeVersionProgressBar != null && _completeVersionProgressText != null)
            {
                _completeVersionProgressBar.Value = e.Progress;
                _completeVersionProgressText.Text = $"{e.Progress:F1}%";
            }
            
            if (!string.IsNullOrEmpty(e.Stage) && _completeVersionStageText != null)
            {
                _completeVersionStageText.Text = e.Stage;
            }
            
            if (!string.IsNullOrEmpty(e.CurrentFile) && _completeVersionCurrentFileText != null)
            {
                // 只显示文件名的前 8 位（hash）
                string displayFile = e.CurrentFile.Length > 8 ? e.CurrentFile.Substring(0, 8) + "..." : e.CurrentFile;
                _completeVersionCurrentFileText.Text = $"当前: {displayFile}";
            }
        });
    }
    
    /// <summary>
    /// 处理版本补全完成事件
    /// </summary>
    private void OnCompleteVersionCompleted(object? sender, (bool Success, string Message) e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (e.Success)
            {
                if (_completeVersionStageText != null)
                    _completeVersionStageText.Text = "补全完成！";
                if (_completeVersionProgressBar != null)
                    _completeVersionProgressBar.Value = 100;
                if (_completeVersionProgressText != null)
                    _completeVersionProgressText.Text = "100%";
                if (_completeVersionCurrentFileText != null)
                    _completeVersionCurrentFileText.Text = "";
            }
            else
            {
                if (_completeVersionStageText != null)
                    _completeVersionStageText.Text = e.Message;
            }
            
            _isCompleteVersionDialogOpen = false;
        });
    }
    
    /// <summary>
    /// 处理导出整合包请求事件，动态创建并显示导出整合包弹窗
    /// </summary>
    private async void OnExportModpackRequested(object? sender, VersionListViewModel.VersionInfoItem e)
    {
        if (DataContext is not VersionListViewModel viewModel)
            return;
        
        // 设置整合包名称和版本的默认值
        viewModel.ModpackName = e.Name;
        viewModel.ModpackVersion = "1.0.0";
        
        // 动态创建ContentDialog
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = "导出整合包",
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        // 创建ScrollViewer包裹内容
        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible
        };
        
        var mainStack = new StackPanel { Spacing = 0 };
        
        // 说明文字
        var instructionText = new TextBlock
        {
            Text = "请选择要导出的数据：",
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        mainStack.Children.Add(instructionText);
        
        // 导出选项区域
        var optionsStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 16)
        };
        
        // 资源目录区域（只在有资源时显示）
        if (viewModel.ResourceDirectories.Count > 0)
        {
            var resourceStack = new StackPanel();
            
            // 资源目录总复选框和展开/折叠按钮
            var headerGrid = new Grid
            {
                Margin = new Thickness(0, 4, 0, 2),
                Padding = new Thickness(4)
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // 展开/折叠按钮
            var toggleButton = new Button
            {
                Content = "▶",
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(toggleButton, 0);
            
            // 资源目录总复选框
            var resourceAllCheckBox = new CheckBox
            {
                Content = "版本目录资源",
                IsThreeState = true,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(resourceAllCheckBox, 1);
            
            // 资源目录TreeView
            var treeView = new ItemsControl
            {
                Margin = new Thickness(0, 0, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Visibility = Visibility.Collapsed
            };
            
            // 绑定数据源
            treeView.ItemsSource = viewModel.ResourceDirectories;
            treeView.ItemTemplate = (DataTemplate)this.Resources["ResourceItemTemplate"];
            
            // 展开/折叠按钮事件
            bool isExpanded = false;
            toggleButton.Click += (s, args) =>
            {
                isExpanded = !isExpanded;
                treeView.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
                toggleButton.Content = isExpanded ? "▼" : "▶";
            };
            
            // 总复选框事件
            resourceAllCheckBox.Checked += (s, args) =>
            {
                foreach (var dir in viewModel.ResourceDirectories)
                    dir.IsSelected = true;
            };
            
            resourceAllCheckBox.Unchecked += (s, args) =>
            {
                foreach (var dir in viewModel.ResourceDirectories)
                    dir.IsSelected = false;
            };
            
            // 监听子项变化更新总复选框状态
            void UpdateResourceAllState()
            {
                if (viewModel.ResourceDirectories.Count == 0)
                {
                    resourceAllCheckBox.IsChecked = false;
                    return;
                }
                
                bool allSelected = true;
                bool noneSelected = true;
                
                foreach (var item in viewModel.ResourceDirectories)
                {
                    if (item.IsSelected)
                        noneSelected = false;
                    else
                        allSelected = false;
                    
                    if (!allSelected && !noneSelected)
                        break;
                }
                
                if (noneSelected)
                    resourceAllCheckBox.IsChecked = false;
                else if (allSelected)
                    resourceAllCheckBox.IsChecked = true;
                else
                    resourceAllCheckBox.IsChecked = null;
            }
            
            // 订阅资源项变化
            foreach (var item in viewModel.ResourceDirectories)
            {
                item.SelectedChanged += (s, args) => UpdateResourceAllState();
            }
            
            headerGrid.Children.Add(toggleButton);
            headerGrid.Children.Add(resourceAllCheckBox);
            
            resourceStack.Children.Add(headerGrid);
            resourceStack.Children.Add(treeView);
            
            optionsStack.Children.Add(resourceStack);
        }
        
        mainStack.Children.Add(optionsStack);
        
        // 整合包信息输入区域
        var inputStack = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        
        // 整合包名称
        var nameStack = new StackPanel { Spacing = 4 };
        nameStack.Children.Add(new TextBlock { Text = "整合包名称", FontSize = 14 });
        var nameTextBox = new TextBox
        {
            PlaceholderText = "请输入整合包名称",
            Text = viewModel.ModpackName,
            Width = 400,
            MaxWidth = 400
        };
        nameTextBox.TextChanged += (s, args) => viewModel.ModpackName = nameTextBox.Text;
        nameStack.Children.Add(nameTextBox);
        inputStack.Children.Add(nameStack);
        
        // 整合包版本
        var versionStack = new StackPanel { Spacing = 4 };
        versionStack.Children.Add(new TextBlock { Text = "整合包版本", FontSize = 14 });
        var versionTextBox = new TextBox
        {
            PlaceholderText = "请输入整合包版本",
            Text = viewModel.ModpackVersion,
            Width = 400,
            MaxWidth = 400
        };
        versionTextBox.TextChanged += (s, args) => viewModel.ModpackVersion = versionTextBox.Text;
        versionStack.Children.Add(versionTextBox);
        inputStack.Children.Add(versionStack);
        
        // 复选框选项
        var checkBoxStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 32,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        
        var offlineModeCheckBox = new CheckBox
        {
            Content = "非联网模式",
            IsChecked = viewModel.IsOfflineMode,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        
        var serverOnlyCheckBox = new CheckBox
        {
            Content = "导出服务端整合包",
            IsChecked = viewModel.IsServerOnly,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        
        // 离线模式警告InfoBar
        var warningInfoBar = new InfoBar
        {
            Title = "许可证警告",
            Message = "直接将 Mod 放入整合包可能会违反部分条例，请不要进行分发！",
            Severity = InfoBarSeverity.Warning,
            IsOpen = true,
            IsClosable = false,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 12, 0, 0)
        };
        
        // 复选框事件
        offlineModeCheckBox.Checked += (s, args) =>
        {
            viewModel.IsOfflineMode = true;
            warningInfoBar.Visibility = Visibility.Visible;
        };
        
        offlineModeCheckBox.Unchecked += (s, args) =>
        {
            viewModel.IsOfflineMode = false;
            warningInfoBar.Visibility = Visibility.Collapsed;
        };
        
        serverOnlyCheckBox.Checked += (s, args) => viewModel.IsServerOnly = true;
        serverOnlyCheckBox.Unchecked += (s, args) => viewModel.IsServerOnly = false;
        
        checkBoxStack.Children.Add(offlineModeCheckBox);
        checkBoxStack.Children.Add(serverOnlyCheckBox);
        inputStack.Children.Add(checkBoxStack);
        
        mainStack.Children.Add(inputStack);
        mainStack.Children.Add(warningInfoBar);
        
        scrollViewer.Content = mainStack;
        dialog.Content = scrollViewer;
        
        // 显示弹窗
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            // 调用原有的确认按钮逻辑
            await ExportModpackDialog_PrimaryButtonClick_Logic();
        }
    }
    
    /// <summary>
    /// 版本项点击事件处理，导航至版本管理页面
    /// </summary>
    private void VersionsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is VersionListViewModel.VersionInfoItem version)
        {
            // 导航至版本管理页面，传递选中的版本信息
            _navigationService.NavigateTo(typeof(VersionManagementViewModel).FullName!, version);
        }
    }

    /// <summary>
    /// 导出整合包确认按钮逻辑（从原事件处理中提取）
    /// </summary>
    private async Task ExportModpackDialog_PrimaryButtonClick_Logic()
    {
        if (DataContext is VersionListViewModel viewModel)
        {
            // 重置取消标志
            _isExportCancelled = false;
            
            // 获取选择的导出选项
            var selectedOptions = viewModel.GetSelectedExportOptions();
            
            // 输出到Debug窗口
            System.Diagnostics.Debug.WriteLine("=== 导出整合包选项 ===");
            System.Diagnostics.Debug.WriteLine($"版本: {viewModel.SelectedVersion?.Name}");
            System.Diagnostics.Debug.WriteLine($"选择的导出项数量: {selectedOptions.Count}");
            foreach (var option in selectedOptions)
            {
                System.Diagnostics.Debug.WriteLine($"- {option}");
            }
            System.Diagnostics.Debug.WriteLine("====================");
            
            // 导出弹窗会在ShowAsync返回后自动关闭,无需手动调用Hide()
            
            // 打开加载弹窗（非阻塞方式）
            ShowLoadingDialog();
            UpdateLoadingDialog("正在获取Modrinth资源...", 0.0);
            
            // 在后台线程执行导出逻辑
            _ = Task.Run(async () =>
            {
                try
                {
                    Dictionary<string, Core.Models.ModrinthVersion> fileResults = new Dictionary<string, Core.Models.ModrinthVersion>();
                    
                    // 检查是否为非联网模式和仅导出服务端模式
                    bool isOfflineMode = viewModel.IsOfflineMode;
                    bool isServerOnly = viewModel.IsServerOnly;
                    
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"导出模式: {(isOfflineMode ? "非联网模式" : "联网模式")}{(isServerOnly ? " + 仅导出服务端" : "")}");
                    });
                    
                    // 仅导出服务端模式下，过滤客户端特定的文件和目录
                    List<string> filteredOptions = new List<string>(selectedOptions);
                    if (isServerOnly)
                    {
                        // 客户端特定的文件和目录列表
                        var clientOnlyItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "resourcepacks",
                            "shaderpacks",
                            "options.txt",
                            "screenshots",
                            "journeymap"
                        };
                        
                        // 过滤客户端特定的文件和目录
                        filteredOptions = filteredOptions.Where(option =>
                        {
                            // 检查是否为客户端特定目录或文件
                            string itemName = Path.GetFileName(option);
                            return !clientOnlyItems.Contains(itemName) && !option.StartsWith("screenshots", StringComparison.OrdinalIgnoreCase) && !option.StartsWith("journeymap", StringComparison.OrdinalIgnoreCase);
                        }).ToList();
                        
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            System.Diagnostics.Debug.WriteLine($"仅导出服务端模式，过滤前选项数量: {selectedOptions.Count}，过滤后选项数量: {filteredOptions.Count}");
                            System.Diagnostics.Debug.WriteLine("过滤掉的客户端特定项:");
                            foreach (string option in selectedOptions.Except(filteredOptions))
                            {
                                System.Diagnostics.Debug.WriteLine($"- {option}");
                            }
                        });
                    }
                    
                    // 搜索Modrinth获取文件信息
                    // 当开启导出服务端时，即使是非联网模式也需要进行服务端兼容性检查
                    bool shouldCheckModrinth = (!isOfflineMode || isServerOnly);
                    if (shouldCheckModrinth && viewModel.SelectedVersion != null && !_isExportCancelled)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            System.Diagnostics.Debug.WriteLine("开始搜索Modrinth获取文件信息...");
                            UpdateLoadingDialog("正在获取Modrinth资源...", 10.0);
                        });
                        
                        fileResults = await viewModel.SearchModrinthForFilesAsync(viewModel.SelectedVersion, filteredOptions);
                        
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            System.Diagnostics.Debug.WriteLine($"Modrinth搜索完成，找到 {fileResults.Count} 个匹配结果");
                        });
                        
                        // 仅导出服务端模式下，过滤服务端不支持的文件
                        if (isServerOnly)
                        {
                            // 获取Modrinth服务实例
                            var modrinthService = App.GetService<Core.Services.ModrinthService>();
                            var filesToRemove = new List<string>();
                            var serverUnsupportedFiles = new HashSet<string>(); // 服务端不支持的文件列表
                            
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                System.Diagnostics.Debug.WriteLine("开始过滤服务端不支持的文件...");
                                UpdateLoadingDialog("正在过滤服务端不支持的文件...", 30.0);
                            });
                            
                            // 遍历所有匹配结果，检查服务端支持情况
                            foreach (var kvp in fileResults)
                            {
                                if (_isExportCancelled) break;
                                
                                string filePath = kvp.Key;
                                var modrinthVersion = kvp.Value;
                                
                                // 检查是否有project_id
                                if (!string.IsNullOrEmpty(modrinthVersion.ProjectId))
                                {
                                    try
                                    {
                                        // 获取项目详情
                                        var projectDetail = await modrinthService.GetProjectDetailAsync(modrinthVersion.ProjectId);
                                        
                                        // 检查server_side字段
                                        if (projectDetail != null)
                                        {
                                            string serverSide = projectDetail.ServerSide?.ToLowerInvariant() ?? "unknown";
                                            
                                            DispatcherQueue.TryEnqueue(() =>
                                            {
                                                System.Diagnostics.Debug.WriteLine($"文件: {filePath}");
                                                System.Diagnostics.Debug.WriteLine($"  ProjectId: {modrinthVersion.ProjectId}");
                                                System.Diagnostics.Debug.WriteLine($"  服务端支持: {serverSide}");
                                            });
                                            
                                            // 如果服务端支持为unsupported，则标记为需要移除
                                            if (serverSide == "unsupported")
                                            {
                                                filesToRemove.Add(filePath);
                                                serverUnsupportedFiles.Add(filePath); // 添加到服务端不支持的文件列表
                                                DispatcherQueue.TryEnqueue(() =>
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"  标记为移除：服务端不支持");
                                                });
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DispatcherQueue.TryEnqueue(() =>
                                        {
                                            System.Diagnostics.Debug.WriteLine($"获取项目详情失败: {modrinthVersion.ProjectId}, 错误: {ex.Message}");
                                        });
                                    }
                                }
                            }
                            
                            // 移除服务端不支持的文件
                            if (filesToRemove.Count > 0)
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine($"移除 {filesToRemove.Count} 个服务端不支持的文件");
                                    foreach (string filePath in filesToRemove)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"- {filePath}");
                                    }
                                });
                                
                                foreach (string filePath in filesToRemove)
                                {
                                    fileResults.Remove(filePath);
                                }
                                
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine($"过滤后剩余 {fileResults.Count} 个文件");
                                });
                            }
                            
                            // 仅导出服务端模式下，从过滤选项中移除服务端不支持的文件
                            filteredOptions = filteredOptions.Where(option => !serverUnsupportedFiles.Contains(option)).ToList();
                            
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                System.Diagnostics.Debug.WriteLine($"过滤后剩余的导出选项数量: {filteredOptions.Count}");
                            });
                        }
                    }
                    
                    // 非联网模式且不导出服务端时，直接跳转到准备保存整合包
                    if (isOfflineMode && !isServerOnly)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            System.Diagnostics.Debug.WriteLine("非联网模式且不导出服务端，跳过Modrinth搜索");
                            UpdateLoadingDialog("准备保存整合包...", 20.0);
                        });
                    }
                    
                    if (_isExportCancelled)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            System.Diagnostics.Debug.WriteLine("导出已取消");
                            HideLoadingDialog();
                        });
                        return;
                    }
                    
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateLoadingDialog("准备保存整合包...", 20.0);
                    });
                    
                    // 打开文件保存对话框需要在UI线程执行
                    StorageFile file = null;
                    var filePickerTask = new TaskCompletionSource<StorageFile>();
                    
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        try
                        {
                            // 打开文件保存对话框
                            var savePicker = new FileSavePicker();
                            
                            // 设置文件选择器的文件类型
                            savePicker.FileTypeChoices.Add("Modrinth Pack", new List<string> { ".mrpack" });
                            
                            // 设置默认文件名
                            string defaultFileName = string.IsNullOrEmpty(viewModel.ModpackName) ? "Untitled" : viewModel.ModpackName;
                            savePicker.SuggestedFileName = defaultFileName;
                            
                            // 设置默认位置
                            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                            
                            // 获取当前窗口的HWND
                            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
                            
                            // 显示文件保存对话框
                            var pickedFile = await savePicker.PickSaveFileAsync();
                            filePickerTask.SetResult(pickedFile);
                        }
                        catch (Exception ex)
                        {
                            filePickerTask.SetException(ex);
                        }
                    });
                    
                    file = await filePickerTask.Task;
                    
                    if (file != null && !_isExportCancelled)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            UpdateLoadingDialog("正在创建整合包...", 30.0);
                        });
                        
                        // 创建临时目录用于构建整合包
                        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                        Directory.CreateDirectory(tempDir);
                        
                        try
                        {
                            // 创建overrides目录
                            string overridesDir = Path.Combine(tempDir, "overrides");
                            Directory.CreateDirectory(overridesDir);
                            
                            // 使用统一的版本信息服务获取版本配置
                            var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
                            string versionPath = viewModel.SelectedVersion?.Path ?? "";
                            string versionName = viewModel.SelectedVersion?.Name ?? "";
                            
                            // 获取完整的版本信息
                            Core.Models.VersionConfig versionConfig = await versionInfoService.GetFullVersionInfoAsync(versionName, versionPath);
                            
                            // 提取加载器和Minecraft版本信息
                            string loaderName = "";
                            string loaderVersion = "";
                            string minecraftVersion = versionConfig?.MinecraftVersion ?? "";
                            
                            // 根据加载器类型设置正确的加载器名称
                            if (!string.IsNullOrEmpty(versionConfig?.ModLoaderType))
                            {
                                switch (versionConfig.ModLoaderType.ToLowerInvariant())
                                {
                                    case "fabric":
                                        loaderName = "fabric-loader";
                                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                                        break;
                                    case "legacyfabric":
                                        loaderName = "LegacyFabric";
                                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                                        break;
                                    case "forge":
                                        loaderName = "forge";
                                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                                        break;
                                    case "neoforge":
                                        loaderName = "neoforge";
                                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                                        break;
                                    case "quilt":
                                        loaderName = "quilt-loader";
                                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                                        break;
                                    default:
                                        loaderName = versionConfig.ModLoaderType;
                                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                                        break;
                                }
                            }
                            
                            // 构建modrinth.index.json内容
                            var indexJson = new
                            {
                                game = "minecraft",
                                formatVersion = 1,
                                versionId = viewModel.ModpackVersion, // 整合包版本
                                name = viewModel.ModpackName, // 整合包名称
                                summary = "",
                                files = new List<object>(),
                                dependencies = new Dictionary<string, string>
                                {
                                    { "minecraft", minecraftVersion ?? viewModel.SelectedVersion?.VersionNumber ?? "" },
                                    // 添加加载器依赖
                                    { loaderName, loaderVersion }
                                }
                            };
                            
                            // 移除无效的加载器依赖（如果加载器名称或版本为空）
                            if (string.IsNullOrEmpty(loaderName) || string.IsNullOrEmpty(loaderVersion))
                            {
                                ((Dictionary<string, string>)indexJson.dependencies).Remove(loaderName);
                            }
                            
                            // 构建files列表（仅在非联网模式下保持为空）
                            if (!isOfflineMode)
                            {
                                var filesList = (List<object>)indexJson.files;
                                foreach (var kvp in fileResults)
                                {
                                    string filePath = kvp.Key;
                                    var modrinthVersion = kvp.Value;
                                    
                                    if (modrinthVersion?.Files != null && modrinthVersion.Files.Count > 0)
                                    {
                                        var primaryFile = modrinthVersion.Files.FirstOrDefault(f => f.Primary) ?? modrinthVersion.Files[0];
                                        
                                        if (primaryFile.Hashes != null && primaryFile.Url != null)
                                        {
                                            var fileEntry = new
                                            {
                                                path = filePath.Replace('\\', '/'), // 使用正斜杠
                                                hashes = primaryFile.Hashes,
                                                downloads = new List<string> { primaryFile.Url.ToString() },
                                                fileSize = primaryFile.Size
                                            };
                                            filesList.Add(fileEntry);
                                        }
                                    }
                                }
                            }
                            
                            // 创建modrinth.index.json文件
                            string indexJsonPath = Path.Combine(tempDir, "modrinth.index.json");
                            string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(indexJson, Newtonsoft.Json.Formatting.Indented);
                            File.WriteAllText(indexJsonPath, jsonContent);
                            
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                UpdateLoadingDialog("正在处理文件...", 40.0);
                            });
                            
                            // 处理选择的导出选项
                            HashSet<string> processedDirectories = new HashSet<string>();
                            
                            // 在仅导出服务端模式下，我们已经在前面的逻辑中获取了服务端不支持的文件列表
                            // 遍历fileResults，找出所有被标记为需要移除的文件
                            var serverUnsupportedFiles = new HashSet<string>();
                            foreach (var kvp in fileResults)
                            {
                                string filePath = kvp.Key;
                                var modrinthVersion = kvp.Value;
                                
                                // 检查是否有project_id
                                if (!string.IsNullOrEmpty(modrinthVersion.ProjectId))
                                {
                                    try
                                    {
                                        // 获取Modrinth服务实例
                                        var modrinthService = App.GetService<Core.Services.ModrinthService>();
                                        var projectDetail = await modrinthService.GetProjectDetailAsync(modrinthVersion.ProjectId);
                                        
                                        // 检查server_side字段
                                        if (projectDetail != null)
                                        {
                                            string serverSide = projectDetail.ServerSide?.ToLowerInvariant() ?? "unknown";
                                            
                                            // 如果服务端支持为unsupported，则添加到不支持列表
                                            if (serverSide == "unsupported")
                                            {
                                                serverUnsupportedFiles.Add(filePath);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DispatcherQueue.TryEnqueue(() =>
                                        {
                                            System.Diagnostics.Debug.WriteLine($"获取项目详情失败: {modrinthVersion.ProjectId}, 错误: {ex.Message}");
                                        });
                                    }
                                }
                            }
                            
                            // 创建一个包含所有需要导出的文件的列表
                            var filesToExport = new List<string>();
                            foreach (string option in filteredOptions)
                            {
                                if (_isExportCancelled)
                                {
                                    break;
                                }
                                
                                // 检查是否为服务端不支持的文件，如果是则跳过
                                if (isServerOnly && serverUnsupportedFiles.Contains(option))
                                {
                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        System.Diagnostics.Debug.WriteLine($"跳过服务端不支持的文件: {option}");
                                    });
                                    continue;
                                }
                                
                                filesToExport.Add(option);
                            }
                            
                            // 现在复制需要导出的文件到overrides目录
                            foreach (string option in filesToExport)
                            {
                                if (_isExportCancelled)
                                {
                                    break;
                                }
                                
                                string fullPath = Path.Combine(viewModel.SelectedVersion!.Path, option);
                                
                                if (Directory.Exists(fullPath))
                                {
                                    // 如果是目录，确保在overrides中创建空目录
                                    string overrideDir = Path.Combine(overridesDir, option);
                                    Directory.CreateDirectory(overrideDir);
                                    processedDirectories.Add(option);
                                }
                                else if (File.Exists(fullPath))
                                {
                                    // 检查是否在Modrinth中找到（仅在非联网模式下跳过）
                                    bool isModrinthFile = !isOfflineMode && fileResults.ContainsKey(option);
                                    
                                    if (!isModrinthFile)
                                    {
                                        // 如果是非联网模式，或者没有在Modrinth中找到，复制到overrides目录
                                        string destPath = Path.Combine(overridesDir, option);
                                        string destDir = Path.GetDirectoryName(destPath)!;
                                        Directory.CreateDirectory(destDir);
                                        File.Copy(fullPath, destPath, true);
                                    }
                                    
                                    // 确保父目录在overrides中存在
                                    string parentDir = Path.GetDirectoryName(option)!;
                                    if (!string.IsNullOrEmpty(parentDir))
                                    {
                                        processedDirectories.Add(parentDir);
                                    }
                                }
                            }
                            
                            if (_isExportCancelled)
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine("导出已取消");
                                    HideLoadingDialog();
                                });
                                return;
                            }
                            
                            // 创建所有需要的空目录
                            foreach (string dir in processedDirectories)
                            {
                                if (_isExportCancelled)
                                {
                                    break;
                                }
                                
                                string overrideDir = Path.Combine(overridesDir, dir);
                                Directory.CreateDirectory(overrideDir);
                            }
                            
                            if (_isExportCancelled)
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine("导出已取消");
                                    HideLoadingDialog();
                                });
                                return;
                            }
                            
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                UpdateLoadingDialog("正在压缩整合包...", 70.0);
                            });
                            
                            // 创建zip压缩包，使用FileMode.Create覆盖已存在的文件
                            using (var fileStream = new FileStream(file.Path, FileMode.Create))
                            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
                            {
                                if (_isExportCancelled)
                                {
                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        HideLoadingDialog();
                                    });
                                    return;
                                }
                                
                                // 添加modrinth.index.json文件
                                archive.CreateEntryFromFile(indexJsonPath, "modrinth.index.json");
                                
                                // 添加overrides目录及其内容
                                AddDirectoryToZip(archive, overridesDir, "overrides");
                            }
                            
                            if (!_isExportCancelled)
                            {
                                DispatcherQueue.TryEnqueue(async () =>
                                {
                                    UpdateLoadingDialog("导出完成！", 100.0);
                                    System.Diagnostics.Debug.WriteLine($"整合包导出成功：{file.Path}");
                                    
                                    // 延迟关闭加载弹窗，让用户看到完成状态
                                    await Task.Delay(1000);
                                    HideLoadingDialog();
                                });
                            }
                            else
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine("导出已取消");
                                    // 如果已取消，删除不完整的文件
                                    if (File.Exists(file.Path))
                                    {
                                        File.Delete(file.Path);
                                    }
                                    HideLoadingDialog();
                                });
                            }
                        }
                        finally
                        {
                            // 清理临时目录
                            if (Directory.Exists(tempDir))
                            {
                                Directory.Delete(tempDir, true);
                            }
                        }
                    }
                    else
                    {
                        // 用户取消了文件保存对话框
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            HideLoadingDialog();
                        });
                    }
                }
                catch (Exception ex)
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine($"导出整合包失败：{ex.Message}");
                        UpdateLoadingDialog($"导出失败：{ex.Message}", 0.0);
                        await Task.Delay(2000);
                        HideLoadingDialog();
                    });
                }
            });
        }
    }
    
    /// <summary>
    /// 动态创建并显示加载弹窗
    /// </summary>
    private void ShowLoadingDialog()
    {
        if (_loadingDialog != null) return;
        
        _loadingDialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = "正在导出整合包",
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = true,
            SecondaryButtonText = "取消"
        };
        
        _loadingDialog.SecondaryButtonClick += (s, args) =>
        {
            _isExportCancelled = true;
            UpdateLoadingDialog("正在取消导出...", 0.0);
            if (_loadingProgressRing != null)
                _loadingProgressRing.IsActive = false;
        };
        
        var mainStack = new StackPanel
        {
            Width = double.NaN,
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        _loadingProgressRing = new ProgressRing
        {
            IsActive = true,
            Width = 64,
            Height = 64,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"],
            HorizontalAlignment = HorizontalAlignment.Center
        };
        mainStack.Children.Add(_loadingProgressRing);
        
        _loadingStatusText = new TextBlock
        {
            Text = "正在获取Modrinth资源...",
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        mainStack.Children.Add(_loadingStatusText);
        
        _loadingProgressBar = new ProgressBar
        {
            Value = 0,
            Maximum = 100,
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        mainStack.Children.Add(_loadingProgressBar);
        
        _loadingProgressText = new TextBlock
        {
            Text = "0.0%",
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        mainStack.Children.Add(_loadingProgressText);
        
        _loadingDialog.Content = mainStack;
        
        _ = _loadingDialog.ShowAsync();
    }
    
    /// <summary>
    /// 隐藏加载弹窗
    /// </summary>
    private void HideLoadingDialog()
    {
        _loadingDialog?.Hide();
        _loadingDialog = null;
        _loadingProgressRing = null;
        _loadingStatusText = null;
        _loadingProgressBar = null;
        _loadingProgressText = null;
    }
    
    /// <summary>
    /// 更新加载弹窗的状态和进度
    /// </summary>
    private void UpdateLoadingDialog(string status, double progress)
    {
        if (_loadingStatusText != null)
            _loadingStatusText.Text = status;
        if (_loadingProgressBar != null)
            _loadingProgressBar.Value = progress;
        if (_loadingProgressText != null)
            _loadingProgressText.Text = $"{progress:0.0}%";
    }
    
    /// <summary>
    /// 将目录及其内容添加到zip归档
    /// </summary>
    private void AddDirectoryToZip(ZipArchive archive, string sourceDir, string entryName)
    {
        // 如果已取消，直接返回
        if (_isExportCancelled)
        {
            return;
        }
        
        // 获取目录中的所有文件
        string[] files = Directory.GetFiles(sourceDir);
        
        foreach (string file in files)
        {
            if (_isExportCancelled)
            {
                return;
            }
            
            string relativePath = Path.GetRelativePath(sourceDir, file);
            string zipEntryName = Path.Combine(entryName, relativePath);
            archive.CreateEntryFromFile(file, zipEntryName);
        }
        
        // 获取目录中的所有子目录
        string[] subDirs = Directory.GetDirectories(sourceDir);
        
        foreach (string subDir in subDirs)
        {
            if (_isExportCancelled)
            {
                return;
            }
            
            string relativePath = Path.GetRelativePath(sourceDir, subDir);
            string zipEntryName = Path.Combine(entryName, relativePath);
            AddDirectoryToZip(archive, subDir, zipEntryName);
        }
    }

    /// <summary>
    #region 资源目录复选框事件处理
    /// <summary>
    /// 资源目录子复选框选中事件处理
    /// </summary>
    private void ResourceItem_Checked(object sender, RoutedEventArgs e)
    {
        UpdateResourceAllState();
    }
    
    /// <summary>
    /// 资源目录子复选框取消选中事件处理
    /// </summary>
    private void ResourceItem_Unchecked(object sender, RoutedEventArgs e)
    {
        UpdateResourceAllState();
    }
    
    /// <summary>
    /// 展开/折叠按钮点击事件处理
    /// </summary>
    private void ItemExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is VersionListViewModel.ResourceItem item)
        {
            // 切换项的展开状态
            item.IsExpanded = !item.IsExpanded;
        }
    }
    
    /// <summary>
    /// 更新资源目录总复选框的状态
    /// </summary>
    private void UpdateResourceAllState()
    {
        // 此方法已不再需要,因为导出弹窗已改为动态创建
        // 保留方法定义以避免其他地方的调用出错
    }
    
    /// <summary>
    /// 资源目录列表变化事件处理，根据资源目录数量设置StackPanel的可见性
    /// </summary>
    private void ResourceDirectories_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // 资源目录变化处理已移至动态创建的弹窗中
    }
    
    #endregion
}