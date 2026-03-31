using Microsoft.Extensions.DependencyInjection;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Features.ErrorAnalysis.Services;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Views;

namespace XianYuLauncher.Services;

public sealed class GameCrashContext
{
    public required int ExitCode { get; init; }

    public required string LaunchCommand { get; init; }

    public required List<string> GameOutput { get; init; }

    public required List<string> GameError { get; init; }

    public required string VersionId { get; init; }

    public required string MinecraftPath { get; init; }

    public GameLaunchObservationOrigin Origin { get; init; }
}

public interface IGameCrashWorkflowService
{
    Task HandleCrashAsync(GameCrashContext context);
}

public sealed class GameCrashWorkflowService : IGameCrashWorkflowService
{
    private readonly ICrashAnalyzer _crashAnalyzer;
    private readonly ICrashReportDialogService _crashReportDialogService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly INavigationService _navigationService;
    private readonly IErrorAnalysisSessionCoordinator _errorAnalysisSessionCoordinator;
    private readonly IErrorAnalysisExportService _errorAnalysisExportService;
    private readonly IUiDispatcher _uiDispatcher;

    public GameCrashWorkflowService(
        ICrashAnalyzer crashAnalyzer,
        ICrashReportDialogService crashReportDialogService,
        ILocalSettingsService localSettingsService,
        INavigationService navigationService,
        IErrorAnalysisSessionCoordinator errorAnalysisSessionCoordinator,
        IErrorAnalysisExportService errorAnalysisExportService,
        IUiDispatcher uiDispatcher)
    {
        _crashAnalyzer = crashAnalyzer;
        _crashReportDialogService = crashReportDialogService;
        _localSettingsService = localSettingsService;
        _navigationService = navigationService;
        _errorAnalysisSessionCoordinator = errorAnalysisSessionCoordinator;
        _errorAnalysisExportService = errorAnalysisExportService;
        _uiDispatcher = uiDispatcher;
    }

    public async Task HandleCrashAsync(GameCrashContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var crashResult = await _crashAnalyzer.AnalyzeCrashAsync(context.ExitCode, context.GameOutput, context.GameError);
        var fullLog = BuildCrashSummaryLog(context, crashResult.Analysis);

        var isEasterEggMode = await _localSettingsService.ReadSettingAsync<bool?>("EasterEggMode") ?? false;
        var action = CrashReportDialogAction.Close;
        await _uiDispatcher.RunOnUiThreadAsync(async () =>
        {
            action = await _crashReportDialogService.ShowCrashReportDialogAsync(
                crashResult.Title,
                crashResult.Analysis,
                fullLog,
                isEasterEggMode);
        });

        if (action == CrashReportDialogAction.Close)
        {
            return;
        }

        _errorAnalysisSessionCoordinator.SetVersionInfo(context.VersionId, context.MinecraftPath);

        var currentContent = _navigationService.Frame?.Content;
        if (currentContent is ErrorAnalysisPage errorAnalysisPage)
        {
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                errorAnalysisPage.ViewModel.SetVersionInfo(context.VersionId, context.MinecraftPath);
                _errorAnalysisSessionCoordinator.RefreshLogDataPreservingAnalysis(context.LaunchCommand, context.GameOutput, context.GameError);
                errorAnalysisPage.ViewModel.SetGameCrashStatus(true);
            });

            if (action == CrashReportDialogAction.ExportLogs)
            {
                await Task.Delay(500);
                await _errorAnalysisExportService.ExportAsync();
            }

            return;
        }

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _navigationService.NavigateTo(typeof(ErrorAnalysisViewModel).FullName!, Tuple.Create(context.LaunchCommand, context.GameOutput, context.GameError));
        });

        if (action == CrashReportDialogAction.ExportLogs)
        {
            await Task.Delay(500);
            await _errorAnalysisExportService.ExportAsync();
        }
    }

    private static string BuildCrashSummaryLog(GameCrashContext context, string crashAnalysis)
    {
        List<string> allLogs =
        [
            "=== 游戏崩溃报告 ===",
            $"崩溃时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"退出代码: {context.ExitCode}",
            $"崩溃分析: {crashAnalysis}",
            string.Empty,
            "=== 游戏错误日志 ==="
        ];

        allLogs.AddRange(context.GameError);
        allLogs.Add(string.Empty);
        return string.Join(Environment.NewLine, allLogs);
    }
}