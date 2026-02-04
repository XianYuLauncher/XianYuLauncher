using System;
using System.IO;

namespace XianYuLauncher.Helpers
{
    public static class ShortcutHelper
    {
        /// <summary>
        /// Trim trailing directory separator from path
        /// </summary>
        public static string TrimTrailingDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            return path.TrimEnd('\\', '/');
        }

        /// <summary>
        /// Prepare default application icon in cache directory
        /// </summary>
        /// <returns>Path to the default icon, or null if preparation failed</returns>
        public static string PrepareDefaultAppIcon(string cacheDir)
        {
            string iconPath = Path.Combine(cacheDir, "DefaultAppIcon.ico");

            if (!File.Exists(iconPath))
            {
                try
                {
                    string installedLoc = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
                    string assetIcon = Path.Combine(installedLoc, "Assets", "WindowIcon.ico");
                    if (File.Exists(assetIcon))
                    {
                        File.Copy(assetIcon, iconPath, true);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to copy default app icon: {ex.Message}");
                }
            }

            return iconPath;
        }

        /// <summary>
        /// Check if a shortcut with the given path already exists
        /// </summary>
        /// <returns>True if shortcut exists, false otherwise</returns>
        public static bool ShortcutExists(string shortcutPath)
        {
            return File.Exists(shortcutPath);
        }

        /// <summary>
        /// Validate a shortcut URL for correctness
        /// </summary>
        public static bool ValidateShortcutUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var validatedUri))
            {
                return false;
            }

            // Check scheme
            if (validatedUri.Scheme != "xianyulauncher")
            {
                return false;
            }

            // Check host
            if (validatedUri.Host != "launch")
            {
                return false;
            }

            // Parse query parameters and validate path parameter for UNC paths
            var query = validatedUri.Query.TrimStart('?');
            foreach (var pair in query.Split('&'))
            {
                var equalIndex = pair.IndexOf('=');
                if (equalIndex >= 0)
                {
                    var key = System.Net.WebUtility.UrlDecode(pair.Substring(0, equalIndex));
                    var value = equalIndex + 1 < pair.Length 
                        ? System.Net.WebUtility.UrlDecode(pair.Substring(equalIndex + 1)) 
                        : string.Empty;

                    // If this is a path parameter, check for UNC paths
                    if (key.Equals("path", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(value))
                    {
                        if (value.StartsWith("\\\\", StringComparison.Ordinal) ||
                            value.StartsWith("//", StringComparison.Ordinal))
                        {
                            return false; // UNC paths not allowed
                        }
                    }
                }
            }

            return true;
        }
    }
}
