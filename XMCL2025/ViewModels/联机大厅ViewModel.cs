using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using XMCL2025.Contracts.Services;
using XMCL2025.Contracts.ViewModels;
using XMCL2025.Core.Contracts.Services;

namespace XMCL2025.ViewModels;

public partial class 联机大厅ViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    
    // 计时器
    private readonly System.Timers.Timer _timer = new System.Timers.Timer(1000); // 每秒触发一次
    private TimeSpan _elapsedTime = TimeSpan.Zero;
    
    // 房间信息
    [ObservableProperty]
    private string _roomId = "";
    
    [ObservableProperty]
    private string _elapsedTimeText = "00:00:00";
    
    [ObservableProperty]
    private string _hostName = "当前玩家";
    
    [ObservableProperty]
    private string _easyTierVersion = "";
    
    // 端口信息，用于获取meta数据
    private string? _port;
    
    // HttpClient用于获取meta数据
    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    
    // FileService用于获取文件路径
    private readonly IFileService _fileService;
    
    // 角色信息类
    private class MinecraftProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
    }
    
    public 联机大厅ViewModel(INavigationService navigationService, IFileService fileService)
    {
        _navigationService = navigationService;
        _fileService = fileService;
        
        // 初始化计时器
        _timer.Elapsed += OnTimerElapsed;
        
        // 加载当前活跃角色名称
        LoadCurrentProfileName();
    }
    
    private void OnTimerElapsed(object sender, ElapsedEventArgs e)
    {
        _elapsedTime += TimeSpan.FromSeconds(1);
        ElapsedTimeText = _elapsedTime.ToString(@"hh:mm:ss");
    }
    
    public void OnNavigatedFrom()
    {
        // 停止计时器
        _timer.Stop();
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        // 启动计时器
        _timer.Start();
        
        // 如果参数是匿名对象，获取RoomId和Port
        if (parameter != null)
        {
            var paramType = parameter.GetType();
            var roomIdProp = paramType.GetProperty("RoomId");
            var portProp = paramType.GetProperty("Port");
            
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
                }
            }
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
                string json = File.ReadAllText(profilesFilePath);
                var profiles = JsonConvert.DeserializeObject<List<MinecraftProfile>>(json) ?? new List<MinecraftProfile>();
                
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
            System.Diagnostics.Debug.WriteLine($"加载角色信息失败: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"获取meta数据失败: {ex.Message}");
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
    private void StopTerracottaProcess()
    {
        try
        {
            // 通过进程名查找并终止所有terracotta进程
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
                    System.Diagnostics.Debug.WriteLine($"终止进程时发生错误：{ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"停止terracotta进程时发生错误：{ex.Message}");
        }
    }
}