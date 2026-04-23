using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Features.VersionList.ViewModels;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Features.VersionList.Views;

public sealed partial class VersionListRootPage : Page
{
    private readonly ICommonDialogService _dialogService;
    private readonly IProgressDialogService _progressDialogService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly Dictionary<string, BitmapImage?> _versionIconImageCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<BitmapImage?>> _versionIconProcessingTasks = new(StringComparer.OrdinalIgnoreCase);
    private int _iconWarmupRequestId;
    private bool _isExportCancelled = false;
    private bool _isCompleteVersionDialogOpen = false;
    private bool _isRenameDialogOpen = false;
    private readonly DialogProgressState _loadingDialogState = new();
    private TaskCompletionSource<bool>? _loadingDialogCloseSignal;

    private readonly DialogProgressState _completeVersionDialogState = new();
    private VersionListViewModel? _attachedViewModel;

    private sealed class DialogProgressState : System.ComponentModel.INotifyPropertyChanged
    {
        private string _status = string.Empty;
        private double _progress;
        private string _progressText = "0.0%";

        public string Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public double Progress
        {
            get => _progress;
            private set
            {
                if (Math.Abs(_progress - value) > 0.001)
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }

        public string ProgressText
        {
            get => _progressText;
            private set
            {
                if (_progressText != value)
                {
                    _progressText = value;
                    OnPropertyChanged(nameof(ProgressText));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public void Set(string status, double progress, string progressText)
        {
            Status = status;
            Progress = progress;
            ProgressText = progressText;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }

    public VersionListViewModel ViewModel { get; private set; } = null!;

    public bool IsLocalNavigationTargetElementEnabled => EntranceNavigationTransitionInfo.GetIsTargetElement(ContentArea);

    public event EventHandler<VersionListViewModel.VersionInfoItem>? VersionManagementRequested;

    public VersionListRootPage()
    {
        _dialogService = App.GetService<ICommonDialogService>();
        _progressDialogService = App.GetService<IProgressDialogService>();
        _uiDispatcher = App.GetService<IUiDispatcher>();
        InitializeComponent();
        VersionsListView.ItemClick += VersionsListView_ItemClick;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        SetViewModel(e.Parameter as VersionListViewModel ?? App.GetService<VersionListViewModel>());
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        DetachViewModel();
    }

    public void ResetEmbeddedVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        ContentArea.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
        VersionsListView.Opacity = 1;
        VersionsListView.Translation = default;
        VersionsListView.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
        EmptyListPanel.Opacity = 1;
        EmptyListPanel.Translation = default;
        EmptyListPanel.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
    }

    public void SetLocalNavigationTargetElementEnabled(bool enabled)
    {
        var current = EntranceNavigationTransitionInfo.GetIsTargetElement(ContentArea);
        if (current == enabled)
        {
            return;
        }

        EntranceNavigationTransitionInfo.SetIsTargetElement(ContentArea, enabled);
    }

    private void SetViewModel(VersionListViewModel viewModel)
    {
        if (ReferenceEquals(_attachedViewModel, viewModel))
        {
            return;
        }

        DetachViewModel();

        ViewModel = viewModel;
        _attachedViewModel = viewModel;
        DataContext = viewModel;

        viewModel.ExportModpackRequested += OnExportModpackRequested;
        viewModel.CompleteVersionRequested += OnCompleteVersionRequested;
        viewModel.CompleteVersionProgressUpdated += OnCompleteVersionProgressUpdated;
        viewModel.CompleteVersionCompleted += OnCompleteVersionCompleted;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;

        if (!viewModel.IsLoading)
        {
            var requestId = ++_iconWarmupRequestId;
            _ = WarmupVersionIconsAsync(viewModel, requestId);
        }
    }

    private void DetachViewModel()
    {
        if (_attachedViewModel == null)
        {
            return;
        }

        _attachedViewModel.ExportModpackRequested -= OnExportModpackRequested;
        _attachedViewModel.CompleteVersionRequested -= OnCompleteVersionRequested;
        _attachedViewModel.CompleteVersionProgressUpdated -= OnCompleteVersionProgressUpdated;
        _attachedViewModel.CompleteVersionCompleted -= OnCompleteVersionCompleted;
        _attachedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _attachedViewModel = null;
    }

    private async Task WarmupVersionIconsAsync(VersionListViewModel viewModel, int requestId)
    {
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in viewModel.FilteredVersions)
        {
            uniquePaths.Add(VersionIconPathHelper.NormalizeOrDefault(item.VersionIconPath));
        }

        foreach (var iconPath in uniquePaths)
        {
            if (requestId != _iconWarmupRequestId)
            {
                return;
            }

            if (_versionIconImageCache.ContainsKey(iconPath))
            {
                continue;
            }

            var processedIcon = await GetOrCreateProcessedIconAsync(iconPath);
            if (processedIcon != null)
            {
                _versionIconImageCache[iconPath] = processedIcon;
            }
        }
    }

    private async void VersionIconImage_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (sender is not Image image || image.DataContext is not VersionListViewModel.VersionInfoItem versionItem)
        {
            return;
        }

        var normalizedPath = VersionIconPathHelper.NormalizeOrDefault(versionItem.VersionIconPath);

        if (Equals(image.Tag, normalizedPath) && image.Source != null)
        {
            return;
        }

        image.Tag = normalizedPath;

        if (_versionIconImageCache.TryGetValue(normalizedPath, out var cachedIcon))
        {
            image.Source = cachedIcon;
            return;
        }

        if (image.Source == null)
        {
            image.Source = BuildImmediateIconSource(normalizedPath);
        }

        var processedIcon = await GetOrCreateProcessedIconAsync(normalizedPath);
        if (processedIcon != null)
        {
            _versionIconImageCache[normalizedPath] = processedIcon;
        }

        if (Equals(image.Tag, normalizedPath))
        {
            image.Source = processedIcon;
        }
    }

    private async Task<BitmapImage?> GetOrCreateProcessedIconAsync(string iconPath)
    {
        if (_versionIconProcessingTasks.TryGetValue(iconPath, out var existingTask))
        {
            return await existingTask;
        }

        var processingTask = VersionIconProcessingHelper.ProcessAsync(iconPath);
        _versionIconProcessingTasks[iconPath] = processingTask;

        try
        {
            return await processingTask;
        }
        finally
        {
            _versionIconProcessingTasks.Remove(iconPath);
        }
    }

    private static BitmapImage? BuildImmediateIconSource(string iconPath)
    {
        try
        {
            if (Uri.TryCreate(iconPath, UriKind.Absolute, out var absoluteUri))
            {
                return new BitmapImage(absoluteUri);
            }

            if (Path.IsPathRooted(iconPath))
            {
                return new BitmapImage(new Uri(iconPath, UriKind.Absolute));
            }
        }
        catch
        {
        }

        return null;
    }

    private async void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not VersionListViewModel viewModel)
        {
            return;
        }

