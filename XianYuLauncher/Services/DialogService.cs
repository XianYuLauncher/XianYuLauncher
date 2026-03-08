using System.IO;
using System.Threading.Tasks; // Explicitly import missing namespace
using System.Threading;
using System.Net.Http;
using Microsoft.UI.Dispatching;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Storage.Streams;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Services;

/// <summary>
/// 弹窗服务，弹窗主题跟随启动器内主题（而非系统主题）。
/// </summary>
public class DialogService : IDialogService
{
    private XamlRoot _xamlRoot;
    // 使用信号量确保同一时间只有一个弹窗显示，防止 WinUI 崩溃 (COM 0x80000019)
    private readonly SemaphoreSlim _dialogSemaphore = new(1, 1);
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly UISettings _uiSettings = new();
    private ContentDialog? _activeDialog;

    public DialogService(IThemeSelectorService themeSelectorService)
    {
        _themeSelectorService = themeSelectorService ?? throw new ArgumentNullException(nameof(themeSelectorService));
        _httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
        _uiSettings.ColorValuesChanged += OnSystemColorValuesChanged;
    }

    /// <summary>
    /// 系统主题变化时（弹窗打开期间用户去系统设置改主题），动态更新弹窗主题。
    /// 仅当启动器主题为「跟随系统」时生效。
    /// </summary>
    private void OnSystemColorValuesChanged(UISettings sender, object args)
    {
        var dispatcher = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcher == null) return;
        dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            if (_activeDialog != null && _themeSelectorService.Theme == ElementTheme.Default)
            {
                _activeDialog.RequestedTheme = GetEffectiveDialogTheme();
            }
        });
    }

    public void SetXamlRoot(XamlRoot xamlRoot)
    {
        _xamlRoot = xamlRoot;
    }
    
    /// <summary>
    /// 安全显示弹窗，自动处理队列。弹窗主题跟随启动器内主题。
    /// </summary>
    private async Task<ContentDialogResult> ShowSafeAsync(ContentDialog dialog)
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (dialog.XamlRoot == null)
            {
                var root = GetXamlRoot();
                if (root == null) return ContentDialogResult.None;
                dialog.XamlRoot = root;
            }

            // ContentDialog 被 reparent 到 popup 层，不继承根元素主题，需显式设置以跟随启动器主题
            dialog.RequestedTheme = GetEffectiveDialogTheme();
            _activeDialog = dialog;

            return await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DialogService] 弹窗显示异常: {ex.Message}");
            return ContentDialogResult.None;
        }
        finally
        {
            _activeDialog = null;
            _dialogSemaphore.Release();
        }
    }

    private XamlRoot GetXamlRoot()
    {
        // 如果显式设置了 XamlRoot，优先使用
        if (_xamlRoot != null)
        {
            return _xamlRoot;
        }

        // 尝试从主窗口获取 (对于 WinUI 3 来说，ContentDialog 必须设置 XamlRoot)
        if (App.MainWindow?.Content?.XamlRoot is XamlRoot root)
        {
            return root;
        }

        return null;
    }

    /// <summary>
    /// 获取弹窗应使用的主题。当用户选择「跟随系统」时，解析为实际系统主题。
    /// </summary>
    private ElementTheme GetEffectiveDialogTheme()
    {
        var theme = _themeSelectorService.Theme;
        if (theme != ElementTheme.Default)
            return theme;

        var uiSettings = new UISettings();
        var background = uiSettings.GetColorValue(UIColorType.Background);
        // 浅色背景 (R=255,G=255,B=255) 表示系统为浅色主题
        return background.R == 255 && background.G == 255 && background.B == 255 ? ElementTheme.Light : ElementTheme.Dark;
    }

    public async Task ShowMessageDialogAsync(string title, string message, string closeButtonText = "确定")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        await ShowSafeAsync(dialog);
    }

    public async Task<bool> ShowConfirmationDialogAsync(string title, string message, string primaryButtonText = "是", string closeButtonText = "否")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var result = await ShowSafeAsync(dialog);
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowJavaNotFoundDialogAsync(string requiredVersion, Action onManualDownload, Action onAutoDownload)
    {
        var dialog = new ContentDialog
        {
            Title = "Java运行时环境未找到",
            Content = $"未找到适用于当前游戏版本的Java运行时环境。\n\n游戏版本需要: Java {requiredVersion}\n\n推荐使用自动下载功能，启动器将自动安装并配置环境。",
            PrimaryButtonText = "自动下载(推荐)",
            SecondaryButtonText = "手动下载",
            CloseButtonText = "取消",
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        dialog.PrimaryButtonClick += (s, e) => onAutoDownload?.Invoke();
        dialog.SecondaryButtonClick += (s, e) => onManualDownload?.Invoke();

        await ShowSafeAsync(dialog);
    }

    public async Task ShowOfflineLaunchTipDialogAsync(int offlineLaunchCount, Action onSupportAction)
    {
        var dialog = new ContentDialog
        {
            Title = "离线游玩提示",
            Content = $"您已经使用离线模式启动{offlineLaunchCount}次了,支持一下正版吧！",
            PrimaryButtonText = "知道了",
            SecondaryButtonText = "支持正版",
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };
        
        var result = await ShowSafeAsync(dialog);
        if (result == ContentDialogResult.Secondary)
        {
            onSupportAction?.Invoke();
        }
    }

    public async Task<bool> ShowTokenExpiredDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "LaunchPage_TokenExpiredTitle".GetLocalized(),
            Content = "LaunchPage_TokenExpiredContent".GetLocalized(),
            PrimaryButtonText = "LaunchPage_GoToLoginText".GetLocalized(),
            CloseButtonText = "TutorialPage_CancelButtonText".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };
        
        var result = await ShowSafeAsync(dialog);
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowExportSuccessDialogAsync(string filePath)
    {
        var dialog = new ContentDialog
        {
            Title = "导出成功",
            Content = $"启动参数已成功导出到桌面:\n{System.IO.Path.GetFileName(filePath)}\n\n您可以双击该文件来启动游戏。",
            PrimaryButtonText = "打开文件位置",
            CloseButtonText = "确定",
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };
        
        dialog.PrimaryButtonClick += (s, e) =>
        {
            // 打开文件所在文件夹并选中文件
            try 
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            catch {}
        };
        
        await ShowSafeAsync(dialog);
    }

    public async Task<bool> ShowRegionRestrictedDialogAsync(string errorMessage)
    {
        var dialog = new ContentDialog
        {
            Title = "地区限制",
            Content = errorMessage,
            PrimaryButtonText = "前往",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var result = await ShowSafeAsync(dialog);
        return result == ContentDialogResult.Primary;
    }

    public async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        return await ShowSafeAsync(dialog);
    }

    public async Task ShowProgressDialogAsync(string title, string message, Func<IProgress<double>, IProgress<string>, CancellationToken, Task> workCallback)
    {
        var progressBar = new ProgressBar { Maximum = 100, Value = 0, MinHeight = 4, Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 10), IsIndeterminate = true };
        var statusText = new TextBlock { Text = message, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap };
        
        var contentPanel = new StackPanel();
        contentPanel.Children.Add(statusText);
        contentPanel.Children.Add(progressBar);
        
        var dialog = new ContentDialog
        {
            Title = title,
            Content = contentPanel,
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.None,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var cts = new CancellationTokenSource();
        Task? backgroundWork = null;
        
        dialog.CloseButtonClick += (s, e) => 
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel();
        };

        var progress = new Progress<double>(p => 
        {
            progressBar.IsIndeterminate = false;
            progressBar.Value = p;
        });
        
        IProgress<string> statusProgress = new Progress<string>(s => statusText.Text = s);

        dialog.Opened += (s, e) =>
        {
            backgroundWork = Task.Run(async () =>
            {
                try
                {
                    await workCallback(progress, statusProgress, cts.Token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    statusProgress.Report($"操作失败: {ex.Message}");
                    await Task.Delay(2000);
                }
                finally
                {
                    // 确保在 UI 线程上关闭
                    dialog.DispatcherQueue.TryEnqueue(() => 
                    {
                        try { dialog.Hide(); } catch { }
                    });
                }
            });
        };

        await ShowSafeAsync(dialog);
        
        // dialog 关闭后，等待后台任务真正完成，确保所有异常都被观察到
        if (backgroundWork != null)
        {
            try
            {
                await backgroundWork;
            }
            catch
            {
                // 所有异常已在 Task.Run 内部处理过，这里兜底防止未观察异常
            }
        }
    }

    public async Task<XianYuLauncher.Core.Services.ExternalProfile?> ShowProfileSelectionDialogAsync(List<XianYuLauncher.Core.Services.ExternalProfile> profiles, string authServer)
    {
        // 1. 映射为 ViewModel 列表
        var items = new System.Collections.ObjectModel.ObservableCollection<ProfileSelectionItem>();
        
        // 预加载并处理默认史蒂夫头像 (Win2D 处理以确保像素清晰)
        BitmapImage defaultAvatar;
        try
        {
            defaultAvatar = await ProcessLocalSteveAvatarAsync();
        }
        catch
        {
            // 兜底回退
            defaultAvatar = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png"));
        }

        foreach (var p in profiles)
        {
            var item = new ProfileSelectionItem 
            { 
                Id = p.Id, 
                Name = p.Name, 
                OriginalProfile = p,
                Avatar = defaultAvatar 
            };
            items.Add(item);
        }

        // 2. 启动异步加载头像任务
        _ = Task.Run(async () => 
        {
            foreach (var item in items)
            {
                try
                {
                    // 构建 Session Server URL
                    var server = authServer;
                    if (!server.EndsWith("/")) server += "/";
                    var sessionUrl = $"{server}sessionserver/session/minecraft/profile/{item.Id}";
                    
                    // 获取皮肤数据
                    var response = await _httpClient.GetStringAsync(sessionUrl);
                    dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(response);
                    
                    string textureProperty = null;
                    if (data.properties != null)
                    {
                        foreach(var prop in data.properties)
                        {
                           if (prop.name == "textures")
                           {
                               textureProperty = prop.value;
                               break;
                           }
                        }
                    }

                    if (!string.IsNullOrEmpty(textureProperty))
                    {
                        var jsonBytes = Convert.FromBase64String(textureProperty);
                        var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
                        dynamic textureData = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                        string skinUrl = null;
                        if (textureData.textures != null && textureData.textures.SKIN != null)
                        {
                           skinUrl = textureData.textures.SKIN.url;
                        }

                        if (!string.IsNullOrEmpty(skinUrl))
                        {
                            // 下载并在 UI 线程使用 Win2D 处理
                            var skinBytes = await _httpClient.GetByteArrayAsync(skinUrl);
                            
                            App.MainWindow.DispatcherQueue.TryEnqueue(async () => 
                            {
                                try 
                                {
                                    var processedAvatar = await ProcessAvatarBytesAsync(skinBytes);
                                    if (processedAvatar != null)
                                    {
                                        item.Avatar = processedAvatar;
                                    }
                                }
                                catch {}
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DialogService] 加载头像失败 {item.Name}: {ex.Message}");
                }
            }
        });

        var itemTemplate = Application.Current.Resources["ProfileSelectionItemTemplate"] as DataTemplate;

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = items,
            SelectedIndex = 0,
            MaxHeight = 300,
            ItemTemplate = itemTemplate
        };

        // 如果列表为空，SelectedIndex设为-1
        if (profiles.Count == 0) listView.SelectedIndex = -1;

        var dialog = new ContentDialog
        {
            Title = "ProfilePage_ExternalLoginDialog_SelectProfileTitle".GetLocalized(),
            PrimaryButtonText = "ProfilePage_ExternalLoginDialog_ConfirmButton".GetLocalized(),
            CloseButtonText = "ProfilePage_ExternalLoginDialog_CancelButton".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            Content = listView,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var result = await ShowSafeAsync(dialog);
        if (result == ContentDialogResult.Primary)
        {
            var selectedItem = listView.SelectedItem as ProfileSelectionItem;
            return selectedItem?.OriginalProfile;
        }
        return null;
    }

    private async Task<BitmapImage> ProcessLocalSteveAvatarAsync()
    {
        try
        {
            var device = CanvasDevice.GetSharedDevice();
            var uri = new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png");
            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            
            using (var stream = await file.OpenReadAsync())
            {
                var originalBitmap = await CanvasBitmap.LoadAsync(device, stream);
                
                // 32x32 足够弹窗列表使用
                var renderTarget = new CanvasRenderTarget(device, 32, 32, 96);
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    PixelArtRenderHelper.SetAliased(ds);
                    
                    // 假设 steve.png 是已经提取好的头部或者需要整体显示的图片
                    // 我们对其进行整体缩放
                    PixelArtRenderHelper.DrawNearestNeighbor(
                        ds,
                        originalBitmap,
                        new Windows.Foundation.Rect(0, 0, 32, 32),
                        originalBitmap.Bounds);
                }

                using (var outputStream = new InMemoryRandomAccessStream())
                {
                    await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
                    outputStream.Seek(0);
                    
                    var bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(outputStream);
                    return bitmapImage;
                }
            }
        }
        catch
        {
            throw; // Let caller handle fallback
        }
    }

    private async Task<BitmapImage> ProcessAvatarBytesAsync(byte[] skinBytes)
    {
        try
        {
            var device = CanvasDevice.GetSharedDevice();
            using (var stream = new MemoryStream(skinBytes))
            {
                var originalBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
                
                var renderTarget = new CanvasRenderTarget(device, 32, 32, 96); // 弹窗使用32x32即可
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    PixelArtRenderHelper.SetAliased(ds);
                    
                    // 绘制头部 (Source: 8,8, 8,8) -> Target: 0,0, 32,32 (放大4倍)
                    PixelArtRenderHelper.DrawNearestNeighbor(
                        ds,
                        originalBitmap,
                        new Windows.Foundation.Rect(0, 0, 32, 32),
                        new Windows.Foundation.Rect(8, 8, 8, 8));
                                 
                    // 绘制第二层头部 (Source: 40,8, 8,8) if exists
                    PixelArtRenderHelper.DrawNearestNeighbor(
                        ds,
                        originalBitmap,
                        new Windows.Foundation.Rect(0, 0, 32, 32),
                        new Windows.Foundation.Rect(40, 8, 8, 8));
                }

                using (var outputStream = new InMemoryRandomAccessStream())
                {
                    await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
                    outputStream.Seek(0);
                    
                    var bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(outputStream);
                    return bitmapImage;
                }
            }
        }
        catch (Exception) // 移除了未使用的 ex
        {
            return null;
        }
    }

    // ==================== 资源下载详情页弹窗 ====================

    public async Task<ContentDialogResult> ShowDownloadMethodDialogAsync(
        string title,
        string instruction,
        IEnumerable<object>? dependencyProjects,
        bool isLoadingDependencies,
        Action<string>? onDependencyClick)
    {
        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = instruction, FontSize = 14 });

        // 前置 Mod 列表
        var deps = dependencyProjects?.ToList();
        if (deps != null && deps.Count > 0)
        {
            if (isLoadingDependencies)
            {
                panel.Children.Add(new ProgressRing
                {
                    IsActive = true,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Height = 32, Width = 32,
                    Margin = new Thickness(0, 8, 0, 8)
                });
            }
            else
            {
                var depsPanel = new StackPanel { Spacing = 8 };
                foreach (dynamic dep in deps)
                {
                    string projectId = dep.ProjectId;
                    string depTitle = dep.Title;
                    string description = dep.DisplayDescription;
                    string iconUrl = dep.IconUrl;

                    var cardContent = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Width = 356
                    };

                    var iconBorder = new Border { CornerRadius = new CornerRadius(4), Width = 40, Height = 40 };
                    var iconImage = new Image { Width = 40, Height = 40, Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill };
                    if (!string.IsNullOrEmpty(iconUrl))
                        iconImage.Source = new BitmapImage(new Uri(iconUrl));
                    iconBorder.Child = iconImage;
                    cardContent.Children.Add(iconBorder);

                    var textPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Width = 300 };
                    textPanel.Children.Add(new TextBlock { Text = depTitle, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
                    textPanel.Children.Add(new TextBlock { Text = description, FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, TextWrapping = TextWrapping.WrapWholeWords });
                    cardContent.Children.Add(textPanel);

                    var btn = new Button
                    {
                        Content = cardContent,
                        Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(12),
                        BorderThickness = new Thickness(1),
                        BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                        Width = 380,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    var capturedId = projectId;
                    btn.Click += (s, e) => onDependencyClick?.Invoke(capturedId);
                    depsPanel.Children.Add(btn);
                }
                panel.Children.Add(depsPanel);
            }
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "选择版本",
            SecondaryButtonText = "自定义位置",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.None,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        return await ShowSafeAsync(dialog);
    }

    public async Task<T?> ShowListSelectionDialogAsync<T>(
        string title,
        string instruction,
        IEnumerable<T> items,
        Func<T, string> displayMemberFunc,
        Func<T, double>? opacityFunc = null,
        string? tip = null,
        string primaryButtonText = "确认",
        string closeButtonText = "取消") where T : class
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = instruction, FontSize = 14 });

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 300
        };

        // 构建列表项
        var itemsList = items.ToList();
        foreach (var item in itemsList)
        {
            var grid = new Grid { Padding = new Thickness(8) };
            var textBlock = new TextBlock { Text = displayMemberFunc(item) };
            if (opacityFunc != null)
                textBlock.Opacity = opacityFunc(item);
            grid.Children.Add(textBlock);
            var lvi = new ListViewItem { Content = grid, Tag = item };
            listView.Items.Add(lvi);
        }

        panel.Children.Add(listView);

        if (!string.IsNullOrEmpty(tip))
        {
            panel.Children.Add(new TextBlock
            {
                Text = tip,
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var result = await ShowSafeAsync(dialog);
        if (result == ContentDialogResult.Primary && listView.SelectedItem is ListViewItem selectedLvi)
        {
            return selectedLvi.Tag as T;
        }
        return null;
    }

    public async Task<T?> ShowModVersionSelectionDialogAsync<T>(
        string title,
        string instruction,
        IEnumerable<T> items,
        Func<T, string> versionNumberFunc,
        Func<T, string> versionTypeFunc,
        Func<T, string> releaseDateFunc,
        Func<T, string> fileNameFunc,
        Func<T, string?>? resourceTypeTagFunc = null,
        string primaryButtonText = "安装",
        string closeButtonText = "取消") where T : class
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = instruction, FontSize = 14, TextWrapping = TextWrapping.WrapWholeWords });

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 400
        };

        foreach (var item in items)
        {
            var card = new Border
            {
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 4, 0, 4)
            };

            var cardPanel = new StackPanel { Spacing = 4 };

            // 版本号 + 类型标签行
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            headerRow.Children.Add(new TextBlock { Text = versionNumberFunc(item), FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

            var typeBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
            };
            var vt = versionTypeFunc(item);
            typeBadge.Child = new TextBlock
            {
                Text = string.IsNullOrEmpty(vt) ? vt : char.ToUpper(vt[0]) + vt[1..],
                FontSize = 11,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            headerRow.Children.Add(typeBadge);

            // 资源类型标签
            if (resourceTypeTagFunc != null)
            {
                var tag = resourceTypeTagFunc(item);
                if (!string.IsNullOrEmpty(tag))
                {
                    var resBadge = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                    };
                    resBadge.Child = new TextBlock
                    {
                        Text = tag,
                        FontSize = 11,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
                    };
                    headerRow.Children.Add(resBadge);
                }
            }

            cardPanel.Children.Add(headerRow);

            // 发布日期
            cardPanel.Children.Add(new TextBlock
            {
                Text = $"发布日期: {releaseDateFunc(item)}",
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            // 文件名
            cardPanel.Children.Add(new TextBlock
            {
                Text = fileNameFunc(item),
                FontSize = 11,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            card.Child = cardPanel;

            var lvi = new ListViewItem { Content = card, Tag = item, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0), Margin = new Thickness(0, 0, 0, 6) };
            listView.Items.Add(lvi);
        }

        panel.Children.Add(listView);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var result = await ShowSafeAsync(dialog);
        if (result == ContentDialogResult.Primary && listView.SelectedItem is ListViewItem selectedLvi)
        {
            return selectedLvi.Tag as T;
        }
        return null;
    }

    public async Task<ContentDialogResult> ShowObservableProgressDialogAsync(
        string title,
        Func<string> getStatus,
        Func<double> getProgress,
        Func<string> getProgressText,
        System.ComponentModel.INotifyPropertyChanged propertyChanged,
        string? primaryButtonText = null,
        string? closeButtonText = "取消",
        Task? autoCloseWhen = null,
        Func<string>? getSpeed = null)
    {
        var statusText = new TextBlock { Text = getStatus(), FontSize = 16, TextWrapping = TextWrapping.WrapWholeWords };
        var progressBar = new ProgressBar { Value = getProgress(), Minimum = 0, Maximum = 100, Height = 8, CornerRadius = new CornerRadius(4) };
        
        // 进度文本和速度文本放在同一行
        var progressInfoPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 12 };
        var progressText = new TextBlock
        {
            Text = getProgressText(),
            FontSize = 14,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        progressInfoPanel.Children.Add(progressText);
        
        TextBlock? speedText = null;
        if (getSpeed != null)
        {
            speedText = new TextBlock
            {
                Text = getSpeed(),
                FontSize = 14,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
            };
            progressInfoPanel.Children.Add(speedText);
        }

        var panel = new StackPanel { Spacing = 16, Width = 400 };
        panel.Children.Add(statusText);
        panel.Children.Add(progressBar);
        panel.Children.Add(progressInfoPanel);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            DefaultButton = ContentDialogButton.None,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        if (!string.IsNullOrEmpty(primaryButtonText))
            dialog.PrimaryButtonText = primaryButtonText;
        if (!string.IsNullOrEmpty(closeButtonText))
            dialog.CloseButtonText = closeButtonText;

        // 监听属性变更，更新 UI
        void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            dialog.DispatcherQueue?.TryEnqueue(() =>
            {
                statusText.Text = getStatus();
                progressBar.Value = getProgress();
                progressText.Text = getProgressText();
                if (speedText != null && getSpeed != null)
                {
                    speedText.Text = getSpeed();
                }
            });
        }

        propertyChanged.PropertyChanged += OnPropertyChanged;

        // 当 autoCloseWhen 完成时自动关闭弹窗
        if (autoCloseWhen != null)
        {
            _ = autoCloseWhen.ContinueWith(_ =>
            {
                dialog.DispatcherQueue?.TryEnqueue(() =>
                {
                    try { dialog.Hide(); }
                    catch { /* 弹窗可能已关闭 */ }
                });
            }, TaskScheduler.Default);
        }

        try
        {
            return await ShowSafeAsync(dialog);
        }
        finally
        {
            propertyChanged.PropertyChanged -= OnPropertyChanged;
        }
    }

    public async Task<System.Collections.Generic.List<XianYuLauncher.Models.UpdatableResourceItem>?> ShowUpdatableResourcesSelectionDialogAsync(System.Collections.Generic.IEnumerable<XianYuLauncher.Models.UpdatableResourceItem> availableUpdates)
    {
        var panel = new StackPanel { Spacing = 12, MaxWidth = 600, MinWidth = 400 };

        panel.Children.Add(new TextBlock {
            Text = "VersionManagerPage_UpdatableResourcesUpdateDialog_InstructionText".GetLocalized(),
            FontSize = 14
        });

        var itemsList = availableUpdates.ToList();
        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            MaxHeight = 350,
            ItemsSource = itemsList
        };
        listView.ItemTemplate = Application.Current.Resources["UpdatableResourceSelectionItemTemplate"] as DataTemplate;

        panel.Children.Add(listView);

        var dialog = new ContentDialog
        {
            Title = "VersionManagerPage_UpdatableResourcesUpdateDialog_Title".GetLocalized(), 
            Content = panel,
            PrimaryButtonText = "VersionManagerPage_UpdatableResourcesUpdateDialog_PrimaryButtonText".GetLocalized(),
            CloseButtonText = "VersionManagerPage_UpdatableResourcesUpdateDialog_CloseButtonText".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var result = await ShowSafeAsync(dialog);
        if (result == ContentDialogResult.Primary)
        {
            return itemsList.Where(x => x.IsSelected).ToList();
        }
        return null;
    }

    public async Task ShowMoveResultDialogAsync(
        System.Collections.Generic.IEnumerable<XianYuLauncher.Features.VersionManagement.ViewModels.MoveModResult> moveResults,
        string title,
        string instruction)
    {
        var panel = new StackPanel { Spacing = 12, MaxWidth = 600, MinWidth = 420 };

        panel.Children.Add(new TextBlock
        {
            Text = instruction,
            FontSize = 14
        });

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            MaxHeight = 400,
            ItemsSource = moveResults?.ToList() ?? new System.Collections.Generic.List<XianYuLauncher.Features.VersionManagement.ViewModels.MoveModResult>()
        };

        listView.ItemTemplate = Application.Current.Resources["MoveResultDialogItemTemplate"] as DataTemplate;
        panel.Children.Add(listView);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "VersionManagerPage_MoveResultDialog_PrimaryButtonText".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        await ShowSafeAsync(dialog);
    }

    public async Task<SettingsCustomSourceDialogResult?> ShowSettingsCustomSourceDialogAsync(SettingsCustomSourceDialogRequest request)
    {
        var dialog = new ContentDialog
        {
            Title = request.Title,
            PrimaryButtonText = request.PrimaryButtonText,
            CloseButtonText = request.CloseButtonText,
            DefaultButton = ContentDialogButton.Primary,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var stackPanel = new StackPanel { Spacing = 12 };

        var nameBox = new TextBox { Text = request.Name, PlaceholderText = "例如：我的镜像站", Header = "源名称" };
        var urlBox = new TextBox { Text = request.BaseUrl, PlaceholderText = "https://mirror.example.com", Header = "Base URL" };
        var priorityBox = new NumberBox
        {
            Header = "优先级（数值越大优先级越高）",
            Value = request.Priority,
            Minimum = 1,
            Maximum = 1000,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };

        stackPanel.Children.Add(nameBox);
        stackPanel.Children.Add(urlBox);

        ComboBox? templateCombo = null;
        if (request.ShowTemplateSelection)
        {
            templateCombo = new ComboBox
            {
                Header = "模板类型",
                ItemsSource = new[] { "官方资源", "社区资源" },
                SelectedIndex = request.Template == DownloadSourceTemplateType.Official ? 0 : 1
            };
            stackPanel.Children.Add(templateCombo);
        }
        else
        {
            var templateText = new TextBlock
            {
                Text = $"模板类型: {(request.Template == DownloadSourceTemplateType.Official ? "官方资源" : "社区资源")}",
                Opacity = 0.7,
                Margin = new Thickness(0, 8, 0, 0)
            };
            stackPanel.Children.Add(templateText);
        }

        stackPanel.Children.Add(priorityBox);

        ToggleSwitch? enabledSwitch = null;
        if (request.ShowEnabledSwitch)
        {
            enabledSwitch = new ToggleSwitch { Header = "启用", IsOn = request.Enabled };
            stackPanel.Children.Add(enabledSwitch);
        }

        dialog.Content = stackPanel;

        var result = await ShowSafeAsync(dialog);
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        var template = request.Template;
        if (templateCombo != null)
        {
            template = templateCombo.SelectedIndex == 0
                ? DownloadSourceTemplateType.Official
                : DownloadSourceTemplateType.Community;
        }

        return new SettingsCustomSourceDialogResult
        {
            Name = nameBox.Text?.Trim() ?? string.Empty,
            BaseUrl = urlBox.Text?.Trim() ?? string.Empty,
            Template = template,
            Priority = (int)priorityBox.Value,
            Enabled = enabledSwitch?.IsOn ?? request.Enabled
        };
    }
}
