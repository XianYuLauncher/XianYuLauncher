using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.ViewModels;

internal static class VersionManagementShortcutOps
{
    public static async Task<string> CreateVersionShortcutFileAsync(string versionName, string versionPath, MinecraftProfile? profile = null)
    {
        var shortcutName = BuildShortcutDisplayName(versionName, profile?.Name);
        var shortcutPath = BuildShortcutPath(shortcutName);
        var targetPath = Helpers.ShortcutHelper.TrimTrailingDirectorySeparator(versionPath);
        var encodedPath = Uri.EscapeDataString(targetPath ?? string.Empty);
        var url = AppendProfileQuery($"xianyulauncher://launch/?path={encodedPath}", profile);

        if (!Helpers.ShortcutHelper.ValidateShortcutUrl(url))
        {
            throw new InvalidOperationException("Invalid shortcut URL constructed for version.");
        }

        var iconPath = Helpers.ShortcutHelper.PrepareDefaultAppIcon(EnsureShortcutCacheDirectory());
        await WriteInternetShortcutFileAsync(shortcutPath, url, iconPath);
        return shortcutName;
    }

    public static async Task<string> CreateMapShortcutFileAsync(MapInfo map, string versionName, string versionPath, MinecraftProfile? profile = null)
    {
        var shortcutName = BuildShortcutDisplayName($"{versionName} - {map.Name}", profile?.Name);
        var shortcutPath = BuildShortcutPath(shortcutName);

        var iconPath = await ResolveMapShortcutIconAsync(map);

        var targetPath = Helpers.ShortcutHelper.TrimTrailingDirectorySeparator(versionPath);
        var encodedPath = Uri.EscapeDataString(targetPath ?? string.Empty);
        var encodedMap = Uri.EscapeDataString(map.FileName ?? string.Empty);
        var url = AppendProfileQuery($"xianyulauncher://launch/?path={encodedPath}&map={encodedMap}", profile);

        if (!Helpers.ShortcutHelper.ValidateShortcutUrl(url))
        {
            throw new InvalidOperationException("Invalid shortcut URL constructed for map.");
        }

        await WriteInternetShortcutFileAsync(shortcutPath, url, iconPath);
        return shortcutName;
    }

    public static async Task<string> CreateServerShortcutFileAsync(ServerItem server, string versionName, string versionPath, MinecraftProfile? profile = null)
    {
        var shortcutName = BuildShortcutDisplayName($"{versionName} - {server.Name}", profile?.Name);
        var shortcutPath = BuildShortcutPath(shortcutName);

        var iconPath = await ResolveServerShortcutIconAsync(server);

        var targetPath = Helpers.ShortcutHelper.TrimTrailingDirectorySeparator(versionPath);
        ParseServerAddressAndPort(server.Address, out var finalAddress, out var portPart);

        var encodedPath = Uri.EscapeDataString(targetPath ?? string.Empty);
        var encodedServer = Uri.EscapeDataString(finalAddress ?? string.Empty);
        var url = AppendProfileQuery($"xianyulauncher://launch/?path={encodedPath}&server={encodedServer}&port={portPart}", profile);

        if (!Helpers.ShortcutHelper.ValidateShortcutUrl(url))
        {
            throw new InvalidOperationException("Invalid shortcut URL constructed for server.");
        }

        await WriteInternetShortcutFileAsync(shortcutPath, url, iconPath);
        return shortcutName;
    }

    public static string BuildVersionShortcutPath(string versionName, string? profileName = null)
    {
        return BuildShortcutPath(BuildShortcutDisplayName(versionName, profileName));
    }

    public static string BuildMapShortcutPath(string mapName, string versionName, string? profileName = null)
    {
        return BuildShortcutPath(BuildShortcutDisplayName($"{versionName} - {mapName}", profileName));
    }

    public static string BuildServerShortcutPath(string serverName, string versionName, string? profileName = null)
    {
        return BuildShortcutPath(BuildShortcutDisplayName($"{versionName} - {serverName}", profileName));
    }

    private static string BuildShortcutDisplayName(string baseName, string? profileName)
    {
        var safeBaseName = string.Join("_", baseName.Split(Path.GetInvalidFileNameChars()));
        return safeBaseName;
    }

