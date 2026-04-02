using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.Features.ErrorAnalysis.Services;

namespace XianYuLauncher.ViewModels;

public sealed partial class LauncherAIViewModel : ObservableObject, IDisposable
{
    private readonly IAISettingsDomainService _aiSettingsDomainService;
    private readonly ILanguageSelectorService _languageSelectorService;
    private readonly ILauncherAIWorkspacePersistenceService _workspacePersistenceService;
    private readonly ErrorAnalysisSessionState _sessionState;
    private readonly LauncherAIWorkspaceState _workspaceState;
    private readonly CancellationTokenSource _lifecycleCts = new();
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private readonly SemaphoreSlim _persistenceSemaphore = new(1, 1);
    private readonly Lock _dirtyStateLock = new();
    private readonly HashSet<UiChatMessage> _trackedSessionMessages = [];
    private readonly Dictionary<Guid, long> _dirtyConversationStamps = [];
    private bool _isApplyingConversationSnapshot;
    private bool _isRestoringWorkspace;
    private Guid? _loadedConversationId;
    private long _dirtyWorkspaceStamp;
    private long _nextDirtyStamp;
    private long _workspaceSaveRequestVersion;

    public LauncherAIViewModel(
        ErrorAnalysisViewModel chatViewModel,
        IAISettingsDomainService aiSettingsDomainService,
        ILanguageSelectorService languageSelectorService,
        ILauncherAIWorkspacePersistenceService workspacePersistenceService,
        ErrorAnalysisSessionState sessionState,
        LauncherAIWorkspaceState workspaceState)
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

    public ObservableCollection<LauncherAIConversationTab> Conversations => _workspaceState.Conversations;

