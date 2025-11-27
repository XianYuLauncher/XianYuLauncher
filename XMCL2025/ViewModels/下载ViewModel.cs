using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XMCL2025.Contracts.Services;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Core.Services;

namespace XMCL2025.ViewModels;

public partial class 下载ViewModel : ObservableRecipient
{
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IFileService _fileService;

    [ObservableProperty]
    private string _latestReleaseVersion = "";

    [ObservableProperty]
    private string _latestSnapshotVersion = "";

    [ObservableProperty]
    private ObservableCollection<Core.Contracts.Services.VersionEntry> _versions = new();

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private bool _isRefreshing = false;

    public ObservableCollection<Core.Contracts.Services.VersionEntry> FilteredVersions =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Versions
            : new ObservableCollection<Core.Contracts.Services.VersionEntry>(
                Versions.Where(v => v.Id.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)));

    public 下载ViewModel()
    {
        _minecraftVersionService = App.GetService<IMinecraftVersionService>();
        _fileService = App.GetService<IFileService>();
        InitializeAsync().ConfigureAwait(false);
    }

    private async Task InitializeAsync()
    {
        await LoadVersionsAsync();
    }

    [RelayCommand]
    private async Task LoadVersionsAsync()
    {
        IsLoading = true;
        try
        {
            var manifest = await _minecraftVersionService.GetVersionManifestAsync();
            LatestReleaseVersion = manifest.Latest.Release;
            LatestSnapshotVersion = manifest.Latest.Snapshot;
            
            Versions.Clear();
            foreach (var version in manifest.Versions)
            {
                Versions.Add(version);
            }
        }
        catch (Exception ex)
        {
            // Handle exception (could show a message to user)
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshVersionsAsync()
    {
        IsRefreshing = true;
        await LoadVersionsAsync();
    }

    [RelayCommand]
    private async Task DownloadVersionAsync(string versionId)
    {
        try
        {
            // 跳转到ModLoader选择页面
            var navigationService = App.GetService<INavigationService>();
            navigationService.NavigateTo("XMCL2025.ViewModels.ModLoader选择ViewModel", versionId);
        }
        catch (Exception ex)
        {
            // 显示错误提示
            await ShowMessageAsync($"下载失败: {ex.Message}");
        }
    }
    
    private async Task ShowMessageAsync(string message)
    {
        // 创建并显示消息对话框
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "下载提示",
            Content = message,
            CloseButtonText = "确定",
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        await dialog.ShowAsync();
    }
}
