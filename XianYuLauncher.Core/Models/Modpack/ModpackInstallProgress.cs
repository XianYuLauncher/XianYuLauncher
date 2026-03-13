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
