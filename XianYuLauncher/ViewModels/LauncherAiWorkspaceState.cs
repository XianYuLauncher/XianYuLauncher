using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XianYuLauncher.ViewModels;

public sealed partial class LauncherAiWorkspaceState : ObservableObject
{
    public ObservableCollection<LauncherAiConversationTab> Conversations { get; } = [];

    [ObservableProperty]
    private Guid? _activeErrorAnalysisConversationId;

    [ObservableProperty]
    private bool _isErrorAnalysisPageOpen;

    [ObservableProperty]
    private Guid? _selectedConversationId;

    [ObservableProperty]
    private int _nextConversationNumber = 1;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private bool _defaultChatEnabled;
}