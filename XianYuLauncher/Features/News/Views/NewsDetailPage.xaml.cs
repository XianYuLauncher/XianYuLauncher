using System.Numerics;
using System.Text.RegularExpressions;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Features.News.Models;
using XianYuLauncher.Features.News.ViewModels;
using XianYuLauncher.Services;

namespace XianYuLauncher.Features.News.Views;

public sealed partial class NewsDetailPage : Page, IHostedLocalPage
{
    private EventHandler? _closeRequested;

    public NewsDetailViewModel ViewModel { get; } = App.GetService<NewsDetailViewModel>();

    public IPageHeaderAware HeaderSource => ViewModel;

    private readonly IUiDispatcher _uiDispatcher;

    // 当前页面本身不会主动请求关闭；保留标准事件实现以满足接口契约并允许外部正常订阅。
    // 如后续新增关闭行为，请在对应时机触发该事件。
    public event EventHandler? CloseRequested
    {
        add => _closeRequested += value;
        remove => _closeRequested -= value;
    }

    public NewsDetailPage()
    {
        _uiDispatcher = App.GetService<IUiDispatcher>();
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (TryNormalizeNavigationParameter(e.Parameter, out var navigationParameter))
        {
            ViewModel.Initialize(navigationParameter);
            UpdateUI();
        }
    }

    public void ResetEmbeddedVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        ContentArea.Scale = new Vector3(1f, 1f, 1f);
    }

    private void UpdateUI()
    {
        TitleTextBlock.Text = ViewModel.Title;
        TypeTextBlock.Text = ViewModel.TypeDisplay;
        VersionTextBlock.Text = ViewModel.Version;
        DateTextBlock.Text = ViewModel.Date;
        ShortTextBlock.Text = ViewModel.ShortText;
        ContentTextBlock.Text = string.Empty;
        ContentBorder.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = ViewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;

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
            return;
        }

        NewsImage.Source = null;
        NewsImageBg.Source = null;
        ImageBorder.Visibility = Visibility.Collapsed;
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

    private static bool TryNormalizeNavigationParameter(object? parameter, out NewsDetailNavigationParameter navigationParameter)
    {
        if (parameter is NewsDetailNavigationParameter typedNavigationParameter)
        {
            navigationParameter = typedNavigationParameter;
            return true;
        }

        if (parameter is MinecraftNewsEntry entry)
        {
            navigationParameter = new NewsDetailNavigationParameter
            {
                Entry = entry,
            };
            return true;
        }

        navigationParameter = null!;
        return false;
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