using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.Features.ErrorAnalysis.Services;

namespace XianYuLauncher.ViewModels;

public sealed partial class LauncherAiViewModel : ObservableObject, IDisposable
{
    private readonly IAiSettingsDomainService _aiSettingsDomainService;
    private readonly ILanguageSelectorService _languageSelectorService;
    private readonly ErrorAnalysisSessionState _sessionState;
    private readonly LauncherAiWorkspaceState _workspaceState;
    private bool _isApplyingConversationSnapshot;

    public LauncherAiViewModel(
        ErrorAnalysisViewModel chatViewModel,
        IAiSettingsDomainService aiSettingsDomainService,
        ILanguageSelectorService languageSelectorService,
        ErrorAnalysisSessionState sessionState,
        LauncherAiWorkspaceState workspaceState)
    {
        ChatViewModel = chatViewModel;
        _aiSettingsDomainService = aiSettingsDomainService;
        _languageSelectorService = languageSelectorService;
        _sessionState = sessionState;
        _workspaceState = workspaceState;

        _workspaceState.PropertyChanged += WorkspaceState_PropertyChanged;
        _workspaceState.Conversations.CollectionChanged += Conversations_CollectionChanged;
        _sessionState.PropertyChanged += SessionState_PropertyChanged;
        _sessionState.ChatMessages.CollectionChanged += SessionMessages_Changed;
    }

    public ErrorAnalysisViewModel ChatViewModel { get; }

    public ObservableCollection<LauncherAiConversationTab> Conversations => _workspaceState.Conversations;

    public LauncherAiConversationTab? SelectedConversation => _workspaceState.SelectedConversationId is Guid id
        ? _workspaceState.Conversations.FirstOrDefault(conversation => conversation.Id == id)
        : _workspaceState.Conversations.FirstOrDefault();

    public string EmptyStateText => _languageSelectorService.Language == "zh-CN"
        ? "还没有对话，先向 Launcher AI 提一个问题。"
        : "No conversation yet. Ask Launcher AI a question to get started.";

    public async Task InitializeAsync()
    {
        var state = await _aiSettingsDomainService.LoadAsync();

        if (!_workspaceState.IsInitialized)
        {
            _workspaceState.DefaultChatEnabled = state.IsEnabled;
            var initialSnapshot = BuildInitialSnapshot(state.IsEnabled);
            var initialConversation = new LauncherAiConversationTab
            {
                Id = Guid.NewGuid(),
                Snapshot = initialSnapshot,
            };
            ApplyConversationMetadata(initialConversation, initialSnapshot, _workspaceState.NextConversationNumber++);
            _workspaceState.Conversations.Add(initialConversation);
            _workspaceState.SelectedConversationId = initialConversation.Id;
            _workspaceState.IsInitialized = true;
        }
        else
        {
            _workspaceState.DefaultChatEnabled = state.IsEnabled;
        }

        EnsureActiveConversationLoaded();
    }

    public void SetErrorAnalysisPageOpen(bool isOpen)
    {
        _workspaceState.IsErrorAnalysisPageOpen = isOpen;

        if (!isOpen)
        {
            CleanupTransientErrorAnalysisConversation();
        }
    }

    public void ActivateErrorAnalysisConversation(bool forceNewConversation = false)
    {
        if (_sessionState.IsAiAnalyzing)
        {
            return;
        }

        var conversation = EnsureErrorAnalysisConversation(forceNewConversation);
        if (conversation == null)
        {
            return;
        }

        if (_workspaceState.SelectedConversationId != conversation.Id)
        {
            PersistActiveConversationSnapshot();
            _workspaceState.SelectedConversationId = conversation.Id;
        }

        EnsureActiveConversationLoaded();
    }

    public void CleanupTransientErrorAnalysisConversation()
    {
        if (_workspaceState.IsErrorAnalysisPageOpen || _sessionState.IsLauncherAiWindowOpen)
        {
            return;
        }

        if (_workspaceState.ActiveErrorAnalysisConversationId is not Guid conversationId)
        {
            return;
        }

        var conversation = _workspaceState.Conversations.FirstOrDefault(item => item.Id == conversationId);
        if (conversation == null)
        {
            _workspaceState.ActiveErrorAnalysisConversationId = null;
            return;
        }

        if (_workspaceState.SelectedConversationId == conversationId)
        {
            PersistActiveConversationSnapshot();
        }

        if (HasUserMessages(conversation.Snapshot))
        {
            return;
        }

        RemoveConversation(conversation, ensureReplacement: false);
        _workspaceState.ActiveErrorAnalysisConversationId = null;
        EnsureActiveConversationLoaded();
    }

