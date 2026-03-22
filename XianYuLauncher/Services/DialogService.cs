using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks; // Explicitly import missing namespace
using System.Threading;
using System.Net.Http;
using Windows.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Storage.Streams;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Helpers;
using Serilog;
using Newtonsoft.Json.Linq;

namespace XianYuLauncher.Services;

/// <summary>
/// 弹窗服务，弹窗主题跟随启动器内主题（而非系统主题）。
/// </summary>
public class DialogService : IDialogService
{
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly ICommonDialogService _commonDialogService;
    private readonly IContentDialogHostService _dialogHost;
    private readonly IDialogThemePaletteService _dialogThemePaletteService;
    private readonly IProgressDialogService _progressDialogService;
    private readonly IUiDispatcher _uiDispatcher;

    public DialogService(
        ICommonDialogService commonDialogService,
        IContentDialogHostService dialogHost,
        IDialogThemePaletteService dialogThemePaletteService,
        IProgressDialogService progressDialogService,
        IUiDispatcher uiDispatcher)
    {
        _commonDialogService = commonDialogService ?? throw new ArgumentNullException(nameof(commonDialogService));
        _dialogHost = dialogHost ?? throw new ArgumentNullException(nameof(dialogHost));
        _dialogThemePaletteService = dialogThemePaletteService ?? throw new ArgumentNullException(nameof(dialogThemePaletteService));
        _progressDialogService = progressDialogService ?? throw new ArgumentNullException(nameof(progressDialogService));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
    }

    private Task<ContentDialogResult> ShowSafeAsync(ContentDialog dialog) => _dialogHost.ShowAsync(dialog);

    private Microsoft.UI.Xaml.Media.Brush GetDialogSecondaryTextBrush() => _dialogThemePaletteService.GetSecondaryTextBrush();

    private Microsoft.UI.Xaml.Media.Brush GetDialogTertiaryTextBrush() => _dialogThemePaletteService.GetTertiaryTextBrush();

    private Microsoft.UI.Xaml.Media.Brush GetDialogCriticalTextBrush() => _dialogThemePaletteService.GetCriticalTextBrush();

    private Microsoft.UI.Xaml.Media.Brush GetDialogCardBackgroundBrush() => _dialogThemePaletteService.GetCardBackgroundBrush();

    private Microsoft.UI.Xaml.Media.Brush GetDialogCardStrokeBrush() => _dialogThemePaletteService.GetCardStrokeBrush();

    private Microsoft.UI.Xaml.Media.Brush GetDialogSubtleFillBrush() => _dialogThemePaletteService.GetSubtleFillBrush();

    private Microsoft.UI.Xaml.Media.Brush GetDialogPrimaryTextBrush() => _dialogThemePaletteService.GetPrimaryTextBrush();

    private Microsoft.UI.Xaml.Media.Brush GetDialogAccentFillBrush() => _dialogThemePaletteService.GetAccentFillBrush();

    private Microsoft.UI.Xaml.Media.Brush GetDialogTextOnAccentBrush() => _dialogThemePaletteService.GetTextOnAccentBrush();

    public Task ShowMessageDialogAsync(string title, string message, string closeButtonText = "确定") => _commonDialogService.ShowMessageDialogAsync(title, message, closeButtonText);

    public Task<bool> ShowConfirmationDialogAsync(
        string title,
        string message,
        string primaryButtonText = "是",
        string closeButtonText = "否",
        ContentDialogButton defaultButton = ContentDialogButton.Primary) =>
        _commonDialogService.ShowConfirmationDialogAsync(title, message, primaryButtonText, closeButtonText, defaultButton);

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

