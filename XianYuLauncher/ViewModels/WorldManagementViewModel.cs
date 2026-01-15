using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.ViewModels;

public partial class WorldManagementViewModel : ObservableRecipient
{
    private readonly IFileService _fileService;
    private readonly INavigationService _navigationService;
    
    [ObservableProperty]
    private string _worldName = string.Empty;
    
    [ObservableProperty]
    private string _worldPath = string.Empty;
    
    [ObservableProperty]
    private string _worldIcon;
    
    [ObservableProperty]
    private string _seed = "未知";
    
    [ObservableProperty]
    private string _difficulty = "未知";
    
    [ObservableProperty]
    private string _gameMode = "未知";
    
    [ObservableProperty]
    private string _worldSize = "0 MB";
    
    [ObservableProperty]
    private DateTime _creationTime;
    
    [ObservableProperty]
    private DateTime _lastPlayedTime;
    
    [ObservableProperty]
    private string _playTime = "未知";
    
    [ObservableProperty]
    private int _selectedTabIndex = 0;
    
    public WorldManagementViewModel(
        IFileService fileService,
        INavigationService navigationService)
    {
        _fileService = fileService;
        _navigationService = navigationService;
    }
    
    public async Task InitializeAsync(string worldPath)
    {
        WorldPath = worldPath;
        WorldName = Path.GetFileName(worldPath);
        
        await LoadWorldDataAsync();
    }
    
    private async Task LoadWorldDataAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                // 加载世界图标
                var iconPath = Path.Combine(WorldPath, "icon.png");
                if (File.Exists(iconPath))
                {
                    WorldIcon = iconPath;
                }
                
                // 加载世界数据
                var worldDataService = new WorldDataService();
                var worldData = worldDataService.ReadWorldData(WorldPath);
                
                if (worldData != null)
                {
                    Seed = worldData.Seed.ToString();
                    Difficulty = worldData.Difficulty;
                    GameMode = worldData.GameMode;
                }
                
                // 获取文件夹信息
                var dirInfo = new DirectoryInfo(WorldPath);
                if (dirInfo.Exists)
                {
                    CreationTime = dirInfo.CreationTime;
                    LastPlayedTime = dirInfo.LastWriteTime;
                    
                    // 计算大小
                    long size = CalculateDirectorySize(dirInfo);
                    WorldSize = FormatFileSize(size);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载世界数据失败: {ex.Message}");
            }
        });
    }
    
    private long CalculateDirectorySize(DirectoryInfo directory)
    {
        long size = 0;
        try
        {
            foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
            {
                size += file.Length;
            }
        }
        catch { }
        return size;
    }
    
    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        else if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        else if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        else
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
    
    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
    }
    
    [RelayCommand]
    private void OpenWorldFolder()
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", WorldPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"打开文件夹失败: {ex.Message}");
        }
    }
}
