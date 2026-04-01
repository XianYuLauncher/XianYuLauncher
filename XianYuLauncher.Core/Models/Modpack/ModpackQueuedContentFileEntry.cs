namespace XianYuLauncher.Core.Models;

/// <summary>
/// 表示整合包内容文件的排队项。
/// </summary>
public sealed record ModpackQueuedContentFileEntry(string FileKey, string FileName);