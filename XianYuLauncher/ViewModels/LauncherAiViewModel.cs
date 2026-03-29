using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.Features.ErrorAnalysis.Services;

namespace XianYuLauncher.ViewModels;

public sealed partial class LauncherAiViewModel : ObservableObject, IDisposable
{
    private readonly IAiSettingsDomainService _aiSettingsDomainService;
    private readonly ILanguageSelectorService _languageSelectorService;
    private readonly ILauncherAiWorkspacePersistenceService _workspacePersistenceService;
    private readonly ErrorAnalysisSessionState _sessionState;
    private readonly LauncherAiWorkspaceState _workspaceState;
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private readonly SemaphoreSlim _persistenceSemaphore = new(1, 1);
    private readonly HashSet<UiChatMessage> _trackedSessionMessages = [];
    private CancellationTokenSource? _saveWorkspaceCts;
    private bool _isApplyingConversationSnapshot;
    private bool _isRestoringWorkspace;

    public LauncherAiViewModel(
        ErrorAnalysisViewModel chatViewModel,
        IAiSettingsDomainService aiSettingsDomainService,
        ILanguageSelectorService languageSelectorService,
        ILauncherAiWorkspacePersistenceService workspacePersistenceService,
        ErrorAnalysisSessionState sessionState,
        LauncherAiWorkspaceState workspaceState)
    {
        ChatViewModel = chatViewModel;
        _aiSettingsDomainService = aiSettingsDomainService;
        _languageSelectorService = languageSelectorService;
        _workspacePersistenceService = workspacePersistenceService;
        _sessionState = sessionState;
        _workspaceState = workspaceState;

        _workspaceState.PropertyChanged += WorkspaceState_PropertyChanged;
        _workspaceState.Conversations.CollectionChanged += Conversations_CollectionChanged;
        _sessionState.PropertyChanged += SessionState_PropertyChanged;
        AttachSessionMessageHandlers(_sessionState.ChatMessages);
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

    public async Task InitializeAsync(bool ensureDefaultConversation = true)
    {
        await _initializationSemaphore.WaitAsync();
        try
        {
            var state = await _aiSettingsDomainService.LoadAsync();

            _workspaceState.DefaultChatEnabled = state.IsEnabled;

            if (!_workspaceState.IsInitialized)
            {
                await RestoreWorkspaceAsync();

                if (_workspaceState.SelectedConversationId == null
                    && _workspaceState.Conversations.FirstOrDefault() is LauncherAiConversationTab conversation)
                {
                    _workspaceState.SelectedConversationId = conversation.Id;
                }

                _workspaceState.IsInitialized = true;
            }

            var createdDefaultConversation = false;
            if (ensureDefaultConversation && _workspaceState.Conversations.Count == 0)
            {
                var initialSnapshot = BuildInitialSnapshot(state.IsEnabled);
                var initialConversation = new LauncherAiConversationTab
                {
                    Id = Guid.NewGuid(),
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    LastUpdatedAtUtc = DateTimeOffset.UtcNow,
                    Snapshot = initialSnapshot,
                };
                ApplyConversationMetadata(initialConversation, initialSnapshot, _workspaceState.NextConversationNumber++);
                _workspaceState.Conversations.Add(initialConversation);
                _workspaceState.SelectedConversationId = initialConversation.Id;
                createdDefaultConversation = true;
            }

            EnsureActiveConversationLoaded();

            if (createdDefaultConversation)
            {
                QueueWorkspacePersistenceSave(0);
            }
        }
        finally
        {
            _initializationSemaphore.Release();
        }
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

        if (_workspaceState.SelectedConversationId == conversation.Id)
        {
            PersistActiveConversationSnapshot();
            return;
        }

        if (_workspaceState.SelectedConversationId != conversation.Id)
        {
            PersistActiveConversationSnapshot();
            _workspaceState.SelectedConversationId = conversation.Id;
        }

        EnsureActiveConversationLoaded();
    }

    public void ActivateConversationForEmbeddedSurface()
    {
        if (_sessionState.IsAiAnalyzing)
        {
            return;
        }

        PersistActiveConversationSnapshot();

        if (CanReuseSelectedConversationForEmbeddedSurface())
        {
            EnsureActiveConversationLoaded();
            return;
        }

        ActivateErrorAnalysisConversation(forceNewConversation: true);
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
        PersistActiveConversationSnapshot(schedulePersistenceSave: false);
        CancelPendingWorkspaceSave();
        _workspaceState.PropertyChanged -= WorkspaceState_PropertyChanged;
        _workspaceState.Conversations.CollectionChanged -= Conversations_CollectionChanged;
        _sessionState.PropertyChanged -= SessionState_PropertyChanged;
        DetachAllSessionMessageHandlers();
        _sessionState.ChatMessages.CollectionChanged -= SessionMessages_Changed;
        ChatViewModel.Dispose();
        _initializationSemaphore.Dispose();
        _persistenceSemaphore.Dispose();
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

    private void SessionMessages_Changed(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (UiChatMessage message in e.OldItems)
            {
                DetachSessionMessageHandler(message);
            }
        }

        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            DetachAllSessionMessageHandlers();
            AttachSessionMessageHandlers(_sessionState.ChatMessages);
        }
        else if (e.NewItems != null)
        {
            foreach (UiChatMessage message in e.NewItems)
            {
                AttachSessionMessageHandler(message);
            }
        }

        if (_isApplyingConversationSnapshot)
        {
            return;
        }

        PersistActiveConversationSnapshot();
    }

