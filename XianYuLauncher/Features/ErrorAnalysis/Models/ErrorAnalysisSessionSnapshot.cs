using XianYuLauncher.Core.Models;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.ErrorAnalysis.Models;

public sealed class ErrorAnalysisSessionSnapshot
{
    public string ChatInput { get; init; } = string.Empty;

    public bool IsChatEnabled { get; init; }

    public bool HasChatMessages { get; init; }

    public List<UiChatMessage> ChatMessages { get; init; } = [];

    public List<AgentActionProposal> ActionProposals { get; init; } = [];

    public AgentConversationContinuation? PendingToolContinuation { get; init; }

    public static ErrorAnalysisSessionSnapshot CreateEmpty(bool isChatEnabled)
    {
        return new ErrorAnalysisSessionSnapshot
        {
            IsChatEnabled = isChatEnabled,
            HasChatMessages = false,
        };
    }
}