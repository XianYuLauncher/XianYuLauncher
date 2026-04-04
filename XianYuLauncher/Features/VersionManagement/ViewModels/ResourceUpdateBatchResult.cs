using System.Collections.Generic;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

public class ResourceUpdateBatchResult
{
    public bool IsSuccess { get; set; }
    public int UpdatedCount { get; set; }
    public int UpToDateCount { get; set; }
    public int FailedCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; } = new();
}