        if (e.PropertyName == nameof(viewModel.FilteredVersions))
        {
            var requestId = ++_iconWarmupRequestId;
            _ = WarmupVersionIconsAsync(viewModel, requestId);
            return;
        }

        if (e.PropertyName == nameof(viewModel.IsLoading))
        {
            if (viewModel.IsLoading)
            {
                _iconWarmupRequestId++;
            }
            else
            {
                var requestId = ++_iconWarmupRequestId;
                _ = WarmupVersionIconsAsync(viewModel, requestId);
            }

            return;
        }

        if (e.PropertyName == nameof(viewModel.IsRenameDialogVisible))
        {
            if (viewModel.IsRenameDialogVisible && !_isRenameDialogOpen)
            {
                try
                {
                    _isRenameDialogOpen = true;

                    var newName = await _dialogService.ShowRenameDialogAsync(
                        "重命名版本",
                        viewModel.NewVersionName,
                        "新版本名称",
                        "请输入新的版本名称：");
                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        viewModel.NewVersionName = newName;

                        var (success, message) = await viewModel.ExecuteRenameVersionAsync();

                        if (!success)
                        {
                            await _dialogService.ShowMessageDialogAsync("重命名失败", message, "确定");
                        }
                    }

                    _isRenameDialogOpen = false;
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

    private void OnCompleteVersionRequested(object? sender, VersionListViewModel.VersionInfoItem e)
    {
        if (_isCompleteVersionDialogOpen)
        {
            return;
        }

        _isCompleteVersionDialogOpen = true;
        _completeVersionDialogState.Set($"版本 {e.Name}\n正在检查依赖...", 0, "0.0%");

        var dialogTask = _progressDialogService.ShowObservableProgressDialogAsync(
            "版本补全",
            () => _completeVersionDialogState.Status,
            () => _completeVersionDialogState.Progress,
            () => _completeVersionDialogState.ProgressText,
            _completeVersionDialogState,
            primaryButtonText: null,
            closeButtonText: "关闭");

        _ = dialogTask.ContinueWith(_ => _isCompleteVersionDialogOpen = false, TaskScheduler.Default);
    }

    private void OnCompleteVersionProgressUpdated(object? sender, (double Progress, string Stage, string CurrentFile) e)
    {
        if (!_isCompleteVersionDialogOpen)
        {
            return;
        }

        _uiDispatcher.TryEnqueue(() =>
        {
            var status = string.IsNullOrEmpty(e.Stage) ? _completeVersionDialogState.Status : e.Stage;
            if (!string.IsNullOrEmpty(e.CurrentFile))
            {
                var displayFile = e.CurrentFile.Length > 8 ? e.CurrentFile.Substring(0, 8) + "..." : e.CurrentFile;
                status = $"{status}\n当前: {displayFile}";
            }

            var progress = e.Progress >= 0 ? e.Progress : _completeVersionDialogState.Progress;
            _completeVersionDialogState.Set(status, progress, $"{progress:F1}%");
        });
    }

    private void OnCompleteVersionCompleted(object? sender, (bool Success, string Message) e)
    {
        _uiDispatcher.TryEnqueue(() =>
        {
            if (e.Success)
            {
                _completeVersionDialogState.Set("补全完成！", 100, "100%");
            }
            else
            {
                _completeVersionDialogState.Set(e.Message, _completeVersionDialogState.Progress, _completeVersionDialogState.ProgressText);
            }
        });
    }

    private async void OnExportModpackRequested(object? sender, VersionListViewModel.VersionInfoItem e)
    {
        if (DataContext is not VersionListViewModel viewModel)
        {
            return;
        }

        viewModel.ModpackName = e.Name;
        viewModel.ModpackVersion = "1.0.0";

        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible
        };

        var mainStack = new StackPanel { Spacing = 0 };

        var instructionText = new TextBlock
        {
            Text = "请选择要导出的数据：",
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        mainStack.Children.Add(instructionText);

        var optionsStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 16)
        };

        if (viewModel.ResourceDirectories.Count > 0)
        {
            var resourceStack = new StackPanel();
            var headerGrid = new Grid
            {
                Margin = new Thickness(0, 4, 0, 2),
                Padding = new Thickness(4)
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var toggleIcon = new FontIcon
            {
                Glyph = "\uE76C",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var toggleButton = new Button
            {
                Content = toggleIcon,
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(toggleButton, 0);

            var resourceAllCheckBox = new CheckBox
            {
                Content = "版本目录资源",
                IsThreeState = true,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(resourceAllCheckBox, 1);

            var treeView = new ItemsControl
            {
                Margin = new Thickness(0, 0, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Visibility = Visibility.Collapsed
            };

            treeView.ItemsSource = viewModel.ResourceDirectories;
            treeView.ItemTemplate = (DataTemplate)Resources["ResourceItemTemplate"];

            var isExpanded = false;
            toggleButton.Click += (_, _) =>
            {
                isExpanded = !isExpanded;
                treeView.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
                toggleIcon.Glyph = isExpanded ? "\uE70D" : "\uE76C";
            };

            resourceAllCheckBox.Checked += (_, _) =>
            {
                foreach (var dir in viewModel.ResourceDirectories)
                {
                    dir.IsSelected = true;
                }
            };

            resourceAllCheckBox.Unchecked += (_, _) =>
            {
                foreach (var dir in viewModel.ResourceDirectories)
                {
                    dir.IsSelected = false;
                }
            };

            void UpdateResourceAllStateCore()
            {
                if (viewModel.ResourceDirectories.Count == 0)
                {
                    resourceAllCheckBox.IsChecked = false;
                    return;
                }

                var allSelected = true;
                var noneSelected = true;

                foreach (var item in viewModel.ResourceDirectories)
                {
                    if (item.IsSelected)
                    {
                        noneSelected = false;
                    }
                    else
                    {
                        allSelected = false;
                    }

                    if (!allSelected && !noneSelected)
                    {
                        break;
                    }
                }

                if (noneSelected)
                {
                    resourceAllCheckBox.IsChecked = false;
                }
                else if (allSelected)
                {
                    resourceAllCheckBox.IsChecked = true;
                }
                else
                {
                    resourceAllCheckBox.IsChecked = null;
                }
            }

            foreach (var item in viewModel.ResourceDirectories)
            {
                item.SelectedChanged += (_, _) => UpdateResourceAllStateCore();
            }

            headerGrid.Children.Add(toggleButton);
            headerGrid.Children.Add(resourceAllCheckBox);

            resourceStack.Children.Add(headerGrid);
            resourceStack.Children.Add(treeView);
            optionsStack.Children.Add(resourceStack);
        }

        mainStack.Children.Add(optionsStack);

        var inputStack = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var nameStack = new StackPanel { Spacing = 4 };
        nameStack.Children.Add(new TextBlock { Text = "整合包名称", FontSize = 14 });
        var nameTextBox = new TextBox
        {
            PlaceholderText = "请输入整合包名称",
            Text = viewModel.ModpackName,
            Width = 400,
            MaxWidth = 400
        };
        nameTextBox.TextChanged += (_, _) => viewModel.ModpackName = nameTextBox.Text;
        nameStack.Children.Add(nameTextBox);
        inputStack.Children.Add(nameStack);

        var versionStack = new StackPanel { Spacing = 4 };
        versionStack.Children.Add(new TextBlock { Text = "整合包版本", FontSize = 14 });
        var versionTextBox = new TextBox
        {
            PlaceholderText = "请输入整合包版本",
            Text = viewModel.ModpackVersion,
            Width = 400,
            MaxWidth = 400
        };
        versionTextBox.TextChanged += (_, _) => viewModel.ModpackVersion = versionTextBox.Text;
        versionStack.Children.Add(versionTextBox);
        inputStack.Children.Add(versionStack);

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

        offlineModeCheckBox.Checked += (_, _) =>
        {
            viewModel.IsOfflineMode = true;
            warningInfoBar.Visibility = Visibility.Visible;
        };

        offlineModeCheckBox.Unchecked += (_, _) =>
        {
            viewModel.IsOfflineMode = false;
            warningInfoBar.Visibility = Visibility.Collapsed;
        };

        serverOnlyCheckBox.Checked += (_, _) => viewModel.IsServerOnly = true;
        serverOnlyCheckBox.Unchecked += (_, _) => viewModel.IsServerOnly = false;

        checkBoxStack.Children.Add(offlineModeCheckBox);
        checkBoxStack.Children.Add(serverOnlyCheckBox);
        inputStack.Children.Add(checkBoxStack);

        mainStack.Children.Add(inputStack);
        mainStack.Children.Add(warningInfoBar);

        scrollViewer.Content = mainStack;

        var result = await _dialogService.ShowCustomDialogAsync(
            "导出整合包",
            scrollViewer,
            primaryButtonText: "确认",
            closeButtonText: "取消",
            defaultButton: ContentDialogButton.Primary);

        if (result == ContentDialogResult.Primary)
        {
            await ExportModpackDialog_PrimaryButtonClick_Logic();
        }
    }

    private void VersionsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is VersionListViewModel.VersionInfoItem version)
        {
            VersionManagementRequested?.Invoke(this, version);
        }
    }

    private async Task ExportModpackDialog_PrimaryButtonClick_Logic()
    {
        if (DataContext is not VersionListViewModel viewModel)
        {
            return;
        }

        _isExportCancelled = false;
        var selectedOptions = viewModel.GetSelectedExportOptions();

        System.Diagnostics.Debug.WriteLine("=== 导出整合包选项 ===");
        System.Diagnostics.Debug.WriteLine($"版本: {viewModel.SelectedVersion?.Name}");
        System.Diagnostics.Debug.WriteLine($"选择的导出项数量: {selectedOptions.Count}");
        foreach (var option in selectedOptions)
        {
            System.Diagnostics.Debug.WriteLine($"- {option}");
        }
        System.Diagnostics.Debug.WriteLine("====================");

        ShowLoadingDialog();
        UpdateLoadingDialog("正在获取Modrinth资源...", 0.0);

        _ = Task.Run(async () =>
        {
            try
            {
                Dictionary<string, Core.Models.ModrinthVersion> fileResults = new Dictionary<string, Core.Models.ModrinthVersion>();
                bool isOfflineMode = viewModel.IsOfflineMode;
                bool isServerOnly = viewModel.IsServerOnly;

                _uiDispatcher.TryEnqueue(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"导出模式: {(isOfflineMode ? "非联网模式" : "联网模式")}{(isServerOnly ? " + 仅导出服务端" : string.Empty)}");
                });

                List<string> filteredOptions = new List<string>(selectedOptions);
                if (isServerOnly)
                {
                    var clientOnlyItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "resourcepacks",
                        "shaderpacks",
                        "options.txt",
                        "screenshots",
                        "journeymap"
                    };

                    filteredOptions = filteredOptions.Where(option =>
                    {
                        string itemName = Path.GetFileName(option);
                        return !clientOnlyItems.Contains(itemName)
                            && !option.StartsWith("screenshots", StringComparison.OrdinalIgnoreCase)
                            && !option.StartsWith("journeymap", StringComparison.OrdinalIgnoreCase);
                    }).ToList();

                    _uiDispatcher.TryEnqueue(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"仅导出服务端模式，过滤前选项数量: {selectedOptions.Count}，过滤后选项数量: {filteredOptions.Count}");
                        System.Diagnostics.Debug.WriteLine("过滤掉的客户端特定项:");
                        foreach (string option in selectedOptions.Except(filteredOptions))
                        {
                            System.Diagnostics.Debug.WriteLine($"- {option}");
                        }
                    });
                }

                bool shouldCheckModrinth = !isOfflineMode || isServerOnly;
                if (shouldCheckModrinth && viewModel.SelectedVersion != null && !_isExportCancelled)
                {
                    _uiDispatcher.TryEnqueue(() =>
                    {
                        System.Diagnostics.Debug.WriteLine("开始搜索Modrinth获取文件信息...");
                        UpdateLoadingDialog("正在获取Modrinth资源...", 10.0);
                    });

                    fileResults = await viewModel.SearchModrinthForFilesAsync(viewModel.SelectedVersion, filteredOptions);

                    _uiDispatcher.TryEnqueue(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Modrinth搜索完成，找到 {fileResults.Count} 个匹配结果");
                    });

                    if (isServerOnly)
                    {
                        var modrinthService = App.GetService<Core.Services.ModrinthService>();
                        var filesToRemove = new List<string>();
                        var serverUnsupportedFiles = new HashSet<string>();
                        var serverUnsupportedProjectCache = new Dictionary<string, bool>(StringComparer.Ordinal);

                        _uiDispatcher.TryEnqueue(() =>
                        {
                            System.Diagnostics.Debug.WriteLine("开始过滤服务端不支持的文件...");
                            UpdateLoadingDialog("正在过滤服务端不支持的文件...", 30.0);
                        });

                        foreach (var kvp in fileResults)
                        {
                            if (_isExportCancelled)
                            {
                                break;
                            }

                            string filePath = kvp.Key;
                            var modrinthVersion = kvp.Value;

                            if (string.IsNullOrEmpty(modrinthVersion.ProjectId))
                            {
                                continue;
                            }

                            try
                            {
                                if (!serverUnsupportedProjectCache.TryGetValue(modrinthVersion.ProjectId, out bool isServerUnsupported))
                                {
                                    var projectDetail = await modrinthService.GetProjectDetailAsync(modrinthVersion.ProjectId);
                                    string serverSide = projectDetail?.ServerSide?.ToLowerInvariant() ?? "unknown";
                                    isServerUnsupported = serverSide == "unsupported";
                                    serverUnsupportedProjectCache[modrinthVersion.ProjectId] = isServerUnsupported;

                                    _uiDispatcher.TryEnqueue(() =>
                                    {
                                        System.Diagnostics.Debug.WriteLine($"文件: {filePath}");
                                        System.Diagnostics.Debug.WriteLine($"  ProjectId: {modrinthVersion.ProjectId}");
                                        System.Diagnostics.Debug.WriteLine($"  服务端支持: {serverSide}");
                                    });
                                }

                                if (isServerUnsupported)
                                {
                                    filesToRemove.Add(filePath);
                                    serverUnsupportedFiles.Add(filePath);
                                    _uiDispatcher.TryEnqueue(() =>
                                    {
                                        System.Diagnostics.Debug.WriteLine("  标记为移除：服务端不支持");
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                _uiDispatcher.TryEnqueue(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine($"获取项目详情失败: {modrinthVersion.ProjectId}, 错误: {ex.Message}");
                                });
                            }
                        }

                        if (filesToRemove.Count > 0)
                        {
                            _uiDispatcher.TryEnqueue(() =>
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

                            _uiDispatcher.TryEnqueue(() =>
                            {
                                System.Diagnostics.Debug.WriteLine($"过滤后剩余 {fileResults.Count} 个文件");
                            });
                        }

                        filteredOptions = filteredOptions.Where(option => !serverUnsupportedFiles.Contains(option)).ToList();

                        _uiDispatcher.TryEnqueue(() =>
                        {
                            System.Diagnostics.Debug.WriteLine($"过滤后剩余的导出选项数量: {filteredOptions.Count}");
                        });
                    }
                }

                if (isOfflineMode && !isServerOnly)
                {
                    _uiDispatcher.TryEnqueue(() =>
                    {
                        System.Diagnostics.Debug.WriteLine("非联网模式且不导出服务端，跳过Modrinth搜索");
                        UpdateLoadingDialog("准备保存整合包...", 20.0);
                    });
                }

                if (_isExportCancelled)
                {
                    _uiDispatcher.TryEnqueue(HideLoadingDialog);
                    return;
                }

                _uiDispatcher.TryEnqueue(() => UpdateLoadingDialog("准备保存整合包...", 20.0));

                StorageFile? file = null;
                var filePickerTask = new TaskCompletionSource<StorageFile?>();

                try
                {
                    await _uiDispatcher.RunOnUiThreadAsync(async () =>
                    {
                        var savePicker = new FileSavePicker();
                        savePicker.FileTypeChoices.Add("Modrinth Pack", new List<string> { FileExtensionConsts.Mrpack });
                        string defaultFileName = string.IsNullOrEmpty(viewModel.ModpackName) ? "Untitled" : viewModel.ModpackName;
                        savePicker.SuggestedFileName = defaultFileName;
                        savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

                        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                        var pickedFile = await savePicker.PickSaveFileAsync();
                        filePickerTask.SetResult(pickedFile);
                    });
                }
                catch (Exception ex)
                {
                    filePickerTask.TrySetException(ex);
                }

                file = await filePickerTask.Task;

                if (file == null || _isExportCancelled)
                {
                    _uiDispatcher.TryEnqueue(HideLoadingDialog);
                    return;
                }

                _uiDispatcher.TryEnqueue(() => UpdateLoadingDialog("正在创建整合包...", 30.0));

                string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                try
                {
                    string overridesDir = Path.Combine(tempDir, "overrides");
                    Directory.CreateDirectory(overridesDir);

                    var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
                    string versionPath = viewModel.SelectedVersion?.Path ?? string.Empty;
                    string versionName = viewModel.SelectedVersion?.Name ?? string.Empty;
                    Core.Models.VersionConfig versionConfig = await versionInfoService.GetFullVersionInfoAsync(versionName, versionPath);

                    string loaderName = string.Empty;
                    string loaderVersion = string.Empty;
                    string minecraftVersion = versionConfig?.MinecraftVersion ?? string.Empty;

                    if (!string.IsNullOrEmpty(versionConfig?.ModLoaderType))
                    {
                        switch (versionConfig.ModLoaderType.ToLowerInvariant())
                        {
                            case "fabric":
                                loaderName = "fabric-loader";
                                loaderVersion = versionConfig.ModLoaderVersion ?? string.Empty;
                                break;
                            case "legacyfabric":
                                loaderName = "LegacyFabric";
                                loaderVersion = versionConfig.ModLoaderVersion ?? string.Empty;
                                break;
                            case "forge":
                                loaderName = "forge";
                                loaderVersion = versionConfig.ModLoaderVersion ?? string.Empty;
                                break;
                            case "neoforge":
                                loaderName = "neoforge";
                                loaderVersion = versionConfig.ModLoaderVersion ?? string.Empty;
                                break;
                            case "quilt":
                                loaderName = "quilt-loader";
                                loaderVersion = versionConfig.ModLoaderVersion ?? string.Empty;
                                break;
                            default:
                                loaderName = versionConfig.ModLoaderType;
                                loaderVersion = versionConfig.ModLoaderVersion ?? string.Empty;
                                break;
                        }
                    }

                    var indexJson = new
                    {
                        game = "minecraft",
                        formatVersion = 1,
                        versionId = viewModel.ModpackVersion,
                        name = viewModel.ModpackName,
                        summary = string.Empty,
                        files = new List<object>(),
                        dependencies = new Dictionary<string, string>
                        {
                            { "minecraft", minecraftVersion ?? viewModel.SelectedVersion?.VersionNumber ?? string.Empty },
                            { loaderName, loaderVersion }
                        }
                    };

                    if (string.IsNullOrEmpty(loaderName) || string.IsNullOrEmpty(loaderVersion))
                    {
                        ((Dictionary<string, string>)indexJson.dependencies).Remove(loaderName);
                    }

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
                                        path = filePath.Replace('\\', '/'),
                                        hashes = primaryFile.Hashes,
                                        downloads = new List<string> { primaryFile.Url.ToString() },
                                        fileSize = primaryFile.Size
                                    };
                                    filesList.Add(fileEntry);
                                }
                            }
                        }
                    }

