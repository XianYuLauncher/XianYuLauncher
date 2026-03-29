using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.System;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.Features.ErrorAnalysis.Services;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.ViewModels
{
    public partial class ErrorAnalysisViewModel : ObservableObject, IDisposable
    {
        private readonly ILanguageSelectorService _languageSelectorService;
        private readonly IErrorAnalysisLogService _logService;
        private readonly IErrorAnalysisAiOrchestrator _aiOrchestrator;
        private readonly IErrorAnalysisSessionCoordinator _sessionCoordinator;
        private readonly IErrorAnalysisExportService _exportService;
        private readonly IFilePickerService _filePickerService;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly ErrorAnalysisSessionState _sessionState;

        private static readonly IReadOnlyDictionary<string, string> SupportedImageContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".gif"] = "image/gif",
            [".bmp"] = "image/bmp"
        };

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
            IFilePickerService filePickerService,
            IUiDispatcher uiDispatcher,
            ErrorAnalysisSessionState sessionState)
        {
            _languageSelectorService = languageSelectorService;
            _logService = logService;
            _aiOrchestrator = aiOrchestrator;
            _sessionCoordinator = sessionCoordinator;
            _exportService = exportService;
            _filePickerService = filePickerService;
            _uiDispatcher = uiDispatcher;
            _sessionState = sessionState;

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
                    e.PropertyName == nameof(ErrorAnalysisSessionState.IsAiAnalysisAvailable) ||
                    e.PropertyName == nameof(ErrorAnalysisSessionState.IsChatEnabled))
                {
                    OnPropertyChanged(nameof(IsAnalyzeButtonEnabled));
                    OnPropertyChanged(nameof(AnalyzeButtonVisibility));
                    OnPropertyChanged(nameof(CancelButtonVisibility));
                    OnPropertyChanged(nameof(CanComposeChat));
                    OnPropertyChanged(nameof(CurrentChatActionCommand));
                    OnPropertyChanged(nameof(CurrentChatActionGlyph));
                }

                if (e.PropertyName == nameof(ErrorAnalysisSessionState.HasPendingImageAttachments))
                {
                    OnPropertyChanged(nameof(HasPendingChatAttachments));
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

        public string NoErrorInfoText => "ErrorAnalysis_NoErrorInfoText".GetLocalized();

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
                PendingImageAttachments.Clear();
                IsChatEnabled = false;
                ResetFixActionState();
                _sessionState.ClearPendingToolContinuation();
            });

            IsAiAnalyzing = false;
            _sessionState.DisposeAiAnalysisToken();
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

        public ObservableCollection<ChatImageAttachment> PendingImageAttachments => _sessionState.PendingImageAttachments;

        public bool HasPendingChatAttachments => _sessionState.HasPendingImageAttachments;

        public bool IsChatEnabled
        {
            get => _sessionState.IsChatEnabled;
            set => _sessionState.IsChatEnabled = value;
        }

        public bool CanComposeChat => IsChatEnabled || IsAiAnalyzing;

        public ICommand CurrentChatActionCommand => IsAiAnalyzing
            ? CancelAiAnalysisCommand
            : SendMessageCommand;

        public string CurrentChatActionGlyph => IsAiAnalyzing ? "\uE71A" : "\uE724";

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
            var tokenSource = BeginAiAnalysisTokenSource();
            try
            {
                await _aiOrchestrator.RejectPendingActionAsync(rejectedText, tokenSource.Token);
            }
            finally
            {
                CompleteAiAnalysisToken(tokenSource);
            }
        }

        private async Task ApproveActionProposalAsync(AgentActionProposal proposal)
        {
            var tokenSource = BeginAiAnalysisTokenSource();
            try
            {
                await _aiOrchestrator.ApproveActionAsync(proposal, tokenSource.Token);
            }
            finally
            {
                CompleteAiAnalysisToken(tokenSource);
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
        private CancellationTokenSource BeginAiAnalysisTokenSource()
        {
            IsAiAnalyzing = true;
            return _sessionState.BeginAiAnalysisTokenSource();
        }

        private void CancelActiveAiAnalysis()
        {
            _sessionState.CancelAiAnalysis();
        }

        private void CompleteAiAnalysisToken(CancellationTokenSource tokenSource)
        {
            if (_sessionState.IsCurrentAiAnalysisTokenSource(tokenSource))
            {
                IsAiAnalyzing = false;
            }

            _sessionState.CompleteAiAnalysisTokenSource(tokenSource);
        }

        private void RemoveTrailingAssistantPlaceholderIfNeeded()
        {
            if (ChatMessages.LastOrDefault() is not UiChatMessage lastAssistant
                || !lastAssistant.IsAssistant)
            {
                return;
            }

            if (lastAssistant.Content == "...")
            {
                ChatMessages.Remove(lastAssistant);
            }
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
            _ = StartCrashAnalysisAsync(preemptCurrentAnalysis: true);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("AI分析: 游戏正常退出，不触发AI分析");
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(ChatInput) && PendingImageAttachments.Count == 0) return;

        if (IsAiAnalyzing)
        {
            _sessionState.SuppressNextCancellationMessage();
            RemoveTrailingAssistantPlaceholderIfNeeded();
        }

        var userMsg = ChatInput.Trim();
        var imageAttachments = PendingImageAttachments
            .Select(CloneAttachment)
            .ToList();

        ChatInput = string.Empty;
        PendingImageAttachments.Clear();

        var tokenSource = BeginAiAnalysisTokenSource();
        try
        {
            await _aiOrchestrator.SendMessageAsync(userMsg, imageAttachments, tokenSource.Token);
        }
        finally
        {
            CompleteAiAnalysisToken(tokenSource);
        }
    }

    [RelayCommand]
    private async Task PickChatImages()
    {
        var selectedPaths = await _filePickerService.PickMultipleFilePathsAsync(
            [".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp"],
            PickerLocationId.PicturesLibrary,
            PickerViewMode.Thumbnail,
            settingsIdentifier: "LauncherAiChatImages",
            commitButtonText: _languageSelectorService.Language == "zh-CN" ? "选择图片" : "Select images");

        if (selectedPaths.Count == 0)
        {
            return;
        }

        var existingPaths = new HashSet<string>(
            PendingImageAttachments.Select(attachment => attachment.FilePath),
            StringComparer.OrdinalIgnoreCase);

        foreach (var path in selectedPaths)
        {
            if (existingPaths.Contains(path))
            {
                continue;
            }

            var attachment = await CreateChatImageAttachmentAsync(path);
            if (attachment == null)
            {
                continue;
            }

            PendingImageAttachments.Add(attachment);
            existingPaths.Add(path);
        }
    }

    [RelayCommand]
    private void RemovePendingImageAttachment(ChatImageAttachment? attachment)
    {
        if (attachment == null)
        {
            return;
        }

        _ = PendingImageAttachments.Remove(attachment);
    }

    [RelayCommand]
    private async Task OpenChatAttachment(ChatImageAttachment? attachment)
    {
        if (attachment == null || string.IsNullOrWhiteSpace(attachment.FilePath) || !File.Exists(attachment.FilePath))
        {
            return;
        }

        StorageFile file = await StorageFile.GetFileFromPathAsync(attachment.FilePath);
        _ = await Launcher.LaunchFileAsync(file);
    }

    /// <summary>
    /// 使用本地知识库进行错误分析（流式输出）
    /// </summary>
    [RelayCommand]
    private async Task AnalyzeWithAiAsync()
    {
        await StartCrashAnalysisAsync(preemptCurrentAnalysis: false);
    }

    private async Task StartCrashAnalysisAsync(bool preemptCurrentAnalysis)
    {
        if (!_isGameCrashed)
        {
            return;
        }

        if (IsAiAnalyzing && !preemptCurrentAnalysis)
        {
            return;
        }

        if (IsAiAnalyzing)
        {
            _sessionState.SuppressNextCancellationMessage();
        }

        var tokenSource = BeginAiAnalysisTokenSource();
        try
        {
            await _aiOrchestrator.AnalyzeCrashAsync(tokenSource.Token);
        }
        finally
        {
            CompleteAiAnalysisToken(tokenSource);
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
            AiAnalysisResult = NoErrorInfoText;
            IsAiAnalysisAvailable = false;
        }

        public void Dispose()
        {
            _sessionState.PropertyChanged -= SessionState_PropertyChanged;
        }

        private static ChatImageAttachment CloneAttachment(ChatImageAttachment attachment)
        {
            return new ChatImageAttachment
            {
                FileName = attachment.FileName,
                FilePath = attachment.FilePath,
                ContentType = attachment.ContentType,
                DataUrl = attachment.DataUrl
            };
        }

        private static async Task<ChatImageAttachment?> CreateChatImageAttachmentAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            var extension = Path.GetExtension(filePath);
            if (!SupportedImageContentTypes.TryGetValue(extension, out var contentType))
            {
                return null;
            }

            byte[] bytes = await File.ReadAllBytesAsync(filePath);
            return new ChatImageAttachment
            {
                FileName = Path.GetFileName(filePath),
                FilePath = filePath,
                ContentType = contentType,
                DataUrl = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}"
            };
        }
    }
}