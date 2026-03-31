using CommunityToolkit.Mvvm.ComponentModel;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.ViewModels;

public sealed partial class LauncherAIConversationTab : ObservableObject
{
    public Guid Id { get; init; }

    public bool IsErrorAnalysisConversation { get; init; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastUpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public LauncherAIConversationInterruptionStorageModel? Interruption { get; set; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _toolTip = string.Empty;

    public ErrorAnalysisSessionSnapshot Snapshot { get; set; } = ErrorAnalysisSessionSnapshot.CreateEmpty(false);
}