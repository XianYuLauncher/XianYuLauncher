using System.Collections.Generic;

namespace XianYuLauncher.ViewModels;

internal class ModUpdateResult
{
    public HashSet<string> ProcessedMods { get; set; } = new HashSet<string>();
    public int UpdatedCount { get; set; }
    public int UpToDateCount { get; set; }
}
