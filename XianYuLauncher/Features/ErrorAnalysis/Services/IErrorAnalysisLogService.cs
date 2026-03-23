namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public interface IErrorAnalysisLogService
{
    void SetLogData(string launchCommand, IReadOnlyList<string> gameOutput, IReadOnlyList<string> gameError);

    void InitializeRealTimeLogs();

    void AddGameOutputLog(string logLine);

    void AddGameErrorLog(string logLine);
}