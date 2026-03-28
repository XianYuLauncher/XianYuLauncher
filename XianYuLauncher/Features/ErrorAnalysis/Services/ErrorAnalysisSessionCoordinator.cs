using System.Collections.Generic;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public interface IErrorAnalysisSessionCoordinator
{
    void SetLogData(string launchCommand, IReadOnlyList<string> gameOutput, IReadOnlyList<string> gameError);

    void RefreshLogDataPreservingAnalysis(string launchCommand, IReadOnlyList<string> gameOutput, IReadOnlyList<string> gameError);

    void SetGameCrashStatus(bool isCrashed);

    void SetLaunchCommand(string launchCommand);

    void SetVersionInfo(string versionId, string minecraftPath);

    void ClearLogsOnly();

    void AddGameOutputLog(string logLine);

    void AddGameErrorLog(string logLine);
}

public sealed class ErrorAnalysisSessionCoordinator : IErrorAnalysisSessionCoordinator
{
    private readonly ErrorAnalysisSessionState _sessionState;
    private readonly IErrorAnalysisLogService _logService;

    public ErrorAnalysisSessionCoordinator(
        ErrorAnalysisSessionState sessionState,
        IErrorAnalysisLogService logService)
    {
        _sessionState = sessionState;
        _logService = logService;
    }

    public void SetLogData(string launchCommand, IReadOnlyList<string> gameOutput, IReadOnlyList<string> gameError)
    {
        ResetAnalysisState();
        _logService.SetLogData(launchCommand, gameOutput, gameError);
    }

    public void RefreshLogDataPreservingAnalysis(string launchCommand, IReadOnlyList<string> gameOutput, IReadOnlyList<string> gameError)
    {
        _logService.SetLogData(launchCommand, gameOutput, gameError);
    }

    public void SetGameCrashStatus(bool isCrashed)
    {
        _sessionState.Context.IsGameCrashed = isCrashed;
        _sessionState.IsAiAnalysisAvailable = isCrashed;

        if (!isCrashed)
        {
            _sessionState.IsAiAnalyzing = false;
        }
    }

    public void SetLaunchCommand(string launchCommand)
    {
        _sessionState.Context.LaunchCommand = launchCommand;
    }

    public void SetVersionInfo(string versionId, string minecraftPath)
    {
        _sessionState.Context.VersionId = versionId;
        _sessionState.Context.MinecraftPath = minecraftPath;
    }

    public void ClearLogsOnly()
    {
        _sessionState.CancelAiAnalysis();
        ResetAnalysisState();
        _logService.InitializeRealTimeLogs();
    }

    public void AddGameOutputLog(string logLine)
    {
        _logService.AddGameOutputLog(logLine);
    }

    public void AddGameErrorLog(string logLine)
    {
        _logService.AddGameErrorLog(logLine);
    }

    private void ResetAnalysisState()
    {
        _sessionState.Context.IsGameCrashed = false;
        _sessionState.IsAiAnalyzing = false;
        _sessionState.IsAiAnalysisAvailable = false;
        _sessionState.AiAnalysisResult = string.Empty;
        _sessionState.ResetFixActions();
    }
}