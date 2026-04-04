using System.Text.RegularExpressions;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.News.ViewModels;
using XianYuLauncher.Services;

namespace XianYuLauncher.Features.News.Views;

public sealed partial class NewsDetailPage : Page
{
    public NewsDetailViewModel ViewModel { get; } = new();
    private readonly IUiDispatcher _uiDispatcher;

    public NewsDetailPage()
    {
        _uiDispatcher = App.GetService<IUiDispatcher>();
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

            BlurBorder.Visibility = Visibility.Collapsed;

            bitmap.ImageOpened += (s, e) =>
            {
                if (_uiDispatcher.HasThreadAccess)
                {
                    BlurBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    _uiDispatcher.TryEnqueue(() => BlurBorder.Visibility = Visibility.Visible);
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

    private void BackButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState(BackAnimatedIcon, "PointerOver");
    }

    private void BackButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState(BackAnimatedIcon, "Normal");
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = text.Replace("&nbsp;", " ")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }
}