using System;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Windows.ApplicationModel.Resources;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Services;

namespace XianYuLauncher.ViewModels;

public partial class NewsDetailViewModel : ObservableRecipient
{
    private readonly INavigationService _navigationService;
    private readonly HttpClient _httpClient;
    private readonly ResourceLoader _resourceLoader;

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
    private bool _isLoading = false;

    public NewsDetailViewModel()
    {
        _navigationService = App.GetService<INavigationService>();
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
        _resourceLoader = ResourceLoader.GetForViewIndependentUse();
    }

    public void Initialize(MinecraftNewsEntry entry)
    {
        NewsEntry = entry;
        Title = entry.Title;
        Version = entry.Version;
        Type = entry.Type;
        
        // 使用本地化资源获取类型显示文本
        TypeDisplay = entry.Type switch
        {
            "release" => _resourceLoader.GetString("NewsType_Release"),
            "snapshot" => _resourceLoader.GetString("NewsType_Snapshot"),
            _ => entry.Type
        };
        
        // 使用系统区域设置格式化日期
        Date = entry.Date.ToLocalTime().ToString("d");
        ShortText = entry.ShortText;
        
        if (entry.Image != null && !string.IsNullOrEmpty(entry.Image.Url))
        {
            ImageUrl = $"https://launchercontent.mojang.com{entry.Image.Url}";
        }

        // 异步加载详细内容
        _ = LoadContentAsync(entry.ContentPath);
    }

    private async Task LoadContentAsync(string contentPath)
    {
        if (string.IsNullOrEmpty(contentPath)) return;

        try
        {
            IsLoading = true;
            var url = $"https://launchercontent.mojang.com/{contentPath}";
            var response = await _httpClient.GetAsync(url);
            
            // 404 或其他错误时静默处理，不显示错误信息
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[新闻详情] 内容不存在或加载失败: {response.StatusCode}");
                ContentHtml = string.Empty; // 保持为空，UI 会自动隐藏
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
                ContentHtml = string.Empty; // 内容为空时隐藏
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[新闻详情] 加载内容失败: {ex.Message}");
            ContentHtml = string.Empty; // 异常时也静默处理
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
    }
}

/// <summary>
/// 新闻详细内容响应
/// </summary>
public class NewsContentResponse
{
    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("body")]
    public string? Body { get; set; }
}
