namespace XianYuLauncher.Models;

/// <summary>
/// 收藏夹导入结果条目（纯数据 DTO，供 DialogService 展示使用）。
/// </summary>
public sealed record FavoritesImportResultItem(string ItemName, string StatusText, bool IsGrayedOut = true);
