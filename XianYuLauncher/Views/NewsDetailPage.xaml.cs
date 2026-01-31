using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Services;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class NewsDetailPage : Page
{
    public NewsDetailViewModel ViewModel { get; } = new NewsDetailViewModel();

    public NewsDetailPage()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        if (e.Parameter is MinecraftNewsEntry entry)
        {
            ViewModel.Initialize(entry);
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        TitleTextBlock.Text = ViewModel.Title;
        TypeTextBlock.Text = ViewModel.TypeDisplay;
        VersionTextBlock.Text = ViewModel.Version;
        DateTextBlock.Text = ViewModel.Date;
        ShortTextBlock.Text = ViewModel.ShortText;

        if (!string.IsNullOrEmpty(ViewModel.ImageUrl))
        {
            var bitmap = new BitmapImage(new System.Uri(ViewModel.ImageUrl));
            
            // 重置模糊层状态
            BlurBorder.Visibility = Visibility.Collapsed;
            
            // 当图片加载完成后，显示模糊层，确保BackdropBlurBrush能采集到正确的像素
            bitmap.ImageOpened += (s, e) => 
            {
                // 确保在 UI 线程执行
                if (DispatcherQueue.HasThreadAccess)
                {
                    BlurBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() => BlurBorder.Visibility = Visibility.Visible);
                }
            };

            NewsImage.Source = bitmap;
            NewsImageBg.Source = bitmap;
            ImageBorder.Visibility = Visibility.Visible;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.ContentHtml))
        {
            var text = StripHtml(ViewModel.ContentHtml);
            if (!string.IsNullOrEmpty(text))
            {
                ContentTextBlock.Text = text;
                ContentBorder.Visibility = Visibility.Visible;
            }
        }
        else if (e.PropertyName == nameof(ViewModel.IsLoading))
        {
            LoadingPanel.Visibility = ViewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        if (navigationService.CanGoBack)
        {
            navigationService.GoBack();
        }
    }

    /// <summary>
    /// 简单的 HTML 标签移除
    /// </summary>
    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        
        // 移除 HTML 标签
        var text = Regex.Replace(html, "<[^>]+>", " ");
        // 解码常见 HTML 实体
        text = text.Replace("&nbsp;", " ")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'");
        // 合并多余空白
        text = Regex.Replace(text, @"\s+", " ").Trim();
        
        return text;
    }
}
