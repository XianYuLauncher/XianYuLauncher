using System.Threading;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public interface IErrorAnalysisAiOrchestrator
{
    Task AnalyzeCrashAsync(CancellationToken cancellationToken);

    Task SendMessageAsync(string userMessage, CancellationToken cancellationToken);
}