    public void CreateConversation()
    {
        if (_sessionState.IsAiAnalyzing)
        {
            return;
        }

        PersistActiveConversationSnapshot();

        var conversation = CreateConversationCore(
            ErrorAnalysisSessionSnapshot.CreateEmpty(_workspaceState.DefaultChatEnabled),
            isErrorAnalysisConversation: false);
        _workspaceState.SelectedConversationId = conversation.Id;
        EnsureActiveConversationLoaded();
    }

    public void CloseConversation(Guid conversationId)
    {
        var conversation = _workspaceState.Conversations.FirstOrDefault(item => item.Id == conversationId);
        if (conversation == null || _sessionState.IsAiAnalyzing)
        {
            return;
        }

        var wasSelected = _workspaceState.SelectedConversationId == conversationId;
        if (wasSelected)
        {
            PersistActiveConversationSnapshot();
        }

        if (_workspaceState.ActiveErrorAnalysisConversationId == conversationId)
        {
            _workspaceState.ActiveErrorAnalysisConversationId = null;
        }

        RemoveConversation(conversation, ensureReplacement: true);

        EnsureActiveConversationLoaded();
    }

    public bool TrySelectConversation(Guid conversationId)
    {
        if (_workspaceState.SelectedConversationId == conversationId)
        {
            return true;
        }

        if (_sessionState.IsAiAnalyzing)
        {
            return false;
        }

        PersistActiveConversationSnapshot();
        _workspaceState.SelectedConversationId = conversationId;
        EnsureActiveConversationLoaded();
        return true;
    }

    public void Dispose()
    {
        PersistActiveConversationSnapshot();
        _workspaceState.PropertyChanged -= WorkspaceState_PropertyChanged;
        _workspaceState.Conversations.CollectionChanged -= Conversations_CollectionChanged;
        _sessionState.PropertyChanged -= SessionState_PropertyChanged;
        _sessionState.ChatMessages.CollectionChanged -= SessionMessages_Changed;
        ChatViewModel.Dispose();
    }