                    string indexJsonPath = Path.Combine(tempDir, MinecraftFileConsts.ModrinthIndexJson);
                    string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(indexJson, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(indexJsonPath, jsonContent);

                    _uiDispatcher.TryEnqueue(() => UpdateLoadingDialog("正在处理文件...", 40.0));

                    HashSet<string> processedDirectories = new HashSet<string>();
                    var filesToExport = new List<string>();
                    foreach (string option in filteredOptions)
                    {
                        if (_isExportCancelled)
                        {
                            break;
                        }

                        filesToExport.Add(option);
                    }

                    foreach (string option in filesToExport)
                    {
                        if (_isExportCancelled)
                        {
                            break;
                        }

                        string fullPath = Path.Combine(viewModel.SelectedVersion!.Path, option);

                        if (Directory.Exists(fullPath))
                        {
                            string overrideDir = Path.Combine(overridesDir, option);
                            Directory.CreateDirectory(overrideDir);
                            processedDirectories.Add(option);
                        }
                        else if (File.Exists(fullPath))
                        {
                            bool isModrinthFile = !isOfflineMode && fileResults.ContainsKey(option);

                            if (!isModrinthFile)
                            {
                                string destPath = Path.Combine(overridesDir, option);
                                string destDir = Path.GetDirectoryName(destPath)!;
                                Directory.CreateDirectory(destDir);
                                File.Copy(fullPath, destPath, true);
                            }

                            string parentDir = Path.GetDirectoryName(option)!;
                            if (!string.IsNullOrEmpty(parentDir))
                            {
                                processedDirectories.Add(parentDir);
                            }
                        }
                    }

                    if (_isExportCancelled)
                    {
                        _uiDispatcher.TryEnqueue(HideLoadingDialog);
                        return;
                    }

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
                        _uiDispatcher.TryEnqueue(HideLoadingDialog);
                        return;
                    }

