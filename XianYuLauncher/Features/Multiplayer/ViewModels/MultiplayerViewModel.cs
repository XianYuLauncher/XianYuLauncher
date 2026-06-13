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
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Features.Multiplayer.Models;
using XianYuLauncher.Helpers;
using Newtonsoft.Json;
using Serilog;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.Multiplayer.ViewModels;

public partial class MultiplayerViewModel : ObservableRecipient, INavigationAware, IPageHeaderAware
{
    private readonly INavigationService _navigationService;
    private readonly TerracottaService _terracottaService;
    
    // 保存启动的 terracotta 进程引用
    private Process? _terracottaProcess;
    
    // 保存临时文件路径，用于进程终止后清理
    private string? _tempFilePath;
    
    // HttpClient 用于发送 HTTP 请求
    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    
    // CancellationTokenSource 用于控制轮询的取消
    private CancellationTokenSource? _pollingCts;
    // 用于 JoinGame 方法的 CancellationTokenSource
    private CancellationTokenSource? _joinGameCts;
    
    // 保存陶瓦联机进程的端口号，用于发送 HTTP 请求
    private string? _terracottaPort;
    
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
    
    // FileService 用于获取文件路径
    private readonly IFileService _fileService;
    private readonly ICommonDialogService _dialogService;
    private readonly IProgressDialogService _progressDialogService;

    public PageHeaderMetadata HeaderMetadata { get; } = new();

    public PageHeaderPresentationMode HeaderPresentationMode => PageHeaderPresentationMode.ControlStrip;

    public event EventHandler<MultiplayerLobbyNavigationParameter>? LobbyNavigationRequested;

    public MultiplayerViewModel(
        INavigationService navigationService,
        IFileService fileService,
        TerracottaService terracottaService,
        ICommonDialogService dialogService,
        IProgressDialogService progressDialogService)
    {
        _navigationService = navigationService;
        _fileService = fileService;
        _terracottaService = terracottaService;
        _dialogService = dialogService;
        _progressDialogService = progressDialogService;

        HeaderMetadata.Title = "MultiplayerPage_HeaderTitle".GetLocalized();
        HeaderMetadata.Subtitle = "MultiplayerPage_HeaderSubtitle".GetLocalized();
    }

    private static ProcessStartInfo CreateTerracottaProcessStartInfo(string terracottaDirectory, string terracottaPath, string tempFilePath)
    {
        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = TerracottaLaunchCommandHelper.BuildHmclStartupCommandArguments(
                terracottaDirectory,
                Path.GetFileName(terracottaPath),
                tempFilePath),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = terracottaDirectory
        };
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
            string profilesFilePath = Path.Combine(_fileService.GetMinecraftDataPath(), MinecraftFileConsts.AccountsJson);

            // 🔒 使用安全方法读取（自动解密 token）
            var profiles = XianYuLauncher.Core.Helpers.TokenEncryption.LoadAccountsSecurely(profilesFilePath);
            
