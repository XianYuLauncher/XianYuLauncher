using XianYuLauncher.Models;

namespace XianYuLauncher.Contracts.Services;

public interface IModLoaderIconPresentationService
{
    string DefaultVersionIconPath { get; }

    IReadOnlyList<VersionIconOption> LoadBuiltInIcons();

    string BuildVersionDisplayText(string minecraftVersion, string? selectedModLoaderName, bool isOptifineSelected, bool isLiteLoaderSelected);

    string ResolveAutoIconPath(
        string? lastSelectedLoaderName,
        string? selectedModLoaderName,
        bool isOptifineSelected,
        bool isLiteLoaderSelected,
        IEnumerable<VersionIconOption> availableIcons);

    /// <summary>
    /// 根据图标文件名和父文件夹名生成友好的显示名称。
    /// </summary>
    /// <param name="fileNameWithoutExt">不带扩展名的文件名</param>
    /// <param name="parentFolderName">父文件夹名称</param>
    /// <returns>友好的显示名称</returns>
    string GetIconDisplayName(string fileNameWithoutExt, string parentFolderName);
}
