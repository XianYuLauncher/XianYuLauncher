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
}
