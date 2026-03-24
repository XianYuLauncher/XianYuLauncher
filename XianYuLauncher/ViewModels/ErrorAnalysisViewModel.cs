using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Windows.ApplicationModel.DataTransfer;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Features.ErrorAnalysis.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.ViewModels
{
    public partial class ErrorAnalysisViewModel : ObservableObject
    {
        private readonly ILanguageSelectorService _languageSelectorService;
        private readonly IErrorAnalysisLogService _logService;
        private readonly IErrorAnalysisAiOrchestrator _aiOrchestrator;
        private readonly IErrorAnalysisExportService _exportService;
        private readonly ICommonDialogService _dialogService;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly ErrorAnalysisSessionState _sessionState;
        private readonly ResourceManager _resourceManager;
        private ResourceContext _resourceContext;

        public string FullLog
        {
            get => _sessionState.FullLog;
            set => _sessionState.FullLog = value;
        }

        public string CrashReason
        {
            get => _sessionState.CrashReason;
            set => _sessionState.CrashReason = value;
        }

        public ObservableCollection<string> LogLines => _sessionState.LogLines;

        /// <summary>
        /// 独立 Fixer 聊天窗口是否打开（打开时离开分析页不清空聊天）
        /// </summary>
        public bool IsFixerWindowOpen
        {
            get => _sessionState.IsFixerWindowOpen;
            set => _sessionState.IsFixerWindowOpen = value;
        }

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
            IErrorAnalysisLogService logService,
            IErrorAnalysisAiOrchestrator aiOrchestrator,
            IErrorAnalysisExportService exportService,
            ICommonDialogService dialogService,
            IUiDispatcher uiDispatcher,
            ErrorAnalysisSessionState sessionState)
        {
            _languageSelectorService = languageSelectorService;
            _logService = logService;
            _aiOrchestrator = aiOrchestrator;
            _exportService = exportService;
            _dialogService = dialogService;
            _uiDispatcher = uiDispatcher;
            _sessionState = sessionState;
            _resourceManager = new ResourceManager();
            _resourceContext = _resourceManager.CreateResourceContext();

            _sessionState.PropertyChanged += SessionState_PropertyChanged;
        }

        private void SessionState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.PropertyName))
            {
                return;
            }

            void Notify()
            {
                OnPropertyChanged(e.PropertyName);

                if (e.PropertyName == nameof(ErrorAnalysisSessionState.IsAiAnalyzing))
                {
                    OnPropertyChanged(nameof(IsAnalyzeButtonEnabled));
                    OnPropertyChanged(nameof(CancelButtonVisibility));
                }
            }

            if (_uiDispatcher.HasThreadAccess)
            {
                Notify();
                return;
            }

            if (!_uiDispatcher.TryEnqueue(Notify))
            {
                Notify();
            }
        }

        /// <summary>
        /// 分析日志
        /// </summary>
        public async Task AnalyzeLogAsync()
        {
            await AnalyzeWithAiAsync();
        }

        public string NoErrorInfoText => GetLocalizedString("ErrorAnalysis_NoErrorInfo.Text");


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

        public string AiAnalysisResult
        {
            get => _sessionState.AiAnalysisResult;
            set => _sessionState.AiAnalysisResult = value;
        }

        public async Task ClearChatStateAsync()
        {
            CancelActiveAiAnalysis();

            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                ChatMessages.Clear();
                ChatInput = string.Empty;
                IsChatEnabled = false;
                ResetFixActionState();
            });

            DisposeAiAnalysisToken();
        }

        // Chat Properties
        public ObservableCollection<UiChatMessage> ChatMessages => _sessionState.ChatMessages;

        /// <summary>
        /// 是否有聊天消息（用于控制空状态占位提示的显示）
        /// </summary>
        public bool HasChatMessages
        {
            get => _sessionState.HasChatMessages;
            set => _sessionState.HasChatMessages = value;
        }

        public string ChatInput
        {
            get => _sessionState.ChatInput;
            set => _sessionState.ChatInput = value;
        }

        public bool IsChatEnabled
        {
            get => _sessionState.IsChatEnabled;
            set => _sessionState.IsChatEnabled = value;
        }

        // 新增：智能修复相关属性
        public bool HasFixAction
        {
            get => _sessionState.HasFixAction;
            set => _sessionState.HasFixAction = value;
        }

        public string FixButtonText
        {
            get => _sessionState.FixButtonText;
            set => _sessionState.FixButtonText = value;
        }

        public bool HasSecondaryFixAction
        {
            get => _sessionState.HasSecondaryFixAction;
            set => _sessionState.HasSecondaryFixAction = value;
        }

        public string SecondaryFixButtonText
        {
            get => _sessionState.SecondaryFixButtonText;
            set => _sessionState.SecondaryFixButtonText = value;
        }

        private CrashFixAction? _currentFixAction
        {
            get => _sessionState.CurrentFixAction;
            set => _sessionState.CurrentFixAction = value;
        }

        private CrashFixAction? _secondaryFixAction
        {
            get => _sessionState.SecondaryFixAction;
            set => _sessionState.SecondaryFixAction = value;
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
                var currentLoader = await GetCurrentLoaderType();
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
                    case "togglemod":
                        await ExecuteToggleModAsync(_currentFixAction.Parameters);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                await _uiDispatcher.RunOnUiThreadAsync(() =>
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
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    AiAnalysisResult += $"\n\n{string.Format(GetLocalizedString("ErrorAnalysis_RequestFailed.Text"), ex.Message)}";
                });
            }
        }

        [RelayCommand]
        private async Task RejectFixAction()
        {
            var rejectedText = FixButtonText;
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                ResetFixActionState();

                if (!string.IsNullOrWhiteSpace(rejectedText))
                {
                    ChatMessages.Add(new UiChatMessage("assistant", $"已拒绝执行：{rejectedText}"));
                }
                else
                {
                    ChatMessages.Add(new UiChatMessage("assistant", "已拒绝执行该操作。"));
                }
            });
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
            List<List<string>>? facets = null;
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
                await _uiDispatcher.RunOnUiThreadAsync(() =>
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
                    await _uiDispatcher.RunOnUiThreadAsync(() =>
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
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    AiAnalysisResult += $"\n\n未找到可用的 Java {requiredMajorVersion} 版本，请先安装对应版本后再重试。";
                });
                return;
            }

            await _uiDispatcher.RunOnUiThreadAsync(() =>
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
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    AiAnalysisResult += $"\n\n未找到 Mod 文件：{modId}";
                });
                return;
            }

            bool shouldDelete = false;
            await _uiDispatcher.RunOnUiThreadAsync(async () =>
            {
                shouldDelete = await _dialogService.ShowConfirmationDialogAsync(
                    "删除 Mod",
                    $"确定要删除该 Mod 吗？\n\n文件名：{Path.GetFileName(modFilePath)}\n路径：{modFilePath}\n\n注意：如果这是依赖库，可能会影响其它 Mod。",
                    "删除",
                    "取消");
            });

            if (!shouldDelete)
            {
                return;
            }

            try
            {
                File.Delete(modFilePath);
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    AiAnalysisResult += $"\n\n已删除 Mod：{Path.GetFileName(modFilePath)}";
                });
            }
            catch (Exception ex)
            {
                await _uiDispatcher.RunOnUiThreadAsync(() =>
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
            var globalModsPath = Path.Combine(_minecraftPath, MinecraftPathConsts.Mods);
            if (Directory.Exists(globalModsPath))
            {
                candidateFiles.AddRange(Directory.GetFiles(globalModsPath, "*.jar*"));
            }

            if (!string.IsNullOrWhiteSpace(_versionId))
            {
                var versionModsPath = Path.Combine(_minecraftPath, MinecraftPathConsts.Versions, _versionId, MinecraftPathConsts.Mods);
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
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    AiAnalysisResult += $"\n\nCurseForge 搜索失败: {ex.Message}";
                });
                return false;
            }
        }

        private async Task ShowNotFoundDialogAsync(string query)
        {
            await _uiDispatcher.RunOnUiThreadAsync(async () =>
            {
                await _dialogService.ShowMessageDialogAsync(
                    "未找到",
                    $"未在 Modrinth 或 CurseForge 找到与 '{query}' 对应的项目。",
                    "确定");
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

        private async Task<string> GetCurrentLoaderType()
        {
            if (string.IsNullOrWhiteSpace(_versionId) || string.IsNullOrWhiteSpace(_minecraftPath))
            {
                return string.Empty;
            }

            try
            {
                var versionDirectory = Path.Combine(_minecraftPath, MinecraftPathConsts.Versions, _versionId);
                var versionInfoService = App.GetService<XianYuLauncher.Core.Services.IVersionInfoService>();
                var config = await versionInfoService.GetFullVersionInfoAsync(_versionId, versionDirectory);
                return config?.ModLoaderType?.Trim().ToLowerInvariant() ?? string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取版本加载器失败: {ex.Message}");
                return string.Empty;
            }
        }
        
        public bool IsAiAnalyzing
        {
            get => _sessionState.IsAiAnalyzing;
            set
            {
                if (_sessionState.IsAiAnalyzing != value)
                {
                    _sessionState.IsAiAnalyzing = value;
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
                    // 确保在 UI 线程上触发 PropertyChanged 事件
                    if (!_uiDispatcher.TryEnqueue(() =>
                    {
                        OnPropertyChanged(nameof(IsAiAnalysisAvailable));
                        OnPropertyChanged(nameof(IsAnalyzeButtonEnabled));
                        OnPropertyChanged(nameof(AnalyzeButtonVisibility));
                    }))
                    {
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
    
    // 手动实现了属性，不再需要自动生成的 partial 方法
        
        // 原始日志数据
        private string _originalLog
        {
            get => _sessionState.Context.OriginalLog;
            set => _sessionState.Context.OriginalLog = value;
        }

        private string _launchCommand
        {
            get => _sessionState.Context.LaunchCommand;
            set => _sessionState.Context.LaunchCommand = value;
        }

        private List<string> _gameOutput
        {
            get => _sessionState.Context.GameOutput;
            set => _sessionState.ReplaceGameOutput(value);
        }

        private List<string> _gameError
        {
            get => _sessionState.Context.GameError;
            set => _sessionState.ReplaceGameError(value);
        }

        private bool _isGameCrashed
        {
            get => _sessionState.Context.IsGameCrashed;
            set => _sessionState.Context.IsGameCrashed = value;
        }

        private string _versionId
        {
            get => _sessionState.Context.VersionId;
            set => _sessionState.Context.VersionId = value;
        }

        private string _minecraftPath
        {
            get => _sessionState.Context.MinecraftPath;
            set => _sessionState.Context.MinecraftPath = value;
        }
        
        // 用于存储当前崩溃分析的取消令牌
        private System.Threading.CancellationTokenSource? _aiAnalysisCts;

        private System.Threading.CancellationToken BeginAiAnalysisToken()
        {
            _aiAnalysisCts?.Dispose();
            _aiAnalysisCts = new System.Threading.CancellationTokenSource();
            return _aiAnalysisCts.Token;
        }

        private void CancelActiveAiAnalysis()
        {
            if (_aiAnalysisCts != null && !_aiAnalysisCts.IsCancellationRequested)
            {
                _aiAnalysisCts.Cancel();
            }
        }

        private void DisposeAiAnalysisToken()
        {
            _aiAnalysisCts?.Dispose();
            _aiAnalysisCts = null;
        }

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

        _logService.SetLogData(launchCommand, gameOutput, gameError);
    }
    
    /// <summary>
    /// 设置游戏崩溃状态，只有在游戏崩溃时才会触发 AI 分析
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
            _ = AnalyzeWithAiAsync();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("AI分析: 游戏正常退出，不触发AI分析");
        }
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(ChatInput)) return;
        if (IsAiAnalyzing) return;

        var userMsg = ChatInput.Trim();
        ChatInput = string.Empty;

        var cancellationToken = BeginAiAnalysisToken();
        try
        {
            await _aiOrchestrator.SendMessageAsync(userMsg, ExecuteToolCallAsync, cancellationToken);
        }
        finally
        {
            DisposeAiAnalysisToken();
        }
    }
    /// <summary>
    /// 执行单个工具调用，返回结果字符串
    /// </summary>
    private async Task<string> ExecuteToolCallAsync(ToolCallInfo toolCall)
    {
        try
        {
            var args = string.IsNullOrWhiteSpace(toolCall.Arguments)
                ? new JObject()
                : JObject.Parse(toolCall.Arguments);

            return toolCall.FunctionName switch
            {
                // 查询类工具
                "listInstalledMods" => await ExecuteListInstalledModsAsync(),
                "getVersionConfig" => await ExecuteGetVersionConfigAsync(),
                "checkJavaVersions" => await ExecuteCheckJavaVersionsAsync(),
                "searchKnowledgeBase" => await ExecuteSearchKnowledgeBaseAsync(args),
                "readModInfo" => await ExecuteReadModInfoAsync(args),

                // 操作类工具 — 返回操作结果，同时设置 UI 修复按钮
                "searchModrinthProject" => await ExecuteToolSearchModrinthAsync(args),
                "deleteMod" => await ExecuteToolDeleteModAsync(args),
                "toggleMod" => await ExecuteToolToggleModAsync(args),
                "switchJavaForVersion" => await ExecuteToolSwitchJavaAsync(),

                _ => $"未知工具: {toolCall.FunctionName}"
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AI Tool Error] {toolCall.FunctionName}: {ex.Message}");
            return $"工具执行失败: {ex.Message}";
        }
    }

    // ===== 查询类工具实现 =====

    private async Task<string> ExecuteListInstalledModsAsync()
    {
        if (string.IsNullOrWhiteSpace(_versionId) || string.IsNullOrWhiteSpace(_minecraftPath))
            return "无法获取版本信息，未设置版本 ID 或 Minecraft 路径。";

        var modsPath = Path.Combine(_minecraftPath, MinecraftPathConsts.Versions, _versionId, MinecraftPathConsts.Mods);
        if (!Directory.Exists(modsPath))
        {
            // 尝试全局 mods 目录
            modsPath = Path.Combine(_minecraftPath, MinecraftPathConsts.Mods);
        }
        if (!Directory.Exists(modsPath))
            return "未找到 mods 目录，当前版本可能没有安装任何 Mod。";

        var files = Directory.GetFiles(modsPath, $"*{FileExtensionConsts.Jar}")
            .Concat(Directory.GetFiles(modsPath, $"*{FileExtensionConsts.JarDisabled}"))
            .ToList();

        if (files.Count == 0)
            return "mods 目录为空，没有安装任何 Mod。";

        var sb = new StringBuilder();
        sb.AppendLine($"共 {files.Count} 个 Mod 文件：");
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var enabled = !fileName.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase);
            sb.AppendLine($"- {fileName} [{(enabled ? "启用" : "禁用")}]");
        }
        return sb.ToString();
    }

    private async Task<string> ExecuteGetVersionConfigAsync()
    {
        if (string.IsNullOrWhiteSpace(_versionId) || string.IsNullOrWhiteSpace(_minecraftPath))
            return "未设置版本信息。";

        try
        {
            var versionDirectory = Path.Combine(_minecraftPath, MinecraftPathConsts.Versions, _versionId);
            var versionInfoService = App.GetService<IVersionInfoService>();
            var config = await versionInfoService.GetFullVersionInfoAsync(_versionId, versionDirectory, preferCache: true);

            var sb = new StringBuilder();
            sb.AppendLine($"版本 ID: {_versionId}");
            sb.AppendLine($"Minecraft 版本: {config.MinecraftVersion}");
            sb.AppendLine($"ModLoader: {config.ModLoaderType ?? "vanilla"} {config.ModLoaderVersion ?? ""}");
            sb.AppendLine($"Java 路径: {(string.IsNullOrEmpty(config.JavaPath) ? "使用全局设置" : config.JavaPath)}");
            sb.AppendLine($"内存设置: 自动={config.AutoMemoryAllocation}, 初始={config.InitialHeapMemory}GB, 最大={config.MaximumHeapMemory}GB");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"读取版本配置失败: {ex.Message}";
        }
    }

    private async Task<string> ExecuteCheckJavaVersionsAsync()
    {
        try
        {
            var javaRuntimeService = App.GetService<IJavaRuntimeService>();
            var javaVersions = await javaRuntimeService.DetectJavaVersionsAsync(true);

            if (javaVersions == null || javaVersions.Count == 0)
                return "未检测到任何已安装的 Java 版本。";

            var sb = new StringBuilder();
            sb.AppendLine($"检测到 {javaVersions.Count} 个 Java 版本：");
            foreach (var jv in javaVersions)
            {
                sb.AppendLine($"- Java {jv.MajorVersion} ({jv.FullVersion}) - {jv.Path}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"检测 Java 版本失败: {ex.Message}";
        }
    }

    private async Task<string> ExecuteSearchKnowledgeBaseAsync(JObject args)
    {
        var keyword = args["keyword"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(keyword))
            return "请提供搜索关键词。";

        try
        {
            var crashAnalyzer = App.GetService<ICrashAnalyzer>();
            // 用关键词构造一个模拟日志行来匹配知识库
            var fakeOutput = new List<string> { keyword };
            var result = await crashAnalyzer.AnalyzeCrashAsync(0, fakeOutput, fakeOutput);

            if (result.Type == CrashType.Unknown)
                return $"知识库中未找到与 \"{keyword}\" 匹配的错误规则。";

            var sb = new StringBuilder();
            sb.AppendLine($"匹配到错误: {result.Title}");
            sb.AppendLine($"类型: {result.Type}");
            sb.AppendLine($"分析: {result.Analysis}");
            if (result.Suggestions.Count > 0)
            {
                sb.AppendLine("建议:");
                foreach (var s in result.Suggestions) sb.AppendLine($"  - {s}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"搜索知识库失败: {ex.Message}";
        }
    }

    private async Task<string> ExecuteReadModInfoAsync(JObject args)
    {
        var fileName = args["fileName"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
            return "请提供 Mod 文件名。";

        // 安全：只取文件名部分
        fileName = Path.GetFileName(fileName);

        var filePath = await FindModFileByIdAsync(fileName);
        if (string.IsNullOrWhiteSpace(filePath))
            return $"未找到 Mod 文件: {fileName}";

        try
        {
            // 尝试读取 fabric.mod.json
            var modId = await TryGetFabricModIdAsync(filePath);
            if (!string.IsNullOrEmpty(modId))
                return $"Mod文件: {fileName}\nFabric Mod ID: {modId}\n(完整元数据需解压 jar 读取 fabric.mod.json)";

            return $"Mod文件: {fileName}\n无法解析元数据（可能不是 Fabric mod，或 jar 格式异常）";
        }
        catch (Exception ex)
        {
            return $"读取 Mod 信息失败: {ex.Message}";
        }
    }

    // ===== 操作类工具实现（包装现有方法）=====

    private async Task<string> ExecuteToolSearchModrinthAsync(JObject args)
    {
        var query = args["query"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
            return "请提供搜索关键词。";

        var parameters = new Dictionary<string, string> { ["query"] = query };
        if (args["projectType"] != null) parameters["projectType"] = args["projectType"]!.ToString();
        if (args["loader"] != null) parameters["loader"] = args["loader"]!.ToString();

        // 自动填充当前 loader
        var currentLoader = await GetCurrentLoaderType();
        if (!string.IsNullOrWhiteSpace(currentLoader) && !parameters.ContainsKey("loader"))
            parameters["loader"] = currentLoader;

        // 设置修复按钮让用户点击执行导航
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            ResetFixActionState();
            _currentFixAction = new CrashFixAction
            {
                Type = "searchModrinthProject",
                ButtonText = $"搜索 {query}",
                Parameters = parameters
            };
            FixButtonText = _currentFixAction.ButtonText;
            HasFixAction = true;
        });

        return $"已准备搜索 \"{query}\"，用户可点击按钮执行搜索并查看结果。";
    }

    private async Task<string> ExecuteToolDeleteModAsync(JObject args)
    {
        var modId = args["modId"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(modId))
            return "请提供要删除的 Mod ID 或文件名。";

        modId = Path.GetFileName(modId);
        var parameters = new Dictionary<string, string> { ["modId"] = modId };

        // 设置修复按钮让用户确认
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            ResetFixActionState();
            _currentFixAction = new CrashFixAction
            {
                Type = "deleteMod",
                ButtonText = $"删除 {modId}",
                Parameters = parameters
            };
            FixButtonText = _currentFixAction.ButtonText;
            HasFixAction = true;
        });

        return $"已准备删除 Mod \"{modId}\"，等待用户确认。";
    }

    private async Task<string> ExecuteToolToggleModAsync(JObject args)
    {
        var fileName = args["fileName"]?.ToString() ?? string.Empty;
        var enabled = args["enabled"]?.Value<bool>() ?? true;

        if (string.IsNullOrWhiteSpace(fileName))
            return "请提供 Mod 文件名。";

        fileName = Path.GetFileName(fileName);
        var parameters = new Dictionary<string, string>
        {
            ["fileName"] = fileName,
            ["enabled"] = enabled.ToString()
        };

        // 设置修复按钮让用户确认
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            ResetFixActionState();
            _currentFixAction = new CrashFixAction
            {
                Type = "toggleMod",
                ButtonText = $"{(enabled ? "启用" : "禁用")} {fileName}",
                Parameters = parameters
            };
            FixButtonText = _currentFixAction.ButtonText;
            HasFixAction = true;
        });
        
        return $"已准备{(enabled ? "启用" : "禁用")} Mod \"{fileName}\"，用户可点击按钮确认执行操作。";
    }
    
    private async Task ExecuteToggleModAsync(Dictionary<string, string> parameters)
    {
        var fileName = parameters["fileName"];
        var enabled = bool.Parse(parameters["enabled"]);
        
        try
        {
            // 查找文件逻辑
            var filePath = await FindModFileByIdAsync(fileName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                // 如果找不到精确匹配，尝试带/不带 .disabled 后缀
                var candidateNames = new List<string>();
                if (enabled)
                {
                    candidateNames.Add(fileName + FileExtensionConsts.Disabled);
                }
                else
                {
                    if (fileName.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase))
                    {
                        candidateNames.Add(fileName[..^FileExtensionConsts.Disabled.Length]);
                    }
                    else
                    {
                        candidateNames.Add(fileName + FileExtensionConsts.Disabled);
                    }
                }

                foreach (var candidateName in candidateNames)
                {
                    filePath = await FindModFileByIdAsync(candidateName);
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    AiAnalysisResult += $"\n\n失败: 未找到 Mod 文件 {fileName}";
                });
                return;
            }

            var currentName = Path.GetFileName(filePath);
            var dir = Path.GetDirectoryName(filePath)!;
            string newName;

            if (enabled)
            {
                // 启用：移除 .disabled 后缀
                newName = currentName.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase)
                    ? currentName[..^FileExtensionConsts.Disabled.Length]
                    : currentName;
            }
            else
            {
                // 禁用：添加 .disabled 后缀
                newName = currentName.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase)
                    ? currentName
                    : currentName + FileExtensionConsts.Disabled;
            }

            if (newName != currentName)
            {
                var newPath = Path.Combine(dir, newName);
                File.Move(filePath, newPath);
                
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    AiAnalysisResult += $"\n\n成功: 已{(enabled ? "启用" : "禁用")} Mod {currentName} → {newName}";
                    HasFixAction = false; // 操作完成后隐藏按钮
                });
            }
            else
            {
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    AiAnalysisResult += $"\n\n提示: Mod {currentName} 已经是{(enabled ? "启用" : "禁用")}状态。";
                    HasFixAction = false;
                });
            }
        }
        catch (Exception ex)
        {
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                 AiAnalysisResult += $"\n\n操作失败: {ex.Message}";
            });
        }
    }

    private async Task<string> ExecuteToolSwitchJavaAsync()
    {
        // 设置修复按钮让用户点击执行
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            ResetFixActionState();
            _currentFixAction = new CrashFixAction
            {
                Type = "switchJavaForVersion",
                ButtonText = "自动切换 Java 版本",
                Parameters = new Dictionary<string, string>()
            };
            FixButtonText = _currentFixAction.ButtonText;
            HasFixAction = true;
        });

        return "已准备自动切换 Java 版本，等待用户点击按钮执行。";
    }
    
    /// <summary>
    /// 使用本地知识库进行错误分析（流式输出）
    /// </summary>
    [RelayCommand]
    private async Task AnalyzeWithAiAsync()
    {
        if (!_isGameCrashed || IsAiAnalyzing)
        {
            return;
        }

        var cancellationToken = BeginAiAnalysisToken();
        try
        {
            await _aiOrchestrator.AnalyzeCrashAsync(ExecuteToolCallAsync, cancellationToken);
        }
        finally
        {
            DisposeAiAnalysisToken();
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
            CancelActiveAiAnalysis();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("崩溃分析: 无法取消，当前没有正在进行的分析");
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
    /// 设置版本信息（用于导出日志时包含 version.json）
    /// </summary>
    /// <param name="versionId">版本 ID</param>
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

        CancelActiveAiAnalysis();
        
        // 重置崩溃分析结果
        IsAiAnalyzing = false;
        IsAiAnalysisAvailable = false;
        AiAnalysisResult = GetLocalizedString("ErrorAnalysis_NoErrorInfo.Text");
        ResetFixActionState();

        _logService.InitializeRealTimeLogs();
    }

    /// <summary>
    /// 重置智能修复按钮状态
    /// </summary>
    public void ResetFixActionState()
    {
        _sessionState.ResetFixActions();
    }
    
    /// <summary>
    /// 实时添加游戏输出日志
    /// </summary>
    /// <param name="logLine">日志行</param>
    public void AddGameOutputLog(string logLine)
    {
        _logService.AddGameOutputLog(logLine);
    }
    
    /// <summary>
    /// 实时添加游戏错误日志
    /// </summary>
    /// <param name="logLine">日志行</param>
    public void AddGameErrorLog(string logLine)
    {
        _logService.AddGameErrorLog(logLine);
        }

        // 导出错误日志
        [RelayCommand]
        private async Task ExportErrorLogsAsync()
        {
            await _exportService.ExportAsync();
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