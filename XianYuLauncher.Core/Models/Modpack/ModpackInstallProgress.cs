using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 整合包安装进度信息
/// </summary>
public class ModpackInstallProgress
{
    public double Progress { get; set; }
    public string ProgressText { get; set; } = "0%";
    public string Status { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
}

public enum ModpackContentFileProgressState
{
    Downloading = 0,
    Completed,
    Failed,
    Cancelled
}

public sealed class ModpackContentFileProgress
{
    public required string FileKey { get; init; }

    public required string FileName { get; init; }

    public ModpackContentFileProgressState State { get; init; }

    public double Progress { get; init; }

    public DownloadProgressStatus? DownloadStatus { get; init; }

    public string? ErrorMessage { get; init; }

    public static ModpackContentFileProgress Downloading(
        string fileKey,
        string fileName,
        DownloadProgressStatus downloadStatus)
    {
        return new ModpackContentFileProgress
        {
            FileKey = fileKey,
            FileName = fileName,
            State = ModpackContentFileProgressState.Downloading,
            Progress = Math.Clamp(downloadStatus.Percent, 0, 100),
            DownloadStatus = downloadStatus,
        };
    }

    public static ModpackContentFileProgress Completed(string fileKey, string fileName)
    {
        return new ModpackContentFileProgress
        {
            FileKey = fileKey,
            FileName = fileName,
            State = ModpackContentFileProgressState.Completed,
            Progress = 100,
        };
    }

    public static ModpackContentFileProgress Failed(string fileKey, string fileName, string errorMessage)
    {
        return new ModpackContentFileProgress
        {
            FileKey = fileKey,
            FileName = fileName,
            State = ModpackContentFileProgressState.Failed,
            Progress = 100,
            ErrorMessage = errorMessage,
        };
    }

    public static ModpackContentFileProgress Cancelled(string fileKey, string fileName)
    {
        return new ModpackContentFileProgress
        {
            FileKey = fileKey,
            FileName = fileName,
            State = ModpackContentFileProgressState.Cancelled,
            Progress = 100,
        };
    }
}

/// <summary>
/// 整合包安装结果
/// </summary>
public class ModpackInstallResult
{
    public bool Success { get; set; }
    public string ModpackName { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public static ModpackInstallResult Succeeded(string modpackName, string versionId) => new()
    {
        Success = true,
        ModpackName = modpackName,
        VersionId = versionId
    };

    public static ModpackInstallResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
