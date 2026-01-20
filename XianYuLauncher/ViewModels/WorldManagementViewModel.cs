using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.ViewModels;

public partial class WorldManagementViewModel : ObservableRecipient
{
    private readonly IFileService _fileService;
    private readonly INavigationService _navigationService;
    private readonly ITranslationService? _translationService;
    private readonly CurseForgeService _curseForgeService;
    private readonly ModInfoService _modInfoService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _dataPacksLoaded = false;
    
    // 本地化字符串
    private static string UnknownText => ResourceExtensions.GetLocalized("WorldManagement_Unknown");
    private static string EnabledText => ResourceExtensions.GetLocalized("WorldManagement_Enabled");
    private static string DisabledText => ResourceExtensions.GetLocalized("WorldManagement_Disabled");
    
    /// <summary>
    /// 本地化游戏模式
    /// </summary>
    private static string LocalizeGameMode(GameModeType gameMode)
    {
        return gameMode switch
        {
            GameModeType.Survival => ResourceExtensions.GetLocalized("WorldManagement_GameMode_Survival"),
            GameModeType.Creative => ResourceExtensions.GetLocalized("WorldManagement_GameMode_Creative"),
            GameModeType.Adventure => ResourceExtensions.GetLocalized("WorldManagement_GameMode_Adventure"),
            GameModeType.Spectator => ResourceExtensions.GetLocalized("WorldManagement_GameMode_Spectator"),
            GameModeType.Hardcore => ResourceExtensions.GetLocalized("WorldManagement_GameMode_Hardcore"),
            _ => UnknownText
        };
    }
    
    /// <summary>
    /// 本地化难度
    /// </summary>
    private static string LocalizeDifficulty(DifficultyType difficulty)
    {
        return difficulty switch
        {
            DifficultyType.Peaceful => ResourceExtensions.GetLocalized("WorldManagement_Difficulty_Peaceful"),
            DifficultyType.Easy => ResourceExtensions.GetLocalized("WorldManagement_Difficulty_Easy"),
            DifficultyType.Normal => ResourceExtensions.GetLocalized("WorldManagement_Difficulty_Normal"),
            DifficultyType.Hard => ResourceExtensions.GetLocalized("WorldManagement_Difficulty_Hard"),
            _ => UnknownText
        };
    }
    
    [ObservableProperty]
    private string _worldName = string.Empty;
    
    [ObservableProperty]
    private string _worldPath = string.Empty;
    
    [ObservableProperty]
    private BitmapImage? _worldIcon;
    
    [ObservableProperty]
    private string _seed = string.Empty;
    
    [ObservableProperty]
    private string _difficulty = string.Empty;
    
    [ObservableProperty]
    private string _gameMode = string.Empty;
    
    [ObservableProperty]
    private string _worldSize = "0 MB";
    
    [ObservableProperty]
    private DateTime _creationTime;
    
    /// <summary>
    /// 格式化的创建时间
    /// </summary>
    public string FormattedCreationTime => CreationTime == DateTime.MinValue ? UnknownText : CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
    
    partial void OnCreationTimeChanged(DateTime value)
    {
        OnPropertyChanged(nameof(FormattedCreationTime));
    }
    
    [ObservableProperty]
    private DateTime _lastPlayedTime;
    
    /// <summary>
    /// 格式化的最后游玩时间
    /// </summary>
    public string FormattedLastPlayedTime => LastPlayedTime == DateTime.MinValue ? UnknownText : LastPlayedTime.ToString("yyyy-MM-dd HH:mm:ss");
    
    partial void OnLastPlayedTimeChanged(DateTime value)
    {
        OnPropertyChanged(nameof(FormattedLastPlayedTime));
    }
    
    [ObservableProperty]
    private string _playTime = string.Empty;
    
    [ObservableProperty]
    private string _mcDays = string.Empty;
    
    [ObservableProperty]
    private string _allowCommands = string.Empty;
    
    [ObservableProperty]
    private int _selectedTabIndex = 0;
    
