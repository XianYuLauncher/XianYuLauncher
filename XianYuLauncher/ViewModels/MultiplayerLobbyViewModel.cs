using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Helpers;
using Serilog;

namespace XianYuLauncher.ViewModels;

// 玩家信息类，用于显示玩家列表
public class RoomPlayer
{
    public string Name { get; set; }
    public Microsoft.UI.Xaml.Media.Imaging.BitmapImage Avatar { get; set; }
}

public partial class MultiplayerLobbyViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    
    // 计时器
    private readonly DispatcherTimer _timer;
    private TimeSpan _elapsedTime = TimeSpan.Zero;
    
    // 房间信息
    [ObservableProperty]
    private string _roomId = "";
    
    [ObservableProperty]
    private string _elapsedTimeText = "00:00:00";
    
    [ObservableProperty]
    private string _hostName = "当前玩家";
    
    [ObservableProperty]
    private string _easyTierVersion = "加载中...";
    
    [ObservableProperty]
    private bool _isGuest = false;
    
    [ObservableProperty]
    private string _url = "";
    
    // 显示文本
    [ObservableProperty]
    private string _hostLabel = "联机大厅Page_HostLabel".GetLocalized();
    
    [ObservableProperty]
    private string _easyTierLabel = "联机大厅Page_EasyTierLabel".GetLocalized();
    
    // 端口信息，用于获取meta数据
    private string? _port;
    
    // HttpClient用于获取meta数据和玩家列表
    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    
    // FileService用于获取文件路径
    private readonly IFileService _fileService;
    
    // 计时器用于轮询玩家列表
    private readonly DispatcherTimer _playerListTimer;
    
    // 玩家列表属性，用于绑定到UI
    [ObservableProperty]
    private ObservableCollection<RoomPlayer> _playerList = new ObservableCollection<RoomPlayer>();
    
    // 玩家列表是否为空的属性，用于绑定到UI
    [ObservableProperty]
    private bool _isPlayerListEmpty = true;
    
    public MultiplayerLobbyViewModel(INavigationService navigationService, IFileService fileService)
    {
        _navigationService = navigationService;
        _fileService = fileService;
        
        // 初始化计时计时器
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1) // 每秒触发一次
        };
        _timer.Tick += OnTimerTick;
        
        // 初始化玩家列表计时器
        _playerListTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2) // 每2秒获取一次玩家列表
        };
        _playerListTimer.Tick += OnPlayerListTimerTick;
        
        // 加载当前活跃角色名称
        LoadCurrentProfileName();
    }
    
    private void OnTimerTick(object sender, object e)
    {
        _elapsedTime += TimeSpan.FromSeconds(1);
        try
        {
            // 使用更安全的格式化方式
            ElapsedTimeText = $"{_elapsedTime.Hours:D2}:{_elapsedTime.Minutes:D2}:{_elapsedTime.Seconds:D2}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"格式化时间失败: {ex.Message}");
            // 确保ElapsedTimeText有一个默认值，避免UI崩溃
            ElapsedTimeText = "00:00:00";
        }
    }
    
    public void OnNavigatedFrom()
    {
        // 停止所有计时器
        _timer.Stop();
        _playerListTimer.Stop();
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        // 启动所有计时器
        _timer.Start();
        
        // 如果参数是匿名对象，获取RoomId、Port、IsGuest和Url
        if (parameter != null)
        {
            var paramType = parameter.GetType();
            var roomIdProp = paramType.GetProperty("RoomId");
            var portProp = paramType.GetProperty("Port");
            var isGuestProp = paramType.GetProperty("IsGuest");
            var urlProp = paramType.GetProperty("Url");
            
            if (roomIdProp != null)
            {
                RoomId = roomIdProp.GetValue(parameter)?.ToString() ?? "";
            }
            
            if (portProp != null)
            {
                _port = portProp.GetValue(parameter)?.ToString();
                // 获取meta数据
                if (!string.IsNullOrEmpty(_port))
                {
                    await GetMetaDataAsync();
                    // 获取初始玩家列表
                    await UpdatePlayerListAsync();
                    // 启动玩家列表计时器
                    _playerListTimer.Start();
                }
            }
            
            if (isGuestProp != null)
            {
                IsGuest = (bool)(isGuestProp.GetValue(parameter) ?? false);
                // 根据是否为房客更新显示内容
                if (IsGuest)
                {
                    HostLabel = "联机大厅Page_GuestLabel".GetLocalized();
                    // 保持显示EasyTier版本，不改为URL
                    EasyTierLabel = "联机大厅Page_EasyTierLabel".GetLocalized();
                }
            }
            
            if (urlProp != null)
            {
                Url = urlProp.GetValue(parameter)?.ToString() ?? "";
                // 房客模式下也获取并显示EasyTier版本，不显示URL
            }
        }
    }
    
    /// <summary>
    /// 玩家列表计时器触发事件
    /// </summary>
    private async void OnPlayerListTimerTick(object sender, object e)
    {
        await UpdatePlayerListAsync();
    }
    
    /// <summary>
    /// 更新玩家列表
    /// </summary>
    private async Task UpdatePlayerListAsync()
    {
        if (string.IsNullOrEmpty(_port)) return;
        
        try
        {
            string stateUrl = $"http://localhost:{_port}/state";
            HttpResponseMessage response = await _httpClient.GetAsync(stateUrl);
            
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                
                // 解析JSON
                using (JsonDocument doc = JsonDocument.Parse(content))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("profiles", out JsonElement profilesElement) &&
                        profilesElement.ValueKind == JsonValueKind.Array)
                    {
                        // 获取默认头像
                        BitmapImage defaultAvatar = await GetDefaultSteveAvatarAsync();
                        
                        // 创建新的玩家列表
                        var newPlayerList = new List<RoomPlayer>();
                        
                        // 遍历所有玩家
                        foreach (JsonElement profileElement in profilesElement.EnumerateArray())
                        {
                            if (profileElement.TryGetProperty("name", out JsonElement nameElement) &&
                                nameElement.ValueKind == JsonValueKind.String)
                            {
                                string playerName = nameElement.GetString();
                                
                                // 创建玩家对象
                                newPlayerList.Add(new RoomPlayer
                                {
                                    Name = playerName,
                                    Avatar = defaultAvatar
                                });
                            }
                        }
                        
                        // 更新UI线程上的玩家列表
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            // 清空现有列表
                            PlayerList.Clear();
                            // 添加新玩家
                            foreach (var player in newPlayerList)
                            {
                                PlayerList.Add(player);
                            }
                            // 更新玩家列表是否为空的属性
                            IsPlayerListEmpty = PlayerList.Count == 0;
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"获取玩家列表失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取默认的史蒂夫头像，使用Win2D进行邻近插值处理
    /// </summary>
    private async Task<BitmapImage> GetDefaultSteveAvatarAsync()
    {
        try
        {
            // 1. 创建CanvasDevice
            var device = CanvasDevice.GetSharedDevice();
            
            // 2. 加载史蒂夫头像图片
            var steveUri = new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png");
            var file = await StorageFile.GetFileFromApplicationUriAsync(steveUri);
            CanvasBitmap canvasBitmap;
            
            using (var stream = await file.OpenReadAsync())
            {
                canvasBitmap = await CanvasBitmap.LoadAsync(device, stream);
            }

            // 3. 创建CanvasRenderTarget用于处理，使用合适的分辨率
            var renderTarget = new CanvasRenderTarget(
                device,
                24, // 显示宽度
                24, // 显示高度
                96 // DPI
            );

            // 4. 执行处理，使用最近邻插值保持像素锐利
            using (var ds = renderTarget.CreateDrawingSession())
            {
                // 绘制整个史蒂夫头像，并使用最近邻插值确保清晰
                ds.DrawImage(
                    canvasBitmap,
                    new Windows.Foundation.Rect(0, 0, 24, 24), // 目标位置和大小
                    new Windows.Foundation.Rect(0, 0, canvasBitmap.Size.Width, canvasBitmap.Size.Height), // 源位置和大小
                    1.0f, // 不透明度
                    CanvasImageInterpolation.NearestNeighbor // 最近邻插值，保持像素锐利
                );
            }

            // 5. 转换为BitmapImage
            using (var outputStream = new InMemoryRandomAccessStream())
            {
                await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
                outputStream.Seek(0);
                
                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(outputStream);
                return bitmapImage;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"处理默认头像失败: {ex.Message}");
            // 失败时返回简单的BitmapImage
            return new BitmapImage(new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png"));
        }
    }
    
    /// <summary>
    /// 加载当前活跃角色名称
    /// </summary>
    private void LoadCurrentProfileName()
    {
        try
        {
            // 获取角色数据文件路径
            string profilesFilePath = Path.Combine(_fileService.GetMinecraftDataPath(), "profiles.json");
            
            if (File.Exists(profilesFilePath))
            {
                // 🔒 使用安全方法读取（自动解密token）
                var profiles = XianYuLauncher.Core.Helpers.TokenEncryption.LoadProfilesSecurely(profilesFilePath);
                
                // 查找活跃角色
                var activeProfile = profiles.FirstOrDefault(p => p.IsActive) ?? profiles.FirstOrDefault();
                if (activeProfile != null)
                {
                    HostName = activeProfile.Name;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"加载角色信息失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 从meta API获取数据
    /// </summary>
    private async Task GetMetaDataAsync()
    {
        if (string.IsNullOrEmpty(_port)) return;
        
        try
        {
            string metaUrl = $"http://localhost:{_port}/meta";
            HttpResponseMessage response = await _httpClient.GetAsync(metaUrl);
            
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                
                // 解析JSON获取easytier_version
                using (JsonDocument doc = JsonDocument.Parse(content))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("easytier_version", out JsonElement easytierVersionElement) &&
                        easytierVersionElement.ValueKind == JsonValueKind.String)
                    {
                        EasyTierVersion = easytierVersionElement.GetString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"获取meta数据失败: {ex.Message}");
        }
    }
    
    [RelayCommand]
    private void Back()
    {
        // 结束后台的联机服务进程
        StopTerracottaProcess();
        
        // 导航回上一页
        _navigationService.GoBack();
    }
    
    /// <summary>
    /// 停止terracotta进程
    /// </summary>
    private async void StopTerracottaProcess()
    {
        try
        {
            // 尝试使用terracotta官方HTTP接口优雅关闭进程
            if (!string.IsNullOrEmpty(_port))
            {
                try
                {
                    // 首先尝试使用peaceful=true优雅退出
                    string panicUrl = $"http://localhost:{_port}/panic?peaceful=true";
                    HttpResponseMessage response = await _httpClient.GetAsync(panicUrl, CancellationToken.None);
                    Log.Information($"调用terracotta /panic接口结果：{response.StatusCode}");
                    
                    // 等待短暂时间，让进程有时间优雅退出
                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"调用terracotta /panic接口时发生错误：{ex.Message}");
                    // 可以尝试使用peaceful=false强制退出
                    try
                    {
                        string panicUrl = $"http://localhost:{_port}/panic?peaceful=false";
                        HttpResponseMessage response = await _httpClient.GetAsync(panicUrl, CancellationToken.None);
                        Log.Information($"调用terracotta /panic?peaceful=false接口结果：{response.StatusCode}");
                        await Task.Delay(2000);
                    }
                    catch (Exception ex2)
                    {
                        Log.Error(ex2, $"调用terracotta强制退出接口时发生错误：{ex2.Message}");
                    }
                }
            }
            
            // 检查是否还有剩余的terracotta进程，如果有则使用传统方式终止
            Process[] terracottaProcesses = Process.GetProcessesByName("terracotta-windows-x86_64");
            foreach (Process process in terracottaProcesses)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000); // 等待最多5秒
                    process.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"终止进程时发生错误：{ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"停止terracotta进程时发生错误：{ex.Message}");
        }
    }
}