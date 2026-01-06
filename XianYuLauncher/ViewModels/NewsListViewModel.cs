using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Services;

namespace XianYuLauncher.ViewModels;

public partial class NewsListViewModel : ObservableRecipient
{
    private readonly INavigationService _navigationService;
    private readonly IFileService _fileService;
    private MinecraftNewsService? _newsService;

    [ObservableProperty]
    private ObservableCollection<MinecraftNewsEntry> _newsItems = new();

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "全部";

    public string[] FilterOptions { get; } = new[] { "全部", "正式版", "快照" };

    public NewsListViewModel()
    {
        _navigationService = App.GetService<INavigationService>();
        _fileService = App.GetService<IFileService>();
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
                // 先筛选，再按日期降序排序（最新的在前）
                var filteredEntries = newsData.Entries
                    .Where(entry => SelectedFilter == "全部" ||
                                   (SelectedFilter == "正式版" && entry.Type == "release") ||
                                   (SelectedFilter == "快照" && entry.Type == "snapshot"))
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
        var newsData = await _newsService.GetLatestNewsAsync(forceRefresh: true);
        await LoadNewsAsync();
    }

    [RelayCommand]
    private void OpenNewsDetail(MinecraftNewsEntry entry)
    {
        if (entry != null)
        {
            _navigationService.NavigateTo(typeof(NewsDetailViewModel).FullName!, entry);
        }
    }

    partial void OnSelectedFilterChanged(string value)
    {
        _ = LoadNewsAsync();
    }
}
