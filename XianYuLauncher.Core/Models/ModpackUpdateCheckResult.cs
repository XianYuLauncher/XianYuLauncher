namespace XianYuLauncher.Core.Models;

public class ModpackUpdateCheckResult
{
    public bool Success { get; set; }

    public bool HasUpdate { get; set; }

    public string Platform { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string CurrentVersionId { get; set; } = string.Empty;

    public string? LatestVersionId { get; set; }

    public string? LatestVersionName { get; set; }

    public string? ErrorMessage { get; set; }

    public static ModpackUpdateCheckResult Failure(
        string errorMessage,
        string platform = "",
        string projectId = "",
        string currentVersionId = "")
    {
        return new ModpackUpdateCheckResult
        {
            Success = false,
            HasUpdate = false,
            Platform = platform,
            ProjectId = projectId,
            CurrentVersionId = currentVersionId,
            ErrorMessage = errorMessage
        };
    }

    public static ModpackUpdateCheckResult NoUpdate(
        string platform,
        string projectId,
        string currentVersionId,
        string? latestVersionId = null,
        string? latestVersionName = null)
    {
        return new ModpackUpdateCheckResult
        {
            Success = true,
            HasUpdate = false,
            Platform = platform,
            ProjectId = projectId,
            CurrentVersionId = currentVersionId,
            LatestVersionId = latestVersionId,
            LatestVersionName = latestVersionName
        };
    }
}