                    _uiDispatcher.TryEnqueue(() => UpdateLoadingDialog("正在压缩整合包...", 70.0));

                    using (var fileStream = new FileStream(file.Path, FileMode.Create, FileAccess.Write))
                    using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
                    {
                        if (_isExportCancelled)
                        {
                            _uiDispatcher.TryEnqueue(HideLoadingDialog);
                            return;
                        }

                        archive.CreateEntryFromFile(indexJsonPath, MinecraftFileConsts.ModrinthIndexJson);
                        AddDirectoryToZip(archive, overridesDir, "overrides");
                    }

                    if (!_isExportCancelled)
                    {
                        _uiDispatcher.EnqueueAsync(async () =>
                        {
                            UpdateLoadingDialog("导出完成！", 100.0);
                            System.Diagnostics.Debug.WriteLine($"整合包导出成功：{file.Path}");
                            await Task.Delay(1000);
                            HideLoadingDialog();
                        }).Observe("VersionListRootPage.ExportModpack.SuccessFinalize");
                    }
                    else
                    {
                        _uiDispatcher.TryEnqueue(() =>
                        {
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
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _uiDispatcher.EnqueueAsync(async () =>
                {
                    System.Diagnostics.Debug.WriteLine($"导出整合包失败：{ex.Message}");
                    UpdateLoadingDialog($"导出失败：{ex.Message}", 0.0);
                    await Task.Delay(2000);
                    HideLoadingDialog();
                }).Observe("VersionListRootPage.ExportModpack.ErrorFinalize");
            }
        });
    }

