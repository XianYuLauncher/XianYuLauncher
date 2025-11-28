using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XMCL2025.Contracts.Services;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Core.Services;
using XMCL2025.Services;
using XMCL2025.Core.Models;

namespace XMCL2025.ViewModels;

public partial class ResourceDownloadViewModel : ObservableRecipient
{
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly INavigationService _navigationService;
    private readonly ModrinthService _modrinthService;
    private readonly FabricService _fabricService;

    // 版本下载相关属性和命令
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Core.Contracts.Services.VersionEntry> _versions = new();

    [ObservableProperty]
    private bool _isVersionLoading = false;
    
    [ObservableProperty]
    private string _latestReleaseVersion = string.Empty;
    
    [ObservableProperty]
    private string _latestSnapshotVersion = string.Empty;
    
    [ObservableProperty]
    private bool _isRefreshing = false;
    
    // 过滤后的版本列表
    public ObservableCollection<Core.Contracts.Services.VersionEntry> FilteredVersions =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Versions
            : new ObservableCollection<Core.Contracts.Services.VersionEntry>(
                Versions.Where(v => v.Id.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)));

    // Mod下载相关属性和命令
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ModrinthProject> _mods = new();

    [ObservableProperty]
    private bool _isModLoading = false;
    
    [ObservableProperty]
    private bool _isModLoadingMore = false;
    
    [ObservableProperty]
    private int _modOffset = 0;
    
    [ObservableProperty]
    private bool _modHasMoreResults = true;
    
    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    [ObservableProperty]
    private string _selectedLoader = "all";
    
    [ObservableProperty]
    private string _selectedVersion = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<string> _availableVersions = new();
    
    // 为了与ModPage.xaml兼容，添加ModList属性，指向Mods集合
    public ObservableCollection<ModrinthProject> ModList => Mods;
    
    // 为了与ModPage.xaml兼容，添加IsLoading属性，指向IsModLoading
    public bool IsLoading => IsModLoading;
    
    // 为了与ModPage.xaml兼容，添加IsLoadingMore属性，指向IsModLoadingMore
    public bool IsLoadingMore => IsModLoadingMore;
    
    // 为了与ModPage.xaml兼容，添加HasMoreResults属性，指向ModHasMoreResults
    public bool HasMoreResults => ModHasMoreResults;
    
    private const int _modPageSize = 20;

    public ResourceDownloadViewModel(
        IMinecraftVersionService minecraftVersionService,
        INavigationService navigationService,
        ModrinthService modrinthService,
        FabricService fabricService)
    {
        _minecraftVersionService = minecraftVersionService;
        _navigationService = navigationService;
        _modrinthService = modrinthService;
        _fabricService = fabricService;
        
        // 初始化时加载版本列表
        _ = InitializeAsync();
        
        // 初始化时加载可用版本列表
        _ = LoadAvailableVersionsAsync();
    }
    
    private async Task InitializeAsync()
    {
        // 初始化时加载版本列表（只加载第一页）
        await SearchVersionsCommand.ExecuteAsync(null);
        // 初始化时加载Mod列表（只加载第一页）
        await SearchModsCommand.ExecuteAsync(null);
    }
    
    // 版本下载命令
    [RelayCommand]
    private async Task SearchVersionsAsync()
    {
        await LoadVersionsAsync();
    }
    
    [RelayCommand]
    private async Task RefreshVersionsAsync()
    {
        IsRefreshing = true;
        await LoadVersionsAsync();
        IsRefreshing = false;
    }
    
    private async Task LoadVersionsAsync()
    {
        IsVersionLoading = true;
        try
        {
            var manifest = await _minecraftVersionService.GetVersionManifestAsync();
            var versionList = manifest.Versions.ToList(); // 加载所有版本
            
            // 更新最新版本信息
            LatestReleaseVersion = versionList.FirstOrDefault(v => v.Type == "release")?.Id ?? string.Empty;
            LatestSnapshotVersion = versionList.FirstOrDefault(v => v.Type == "snapshot")?.Id ?? string.Empty;
            
            // 更新版本列表
            Versions.Clear();
            foreach (var version in versionList)
            {
                Versions.Add(version);
            }
        }
        catch (Exception ex)
        {
            // 处理异常
        }
        finally
        {
            IsVersionLoading = false;
        }
    }

    [RelayCommand]
    private async Task DownloadVersionAsync(object parameter)
    {
        string versionId = string.Empty;
        
        // 处理不同类型的参数
        if (parameter is Core.Contracts.Services.VersionEntry versionEntry)
        {
            versionId = versionEntry.Id;
        }
        else if (parameter is string stringId)
        {
            versionId = stringId;
        }
        
        if (string.IsNullOrEmpty(versionId))
        {
            return;
        }

        try
        {
            IsVersionLoading = true;
            // 导航到版本选择页面
            _navigationService.NavigateTo(typeof(ModLoader选择ViewModel).FullName!, versionId);
        }
        catch (Exception ex)
        {
            // 处理异常
        }
        finally
        {
            IsVersionLoading = false;
        }
    }

    // Mod下载命令
    [RelayCommand]
    private async Task SearchModsAsync()
    {
        IsModLoading = true;
        ModOffset = 0;
        ModHasMoreResults = true;

        try
        {
            // 构建facets参数
            var facets = new List<List<string>>();
            
            // 添加加载器筛选条件
            if (SelectedLoader != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedLoader}" });
            }
            
            // 添加版本筛选条件
            if (!string.IsNullOrEmpty(SelectedVersion))
            {
                facets.Add(new List<string> { $"versions:{SelectedVersion}" });
            }

            // 这里使用Modrinth API搜索mods，只加载第一页
            var result = await _modrinthService.SearchModsAsync(
                query: SearchQuery,
                facets: facets,
                index: "relevance",
                offset: ModOffset,
                limit: _modPageSize
            );

            // 更新Mod列表
            Mods.Clear();
            foreach (var hit in result.Hits)
            {
                Mods.Add(hit);
            }
            ModOffset = result.Hits.Count;
            // 使用total_hits更准确地判断是否还有更多结果
            ModHasMoreResults = ModOffset < result.TotalHits;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsModLoading = false;
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanLoadMoreMods))]
    public async Task LoadMoreModsAsync()
    {
        if (IsModLoading || IsModLoadingMore || !ModHasMoreResults)
        {
            return;
        }

        IsModLoadingMore = true;

        try
        {
            // 构建facets参数
            var facets = new List<List<string>>();
            
            // 添加加载器筛选条件
            if (SelectedLoader != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedLoader}" });
            }
            
            // 添加版本筛选条件
            if (!string.IsNullOrEmpty(SelectedVersion))
            {
                facets.Add(new List<string> { $"versions:{SelectedVersion}" });
            }
            
            // 调用Modrinth API加载更多Mod
            var result = await _modrinthService.SearchModsAsync(
                query: SearchQuery,
                facets: facets,
                index: "relevance",
                offset: ModOffset,
                limit: _modPageSize
            );

            // 追加到现有列表
            foreach (var hit in result.Hits)
            {
                Mods.Add(hit);
            }
            
            ModOffset += result.Hits.Count;
            
            // 使用total_hits更准确地判断是否还有更多结果
            ModHasMoreResults = ModOffset < result.TotalHits;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsModLoadingMore = false;
        }
    }
    
    private bool CanLoadMoreMods()
    {
        return !IsModLoading && !IsModLoadingMore && ModHasMoreResults;
    }
    
    /// <summary>
    /// 加载可用版本列表
    /// </summary>
    private async Task LoadAvailableVersionsAsync()
    {
        try
        {
            // 获取所有可用的Minecraft版本
            var manifest = await _minecraftVersionService.GetVersionManifestAsync();
            var versions = manifest.Versions.Select(v => v.Id).Distinct().ToList();
            
            // 保存当前选中的版本
            var currentSelectedVersion = SelectedVersion;
            
            // 更新可用版本列表
            AvailableVersions.Clear();
            foreach (var version in versions)
            {
                AvailableVersions.Add(version);
            }
            
            // 如果当前选中的版本仍然在可用版本列表中，则保留选中状态
            if (!string.IsNullOrEmpty(currentSelectedVersion) && versions.Contains(currentSelectedVersion))
            {
                SelectedVersion = currentSelectedVersion;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DownloadModAsync(ModrinthProject mod)
    {
        if (mod == null)
        {
            return;
        }

        // 导航到Mod下载详情页面
        _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, mod.ProjectId);
    }
}