    public LauncherAIConversationTab? SelectedConversation => _workspaceState.SelectedConversationId is Guid id
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
                    && _workspaceState.Conversations.FirstOrDefault() is LauncherAIConversationTab conversation)
                {
                    _workspaceState.SelectedConversationId = conversation.Id;
                }

                _workspaceState.IsInitialized = true;
            }

            var createdDefaultConversation = false;
            if (ensureDefaultConversation && _workspaceState.Conversations.Count == 0)
            {
                var initialSnapshot = BuildInitialSnapshot(state.IsEnabled);
                var initialConversation = new LauncherAIConversationTab
                {
                    Id = Guid.NewGuid(),
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    LastUpdatedAtUtc = DateTimeOffset.UtcNow,
                    Snapshot = initialSnapshot,
                };
                ApplyConversationMetadata(initialConversation, initialSnapshot, _workspaceState.NextConversationNumber++);
                _workspaceState.Conversations.Add(initialConversation);
                _workspaceState.SelectedConversationId = initialConversation.Id;
                MarkConversationDirty(initialConversation.Id);
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
        if (_sessionState.IsAIAnalyzing)
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
        if (_sessionState.IsAIAnalyzing)
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
        if (_workspaceState.IsErrorAnalysisPageOpen || _sessionState.IsLauncherAIWindowOpen)
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
        if (_sessionState.IsAIAnalyzing)
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
        if (conversation == null || _sessionState.IsAIAnalyzing)
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

        if (_sessionState.IsAIAnalyzing)
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
        if (_lifecycleCts.IsCancellationRequested)
        {
            return;
        }

        PersistActiveConversationSnapshot(schedulePersistenceSave: false);
        CancelPendingWorkspaceSave();
        _lifecycleCts.Cancel();
        _workspaceState.PropertyChanged -= WorkspaceState_PropertyChanged;
        _workspaceState.Conversations.CollectionChanged -= Conversations_CollectionChanged;
        _sessionState.PropertyChanged -= SessionState_PropertyChanged;
        DetachAllSessionMessageHandlers();
        _sessionState.ChatMessages.CollectionChanged -= SessionMessages_Changed;
        ChatViewModel.Dispose();
    }

    private void WorkspaceState_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || e.PropertyName == nameof(LauncherAIWorkspaceState.SelectedConversationId))
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

    private LauncherAIConversationTab? EnsureErrorAnalysisConversation(bool forceNewConversation)
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
            _loadedConversationId = null;
            return;
        }

        _isApplyingConversationSnapshot = true;
        try
        {
            _sessionState.ApplySnapshot(activeConversation.Snapshot);
            _loadedConversationId = activeConversation.Id;
        }
        finally
        {
            _isApplyingConversationSnapshot = false;
        }

        OnPropertyChanged(nameof(SelectedConversation));
    }

    private void PersistActiveConversationSnapshot(bool schedulePersistenceSave = true)
    {
        var activeConversation = GetLoadedConversation();
        if (activeConversation == null)
        {
            return;
        }

        var snapshot = _sessionState.CreateSnapshot();
        activeConversation.Snapshot = snapshot;
        activeConversation.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        ApplyConversationMetadata(activeConversation, snapshot, 0);
        MarkConversationDirty(activeConversation.Id);

        if (schedulePersistenceSave)
        {
            QueueWorkspacePersistenceSave();
        }
    }

    private LauncherAIConversationTab? GetLoadedConversation()
    {
        if (_loadedConversationId is Guid loadedConversationId)
        {
            return _workspaceState.Conversations.FirstOrDefault(conversation => conversation.Id == loadedConversationId);
        }

        return null;
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

    private void ApplyConversationMetadata(LauncherAIConversationTab conversation, ErrorAnalysisSessionSnapshot snapshot, int fallbackNumber)
    {
        var title = BuildConversationTitle(conversation, snapshot, fallbackNumber);
        conversation.Title = title;
        conversation.ToolTip = title;
    }

    private string BuildConversationTitle(LauncherAIConversationTab conversation, ErrorAnalysisSessionSnapshot snapshot, int fallbackNumber)
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

    private LauncherAIConversationTab CreateConversationCore(ErrorAnalysisSessionSnapshot snapshot, bool isErrorAnalysisConversation)
    {
        var now = DateTimeOffset.UtcNow;
        var conversation = new LauncherAIConversationTab
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
        MarkConversationDirty(conversation.Id);
        QueueWorkspacePersistenceSave(0);
        return conversation;
    }

    private void RemoveConversation(LauncherAIConversationTab conversation, bool ensureReplacement)
    {
        var removedConversationId = conversation.Id;
        var closingIndex = _workspaceState.Conversations.IndexOf(conversation);
        var wasSelected = _workspaceState.SelectedConversationId == conversation.Id;

        _workspaceState.Conversations.Remove(conversation);
        if (_loadedConversationId == removedConversationId)
        {
            _loadedConversationId = null;
        }

        ForgetDirtyConversation(removedConversationId);
        MarkWorkspaceDirty();
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
        if (_isRestoringWorkspace || !_workspaceState.IsInitialized || _lifecycleCts.IsCancellationRequested)
        {
            return;
        }

        var requestVersion = Interlocked.Increment(ref _workspaceSaveRequestVersion);
        _ = PersistWorkspaceDebouncedAsync(requestVersion, debounceMilliseconds);
    }

    private void CancelPendingWorkspaceSave()
    {
        Interlocked.Increment(ref _workspaceSaveRequestVersion);
    }

    private async Task PersistWorkspaceDebouncedAsync(long requestVersion, int debounceMilliseconds)
    {
        try
        {
            if (debounceMilliseconds > 0)
            {
                await Task.Delay(debounceMilliseconds);
            }

            if (_lifecycleCts.IsCancellationRequested
                || requestVersion != Volatile.Read(ref _workspaceSaveRequestVersion))
            {
                return;
            }

            await PersistWorkspaceToStorageAsync(_lifecycleCts.Token);
        }
        catch (OperationCanceledException) when (_lifecycleCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LauncherAIPersistence] 保存工作区失败: {ex.Message}");
        }
    }

    private async Task PersistWorkspaceToStorageAsync(CancellationToken cancellationToken)
    {
        if (_isRestoringWorkspace || _lifecycleCts.IsCancellationRequested)
        {
            return;
        }

        await _persistenceSemaphore.WaitAsync(cancellationToken);
        try
        {
            PersistActiveConversationSnapshot(schedulePersistenceSave: false);

            var (workspaceStamp, dirtyConversationStamps) = CaptureDirtyState();
            if (workspaceStamp == 0 && dirtyConversationStamps.Count == 0)
            {
                return;
            }

            var workspaceStorage = CreateWorkspaceStorageModel();
            var conversations = _workspaceState.Conversations.ToList();
            var conversationsById = conversations.ToDictionary(conversation => conversation.Id);
            List<LauncherAIConversationStorageModel> conversationStorages = [];
            Dictionary<Guid, long> savedConversationStamps = [];
            foreach (var dirtyConversation in dirtyConversationStamps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!conversationsById.TryGetValue(dirtyConversation.Key, out var conversation))
                {
                    continue;
                }

                conversationStorages.Add(await CreateConversationStorageModelAsync(conversation, cancellationToken));
                savedConversationStamps[dirtyConversation.Key] = dirtyConversation.Value;
            }

            foreach (var conversationStorage in conversationStorages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _workspacePersistenceService.SaveConversationAsync(conversationStorage, cancellationToken);
            }

            await _workspacePersistenceService.SaveWorkspaceAsync(workspaceStorage, cancellationToken);
            AcknowledgeSavedDirtyState(workspaceStamp, savedConversationStamps);
        }
        finally
        {
            _persistenceSemaphore.Release();
        }
    }

    private void MarkConversationDirty(Guid conversationId)
    {
        long stamp = Interlocked.Increment(ref _nextDirtyStamp);
        lock (_dirtyStateLock)
        {
            _dirtyConversationStamps[conversationId] = stamp;
            _dirtyWorkspaceStamp = stamp;
        }
    }

    private void MarkWorkspaceDirty()
    {
        long stamp = Interlocked.Increment(ref _nextDirtyStamp);
        lock (_dirtyStateLock)
        {
            _dirtyWorkspaceStamp = stamp;
        }
    }

    private (long WorkspaceStamp, Dictionary<Guid, long> ConversationStamps) CaptureDirtyState()
    {
        lock (_dirtyStateLock)
        {
            return (_dirtyWorkspaceStamp, new Dictionary<Guid, long>(_dirtyConversationStamps));
        }
    }

    private void AcknowledgeSavedDirtyState(long workspaceStamp, IReadOnlyDictionary<Guid, long> savedConversationStamps)
    {
        lock (_dirtyStateLock)
        {
            if (_dirtyWorkspaceStamp == workspaceStamp)
            {
                _dirtyWorkspaceStamp = 0;
            }

            foreach (var savedConversation in savedConversationStamps)
            {
                if (_dirtyConversationStamps.TryGetValue(savedConversation.Key, out long currentStamp)
                    && currentStamp == savedConversation.Value)
                {
                    _dirtyConversationStamps.Remove(savedConversation.Key);
                }
            }
        }
    }

    private void ForgetDirtyConversation(Guid conversationId)
    {
        lock (_dirtyStateLock)
        {
            _dirtyConversationStamps.Remove(conversationId);
        }
    }

    private LauncherAIWorkspaceStorageModel CreateWorkspaceStorageModel()
    {
        return new LauncherAIWorkspaceStorageModel
        {
            SelectedConversationId = _workspaceState.SelectedConversationId,
            ActiveErrorAnalysisConversationId = _workspaceState.ActiveErrorAnalysisConversationId,
            NextConversationNumber = Math.Max(1, _workspaceState.NextConversationNumber),
            Conversations = _workspaceState.Conversations
                .Select(conversation => new LauncherAIConversationIndexEntryStorageModel
                {
                    ConversationId = conversation.Id,
                    IsErrorAnalysisConversation = conversation.IsErrorAnalysisConversation,
                    CreatedAtUtc = conversation.CreatedAtUtc,
                    LastUpdatedAtUtc = conversation.LastUpdatedAtUtc,
                })
                .ToList(),
        };
    }

    private async Task<LauncherAIConversationStorageModel> CreateConversationStorageModelAsync(
        LauncherAIConversationTab conversation,
        CancellationToken cancellationToken)
    {
        return new LauncherAIConversationStorageModel
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

    private async Task<LauncherAISessionStorageModel> CreateSessionStorageModelAsync(
        Guid conversationId,
        ErrorAnalysisSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        return new LauncherAISessionStorageModel
        {
            ChatInput = snapshot.ChatInput,
            IsChatEnabled = snapshot.IsChatEnabled,
            PendingImageAttachments = await CreateAttachmentStorageModelsAsync(conversationId, snapshot.PendingImageAttachments, cancellationToken),
            ChatMessages = await CreateMessageStorageModelsAsync(conversationId, snapshot.ChatMessages, cancellationToken),
            ActionProposals = snapshot.ActionProposals.Select(CreateActionProposalStorageModel).ToList(),
        };
    }

    private async Task<List<LauncherAIChatMessageStorageModel>> CreateMessageStorageModelsAsync(
        Guid conversationId,
        IEnumerable<UiChatMessage> messages,
        CancellationToken cancellationToken)
    {
        List<LauncherAIChatMessageStorageModel> results = [];
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(new LauncherAIChatMessageStorageModel
            {
                Role = message.Role,
                Content = message.Content,
                IncludeInAIHistory = message.IncludeInAIHistory,
                ShowRoleHeader = message.ShowRoleHeader,
                DisplayRoleText = message.DisplayRoleText,
                AIHistoryContent = message.AIHistoryContent,
                ToolCallId = message.ToolCallId,
                ToolCalls = CloneToolCalls(message.ToolCalls),
                ToolInputContent = message.ToolInputContent,
                ToolOutputContent = message.ToolOutputContent,
                ImageAttachments = await CreateAttachmentStorageModelsAsync(conversationId, message.ImageAttachments, cancellationToken),
                AIHistoryImageAttachments = message.AIHistoryImageAttachments == null
                    ? null
                    : await CreateAttachmentStorageModelsAsync(conversationId, message.AIHistoryImageAttachments, cancellationToken),
                SuppressContentRendering = message.SuppressContentRendering,
            });
        }

        return results;
    }

    private async Task<List<LauncherAIAttachmentStorageModel>> CreateAttachmentStorageModelsAsync(
        Guid conversationId,
        IEnumerable<ChatImageAttachment> attachments,
        CancellationToken cancellationToken)
    {
        List<LauncherAIAttachmentStorageModel> results = [];
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

            foreach (var conversationIndex in workspaceStorage.Conversations ?? [])
            {
                try
                {
                    var conversationStorage = await _workspacePersistenceService.LoadConversationAsync(conversationIndex.ConversationId);
                    if (conversationStorage == null)
                    {
                        continue;
                    }

                    var snapshot = CreateSnapshotFromStorage(conversationStorage);
                    var conversation = new LauncherAIConversationTab
                    {
                        Id = conversationStorage.ConversationId,
                        IsErrorAnalysisConversation = conversationStorage.IsErrorAnalysisConversation,
                        CreatedAtUtc = conversationStorage.CreatedAtUtc == default ? conversationIndex.CreatedAtUtc : conversationStorage.CreatedAtUtc,
                        LastUpdatedAtUtc = conversationStorage.LastUpdatedAtUtc == default ? conversationIndex.LastUpdatedAtUtc : conversationStorage.LastUpdatedAtUtc,
                        Interruption = null,
                        Snapshot = snapshot,
                        Title = conversationStorage.Title ?? string.Empty,
                        ToolTip = conversationStorage.ToolTip ?? string.Empty,
                    };

                    if (string.IsNullOrWhiteSpace(conversation.Title))
                    {
                        ApplyConversationMetadata(conversation, snapshot, 0);
                    }

                    _workspaceState.Conversations.Add(conversation);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LauncherAIPersistence] 恢复会话失败: {conversationIndex.ConversationId}, {ex.Message}");
                }
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

    private ErrorAnalysisSessionSnapshot CreateSnapshotFromStorage(LauncherAIConversationStorageModel conversationStorage)
    {
        var storage = conversationStorage.Session ?? new LauncherAISessionStorageModel();
        var chatMessages = (storage.ChatMessages ?? []).Select(CreateUiChatMessage).ToList();
        if (conversationStorage.Interruption != null)
        {
            chatMessages.Add(CreateInterruptedConversationMessage(conversationStorage.Interruption));
        }

        return new ErrorAnalysisSessionSnapshot
        {
            ChatInput = storage.ChatInput ?? string.Empty,
            PendingImageAttachments = RestoreAttachments(storage.PendingImageAttachments),
            IsChatEnabled = storage.IsChatEnabled,
            HasChatMessages = chatMessages.Count > 0,
            ChatMessages = chatMessages,
            ActionProposals = (storage.ActionProposals ?? []).Select(CreateActionProposal).ToList(),
        };
    }

    private UiChatMessage CreateUiChatMessage(LauncherAIChatMessageStorageModel storage)
    {
        var role = storage.Role ?? string.Empty;
        var content = storage.Content ?? string.Empty;
        var message = new UiChatMessage(
            role,
            content,
            storage.IncludeInAIHistory,
            RestoreAttachments(storage.ImageAttachments))
        {
            ShowRoleHeader = storage.ShowRoleHeader,
            DisplayRoleText = string.IsNullOrWhiteSpace(storage.DisplayRoleText) ? role : storage.DisplayRoleText,
            AIHistoryContent = storage.AIHistoryContent,
            ToolCallId = storage.ToolCallId,
            ToolCalls = CloneToolCalls(storage.ToolCalls),
            ToolInputContent = storage.ToolInputContent,
            ToolOutputContent = storage.ToolOutputContent,
            AIHistoryImageAttachments = storage.AIHistoryImageAttachments == null
                ? null
                : RestoreAttachments(storage.AIHistoryImageAttachments),
            SuppressContentRendering = storage.SuppressContentRendering,
        };

        return message;
    }

    private List<ChatImageAttachment> RestoreAttachments(IEnumerable<LauncherAIAttachmentStorageModel>? attachments)
    {
        List<ChatImageAttachment> results = [];
        foreach (var attachment in attachments ?? [])
        {
            var restored = _workspacePersistenceService.RestoreAttachment(attachment);
            if (restored != null)
            {
                results.Add(restored);
            }
        }

        return results;
    }

    private static LauncherAIActionProposalStorageModel CreateActionProposalStorageModel(AgentActionProposal proposal)
    {
        return new LauncherAIActionProposalStorageModel
        {
            ActionType = proposal.ActionType,
            ButtonText = proposal.ButtonText,
            DisplayMessage = proposal.DisplayMessage,
            PermissionLevel = proposal.PermissionLevel.ToString(),
            Parameters = new Dictionary<string, string>(proposal.Parameters, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static AgentActionProposal CreateActionProposal(LauncherAIActionProposalStorageModel storage)
    {
        return new AgentActionProposal
        {
            ActionType = storage.ActionType ?? string.Empty,
            ButtonText = storage.ButtonText ?? string.Empty,
            DisplayMessage = storage.DisplayMessage ?? string.Empty,
            PermissionLevel = ParsePermissionLevel(storage.PermissionLevel),
            Parameters = new Dictionary<string, string>(storage.Parameters ?? [], StringComparer.OrdinalIgnoreCase)
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

    private LauncherAIConversationInterruptionStorageModel? CreateConversationInterruption(LauncherAIConversationTab conversation)
    {
        if (SelectedConversation?.Id != conversation.Id)
        {
            return conversation.Interruption;
        }

        if (_sessionState.IsAIAnalyzing)
        {
            return new LauncherAIConversationInterruptionStorageModel
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
            return new LauncherAIConversationInterruptionStorageModel
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

    private UiChatMessage CreateInterruptedConversationMessage(LauncherAIConversationInterruptionStorageModel interruption)
    {
        var content = string.IsNullOrWhiteSpace(interruption.Message)
            ? (_languageSelectorService.Language == "zh-CN"
                ? "上次应用退出时，此对话已中断。聊天历史已恢复，但不会自动继续。"
                : "This conversation was interrupted when the app exited. History has been restored, but it will not resume automatically.")
            : interruption.Message!;

        return new UiChatMessage("assistant", content, includeInAIHistory: false)
        {
            ShowRoleHeader = false,
            AIHistoryContent = null,
        };
    }

    private void QueueConversationDeletion(Guid conversationId)
    {
        if (_lifecycleCts.IsCancellationRequested)
        {
            return;
        }

        _ = DeleteConversationFromPersistenceAsync(conversationId, _lifecycleCts.Token);
    }

    private async Task DeleteConversationFromPersistenceAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        try
        {
            await _persistenceSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _workspacePersistenceService.DeleteConversationAsync(conversationId, cancellationToken);
            }
            finally
            {
                _persistenceSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LauncherAIPersistence] 删除会话失败: {conversationId}, {ex.Message}");
        }
    }
}