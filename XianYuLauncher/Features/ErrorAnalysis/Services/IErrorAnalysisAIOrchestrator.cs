using System.Threading;

using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public interface IErrorAnalysisAIOrchestrator
{
    Task AnalyzeCrashAsync(CancellationToken cancellationToken);

    Task SendMessageAsync(string userMessage, IReadOnlyList<ChatImageAttachment> imageAttachments, CancellationToken cancellationToken);

    Task ApproveActionAsync(AgentActionProposal proposal, CancellationToken cancellationToken);

    Task RejectPendingActionAsync(string? rejectedActionText, CancellationToken cancellationToken);
}