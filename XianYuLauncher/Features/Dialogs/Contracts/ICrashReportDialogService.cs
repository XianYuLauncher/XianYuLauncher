using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface ICrashReportDialogService
{
    Task<CrashReportDialogAction> ShowCrashReportDialogAsync(
        string crashTitle,
        string crashAnalysis,
        string fullLog,
        bool isEasterEggMode);
}