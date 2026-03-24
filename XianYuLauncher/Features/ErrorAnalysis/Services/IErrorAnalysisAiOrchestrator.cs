using System.Threading;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public interface IErrorAnalysisAiOrchestrator
{
    Task AnalyzeCrashAsync(Func<ToolCallInfo, Task<string>> executeToolCallAsync, CancellationToken cancellationToken);

    Task SendMessageAsync(string userMessage, Func<ToolCallInfo, Task<string>> executeToolCallAsync, CancellationToken cancellationToken);
}