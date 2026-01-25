using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Windows.ApplicationModel.DataTransfer;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.ViewModels
{
    public partial class ErrorAnalysisViewModel : ObservableObject
    {
        private readonly ILanguageSelectorService _languageSelectorService;
        private readonly ILogSanitizerService _logSanitizerService;
        private readonly IAIAnalysisService _aiAnalysisService; // New Service
        private readonly ILocalSettingsService _localSettingsService; // To read settings
        private readonly ResourceManager _resourceManager;
        private ResourceContext _resourceContext;

        [ObservableProperty]
        private string _fullLog = string.Empty;

        [ObservableProperty]
        private string _crashReason = string.Empty;
        
        // 新增：用于ListView的日志行集合
        [ObservableProperty]
        private ObservableCollection<string> _logLines = new();

        /// <summary>
        /// 加入QQ群进行反馈
        /// </summary>
        [RelayCommand]
        private async Task JoinQQGroup()
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://qm.qq.com/q/Vj1SWx7EkO"));
        }
        
        // 构造函数
        public ErrorAnalysisViewModel(
            ILanguageSelectorService languageSelectorService, 
            ILogSanitizerService logSanitizerService,
            IAIAnalysisService aiAnalysisService,
            ILocalSettingsService localSettingsService)
        {
            _languageSelectorService = languageSelectorService;
            _logSanitizerService = logSanitizerService;
            _aiAnalysisService = aiAnalysisService;
            _localSettingsService = localSettingsService;
            _resourceManager = new ResourceManager();
            _resourceContext = _resourceManager.CreateResourceContext();
        }

        /// <summary>
        /// 分析日志
        /// </summary>
        public async Task AnalyzeLogAsync()
        {
            await AnalyzeWithAiAsync();
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
                        return isChinese ? "正在分析崩溃原因..." : "Analyzing crash reason...";
                    case "ErrorAnalysis_AnalysisComplete.Text":
                        return isChinese ? "分析完成。" : "Analysis completed.";
                    case "ErrorAnalysis_AnalysisCanceled.Text":
                        return isChinese ? "分析已取消。" : "Analysis canceled.";
                    case "ErrorAnalysis_RequestFailed.Text":
                        return isChinese ? "分析请求失败: {0}" : "Analysis request failed: {0}";
                    case "ErrorAnalysis_AnalysisFailed.Text":
                        return isChinese ? "分析失败: {0}" : "Analysis failed: {0}";
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

        // 崩溃分析相关属性
        private string _aiAnalysisResult = string.Empty;
        public string AiAnalysisResult
        {
            get => _aiAnalysisResult;
            set
            {
                if (_aiAnalysisResult != value)
                {
                    _aiAnalysisResult = value;
                    OnPropertyChanged(nameof(AiAnalysisResult));
                }
            }
        }

        // 新增：智能修复相关属性
        [ObservableProperty]
        private bool _hasFixAction;

        [ObservableProperty]
        private string _fixButtonText = string.Empty;

        [ObservableProperty]
        private bool _hasSecondaryFixAction;

        [ObservableProperty]
        private string _secondaryFixButtonText = string.Empty;

        private CrashFixAction? _currentFixAction;
        private CrashFixAction? _secondaryFixAction;

        private Microsoft.UI.Dispatching.DispatcherQueue? GetUiDispatcherQueue()
        {
            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (dispatcherQueue == null && App.MainWindow != null)
            {
                dispatcherQueue = App.MainWindow.DispatcherQueue;
            }

            return dispatcherQueue;
        }

        private Task EnqueueOnUiAsync(Action action)
        {
            var dispatcherQueue = GetUiDispatcherQueue();
            if (dispatcherQueue == null)
            {
                action();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource();
            dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        private Task EnqueueOnUiAsync(Func<Task> action)
        {
            var dispatcherQueue = GetUiDispatcherQueue();
            if (dispatcherQueue == null)
            {
                return action();
            }

            var tcs = new TaskCompletionSource();
            dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        // 占位命令，后续实现逻辑
        [RelayCommand]
        private async Task FixError()
        {
            if (_currentFixAction == null)
            {
                return;
            }

            try
            {
                var currentLoader = GetCurrentLoaderType();
                if (!string.IsNullOrWhiteSpace(currentLoader) && !string.Equals(currentLoader, "vanilla", StringComparison.OrdinalIgnoreCase))
                {
                    _currentFixAction.Parameters["loader"] = currentLoader;
                }
                else if (_currentFixAction.Parameters.TryGetValue("loader", out var loaderValue) &&
                         string.Equals(loaderValue, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    _currentFixAction.Parameters["loader"] = string.Empty;
                }

                switch (_currentFixAction.Type?.Trim().ToLowerInvariant())
                {
                    case "searchmodrinthproject":
                        await ExecuteSearchModrinthProjectAsync(_currentFixAction.Parameters);
                        break;
                    case "switchjavaforversion":
                        await ExecuteSwitchJavaForVersionAsync();
                        break;
                    case "deletemod":
                        await ExecuteDeleteModAsync(_currentFixAction.Parameters);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                await EnqueueOnUiAsync(() =>
                {
                    AiAnalysisResult += $"\n\n{string.Format(GetLocalizedString("ErrorAnalysis_RequestFailed.Text"), ex.Message)}";
                });
            }
        }

        [RelayCommand]
        private async Task FixErrorSecondary()
        {
            if (_secondaryFixAction == null)
            {
                return;
            }

            try
            {
                switch (_secondaryFixAction.Type?.Trim().ToLowerInvariant())
                {
                    case "searchmodrinthproject":
                        await ExecuteSearchModrinthProjectAsync(_secondaryFixAction.Parameters);
                        break;
                    case "switchjavaforversion":
                        await ExecuteSwitchJavaForVersionAsync();
                        break;
                    case "deletemod":
                        await ExecuteDeleteModAsync(_secondaryFixAction.Parameters);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                await EnqueueOnUiAsync(() =>
                {
                    AiAnalysisResult += $"\n\n{string.Format(GetLocalizedString("ErrorAnalysis_RequestFailed.Text"), ex.Message)}";
                });
            }
        }

        private async Task ExecuteSearchModrinthProjectAsync(Dictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("query", out var query) || string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            var projectType = parameters.TryGetValue("projectType", out var projectTypeValue)
                ? projectTypeValue
                : "mod";

            var loader = parameters.TryGetValue("loader", out var loaderValue)
                ? loaderValue
                : string.Empty;

            var modrinthService = App.GetService<XianYuLauncher.Core.Services.ModrinthService>();
            List<List<string>> facets = null;
            if (!string.IsNullOrWhiteSpace(loader))
            {
                facets = new List<List<string>>
                {
                    new List<string> { $"categories:{loader}" }
                };
            }

            var result = await modrinthService.SearchModsAsync(query, facets, "relevance", 0, 5, projectType);
            if (result?.Hits == null || result.Hits.Count == 0)
            {
                var cfFound = await TrySearchCurseForgeAsync(query, loader);
                if (!cfFound)
                {
                    await ShowNotFoundDialogAsync(query);
                }
                return;
            }

            var normalizedQuery = NormalizeSlug(query);
            var bestMatch = result.Hits.FirstOrDefault(h => NormalizeSlug(h.Slug) == normalizedQuery)
                            ?? result.Hits.FirstOrDefault(h => NormalizeSlug(h.Title) == normalizedQuery)
                            ?? result.Hits.First();

            var navigationService = App.GetService<INavigationService>();
            navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, new Tuple<ModrinthProject, string>(bestMatch, "Modrinth"));
        }

        private async Task ExecuteSwitchJavaForVersionAsync()
        {
            if (string.IsNullOrWhiteSpace(_versionId) || string.IsNullOrWhiteSpace(_minecraftPath))
            {
                await EnqueueOnUiAsync(() =>
                {
                    AiAnalysisResult += "\n\n未找到当前版本信息，无法自动切换 Java。";
                });
                return;
            }

            var versionInfoManager = App.GetService<IVersionInfoManager>();
            VersionInfo? versionInfo = null;
            try
            {
                versionInfo = await versionInfoManager.GetVersionInfoAsync(_versionId, _minecraftPath, allowNetwork: false);
            }
            catch
            {
                try
                {
                    versionInfo = await versionInfoManager.GetVersionInfoAsync(_versionId, _minecraftPath, allowNetwork: true);
                }
                catch (Exception ex)
                {
                    await EnqueueOnUiAsync(() =>
                    {
                        AiAnalysisResult += $"\n\n读取版本信息失败：{ex.Message}";
                    });
                    return;
                }
            }

            int requiredMajorVersion = versionInfo?.JavaVersion?.MajorVersion ?? 8;

            var javaRuntimeService = App.GetService<IJavaRuntimeService>();
            var javaVersions = await javaRuntimeService.DetectJavaVersionsAsync(true);
            var bestJava = SelectBestJava(javaVersions, requiredMajorVersion);

            if (bestJava == null || string.IsNullOrWhiteSpace(bestJava.Path))
            {
                await EnqueueOnUiAsync(() =>
                {
                    AiAnalysisResult += $"\n\n未找到可用的 Java {requiredMajorVersion} 版本，请先安装对应版本后再重试。";
                });
                return;
            }

            await EnqueueOnUiAsync(() =>
            {
                var launchViewModel = App.GetService<LaunchViewModel>();
                if (!string.Equals(launchViewModel.SelectedVersion, _versionId, StringComparison.OrdinalIgnoreCase))
                {
                    launchViewModel.SelectedVersion = _versionId;
                }

                launchViewModel.SetTemporaryJavaOverride(bestJava.Path);

                var navigationService = App.GetService<INavigationService>();
                navigationService.NavigateTo(typeof(LaunchViewModel).FullName!);

                launchViewModel.LaunchGameCommand.Execute(null);
            });
        }

        private async Task ExecuteDeleteModAsync(Dictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("modId", out var modId) || string.IsNullOrWhiteSpace(modId))
            {
                if (!parameters.TryGetValue("modFile", out modId) || string.IsNullOrWhiteSpace(modId))
                {
                    if (!parameters.TryGetValue("fileName", out modId) || string.IsNullOrWhiteSpace(modId))
                    {
                        return;
                    }
                }
            }

            // 规范化为文件名（避免直接用路径执行删除）
            modId = Path.GetFileName(modId);
            System.Diagnostics.Debug.WriteLine($"[AI] 删除Mod参数: {modId}");

            var modFilePath = await FindModFileByIdAsync(modId);
            if (string.IsNullOrWhiteSpace(modFilePath))
            {
                await EnqueueOnUiAsync(() =>
                {
                    AiAnalysisResult += $"\n\n未找到 Mod 文件：{modId}";
                });
                return;
            }

            bool shouldDelete = false;
            await EnqueueOnUiAsync(async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = "删除 Mod",
                    Content = $"确定要删除该 Mod 吗？\n\n文件名：{Path.GetFileName(modFilePath)}\n路径：{modFilePath}\n\n注意：如果这是依赖库，可能会影响其它 Mod。",
                    PrimaryButtonText = "删除",
                    CloseButtonText = "取消",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                shouldDelete = result == ContentDialogResult.Primary;
            });

            if (!shouldDelete)
            {
                return;
            }

            try
            {
                File.Delete(modFilePath);
                await EnqueueOnUiAsync(() =>
                {
                    AiAnalysisResult += $"\n\n已删除 Mod：{Path.GetFileName(modFilePath)}";
                });
            }
            catch (Exception ex)
            {
                await EnqueueOnUiAsync(() =>
                {
                    AiAnalysisResult += $"\n\n删除 Mod 失败：{ex.Message}";
                });
            }
        }

        private async Task<string?> FindModFileByIdAsync(string modId)
        {
            if (string.IsNullOrWhiteSpace(_minecraftPath))
            {
                return null;
            }

            var candidateFiles = new List<string>();
            var globalModsPath = Path.Combine(_minecraftPath, "mods");
            if (Directory.Exists(globalModsPath))
            {
                candidateFiles.AddRange(Directory.GetFiles(globalModsPath, "*.jar*"));
            }

            if (!string.IsNullOrWhiteSpace(_versionId))
            {
                var versionModsPath = Path.Combine(_minecraftPath, "versions", _versionId, "mods");
                if (Directory.Exists(versionModsPath))
                {
                    candidateFiles.AddRange(Directory.GetFiles(versionModsPath, "*.jar*"));
                }
            }

            var normalizedModId = modId.Trim();
            foreach (var file in candidateFiles)
            {
                var fileName = Path.GetFileName(file);
                if (!string.IsNullOrWhiteSpace(fileName) &&
                    fileName.Contains(normalizedModId, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }

                var fabricId = await TryGetFabricModIdAsync(file);
                if (!string.IsNullOrWhiteSpace(fabricId) &&
                    string.Equals(fabricId, normalizedModId, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }

            return null;
        }

        private static async Task<string?> TryGetFabricModIdAsync(string jarPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(jarPath);
                var entry = archive.GetEntry("fabric.mod.json");
                if (entry == null)
                {
                    return null;
                }

                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                var obj = JObject.Parse(json);
                var idToken = obj["id"];
                return idToken?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static JavaVersion? SelectBestJava(IReadOnlyCollection<JavaVersion> javaVersions, int requiredMajorVersion)
        {
            if (javaVersions == null || javaVersions.Count == 0)
            {
                return null;
            }

            return javaVersions
                .Where(j => !string.IsNullOrWhiteSpace(j.Path) && File.Exists(j.Path) && j.MajorVersion > 0)
                .OrderByDescending(j => j.MajorVersion == requiredMajorVersion)
                .ThenByDescending(j => j.IsJDK)
                .ThenBy(j => Math.Abs(j.MajorVersion - requiredMajorVersion))
                .FirstOrDefault();
        }

        private async Task<bool> TrySearchCurseForgeAsync(string query, string loader)
        {
            try
            {
                var curseForgeService = App.GetService<XianYuLauncher.Core.Services.CurseForgeService>();
                int? modLoaderType = loader?.Trim().ToLowerInvariant() switch
                {
                    "forge" => 1,
                    "fabric" => 4,
                    "quilt" => 5,
                    "neoforge" => 6,
                    _ => null
                };

                var cfResult = await curseForgeService.SearchModsAsync(query, null, modLoaderType, null, 0, 5);
                if (cfResult?.Data == null || cfResult.Data.Count == 0)
                {
                    return false;
                }

                var normalizedQuery = NormalizeSlug(query);
                var best = cfResult.Data.FirstOrDefault(h => NormalizeSlug(h.Slug) == normalizedQuery)
                           ?? cfResult.Data.FirstOrDefault(h => NormalizeSlug(h.Name) == normalizedQuery)
                           ?? cfResult.Data.First();

                var navigationService = App.GetService<INavigationService>();
                navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, $"curseforge-{best.Id}");
                return true;
            }
            catch (Exception ex)
            {
                await EnqueueOnUiAsync(() =>
                {
                    AiAnalysisResult += $"\n\nCurseForge 搜索失败: {ex.Message}";
                });
                return false;
            }
        }

        private async Task ShowNotFoundDialogAsync(string query)
        {
            await EnqueueOnUiAsync(async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = "未找到",
                    Content = $"未在 Modrinth 或 CurseForge 找到与 '{query}' 对应的项目。",
                    PrimaryButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await dialog.ShowAsync();
            });
        }

        private static string NormalizeSlug(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray());
            return normalized.ToLowerInvariant();
        }

        private string GetCurrentLoaderType()
        {
            if (string.IsNullOrWhiteSpace(_versionId) || string.IsNullOrWhiteSpace(_minecraftPath))
            {
                return string.Empty;
            }

            try
            {
                var versionDirectory = Path.Combine(_minecraftPath, "versions", _versionId);
                var versionInfoService = App.GetService<XianYuLauncher.Core.Services.IVersionInfoService>();
                var config = versionInfoService?.GetFullVersionInfo(_versionId, versionDirectory);
                return config?.ModLoaderType?.Trim().ToLowerInvariant() ?? string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取版本加载器失败: {ex.Message}");
                return string.Empty;
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
        private string _versionId = string.Empty; // 当前启动的版本ID
        private string _minecraftPath = string.Empty; // Minecraft 路径
        
        // 节流机制相关字段
        private DateTime _lastLogUpdateTime = DateTime.MinValue;
        private const int LogUpdateIntervalMs = 100; // 日志更新间隔，单位毫秒
        private bool _isUpdateScheduled = false;
        
        // 用于存储当前崩溃分析的取消令牌
        private System.Threading.CancellationTokenSource _aiAnalysisCts = null;

        // 设置日志数据
    public void SetLogData(string launchCommand, List<string> gameOutput, List<string> gameError)
    {
        System.Diagnostics.Debug.WriteLine($"崩溃分析: 设置日志数据，输出日志行数: {gameOutput.Count}，错误日志行数: {gameError.Count}");
        
        // 重置崩溃分析结果
        IsAiAnalyzing = false;
        IsAiAnalysisAvailable = false;
        HasFixAction = false;
        FixButtonText = string.Empty;
        _currentFixAction = null;
        
        // 设置默认文字
        AiAnalysisResult = GetLocalizedString("ErrorAnalysis_NoErrorInfo.Text");
        
        _launchCommand = launchCommand;
        _gameOutput = new List<string>(gameOutput);
        _gameError = new List<string>(gameError);
        
        // 清空并重新填充UI集合
        LogLines.Clear();
        
        // 添加头部信息
        LogLines.Add("=== 实时游戏日志 ===");
        LogLines.Add($"日志开始时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        LogLines.Add("");
        
        // 添加输出日志
        LogLines.Add("=== 游戏输出日志 ===");
        foreach (var line in gameOutput)
        {
            LogLines.Add(line);
        }
        LogLines.Add("");
        
        // 添加错误日志
        LogLines.Add("=== 游戏错误日志 ===");
        foreach (var line in gameError)
        {
            LogLines.Add(line);
        }
        
        // 生成完整日志（用于导出）
        GenerateFullLog();
    }
    
    /// <summary>
    /// 设置游戏崩溃状态，只有在游戏崩溃时才会触发AI分析
    /// </summary>
    /// <param name="isCrashed">是否崩溃</param>
    public void SetGameCrashStatus(bool isCrashed)
    {
        System.Diagnostics.Debug.WriteLine($"崩溃分析: 设置游戏崩溃状态: {isCrashed}");
        _isGameCrashed = isCrashed;
        IsAiAnalysisAvailable = isCrashed;
        
        // 如果游戏崩溃，自动触发崩溃分析
        if (isCrashed)
        {
            System.Diagnostics.Debug.WriteLine("崩溃分析: 游戏崩溃，自动触发崩溃分析");
            Task.Run(async () => await AnalyzeWithAiAsync());
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("AI分析: 游戏正常退出，不触发AI分析");
        }
    }

    private const string ToolStartTag = "<TOOL_CALLS>";
    private const string ToolEndTag = "</TOOL_CALLS>";
    private bool _isToolBlockActive;
    private string _toolTagCarry = string.Empty;
    private readonly StringBuilder _toolBlockBuffer = new();
    
    /// <summary>
    /// 使用本地知识库进行错误分析（流式输出）
    /// </summary>
    [RelayCommand]
    private async Task AnalyzeWithAiAsync()
    {
        // 检查是否可以进行分析
        if (!_isGameCrashed || IsAiAnalyzing)
        {
            System.Diagnostics.Debug.WriteLine("==================== 崩溃分析 ====================");
            System.Diagnostics.Debug.WriteLine("崩溃分析: 条件不满足，跳过分析");
            System.Diagnostics.Debug.WriteLine("游戏崩溃状态: " + _isGameCrashed);
            System.Diagnostics.Debug.WriteLine("是否正在分析: " + IsAiAnalyzing);
            System.Diagnostics.Debug.WriteLine("===============================================");
            return;
        }
        
        try
        {
            System.Diagnostics.Debug.WriteLine("==================== 崩溃分析 ====================");
            System.Diagnostics.Debug.WriteLine("崩溃分析: 开始分析崩溃原因");
            
            // 重置分析结果和状态
            IsAiAnalyzing = true;
            await EnqueueOnUiAsync(() =>
            {
                AiAnalysisResult = "正在分析崩溃原因...\n\n";
                HasFixAction = false;
                FixButtonText = string.Empty;
                _currentFixAction = null;
            });
            
            // 创建取消令牌
            _aiAnalysisCts = new System.Threading.CancellationTokenSource();

            var isAiEnabled = await _localSettingsService.ReadSettingAsync<bool?>("EnableAIAnalysis") ?? false;

            if (isAiEnabled)
            {
                // 仅使用外部 AI 分析（不执行知识库分析）
                var endpoint = await _localSettingsService.ReadSettingAsync<string>("AIApiEndpoint") ?? string.Empty;
                var storedKey = await _localSettingsService.ReadSettingAsync<string>("AIApiKey") ?? string.Empty;
                var key = TokenEncryption.IsEncrypted(storedKey) ? TokenEncryption.Decrypt(storedKey) : storedKey;
                var model = await _localSettingsService.ReadSettingAsync<string>("AIModel") ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(key))
                {
                    await EnqueueOnUiAsync(() =>
                    {
                        AiAnalysisResult = "正在进行外部 AI 分析...\n\n";
                        ResetFixActionState();
                    });

                    var sanitizedLog = await _logSanitizerService.SanitizeAsync(FullLog);
                    var fullTextBuilder = new StringBuilder();

                    _isToolBlockActive = false;
                    _toolTagCarry = string.Empty;
                    _toolBlockBuffer.Clear();

                    // Get current language from service
                    var langCode = _languageSelectorService.Language;
                    var languageForAi = (langCode == "zh-CN") ? "Simplified Chinese" : "English";
                    
                    await foreach (var chunk in _aiAnalysisService.StreamAnalyzeLogAsync(sanitizedLog, key, endpoint, model, languageForAi))
                    {
                        if (_aiAnalysisCts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        var visibleChunk = ConsumeToolBlockAndGetVisibleText(chunk);
                        if (!string.IsNullOrEmpty(visibleChunk))
                        {
                            fullTextBuilder.Append(visibleChunk);
                            var textChunk = visibleChunk;
                            await EnqueueOnUiAsync(() =>
                            {
                                AiAnalysisResult += textChunk;
                            });
                        }
                    }

                    if (!_isToolBlockActive && !string.IsNullOrEmpty(_toolTagCarry))
                    {
                        fullTextBuilder.Append(_toolTagCarry);
                        var carry = _toolTagCarry;
                        _toolTagCarry = string.Empty;
                        await EnqueueOnUiAsync(() =>
                        {
                            AiAnalysisResult += carry;
                        });
                    }
                    else if (_isToolBlockActive && !string.IsNullOrEmpty(_toolTagCarry))
                    {
                        _toolBlockBuffer.Append(_toolTagCarry);
                        _toolTagCarry = string.Empty;
                    }

                    var fullText = fullTextBuilder.ToString();
                    if (_toolBlockBuffer.Length > 0)
                    {
                        TryApplyToolCallsFromAiResponse($"{ToolStartTag}{_toolBlockBuffer}{ToolEndTag}");
                    }
                }
                else
                {
                    await EnqueueOnUiAsync(() =>
                    {
                        AiAnalysisResult = "未配置 API Key，无法进行 AI 分析。";
                    });
                }

                System.Diagnostics.Debug.WriteLine("崩溃分析: AI 分析完成");
                System.Diagnostics.Debug.WriteLine("===============================================");
                return;
            }
            
            // 使用知识库服务进行分析
            var crashAnalyzer = App.GetService<XianYuLauncher.Core.Contracts.Services.ICrashAnalyzer>();
            
            // 获取流式分析结果
            var buffered = new StringBuilder();
            var lastFlush = DateTime.UtcNow;
            const int flushIntervalMs = 10;
            const int flushSize = 80;

            await foreach (var chunk in crashAnalyzer.GetStreamingAnalysisAsync(0, _gameOutput, _gameError))
            {
                if (_aiAnalysisCts.Token.IsCancellationRequested)
                {
                    break;
                }

                buffered.Append(chunk);

                var now = DateTime.UtcNow;
                if (buffered.Length >= flushSize || (now - lastFlush).TotalMilliseconds >= flushIntervalMs)
                {
                    var text = buffered.ToString();
                    buffered.Clear();
                    lastFlush = now;

                    await EnqueueOnUiAsync(() =>
                    {
                        AiAnalysisResult += text;
                    });
                }
            }

            if (buffered.Length > 0)
            {
                var remaining = buffered.ToString();
                buffered.Clear();
                await EnqueueOnUiAsync(() =>
                {
                    AiAnalysisResult += remaining;
                });
            }

            // 获取结构化结果用于修复按钮
            var analysisResult = await crashAnalyzer.AnalyzeCrashAsync(0, _gameOutput, _gameError);
            await EnqueueOnUiAsync(() =>
            {
                ResetFixActionState();

                var actions = analysisResult.FixActions ?? new List<CrashFixAction>();
                if (actions.Count == 0 && analysisResult.FixAction != null)
                {
                    actions.Add(analysisResult.FixAction);
                }

                if (actions.Count > 0)
                {
                    _currentFixAction = actions[0];
                    FixButtonText = actions[0].ButtonText;
                    HasFixAction = !string.IsNullOrWhiteSpace(FixButtonText);
                }

                if (actions.Count > 1)
                {
                    _secondaryFixAction = actions[1];
                    SecondaryFixButtonText = actions[1].ButtonText;
                    HasSecondaryFixAction = !string.IsNullOrWhiteSpace(SecondaryFixButtonText);
                }
            });
            
            System.Diagnostics.Debug.WriteLine("崩溃分析: 分析完成");
            System.Diagnostics.Debug.WriteLine("===============================================");
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("崩溃分析: 分析被用户取消");
            System.Diagnostics.Debug.WriteLine("===============================================");
            
            // 用户取消了分析
            await EnqueueOnUiAsync(() =>
            {
                AiAnalysisResult += $"\n\n{GetLocalizedString("ErrorAnalysis_AnalysisCanceled.Text")}";
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("崩溃分析: 失败: " + ex.Message);
            System.Diagnostics.Debug.WriteLine("异常类型: " + ex.GetType().Name);
            System.Diagnostics.Debug.WriteLine("堆栈跟踪: " + ex.StackTrace);
            System.Diagnostics.Debug.WriteLine("===============================================");
            
            await EnqueueOnUiAsync(() =>
            {
                AiAnalysisResult += $"\n\n{string.Format(GetLocalizedString("ErrorAnalysis_AnalysisFailed.Text"), ex.Message)}";
            });
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
    /// 取消分析
    /// </summary>
    [RelayCommand]
    private void CancelAiAnalysis()
    {
        System.Diagnostics.Debug.WriteLine("崩溃分析: 收到取消分析请求");
        if (_aiAnalysisCts != null && !_aiAnalysisCts.IsCancellationRequested)
        {
            System.Diagnostics.Debug.WriteLine("崩溃分析: 执行取消操作");
            _aiAnalysisCts.Cancel();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("崩溃分析: 无法取消，当前没有正在进行的分析");
        }
    }

    private string TryApplyToolCallsFromAiResponse(string fullText)
    {
        var startIndex = fullText.IndexOf(ToolStartTag, StringComparison.OrdinalIgnoreCase);
        var endIndex = fullText.IndexOf(ToolEndTag, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex)
        {
            return fullText;
        }

        var jsonStart = startIndex + ToolStartTag.Length;
        var jsonText = fullText.Substring(jsonStart, endIndex - jsonStart).Trim();
        if (jsonText.Contains("```") )
        {
            var fenceStart = jsonText.IndexOf("```", StringComparison.OrdinalIgnoreCase);
            var fenceEnd = jsonText.LastIndexOf("```", StringComparison.OrdinalIgnoreCase);
            if (fenceStart >= 0 && fenceEnd > fenceStart)
            {
                var innerStart = jsonText.IndexOf('\n', fenceStart + 3);
                if (innerStart < 0)
                {
                    innerStart = fenceStart + 3;
                }
                var inner = jsonText.Substring(innerStart, fenceEnd - innerStart).Trim();
                jsonText = inner;
            }
            else
            {
                jsonText = jsonText.Replace("```", string.Empty).Trim();
            }
        }
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return fullText;
        }

        try
        {
            var array = JArray.Parse(jsonText);
            var actions = new List<CrashFixAction>();

            foreach (var item in array)
            {
                var type = item["type"]?.ToString() ?? string.Empty;
                var buttonText = item["buttonText"]?.ToString() ?? string.Empty;
                var parameters = new Dictionary<string, string>();

                var paramObj = item["parameters"] as JObject;
                if (paramObj != null)
                {
                    foreach (var prop in paramObj.Properties())
                    {
                        parameters[prop.Name] = prop.Value?.ToString() ?? string.Empty;
                    }
                }

                if (type.Trim().Equals("deleteMod", StringComparison.OrdinalIgnoreCase))
                {
                    if (!parameters.ContainsKey("modId") && parameters.TryGetValue("modFile", out var modFile))
                    {
                        parameters["modId"] = modFile;
                    }
                }

                if (!string.IsNullOrWhiteSpace(type) && !string.IsNullOrWhiteSpace(buttonText))
                {
                    actions.Add(new CrashFixAction
                    {
                        Type = type,
                        ButtonText = buttonText,
                        Parameters = parameters
                    });
                }
            }

            if (actions.Count > 0)
            {
                var prettyToolJson = array.ToString(Newtonsoft.Json.Formatting.Indented);
                System.Diagnostics.Debug.WriteLine($"[AI] ToolCalls:\n{prettyToolJson}");

                _ = EnqueueOnUiAsync(() =>
                {
                    ResetFixActionState();

                    _currentFixAction = actions[0];
                    FixButtonText = actions[0].ButtonText;
                    HasFixAction = !string.IsNullOrWhiteSpace(FixButtonText);

                    if (actions.Count > 1)
                    {
                        _secondaryFixAction = actions[1];
                        SecondaryFixButtonText = actions[1].ButtonText;
                        HasSecondaryFixAction = !string.IsNullOrWhiteSpace(SecondaryFixButtonText);
                    }
                });

                var codeBlockStart = fullText.LastIndexOf("```", startIndex, StringComparison.OrdinalIgnoreCase);
                var codeBlockEnd = fullText.IndexOf("```", endIndex + ToolEndTag.Length, StringComparison.OrdinalIgnoreCase);

                string cleaned;
                if (codeBlockStart >= 0 && codeBlockEnd > endIndex)
                {
                    var beforeBlock = fullText.Substring(0, codeBlockStart).TrimEnd();
                    var afterBlock = fullText.Substring(codeBlockEnd + 3).TrimStart();
                    cleaned = string.IsNullOrEmpty(beforeBlock)
                        ? afterBlock
                        : string.IsNullOrEmpty(afterBlock)
                            ? beforeBlock
                            : beforeBlock + "\n\n" + afterBlock;
                }
                else
                {
                    var before = fullText.Substring(0, startIndex).TrimEnd();
                    var after = fullText.Substring(endIndex + ToolEndTag.Length).TrimStart();
                    cleaned = string.IsNullOrEmpty(before)
                        ? after
                        : string.IsNullOrEmpty(after)
                            ? before
                            : before + "\n\n" + after;
                }

                return cleaned;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AI ToolCall 解析失败: {ex.Message}");
            return fullText;
        }

        var fallbackBefore = fullText.Substring(0, startIndex).TrimEnd();
        var fallbackAfter = fullText.Substring(endIndex + ToolEndTag.Length).TrimStart();
        if (string.IsNullOrEmpty(fallbackBefore))
        {
            return fallbackAfter;
        }

        if (string.IsNullOrEmpty(fallbackAfter))
        {
            return fallbackBefore;
        }

        return fallbackBefore + "\n\n" + fallbackAfter;
    }

    private string ConsumeToolBlockAndGetVisibleText(string chunk)
    {
        var data = _toolTagCarry + chunk;
        _toolTagCarry = string.Empty;

        var output = new StringBuilder();
        var index = 0;
        var startTagLen = ToolStartTag.Length;
        var endTagLen = ToolEndTag.Length;

        while (index < data.Length)
        {
            if (!_isToolBlockActive)
            {
                var start = data.IndexOf(ToolStartTag, index, StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                {
                    var remaining = data.Substring(index);
                    if (remaining.Length >= startTagLen)
                    {
                        var safeLen = remaining.Length - (startTagLen - 1);
                        output.Append(remaining.Substring(0, safeLen));
                        _toolTagCarry = remaining.Substring(safeLen);
                    }
                    else
                    {
                        _toolTagCarry = remaining;
                    }
                    break;
                }

                output.Append(data.Substring(index, start - index));
                _isToolBlockActive = true;
                index = start + startTagLen;
            }
            else
            {
                var end = data.IndexOf(ToolEndTag, index, StringComparison.OrdinalIgnoreCase);
                if (end < 0)
                {
                    var remaining = data.Substring(index);
                    if (remaining.Length >= endTagLen)
                    {
                        var safeLen = remaining.Length - (endTagLen - 1);
                        _toolBlockBuffer.Append(remaining.Substring(0, safeLen));
                        _toolTagCarry = remaining.Substring(safeLen);
                    }
                    else
                    {
                        _toolTagCarry = remaining;
                    }
                    break;
                }

                _toolBlockBuffer.Append(data.Substring(index, end - index));
                _isToolBlockActive = false;
                index = end + endTagLen;
            }
        }

        return output.ToString();
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
    /// 设置版本信息（用于导出日志时包含 version.json）
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="minecraftPath">Minecraft 路径</param>
    public void SetVersionInfo(string versionId, string minecraftPath)
    {
        _versionId = versionId;
        _minecraftPath = minecraftPath;
        System.Diagnostics.Debug.WriteLine($"ErrorAnalysisViewModel: 设置版本信息，版本ID: {versionId}");
    }
    
    /// <summary>
    /// 仅清空日志数据，保留启动命令（用于实时日志模式）
    /// </summary>
    public void ClearLogsOnly()
    {
        System.Diagnostics.Debug.WriteLine("ErrorAnalysisViewModel: 仅清空日志数据，保留启动命令");
        
        // 取消正在进行的流式分析
        if (_aiAnalysisCts != null && !_aiAnalysisCts.IsCancellationRequested)
        {
            System.Diagnostics.Debug.WriteLine("ErrorAnalysisViewModel: 取消正在进行的崩溃分析");
            _aiAnalysisCts.Cancel();
        }
        
        // 重置崩溃分析结果
        IsAiAnalyzing = false;
        IsAiAnalysisAvailable = false;
        AiAnalysisResult = GetLocalizedString("ErrorAnalysis_NoErrorInfo.Text");
        ResetFixActionState();
        
        // 清空日志但保留启动命令
        _gameOutput = new List<string>();
        _gameError = new List<string>();
        
        // 清空UI集合
        LogLines.Clear();
        LogLines.Add("=== 实时游戏日志 ===");
        LogLines.Add($"日志开始时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        LogLines.Add("");
        LogLines.Add("等待游戏输出...");
        
        // 生成完整日志
        GenerateFullLog();
    }

    /// <summary>
    /// 重置智能修复按钮状态
    /// </summary>
    public void ResetFixActionState()
    {
        HasFixAction = false;
        FixButtonText = string.Empty;
        _currentFixAction = null;
        HasSecondaryFixAction = false;
        SecondaryFixButtonText = string.Empty;
        _secondaryFixAction = null;
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
        }
        
        // 直接添加到UI集合，利用虚拟化提升性能
        AddLogLineToUI(logLine);
        
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
        }
        
        // 直接添加到UI集合，利用虚拟化提升性能
        AddLogLineToUI(logLine);
        
        ScheduleLogUpdate();
    }
    
    /// <summary>
    /// 添加日志行到UI集合（在UI线程上执行）
    /// </summary>
    private void AddLogLineToUI(string line)
    {
        // 确保在UI线程上执行
        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue == null && App.MainWindow != null)
        {
            dispatcherQueue = App.MainWindow.DispatcherQueue;
        }
        
        if (dispatcherQueue != null)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                LogLines.Add(line);
            });
        }
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
            // 检查是否为正常启动过程（避免误判）
            if (IsNormalStartup(gameOutput, gameError))
            {
                return "游戏正在启动中";
            }
            
            // 检查手动触发崩溃
            if (ContainsKeyword(gameError, "Manually triggered debug crash") || ContainsKeyword(gameOutput, "Manually triggered debug crash"))
            {
                return "玩家手动触发崩溃";
            }
            
            // 检查 Java 版本不匹配 - 使用多个关键词提高检测准确性
            if (ContainsKeyword(gameError, "UnsupportedClassVersionError") || ContainsKeyword(gameOutput, "UnsupportedClassVersionError") ||
                ContainsKeyword(gameError, "java.lang.UnsupportedClassVersionError") || ContainsKeyword(gameOutput, "java.lang.UnsupportedClassVersionError") ||
                ContainsKeyword(gameError, "class file version") || ContainsKeyword(gameOutput, "class file version") ||
                ContainsKeyword(gameError, "compiled by a more recent version") || ContainsKeyword(gameOutput, "compiled by a more recent version"))
            {
                return "Java 版本不匹配 - 需要更高版本的 Java";
            }
            
            // 检查致命错误
            if (ContainsKeyword(gameError, "[Fatal Error]") || ContainsKeyword(gameOutput, "[Fatal Error]"))
            {
                return "致命错误导致崩溃";
            }
            
            // 检查Java异常（排除警告中的异常）
            if (ContainsKeywordWithContext(gameError, "java.lang.Exception", "[ERROR]", "[FATAL]") || 
                ContainsKeywordWithContext(gameOutput, "java.lang.Exception", "[ERROR]", "[FATAL]"))
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
            
            // 检查真正的错误（不是警告）
            if (ContainsKeyword(gameError, "[ERROR]") || ContainsKeyword(gameOutput, "[ERROR]") ||
                ContainsKeyword(gameError, "[FATAL]") || ContainsKeyword(gameOutput, "[FATAL]"))
            {
                return "错误日志中存在错误信息";
            }

            return "未知崩溃原因";
        }
        
        /// <summary>
        /// 检查是否为正常启动过程
        /// </summary>
        private bool IsNormalStartup(List<string> gameOutput, List<string> gameError)
        {
            // 合并所有日志
            var allLogs = new List<string>();
            allLogs.AddRange(gameOutput);
            allLogs.AddRange(gameError);
            
            // 检查是否包含正常启动的标志
            bool hasLoadingMessage = ContainsKeyword(allLogs, "Loading Minecraft") ||
                                    ContainsKeyword(allLogs, "Loading mods") ||
                                    ContainsKeyword(allLogs, "Datafixer optimizations");
            
            // 检查是否只有警告而没有真正的错误
            bool hasOnlyWarnings = (ContainsKeyword(allLogs, "[WARN]") || ContainsKeyword(allLogs, "[INFO]")) && 
                                  !ContainsKeyword(allLogs, "[ERROR]") && 
                                  !ContainsKeyword(allLogs, "[FATAL]");
            
            // 检查是否包含 Mixin 相关的警告（这些通常是正常的）
            bool hasMixinWarnings = ContainsKeyword(allLogs, "Reference map") ||
                                   ContainsKeyword(allLogs, "@Mixin target") ||
                                   ContainsKeyword(allLogs, "Force-disabling mixin");
            
            // 如果有加载信息且只有警告（特别是 Mixin 警告），认为是正常启动
            return hasLoadingMessage && hasOnlyWarnings && hasMixinWarnings;
        }
        
        /// <summary>
        /// 检查关键词是否在特定上下文中出现
        /// </summary>
        private bool ContainsKeywordWithContext(List<string> lines, string keyword, params string[] contextKeywords)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    // 检查是否包含任何上下文关键词
                    foreach (var contextKeyword in contextKeywords)
                    {
                        if (lines[i].Contains(contextKeyword, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
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

                // 1. 生成启动参数.bat文件 (已脱敏)
                string batFilePath = Path.Combine(tempDir, "启动参数.bat");
                string sanitizedLaunchCommand = await _logSanitizerService.SanitizeAsync(_launchCommand);
                await File.WriteAllTextAsync(batFilePath, sanitizedLaunchCommand);

                // 2. 生成崩溃日志文件 (已脱敏)
                string crashLogFile = Path.Combine(tempDir, string.Format("crash_report_{0}.txt", timestamp));
                string sanitizedCrashLog = await _logSanitizerService.SanitizeAsync(FullLog);
                await File.WriteAllTextAsync(crashLogFile, sanitizedCrashLog);

                // 3. 复制启动器日志
                try
                {
                    string launcherLogDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "XianYuLauncher",
                        "logs");
                    
                    if (Directory.Exists(launcherLogDir))
                    {
                        // 创建日志子目录
                        string logSubDir = Path.Combine(tempDir, "launcher_logs");
                        Directory.CreateDirectory(logSubDir);
                        
                        // 复制最近的日志文件（最多3个）
                        var logFiles = Directory.GetFiles(launcherLogDir, "log-*.txt")
                            .OrderByDescending(f => File.GetLastWriteTime(f))
                            .Take(3);
                        
                        foreach (var logFile in logFiles)
                        {
                            try 
                            {
                                string fileName = Path.GetFileName(logFile);
                                string destPath = Path.Combine(logSubDir, fileName);
                                
                                // 读取并脱敏后再保存
                                // 使用 FileStream 以 FileShare.ReadWrite 方式打开，避免被日志记录器锁住无法读取
                                using (var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                using (var streamReader = new StreamReader(fileStream))
                                {
                                    string content = await streamReader.ReadToEndAsync();
                                    string sanitizedContent = await _logSanitizerService.SanitizeAsync(content);
                                    await File.WriteAllTextAsync(destPath, sanitizedContent);
                                }
                            }
                            catch (IOException)
                            {
                                // 如果无法读取（被完全锁住），尝试直接复制作为后备方案
                                // 虽然不会脱敏，但至少能保住日志（或者选择跳过以保护隐私，这里选择跳过比较安全）
                                System.Diagnostics.Debug.WriteLine($"无法读取日志文件进行脱敏: {logFile}");
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"已复制 {logFiles.Count()} 个启动器日志文件");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"复制启动器日志失败: {ex.Message}");
                    // 不中断导出流程，继续执行
                }

                // 4. 复制 version.json
                try
                {
                    if (!string.IsNullOrEmpty(_versionId) && !string.IsNullOrEmpty(_minecraftPath))
                    {
                        string versionJsonPath = Path.Combine(_minecraftPath, "versions", _versionId, $"{_versionId}.json");
                        
                        if (File.Exists(versionJsonPath))
                        {
                            string destPath = Path.Combine(tempDir, "version.json");
                            File.Copy(versionJsonPath, destPath);
                            System.Diagnostics.Debug.WriteLine($"已复制 version.json: {versionJsonPath}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"version.json 不存在: {versionJsonPath}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("版本信息未设置，跳过 version.json 复制");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"复制 version.json 失败: {ex.Message}");
                    // 不中断导出流程，继续执行
                }

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
                    Content = string.Format("崩溃日志已成功导出到：{0}\n\n包含内容：\n• 游戏崩溃日志\n• 启动参数\n• 启动器日志\n• 版本配置文件", zipFilePath),
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
            LogLines.Clear();
            LogLines.Add("日志已清空");
            
            // 同时重置崩溃分析结果为默认文字
            AiAnalysisResult = GetLocalizedString("ErrorAnalysis_NoErrorInfo.Text");
            IsAiAnalysisAvailable = false;
        }
    }
}