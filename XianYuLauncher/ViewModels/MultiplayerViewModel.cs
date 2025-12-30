using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XMCL2025.Contracts.Services;
using XMCL2025.Contracts.ViewModels;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Helpers;
using Newtonsoft.Json;

namespace XMCL2025.ViewModels;

public partial class MultiplayerViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    
    // 保存启动的terracotta进程引用
    private Process? _terracottaProcess;
    
    // 保存临时文件路径，用于进程终止后清理
    private string? _tempFilePath;
    
    // HttpClient用于发送HTTP请求
    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    
    // CancellationTokenSource用于控制轮询的取消
    private CancellationTokenSource? _pollingCts;
    
    // 保存弹窗引用，用于在检测到房间时关闭
    private Microsoft.UI.Xaml.Controls.ContentDialog? _statusDialog;
    
    // 用于实时显示轮询结果的字段
    private string _pollingResult = "";
    
    // 用于显示状态的属性，绑定到弹窗内容
    public string PollingResult
    {
        get => _pollingResult;
        set => SetProperty(ref _pollingResult, value);
    }
    
    // FileService用于获取文件路径
    private readonly IFileService _fileService;
    
    // 角色信息类
    private class MinecraftProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
    }

    public MultiplayerViewModel(INavigationService navigationService, IFileService fileService)
    {
        _navigationService = navigationService;
        _fileService = fileService;
    }
    
    /// <summary>
    /// 获取当前活跃角色名称
    /// </summary>
    /// <returns>当前角色名称，如果没有找到则返回空字符串</returns>
    private string GetCurrentProfileName()
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
                    return activeProfile.Name;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载角色信息失败: {ex.Message}");
        }
        return string.Empty;
    }

    public void OnNavigatedFrom()
    {
        // TODO: 处理页面离开时的逻辑
    }

    public void OnNavigatedTo(object parameter)
    {
        // TODO: 处理页面加载时的逻辑
    }

    [RelayCommand]
    private async Task HostGame()
    {
        bool isSuccess = true;
        string errorMessage = string.Empty;
        string? port = null;

        // 先启动联机服务
        try
        {
            // 获取当前项目目录
            string appDirectory = AppContext.BaseDirectory;
            
            // 构建联机插件的路径
            string terracottaPath = Path.Combine(appDirectory, "plugins", "Terracotta", "terracotta-windows-x86_64.exe");
            
            // 检查文件是否存在
            if (File.Exists(terracottaPath))
            {
                // 使用系统临时目录
                string tempDir = Path.GetTempPath();
                
                // 生成时间戳
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                
                // 生成文件名，Port使用纯字母
                string tempFileName = $"terracotta-{timestamp}-Port.json";
                string tempFilePath = Path.Combine(tempDir, tempFileName);
                
                // 创建空的json文件
                File.WriteAllText(tempFilePath, "{}");
                
                // 保存临时文件路径
                _tempFilePath = tempFilePath;
                
                // 启动联机服务，添加--hmcl参数
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = terracottaPath,
                    Arguments = $"--hmcl \"{tempFilePath}\"",
                    UseShellExecute = false,
                    RedirectStandardInput = true
                };
                
                // 启动进程并保存引用
                _terracottaProcess = Process.Start(processStartInfo);
                
                // 等待短暂时间，让terracotta进程有时间写入端口信息
                await Task.Delay(1000);
                
                // 读取临时文件，获取端口号
                if (File.Exists(tempFilePath))
                {
                    string jsonContent = File.ReadAllText(tempFilePath);
                    // 解析json，获取port字段
                    using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.TryGetProperty("port", out JsonElement portElement))
                        {
                            port = portElement.ToString();
                        }
                    }
                }
            }
            else
            {
                isSuccess = false;
                errorMessage = "联机服务文件不存在，请检查plugins\\Terracotta目录下是否有terracotta-windows-x86_64.exe文件";
            }
        }
        catch (Exception ex)
        {
            isSuccess = false;
            errorMessage = $"启动联机服务时发生错误：{ex.Message}";
        }

        // 根据启动结果显示不同的弹窗
        if (isSuccess)
        {
            // 如果获取到端口号，启动轮询
            if (!string.IsNullOrEmpty(port))
            {
                // 初始化轮询结果
                PollingResult = "联机Page_GettingStatusText".GetLocalized();
                
                // 创建CancellationTokenSource
                _pollingCts = new CancellationTokenSource();
                
                // 启动轮询
                StartPolling(port, _pollingCts.Token);
            }
            
            // 创建弹窗
            _statusDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "联机Page_FindingRoomTitle".GetLocalized(),
                Content = this, // 绑定到当前ViewModel，以便显示PollingResult
                ContentTemplate = (DataTemplate)Microsoft.UI.Xaml.Application.Current.Resources["PollingContentTemplate"],
                PrimaryButtonText = "联机Page_StopButton".GetLocalized(),
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            
            // 处理停止按钮点击事件
            _statusDialog.PrimaryButtonClick += (sender, args) =>
            {
                StopTerracottaProcess();
            };
            
            // 处理对话框关闭事件，确保轮询停止
            _statusDialog.Closed += (sender, args) =>
            {
                StopPolling();
                _statusDialog = null; // 清除引用
            };
            
            await _statusDialog.ShowAsync();
        }
        else
        {
            // 显示错误信息
            var errorDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Common_ErrorTitle.Text".GetLocalized(),
                Content = errorMessage,
                CloseButtonText = "Common_OKButton".GetLocalized(),
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            
            await errorDialog.ShowAsync();
        }
    }
    
    /// <summary>
    /// 开始轮询状态
    /// </summary>
    /// <param name="port">端口号</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async void StartPolling(string port, CancellationToken cancellationToken)
    {
        try
        {
            // 获取当前启动器内选择的角色名
            string playerName = GetCurrentProfileName();
            
            string stateUrl = $"http://localhost:{port}/state";
            // 在扫描URL后面加上?player={当前角色名}
            string scanningUrl = $"http://localhost:{port}/state/scanning?player={Uri.EscapeDataString(playerName)}";
            
            // 首先发送一次请求到/scanning，用于设置当前为查询状态
            try
            {
                HttpResponseMessage scanningResponse = await _httpClient.GetAsync(scanningUrl, cancellationToken);
                if (scanningResponse.IsSuccessStatusCode)
                {
                    PollingResult = "联机Page_ScanningStatusSetText".GetLocalized();
                }
                else
                {
                    PollingResult = "联机Page_ScanningStatusFailedText".GetLocalized(scanningResponse.StatusCode);
                }
            }
            catch (Exception ex)
            {
                PollingResult = "联机Page_ScanningStatusErrorText".GetLocalized(ex.Message);
            }
            
            // 等待短暂时间，然后开始轮询
            await Task.Delay(500, cancellationToken);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 发送GET请求
                    HttpResponseMessage response = await _httpClient.GetAsync(stateUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        // 读取响应内容
                        string content = await response.Content.ReadAsStringAsync(cancellationToken);
                        // 更新轮询结果
                        PollingResult = content;
                        
                        // 解析JSON，检查index是否为3
                        try
                        {
                            using (JsonDocument doc = JsonDocument.Parse(content))
                            {
                                JsonElement root = doc.RootElement;
                                if (root.TryGetProperty("index", out JsonElement indexElement) && 
                                    indexElement.ValueKind == JsonValueKind.Number && 
                                    indexElement.GetInt32() == 3)
                                {
                                    // 检测到index为3，检查是否有room字段
                                    if (root.TryGetProperty("room", out JsonElement roomElement) && 
                                        roomElement.ValueKind == JsonValueKind.String)
                                    {
                                        string roomId = roomElement.GetString();
                                        if (!string.IsNullOrEmpty(roomId))
                                        {
                                            // 停止轮询
                                            StopPolling();
                                            
                                            // 关闭弹窗
                                            if (_statusDialog != null)
                                            {
                                                _statusDialog.Hide();
                                                _statusDialog = null;
                                            }
                                            
                                            // 导航到联机大厅页面，传递端口和房间ID信息
                                            _navigationService.NavigateTo(typeof(MultiplayerLobbyViewModel).FullName, new { RoomId = roomId, Port = port });
                                            
                                            // 退出循环
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            // JSON解析错误，忽略
                            System.Diagnostics.Debug.WriteLine($"解析轮询结果JSON错误: {ex.Message}");
                        }                        
                    }                        
                    else
                    {
                        PollingResult = "联机Page_PollingFailedText".GetLocalized(response.StatusCode);
                    }
                }
                catch (HttpRequestException ex)
                {
                    PollingResult = "联机Page_NetworkErrorText".GetLocalized(ex.Message);
                }
                catch (Exception ex)
                {
                    PollingResult = "联机Page_ErrorText".GetLocalized(ex.Message);
                }
                
                // 等待1秒后再次轮询
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 轮询被取消，忽略
        }
    }
    
    /// <summary>
    /// 停止轮询
    /// </summary>
    private void StopPolling()
    {
        if (_pollingCts != null)
        {
            _pollingCts.Cancel();
            _pollingCts.Dispose();
            _pollingCts = null;
        }
    }
    
    /// <summary>
    /// 停止terracotta进程
    /// </summary>
    private void StopTerracottaProcess()
    {
        try
        {
            // 首先停止轮询
            StopPolling();
            
            // 首先尝试通过保存的进程引用终止
            if (_terracottaProcess != null && !_terracottaProcess.HasExited)
            {
                _terracottaProcess.Kill();
                _terracottaProcess.WaitForExit(5000); // 等待最多5秒
                _terracottaProcess.Dispose();
                _terracottaProcess = null;
            }
            
            // 如果直接引用终止失败，通过进程名查找并终止所有terracotta进程
            Process[] terracottaProcesses = Process.GetProcessesByName("terracotta-windows-x86_64");
            foreach (Process process in terracottaProcesses)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                    process.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"终止进程时发生错误：{ex.Message}");
                }
            }
            
            // 清理临时文件
            if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
                _tempFilePath = null;
            }
        }
        catch (Exception ex)
        {
            // 这里可以添加日志记录，但不显示给用户，避免影响体验
            System.Diagnostics.Debug.WriteLine($"停止terracotta进程时发生错误：{ex.Message}");
        }
    }
    
    [RelayCommand]
    private async Task JoinGame()
    {
        // 1. 弹出输入房间号的弹窗
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "联机Page_InputRoomIdTitle".GetLocalized(),
            Content = new TextBox 
            { 
                PlaceholderText = "联机Page_RoomIdPlaceholder".GetLocalized(),
                Width = 300,
                Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0)
            },
            PrimaryButtonText = "联机Page_ConfirmButton".GetLocalized(),
            SecondaryButtonText = "联机Page_CancelButton".GetLocalized(),
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        var result = await dialog.ShowAsync();
        
        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary && 
            dialog.Content is TextBox textBox && 
            !string.IsNullOrWhiteSpace(textBox.Text))
        {
            string roomId = textBox.Text;
            
            // 2. 启动客户端进程
            bool isSuccess = true;
            string? port = null;
            
            try
            {
                // 获取当前项目目录
                string appDirectory = AppContext.BaseDirectory;
                
                // 构建联机插件的路径
                string terracottaPath = Path.Combine(appDirectory, "plugins", "Terracotta", "terracotta-windows-x86_64.exe");
                
                // 检查文件是否存在
                if (File.Exists(terracottaPath))
                {
                    // 使用系统临时目录
                    string tempDir = Path.GetTempPath();
                    
                    // 生成时间戳
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                    
                    // 生成文件名，Port使用纯字母
                    string tempFileName = $"terracotta-{timestamp}-Port.json";
                    string tempFilePath = Path.Combine(tempDir, tempFileName);
                    
                    // 创建空的json文件
                    File.WriteAllText(tempFilePath, "{}");
                    
                    // 保存临时文件路径，用于进程终止后清理
                    _tempFilePath = tempFilePath;
                    
                    // 启动联机服务，添加--hmcl参数
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = terracottaPath,
                        Arguments = $"--hmcl \"{tempFilePath}\"",
                        UseShellExecute = false,
                        RedirectStandardInput = true
                    };
                    
                    // 启动进程并保存引用
                    _terracottaProcess = Process.Start(processStartInfo);
                    
                    // 等待短暂时间，让terracotta进程有时间写入端口信息
                    await Task.Delay(1000);
                    
                    // 读取临时文件，获取端口号
                    if (File.Exists(tempFilePath))
                    {
                        string jsonContent = File.ReadAllText(tempFilePath);
                        // 解析json，获取port字段
                        using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                        {
                            JsonElement root = doc.RootElement;
                            if (root.TryGetProperty("port", out JsonElement portElement))
                            {
                                port = portElement.ToString();
                            }
                        }
                    }
                }
                else
                {
                    isSuccess = false;
                    await ShowErrorDialogAsync("联机Page_TerracottaNotFoundError".GetLocalized());
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
                await ShowErrorDialogAsync($"启动联机服务时发生错误：{ex.Message}");
            }
            
            if (isSuccess && !string.IsNullOrEmpty(port))
            {
                // 3. 访问http://localhost:{端口}/state/guesting?room=房间号&player=角色名
                try
                {
                    // 获取当前启动器内选择的角色名
                    string playerName = GetCurrentProfileName();
                    // 构建包含玩家名的URL
                    string guestingUrl = $"http://localhost:{port}/state/guesting?room={roomId}&player={Uri.EscapeDataString(playerName)}";
                    HttpResponseMessage response = await _httpClient.GetAsync(guestingUrl);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        // 400表示错误，通知玩家房间不存在
                        await ShowErrorDialogAsync("联机Page_RoomNotFoundError".GetLocalized());
                        // 停止进程
                        StopTerracottaProcess();
                        return;
                    }
                    
                    // 4. 轮询访问http://localhost:{端口}/state，直到state为guest-ok
                    bool isGuestOk = false;
                    string url = string.Empty;
                    
                    for (int i = 0; i < 30; i++) // 最多尝试30次，每次间隔1秒
                    {
                        try
                        {
                            string stateUrl = $"http://localhost:{port}/state";
                            HttpResponseMessage stateResponse = await _httpClient.GetAsync(stateUrl);
                            
                            if (stateResponse.IsSuccessStatusCode)
                            {
                                string content = await stateResponse.Content.ReadAsStringAsync();
                                
                                // 解析JSON
                                using (JsonDocument doc = JsonDocument.Parse(content))
                                {
                                    JsonElement root = doc.RootElement;
                                    if (root.TryGetProperty("state", out JsonElement stateElement) && 
                                        stateElement.ValueKind == JsonValueKind.String &&
                                        stateElement.GetString() == "guest-ok")
                                    {
                                        // 获取url字段
                                        if (root.TryGetProperty("url", out JsonElement urlElement) &&
                                            urlElement.ValueKind == JsonValueKind.String)
                                        {
                                            url = urlElement.GetString();
                                            isGuestOk = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"获取状态失败: {ex.Message}");
                        }
                        
                        // 等待1秒后再次尝试
                        await Task.Delay(1000);
                    }
                    
                    if (isGuestOk && !string.IsNullOrEmpty(url))
                    {
                        // 5. 导航至联机大厅页，传递房间号、端口和房客标识
                    _navigationService.NavigateTo(typeof(MultiplayerLobbyViewModel).FullName, new { 
                            RoomId = roomId, 
                            Port = port, 
                            IsGuest = true, 
                            Url = url 
                        });
                    }
                    else
                    {
                        await ShowErrorDialogAsync("联机Page_JoinFailedError".GetLocalized());
                        // 停止进程
                        StopTerracottaProcess();
                    }
                }
                catch (Exception ex)
                {
                    await ShowErrorDialogAsync($"连接房间时发生错误：{ex.Message}");
                    // 停止进程
                    StopTerracottaProcess();
                }
            }
        }
    }
    
    /// <summary>
    /// 显示错误对话框
    /// </summary>
    private async Task ShowErrorDialogAsync(string message)
    {
        var errorDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "Common_ErrorTitle.Text".GetLocalized(),
            Content = message,
            CloseButtonText = "Common_OKButton".GetLocalized(),
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        await errorDialog.ShowAsync();
    }
}