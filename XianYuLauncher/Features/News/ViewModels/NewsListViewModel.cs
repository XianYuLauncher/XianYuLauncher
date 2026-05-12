using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.ApplicationModel.Resources;

using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Features.News.Models;
using XianYuLauncher.Services;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.News.ViewModels;

public partial class NewsListViewModel : ObservableRecipient, IPageHeaderAware
{
    private const string SourceAll = "All";
    private const string SourceJavaPatchNote = "JavaPatchNote";
    private const string SourceNews = "News";
    private const string FilterRelease = "Release";
    private const string FilterSnapshot = "Snapshot";
    private const string FilterWindows = "Windows";
    private const string FilterJavaEdition = "JavaEdition";
    private const string FilterAll = "All";

    private readonly IFileService _fileService;
    private readonly ResourceLoader _resourceLoader;
    private MinecraftNewsService? _newsService;
    private BreadcrumbNavigationRoot _globalBreadcrumbRoot = BreadcrumbNavigationRoot.Empty;
    private PageHeaderPresentationMode _headerPresentationMode = PageHeaderPresentationMode.Standard;

    public event EventHandler<NewsDetailNavigationParameter>? NewsDetailRequested;

    public PageHeaderMetadata HeaderMetadata { get; } = new();

    public PageHeaderPresentationMode HeaderPresentationMode => _headerPresentationMode;

    public bool HasGlobalBreadcrumbRoot => _globalBreadcrumbRoot.HasLabel
        && _globalBreadcrumbRoot.HasGlobalNavigationTarget;

    public BreadcrumbNavigationRoot GlobalBreadcrumbRoot => _globalBreadcrumbRoot;

    public string GlobalBreadcrumbRootLabel => _globalBreadcrumbRoot.Label;

    public string? GlobalBreadcrumbRootPageKey => _globalBreadcrumbRoot.PageKey;

    public object? GlobalBreadcrumbRootNavigationParameter => _globalBreadcrumbRoot.NavigationParameter;

    [ObservableProperty]
    private ObservableCollection<MinecraftNewsEntry> _newsItems = new();

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = FilterAll;

    [ObservableProperty]
    private string _selectedSourceFilter = SourceAll;

    [ObservableProperty]
    private bool _isNewsTeachingTipOpen;

    [ObservableProperty]
    private string _newsTeachingTipTitle = string.Empty;

    [ObservableProperty]
    private string _newsTeachingTipSummary = string.Empty;

    [ObservableProperty]
    private string _newsTeachingTipImageUrl = string.Empty;

    [ObservableProperty]
    private ImageSource? _newsTeachingTipImageSource;

    [ObservableProperty]
    private string _newsTeachingTipLinkUrl = string.Empty;

    [ObservableProperty]
    private bool _isNewsTeachingTipImageVisible;

    public NewsListViewModel()
    {
        _fileService = App.GetService<IFileService>();
        _resourceLoader = ResourceLoader.GetForViewIndependentUse();
        ApplyNavigationContext(null);
    }

    public void ApplyNavigationContext(NewsListNavigationParameter? navigationParameter)
    {
        _globalBreadcrumbRoot = navigationParameter?.BreadcrumbRoot ?? BreadcrumbNavigationRoot.Empty;

        HeaderMetadata.Title = GetResourceString("NewsListPage_Title.Text", "新闻与公告");
        HeaderMetadata.Subtitle = GetResourceString("NewsListPage_Subtitle.Text", "Minecraft Java 版更新动态");
        HeaderMetadata.BreadcrumbItems.Clear();

        if (navigationParameter?.HasBreadcrumbRoot == true)
        {
            _headerPresentationMode = PageHeaderPresentationMode.ProminentBreadcrumb;
            HeaderMetadata.ShowBreadcrumb = true;
            HeaderMetadata.BreadcrumbItems.Add(navigationParameter.BreadcrumbRoot.ToBreadcrumbItem());
            HeaderMetadata.BreadcrumbItems.Add(new NavigationBreadcrumbItem
            {
                DisplayText = HeaderMetadata.Title,
                IsCurrent = true,
            });
            return;
        }

        _headerPresentationMode = PageHeaderPresentationMode.Standard;
        HeaderMetadata.ShowBreadcrumb = false;
    }

    public NewsDetailNavigationParameter CreateDetailNavigationParameter(MinecraftNewsEntry entry)
    {
        return new NewsDetailNavigationParameter
        {
            Entry = entry,
            GlobalBreadcrumbRoot = _globalBreadcrumbRoot,
            BreadcrumbRoot = BreadcrumbNavigationRoot.CreateLocal(
                HeaderMetadata.Title,
                new LocalNavigationTarget
                {
                    RouteKey = NewsNavigationRouteKeys.Root,
                }),
        };
    }

    public async Task LoadNewsAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            _newsService ??= new MinecraftNewsService(_fileService);
            var newsData = await _newsService.GetLatestNewsAsync();

            NewsItems.Clear();

