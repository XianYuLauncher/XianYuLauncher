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
    private bool _isLoading = false;
    
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
        IsLoading = true;
        try
        {
            var manifest = await _minecraftVersionService.GetVersionManifestAsync();
            var versionList = manifest.Versions.ToList(); // 加载所有版本
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
            IsLoading = false;
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
            IsLoading = true;
            // 导航到版本选择页面
            _navigationService.NavigateTo(typeof(ModLoader选择ViewModel).FullName!, versionId);
        }
        catch (Exception ex)
        {
            // 处理异常
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Mod下载命令
    [RelayCommand]
    private async Task SearchModsAsync()
    {
        IsModLoading = true;

        try
        {
            // 这里使用Modrinth API搜索mods，加载所有结果
            var result = await _modrinthService.SearchModsAsync(
                query: SearchQuery,
                facets: new List<List<string>> { new List<string> { "categories:fabric" } },
                index: "relevance",
                offset: 0,
                limit: 100 // 增加限制，加载更多结果
            );

            // 更新Mod列表
            Mods.Clear();
            foreach (var hit in result.Hits)
            {
                Mods.Add(hit);
            }
        }
        catch (Exception ex)
        {
            // 处理异常
        }
        finally
        {
            IsModLoading = false;
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
