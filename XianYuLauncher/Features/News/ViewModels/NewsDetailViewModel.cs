using System;
using System.Net.Http;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

using Newtonsoft.Json;

using Windows.ApplicationModel.Resources;

using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Features.News.Models;
using XianYuLauncher.Services;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.News.ViewModels;

public partial class NewsDetailViewModel : ObservableRecipient, IPageHeaderAware
{
    private readonly HttpClient _httpClient;
    private readonly ResourceLoader _resourceLoader;
    private NewsDetailNavigationParameter? _navigationParameter;

    public PageHeaderMetadata HeaderMetadata { get; } = new();

    public PageHeaderPresentationMode HeaderPresentationMode => PageHeaderPresentationMode.ProminentBreadcrumb;

    [ObservableProperty]
    private MinecraftNewsEntry? _newsEntry;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _version = string.Empty;

    [ObservableProperty]
    private string _type = string.Empty;

    [ObservableProperty]
    private string _typeDisplay = string.Empty;

    [ObservableProperty]
    private string _date = string.Empty;

    [ObservableProperty]
    private string _shortText = string.Empty;

    [ObservableProperty]
    private string _imageUrl = string.Empty;

    [ObservableProperty]
    private string _contentHtml = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public NewsDetailViewModel()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
        _resourceLoader = ResourceLoader.GetForViewIndependentUse();
    }

    public void Initialize(NewsDetailNavigationParameter navigationParameter)
    {
        _navigationParameter = navigationParameter;

        var entry = navigationParameter.Entry;
        NewsEntry = entry;
        Title = entry.Title;
        Version = entry.Version;
        Type = entry.Type;
        ContentHtml = string.Empty;
        ImageUrl = string.Empty;

        TypeDisplay = entry.Type switch
        {
            "release" => _resourceLoader.GetString("NewsType_Release"),
            "snapshot" => _resourceLoader.GetString("NewsType_Snapshot"),
            _ => entry.Type
        };

        Date = entry.Date.ToLocalTime().ToString("d");
        ShortText = entry.ShortText;

        if (entry.Image != null && !string.IsNullOrEmpty(entry.Image.Url))
        {
            ImageUrl = $"https://launchercontent.mojang.com{entry.Image.Url}";
        }

        ApplyHeaderMetadata();
        _ = LoadContentAsync(entry.ContentPath);
    }

    private async Task LoadContentAsync(string contentPath)
    {
        if (string.IsNullOrEmpty(contentPath))
        {
            return;
        }

        try
        {
            IsLoading = true;
            var url = $"https://launchercontent.mojang.com/{contentPath}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[新闻详情] 内容不存在或加载失败: {response.StatusCode}");
                ContentHtml = string.Empty;
                return;
            }

            var responseText = await response.Content.ReadAsStringAsync();
            var content = JsonConvert.DeserializeObject<NewsContentResponse>(responseText);

            if (content != null && !string.IsNullOrEmpty(content.Body))
            {
                ContentHtml = content.Body;
            }
            else
            {
                ContentHtml = string.Empty;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[新闻详情] 加载内容失败: {ex.Message}");
            ContentHtml = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyHeaderMetadata()
    {
        HeaderMetadata.Title = Title;
        HeaderMetadata.Subtitle = string.Empty;
        HeaderMetadata.ShowBreadcrumb = true;
        HeaderMetadata.BreadcrumbItems.Clear();

        if (_navigationParameter?.HasBreadcrumbRoot == true)
        {
            HeaderMetadata.BreadcrumbItems.Add(new NavigationBreadcrumbItem
            {
                DisplayText = _navigationParameter.BreadcrumbRootLabel,
                LocalNavigationTarget = _navigationParameter.BreadcrumbRootTarget,
            });
        }

        HeaderMetadata.BreadcrumbItems.Add(new NavigationBreadcrumbItem
        {
            DisplayText = Title,
            IsCurrent = true,
        });
    }
}

public class NewsContentResponse
{
    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("body")]
    public string? Body { get; set; }
}