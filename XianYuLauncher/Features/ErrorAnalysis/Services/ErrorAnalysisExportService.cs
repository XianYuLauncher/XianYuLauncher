using System.IO.Compression;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public class ErrorAnalysisExportService : IErrorAnalysisExportService
{
    private readonly ErrorAnalysisSessionState _sessionState;
    private readonly ILogSanitizerService _logSanitizerService;
    private readonly IFilePickerService _filePickerService;
    private readonly ICommonDialogService _dialogService;

    public ErrorAnalysisExportService(
        ErrorAnalysisSessionState sessionState,
        ILogSanitizerService logSanitizerService,
        IFilePickerService filePickerService,
        ICommonDialogService dialogService)
    {
        _sessionState = sessionState;
        _logSanitizerService = logSanitizerService;
        _filePickerService = filePickerService;
        _dialogService = dialogService;
    }

    public async Task ExportAsync()
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string? zipFilePath = await _filePickerService.PickSaveFilePathAsync(
                string.Format("crash_logs_{0}", timestamp),
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["ZIP 压缩文件"] = new[] { FileExtensionConsts.Zip }
                },
                Windows.Storage.Pickers.PickerLocationId.Downloads);

            if (string.IsNullOrWhiteSpace(zipFilePath))
            {
                return;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Directory.CreateDirectory(tempDir);

                await ExportLaunchCommandAsync(tempDir);
                await ExportCrashReportAsync(tempDir, timestamp);
                await ExportLauncherLogsAsync(tempDir);
                ExportVersionJson(tempDir);

                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }

                ZipFile.CreateFromDirectory(tempDir, zipFilePath);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }

            await _dialogService.ShowMessageDialogAsync(
                "成功",
                string.Format("崩溃日志已成功导出到：{0}\n\n包含内容：\n• 游戏崩溃日志\n• 启动参数\n• 启动器日志\n• 版本配置文件", zipFilePath),
                "确定");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("错误", string.Format("导出崩溃日志失败：{0}", ex.Message), "确定");
        }
    }

    private async Task ExportLaunchCommandAsync(string tempDir)
    {
        string batFilePath = Path.Combine(tempDir, "启动参数.bat");
        string sanitizedLaunchCommand = await _logSanitizerService.SanitizeAsync(_sessionState.Context.LaunchCommand);
        await File.WriteAllTextAsync(batFilePath, sanitizedLaunchCommand);
    }

    private async Task ExportCrashReportAsync(string tempDir, string timestamp)
    {
        string crashLogFile = Path.Combine(tempDir, string.Format("crash_report_{0}.txt", timestamp));
        string sanitizedCrashLog = await _logSanitizerService.SanitizeAsync(_sessionState.FullLog);
        await File.WriteAllTextAsync(crashLogFile, sanitizedCrashLog);
    }

    private async Task ExportLauncherLogsAsync(string tempDir)
    {
        try
        {
            string launcherLogDir = AppEnvironment.SafeLogPath;
            if (!Directory.Exists(launcherLogDir))
            {
                return;
            }

            string logSubDir = Path.Combine(tempDir, "launcher_logs");
            Directory.CreateDirectory(logSubDir);

            var logFiles = Directory.GetFiles(launcherLogDir, "log-*.txt")
                .OrderByDescending(File.GetLastWriteTime)
                .Take(3)
                .ToList();

            foreach (var logFile in logFiles)
            {
                try
                {
                    string fileName = Path.GetFileName(logFile);
                    string destPath = Path.Combine(logSubDir, fileName);

                    using var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var streamReader = new StreamReader(fileStream);
                    string content = await streamReader.ReadToEndAsync();
                    string sanitizedContent = await _logSanitizerService.SanitizeAsync(content);
                    await File.WriteAllTextAsync(destPath, sanitizedContent);
                }
                catch (IOException)
                {
                    System.Diagnostics.Debug.WriteLine("无法读取日志文件进行脱敏");
                }
            }

            System.Diagnostics.Debug.WriteLine($"已复制 {logFiles.Count} 个启动器日志文件");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"复制启动器日志失败: {ex.Message}");
        }
    }

    private void ExportVersionJson(string tempDir)
    {
        try
        {
            string versionId = _sessionState.Context.VersionId;
            string minecraftPath = _sessionState.Context.MinecraftPath;
            if (string.IsNullOrWhiteSpace(versionId) || string.IsNullOrWhiteSpace(minecraftPath))
            {
                System.Diagnostics.Debug.WriteLine("版本信息未设置，跳过 version.json 复制");
                return;
            }

            string versionJsonPath = Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionId, $"{versionId}.json");
            if (!File.Exists(versionJsonPath))
            {
                System.Diagnostics.Debug.WriteLine("version.json 不存在");
                return;
            }

            string destPath = Path.Combine(tempDir, "version.json");
            File.Copy(versionJsonPath, destPath);
            System.Diagnostics.Debug.WriteLine("已复制 version.json");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"复制 version.json 失败: {ex.Message}");
        }
    }
}