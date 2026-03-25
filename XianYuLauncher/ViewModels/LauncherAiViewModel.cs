using CommunityToolkit.Mvvm.ComponentModel;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;

namespace XianYuLauncher.ViewModels;

public sealed partial class LauncherAiViewModel : ObservableObject
{
    private readonly IAiSettingsDomainService _aiSettingsDomainService;
    private readonly ILanguageSelectorService _languageSelectorService;

    public LauncherAiViewModel(
        ErrorAnalysisViewModel chatViewModel,
        IAiSettingsDomainService aiSettingsDomainService,
        ILanguageSelectorService languageSelectorService)
    {
        ChatViewModel = chatViewModel;
        _aiSettingsDomainService = aiSettingsDomainService;
        _languageSelectorService = languageSelectorService;
    }

    public ErrorAnalysisViewModel ChatViewModel { get; }

    public string EmptyStateText => _languageSelectorService.Language == "zh-CN"
        ? "还没有对话，先向 Launcher AI 提一个问题。"
        : "No conversation yet. Ask Launcher AI a question to get started.";

    public async Task InitializeAsync()
    {
        var state = await _aiSettingsDomainService.LoadAsync();
        ChatViewModel.ResetFixActionState();
        ChatViewModel.IsChatEnabled = state.IsEnabled;
    }
}