            if (newsData?.Entries != null)
            {
                var filteredEntries = newsData.Entries
                    .Where(IsSourceFilterMatch)
                    .Where(IsDetailFilterMatch)
                    .OrderByDescending(entry => entry.Date);

                foreach (var entry in filteredEntries)
                {
                    NewsItems.Add(entry);
                }
            }

            if (NewsItems.Count == 0)
            {
                ErrorMessage = "暂无新闻";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _newsService ??= new MinecraftNewsService(_fileService);
        await _newsService.GetLatestNewsAsync(forceRefresh: true);
        await LoadNewsAsync();
    }

    public async Task ApplyFiltersAsync(string sourceFilter, string detailFilter)
    {
        var normalizedSource = string.IsNullOrWhiteSpace(sourceFilter) ? SourceAll : sourceFilter;
        var normalizedDetail = string.IsNullOrWhiteSpace(detailFilter)
            ? (normalizedSource == SourceAll ? FilterAll : FilterRelease)
            : detailFilter;

        var isSourceChanged = !string.Equals(SelectedSourceFilter, normalizedSource, StringComparison.OrdinalIgnoreCase);
        var isDetailChanged = !string.Equals(SelectedFilter, normalizedDetail, StringComparison.OrdinalIgnoreCase);

        if (!isSourceChanged && !isDetailChanged)
        {
            if (NewsItems.Count == 0)
            {
                await LoadNewsAsync();
            }

            return;
        }

        SelectedSourceFilter = normalizedSource;
        SelectedFilter = normalizedDetail;
        await LoadNewsAsync();
    }

    [RelayCommand]
    private void OpenNewsDetail(MinecraftNewsEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        var action = NewsClickRouter.Resolve(entry);
        if (action.Type == NewsClickActionType.NavigateDetail)
        {
            NewsDetailRequested?.Invoke(this, CreateDetailNavigationParameter(entry));
            return;
        }

        if (action.Type == NewsClickActionType.ShowActivityTip)
        {
            var tipImageUrl = entry.NewsPageImage?.Url
                ?? entry.PlayPageImage?.Url
                ?? entry.Image?.Url
                ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(tipImageUrl) && tipImageUrl.StartsWith("/"))
            {
                tipImageUrl = $"https://launchercontent.mojang.com{tipImageUrl}";
            }

            NewsTeachingTipTitle = entry.Title;
            NewsTeachingTipSummary = string.IsNullOrWhiteSpace(entry.ShortText)
                ? entry.Category
                : entry.ShortText;
            NewsTeachingTipImageUrl = tipImageUrl;
            NewsTeachingTipImageSource = CreateNewsImageSource(tipImageUrl);
            NewsTeachingTipLinkUrl = entry.ReadMoreLink;
            IsNewsTeachingTipImageVisible = NewsTeachingTipImageSource != null;
            IsNewsTeachingTipOpen = true;
        }
    }

    private static ImageSource? CreateNewsImageSource(string? tipImageUrl)
    {
        if (string.IsNullOrWhiteSpace(tipImageUrl) ||
            !Uri.TryCreate(tipImageUrl, UriKind.Absolute, out var imageUri))
        {
            return null;
        }

        try
        {
            return new BitmapImage(imageUri);
        }
        catch
        {
            return null;
        }
    }

    [RelayCommand]
    private async Task OpenNewsTeachingTipLinkAsync()
    {
        if (!string.IsNullOrWhiteSpace(NewsTeachingTipLinkUrl) &&
            Uri.TryCreate(NewsTeachingTipLinkUrl, UriKind.Absolute, out var uri))
        {
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
    }

    [RelayCommand]
    private void CloseNewsTeachingTip()
    {
        IsNewsTeachingTipOpen = false;
    }

    private bool IsSourceFilterMatch(MinecraftNewsEntry entry)
    {
        return SelectedSourceFilter switch
        {
            SourceNews => entry.SourceKind == MinecraftNewsSourceKind.NewsFeed,
            SourceAll => true,
            _ => entry.SourceKind == MinecraftNewsSourceKind.JavaPatchNotes
        };
    }

    private bool IsDetailFilterMatch(MinecraftNewsEntry entry)
    {
        if (SelectedSourceFilter == SourceAll)
        {
            return true;
        }

        if (SelectedSourceFilter == SourceNews)
        {
            return SelectedFilter switch
            {
                FilterWindows => IsNewsCategory(entry, "Minecraft for Windows"),
                FilterJavaEdition => IsNewsCategory(entry, "Minecraft: Java Edition"),
                _ => true
            };
        }

        return SelectedFilter switch
        {
            FilterRelease => string.Equals(entry.Type, "release", StringComparison.OrdinalIgnoreCase),
            FilterSnapshot => string.Equals(entry.Type, "snapshot", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static bool IsNewsCategory(MinecraftNewsEntry entry, string expectedCategory)
    {
        return string.Equals(entry.Version, expectedCategory, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(entry.Category, expectedCategory, StringComparison.OrdinalIgnoreCase);
    }

    private string GetResourceString(string key, string fallback)
    {
        var value = _resourceLoader.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}