using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XianYuLauncher.Core.Models;
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

    public CrashFixAction? CurrentFixAction { get; set; }

    public CrashFixAction? SecondaryFixAction { get; set; }

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
}
