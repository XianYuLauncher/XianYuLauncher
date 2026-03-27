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
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.Features.ErrorAnalysis.Services;

namespace XianYuLauncher.ViewModels
{
    public partial class ErrorAnalysisViewModel : ObservableObject, IDisposable
    {
        private readonly ILanguageSelectorService _languageSelectorService;
        private readonly IErrorAnalysisLogService _logService;
        private readonly IErrorAnalysisAiOrchestrator _aiOrchestrator;
        private readonly IErrorAnalysisSessionCoordinator _sessionCoordinator;
        private readonly IErrorAnalysisExportService _exportService;
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
        /// 独立 Launcher AI 窗口是否打开。
        /// </summary>
        public bool IsLauncherAiWindowOpen
        {
            get => _sessionState.IsLauncherAiWindowOpen;
            set => _sessionState.IsLauncherAiWindowOpen = value;
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
            IErrorAnalysisSessionCoordinator sessionCoordinator,
            IErrorAnalysisExportService exportService,
            IUiDispatcher uiDispatcher,
            ErrorAnalysisSessionState sessionState)
        {
            _languageSelectorService = languageSelectorService;
            _logService = logService;
            _aiOrchestrator = aiOrchestrator;
            _sessionCoordinator = sessionCoordinator;
            _exportService = exportService;
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

                if (e.PropertyName == nameof(ErrorAnalysisSessionState.IsAiAnalyzing) ||
                    e.PropertyName == nameof(ErrorAnalysisSessionState.IsAiAnalysisAvailable))
                {
                    OnPropertyChanged(nameof(IsAnalyzeButtonEnabled));
                    OnPropertyChanged(nameof(AnalyzeButtonVisibility));
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
                _sessionState.ClearPendingToolContinuation();
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

        private AgentActionProposal? _currentFixAction
        {
            get => _sessionState.CurrentFixAction;
            set => _sessionState.CurrentFixAction = value;
        }

        private AgentActionProposal? _secondaryFixAction
        {
            get => _sessionState.SecondaryFixAction;
            set => _sessionState.SecondaryFixAction = value;
        }

        [RelayCommand]
        private async Task FixError()
        {
            if (_currentFixAction == null)
            {
                return;
            }

            await ApproveActionProposalAsync(_currentFixAction);
        }

        [RelayCommand]
        private async Task FixErrorSecondary()
        {
            if (_secondaryFixAction == null)
            {
                return;
            }

            await ApproveActionProposalAsync(_secondaryFixAction);
        }

        [RelayCommand]
        private async Task RejectFixAction()
        {
            var rejectedText = FixButtonText;
            var cancellationToken = BeginAiAnalysisToken();
            try
            {
                await _aiOrchestrator.RejectPendingActionAsync(rejectedText, cancellationToken);
            }
            finally
            {
                DisposeAiAnalysisToken();
            }
        }

        private async Task ApproveActionProposalAsync(AgentActionProposal proposal)
        {
            var cancellationToken = BeginAiAnalysisToken();
            try
            {
                await _aiOrchestrator.ApproveActionAsync(proposal, cancellationToken);
            }
            finally
            {
                DisposeAiAnalysisToken();
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

        public bool IsAiAnalysisAvailable
        {
            get => _sessionState.IsAiAnalysisAvailable;
            set => _sessionState.IsAiAnalysisAvailable = value;
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
        private System.Threading.CancellationToken BeginAiAnalysisToken()
        {
            return _sessionState.BeginAiAnalysisToken();
        }

        private void CancelActiveAiAnalysis()
        {
            _sessionState.CancelAiAnalysis();
        }

        private void DisposeAiAnalysisToken()
        {
            _sessionState.DisposeAiAnalysisToken();
        }

        // 设置日志数据
    public void SetLogData(string launchCommand, List<string> gameOutput, List<string> gameError)
    {
        System.Diagnostics.Debug.WriteLine($"崩溃分析: 设置日志数据，输出日志行数: {gameOutput.Count}，错误日志行数: {gameError.Count}");
        _sessionCoordinator.SetLogData(launchCommand, gameOutput, gameError);
    }
    
    /// <summary>
    /// 设置游戏崩溃状态，只有在游戏崩溃时才会触发 AI 分析
    /// </summary>
    /// <param name="isCrashed">是否崩溃</param>
    public void SetGameCrashStatus(bool isCrashed)
    {
        System.Diagnostics.Debug.WriteLine($"崩溃分析: 设置游戏崩溃状态: {isCrashed}");
        _sessionCoordinator.SetGameCrashStatus(isCrashed);
        
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
            await _aiOrchestrator.SendMessageAsync(userMsg, cancellationToken);
        }
        finally
        {
            DisposeAiAnalysisToken();
        }
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
            await _aiOrchestrator.AnalyzeCrashAsync(cancellationToken);
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
        if (IsAiAnalyzing)
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
        _sessionCoordinator.SetLaunchCommand(launchCommand);
        System.Diagnostics.Debug.WriteLine($"ErrorAnalysisViewModel: 设置启动命令，长度: {launchCommand?.Length ?? 0}");
    }
    
    /// <summary>
    /// 设置版本信息（用于导出日志时包含 version.json）
    /// </summary>
    /// <param name="versionId">版本 ID</param>
    /// <param name="minecraftPath">Minecraft 路径</param>
    public void SetVersionInfo(string versionId, string minecraftPath)
    {
        _sessionCoordinator.SetVersionInfo(versionId, minecraftPath);
        System.Diagnostics.Debug.WriteLine($"ErrorAnalysisViewModel: 设置版本信息，版本ID: {versionId}");
    }
    
    /// <summary>
    /// 仅清空日志数据，保留启动命令（用于实时日志模式）
    /// </summary>
    public void ClearLogsOnly()
    {
        System.Diagnostics.Debug.WriteLine("ErrorAnalysisViewModel: 仅清空日志数据，保留启动命令");
        _sessionCoordinator.ClearLogsOnly();
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
        _sessionCoordinator.AddGameOutputLog(logLine);
    }
    
    /// <summary>
    /// 实时添加游戏错误日志
    /// </summary>
    /// <param name="logLine">日志行</param>
    public void AddGameErrorLog(string logLine)
    {
        _sessionCoordinator.AddGameErrorLog(logLine);
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

        public void Dispose()
        {
            _sessionState.PropertyChanged -= SessionState_PropertyChanged;
        }
    }
}