using System.IO;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Models;

namespace XianYuLauncher.Services;

public sealed class ModLoaderIconPresentationService : IModLoaderIconPresentationService
{
    public string DefaultVersionIconPath => "ms-appx:///Assets/Icons/Download_Options/Vanilla/icon_128x128.png";
    private readonly object _builtInIconsLock = new();
    private IReadOnlyList<VersionIconOption>? _builtInIcons;

    private static readonly Dictionary<string, string> LoaderIconFallbackMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["forge"] = "ms-appx:///Assets/Icons/Download_Options/Forge/MinecraftForge_Icon.jpg",
        ["fabric"] = "ms-appx:///Assets/Icons/Download_Options/Fabric/fabric_Icon.png",
        ["neoforge"] = "ms-appx:///Assets/Icons/Download_Options/NeoForge/NeoForge_Icon.png",
        ["optifine"] = "ms-appx:///Assets/Icons/Download_Options/Optifine/Optifine.ico",
        ["quilt"] = "ms-appx:///Assets/Icons/Download_Options/Quilt/Quilt.png",
        ["legacyfabric"] = "ms-appx:///Assets/Icons/Download_Options/Legacy-Fabric/Legacy-Fabric.png",
        ["cleanroom"] = "ms-appx:///Assets/Icons/Download_Options/Cleanroom/Cleanroom.png"
    };

    public IReadOnlyList<VersionIconOption> LoadBuiltInIcons()
    {
        if (_builtInIcons is not null)
        {
            return _builtInIcons;
        }

        lock (_builtInIconsLock)
        {
            if (_builtInIcons is not null)
            {
                return _builtInIcons;
            }

        var result = new List<VersionIconOption>();
        var baseDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "Download_Options");

        if (!Directory.Exists(baseDirectory))
        {
            _builtInIcons = result.AsReadOnly();
            return _builtInIcons;
        }

        var iconFiles = Directory
            .GetFiles(baseDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in iconFiles)
        {
            var relativeFromAssets = Path.GetRelativePath(
                Path.Combine(AppContext.BaseDirectory, "Assets"),
                file).Replace("\\", "/");

            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
            var parentFolderName = Directory.GetParent(file)?.Name ?? string.Empty;

            result.Add(new VersionIconOption
            {
                DisplayName = BuildIconDisplayName(fileNameWithoutExt, parentFolderName),
                IconPath = $"ms-appx:///Assets/{relativeFromAssets}"
            });
        }

            _builtInIcons = result.AsReadOnly();
            return _builtInIcons;
        }
    }

    public string BuildVersionDisplayText(string minecraftVersion, string? selectedModLoaderName, bool isOptifineSelected, bool isLiteLoaderSelected)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return string.Empty;
        }

        var selectedLoaders = new List<string>();

        if (!string.IsNullOrWhiteSpace(selectedModLoaderName))
        {
            selectedLoaders.Add(NormalizeLoaderDisplayName(selectedModLoaderName));
        }

        if (isOptifineSelected && selectedLoaders.All(x => !string.Equals(x, "Optifine", StringComparison.OrdinalIgnoreCase)))
        {
            selectedLoaders.Add("Optifine");
        }

        if (isLiteLoaderSelected && selectedLoaders.All(x => !string.Equals(x, "LiteLoader", StringComparison.OrdinalIgnoreCase)))
        {
            selectedLoaders.Add("LiteLoader");
        }

        return selectedLoaders.Count == 0
            ? minecraftVersion
            : $"{minecraftVersion}-{string.Join("-", selectedLoaders)}";
    }

    public string ResolveAutoIconPath(
        string? lastSelectedLoaderName,
        string? selectedModLoaderName,
        bool isOptifineSelected,
        bool isLiteLoaderSelected,
        IEnumerable<VersionIconOption> availableIcons)
    {
        var loaderName = ResolveLastSelectedActiveLoaderName(
            lastSelectedLoaderName,
            selectedModLoaderName,
            isOptifineSelected,
            isLiteLoaderSelected);

        if (string.IsNullOrWhiteSpace(loaderName))
        {
            return DefaultVersionIconPath;
        }

        var normalizedLoaderName = NormalizeLoaderLookupKey(loaderName);
        var matchedIcon = availableIcons.FirstOrDefault(icon =>
            NormalizeLoaderLookupKey(icon.DisplayName) == normalizedLoaderName);

        if (matchedIcon != null && !string.IsNullOrWhiteSpace(matchedIcon.IconPath))
        {
            return matchedIcon.IconPath;
        }

        if (LoaderIconFallbackMap.TryGetValue(normalizedLoaderName, out var fallbackPath))
        {
            return fallbackPath;
        }

        return DefaultVersionIconPath;
    }

    private static string ResolveLastSelectedActiveLoaderName(
        string? lastSelectedLoaderName,
        string? selectedModLoaderName,
        bool isOptifineSelected,
        bool isLiteLoaderSelected)
    {
        if (!string.IsNullOrWhiteSpace(lastSelectedLoaderName) && IsLoaderStillSelected(lastSelectedLoaderName, selectedModLoaderName, isOptifineSelected, isLiteLoaderSelected))
        {
            return lastSelectedLoaderName;
        }

        if (isOptifineSelected)
        {
            return "Optifine";
        }

        if (isLiteLoaderSelected)
        {
            return "LiteLoader";
        }

        return selectedModLoaderName ?? string.Empty;
    }

    private static bool IsLoaderStillSelected(
        string loaderName,
        string? selectedModLoaderName,
        bool isOptifineSelected,
        bool isLiteLoaderSelected)
    {
        if (string.Equals(loaderName, "Optifine", StringComparison.OrdinalIgnoreCase))
        {
            return isOptifineSelected;
        }

        if (string.Equals(loaderName, "LiteLoader", StringComparison.OrdinalIgnoreCase))
        {
            return isLiteLoaderSelected;
        }

        return !string.IsNullOrWhiteSpace(selectedModLoaderName)
            && string.Equals(loaderName, selectedModLoaderName, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildIconDisplayName(string fileNameWithoutExt, string parentFolderName)
    {
        return fileNameWithoutExt.ToLowerInvariant() switch
        {
            "grass_block_side" => "Vanilla",
            "fabric" => "Fabric",
            "fabric_icon" => "Fabric",
            "forge" => "Forge",
            "minecraftforge_icon" => "Forge",
            "quilt" => "Quilt",
            "neoforge" => "NeoForge",
            "neoforge_icon" => "NeoForge",
            "optifine" => "Optifine",
            "liteloader" => "LiteLoader",
            "legacy-fabric" => "LegacyFabric",
            "cleanroom" => "Cleanroom",
            _ => string.IsNullOrWhiteSpace(parentFolderName) ? fileNameWithoutExt : parentFolderName
        };
    }

    private static string NormalizeLoaderDisplayName(string loaderName)
    {
        return loaderName switch
        {
            "NeoForge" => "NeoForge",
            "LegacyFabric" => "LegacyFabric",
            "Optifine" => "Optifine",
            _ => loaderName
        };
    }

    private static string NormalizeLoaderLookupKey(string loaderName)
    {
        return loaderName
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }
}