    private void WorkspaceState_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || e.PropertyName == nameof(LauncherAiWorkspaceState.SelectedConversationId))
        {
            OnPropertyChanged(nameof(SelectedConversation));
        }
    }

    private void Conversations_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Conversations));
        OnPropertyChanged(nameof(SelectedConversation));
    }

    private void SessionState_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isApplyingConversationSnapshot || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        if (e.PropertyName == nameof(ErrorAnalysisSessionState.ChatInput)
            || e.PropertyName == nameof(ErrorAnalysisSessionState.IsChatEnabled)
            || e.PropertyName == nameof(ErrorAnalysisSessionState.HasFixAction)
            || e.PropertyName == nameof(ErrorAnalysisSessionState.FixButtonText)
            || e.PropertyName == nameof(ErrorAnalysisSessionState.HasSecondaryFixAction)
            || e.PropertyName == nameof(ErrorAnalysisSessionState.SecondaryFixButtonText)
            || e.PropertyName == nameof(ErrorAnalysisSessionState.PendingToolContinuation)
            || e.PropertyName == nameof(ErrorAnalysisSessionState.HasPendingToolContinuation))
        {
            PersistActiveConversationSnapshot();
        }
    }

    private void SessionMessages_Changed(object? sender, EventArgs e)
    {
        if (_isApplyingConversationSnapshot)
        {
            return;
        }

        PersistActiveConversationSnapshot();
    }

    private ErrorAnalysisSessionSnapshot BuildInitialSnapshot(bool isChatEnabled)
    {
        if (_sessionState.ChatMessages.Count == 0
            && string.IsNullOrWhiteSpace(_sessionState.ChatInput)
            && !_sessionState.HasFixAction
            && !_sessionState.HasSecondaryFixAction
            && !_sessionState.HasPendingToolContinuation)
        {
            return ErrorAnalysisSessionSnapshot.CreateEmpty(_sessionState.IsChatEnabled || isChatEnabled);
        }

        return _sessionState.CreateSnapshot();
    }

    private LauncherAiConversationTab? EnsureErrorAnalysisConversation(bool forceNewConversation)
    {
        if (forceNewConversation)
        {
            ArchiveActiveErrorAnalysisConversation();
        }

        if (_workspaceState.ActiveErrorAnalysisConversationId is Guid activeConversationId)
        {
            var activeConversation = _workspaceState.Conversations.FirstOrDefault(item => item.Id == activeConversationId);
            if (activeConversation != null)
            {
                return activeConversation;
            }

            _workspaceState.ActiveErrorAnalysisConversationId = null;
        }

        var conversation = CreateConversationCore(
            ErrorAnalysisSessionSnapshot.CreateEmpty(_workspaceState.DefaultChatEnabled),
            isErrorAnalysisConversation: true);
        _workspaceState.ActiveErrorAnalysisConversationId = conversation.Id;
        return conversation;
    }

    private void ArchiveActiveErrorAnalysisConversation()
    {
        if (_workspaceState.ActiveErrorAnalysisConversationId is not Guid activeConversationId)
        {
            return;
        }

        var activeConversation = _workspaceState.Conversations.FirstOrDefault(item => item.Id == activeConversationId);
        if (activeConversation == null)
        {
            _workspaceState.ActiveErrorAnalysisConversationId = null;
            return;
        }

        if (_workspaceState.SelectedConversationId == activeConversationId)
        {
            PersistActiveConversationSnapshot();
        }

        if (!HasUserMessages(activeConversation.Snapshot))
        {
            RemoveConversation(activeConversation, ensureReplacement: false);
        }

        _workspaceState.ActiveErrorAnalysisConversationId = null;
    }

    private void EnsureActiveConversationLoaded()
    {
        var activeConversation = SelectedConversation;
        if (activeConversation == null)
        {
            return;
        }

        _isApplyingConversationSnapshot = true;
        try
        {
            _sessionState.ApplySnapshot(activeConversation.Snapshot);
        }
        finally
        {
            _isApplyingConversationSnapshot = false;
        }

        OnPropertyChanged(nameof(SelectedConversation));
    }

    private void PersistActiveConversationSnapshot()
    {
        var activeConversation = SelectedConversation;
        if (activeConversation == null)
        {
            return;
        }

        var snapshot = _sessionState.CreateSnapshot();
        activeConversation.Snapshot = snapshot;
        ApplyConversationMetadata(activeConversation, snapshot, 0);
    }

    private void ApplyConversationMetadata(LauncherAiConversationTab conversation, ErrorAnalysisSessionSnapshot snapshot, int fallbackNumber)
    {
        var title = BuildConversationTitle(conversation, snapshot, fallbackNumber);
        conversation.Title = title;
        conversation.ToolTip = title;
    }

    private string BuildConversationTitle(LauncherAiConversationTab conversation, ErrorAnalysisSessionSnapshot snapshot, int fallbackNumber)
    {
        var firstUserMessage = snapshot.ChatMessages.FirstOrDefault(message => message.IsUser)?.Content?.Trim();
        if (!string.IsNullOrWhiteSpace(firstUserMessage))
        {
            return Truncate(firstUserMessage, 18);
        }

        if (conversation.IsErrorAnalysisConversation)
        {
            return _languageSelectorService.Language == "zh-CN" ? "崩溃分析" : "Crash Analysis";
        }

        var prefix = _languageSelectorService.Language == "zh-CN" ? "新对话" : "New Chat";
        if (fallbackNumber > 0)
        {
            return $"{prefix} {fallbackNumber}";
        }

        return prefix;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(0, maxLength - 1)] + "…";
    }

    private LauncherAiConversationTab CreateConversationCore(ErrorAnalysisSessionSnapshot snapshot, bool isErrorAnalysisConversation)
    {
        var conversation = new LauncherAiConversationTab
        {
            Id = Guid.NewGuid(),
            IsErrorAnalysisConversation = isErrorAnalysisConversation,
            Snapshot = snapshot,
        };

        var fallbackNumber = isErrorAnalysisConversation ? 0 : _workspaceState.NextConversationNumber++;
        ApplyConversationMetadata(conversation, snapshot, fallbackNumber);
        _workspaceState.Conversations.Add(conversation);
        return conversation;
    }

    private void RemoveConversation(LauncherAiConversationTab conversation, bool ensureReplacement)
    {
        var closingIndex = _workspaceState.Conversations.IndexOf(conversation);
        var wasSelected = _workspaceState.SelectedConversationId == conversation.Id;

        _workspaceState.Conversations.Remove(conversation);

        if (_workspaceState.Conversations.Count == 0)
        {
            if (!ensureReplacement)
            {
                _workspaceState.SelectedConversationId = null;
                return;
            }

            var replacement = CreateConversationCore(
                ErrorAnalysisSessionSnapshot.CreateEmpty(_workspaceState.DefaultChatEnabled),
                isErrorAnalysisConversation: false);
            _workspaceState.SelectedConversationId = replacement.Id;
            return;
        }

        if (wasSelected)
        {
            var nextIndex = Math.Clamp(closingIndex - 1, 0, _workspaceState.Conversations.Count - 1);
            _workspaceState.SelectedConversationId = _workspaceState.Conversations[nextIndex].Id;
        }
    }

    private static bool HasUserMessages(ErrorAnalysisSessionSnapshot snapshot)
    {
        return snapshot.ChatMessages.Any(message => message.IsUser);
    }
}