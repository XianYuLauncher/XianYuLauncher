using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public partial class ErrorAnalysisSessionState : ObservableObject
{
    private readonly List<AgentActionProposal> _pendingActionProposals = [];

    public ErrorAnalysisSessionState()
    {
        ChatMessages.CollectionChanged += (_, _) => HasChatMessages = ChatMessages.Count > 0;
        PendingImageAttachments.CollectionChanged += (_, _) => HasPendingImageAttachments = PendingImageAttachments.Count > 0;
    }

    public ErrorAnalysisSessionContext Context { get; } = new();

    public ObservableCollection<string> LogLines { get; } = [];

    public ObservableCollection<UiChatMessage> ChatMessages { get; } = [];

    public ObservableCollection<ChatImageAttachment> PendingImageAttachments { get; } = [];

    [ObservableProperty]
    private string _fullLog = string.Empty;

    [ObservableProperty]
    private string _crashReason = string.Empty;

    [ObservableProperty]
    private bool _isLauncherAIWindowOpen;

    [ObservableProperty]
    private string _aIAnalysisResult = string.Empty;

    [ObservableProperty]
    private bool _isAIAnalyzing;

    [ObservableProperty]
    private bool _isAIAnalysisAvailable;

    [ObservableProperty]
    private bool _hasChatMessages;

    [ObservableProperty]
    private string _chatInput = string.Empty;

    [ObservableProperty]
    private bool _hasPendingImageAttachments;

    [ObservableProperty]
    private bool _isChatEnabled;

    [ObservableProperty]
    private bool _hasFixAction;

    [ObservableProperty]
    private string _fixButtonText = string.Empty;

    [ObservableProperty]
    private bool _hasSecondaryFixAction;

    [ObservableProperty]
    private string _secondaryFixButtonText = string.Empty;

    [ObservableProperty]
    private AgentActionProposal? _currentFixAction;

    [ObservableProperty]
    private AgentActionProposal? _secondaryFixAction;

    [ObservableProperty]
    private int _pendingActionProposalCount;

    public AgentConversationContinuation? PendingToolContinuation { get; private set; }

    public bool HasPendingToolContinuation => PendingToolContinuation != null;

    private CancellationTokenSource? _aiAnalysisCts;
    private bool _suppressNextCancellationMessage;

    public void ReplaceGameOutput(IReadOnlyCollection<string> lines)
    {
        lock (Context.GameOutput)
        {
            Context.GameOutput.Clear();
            Context.GameOutput.AddRange(lines);
        }
    }

    public void ReplaceGameError(IReadOnlyCollection<string> lines)
    {
        lock (Context.GameError)
        {
            Context.GameError.Clear();
            Context.GameError.AddRange(lines);
        }
    }

    public (List<string> Output, List<string> Error) CreateLogSnapshot()
    {
        List<string> output = [];
        List<string> error = [];

        lock (Context.GameOutput)
        {
            output.AddRange(Context.GameOutput);
        }

        lock (Context.GameError)
        {
            error.AddRange(Context.GameError);
        }

        return (output, error);
    }

    public void ResetFixActions()
    {
        _pendingActionProposals.Clear();
        UpdateVisibleFixActions();
    }

    public void ApplyActionProposals(IReadOnlyList<AgentActionProposal> actions)
    {
        _pendingActionProposals.Clear();
        _pendingActionProposals.AddRange(CloneActionProposals(actions));
        UpdateVisibleFixActions();
    }

    public void SetPendingToolContinuation(List<ChatMessage> apiMessages)
    {
        PendingToolContinuation = new AgentConversationContinuation
        {
            ApiMessages = CloneApiMessages(apiMessages)
        };
        OnPropertyChanged(nameof(PendingToolContinuation));
        OnPropertyChanged(nameof(HasPendingToolContinuation));
    }

    public AgentConversationContinuation? PeekPendingToolContinuation()
    {
        return PendingToolContinuation;
    }

    public void AppendPendingToolContinuationUserMessage(ChatMessage message)
    {
        if (PendingToolContinuation == null)
        {
            return;
        }

        PendingToolContinuation.ApiMessages.AddRange(CloneApiMessages([message]));
        OnPropertyChanged(nameof(PendingToolContinuation));
    }

    public AgentActionProposal? CompleteCurrentActionProposal()
    {
        if (_pendingActionProposals.Count > 0)
        {
            _pendingActionProposals.RemoveAt(0);
        }

        UpdateVisibleFixActions();
        return CurrentFixAction;
    }

    public AgentConversationContinuation? TakePendingToolContinuation()
    {
        var continuation = PendingToolContinuation;
        PendingToolContinuation = null;
        OnPropertyChanged(nameof(PendingToolContinuation));
        OnPropertyChanged(nameof(HasPendingToolContinuation));
        return continuation;
    }

    public void ClearPendingToolContinuation()
    {
        PendingToolContinuation = null;
        OnPropertyChanged(nameof(PendingToolContinuation));
        OnPropertyChanged(nameof(HasPendingToolContinuation));
    }

    public CancellationTokenSource BeginAIAnalysisTokenSource()
    {
        if (_aiAnalysisCts != null)
        {
            _aiAnalysisCts.Cancel();
            _aiAnalysisCts.Dispose();
        }

        _aiAnalysisCts = new CancellationTokenSource();
        return _aiAnalysisCts;
    }

    public bool IsCurrentAiAnalysisTokenSource(CancellationTokenSource tokenSource)
    {
        return ReferenceEquals(_aiAnalysisCts, tokenSource);
    }

    public void CompleteAIAnalysisTokenSource(CancellationTokenSource tokenSource)
    {
        if (!ReferenceEquals(_aiAnalysisCts, tokenSource))
        {
            return;
        }

        tokenSource.Dispose();
        _aiAnalysisCts = null;
    }

    public void SuppressNextCancellationMessage()
    {
        _suppressNextCancellationMessage = true;
    }

    public bool TryConsumeCancellationMessageSuppression()
    {
        if (!_suppressNextCancellationMessage)
        {
            return false;
        }

        _suppressNextCancellationMessage = false;
        return true;
    }

    public CancellationToken BeginAIAnalysisToken()
    {
        return BeginAIAnalysisTokenSource().Token;
    }

    public void CancelAIAnalysis()
    {
        if (_aiAnalysisCts != null && !_aiAnalysisCts.IsCancellationRequested)
        {
            _aiAnalysisCts.Cancel();
        }
    }

    public void DisposeAiAnalysisToken()
    {
        _aiAnalysisCts?.Dispose();
        _aiAnalysisCts = null;
    }

    public ErrorAnalysisSessionSnapshot CreateSnapshot()
    {
        return new ErrorAnalysisSessionSnapshot
        {
            ChatInput = ChatInput,
            PendingImageAttachments = CloneImageAttachments(PendingImageAttachments),
            IsChatEnabled = IsChatEnabled,
            HasChatMessages = HasChatMessages,
            ChatMessages = CloneUiMessages(ChatMessages),
            ActionProposals = CloneActionProposals(_pendingActionProposals),
            PendingToolContinuation = CloneContinuation(PendingToolContinuation),
        };
    }

    public void ApplySnapshot(ErrorAnalysisSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ChatInput = snapshot.ChatInput;
        ReplacePendingImageAttachments(snapshot.PendingImageAttachments);
        IsChatEnabled = snapshot.IsChatEnabled;
        ReplaceChatMessages(snapshot.ChatMessages);
        ApplyActionProposals(snapshot.ActionProposals);
        PendingToolContinuation = CloneContinuation(snapshot.PendingToolContinuation);
        OnPropertyChanged(nameof(PendingToolContinuation));
        OnPropertyChanged(nameof(HasPendingToolContinuation));
        HasChatMessages = ChatMessages.Count > 0 || snapshot.HasChatMessages;
    }

    public void ClearPendingImageAttachments()
    {
        PendingImageAttachments.Clear();
    }

    private void ReplacePendingImageAttachments(IEnumerable<ChatImageAttachment> attachments)
    {
        PendingImageAttachments.Clear();
        foreach (var attachment in CloneImageAttachments(attachments))
        {
            PendingImageAttachments.Add(attachment);
        }
    }

    private void ReplaceChatMessages(IEnumerable<UiChatMessage> messages)
    {
        ChatMessages.Clear();
        foreach (var message in CloneUiMessages(messages))
        {
            ChatMessages.Add(message);
        }
    }

    private void UpdateVisibleFixActions()
    {
        PendingActionProposalCount = _pendingActionProposals.Count;
        CurrentFixAction = _pendingActionProposals.FirstOrDefault();
        FixButtonText = CurrentFixAction?.ButtonText ?? string.Empty;
        HasFixAction = !string.IsNullOrWhiteSpace(FixButtonText);

        SecondaryFixAction = null;
        SecondaryFixButtonText = string.Empty;
        HasSecondaryFixAction = false;
    }

    private static List<ChatMessage> CloneApiMessages(IEnumerable<ChatMessage> apiMessages)
    {
        return apiMessages.Select(message => new ChatMessage(message.Role, message.Content, CloneToolCalls(message.ToolCalls))
        {
            ToolCallId = message.ToolCallId,
            ImageAttachments = CloneImageAttachments(message.ImageAttachments)
        }).ToList();
    }

    private static List<ToolCallInfo>? CloneToolCalls(IEnumerable<ToolCallInfo>? toolCalls)
    {
        return toolCalls?.Select(toolCall => new ToolCallInfo
        {
            Id = toolCall.Id,
            FunctionName = toolCall.FunctionName,
            Arguments = toolCall.Arguments
        }).ToList();
    }

    private static List<UiChatMessage> CloneUiMessages(IEnumerable<UiChatMessage> messages)
    {
        return messages.Select(message => new UiChatMessage(message.Role, message.Content, message.IncludeInAIHistory, message.ImageAttachments)
        {
            ShowRoleHeader = message.ShowRoleHeader,
            DisplayRoleText = message.DisplayRoleText,
            AIHistoryContent = message.AIHistoryContent,
            AIHistoryImageAttachments = CloneImageAttachments(message.AIHistoryImageAttachments),
            SuppressContentRendering = message.SuppressContentRendering,
            ToolCallId = message.ToolCallId,
            ToolCalls = CloneToolCalls(message.ToolCalls)
        }).ToList();
    }

    private static List<ChatImageAttachment> CloneImageAttachments(IEnumerable<ChatImageAttachment>? attachments)
    {
        return attachments?.Select(attachment => new ChatImageAttachment
        {
            FileName = attachment.FileName,
            FilePath = attachment.FilePath,
            ContentType = attachment.ContentType,
            DataUrl = attachment.DataUrl
        }).ToList() ?? [];
    }

    private static List<AgentActionProposal> CloneActionProposals(IEnumerable<AgentActionProposal>? proposals)
    {
        return proposals?
            .Select(CloneActionProposal)
            .ToList() ?? [];
    }

    private static AgentActionProposal CloneActionProposal(AgentActionProposal proposal)
    {
        return new AgentActionProposal
        {
            ActionType = proposal.ActionType,
            ButtonText = proposal.ButtonText,
            DisplayMessage = proposal.DisplayMessage,
            PermissionLevel = proposal.PermissionLevel,
            Parameters = new Dictionary<string, string>(proposal.Parameters, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static AgentConversationContinuation? CloneContinuation(AgentConversationContinuation? continuation)
    {
        if (continuation == null)
        {
            return null;
        }

        return new AgentConversationContinuation
        {
            ApiMessages = CloneApiMessages(continuation.ApiMessages)
        };
    }
}
