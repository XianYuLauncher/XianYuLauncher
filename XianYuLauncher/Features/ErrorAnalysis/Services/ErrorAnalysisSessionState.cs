using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public partial class ErrorAnalysisSessionState : ObservableObject
{
    public ErrorAnalysisSessionState()
    {
        ChatMessages.CollectionChanged += (_, _) => HasChatMessages = ChatMessages.Count > 0;
    }

    public ErrorAnalysisSessionContext Context { get; } = new();

    public ObservableCollection<string> LogLines { get; } = [];

    public ObservableCollection<UiChatMessage> ChatMessages { get; } = [];

    [ObservableProperty]
    private string _fullLog = string.Empty;

    [ObservableProperty]
    private string _crashReason = string.Empty;

    [ObservableProperty]
    private bool _isFixerWindowOpen;

    [ObservableProperty]
    private string _aiAnalysisResult = string.Empty;

    [ObservableProperty]
    private bool _isAiAnalyzing;

    [ObservableProperty]
    private bool _isAiAnalysisAvailable;

    [ObservableProperty]
    private bool _hasChatMessages;

    [ObservableProperty]
    private string _chatInput = string.Empty;

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

    public ErrorAnalysisActionProposal? CurrentFixAction { get; set; }

    public ErrorAnalysisActionProposal? SecondaryFixAction { get; set; }

    private CancellationTokenSource? _aiAnalysisCts;

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
        HasFixAction = false;
        FixButtonText = string.Empty;
        CurrentFixAction = null;
        HasSecondaryFixAction = false;
        SecondaryFixButtonText = string.Empty;
        SecondaryFixAction = null;
    }

    public void ApplyActionProposals(IReadOnlyList<ErrorAnalysisActionProposal> actions)
    {
        ResetFixActions();

        if (actions.Count > 0)
        {
            CurrentFixAction = actions[0];
            FixButtonText = actions[0].ButtonText;
            HasFixAction = !string.IsNullOrWhiteSpace(FixButtonText);
        }

        if (actions.Count > 1)
        {
            SecondaryFixAction = actions[1];
            SecondaryFixButtonText = actions[1].ButtonText;
            HasSecondaryFixAction = !string.IsNullOrWhiteSpace(SecondaryFixButtonText);
        }
    }

    public CancellationToken BeginAiAnalysisToken()
    {
        _aiAnalysisCts?.Dispose();
        _aiAnalysisCts = new CancellationTokenSource();
        return _aiAnalysisCts.Token;
    }

    public void CancelAiAnalysis()
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
}
