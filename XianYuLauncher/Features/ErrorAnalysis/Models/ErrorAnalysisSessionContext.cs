using System.Collections.Generic;

namespace XianYuLauncher.Features.ErrorAnalysis.Models;

public sealed class ErrorAnalysisSessionContext
{
    public string LaunchCommand { get; set; } = string.Empty;

    public string OriginalLog { get; set; } = string.Empty;

    public bool IsGameCrashed { get; set; }

    public string VersionId { get; set; } = string.Empty;

    public string MinecraftPath { get; set; } = string.Empty;

    public List<string> GameOutput { get; } = [];

    public List<string> GameError { get; } = [];
}
