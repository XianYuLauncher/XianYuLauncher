using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Models;

public sealed class AgentConversationContinuation
{
    public required List<ChatMessage> ApiMessages { get; init; }
}