    private void SessionMessage_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isApplyingConversationSnapshot || sender is not UiChatMessage)
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

    private void PersistActiveConversationSnapshot(bool schedulePersistenceSave = true)
    {
        var activeConversation = SelectedConversation;
        if (activeConversation == null)
        {
            return;
        }

        var snapshot = _sessionState.CreateSnapshot();
        activeConversation.Snapshot = snapshot;
        activeConversation.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        ApplyConversationMetadata(activeConversation, snapshot, 0);

        if (schedulePersistenceSave)
        {
            QueueWorkspacePersistenceSave();
        }
    }

    private void AttachSessionMessageHandlers(IEnumerable<UiChatMessage> messages)
    {
        foreach (var message in messages)
        {
            AttachSessionMessageHandler(message);
        }
    }

    private void AttachSessionMessageHandler(UiChatMessage message)
    {
        if (!_trackedSessionMessages.Add(message))
        {
            return;
        }

        message.PropertyChanged += SessionMessage_PropertyChanged;
    }

    private void DetachSessionMessageHandler(UiChatMessage message)
    {
        if (!_trackedSessionMessages.Remove(message))
        {
            return;
        }

        message.PropertyChanged -= SessionMessage_PropertyChanged;
    }

    private void DetachAllSessionMessageHandlers()
    {
        foreach (var message in _trackedSessionMessages)
        {
            message.PropertyChanged -= SessionMessage_PropertyChanged;
        }

        _trackedSessionMessages.Clear();
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

        if (snapshot.ChatMessages.Any(message => message.IsUser && message.HasImageAttachments))
        {
            return _languageSelectorService.Language == "zh-CN" ? "图片对话" : "Image Chat";
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
        var now = DateTimeOffset.UtcNow;
        var conversation = new LauncherAiConversationTab
        {
            Id = Guid.NewGuid(),
            IsErrorAnalysisConversation = isErrorAnalysisConversation,
            CreatedAtUtc = now,
            LastUpdatedAtUtc = now,
            Snapshot = snapshot,
        };

        var fallbackNumber = isErrorAnalysisConversation ? 0 : _workspaceState.NextConversationNumber++;
        ApplyConversationMetadata(conversation, snapshot, fallbackNumber);
        _workspaceState.Conversations.Add(conversation);
        QueueWorkspacePersistenceSave(0);
        return conversation;
    }

    private void RemoveConversation(LauncherAiConversationTab conversation, bool ensureReplacement)
    {
        var removedConversationId = conversation.Id;
        var closingIndex = _workspaceState.Conversations.IndexOf(conversation);
        var wasSelected = _workspaceState.SelectedConversationId == conversation.Id;

        _workspaceState.Conversations.Remove(conversation);
        QueueConversationDeletion(removedConversationId);

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

        QueueWorkspacePersistenceSave(0);
    }

    private static bool HasUserMessages(ErrorAnalysisSessionSnapshot snapshot)
    {
        return snapshot.ChatMessages.Any(message => message.IsUser);
    }

    private bool CanReuseSelectedConversationForEmbeddedSurface()
    {
        var selectedConversation = SelectedConversation;
        return selectedConversation != null && HasUserMessages(selectedConversation.Snapshot);
    }

    private void QueueWorkspacePersistenceSave(int debounceMilliseconds = 150)
    {
        if (_isRestoringWorkspace || !_workspaceState.IsInitialized)
        {
            return;
        }

        CancelPendingWorkspaceSave();

        var cts = new CancellationTokenSource();
        _saveWorkspaceCts = cts;
        _ = PersistWorkspaceDebouncedAsync(cts, debounceMilliseconds);
    }

    private void CancelPendingWorkspaceSave()
    {
        if (_saveWorkspaceCts == null)
        {
            return;
        }

        _saveWorkspaceCts.Cancel();
        _saveWorkspaceCts.Dispose();
        _saveWorkspaceCts = null;
    }

    private async Task PersistWorkspaceDebouncedAsync(CancellationTokenSource cts, int debounceMilliseconds)
    {
        try
        {
            if (debounceMilliseconds > 0)
            {
                await Task.Delay(debounceMilliseconds, cts.Token);
            }

            await PersistWorkspaceToStorageAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LauncherAiPersistence] 保存工作区失败: {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_saveWorkspaceCts, cts))
            {
                _saveWorkspaceCts = null;
            }

            cts.Dispose();
        }
    }

    private async Task PersistWorkspaceToStorageAsync(CancellationToken cancellationToken)
    {
        if (_isRestoringWorkspace)
        {
            return;
        }

        await _persistenceSemaphore.WaitAsync(cancellationToken);
        try
        {
            PersistActiveConversationSnapshot(schedulePersistenceSave: false);

            var workspaceStorage = CreateWorkspaceStorageModel();
            var conversations = _workspaceState.Conversations.ToList();
            List<LauncherAiConversationStorageModel> conversationStorages = [];
            foreach (var conversation in conversations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                conversationStorages.Add(await CreateConversationStorageModelAsync(conversation, cancellationToken));
            }

            await _workspacePersistenceService.SaveWorkspaceAsync(workspaceStorage, cancellationToken);

            foreach (var conversationStorage in conversationStorages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _workspacePersistenceService.SaveConversationAsync(conversationStorage, cancellationToken);
            }
        }
        finally
        {
            _persistenceSemaphore.Release();
        }
    }

    private LauncherAiWorkspaceStorageModel CreateWorkspaceStorageModel()
    {
        return new LauncherAiWorkspaceStorageModel
        {
            SelectedConversationId = _workspaceState.SelectedConversationId,
            ActiveErrorAnalysisConversationId = _workspaceState.ActiveErrorAnalysisConversationId,
            NextConversationNumber = Math.Max(1, _workspaceState.NextConversationNumber),
            Conversations = _workspaceState.Conversations
                .Select(conversation => new LauncherAiConversationIndexEntryStorageModel
                {
                    ConversationId = conversation.Id,
                    IsErrorAnalysisConversation = conversation.IsErrorAnalysisConversation,
                    CreatedAtUtc = conversation.CreatedAtUtc,
                    LastUpdatedAtUtc = conversation.LastUpdatedAtUtc,
                })
                .ToList(),
        };
    }

    private async Task<LauncherAiConversationStorageModel> CreateConversationStorageModelAsync(
        LauncherAiConversationTab conversation,
        CancellationToken cancellationToken)
    {
        return new LauncherAiConversationStorageModel
        {
            ConversationId = conversation.Id,
            IsErrorAnalysisConversation = conversation.IsErrorAnalysisConversation,
            Title = conversation.Title,
            ToolTip = conversation.ToolTip,
            CreatedAtUtc = conversation.CreatedAtUtc,
            LastUpdatedAtUtc = conversation.LastUpdatedAtUtc,
            Interruption = CreateConversationInterruption(conversation),
            Session = await CreateSessionStorageModelAsync(conversation.Id, conversation.Snapshot, cancellationToken)
        };
    }

    private async Task<LauncherAiSessionStorageModel> CreateSessionStorageModelAsync(
        Guid conversationId,
        ErrorAnalysisSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        return new LauncherAiSessionStorageModel
        {
            ChatInput = snapshot.ChatInput,
            IsChatEnabled = snapshot.IsChatEnabled,
            PendingImageAttachments = await CreateAttachmentStorageModelsAsync(conversationId, snapshot.PendingImageAttachments, cancellationToken),
            ChatMessages = await CreateMessageStorageModelsAsync(conversationId, snapshot.ChatMessages, cancellationToken),
            ActionProposals = snapshot.ActionProposals.Select(CreateActionProposalStorageModel).ToList(),
        };
    }

    private async Task<List<LauncherAiChatMessageStorageModel>> CreateMessageStorageModelsAsync(
        Guid conversationId,
        IEnumerable<UiChatMessage> messages,
        CancellationToken cancellationToken)
    {
        List<LauncherAiChatMessageStorageModel> results = [];
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(new LauncherAiChatMessageStorageModel
            {
                Role = message.Role,
                Content = message.Content,
                IncludeInAiHistory = message.IncludeInAiHistory,
                ShowRoleHeader = message.ShowRoleHeader,
                DisplayRoleText = message.DisplayRoleText,
                AiHistoryContent = message.AiHistoryContent,
                ToolCallId = message.ToolCallId,
                ToolCalls = CloneToolCalls(message.ToolCalls),
                ImageAttachments = await CreateAttachmentStorageModelsAsync(conversationId, message.ImageAttachments, cancellationToken),
                AiHistoryImageAttachments = message.AiHistoryImageAttachments == null
                    ? null
                    : await CreateAttachmentStorageModelsAsync(conversationId, message.AiHistoryImageAttachments, cancellationToken),
                SuppressContentRendering = message.SuppressContentRendering,
            });
        }

        return results;
    }

    private async Task<List<LauncherAiAttachmentStorageModel>> CreateAttachmentStorageModelsAsync(
        Guid conversationId,
        IEnumerable<ChatImageAttachment> attachments,
        CancellationToken cancellationToken)
    {
        List<LauncherAiAttachmentStorageModel> results = [];
        foreach (var attachment in attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var storedAttachment = await _workspacePersistenceService.PersistAttachmentAsync(conversationId, attachment, cancellationToken);
            if (storedAttachment != null)
            {
                results.Add(storedAttachment);
            }
        }

        return results;
    }

    private async Task RestoreWorkspaceAsync()
    {
        _isRestoringWorkspace = true;
        try
        {
            _workspaceState.Conversations.Clear();
            _workspaceState.SelectedConversationId = null;
            _workspaceState.ActiveErrorAnalysisConversationId = null;
            _workspaceState.NextConversationNumber = 1;

            var workspaceStorage = await _workspacePersistenceService.LoadWorkspaceAsync();
            if (workspaceStorage == null)
            {
                return;
            }

            _workspaceState.NextConversationNumber = Math.Max(1, workspaceStorage.NextConversationNumber);

            foreach (var conversationIndex in workspaceStorage.Conversations)
            {
                var conversationStorage = await _workspacePersistenceService.LoadConversationAsync(conversationIndex.ConversationId);
                if (conversationStorage == null)
                {
                    continue;
                }

                var snapshot = CreateSnapshotFromStorage(conversationStorage);
                var conversation = new LauncherAiConversationTab
                {
                    Id = conversationStorage.ConversationId,
                    IsErrorAnalysisConversation = conversationStorage.IsErrorAnalysisConversation,
                    CreatedAtUtc = conversationStorage.CreatedAtUtc == default ? conversationIndex.CreatedAtUtc : conversationStorage.CreatedAtUtc,
                    LastUpdatedAtUtc = conversationStorage.LastUpdatedAtUtc == default ? conversationIndex.LastUpdatedAtUtc : conversationStorage.LastUpdatedAtUtc,
                    Interruption = null,
                    Snapshot = snapshot,
                    Title = conversationStorage.Title,
                    ToolTip = conversationStorage.ToolTip,
                };

                if (string.IsNullOrWhiteSpace(conversation.Title))
                {
                    ApplyConversationMetadata(conversation, snapshot, 0);
                }

                _workspaceState.Conversations.Add(conversation);
            }

            _workspaceState.SelectedConversationId = ResolveConversationId(workspaceStorage.SelectedConversationId);
            _workspaceState.ActiveErrorAnalysisConversationId = ResolveConversationId(workspaceStorage.ActiveErrorAnalysisConversationId);
        }
        finally
        {
            _isRestoringWorkspace = false;
        }
    }

    private Guid? ResolveConversationId(Guid? conversationId)
    {
        if (conversationId == null)
        {
            return null;
        }

        return _workspaceState.Conversations.Any(conversation => conversation.Id == conversationId.Value)
            ? conversationId
            : null;
    }

    private ErrorAnalysisSessionSnapshot CreateSnapshotFromStorage(LauncherAiConversationStorageModel conversationStorage)
    {
        var storage = conversationStorage.Session;
        var chatMessages = storage.ChatMessages.Select(CreateUiChatMessage).ToList();
        if (conversationStorage.Interruption != null)
        {
            chatMessages.Add(CreateInterruptedConversationMessage(conversationStorage.Interruption));
        }

        return new ErrorAnalysisSessionSnapshot
        {
            ChatInput = storage.ChatInput,
            PendingImageAttachments = RestoreAttachments(storage.PendingImageAttachments),
            IsChatEnabled = storage.IsChatEnabled,
            HasChatMessages = chatMessages.Count > 0,
            ChatMessages = chatMessages,
            ActionProposals = storage.ActionProposals.Select(CreateActionProposal).ToList(),
        };
    }

    private UiChatMessage CreateUiChatMessage(LauncherAiChatMessageStorageModel storage)
    {
        var message = new UiChatMessage(
            storage.Role,
            storage.Content,
            storage.IncludeInAiHistory,
            RestoreAttachments(storage.ImageAttachments))
        {
            ShowRoleHeader = storage.ShowRoleHeader,
            DisplayRoleText = storage.DisplayRoleText,
            AiHistoryContent = storage.AiHistoryContent,
            ToolCallId = storage.ToolCallId,
            ToolCalls = CloneToolCalls(storage.ToolCalls),
            AiHistoryImageAttachments = storage.AiHistoryImageAttachments == null
                ? null
                : RestoreAttachments(storage.AiHistoryImageAttachments),
            SuppressContentRendering = storage.SuppressContentRendering,
        };

        return message;
    }

    private List<ChatImageAttachment> RestoreAttachments(IEnumerable<LauncherAiAttachmentStorageModel> attachments)
    {
        List<ChatImageAttachment> results = [];
        foreach (var attachment in attachments)
        {
            var restored = _workspacePersistenceService.RestoreAttachment(attachment);
            if (restored != null)
            {
                results.Add(restored);
            }
        }

        return results;
    }

    private static LauncherAiActionProposalStorageModel CreateActionProposalStorageModel(AgentActionProposal proposal)
    {
        return new LauncherAiActionProposalStorageModel
        {
            ActionType = proposal.ActionType,
            ButtonText = proposal.ButtonText,
            DisplayMessage = proposal.DisplayMessage,
            PermissionLevel = proposal.PermissionLevel.ToString(),
            Parameters = new Dictionary<string, string>(proposal.Parameters, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static AgentActionProposal CreateActionProposal(LauncherAiActionProposalStorageModel storage)
    {
        return new AgentActionProposal
        {
            ActionType = storage.ActionType,
            ButtonText = storage.ButtonText,
            DisplayMessage = storage.DisplayMessage,
            PermissionLevel = ParsePermissionLevel(storage.PermissionLevel),
            Parameters = new Dictionary<string, string>(storage.Parameters, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static AgentToolPermissionLevel ParsePermissionLevel(string? permissionLevel)
    {
        return Enum.TryParse<AgentToolPermissionLevel>(permissionLevel, ignoreCase: true, out var parsed)
            ? parsed
            : AgentToolPermissionLevel.ConfirmationRequired;
    }

    private static List<ToolCallInfo>? CloneToolCalls(IEnumerable<ToolCallInfo>? toolCalls)
    {
        return toolCalls?.Select(toolCall => new ToolCallInfo
        {
            Id = toolCall.Id,
            FunctionName = toolCall.FunctionName,
            Arguments = toolCall.Arguments,
        }).ToList();
    }

    private LauncherAiConversationInterruptionStorageModel? CreateConversationInterruption(LauncherAiConversationTab conversation)
    {
        if (SelectedConversation?.Id != conversation.Id)
        {
            return conversation.Interruption;
        }

        if (_sessionState.IsAiAnalyzing)
        {
            return new LauncherAiConversationInterruptionStorageModel
            {
                Kind = "ai_analysis_in_progress",
                InterruptedAtUtc = DateTimeOffset.UtcNow,
                Message = _languageSelectorService.Language == "zh-CN"
                    ? "上次应用退出时，此对话中的 AI 处理已中断。聊天历史已恢复，但不会自动继续，请根据需要重新发起请求。"
                    : "The previous AI run in this conversation was interrupted when the app exited. History has been restored, but it will not resume automatically."
            };
        }

        if (_sessionState.HasPendingToolContinuation)
        {
            return new LauncherAiConversationInterruptionStorageModel
            {
                Kind = "tool_continuation_pending",
                InterruptedAtUtc = DateTimeOffset.UtcNow,
                Message = _languageSelectorService.Language == "zh-CN"
                    ? "上次应用退出时，此对话中的待继续工具流程已中断。聊天历史和待确认操作已恢复，但不会自动继续，请根据需要重新发起请求。"
                    : "The pending tool flow in this conversation was interrupted when the app exited. History and pending actions were restored, but the flow will not resume automatically."
            };
        }

        return conversation.Interruption;
    }

    private UiChatMessage CreateInterruptedConversationMessage(LauncherAiConversationInterruptionStorageModel interruption)
    {
        var content = string.IsNullOrWhiteSpace(interruption.Message)
            ? (_languageSelectorService.Language == "zh-CN"
                ? "上次应用退出时，此对话已中断。聊天历史已恢复，但不会自动继续。"
                : "This conversation was interrupted when the app exited. History has been restored, but it will not resume automatically.")
            : interruption.Message!;

        return new UiChatMessage("assistant", content, includeInAiHistory: false)
        {
            ShowRoleHeader = false,
            AiHistoryContent = null,
        };
    }

    private void QueueConversationDeletion(Guid conversationId)
    {
        _ = DeleteConversationFromPersistenceAsync(conversationId);
    }

    private async Task DeleteConversationFromPersistenceAsync(Guid conversationId)
    {
        try
        {
            await _persistenceSemaphore.WaitAsync();
            try
            {
                await _workspacePersistenceService.DeleteConversationAsync(conversationId);
            }
            finally
            {
                _persistenceSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LauncherAiPersistence] 删除会话失败: {conversationId}, {ex.Message}");
        }
    }
}