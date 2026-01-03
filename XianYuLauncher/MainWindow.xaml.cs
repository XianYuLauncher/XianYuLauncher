using Windows.UI.ViewManagement;

using XianYuLauncher.Core.Services;
using XianYuLauncher.Helpers;

namespace XianYuLauncher;

public sealed partial class MainWindow : WindowEx
{
    private Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue;

    private UISettings settings;

    public MainWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));
        Content = null;
        Title = "XianYu Launcher";

        // Theme change code picked from https://github.com/microsoft/WinUI-Gallery/pull/1239
        dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        settings = new UISettings();
        settings.ColorValuesChanged += Settings_ColorValuesChanged; // cannot use FrameworkElement.ActualThemeChanged event

        // 应用材质设置
        ApplyMaterialSettings();
    }

    /// <summary>
    /// 应用材质设置
    /// </summary>
    private async void ApplyMaterialSettings()
    {
        try
        {
            var materialService = App.GetService<MaterialService>();
            await materialService.LoadAndApplyMaterialAsync(this);
            
            // 同时加载字体设置
            await LoadFontSettingsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"应用材质设置失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 加载并应用字体设置
    /// </summary>
    private async Task LoadFontSettingsAsync()
    {
        try
        {
            var localSettingsService = App.GetService<XianYuLauncher.Contracts.Services.ILocalSettingsService>();
            const string FontFamilyKey = "FontFamily";
            
            // 加载保存的字体设置
            var fontFamily = await localSettingsService.ReadSettingAsync<string>(FontFamilyKey);
            
            // 应用字体到整个应用程序
            ApplyFontToApplication(fontFamily);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载字体设置失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 应用字体到整个应用程序
    /// </summary>
    private void ApplyFontToApplication(string fontFamilyName)
    {
        try
        {
            // 只在Content加载完成后应用字体，避免干扰XAML解析器
            // 这样可以确保XAML解析错误能正常报告，而不会被字体修改掩盖
            if (Content is Microsoft.UI.Xaml.Controls.Control rootControl)
            {
                // 创建FontFamily对象或使用null（默认字体）
                Microsoft.UI.Xaml.Media.FontFamily fontFamily = null;
                if (!string.IsNullOrEmpty(fontFamilyName) && fontFamilyName != "默认")
                {
                    fontFamily = new Microsoft.UI.Xaml.Media.FontFamily(fontFamilyName);
                }
                
                // 设置主窗口内容的字体
                rootControl.FontFamily = fontFamily;
                
                // 遍历视觉树，强制所有子控件应用字体
                ApplyFontToVisualTree(rootControl, fontFamily);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"应用全局字体失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 设置所有字体相关的资源
    /// </summary>
    private void SetFontFamilyResource(Microsoft.UI.Xaml.Media.FontFamily fontFamily)
    {
        if (fontFamily != null)
        {
            // 设置所有可能的全局FontFamily资源
            App.Current.Resources["ContentControlThemeFontFamily"] = fontFamily;
            App.Current.Resources["ControlContentThemeFontFamily"] = fontFamily;
            App.Current.Resources["TextControlThemeFontFamily"] = fontFamily;
            App.Current.Resources["BodyFontFamily"] = fontFamily;
            App.Current.Resources["CaptionFontFamily"] = fontFamily;
            App.Current.Resources["TitleFontFamily"] = fontFamily;
            App.Current.Resources["SubtitleFontFamily"] = fontFamily;
            App.Current.Resources["HeaderFontFamily"] = fontFamily;
            App.Current.Resources["SubheaderFontFamily"] = fontFamily;
            App.Current.Resources["TitleLargeFontFamily"] = fontFamily;
            App.Current.Resources["TitleMediumFontFamily"] = fontFamily;
            App.Current.Resources["TitleSmallFontFamily"] = fontFamily;
            App.Current.Resources["BodyLargeFontFamily"] = fontFamily;
            App.Current.Resources["BodyMediumFontFamily"] = fontFamily;
            App.Current.Resources["BodySmallFontFamily"] = fontFamily;
            App.Current.Resources["CaptionLargeFontFamily"] = fontFamily;
            App.Current.Resources["CaptionSmallFontFamily"] = fontFamily;
            App.Current.Resources["HeaderLargeFontFamily"] = fontFamily;
            App.Current.Resources["HeaderMediumFontFamily"] = fontFamily;
            App.Current.Resources["HeaderSmallFontFamily"] = fontFamily;
            App.Current.Resources["SubheaderLargeFontFamily"] = fontFamily;
            App.Current.Resources["SubheaderMediumFontFamily"] = fontFamily;
            App.Current.Resources["SubheaderSmallFontFamily"] = fontFamily;
            App.Current.Resources["SubtitleLargeFontFamily"] = fontFamily;
            App.Current.Resources["SubtitleMediumFontFamily"] = fontFamily;
            App.Current.Resources["SubtitleSmallFontFamily"] = fontFamily;
            
            // 也设置默认的TextBlockStyle和其他基础样式
            UpdateTextBlockStyles(fontFamily);
        }
        else
        {
            // 恢复默认字体，移除自定义资源
            App.Current.Resources.Remove("ContentControlThemeFontFamily");
            App.Current.Resources.Remove("ControlContentThemeFontFamily");
            App.Current.Resources.Remove("TextControlThemeFontFamily");
            App.Current.Resources.Remove("BodyFontFamily");
            App.Current.Resources.Remove("CaptionFontFamily");
            App.Current.Resources.Remove("TitleFontFamily");
            App.Current.Resources.Remove("SubtitleFontFamily");
            App.Current.Resources.Remove("HeaderFontFamily");
            App.Current.Resources.Remove("SubheaderFontFamily");
            App.Current.Resources.Remove("TitleLargeFontFamily");
            App.Current.Resources.Remove("TitleMediumFontFamily");
            App.Current.Resources.Remove("TitleSmallFontFamily");
            App.Current.Resources.Remove("BodyLargeFontFamily");
            App.Current.Resources.Remove("BodyMediumFontFamily");
            App.Current.Resources.Remove("BodySmallFontFamily");
            App.Current.Resources.Remove("CaptionLargeFontFamily");
            App.Current.Resources.Remove("CaptionSmallFontFamily");
            App.Current.Resources.Remove("HeaderLargeFontFamily");
            App.Current.Resources.Remove("HeaderMediumFontFamily");
            App.Current.Resources.Remove("HeaderSmallFontFamily");
            App.Current.Resources.Remove("SubheaderLargeFontFamily");
            App.Current.Resources.Remove("SubheaderMediumFontFamily");
            App.Current.Resources.Remove("SubheaderSmallFontFamily");
            App.Current.Resources.Remove("SubtitleLargeFontFamily");
            App.Current.Resources.Remove("SubtitleMediumFontFamily");
            App.Current.Resources.Remove("SubtitleSmallFontFamily");
        }
    }
    
    /// <summary>
    /// 更新TextBlock相关样式
    /// </summary>
    private void UpdateTextBlockStyles(Microsoft.UI.Xaml.Media.FontFamily fontFamily)
    {
        // 更新默认的TextBlockStyle
        var textBlockStyle = new Microsoft.UI.Xaml.Style(typeof(Microsoft.UI.Xaml.Controls.TextBlock));
        textBlockStyle.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.Controls.TextBlock.FontFamilyProperty, fontFamily));
        App.Current.Resources["TextBlockStyle"] = textBlockStyle;
        
        // 更新CaptionTextBlockStyle（用于灰色小字）
        var captionStyle = new Microsoft.UI.Xaml.Style(typeof(Microsoft.UI.Xaml.Controls.TextBlock));
        captionStyle.BasedOn = App.Current.Resources["CaptionTextBlockStyle"] as Microsoft.UI.Xaml.Style;
        captionStyle.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.Controls.TextBlock.FontFamilyProperty, fontFamily));
        App.Current.Resources["CaptionTextBlockStyle"] = captionStyle;
        
        // 更新BodyTextBlockStyle
        var bodyStyle = new Microsoft.UI.Xaml.Style(typeof(Microsoft.UI.Xaml.Controls.TextBlock));
        bodyStyle.BasedOn = App.Current.Resources["BodyTextBlockStyle"] as Microsoft.UI.Xaml.Style;
        bodyStyle.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.Controls.TextBlock.FontFamilyProperty, fontFamily));
        App.Current.Resources["BodyTextBlockStyle"] = bodyStyle;
    }
    
    /// <summary>
    /// 遍历视觉树，将字体应用到所有控件
    /// </summary>
    private void ApplyFontToVisualTree(Microsoft.UI.Xaml.DependencyObject root, Microsoft.UI.Xaml.Media.FontFamily fontFamily)
    {
        // 应用到当前元素（如果是Control类型）
        if (root is Microsoft.UI.Xaml.Controls.Control control)
        {
            control.FontFamily = fontFamily;
        }
        
        // 递归应用到所有子元素
        int childrenCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            ApplyFontToVisualTree(child, fontFamily);
        }
    }

    // this handles updating the caption button colors correctly when indows system theme is changed
    // while the app is open
    private void Settings_ColorValuesChanged(UISettings sender, object args)
    {
        // This calls comes off-thread, hence we will need to dispatch it to current app's thread
        dispatcherQueue.TryEnqueue(() =>
        {
            TitleBarHelper.ApplySystemThemeToCaptionButtons();
        });
    }
}
