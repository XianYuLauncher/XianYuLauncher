using System.Threading;

using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public interface IErrorAnalysisAiOrchestrator
{
    Task AnalyzeCrashAsync(CancellationToken cancellationToken);

    Task SendMessageAsync(string userMessage, CancellationToken cancellationToken);

    Task ApproveActionAsync(AgentActionProposal proposal, CancellationToken cancellationToken);

    Task RejectPendingActionAsync(string? rejectedActionText, CancellationToken cancellationToken);
}