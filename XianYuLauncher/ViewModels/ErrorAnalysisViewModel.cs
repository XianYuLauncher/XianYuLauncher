using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.ViewModels
{
    public partial class ErrorAnalysisViewModel : ObservableObject
    {
        private readonly ILanguageSelectorService _languageSelectorService;
        private readonly ResourceManager _resourceManager;
        private ResourceContext _resourceContext;

        [ObservableProperty]
        private string _fullLog = string.Empty;

        [ObservableProperty]
        private string _crashReason = string.Empty;
        
        // 构造函数
        public ErrorAnalysisViewModel(ILanguageSelectorService languageSelectorService)
        {
            _languageSelectorService = languageSelectorService;
            _resourceManager = new ResourceManager();
            _resourceContext = _resourceManager.CreateResourceContext();
        }

        /// <summary>
        /// 获取本地化字符串
        /// </summary>
        /// <param name="resourceKey">资源键</param>
        /// <returns>本地化后的字符串</returns>
        private string GetLocalizedString(string resourceKey)
        {
            try
            {
                // 根据当前语言返回相应的本地化文本
                var isChinese = _languageSelectorService.Language == "zh-CN";
                
                switch (resourceKey)
                {
                    case "ErrorAnalysis_NoErrorInfo.Text":
                        return isChinese ? "没有分析内容,因为Minecraft还没崩溃..." : "No analysis content because Minecraft hasn't crashed yet...";
                    case "ErrorAnalysis_Analyzing.Text":
                        return isChinese ? "AI正在分析崩溃原因..." : "AI is analyzing the crash reason...";
                    case "ErrorAnalysis_AnalysisComplete.Text":
                        return isChinese ? "AI分析完成。" : "AI analysis completed.";
                    case "ErrorAnalysis_AnalysisCanceled.Text":
                        return isChinese ? "AI分析已取消。" : "AI analysis canceled.";
                    case "ErrorAnalysis_RequestFailed.Text":
                        return isChinese ? "AI分析请求失败: {0}" : "AI analysis request failed: {0}";
                    case "ErrorAnalysis_AnalysisFailed.Text":
                        return isChinese ? "AI分析失败: {0}" : "AI analysis failed: {0}";
                    default:
                        return resourceKey;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取本地化资源失败: {ex.Message}");
                return resourceKey; // 发生异常时，返回资源键作为默认值
            }
        }

        // AI分析相关属性
        private string _aiAnalysisResult = string.Empty;
        public string AiAnalysisResult
        {
            get => _aiAnalysisResult;
            set
            {
                if (_aiAnalysisResult != value)
                {
                    _aiAnalysisResult = value;
                    // 确保在UI线程上触发PropertyChanged事件
                    var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                    if (dispatcherQueue == null && App.MainWindow != null)
                    {
                        dispatcherQueue = App.MainWindow.DispatcherQueue;
                    }
                    
                    if (dispatcherQueue != null)
                    {
                        dispatcherQueue.TryEnqueue(() =>
                        {
                            OnPropertyChanged(nameof(AiAnalysisResult));
                        });
                    }
                    else
                    {
                        // 如果无法获取DispatcherQueue，直接触发事件（可能会在非UI线程上执行）
                        OnPropertyChanged(nameof(AiAnalysisResult));
                    }
                }
            }
        }
        
        private bool _isAiAnalyzing = false;
        public bool IsAiAnalyzing
        {
            get => _isAiAnalyzing;
            set
            {
                if (_isAiAnalyzing != value)
                {
                    _isAiAnalyzing = value;
                    // 确保在UI线程上触发PropertyChanged事件
                    var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                    if (dispatcherQueue == null && App.MainWindow != null)
                    {
                        dispatcherQueue = App.MainWindow.DispatcherQueue;
                    }
                    
                    if (dispatcherQueue != null)
                    {
                        dispatcherQueue.TryEnqueue(() =>
                        {
                            OnPropertyChanged(nameof(IsAiAnalyzing));
                            OnPropertyChanged(nameof(IsAnalyzeButtonEnabled));
                            OnPropertyChanged(nameof(CancelButtonVisibility));
                        });
                    }
                    else
                    {
                        // 如果无法获取DispatcherQueue，直接触发事件（可能会在非UI线程上执行）
                        OnPropertyChanged(nameof(IsAiAnalyzing));
                        OnPropertyChanged(nameof(IsAnalyzeButtonEnabled));
                        OnPropertyChanged(nameof(CancelButtonVisibility));
                    }
                }
            }
        }
        
        private bool _isAiAnalysisAvailable = false;
        public bool IsAiAnalysisAvailable
        {
            get => _isAiAnalysisAvailable;
            set
            {
                if (_isAiAnalysisAvailable != value)
                {
                    _isAiAnalysisAvailable = value;
                    // 确保在UI线程上触发PropertyChanged事件
                    var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                    if (dispatcherQueue == null && App.MainWindow != null)
                    {
                        dispatcherQueue = App.MainWindow.DispatcherQueue;
                    }
                    
                    if (dispatcherQueue != null)
                    {
                        dispatcherQueue.TryEnqueue(() =>
                        {
                            OnPropertyChanged(nameof(IsAiAnalysisAvailable));
                            OnPropertyChanged(nameof(IsAnalyzeButtonEnabled));
                            OnPropertyChanged(nameof(AnalyzeButtonVisibility));
                        });
                    }
                    else
                    {
                        // 如果无法获取DispatcherQueue，直接触发事件（可能会在非UI线程上执行）
                        OnPropertyChanged(nameof(IsAiAnalysisAvailable));
                        OnPropertyChanged(nameof(IsAnalyzeButtonEnabled));
                        OnPropertyChanged(nameof(AnalyzeButtonVisibility));
                    }
                }
            }
        }
        
        // 计算属性，用于控制分析按钮的可见性
        public Microsoft.UI.Xaml.Visibility AnalyzeButtonVisibility => IsAiAnalysisAvailable ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        
        // 计算属性，用于控制分析按钮的启用状态
        public bool IsAnalyzeButtonEnabled => IsAiAnalysisAvailable && !IsAiAnalyzing;
        
        // 计算属性，用于控制取消按钮的可见性
    public Microsoft.UI.Xaml.Visibility CancelButtonVisibility => IsAiAnalyzing ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    
    // 手动实现了属性，不再需要自动生成的partial方法
        
        // 原始日志数据
        private string _originalLog = string.Empty;
        private string _launchCommand = string.Empty;
        private List<string> _gameOutput = new();
        private List<string> _gameError = new();
        private bool _isGameCrashed = false;
        
        // 节流机制相关字段
        private DateTime _lastLogUpdateTime = DateTime.MinValue;
        private const int LogUpdateIntervalMs = 100; // 日志更新间隔，单位毫秒
        private bool _isUpdateScheduled = false;
        
        // 日志限制相关字段
        private const int MaxLogLines = 10000; // 最大日志行数限制，避免内存占用过大
        private const int LogTrimAmount = 2000; // 超过限制时，每次删除的行数
        
        // 用于存储当前AI分析的取消令牌
        private System.Threading.CancellationTokenSource _aiAnalysisCts = null;

        // 设置日志数据
    public void SetLogData(string launchCommand, List<string> gameOutput, List<string> gameError)
    {
        System.Diagnostics.Debug.WriteLine($"AI分析: 设置日志数据，输出日志行数: {gameOutput.Count}，错误日志行数: {gameError.Count}");
        
        // 重置AI分析结果
        IsAiAnalyzing = false;
        IsAiAnalysisAvailable = false;
        
        // 设置默认文字
        AiAnalysisResult = GetLocalizedString("ErrorAnalysis_NoErrorInfo.Text");
        
        _launchCommand = launchCommand;
        _gameOutput = new List<string>(gameOutput);
        _gameError = new List<string>(gameError);
        
        // 生成完整日志
        GenerateFullLog();
    }
    
    /// <summary>
    /// 设置游戏崩溃状态，只有在游戏崩溃时才会触发AI分析
    /// </summary>
    /// <param name="isCrashed">是否崩溃</param>
    public void SetGameCrashStatus(bool isCrashed)
    {
        System.Diagnostics.Debug.WriteLine($"AI分析: 设置游戏崩溃状态: {isCrashed}");
        _isGameCrashed = isCrashed;
        IsAiAnalysisAvailable = isCrashed;
        
        // 如果游戏崩溃，自动触发AI分析
        if (isCrashed)
        {
            System.Diagnostics.Debug.WriteLine("AI分析: 游戏崩溃，自动触发AI分析");
            Task.Run(async () => await AnalyzeWithAiAsync());
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("AI分析: 游戏正常退出，不触发AI分析");
        }
    }
    
    /// <summary>
    /// 使用AI进行错误分析（流式输出）
    /// </summary>
    [RelayCommand]
    private async Task AnalyzeWithAiAsync()
    {
        // 检查是否可以进行AI分析
        if (!_isGameCrashed || IsAiAnalyzing)
        {
            System.Diagnostics.Debug.WriteLine("==================== AI分析 ====================");
            System.Diagnostics.Debug.WriteLine("AI分析: 条件不满足，跳过分析");
            System.Diagnostics.Debug.WriteLine("游戏崩溃状态: " + _isGameCrashed);
            System.Diagnostics.Debug.WriteLine("是否正在分析: " + IsAiAnalyzing);
            System.Diagnostics.Debug.WriteLine("===============================================");
            return;
        }
        
        try
        {
            System.Diagnostics.Debug.WriteLine("==================== AI分析 ====================");
            System.Diagnostics.Debug.WriteLine("AI分析: 开始分析崩溃原因");
            
            // 重置AI分析结果和状态
            IsAiAnalyzing = true;
            AiAnalysisResult = GetLocalizedString("ErrorAnalysis_Analyzing.Text") + "\n\n";
            
            // 创建取消令牌
            _aiAnalysisCts = new System.Threading.CancellationTokenSource();
            
            // 构建日志摘要，用于AI分析
            string logSummary = BuildLogSummaryForAi();
            System.Diagnostics.Debug.WriteLine("AI分析: 日志摘要构建完成");
            
            // 创建HTTP客户端
            using var client = new HttpClient();
            
            // 从配置读取AI设置
            var aiConfig = XianYuLauncher.Core.Services.SecretsService.Config.AiAnalysis;
            var apiKey = aiConfig.ApiKey;
            var model = aiConfig.Model;
            var baseUrl = aiConfig.BaseUrl;
            
            // 检查API Key是否配置
            if (string.IsNullOrEmpty(apiKey))
            {
                System.Diagnostics.Debug.WriteLine("[AI分析] 错误: API Key未配置");
                AiAnalysisResult = "AI分析功能未配置，请在 secrets.json 中配置 AiAnalysis.ApiKey";
                IsAiAnalyzing = false;
                return;
            }
            
            // 设置请求头
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            System.Diagnostics.Debug.WriteLine("[AI分析] HTTP客户端配置完成");
            
            // 获取用户偏好语言
            var userLanguage = _languageSelectorService.Language;
            var languageName = userLanguage == "zh-CN" ? "简体中文" : "English";
            
            // 构建请求体，添加语言适配
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = $"你是一个Minecraft崩溃分析专家，隶属于XianYuLauncher中的错误分析助手，擅长分析游戏崩溃日志并提供解决方案。请分析以下崩溃日志，提供详细的崩溃原因和修复建议。注:仅分析与崩溃直接相关的内容，不相关的警告信息无需分析。记得口语化，避免使用专业术语。用户的偏好语言为{languageName}，请使用此语言进行对话。" },
                    new { role = "user", content = logSummary }
                },
                stream = true, // 启用流式输出
                temperature = 0.7,
                max_tokens = 1000
            };
            System.Diagnostics.Debug.WriteLine($"[AI分析] 使用模型: {model}");
            
            // 发送请求
            var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            
            System.Diagnostics.Debug.WriteLine($"[AI分析] 发送请求到: {baseUrl}");
            System.Diagnostics.Debug.WriteLine("[AI分析] 正在等待API响应...");
            
            // 设置请求超时
            client.Timeout = TimeSpan.FromSeconds(30);
            
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _aiAnalysisCts.Token);
            
            // 检查响应状态码
            System.Diagnostics.Debug.WriteLine($"AI分析: 收到响应，状态码: {response.StatusCode}");
            
            // 确保响应成功
            if (!response.IsSuccessStatusCode)
            {
                // 读取错误响应内容
                string errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"AI分析: API错误响应: {errorContent}");
                response.EnsureSuccessStatusCode();
            }
            
            // 处理流式响应
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            System.Diagnostics.Debug.WriteLine("AI分析: 开始处理流式响应");
            
            string line;
            int lineCount = 0;
            while ((line = await reader.ReadLineAsync()) != null && !_aiAnalysisCts.Token.IsCancellationRequested)
            {
                lineCount++;
                
                // 跳过空行
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                // 处理SSE格式的响应
                if (line.StartsWith("data: "))
                {
                    string data = line[6..].Trim();
                    
                    // 检查是否结束
                    if (data == "[DONE]")
                    {
                        System.Diagnostics.Debug.WriteLine("AI分析: 流式响应结束");
                        break;
                    }
                    
                    try
                    {
                        // 解析JSON
                        var responseData = JsonSerializer.Deserialize<OpenAiStreamResponse>(data);
                        if (responseData?.choices != null && responseData.choices.Count > 0)
                        {
                            var delta = responseData.choices[0].delta;
                            if (!string.IsNullOrEmpty(delta.content))
                            {
                                // 直接更新属性，因为我们已经手动实现了线程安全的属性设置器
                                AiAnalysisResult += delta.content;
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"AI分析: 解析响应失败: {ex.Message}");
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"AI分析: 处理完成，共接收 {lineCount} 行响应");
            System.Diagnostics.Debug.WriteLine("===============================================");
            
            // 分析完成
            AiAnalysisResult += $"\n\n{GetLocalizedString("ErrorAnalysis_AnalysisComplete.Text")}";
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("AI分析: 分析被用户取消");
            System.Diagnostics.Debug.WriteLine("===============================================");
            
            // 用户取消了分析
            AiAnalysisResult += $"\n\n{GetLocalizedString("ErrorAnalysis_AnalysisCanceled.Text")}";
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine("AI分析: 请求失败: " + ex.Message);
            System.Diagnostics.Debug.WriteLine("===============================================");
            
            AiAnalysisResult += $"\n\n{string.Format(GetLocalizedString("ErrorAnalysis_RequestFailed.Text"), ex.Message)}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("AI分析: 失败: " + ex.Message);
            System.Diagnostics.Debug.WriteLine("异常类型: " + ex.GetType().Name);
            System.Diagnostics.Debug.WriteLine("堆栈跟踪: " + ex.StackTrace);
            System.Diagnostics.Debug.WriteLine("===============================================");
            
            AiAnalysisResult += $"\n\n{string.Format(GetLocalizedString("ErrorAnalysis_AnalysisFailed.Text"), ex.Message)}";
        }
        finally
        {
            // 清理资源并更新状态
            _aiAnalysisCts?.Dispose();
            _aiAnalysisCts = null;
            
            // 更新分析状态
            IsAiAnalyzing = false;
        }
    }
    
    /// <summary>
    /// 取消AI分析
    /// </summary>
    [RelayCommand]
    private void CancelAiAnalysis()
    {
        System.Diagnostics.Debug.WriteLine("AI分析: 收到取消AI分析请求");
        if (_aiAnalysisCts != null && !_aiAnalysisCts.IsCancellationRequested)
        {
            System.Diagnostics.Debug.WriteLine("AI分析: 执行取消操作");
            _aiAnalysisCts.Cancel();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("AI分析: 无法取消，当前没有正在进行的AI分析");
        }
    }
    
    /// <summary>
    /// 构建用于AI分析的日志摘要
    /// </summary>
    private string BuildLogSummaryForAi()
    {
        System.Diagnostics.Debug.WriteLine("AI分析: 开始构建日志摘要");
        
        var sb = new StringBuilder();
        
        sb.AppendLine("=== Minecraft 完整崩溃日志 ===");
        sb.AppendLine();
        
        // 添加崩溃原因
        if (!string.IsNullOrEmpty(CrashReason))
        {
            System.Diagnostics.Debug.WriteLine($"AI分析: 添加崩溃原因到摘要: {CrashReason}");
            sb.AppendLine($"初步崩溃分析: {CrashReason}");
            sb.AppendLine();
        }
        
        // 添加启动命令
        if (!string.IsNullOrEmpty(_launchCommand))
        {
            System.Diagnostics.Debug.WriteLine("AI分析: 添加启动命令到摘要");
            sb.AppendLine("启动命令:");
            sb.AppendLine(_launchCommand);
            sb.AppendLine();
        }
        
        // 添加完整的错误日志
        if (_gameError.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"AI分析: 添加完整错误日志到摘要，共 {_gameError.Count} 行");
            sb.AppendLine("=== 完整错误日志 ===");
            for (int i = 0; i < _gameError.Count; i++)
            {
                sb.AppendLine(_gameError[i]);
            }
            sb.AppendLine();
        }
        
        // 添加完整的输出日志
        if (_gameOutput.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"AI分析: 添加完整输出日志到摘要，共 {_gameOutput.Count} 行");
            sb.AppendLine("=== 完整输出日志 ===");
            // 限制日志大小，避免API请求过大
            int maxLines = Math.Min(_gameOutput.Count, 500);
            for (int i = Math.Max(0, _gameOutput.Count - maxLines); i < _gameOutput.Count; i++)
            {
                sb.AppendLine(_gameOutput[i]);
            }
            if (_gameOutput.Count > maxLines)
            {
                sb.AppendLine($"... 省略 {_gameOutput.Count - maxLines} 行（仅显示最后 {maxLines} 行）");
            }
            sb.AppendLine();
        }
        
        string logSummary = sb.ToString();
        System.Diagnostics.Debug.WriteLine($"AI分析: 日志摘要构建完成，长度: {logSummary.Length} 字符");
        return logSummary;
    }
    
    /// <summary>
    /// OpenAI流式响应模型
    /// </summary>
    private class OpenAiStreamResponse
    {
        public List<Choice> choices { get; set; }
        
        public class Choice
        {
            public Delta delta { get; set; }
        }
        
        public class Delta
        {
            public string content { get; set; }
        }
    }
    
    /// <summary>
    /// 设置启动命令（用于实时日志模式）
    /// </summary>
    /// <param name="launchCommand">启动命令</param>
    public void SetLaunchCommand(string launchCommand)
    {
        _launchCommand = launchCommand;
        System.Diagnostics.Debug.WriteLine($"ErrorAnalysisViewModel: 设置启动命令，长度: {launchCommand?.Length ?? 0}");
    }
    
    /// <summary>
    /// 仅清空日志数据，保留启动命令（用于实时日志模式）
    /// </summary>
    public void ClearLogsOnly()
    {
        System.Diagnostics.Debug.WriteLine("ErrorAnalysisViewModel: 仅清空日志数据，保留启动命令");
        
        // 重置AI分析结果
        IsAiAnalyzing = false;
        IsAiAnalysisAvailable = false;
        AiAnalysisResult = GetLocalizedString("ErrorAnalysis_NoErrorInfo.Text");
        
        // 清空日志但保留启动命令
        _gameOutput = new List<string>();
        _gameError = new List<string>();
        
        // 生成完整日志
        GenerateFullLog();
    }
    
    /// <summary>
    /// 实时添加游戏输出日志
    /// </summary>
    /// <param name="logLine">日志行</param>
    public void AddGameOutputLog(string logLine)
    {
        // 使用锁确保线程安全
        lock (_gameOutput)
        {
            _gameOutput.Add(logLine);
            // 限制日志行数，避免内存占用过大
            if (_gameOutput.Count > MaxLogLines)
            {
                _gameOutput.RemoveRange(0, LogTrimAmount);
            }
        }
        
        ScheduleLogUpdate();
    }
    
    /// <summary>
    /// 实时添加游戏错误日志
    /// </summary>
    /// <param name="logLine">日志行</param>
    public void AddGameErrorLog(string logLine)
    {
        // 使用锁确保线程安全
        lock (_gameError)
        {
            _gameError.Add(logLine);
            // 限制日志行数，避免内存占用过大
            if (_gameError.Count > MaxLogLines)
            {
                _gameError.RemoveRange(0, LogTrimAmount);
            }
        }
        
        ScheduleLogUpdate();
    }
    
    /// <summary>
    /// 调度日志更新，添加节流机制
    /// </summary>
    private void ScheduleLogUpdate()
    {
        var now = DateTime.Now;
        var timeSinceLastUpdate = now - _lastLogUpdateTime;
        
        // 如果距离上次更新已经超过间隔时间，立即更新
        if (timeSinceLastUpdate.TotalMilliseconds >= LogUpdateIntervalMs)
        {
            _lastLogUpdateTime = now;
            GenerateFullLog();
        }
        // 否则，只调度一次更新
        else if (!_isUpdateScheduled)
        {
            _isUpdateScheduled = true;
            var delay = LogUpdateIntervalMs - (int)timeSinceLastUpdate.TotalMilliseconds;
            
            // 使用Task.Delay实现延迟更新，避免阻塞主线程
            Task.Delay(delay).ContinueWith(_ =>
            {
                if (_isUpdateScheduled)
                {
                    _lastLogUpdateTime = DateTime.Now;
                    _isUpdateScheduled = false;
                    GenerateFullLog();
                }
            });
        }
    }

        // 生成完整日志
    private void GenerateFullLog()
    {
        // 简化日志生成，只保留必要的日志内容
        var sb = new StringBuilder();

        sb.AppendLine("=== 实时游戏日志 ===");
        sb.AppendLine(string.Format("日志开始时间: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));
        sb.AppendLine();
        
        // 准备分析和生成日志所需的列表
        List<string> outputList = new();
        List<string> errorList = new();
        
        // 使用锁保护日志列表，避免并发修改异常
        lock (_gameOutput)
        {
            outputList.AddRange(_gameOutput);
        }
        
        lock (_gameError)
        {
            errorList.AddRange(_gameError);
        }
        
        if (outputList.Count == 0 && errorList.Count == 0)
        {
            sb.AppendLine("等待游戏输出...");
        }
        else
        {
            // 总是分析崩溃原因，确保完整性
            string errorAnalysis = AnalyzeCrash(outputList, errorList);
            sb.AppendLine(string.Format("崩溃分析: {0}", errorAnalysis));
            sb.AppendLine();

            // 设置崩溃原因属性
            // 确保在UI线程上更新属性
            if (App.MainWindow.DispatcherQueue.HasThreadAccess)
            {
                CrashReason = errorAnalysis;
            }
            else
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    CrashReason = errorAnalysis;
                });
            }
        }

        sb.AppendLine("=== 游戏输出日志 ===");
        foreach (var line in outputList)
        {
            sb.AppendLine(line);
        }
        sb.AppendLine();

        sb.AppendLine("=== 游戏错误日志 ===");
        foreach (var line in errorList)
        {
            sb.AppendLine(line);
        }
        sb.AppendLine();

        // 移除系统信息，减少生成时间
        sb.AppendLine("实时日志持续更新中...");

        // 限制最终日志的大小，避免内存占用过大
        string finalLog = sb.ToString();
        _originalLog = finalLog; // 保存原始日志

        // 确保在UI线程上更新FullLog属性
        if (App.MainWindow.DispatcherQueue.HasThreadAccess)
        {
            FullLog = finalLog;
        }
        else
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                FullLog = finalLog;
            });
        }
    }

        // 分析崩溃原因
        private string AnalyzeCrash(List<string> gameOutput, List<string> gameError)
        {
            // 优化分析逻辑，先检查错误日志，再检查输出日志
            // 并且使用更高效的方式检查关键词
            
            // 检查手动触发崩溃
            if (ContainsKeyword(gameError, "Manually triggered debug crash") || ContainsKeyword(gameOutput, "Manually triggered debug crash"))
            {
                return "玩家手动触发崩溃";
            }
            
            // 检查致命错误
            if (ContainsKeyword(gameError, "[Fatal Error]") || ContainsKeyword(gameOutput, "[Fatal Error]"))
            {
                return "致命错误导致崩溃";
            }
            
            // 检查Java异常
            if (ContainsKeyword(gameError, "java.lang.Exception") || ContainsKeyword(gameOutput, "java.lang.Exception"))
            {
                return "Java异常导致崩溃";
            }
            
            // 检查内存不足
            if (ContainsKeyword(gameError, "OutOfMemoryError") || ContainsKeyword(gameOutput, "OutOfMemoryError"))
            {
                return "内存不足导致崩溃";
            }
            
            // 检查玩家崩溃
            if (ContainsKeyword(gameError, "玩家崩溃") || ContainsKeyword(gameOutput, "玩家崩溃"))
            {
                return "玩家手动触发崩溃";
            }
            
            // 检查普通错误
            if (ContainsKeyword(gameError, "[ERROR]") || ContainsKeyword(gameOutput, "[ERROR]"))
            {
                return "错误日志中存在错误信息";
            }

            return "未知崩溃原因";
        }
        
        /// <summary>
        /// 高效检查列表中是否包含关键词
        /// </summary>
        /// <param name="lines">日志行列表</param>
        /// <param name="keyword">关键词</param>
        /// <returns>是否包含关键词</returns>
        private bool ContainsKeyword(List<string> lines, string keyword)
        {
            // 使用高效的循环方式，避免LINQ的额外开销
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // 导出错误日志
        [RelayCommand]
        private async Task ExportErrorLogsAsync()
        {
            try
            {
                // 打开文件保存对话框让用户选择导出位置
                var filePicker = new Windows.Storage.Pickers.FileSavePicker();
                
                // 设置文件选择器的起始位置
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, windowHandle);
                
                // 设置文件类型
                filePicker.FileTypeChoices.Add("ZIP 压缩文件", new[] { ".zip" });
                
                // 设置默认文件名
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                filePicker.SuggestedFileName = string.Format("crash_logs_{0}", timestamp);
                
                // 显示文件选择器
                var selectedFile = await filePicker.PickSaveFileAsync();
                
                // 检查用户是否取消了选择
                if (selectedFile == null)
                {
                    return; // 用户取消了操作
                }
                
                // 创建临时目录用于存放日志文件
                string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                // 生成启动参数.bat文件
                string batFilePath = Path.Combine(tempDir, "启动参数.bat");
                File.WriteAllText(batFilePath, _launchCommand);

                // 生成崩溃日志文件名
                string crashLogFile = Path.Combine(tempDir, string.Format("crash_report_{0}.txt", timestamp));

                // 写入完整日志（不包含启动参数，已单独保存为.bat文件）
                File.WriteAllText(crashLogFile, FullLog);

                // 获取用户选择的zip文件路径
                string zipFilePath = selectedFile.Path;

                // 如果文件已存在，先删除
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }

                // 创建zip文件
                ZipFile.CreateFromDirectory(tempDir, zipFilePath);

                // 清理临时目录
                Directory.Delete(tempDir, true);

                // 显示成功提示
                var successDialog = new ContentDialog
                {
                    Title = "成功",
                    Content = string.Format("崩溃日志已成功导出到：{0}", zipFilePath),
                    PrimaryButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };

                await successDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                // 显示错误提示
                var errorDialog = new ContentDialog
                {
                    Title = "错误",
                    Content = string.Format("导出崩溃日志失败：{0}", ex.Message),
                    PrimaryButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };

                await errorDialog.ShowAsync();
            }
        }

        // 复制日志到剪贴板
        [RelayCommand]
        private void CopyLogs()
        {
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(FullLog);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("复制日志失败: " + ex.Message);
            }
        }

        // 清空日志
        [RelayCommand]
        private void ClearLogs()
        {
            FullLog = string.Empty;
            // 同时重置AI分析结果为默认文字
            AiAnalysisResult = GetLocalizedString("ErrorAnalysis_NoErrorInfo.Text");
            IsAiAnalysisAvailable = false;
        }
    }
}