    private static string BuildShortcutPath(string shortcutName)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Path.Combine(desktopPath, $"{shortcutName}.url");
    }

    private static async Task<string> ResolveMapShortcutIconAsync(MapInfo map)
    {
        var cacheDirectory = EnsureShortcutCacheDirectory();
        var iconPath = Helpers.ShortcutHelper.PrepareDefaultAppIcon(cacheDirectory);

        if (!string.IsNullOrEmpty(map.Icon) && File.Exists(map.Icon))
        {
            try
            {
                var mapIconHash = Helpers.HashHelper.ComputeMD5(map.Icon);
                var customIconPath = Path.Combine(cacheDirectory, $"{mapIconHash}.ico");
                if (!File.Exists(customIconPath))
                {
                    await Helpers.IconHelper.ConvertPngToIcoAsync(map.Icon, customIconPath);
                }

                if (File.Exists(customIconPath))
                {
                    iconPath = customIconPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to convert map icon: {ex.Message}");
            }
        }

        return iconPath;
    }

    private static async Task<string> ResolveServerShortcutIconAsync(ServerItem server)
    {
        var cacheDirectory = EnsureShortcutCacheDirectory();
        var iconPath = Helpers.ShortcutHelper.PrepareDefaultAppIcon(cacheDirectory);

        if (!string.IsNullOrEmpty(server.IconBase64))
        {
            try
            {
                var validIconName = Helpers.HashHelper.ComputeMD5(server.Address + server.Name);
                var savedIconPath = Path.Combine(cacheDirectory, $"{validIconName}.ico");

                if (!File.Exists(savedIconPath))
                {
                    var base64Data = server.IconBase64;
                    if (base64Data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        var commaIndex = base64Data.IndexOf(',');
                        if (commaIndex >= 0 && commaIndex < base64Data.Length - 1)
                        {
                            base64Data = base64Data[(commaIndex + 1)..];
                        }
                        else
                        {
                            throw new FormatException("Invalid data URI format for server icon");
                        }
                    }

                    var pngBytes = Convert.FromBase64String(base64Data);
                    var icoBytes = Helpers.IconHelper.CreateIcoFromPng(pngBytes);
                    await File.WriteAllBytesAsync(savedIconPath, icoBytes);
                }

                iconPath = savedIconPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Saving server icon failed: {ex.Message}");
            }
        }

        return iconPath;
    }

    private static string EnsureShortcutCacheDirectory()
    {
        var cacheDirectory = Path.Combine(AppEnvironment.SafeCachePath, "Shortcuts");
        if (!Directory.Exists(cacheDirectory))
        {
            Directory.CreateDirectory(cacheDirectory);
        }

        return cacheDirectory;
    }

    private static void ParseServerAddressAndPort(string serverAddress, out string finalAddress, out string portPart)
    {
        finalAddress = serverAddress;
        portPart = "25565";

        if (string.IsNullOrEmpty(finalAddress))
        {
            return;
        }

        if (finalAddress.StartsWith("[", StringComparison.Ordinal))
        {
            var endBracket = finalAddress.IndexOf(']');
            if (endBracket > 0)
            {
                var hostPart = finalAddress.Substring(1, endBracket - 1);
                var remainder = endBracket + 1 < finalAddress.Length
                    ? finalAddress[(endBracket + 1)..]
                    : string.Empty;

                if (remainder.StartsWith(":", StringComparison.Ordinal))
                {
                    var portCandidate = remainder[1..];
                    if (int.TryParse(portCandidate, out _))
                    {
                        portPart = portCandidate;
                    }
                }

                finalAddress = hostPart;
            }

            return;
        }

        var firstColon = finalAddress.IndexOf(':');
        var lastColon = finalAddress.LastIndexOf(':');
        if (firstColon > 0 && firstColon == lastColon)
        {
            var hostPart = finalAddress[..lastColon];
            var portCandidate = lastColon + 1 < finalAddress.Length
                ? finalAddress[(lastColon + 1)..]
                : string.Empty;

            if (!string.IsNullOrEmpty(portCandidate) && int.TryParse(portCandidate, out _))
            {
                finalAddress = hostPart;
                portPart = portCandidate;
            }
        }
    }

    private static async Task WriteInternetShortcutFileAsync(string shortcutPath, string url, string iconPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine("[InternetShortcut]");
        builder.AppendLine($"URL={url}");
        builder.AppendLine("IconIndex=0");
        builder.AppendLine($"IconFile={iconPath}");
        await File.WriteAllTextAsync(shortcutPath, builder.ToString());
    }

    private static string AppendProfileQuery(string url, MinecraftProfile? profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
        {
            return url;
        }

        return $"{url}&profileId={Uri.EscapeDataString(profile.Id)}";
    }
}
