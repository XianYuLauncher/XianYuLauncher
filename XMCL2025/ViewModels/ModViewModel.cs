using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using XMCL2025.Core.Models;
using XMCL2025.Core.Services;
using XMCL2025.Core.Contracts.Services;

namespace XMCL2025.ViewModels
{
    public partial class ModViewModel : ObservableObject
    {
        private readonly ModrinthService _modrinthService;
        private readonly IMinecraftVersionService _minecraftVersionService;

        [ObservableProperty]
        private string _searchQuery = "";

        [ObservableProperty]
        private ObservableCollection<ModrinthProject> _modList = new();

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private bool _isLoadingMore = false;

        [ObservableProperty]
        private string _errorMessage = "";
        


        [ObservableProperty]
        private int _offset = 0;

        [ObservableProperty]
        private bool _hasMoreResults = true;

        [ObservableProperty]
        private string _selectedLoader = "all";

        [ObservableProperty]
        private string _selectedVersion = "all";

        [ObservableProperty]
        private ObservableCollection<string> _availableVersions = new();

        public ModViewModel(ModrinthService modrinthService, IMinecraftVersionService minecraftVersionService)
        {
            _modrinthService = modrinthService;
            _minecraftVersionService = minecraftVersionService;
            _ = LoadMinecraftVersionsAsync();
        }

        private async Task LoadMinecraftVersionsAsync()
        {
            try
            {
                var manifest = await _minecraftVersionService.GetVersionManifestAsync();
                // 只添加发布版本，排除快照版本
                var releaseVersions = manifest.Versions
                    .Where(v => v.Type == "release")
                    .Select(v => v.Id)
                    .ToList();
                
                AvailableVersions.Clear();
                AvailableVersions.Add("all");
                foreach (var version in releaseVersions)
                {
                    AvailableVersions.Add(version);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load Minecraft versions: " + ex.Message;
            }
        }

        [RelayCommand]
        public async Task SearchModsAsync()
        {
            IsLoading = true;
            ErrorMessage = "";
            Offset = 0;
            HasMoreResults = true;

            try
            {
                // 构建facets参数
                var facets = new List<List<string>>();
                
                // 添加加载器筛选
                if (SelectedLoader != "all")
                {
                    facets.Add(new List<string> { $"categories:{SelectedLoader}" });
                }
                
                // 添加版本筛选
                if (SelectedVersion != "all")
                {
                    facets.Add(new List<string> { $"versions:{SelectedVersion}" });
                }
                

                
                // 调用Modrinth API搜索Mod
                var result = await _modrinthService.SearchModsAsync(
                    query: SearchQuery,
                    facets: facets,
                    limit: 20,
                    offset: Offset
                );

                // 更新Mod列表
                ModList.Clear();
                foreach (var hit in result.Hits)
                {
                    ModList.Add(hit);
                }
                Offset = result.Hits.Count;
                // 使用total_hits更准确地判断是否还有更多结果
                HasMoreResults = Offset < result.TotalHits;
                

            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanLoadMoreMods))]
        public async Task LoadMoreModsAsync()
        {
            if (IsLoading || IsLoadingMore || !HasMoreResults)
            {
                return;
            }

            IsLoadingMore = true;

            try
            {
                // 构建facets参数
                var facets = new List<List<string>>();
                
                // 添加加载器筛选
                if (SelectedLoader != "all")
                {
                    facets.Add(new List<string> { $"categories:{SelectedLoader}" });
                }
                
                // 添加版本筛选
                if (SelectedVersion != "all")
                {
                    facets.Add(new List<string> { $"versions:{SelectedVersion}" });
                }
                
                // 调用Modrinth API加载更多Mod
                var result = await _modrinthService.SearchModsAsync(
                    query: SearchQuery,
                    facets: facets,
                    limit: 20,
                    offset: Offset
                );

                // 追加到现有列表
                foreach (var hit in result.Hits)
                {
                    ModList.Add(hit);
                }
                
                Offset += result.Hits.Count;
                
                // 使用total_hits更准确地判断是否还有更多结果
                HasMoreResults = Offset < result.TotalHits;
                

            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private bool CanLoadMoreMods()
        {
            return !IsLoading && !IsLoadingMore && HasMoreResults;
        }
    }
}