            // 查找活跃角色
            var activeProfile = profiles.FirstOrDefault(p => p.IsActive) ?? profiles.FirstOrDefault();
            if (activeProfile != null)
            {
                return activeProfile.Name;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"加载角色信息失败: {ex.Message}");
        }
        return string.Empty;
    }

    public void OnNavigatedFrom()
    {
        //处理页面离开时的逻辑
    }

    public void OnNavigatedTo(object parameter)
    {
        //处理页面加载时的逻辑
    }

    private MultiplayerLobbyNavigationParameter CreateLobbyNavigationParameter(
        string roomId,
        string? port,
        bool isGuest,
        string url)
    {
        return new MultiplayerLobbyNavigationParameter
        {
            RoomId = roomId,
            Port = port,
            IsGuest = isGuest,
            Url = url,
            BreadcrumbRoot = BreadcrumbNavigationRoot.CreateLocal(
                HeaderMetadata.Title,
                new LocalNavigationTarget
                {
                    RouteKey = MultiplayerNavigationRouteKeys.Root,
                }),
        };
    }

    private void RequestLobbyNavigation(MultiplayerLobbyNavigationParameter navigationParameter)
    {
        LobbyNavigationRequested?.Invoke(this, navigationParameter);
    }

    [RelayCommand]
    private async Task HostGame()
    {
        bool isSuccess = true;
        string errorMessage = string.Empty;
        string? port = null;
        string? terracottaPath = null;
        
        try
        {
            terracottaPath = await _progressDialogService.ShowProgressCallbackDialogAsync(
                "MultiplayerPage_DownloadingTerracottaTitle".GetLocalized(),
                "MultiplayerPage_DownloadingTerracottaMessage".GetLocalized(),
                async (progress) => await _terracottaService.EnsureTerracottaAsync(p => progress.Report(p)));
            
            // 检查陶瓦插件是否成功获取
            if (!string.IsNullOrEmpty(terracottaPath) && File.Exists(terracottaPath))
            {
                // 获取真实的物理路径（非虚拟化路径）
                // 已经在 TerracottaService 中确保返回的是 SafeAppDataPath (LocalState)
                // 所以这里无需再做任何复杂的转换逻辑，直接使用即可
                string realTerracottaDir = Path.GetDirectoryName(terracottaPath) ?? AppEnvironment.SafeAppDataPath;
                
                // 临时文件目录
                string realTempDir = Path.Combine(realTerracottaDir, "temp");
                
                // 确保目录存在
                Directory.CreateDirectory(realTempDir);
                
                Log.Information($"[Multiplayer] Terracotta 目录: {realTerracottaDir}");
                Log.Information($"[Multiplayer] 临时文件目录: {realTempDir}");
                
                // 生成时间戳
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                
                // 生成文件名
                string tempFileName = $"terracotta-{timestamp}-Port.json";
                string tempFilePath = Path.Combine(realTempDir, tempFileName);
                
                Log.Information($"[Multiplayer] 临时文件完整路径: {tempFilePath}");
                
                // 创建空的 json 文件
                File.WriteAllText(tempFilePath, "{}");
                
                // 保存临时文件路径
                _tempFilePath = tempFilePath;
                
                // 启动联机服务，添加--hmcl 参数
                // 使用真实物理路径启动
                string realExePath = Path.Combine(realTerracottaDir, Path.GetFileName(terracottaPath));

                var processStartInfo = CreateTerracottaProcessStartInfo(realTerracottaDir, terracottaPath, tempFilePath);
                
                Log.Information($"[Multiplayer] 准备启动 Terracotta (通过 CMD)");
                Log.Information($"[Multiplayer] 真实可执行文件: {realExePath}");
                Log.Information($"[Multiplayer] 真实工作目录: {realTerracottaDir}");
                Log.Information($"[Multiplayer] CMD 命令: {processStartInfo.Arguments}");
                Log.Information($"[Multiplayer] 真实临时文件路径: {tempFilePath}");
                
                // 启动进程并保存引用
                _terracottaProcess = Process.Start(processStartInfo);
                
                if (_terracottaProcess == null)
                {
                    Log.Error($"[Multiplayer] 错误：Process.Start 返回 null");
                    isSuccess = false;
                    errorMessage = "无法启动 Terracotta 进程";
                    goto ShowResult;
                }
                
                // 读取输出
                _terracottaProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Log.Information($"[Terracotta CMD Output] {e.Data}");
                    }
                };
                _terracottaProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Log.Error($"[Terracotta CMD Error] {e.Data}");
                    }
                };
                _terracottaProcess.BeginOutputReadLine();
                _terracottaProcess.BeginErrorReadLine();
                
                Log.Information($"[Multiplayer] CMD 进程已启动，PID: {_terracottaProcess?.Id}");
                
                // 注意：_terracottaProcess 是 CMD 进程，不是 Terracotta 进程
                // CMD 会启动 Terracotta 后立即退出，这是正常的
                // 我们只需要等待文件被写入即可
                
                // 等待并多次尝试读取端口信息
                int maxRetries = 20; // 增加到 20 次
                for (int i = 0; i < maxRetries; i++)
                {
                    await Task.Delay(500); // 每次等待 500ms
                    
                    // 尝试读取临时文件
                    if (File.Exists(tempFilePath))
                    {
                        try
                        {
                            string jsonContent = File.ReadAllText(tempFilePath);
                            Log.Information($"[Multiplayer] 尝试 {i + 1}/{maxRetries}，文件内容: {jsonContent}");
                            
                            // 检查文件是否为空或只有空对象
                            if (string.IsNullOrWhiteSpace(jsonContent) || jsonContent.Trim() == "{}")
                            {
                                Log.Information($"[Multiplayer] 文件内容为空，继续等待...");
                                
                                // 在第 5 次尝试时，检查进程是否真的在运行
                                if (i == 4 && _terracottaProcess != null)
                                {
                                    Log.Information($"[Multiplayer] 进程状态检查：");
                                    Log.Information($"  - HasExited: {_terracottaProcess.HasExited}");
                                    Log.Information($"  - Responding: {_terracottaProcess.Responding}");
                                    try
                                    {
                                        Log.Information($"  - WorkingSet64: {_terracottaProcess.WorkingSet64} bytes");
                                        Log.Information($"  - Threads: {_terracottaProcess.Threads.Count}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, $"  - 无法获取进程详细信息: {ex.Message}");
                                    }
                                }
                                
                                continue;
                            }
                            
                            // 解析 json，获取 port 字段
                            using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                            {
                                JsonElement root = doc.RootElement;
                                if (root.TryGetProperty("port", out JsonElement portElement))
                                {
                                    port = portElement.ToString();
                                    _terracottaPort = port; // 保存端口号
                                    Log.Information($"[Multiplayer] 成功获取端口号: {port}");
                                    
                                    // 找到真正的 Terracotta 进程并保存引用
                                    try
                                    {
                                        var terracottaProcesses = Process.GetProcessesByName("terracotta-0.4.1-windows-x86_64");
                                        if (terracottaProcesses.Length > 0)
                                        {
                                            _terracottaProcess = terracottaProcesses[0];
                                            Log.Information($"[Multiplayer] 找到 Terracotta 进程，PID: {_terracottaProcess.Id}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, $"[Multiplayer] 查找 Terracotta 进程失败: {ex.Message}");
                                    }
                                    
                                    break; // 成功获取端口，退出循环
                                }
                                else
                                {
                                    Log.Warning($"[Multiplayer] JSON 中没有 port 字段");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"[Multiplayer] 读取/解析文件失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        // 只在第一次和最后一次尝试时记录日志，避免刷屏
                        if (i == 0 || i == maxRetries - 1)
                        {
                            Log.Warning($"[Multiplayer] 尝试 {i + 1}/{maxRetries}，临时文件不存在");
                        }
                    }
                }
                
                // 检查是否成功获取端口
                if (string.IsNullOrEmpty(port))
                {
                    Log.Error($"[Multiplayer] 超时：未能获取端口号");
                    isSuccess = false;
                    errorMessage = "Dialog_Multiplayer_PortTimeout_Content".GetLocalized();
                    
                    // 终止进程
                    if (_terracottaProcess != null && !_terracottaProcess.HasExited)
                    {
                        _terracottaProcess.Kill();
                        _terracottaProcess.Dispose();
                        _terracottaProcess = null;
                    }
                    
                    goto ShowResult;
                }
            }
            else
            {
                isSuccess = false;
                errorMessage = "Dialog_Multiplayer_ServiceUnavailable".GetLocalized();
            }
        }
        catch (Exception ex)
        {
            isSuccess = false;
            errorMessage = "Dialog_Multiplayer_StartFailed_Format".GetLocalized(ex.Message);
        }

        ShowResult: // 标签，用于 goto 跳转
        // 根据启动结果显示不同的弹窗
        if (isSuccess)
        {
            // 如果获取到端口号，启动轮询
            if (!string.IsNullOrEmpty(port))
            {
                // 初始化轮询结果
                PollingResult = "MultiplayerPage_GettingStatusText".GetLocalized();
                
                // 创建 CancellationTokenSource
                _pollingCts = new CancellationTokenSource();
                
                // 启动轮询
                StartPolling(port, _pollingCts.Token);
            }
            
            // 创建弹窗
            _statusDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "MultiplayerPage_FindingRoomTitle".GetLocalized(),
                Content = this, // 绑定到当前 ViewModel，以便显示 PollingResult
                ContentTemplate = (DataTemplate)Microsoft.UI.Xaml.Application.Current.Resources["PollingContentTemplate"],
                PrimaryButtonText = "MultiplayerPage_StopButton".GetLocalized(),
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                DefaultButton = ContentDialogButton.None
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
                _statusDialog = null;
            };
            
            await _dialogService.ShowDialogAsync(_statusDialog);
        }
        else
        {
            await _dialogService.ShowMessageDialogAsync(
                "Common_ErrorTitle".GetLocalized(),
                errorMessage,
                "Common_OKButton".GetLocalized());
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
            // 在扫描 URL 后面加上?player={当前角色名}
            string scanningUrl = $"http://localhost:{port}/state/scanning?player={Uri.EscapeDataString(playerName)}";
            
            // 首先发送一次请求到/scanning，用于设置当前为查询状态
            try
            {
                Log.Information($"[Multiplayer] 发送扫描请求: {scanningUrl}");
                HttpResponseMessage scanningResponse = await _httpClient.GetAsync(scanningUrl, cancellationToken);
                if (scanningResponse.IsSuccessStatusCode)
                {
                    Log.Information("[Multiplayer] 扫描请求成功");
                    PollingResult = "MultiplayerPage_ScanningStatusSetText".GetLocalized();
                }
                else
                {
                    Log.Warning($"[Multiplayer] 扫描请求失败: {scanningResponse.StatusCode}");
                    PollingResult = "MultiplayerPage_ScanningStatusFailedText".GetLocalized(scanningResponse.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[Multiplayer] 扫描请求异常: {ex.Message}");
                PollingResult = "MultiplayerPage_ScanningStatusErrorText".GetLocalized(ex.Message);
            }
            
            // 等待短暂时间，然后开始轮询
            await Task.Delay(500, cancellationToken);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 发送 GET 请求
                    HttpResponseMessage response = await _httpClient.GetAsync(stateUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        // 读取响应内容
                        string content = await response.Content.ReadAsStringAsync(cancellationToken);
                        // 更新轮询结果
                        PollingResult = content;
                        
                        // 解析 JSON，检查 index 是否为 3
                        try
                        {
                            using (JsonDocument doc = JsonDocument.Parse(content))
                            {
                                JsonElement root = doc.RootElement;
                                if (root.TryGetProperty("index", out JsonElement indexElement) && 
                                    indexElement.ValueKind == JsonValueKind.Number && 
                                    indexElement.GetInt32() == 3)
                                {
                                    // 检测到 index 为 3，检查是否有 room 字段
                                    if (root.TryGetProperty("room", out JsonElement roomElement) && 
                                        roomElement.ValueKind == JsonValueKind.String)
                                    {
                                        string roomId = roomElement.GetString() ?? string.Empty;
                                        if (!string.IsNullOrEmpty(roomId))
                                        {
                                            Log.Information($"[Multiplayer] 成功创建/加入房间: {roomId}");
                                            
                                            // 停止轮询
                                            StopPolling();
                                            
                                            // 关闭弹窗
                                            if (_statusDialog != null)
                                            {
                                                _statusDialog.Hide();
                                                _statusDialog = null;
                                            }
                                            
                                            // 导航到联机大厅页面，传递端口和房间 ID 信息
                                            RequestLobbyNavigation(CreateLobbyNavigationParameter(
                                                roomId,
                                                port,
                                                isGuest: false,
                                                url: string.Empty));
                                            
                                            // 退出循环
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            // JSON 解析错误，忽略
                            Log.Error(ex, $"解析轮询结果 JSON 错误: {ex.Message}");
                        }                        
                    }                        
                    else
                    {
                        Log.Warning($"[Multiplayer] 轮询状态失败: {response.StatusCode}");
                        PollingResult = "MultiplayerPage_PollingFailedText".GetLocalized(response.StatusCode);
                    }
                }
                catch (HttpRequestException ex)
                {
                    Log.Error(ex, $"[Multiplayer] 轮询网络错误: {ex.Message}");
                    PollingResult = "MultiplayerPage_NetworkErrorText".GetLocalized(ex.Message);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"[Multiplayer] 轮询未捕获异常: {ex.Message}");
                    PollingResult = "MultiplayerPage_ErrorText".GetLocalized(ex.Message);
                }
                
                // 等待 1 秒后再次轮询
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            // 轮询被取消，忽略
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
    /// 停止 terracotta 进程
    /// </summary>
    private async void StopTerracottaProcess()
    {
        try
        {
            // 首先停止轮询
            StopPolling();
            
            // 取消 JoinGame 方法中的异步操作
            if (_joinGameCts != null)
            {
                _joinGameCts.Cancel();
                _joinGameCts.Dispose();
                _joinGameCts = null;
            }
            
            // 尝试使用 terracotta 官方 HTTP 接口优雅关闭进程
            if (!string.IsNullOrEmpty(_terracottaPort))
            {
                try
                {
                    // 首先尝试使用 peaceful=true 优雅退出
                    string panicUrl = $"http://localhost:{_terracottaPort}/panic?peaceful=true";
                    HttpResponseMessage response = await _httpClient.GetAsync(panicUrl, CancellationToken.None);
                    Log.Information($"调用 terracotta /panic 接口结果：{response.StatusCode}");
                    
                    // 等待短暂时间，让进程有时间优雅退出
                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"调用 terracotta /panic 接口时发生错误：{ex.Message}");
                    // 可以尝试使用 peaceful=false 强制退出
                    try
                    {
                        string panicUrl = $"http://localhost:{_terracottaPort}/panic?peaceful=false";
                        HttpResponseMessage response = await _httpClient.GetAsync(panicUrl, CancellationToken.None);
                        Log.Information($"调用 terracotta /panic?peaceful=false 接口结果：{response.StatusCode}");
                        await Task.Delay(2000);
                    }
                    catch (Exception ex2)
                    {
                        Log.Error(ex2, $"调用 terracotta 强制退出接口时发生错误：{ex2.Message}");
                    }
                }
            }
            
            // 验证进程是否已退出，如果没有则使用传统方式终止
            if (_terracottaProcess != null && !_terracottaProcess.HasExited)
            {
                _terracottaProcess.Kill();
                _terracottaProcess.WaitForExit(5000); // 等待最多 5 秒
                _terracottaProcess.Dispose();
                _terracottaProcess = null;
            }
            
            // 检查是否还有剩余的 terracotta 进程，如果有则终止
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
                    Log.Error(ex, $"终止进程时发生错误：{ex.Message}");
                }
            }
            
            // 清理临时文件
            if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
                _tempFilePath = null;
            }
            
            // 清理端口号
            _terracottaPort = null;
        }
        catch (Exception ex)
        {
            // 这里可以添加日志记录，但不显示给用户，避免影响体验
            Log.Error(ex, $"停止 terracotta 进程时发生错误：{ex.Message}");
        }
    }
    
    [RelayCommand]
    private async Task JoinGame()
    {
        // 1. 弹出输入房间号的弹窗
        var roomId = await _dialogService.ShowTextInputDialogAsync(
            "MultiplayerPage_InputRoomIdTitle".GetLocalized(),
            "MultiplayerPage_RoomIdPlaceholder".GetLocalized(),
            "MultiplayerPage_ConfirmButton".GetLocalized(),
            "MultiplayerPage_CancelButton".GetLocalized());
        
        if (!string.IsNullOrWhiteSpace(roomId))
        {
            // 2. 启动客户端进程
            bool isSuccess = true;
            string? port = null;
            string? terracottaPath = null;
            
            try
            {
                terracottaPath = await _progressDialogService.ShowProgressCallbackDialogAsync(
                    "MultiplayerPage_DownloadingTerracottaTitle".GetLocalized(),
                    "MultiplayerPage_DownloadingTerracottaMessage".GetLocalized(),
                    async (progress) => await _terracottaService.EnsureTerracottaAsync(p => progress.Report(p)));
                
                // 检查陶瓦插件是否成功获取
                if (!string.IsNullOrEmpty(terracottaPath) && File.Exists(terracottaPath))
                {
                    // 获取真实的物理路径（与 HostGame 相同的逻辑）
                    string terracottaDir = Path.GetDirectoryName(terracottaPath) ?? AppEnvironment.SafeAppDataPath;
                    string realTerracottaDir = terracottaDir;
                    string realTempDir;
                    
                    // 检查原路径是否存在
                    bool originalExists = Directory.Exists(terracottaDir);
                    
                    if (!originalExists)
                    {
                        // 原路径不存在，尝试转换
                        if (!terracottaDir.Contains("Packages"))
                        {
                            // 真实路径 -> 沙盒路径
                            try
                            {
                                if (!AppEnvironment.HasPackageIdentity)
                                {
                                    throw new InvalidOperationException("当前运行环境不存在沙盒 LocalState 路径。");
                                }

                                string packagePath = AppEnvironment.SafeAppDataPath;
                                int localStateIndex = packagePath.LastIndexOf("LocalState", StringComparison.OrdinalIgnoreCase);
                                if (localStateIndex < 0)
                                {
                                    throw new InvalidOperationException("无法从当前应用数据路径推导 Packages 根目录。");
                                }

                                string packagesRoot = packagePath[..localStateIndex];
                                string sandboxPath = Path.Combine(packagesRoot, "LocalCache", "Local", "XianYuLauncher", "terracotta");
                                if (Directory.Exists(sandboxPath) || File.Exists(Path.Combine(sandboxPath, Path.GetFileName(terracottaPath))))
                                {
                                    realTerracottaDir = sandboxPath;
                                    realTempDir = Path.Combine(packagesRoot, "LocalCache", "Local", "XianYuLauncher", "temp");
                                }
                                else
                                {
                                    realTempDir = Path.Combine(Path.GetDirectoryName(realTerracottaDir) ?? realTerracottaDir, "temp");
                                }
                            }
                            catch
                            {
                                realTempDir = Path.Combine(Path.GetDirectoryName(realTerracottaDir) ?? realTerracottaDir, "temp");
                            }
                        }
                        else
                        {
                            // 沙盒路径 -> 真实路径
                            int localCacheIndex = terracottaDir.IndexOf("LocalCache\\Local\\");
                            if (localCacheIndex > 0)
                            {
                                string relativePath = terracottaDir.Substring(localCacheIndex + "LocalCache\\Local\\".Length);
                                string realPath = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                    "AppData", "Local", relativePath);
                                if (Directory.Exists(realPath))
                                {
                                    realTerracottaDir = realPath;
                                }
                            }
                            realTempDir = Path.Combine(Path.GetDirectoryName(realTerracottaDir) ?? realTerracottaDir, "temp");
                        }
                    }
                    else
                    {
                        realTempDir = Path.Combine(Path.GetDirectoryName(realTerracottaDir) ?? realTerracottaDir, "temp");
                    }
                    
                    Directory.CreateDirectory(realTempDir);
                    
                    // 生成时间戳
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                    string tempFileName = $"terracotta-{timestamp}-Port.json";
                    string tempFilePath = Path.Combine(realTempDir, tempFileName);
                    
                    // 创建空的 json 文件
                    File.WriteAllText(tempFilePath, "{}");
                    
                    // 保存临时文件路径，用于进程终止后清理
                    _tempFilePath = tempFilePath;

                    // 加入房间时只启动本地 Terracotta 服务，房间号通过后续 HTTP 请求传递
                    var processStartInfo = CreateTerracottaProcessStartInfo(realTerracottaDir, terracottaPath, tempFilePath);

                    Log.Information($"[Multiplayer-Join] 准备启动 Terracotta 本地服务");
                    Log.Information($"[Multiplayer-Join] CMD 命令: {processStartInfo.Arguments}");
                    
                    // 启动进程并保存引用
                    _terracottaProcess = Process.Start(processStartInfo);
                    
                    if (_terracottaProcess != null)
                    {
                        // 读取输出
                        _terracottaProcess.OutputDataReceived += (sender, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                Log.Information($"[Terracotta-Client CMD Output] {e.Data}");
                            }
                        };
                        _terracottaProcess.ErrorDataReceived += (sender, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                Log.Error($"[Terracotta-Client CMD Error] {e.Data}");
                            }
                        };
                        _terracottaProcess.BeginOutputReadLine();
                        _terracottaProcess.BeginErrorReadLine();
                        
                        Log.Information($"[Multiplayer-Join] CMD 进程已启动，PID: {_terracottaProcess.Id}");
                    }
                    else
                    {
                        Log.Error("[Multiplayer-Join] Process.Start 返回 null");
                    }
                    
                    // 等待并读取端口信息
                    await Task.Delay(1000);
                    
                    // 读取临时文件，获取端口号
                    if (File.Exists(tempFilePath))
                    {
                        string jsonContent = File.ReadAllText(tempFilePath);
                        // 解析 json，获取 port 字段
                        using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                        {
                            JsonElement root = doc.RootElement;
                            if (root.TryGetProperty("port", out JsonElement portElement))
                            {
                                port = portElement.ToString();
                                _terracottaPort = port; // 保存端口号
                                Log.Information($"[Multiplayer-Join] 成功获取客户端端口: {port}");
                            }
                        }
                    }
                    else
                    {
                        Log.Warning($"[Multiplayer-Join] 临时文件未找到或未在 1 秒内生成: {tempFilePath}");
                    }
                }
                else
                {
                    isSuccess = false;
                    await ShowErrorDialogAsync("Dialog_Multiplayer_ServiceUnavailable".GetLocalized());
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
                Log.Error(ex, $"[Multiplayer-Join] 启动联机服务异常: {ex.Message}");
                await ShowErrorDialogAsync("Dialog_Multiplayer_StartFailed_Format".GetLocalized(ex.Message));
            }
            
            if (isSuccess && !string.IsNullOrEmpty(port))
            {
                // 3. 访问 http://localhost:{端口}/state/guesting?room=房间号&player=角色名
                try
                {
                    // 初始化 JoinGame 的 CancellationTokenSource
                    _joinGameCts = new CancellationTokenSource();
                    CancellationToken cancellationToken = _joinGameCts.Token;
                    
                    // 获取当前启动器内选择的角色名
                    string playerName = GetCurrentProfileName();
                    // 构建包含玩家名的 URL
                    string guestingUrl = $"http://localhost:{port}/state/guesting?room={roomId}&player={Uri.EscapeDataString(playerName)}";
                    
                    Log.Information($"[Multiplayer-Join] 发送 Guesting 请求: {guestingUrl}");
                    HttpResponseMessage response = await _httpClient.GetAsync(guestingUrl, cancellationToken);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        Log.Warning("[Multiplayer-Join] 房间不存在 (400 Bad Request)");
                        // 400 表示错误，通知玩家房间不存在
                        await ShowErrorDialogAsync("MultiplayerPage_RoomNotFoundError".GetLocalized());
                        // 停止进程
                        StopTerracottaProcess();
                        return;
                    }
                    
                    // 4. 轮询访问 http://localhost:{端口}/state，直到 state 为 guest-ok
                    string url = string.Empty;
                    
                    for (int i = 0; i < 30 && !cancellationToken.IsCancellationRequested; i++) // 最多尝试 30 次，每次间隔 1 秒
                    {
                        try
                        {
                            string stateUrl = $"http://localhost:{port}/state";
                            HttpResponseMessage stateResponse = await _httpClient.GetAsync(stateUrl, cancellationToken);
                            
                            if (stateResponse.IsSuccessStatusCode)
                            {
                                string content = await stateResponse.Content.ReadAsStringAsync(cancellationToken);
                                
                                // 解析 JSON
                                using (JsonDocument doc = JsonDocument.Parse(content))
                                {
                                    JsonElement root = doc.RootElement;
                                    if (root.TryGetProperty("state", out JsonElement stateElement) && 
                                        stateElement.ValueKind == JsonValueKind.String &&
                                        stateElement.GetString() == "guest-ok")
                                    {
                                        // 获取 url 字段
                                        if (root.TryGetProperty("url", out JsonElement urlElement) &&
                                            urlElement.ValueKind == JsonValueKind.String)
                                        {
                                            url = urlElement.GetString() ?? string.Empty;
                                            Log.Information($"[Multiplayer-Join] 加入成功, URL: {url}");
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Log.Warning($"[Multiplayer-Join] 状态轮询失败: {stateResponse.StatusCode}");
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            // 任务被取消，退出循环
                            break;
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"获取状态失败: {ex.Message}");
                        }
                        
                        // 等待 1 秒后再次尝试，支持取消
                        await Task.Delay(1000, cancellationToken);
                    }
                    
                    if (isSuccess && !string.IsNullOrEmpty(url))
                    {
                        // 5. 导航至联机大厅页，传递房间号、端口和房客标识
                        RequestLobbyNavigation(CreateLobbyNavigationParameter(
                            roomId,
                            port,
                            isGuest: true,
                            url));
                    }
                    else if (!cancellationToken.IsCancellationRequested)
                    {
                        await ShowErrorDialogAsync("MultiplayerPage_JoinFailedError".GetLocalized());
                        // 停止进程
                        StopTerracottaProcess();
                    }
                }
                catch (TaskCanceledException)
                {
                    // 任务被取消，不显示错误
                    StopTerracottaProcess();
                }
                catch (Exception ex)
                {
                    await ShowErrorDialogAsync($"连接房间时发生错误：{ex.Message}");
                    // 停止进程
                    StopTerracottaProcess();
                }
                finally
                {
                    // 清理 CancellationTokenSource
                    if (_joinGameCts != null)
                    {
                        _joinGameCts.Dispose();
                        _joinGameCts = null;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 显示错误对话框
    /// </summary>
    private async Task ShowErrorDialogAsync(string message)
    {
        await _dialogService.ShowMessageDialogAsync(
            "Common_ErrorTitle".GetLocalized(),
            message,
            "Common_OKButton".GetLocalized());
    }
}