    private void ShowLoadingDialog()
    {
        if (_loadingDialogCloseSignal != null)
        {
            return;
        }

        _loadingDialogState.Set("正在获取Modrinth资源...", 0.0, "0.0%");
        _loadingDialogCloseSignal = new TaskCompletionSource<bool>();

        var dialogTask = _progressDialogService.ShowObservableProgressDialogAsync(
            "正在导出整合包",
            () => _loadingDialogState.Status,
            () => _loadingDialogState.Progress,
            () => _loadingDialogState.ProgressText,
            _loadingDialogState,
            primaryButtonText: null,
            closeButtonText: "取消",
            autoCloseWhen: _loadingDialogCloseSignal.Task);

        _ = dialogTask.ContinueWith(task =>
        {
            if (!_isExportCancelled && task.Status == TaskStatus.RanToCompletion && task.Result == ContentDialogResult.None)
            {
                _isExportCancelled = true;
                _loadingDialogState.Set("正在取消导出...", _loadingDialogState.Progress, _loadingDialogState.ProgressText);
            }

            _loadingDialogCloseSignal = null;
        }, TaskScheduler.Default);
    }

    private void HideLoadingDialog()
    {
        if (_loadingDialogCloseSignal == null)
        {
            return;
        }

        _loadingDialogCloseSignal.TrySetResult(true);
    }

    private void UpdateLoadingDialog(string status, double progress)
    {
        _loadingDialogState.Set(status, progress, $"{progress:0.0}%");
    }

    private void AddDirectoryToZip(ZipArchive archive, string sourceDir, string entryName)
    {
        if (_isExportCancelled)
        {
            return;
        }

        string[] files = Directory.GetFiles(sourceDir);
        foreach (string file in files)
        {
            if (_isExportCancelled)
            {
                return;
            }

            string relativePath = Path.GetRelativePath(sourceDir, file);
            string zipEntryName = Path.Combine(entryName, relativePath).Replace('\\', '/');
            archive.CreateEntryFromFile(file, zipEntryName);
        }

        string[] subDirs = Directory.GetDirectories(sourceDir);
        foreach (string subDir in subDirs)
        {
            if (_isExportCancelled)
            {
                return;
            }

            string relativePath = Path.GetRelativePath(sourceDir, subDir);
            string zipEntryName = Path.Combine(entryName, relativePath).Replace('\\', '/');
            AddDirectoryToZip(archive, subDir, zipEntryName);
        }
    }

    private void ItemExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is VersionListViewModel.ResourceItem item)
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }
}