    public Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog) => _commonDialogService.ShowDialogAsync(dialog);

    public Task<ContentDialogResult> ShowCustomDialogAsync(
        string title,
        object content,
        string? primaryButtonText = null,
        string? secondaryButtonText = null,
        string? closeButtonText = null,
        ContentDialogButton defaultButton = ContentDialogButton.None,
        bool isPrimaryButtonEnabled = true,
        bool isSecondaryButtonEnabled = true,
        Windows.Foundation.TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs>? onPrimaryButtonClick = null,
        Windows.Foundation.TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs>? onSecondaryButtonClick = null) =>
        _commonDialogService.ShowCustomDialogAsync(
            title,
            content,
            primaryButtonText,
            secondaryButtonText,
            closeButtonText,
            defaultButton,
            isPrimaryButtonEnabled,
            isSecondaryButtonEnabled,
            onPrimaryButtonClick,
            onSecondaryButtonClick);

    public async Task ShowFavoritesImportResultDialogAsync(IEnumerable<XianYuLauncher.Models.FavoritesImportResultItem> results)
    {
        var resultList = results.ToList();
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "以下资源不支持所选版本：", FontSize = 14 });

        var listView = new ListView
        {
            MaxHeight = 400,
            ItemsSource = resultList,
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = false,
        };
        listView.ContainerContentChanging += (s, args) =>
        {
            if (args.Phase != 0)
                return;
            args.Handled = true;
            if (args.Item is not XianYuLauncher.Models.FavoritesImportResultItem item)
                return;
            var opacity = item.IsGrayedOut ? 0.5 : 1.0;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = item.ItemName,
                VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
                Opacity = opacity,
            });
            row.Children.Add(new TextBlock
            {
                Text = item.StatusText,
                VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
                Opacity = opacity,
                Style = (Microsoft.UI.Xaml.Style)Application.Current.Resources["CaptionTextBlockStyle"],
            });
            args.ItemContainer.Content = row;
        };

        panel.Children.Add(listView);
        await ShowCustomDialogAsync("部分资源不支持此版本", panel, primaryButtonText: "确定", closeButtonText: null);
    }

    public Task<string?> ShowTextInputDialogAsync(
        string title,
        string placeholder = "",
        string primaryButtonText = "确认",
        string closeButtonText = "取消",
        bool acceptsReturn = false) =>
        _commonDialogService.ShowTextInputDialogAsync(title, placeholder, primaryButtonText, closeButtonText, acceptsReturn);

    public async Task<string?> ShowModpackInstallNameDialogAsync(
        string defaultName,
        string? tip = null,
        Func<string, (bool IsValid, string ErrorMessage)>? validateInput = null)
    {
        var primaryTextBrush = GetDialogPrimaryTextBrush();
        var secondaryTextBrush = GetDialogSecondaryTextBrush();
        var criticalTextBrush = GetDialogCriticalTextBrush();

        var inputBox = new TextBox
        {
            Text = defaultName?.Trim() ?? string.Empty,
            PlaceholderText = "ModDownloadDetailPage_ModpackInstallNameDialog_PlaceholderText".GetLocalized(),
            MinWidth = 380,
            Width = 380
        };

        var instructionText = new TextBlock
        {
            Text = "ModDownloadDetailPage_ModpackInstallNameDialog_InstructionText".GetLocalized(),
            FontSize = 14,
            Foreground = primaryTextBrush,
            TextWrapping = TextWrapping.Wrap
        };

        var errorText = new TextBlock
        {
            FontSize = 12,
            Foreground = criticalTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(instructionText);
        content.Children.Add(inputBox);
        content.Children.Add(errorText);

        if (!string.IsNullOrWhiteSpace(tip))
        {
            content.Children.Add(new TextBlock
            {
                Text = tip,
                FontSize = 12,
                Foreground = secondaryTextBrush,
                TextWrapping = TextWrapping.Wrap
            });
        }

        var dialog = new ContentDialog
        {
            Title = "ModDownloadDetailPage_ModpackInstallNameDialog_Title".GetLocalized(),
            Content = content,
            PrimaryButtonText = "ModDownloadDetailPage_ModpackInstallNameDialog_PrimaryButtonText".GetLocalized(),
            CloseButtonText = "ModDownloadDetailPage_ModpackInstallNameDialog_CloseButtonText".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        void UpdateValidationState()
        {
            var value = inputBox.Text ?? string.Empty;
            var validationResult = validateInput?.Invoke(value)
                ?? (!string.IsNullOrWhiteSpace(value), !string.IsNullOrWhiteSpace(value)
                    ? string.Empty
                    : "ModDownloadDetailPage_ModpackInstallNameDialog_Error_Empty".GetLocalized());

            dialog.IsPrimaryButtonEnabled = validationResult.IsValid;
            errorText.Text = validationResult.IsValid ? string.Empty : validationResult.ErrorMessage;
            errorText.Visibility = string.IsNullOrWhiteSpace(errorText.Text) ? Visibility.Collapsed : Visibility.Visible;
        }

        inputBox.TextChanged += (_, _) => UpdateValidationState();
        dialog.Opened += (_, _) =>
        {
            inputBox.Focus(FocusState.Programmatic);
            inputBox.SelectionStart = inputBox.Text.Length;
            UpdateValidationState();
        };

        UpdateValidationState();

        var result = await ShowSafeAsync(dialog);
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return inputBox.Text?.Trim();
    }

    public Task ShowProgressDialogAsync(string title, string message, Func<IProgress<double>, IProgress<string>, CancellationToken, Task> workCallback) =>
        _progressDialogService.ShowProgressDialogAsync(title, message, workCallback);

    public Task<T> ShowProgressCallbackDialogAsync<T>(string title, string message, Func<IProgress<double>, Task<T>> workCallback) =>
        _progressDialogService.ShowProgressCallbackDialogAsync(title, message, workCallback);

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

        // 2. 启动异步加载头像任务（对话框关闭时取消，避免后台继续请求）
        Log.Information("[Avatar.DialogService] 外置角色选择对话框，AuthServer: {AuthServer}, 角色数: {Count}", authServer ?? "(null)", profiles.Count);
        var avatarLoadCts = new CancellationTokenSource();
        _ = Task.Run(async () => 
        {
            if (string.IsNullOrEmpty(authServer))
            {
                Log.Warning("[Avatar.DialogService] AuthServer 为空，跳过头像加载");
                return;
            }
            foreach (var item in items)
            {
                avatarLoadCts.Token.ThrowIfCancellationRequested();
                try
                {
                    var server = authServer;
                    if (!server.EndsWith("/")) server += "/";
                    var sessionUrl = $"{server}sessionserver/session/minecraft/profile/{item.Id}";
                    Log.Information("[Avatar.DialogService] 加载角色 {Name} 头像，Session URL: {Url}", item.Name, sessionUrl);
                    
                    var response = await _httpClient.GetStringAsync(sessionUrl, avatarLoadCts.Token);
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(response);
                    
                    var properties = data?["properties"] as JArray;
                    string? textureProperty = properties
                        ?.OfType<JObject>()
                        .FirstOrDefault(prop => string.Equals(prop["name"]?.ToString(), "textures", StringComparison.Ordinal))?
                        ["value"]?.ToString();
                    if (string.IsNullOrEmpty(textureProperty))
                    {
                        Log.Warning("[Avatar.DialogService] 角色 {Name} Session API 无 textures，URL: {Url}", item.Name, sessionUrl);
                    }

                    if (!string.IsNullOrEmpty(textureProperty))
                    {
                        var jsonBytes = Convert.FromBase64String(textureProperty);
                        var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
                        var textureData = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(json);

                        string? skinUrl = textureData?["textures"]?["SKIN"]?["url"]?.ToString();

                        if (!string.IsNullOrEmpty(skinUrl))
                        {
                            Log.Debug("[Avatar.DialogService] 角色 {Name} 皮肤 URL: {SkinUrl}", item.Name, skinUrl);
                            var skinBytes = await _httpClient.GetByteArrayAsync(skinUrl, avatarLoadCts.Token);
                            
                            _uiDispatcher.EnqueueAsync(async () =>
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
                            }).Observe("DialogService.LoadProfileAvatar");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Debug("[Avatar.DialogService] 头像加载任务已取消");
                    break;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[Avatar.DialogService] 加载角色 {Name} 头像失败，AuthServer: {AuthServer}", item.Name, authServer);
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
        dialog.Closed += (_, _) => avatarLoadCts.Cancel();

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

    private async Task<BitmapImage?> ProcessAvatarBytesAsync(byte[] skinBytes)
    {
        try
        {
            var device = CanvasDevice.GetSharedDevice();
            using (var stream = new MemoryStream(skinBytes))
            {
                var originalBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
                
                return await SkinAvatarHelper.CropHeadFromSkinAsync(originalBitmap, outputSize: 32, includeOverlay: true);
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
        var primaryTextBrush = GetDialogPrimaryTextBrush();
        var secondaryTextBrush = GetDialogSecondaryTextBrush();
        var cardBgBrush = GetDialogCardBackgroundBrush();
        var cardStrokeBrush = GetDialogCardStrokeBrush();
        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = instruction, FontSize = 14, Foreground = primaryTextBrush });

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "ModDownloadDetailPage_DownloadDialog_PrimaryButtonText".GetLocalized(),
            SecondaryButtonText = "ModDownloadDetailPage_DownloadDialog_SecondaryButtonText".GetLocalized(),
            CloseButtonText = "ModDownloadDetailPage_DownloadDialog_CloseButtonText".GetLocalized(),
            DefaultButton = ContentDialogButton.None,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

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
                    textPanel.Children.Add(new TextBlock { Text = depTitle, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = primaryTextBrush });
                    textPanel.Children.Add(new TextBlock { Text = description, FontSize = 12, Foreground = secondaryTextBrush, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, TextWrapping = TextWrapping.WrapWholeWords });
                    cardContent.Children.Add(textPanel);

                    var btn = new Button
                    {
                        Content = cardContent,
                        Background = cardBgBrush,
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(12),
                        BorderThickness = new Thickness(1),
                        BorderBrush = cardStrokeBrush,
                        Width = 380,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    // 覆盖默认 Hover/Pressed 样式，避免从卡片灰闪到白再恢复的 UX 问题
                    var subtleFill = GetDialogSubtleFillBrush();
                    btn.Resources["ButtonBackgroundPointerOver"] = subtleFill;
                    btn.Resources["ButtonBackgroundPressed"] = subtleFill;
                    var capturedId = projectId;
                    btn.Click += (s, e) =>
                    {
                        dialog.Hide();
                        onDependencyClick?.Invoke(capturedId);
                    };
                    depsPanel.Children.Add(btn);
                }
                panel.Children.Add(depsPanel);
            }
        }

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
        var primaryTextBrush = GetDialogPrimaryTextBrush();
        var secondaryTextBrush = GetDialogSecondaryTextBrush();
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = instruction, FontSize = 14, Foreground = primaryTextBrush });

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
            var textBlock = new TextBlock { Text = displayMemberFunc(item), Foreground = primaryTextBrush };
            if (opacityFunc != null)
                textBlock.Opacity = opacityFunc(item);
            grid.Children.Add(textBlock);
            var lvi = new ListViewItem { Content = grid, Tag = item };
            listView.Items.Add(lvi);
        }

        if (listView.Items.Count > 0)
        {
            listView.SelectedIndex = 0;
        }

        panel.Children.Add(listView);

        if (!string.IsNullOrEmpty(tip))
        {
            panel.Children.Add(new TextBlock
            {
                Text = tip,
                FontSize = 12,
                Foreground = secondaryTextBrush
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
        var primaryTextBrush = GetDialogPrimaryTextBrush();
        var secondaryTextBrush = GetDialogSecondaryTextBrush();
        var tertiaryTextBrush = GetDialogTertiaryTextBrush();
        var cardBgBrush = GetDialogCardBackgroundBrush();
        var cardStrokeBrush = GetDialogCardStrokeBrush();
        var subtleFillBrush = GetDialogSubtleFillBrush();
        var accentFillBrush = GetDialogAccentFillBrush();
        var textOnAccentBrush = GetDialogTextOnAccentBrush();
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = instruction, FontSize = 14, TextWrapping = TextWrapping.WrapWholeWords, Foreground = primaryTextBrush });

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 400
        };

        foreach (var item in items)
        {
            var card = new Border
            {
                Background = cardBgBrush,
                BorderBrush = cardStrokeBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 4, 0, 4)
            };

            var cardPanel = new StackPanel { Spacing = 4 };

            // 版本号 + 类型标签行
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            headerRow.Children.Add(new TextBlock { Text = versionNumberFunc(item), FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = primaryTextBrush });

            var typeBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Background = subtleFillBrush
            };
            var vt = versionTypeFunc(item);
            typeBadge.Child = new TextBlock
            {
                Text = string.IsNullOrEmpty(vt) ? vt : char.ToUpper(vt[0]) + vt[1..],
                FontSize = 11,
                Foreground = secondaryTextBrush
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
                        Background = accentFillBrush
                    };
                    resBadge.Child = new TextBlock
                    {
                        Text = tag,
                        FontSize = 11,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = textOnAccentBrush
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
                Foreground = secondaryTextBrush
            });

            // 文件名
            cardPanel.Children.Add(new TextBlock
            {
                Text = fileNameFunc(item),
                FontSize = 11,
                Foreground = tertiaryTextBrush,
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

    public Task<ContentDialogResult> ShowObservableProgressDialogAsync(
        string title,
        Func<string> getStatus,
        Func<double> getProgress,
        Func<string> getProgressText,
        System.ComponentModel.INotifyPropertyChanged propertyChanged,
        string? primaryButtonText = null,
        string? closeButtonText = "取消",
        Task? autoCloseWhen = null,
        Func<string>? getSpeed = null) =>
        _progressDialogService.ShowObservableProgressDialogAsync(
            title,
            getStatus,
            getProgress,
            getProgressText,
            propertyChanged,
            primaryButtonText,
            closeButtonText,
            autoCloseWhen,
            getSpeed);

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

    public Task<string?> ShowRenameDialogAsync(
        string title,
        string currentName,
        string placeholder = "输入新名称",
        string instruction = "请输入新的名称：") =>
        _commonDialogService.ShowRenameDialogAsync(title, currentName, placeholder, instruction);

    public async Task<AddServerDialogResult?> ShowAddServerDialogAsync(string defaultName = "Minecraft Server")
    {
        var stackPanel = new StackPanel { Spacing = 12 };

        var nameInput = new TextBox
        {
            Header = "服务器名称",
            PlaceholderText = "Minecraft Server",
            Text = defaultName
        };

        var addrInput = new TextBox
        {
            Header = "服务器地址",
            PlaceholderText = "例如: 127.0.0.1"
        };

        stackPanel.Children.Add(nameInput);
        stackPanel.Children.Add(addrInput);

        var dialog = new ContentDialog
        {
            Title = "添加服务器",
            PrimaryButtonText = "添加",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = stackPanel,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var result = await ShowSafeAsync(dialog);
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return new AddServerDialogResult
        {
            Name = nameInput.Text?.Trim() ?? string.Empty,
            Address = addrInput.Text?.Trim() ?? string.Empty
        };
    }

    public async Task<LoginMethodSelectionResult> ShowLoginMethodSelectionDialogAsync(
        string title = "选择登录方式",
        string instruction = "请选择您喜欢的登录方式：",
        string browserDescription = "• 浏览器登录：打开系统默认浏览器进行登录 (推荐)",
        string deviceCodeDescription = "• 设备代码登录：获取代码后手动访问网页输入",
        string browserButtonText = "浏览器登录",
        string deviceCodeButtonText = "设备代码登录",
        string cancelButtonText = "取消")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = instruction, Margin = new Thickness(0, 0, 0, 10) },
                    new TextBlock { Text = browserDescription, Opacity = 0.8, FontSize = 12 },
                    new TextBlock { Text = deviceCodeDescription, Opacity = 0.8, FontSize = 12 }
                }
            },
            PrimaryButtonText = browserButtonText,
            SecondaryButtonText = deviceCodeButtonText,
            CloseButtonText = cancelButtonText,
            DefaultButton = ContentDialogButton.Primary,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var result = await ShowSafeAsync(dialog);
        return result switch
        {
            ContentDialogResult.Primary => LoginMethodSelectionResult.Browser,
            ContentDialogResult.Secondary => LoginMethodSelectionResult.DeviceCode,
            _ => LoginMethodSelectionResult.Cancel
        };
    }

    public async Task ShowPublishersListDialogAsync(
        IEnumerable<PublisherDialogItem> publishers,
        bool isLoading,
        string title = "所有发布者",
        string closeButtonText = "关闭")
    {
        var stackPanel = new StackPanel { Spacing = 12, Width = 420, MaxHeight = 500 };
        var secondaryTextBrush = GetDialogSecondaryTextBrush();

        if (isLoading)
        {
            stackPanel.Children.Add(new ProgressRing
            {
                IsActive = true,
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }
        else
        {
            var listView = new ListView
            {
                SelectionMode = ListViewSelectionMode.None,
                MaxHeight = 420,
                IsItemClickEnabled = false
            };

            foreach (var publisher in publishers)
            {
                var rowGrid = new Grid { Padding = new Thickness(8) };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var avatarContainer = new Grid { Margin = new Thickness(0, 0, 12, 0) };
                Grid.SetColumn(avatarContainer, 0);

                var hasAvatar = !string.IsNullOrWhiteSpace(publisher.AvatarUrl) &&
                                !publisher.AvatarUrl.Contains("Placeholder", StringComparison.OrdinalIgnoreCase);

                if (hasAvatar)
                {
                    var avatarBorder = new Border
                    {
                        Width = 40,
                        Height = 40,
                        CornerRadius = new CornerRadius(20)
                    };

                    try
                    {
                        avatarBorder.Background = new Microsoft.UI.Xaml.Media.ImageBrush
                        {
                            ImageSource = new BitmapImage(new Uri(publisher.AvatarUrl, UriKind.RelativeOrAbsolute)),
                            Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
                        };
                    }
                    catch
                    {
                        hasAvatar = false;
                    }

                    if (hasAvatar)
                    {
                        avatarContainer.Children.Add(avatarBorder);
                    }
                }

                if (!hasAvatar)
                {
                    var placeholder = new Border
                    {
                        Width = 40,
                        Height = 40,
                        CornerRadius = new CornerRadius(20),
                        Background = GetDialogCardBackgroundBrush()
                    };
                    placeholder.Child = new FontIcon
                    {
                        Glyph = "\uE77B",
                        FontSize = 20,
                        FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                        Foreground = secondaryTextBrush
                    };
                    avatarContainer.Children.Add(placeholder);
                }

                var textPanel = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(textPanel, 1);
                textPanel.Children.Add(new TextBlock { Text = publisher.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = GetDialogPrimaryTextBrush() });
                textPanel.Children.Add(new TextBlock
                {
                    Text = publisher.Role,
                    FontSize = 12,
                    Foreground = secondaryTextBrush
                });

                rowGrid.Children.Add(avatarContainer);
                rowGrid.Children.Add(textPanel);
                listView.Items.Add(rowGrid);
            }

            stackPanel.Children.Add(listView);
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = stackPanel,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.None,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        await ShowSafeAsync(dialog);
    }

    public async Task<CrashReportDialogAction> ShowCrashReportDialogAsync(
        string crashTitle,
        string crashAnalysis,
        string fullLog,
        bool isEasterEggMode)
    {
        var errorRedColor = Windows.UI.Color.FromArgb(255, 196, 43, 28);
        var errorBgColor = Windows.UI.Color.FromArgb(30, 232, 17, 35);

        var warningPanel = new StackPanel
        {
            Spacing = 20,
            Margin = new Thickness(0)
        };

        var warningCard = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(errorBgColor),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(errorRedColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 16, 20, 16)
        };

        var warningCardContent = new StackPanel { Spacing = 12 };
        var headerStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var warningIcon = new FontIcon
        {
            Glyph = "\uE7BA",
            FontSize = 24,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(errorRedColor)
        };

        var titleText = string.IsNullOrWhiteSpace(crashTitle)
            ? "游戏意外退出"
            : $"游戏意外退出：{crashTitle}";

        var warningTitle = new TextBlock
        {
            Text = titleText,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(errorRedColor),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        headerStack.Children.Add(warningIcon);
        headerStack.Children.Add(warningTitle);
        warningCardContent.Children.Add(headerStack);

        var hintTextBrush = GetDialogPrimaryTextBrush();
        var hintText = new TextBlock
        {
            Text = "为了快速解决问题，请导出完整的崩溃日志，而不是截图。",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Foreground = hintTextBrush
        };

        if (isEasterEggMode)
        {
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleXAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 5.15,
                Duration = new Duration(TimeSpan.FromMilliseconds(500)),
                AutoReverse = true,
                RepeatBehavior = Microsoft.UI.Xaml.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.SineEase
                {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut
                }
            };

            hintText.RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform();
            hintText.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleXAnimation, hintText);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleXAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");

            var scaleYAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 5.15,
                Duration = new Duration(TimeSpan.FromMilliseconds(500)),
                AutoReverse = true,
                RepeatBehavior = Microsoft.UI.Xaml.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.SineEase
                {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut
                }
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleYAnimation, hintText);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleYAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");

            scaleAnimation.Children.Add(scaleXAnimation);
            scaleAnimation.Children.Add(scaleYAnimation);
            hintText.Loaded += (s, e) => scaleAnimation.Begin();
        }

        warningCardContent.Children.Add(hintText);
        warningCard.Child = warningCardContent;
        warningPanel.Children.Add(warningCard);

        var instructionCard = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 16, 20, 16)
        };

        var cardBgBrush = GetDialogCardBackgroundBrush();
        var cardStrokeBrush = GetDialogCardStrokeBrush();
        var primaryTextBrush = GetDialogPrimaryTextBrush();
        var secondaryTextBrush = GetDialogSecondaryTextBrush();
        var tertiaryTextBrush = GetDialogTertiaryTextBrush();

        instructionCard.Background = cardBgBrush;
        instructionCard.BorderBrush = cardStrokeBrush;
        instructionCard.BorderThickness = new Thickness(1);

        var instructionStack = new StackPanel { Spacing = 10 };
        instructionStack.Children.Add(new TextBlock
        {
            Text = "正确的求助步骤",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = primaryTextBrush
        });

        instructionStack.Children.Add(new TextBlock { Text = "1. 点击下方「导出崩溃日志」按钮", FontSize = 14, TextWrapping = TextWrapping.Wrap, Foreground = secondaryTextBrush });
        instructionStack.Children.Add(new TextBlock { Text = "2. 将导出的 ZIP 文件发送给技术支持", FontSize = 14, TextWrapping = TextWrapping.Wrap, Foreground = secondaryTextBrush });
        instructionStack.Children.Add(new TextBlock
        {
            Text = "日志文件包含启动器日志、游戏日志等信息，能帮助快速定位问题",
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = tertiaryTextBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });

        instructionCard.Child = instructionStack;
        warningPanel.Children.Add(instructionCard);

        var logExpander = new Expander
        {
            Header = "查看日志预览",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };

        var logPreviewText = new TextBlock
        {
            Text = fullLog,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = tertiaryTextBrush
        };

        var logScroller = new ScrollViewer
        {
            Content = logPreviewText,
            MaxHeight = 200,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 8, 0, 0)
        };

        logExpander.Content = logScroller;
        warningPanel.Children.Add(logExpander);

        var dialog = new ContentDialog
        {
            Title = "游戏崩溃",
            Content = warningPanel,
            PrimaryButtonText = "导出崩溃日志",
            SecondaryButtonText = "查看详细日志",
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Primary,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        CancellationTokenSource? shakeTokenSource = null;
        if (isEasterEggMode)
        {
            shakeTokenSource = new CancellationTokenSource();
            var shakeToken = shakeTokenSource.Token;

            _ = Task.Run(async () =>
            {
                var random = new Random();
                var originalPosition = new Windows.Graphics.PointInt32();
                var gotOriginalPosition = false;

                while (!shakeToken.IsCancellationRequested)
                {
                    try
                    {
                        _uiDispatcher.TryEnqueue(() =>
                        {
                            try
                            {
                                var appWindow = App.MainWindow.AppWindow;
                                if (!gotOriginalPosition)
                                {
                                    originalPosition = appWindow.Position;
                                    gotOriginalPosition = true;
                                }

                                var offsetX = random.Next(-15, 6);
                                var offsetY = random.Next(-15, 6);
                                appWindow.Move(new Windows.Graphics.PointInt32(originalPosition.X + offsetX, originalPosition.Y + offsetY));
                            }
                            catch { }
                        });

                        await Task.Delay(50, shakeToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                if (gotOriginalPosition)
                {
                    _uiDispatcher.TryEnqueue(() =>
                    {
                        try { App.MainWindow.AppWindow.Move(originalPosition); } catch { }
                    });
                }
            }, shakeToken);
        }

        dialog.Closed += (s, e) => shakeTokenSource?.Cancel();

        var result = await ShowSafeAsync(dialog);
        return result switch
        {
            ContentDialogResult.Primary => CrashReportDialogAction.ExportLogs,
            ContentDialogResult.Secondary => CrashReportDialogAction.ViewDetails,
            _ => CrashReportDialogAction.Close
        };
    }

    public async Task<SkinModelSelectionResult> ShowSkinModelSelectionDialogAsync(
        string title = "选择皮肤模型",
        string content = "请选择此皮肤适用的人物模型",
        string steveButtonText = "Steve",
        string alexButtonText = "Alex",
        string cancelButtonText = "取消")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = steveButtonText,
            SecondaryButtonText = alexButtonText,
            CloseButtonText = cancelButtonText,
            DefaultButton = ContentDialogButton.None,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var result = await ShowSafeAsync(dialog);
        return result switch
        {
            ContentDialogResult.Primary => SkinModelSelectionResult.Steve,
            ContentDialogResult.Secondary => SkinModelSelectionResult.Alex,
            _ => SkinModelSelectionResult.Cancel
        };
    }

    public async Task<ContentDialogResult> ShowPrivacyAgreementDialogAsync(
        string title,
        string agreementContent,
        Func<Task>? onOpenAgreementLink = null,
        string primaryButtonText = "同意",
        string secondaryButtonText = "用户协议",
        string closeButtonText = "拒绝")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = agreementContent,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12),
                    FontSize = 14
                },
                MaxHeight = 400,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            },
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = secondaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        dialog.SecondaryButtonClick += async (sender, args) =>
        {
            if (onOpenAgreementLink != null)
            {
                args.Cancel = true;
                await onOpenAgreementLink();
            }
        };

        return await ShowSafeAsync(dialog);
    }
}