    public WorldManagementViewModel(
        IFileService fileService,
        INavigationService navigationService,
        CurseForgeService curseForgeService,
        ModInfoService modInfoService)
    {
        _fileService = fileService;
        _navigationService = navigationService;
        _curseForgeService = curseForgeService;
        _modInfoService = modInfoService;
        
        // 尝试获取翻译服务（可选）
        try
        {
            _translationService = App.GetService<ITranslationService>();
        }
        catch
        {
            _translationService = null;
            System.Diagnostics.Debug.WriteLine("[WorldManagement] 翻译服务不可用");
        }
    }
    
    public async Task InitializeAsync(string worldPath)
    {
        System.Diagnostics.Debug.WriteLine($"[WorldManagement] InitializeAsync 开始: {worldPath}");
        
        try
        {
            // 取消之前的操作
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            
            WorldPath = worldPath;
            WorldName = Path.GetFileName(worldPath);
            
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] WorldPath 已设置: {WorldPath}");
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] WorldName 已设置: {WorldName}");
            
            await LoadWorldDataAsync(_cancellationTokenSource.Token);
            
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] InitializeAsync 完成");
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] InitializeAsync 已取消");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] InitializeAsync 异常: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] 堆栈: {ex.StackTrace}");
            throw;
        }
    }
    
    public void Cleanup()
    {
        System.Diagnostics.Debug.WriteLine($"[WorldManagement] Cleanup 开始");
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }
    
    private async Task LoadWorldDataAsync(CancellationToken cancellationToken)
    {
        System.Diagnostics.Debug.WriteLine($"[WorldManagement] LoadWorldDataAsync 开始");
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // 在后台线程读取所有数据
            var data = await Task.Run(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - 开始读取数据");
                
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var result = new
                    {
                        IconPath = (string?)null,
                        Seed = UnknownText,
                        Difficulty = UnknownText,
                        GameMode = UnknownText,
                        WorldSize = "0 MB",
                        CreationTime = DateTime.MinValue,
                        LastPlayedTime = DateTime.MinValue,
                        PlayTime = "0",
                        McDays = "0",
                        AllowCommands = UnknownText
                    };
                    
                    // 加载世界图标
                    var iconPath = Path.Combine(WorldPath, "icon.png");
                    var hasIcon = File.Exists(iconPath);
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - 图标存在: {hasIcon}");
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 加载世界数据
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - 准备读取 NBT 数据");
                    WorldData? worldData = null;
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var worldDataService = new WorldDataService();
                        System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - WorldDataService 已创建");
                        
                        worldData = worldDataService.ReadWorldData(WorldPath);
                        System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - NBT 数据读取完成");
                    }
                    catch (OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - NBT 读取被取消");
                        throw;
                    }
                    catch (Exception nbtEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - NBT 读取异常: {nbtEx.GetType().Name}: {nbtEx.Message}");
                        System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - NBT 异常堆栈: {nbtEx.StackTrace}");
                        if (nbtEx.InnerException != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - 内部异常: {nbtEx.InnerException.GetType().Name}: {nbtEx.InnerException.Message}");
                        }
                        // 继续执行，使用默认值
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - 世界数据读取完成");
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 计算游玩时长和 MC 天数
                    string playTime = "0";
                    string mcDays = "0";
                    string allowCommands = UnknownText;
                    
                    if (worldData != null)
                    {
                        if (worldData.Time > 0)
                        {
                            // 1 秒 = 20 游戏刻
                            // 1 小时 = 3600 秒 = 72000 游戏刻
                            double hours = worldData.Time / 72000.0;
                            
                            if (hours >= 1)
                            {
                                playTime = $"{hours:F1} {ResourceExtensions.GetLocalized("WorldManagement_Hours")}";
                            }
                            else
                            {
                                // 小于 1 小时，显示分钟
                                double minutes = worldData.Time / 1200.0; // 1 分钟 = 1200 游戏刻
                                playTime = $"{minutes:F0} {ResourceExtensions.GetLocalized("WorldManagement_Minutes")}";
                            }
                            
                            // 1 MC 天 = 24000 游戏刻
                            double days = worldData.Time / 24000.0;
                            mcDays = $"{days:F1} {ResourceExtensions.GetLocalized("WorldManagement_Days")}";
                            
                            System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - 游玩时长: {playTime}, MC天数: {mcDays}");
                        }
                        
                        // 设置允许命令状态
                        allowCommands = worldData.AllowCommands ? EnabledText : DisabledText;
                    }
                    
                    // 获取文件夹信息
                    var dirInfo = new DirectoryInfo(WorldPath);
                    DateTime creationTime = DateTime.MinValue;
                    DateTime lastPlayedTime = DateTime.MinValue;
                    string worldSize = "0 MB";
                    
                    if (dirInfo.Exists)
                    {
                        creationTime = dirInfo.CreationTime;
                        lastPlayedTime = dirInfo.LastWriteTime;
                        
                        // 计算大小
                        long size = CalculateDirectorySize(dirInfo);
                        worldSize = FormatFileSize(size);
                        System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - 文件夹信息读取完成, 大小: {worldSize}");
                    }
                    
                    return new
                    {
                        IconPath = hasIcon ? iconPath : null,
                        Seed = worldData?.Seed.ToString() ?? UnknownText,
                        Difficulty = LocalizeDifficulty(worldData?.Difficulty ?? DifficultyType.Unknown),
                        GameMode = LocalizeGameMode(worldData?.GameMode ?? GameModeType.Unknown),
                        WorldSize = worldSize,
                        CreationTime = creationTime,
                        LastPlayedTime = lastPlayedTime,
                        PlayTime = playTime,
                        McDays = mcDays,
                        AllowCommands = allowCommands
                    };
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程 - 操作已取消");
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程异常: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程堆栈: {ex.StackTrace}");
                    return new
                    {
                        IconPath = (string?)null,
                        Seed = UnknownText,
                        Difficulty = UnknownText,
                        GameMode = UnknownText,
                        WorldSize = "0 MB",
                        CreationTime = DateTime.MinValue,
                        LastPlayedTime = DateTime.MinValue,
                        PlayTime = "0",
                        McDays = "0",
                        AllowCommands = UnknownText
                    };
                }
            }, cancellationToken);
            
            cancellationToken.ThrowIfCancellationRequested();
            
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] 后台线程完成，准备更新 UI");
            
            // 在 UI 线程更新所有属性
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 开始更新属性");
                
                try
                {
                    // 再次检查是否已取消
                    if (cancellationToken.IsCancellationRequested)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 操作已取消");
                        return;
                    }
                    
                    if (data.IconPath != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 设置图标: {data.IconPath}");
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.UriSource = new Uri(data.IconPath);
                            WorldIcon = bitmap;
                        }
                        catch (Exception iconEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 加载图标失败: {iconEx.Message}");
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 设置种子: {data.Seed}");
                    Seed = data.Seed;
                    
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 设置难度: {data.Difficulty}");
                    Difficulty = data.Difficulty;
                    
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 设置游戏模式: {data.GameMode}");
                    GameMode = data.GameMode;
                    
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 设置世界大小: {data.WorldSize}");
                    WorldSize = data.WorldSize;
                    
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 设置创建时间: {data.CreationTime}");
                    CreationTime = data.CreationTime;
                    
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 设置最后游玩时间: {data.LastPlayedTime}");
                    LastPlayedTime = data.LastPlayedTime;
                    
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 设置游玩时长: {data.PlayTime}");
                    PlayTime = data.PlayTime;
                    
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 设置MC天数: {data.McDays}");
                    McDays = data.McDays;
                    
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 设置允许命令: {data.AllowCommands}");
                    AllowCommands = data.AllowCommands;
                    
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程 - 世界数据加载完成: {WorldName}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程异常: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[WorldManagement] UI线程堆栈: {ex.StackTrace}");
                }
            });
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] LoadWorldDataAsync 已取消");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] LoadWorldDataAsync 异常: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[WorldManagement] 堆栈: {ex.StackTrace}");
        }
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
    
    /// <summary>
    /// 当选中的 Tab 改变时调用
    /// </summary>
    public async Task OnSelectedTabChangedAsync(int tabIndex)
    {
        System.Diagnostics.Debug.WriteLine($"[WorldManagement] Tab 切换到: {tabIndex}");
        
        // Tab 1 是数据包页签
        if (tabIndex == 1 && !_dataPacksLoaded)
        {
            _dataPacksLoaded = true;
            
            // 延迟 300ms 等待 Tab 切换动画完成
            await Task.Delay(300);
            
            if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                await LoadDataPacksAsync(_cancellationTokenSource.Token);
            }
        }
    }
}
