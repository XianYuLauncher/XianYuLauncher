using System;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XMCL2025.Contracts.Services;
using XMCL2025.Contracts.ViewModels;

namespace XMCL2025.ViewModels;

public partial class 联机ViewModel : ObservableRecipient, INavigationAware
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

    public 联机ViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
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
                PollingResult = "正在获取状态...";
                
                // 创建CancellationTokenSource
                _pollingCts = new CancellationTokenSource();
                
                // 启动轮询
                StartPolling(port, _pollingCts.Token);
            }
            
            // 创建弹窗
            _statusDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "正在寻找房间",
                Content = this, // 绑定到当前ViewModel，以便显示PollingResult
                ContentTemplate = (DataTemplate)Microsoft.UI.Xaml.Application.Current.Resources["PollingContentTemplate"],
                PrimaryButtonText = "停止",
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
                Title = "错误",
                Content = errorMessage,
                CloseButtonText = "确定",
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
            string stateUrl = $"http://localhost:{port}/state";
            string scanningUrl = $"http://localhost:{port}/state/scanning";
            
            // 首先发送一次请求到/scanning，用于设置当前为查询状态
            try
            {
                HttpResponseMessage scanningResponse = await _httpClient.GetAsync(scanningUrl, cancellationToken);
                if (scanningResponse.IsSuccessStatusCode)
                {
                    PollingResult = "已设置为查询状态，开始轮询...";
                }
                else
                {
                    PollingResult = $"设置查询状态失败: {scanningResponse.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                PollingResult = $"设置查询状态时发生错误: {ex.Message}";
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
                                            _navigationService.NavigateTo(typeof(联机大厅ViewModel).FullName, new { RoomId = roomId, Port = port });
                                            
                                            // 退出循环
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            // JSON解析错误，忽略
                            System.Diagnostics.Debug.WriteLine($"解析轮询结果JSON错误: {ex.Message}");
                        }
                    }
                    else
                    {
                        PollingResult = $"请求失败: {response.StatusCode}";
                    }
                }
                catch (HttpRequestException ex)
                {
                    PollingResult = $"网络错误: {ex.Message}";
                }
                catch (Exception ex)
                {
                    PollingResult = $"错误: {ex.Message}";
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
    private void JoinGame()
    {
        // TODO: 实现当房客的逻辑
    }
}