using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace XianYuLauncher.Core.Helpers;

public static class AppAssetResolver
{
    private const string MsAppxPrefix = "ms-appx:///";
    private const string MsAppxWebPrefix = "ms-appx-web:///";

    public const string PlaceholderAssetPath = "Assets/Placeholder.png";
    public const string DefaultAvatarAssetPath = "Assets/Icons/Avatars/Steve.png";
    public const string DefaultSkinTextureAssetPath = "Assets/Icons/Textures/steve.png";
    public const string DefaultVersionIconAssetPath = "Assets/Icons/Download_Options/Vanilla/icon_128x128.png";
    public const string FabricIconAssetPath = "Assets/Icons/Download_Options/Fabric/Fabric_Icon.png";
    public const string LegacyFabricIconAssetPath = "Assets/Icons/Download_Options/Legacy-Fabric/Legacy-Fabric.png";
    public const string ForgeIconAssetPath = "Assets/Icons/Download_Options/Forge/MinecraftForge_Icon.jpg";
    public const string NeoForgeIconAssetPath = "Assets/Icons/Download_Options/NeoForge/NeoForge_Icon.png";
    public const string OptifineIconAssetPath = "Assets/Icons/Download_Options/Optifine/Optifine.ico";
    public const string LiteLoaderIconAssetPath = "Assets/Icons/Download_Options/Liteloader/Liteloader.ico";
    public const string QuiltIconAssetPath = "Assets/Icons/Download_Options/Quilt/Quilt.png";
    public const string CleanroomIconAssetPath = "Assets/Icons/Download_Options/Cleanroom/Cleanroom.png";
    public const string WindowIconAssetPath = "Assets/WindowIcon.ico";
    public const string Skin3DPreviewHtmlAssetPath = "Assets/Skin3DPreview.html";
    public const string Bangbang93AvatarAssetPath = "Assets/Icons/Contributors/bangbang93.jpg";
    public const string McModAvatarAssetPath = "Assets/Icons/Contributors/mcmod.ico";
    public const string SpiritStudioAvatarAssetPath = "Assets/Icons/Contributors/SpiritStudio.png";

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();

        if (trimmed.StartsWith(MsAppxPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[MsAppxPrefix.Length..];
        }

        if (trimmed.StartsWith(MsAppxWebPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[MsAppxWebPrefix.Length..];
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return Path.GetFullPath(uri.LocalPath);
        }

        if (Path.IsPathRooted(trimmed))
        {
            return Path.GetFullPath(trimmed);
        }

        return trimmed.TrimStart('/', '\\').Replace('\\', '/');
    }

    public static bool IsAppAssetPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim();

        if (trimmed.StartsWith(MsAppxPrefix, StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith(MsAppxWebPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return string.Equals(uri.Scheme, "ms-appx", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, "ms-appx-web", StringComparison.OrdinalIgnoreCase);
        }

        var normalizedPath = trimmed.TrimStart('/', '\\');
        return normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedPath, "Assets", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetLoaderIconAssetPath(string? loaderName, out string iconAssetPath)
    {
        iconAssetPath = NormalizeLoaderKey(loaderName) switch
        {
            "fabric" => FabricIconAssetPath,
            "legacyfabric" => LegacyFabricIconAssetPath,
            "forge" => ForgeIconAssetPath,
            "neoforge" => NeoForgeIconAssetPath,
            "quilt" => QuiltIconAssetPath,
            "optifine" => OptifineIconAssetPath,
            "liteloader" => LiteLoaderIconAssetPath,
            "cleanroom" => CleanroomIconAssetPath,
            _ => string.Empty
        };

        return !string.IsNullOrEmpty(iconAssetPath);
    }

    public static string ToAbsolutePath(string path)
    {
        var normalizedPath = NormalizePath(path);

        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(normalizedPath))
        {
            return normalizedPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    public static bool TryCreateUri(string? path, out Uri? uri)
    {
        uri = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri))
        {
            uri = absoluteUri;
            return true;
        }

        if (Path.IsPathRooted(trimmed))
        {
            uri = new Uri(Path.GetFullPath(trimmed));
            return true;
        }

        if (!IsAppAssetPath(trimmed))
        {
            return false;
        }

        uri = AppEnvironment.IsMSIX
            ? new Uri($"{MsAppxPrefix}{NormalizePath(trimmed)}")
            : new Uri(ToAbsolutePath(trimmed));

        return true;
    }

    public static Uri ToUri(string path)
    {
        if (!TryCreateUri(path, out var uri) || uri is null)
        {
            throw new InvalidOperationException($"无法解析资源路径: {path}");
        }

        return uri;
    }

    public static string ToUriString(string path)
    {
        return ToUri(path).ToString();
    }

    public static async Task<StorageFile> GetStorageFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("资源路径不能为空。", nameof(path));
        }

        var trimmed = path.Trim();

        if (IsAppAssetPath(trimmed))
        {
            return AppEnvironment.IsMSIX
                ? await StorageFile.GetFileFromApplicationUriAsync(new Uri($"{MsAppxPrefix}{NormalizePath(trimmed)}"))
                : await StorageFile.GetFileFromPathAsync(ToAbsolutePath(trimmed));
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return await StorageFile.GetFileFromPathAsync(Path.GetFullPath(uri.LocalPath));
        }

        if (Path.IsPathRooted(trimmed))
        {
            return await StorageFile.GetFileFromPathAsync(Path.GetFullPath(trimmed));
        }

        throw new FileNotFoundException($"不支持的资源路径: {path}", path);
    }

    private static string NormalizeLoaderKey(string? loaderName)
    {
        return loaderName?
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant() ?? string.Empty;
    }
}