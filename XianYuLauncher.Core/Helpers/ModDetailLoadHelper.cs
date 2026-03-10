using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

public static class ModDetailLoadHelper
{
    private static readonly HashSet<string> CurseForgeLoaderTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "forge",
        "neoforge",
        "fabric",
        "quilt",
        "optifine",
        "iris",
        "legacyfabric"
    };

    public static string ResolveModrinthProjectType(string? sourceType, string? projectType)
    {
        if (string.Equals(sourceType, "mod", StringComparison.OrdinalIgnoreCase))
        {
            return "mod";
        }

        if (string.Equals(sourceType, "datapack", StringComparison.OrdinalIgnoreCase))
        {
            return "datapack";
        }

        return string.IsNullOrWhiteSpace(projectType) ? "mod" : projectType;
    }

    public static string ResolveCurseForgeProjectType(int? classId, string? sourceType)
    {
        if (classId.HasValue)
        {
            return ModResourcePathHelper.MapCurseForgeClassIdToProjectType(classId);
        }

        return string.IsNullOrWhiteSpace(sourceType) ? "mod" : sourceType;
    }

    public static IReadOnlyList<string> BuildModrinthSupportedLoaders(
        IEnumerable<string>? projectLoaders,
        IEnumerable<string>? categories,
        string projectType,
        string? sourceType)
    {
        IEnumerable<string> values;

        if (string.Equals(sourceType, "mod", StringComparison.OrdinalIgnoreCase))
        {
            values = projectLoaders?.Where(loader => !string.Equals(loader, "datapack", StringComparison.OrdinalIgnoreCase))
                ?? Enumerable.Empty<string>();
        }
        else if (string.Equals(sourceType, "datapack", StringComparison.OrdinalIgnoreCase)
            || projectType is "resourcepack" or "datapack" or "shader" or "world")
        {
            values = categories ?? Enumerable.Empty<string>();
        }
        else
        {
            values = projectLoaders ?? Enumerable.Empty<string>();
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(FormatDisplayToken)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ModrinthVersion> FilterModrinthVersionsBySourceType(
        IEnumerable<ModrinthVersion>? versions,
        string? sourceType)
    {
        var versionList = versions?.ToList() ?? [];

        return sourceType?.ToLowerInvariant() switch
        {
            "mod" => versionList.Where(HasNonDatapackLoader).ToList(),
            "datapack" => versionList.Where(HasDatapackLoader).ToList(),
            _ => versionList
        };
    }

    public static IReadOnlyList<ModDetailGameVersionGroup> BuildModrinthVersionGroups(
        IEnumerable<string>? projectGameVersions,
        IEnumerable<ModrinthVersion>? versions,
        bool hideSnapshots)
    {
        var versionList = versions?.ToList() ?? [];
        if (versionList.Count == 0)
        {
            return [];
        }

        var loaderNameCache = versionList
            .Where(version => version.Loaders != null)
            .SelectMany(version => version.Loaders)
            .Where(loader => !string.IsNullOrWhiteSpace(loader))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(loader => loader, FormatDisplayToken, StringComparer.OrdinalIgnoreCase);

        var versionsByGameVersion = versionList
            .Where(version => version.GameVersions != null)
            .SelectMany(version => version.GameVersions.Select(gameVersion => new { GameVersion = gameVersion, Version = version }))
            .GroupBy(item => item.GameVersion)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Version)
                    .Distinct()
                    .OrderByDescending(version => ParseDate(version.DatePublished))
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var orderedGameVersions = BuildOrderedGameVersions(projectGameVersions, versionsByGameVersion.Keys, hideSnapshots);
        var result = new List<ModDetailGameVersionGroup>();

        foreach (var gameVersion in orderedGameVersions)
        {
            if (!versionsByGameVersion.TryGetValue(gameVersion, out var gameVersionVersions) || gameVersionVersions.Count == 0)
            {
                continue;
            }

            var loaderGroups = gameVersionVersions
                .Where(version => version.Loaders != null)
                .SelectMany(version => version.Loaders.Select(loader => new { Loader = loader, Version = version }))
                .GroupBy(item => item.Loader)
                .Select(group => new ModDetailLoaderGroup(
                    loaderNameCache.TryGetValue(group.Key, out var formattedLoader) ? formattedLoader : FormatDisplayToken(group.Key),
                    group.Select(item => item.Version)
                        .Distinct()
                        .Select(version =>
                        {
                            var file = version.Files?.FirstOrDefault();
                            return file == null
                                ? null
                                : new ModDetailVersionItem(
                                    version.VersionNumber,
                                    version.DatePublished,
                                    version.Name,
                                    file.Url?.ToString(),
                                    file.Filename,
                                    version.Loaders.Select(loader => loaderNameCache.TryGetValue(loader, out var cachedLoader) ? cachedLoader : FormatDisplayToken(loader)).ToList(),
                                    version.VersionType,
                                    gameVersion,
                                    version,
                                    null);
                        })
                        .Where(item => item != null)
                        .Cast<ModDetailVersionItem>()
                        .ToList()))
                .Where(group => group.Versions.Count > 0)
                .ToList();

            if (loaderGroups.Count > 0)
            {
                result.Add(new ModDetailGameVersionGroup(gameVersion, loaderGroups));
            }
        }

        return result;
    }

    public static IReadOnlyList<string> BuildCurseForgeSupportedLoaders(IEnumerable<CurseForgeFileIndex>? fileIndexes)
    {
        return (fileIndexes ?? Enumerable.Empty<CurseForgeFileIndex>())
            .Where(fileIndex => fileIndex.ModLoader.HasValue)
            .Select(fileIndex => fileIndex.ModLoader!.Value switch
            {
                1 => "Forge",
                4 => "Fabric",
                5 => "Quilt",
                6 => "NeoForge",
                _ => null
            })
            .Where(loader => !string.IsNullOrWhiteSpace(loader))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
    }

    public static IReadOnlyList<ModDetailGameVersionGroup> BuildCurseForgeVersionGroups(
        IEnumerable<CurseForgeFile>? files,
        bool hideSnapshots)
    {
        var fileList = files?.ToList() ?? [];
        if (fileList.Count == 0)
        {
            return [];
        }

        var fileInfoList = new List<(CurseForgeFile File, string GameVersion, string Loader)>();

        foreach (var file in fileList)
        {
            var gameVersions = new List<string>();
            var loaders = new List<string>();

            foreach (var value in file.GameVersions ?? [])
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (CurseForgeLoaderTokens.Contains(value))
                {
                    loaders.Add(FormatDisplayToken(value));
                    continue;
                }

                gameVersions.Add(value);
            }

            if (loaders.Count == 0)
            {
                loaders.Add("Generic");
            }

            foreach (var gameVersion in gameVersions)
            {
                foreach (var loader in loaders.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    fileInfoList.Add((file, gameVersion, loader));
                }
            }
        }

        var gameVersionGroups = fileInfoList
            .GroupBy(item => item.GameVersion)
            .Where(group => !hideSnapshots || !ModVersionClassifierHelper.IsSnapshotVersion(group.Key))
            .OrderByDescending(group => group.Key, new MinecraftVersionStringComparer())
            .Select(group => new ModDetailGameVersionGroup(
                group.Key,
                group.GroupBy(item => item.Loader)
                    .Select(loaderGroup => new ModDetailLoaderGroup(
                        loaderGroup.Key,
                        loaderGroup.Select(item => item.File)
                            .DistinctBy(file => file.Id)
                            .OrderByDescending(file => file.FileDate)
                            .Select(file => new ModDetailVersionItem(
                                file.DisplayName,
                                file.FileDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                file.DisplayName,
                                file.DownloadUrl,
                                file.FileName,
                                [loaderGroup.Key],
                                ModVersionClassifierHelper.GetCurseForgeVersionType(file.ReleaseType),
                                group.Key,
                                null,
                                file))
                            .ToList()))
                    .Where(loaderGroup => loaderGroup.Versions.Count > 0)
                    .ToList()))
            .Where(group => group.Loaders.Count > 0)
            .ToList();

        return gameVersionGroups;
    }

    private static IReadOnlyList<string> BuildOrderedGameVersions(
        IEnumerable<string>? projectGameVersions,
        IEnumerable<string> fallbackGameVersions,
        bool hideSnapshots)
    {
        var versions = (projectGameVersions?.Any() == true ? projectGameVersions : fallbackGameVersions)
            .Where(version => !string.IsNullOrWhiteSpace(version));

        if (hideSnapshots)
        {
            versions = versions.Where(version => !ModVersionClassifierHelper.IsSnapshotVersion(version));
        }

        return versions
            .Reverse()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasDatapackLoader(ModrinthVersion version) =>
        version.Loaders?.Any(loader => string.Equals(loader, "datapack", StringComparison.OrdinalIgnoreCase)) == true;

    private static bool HasNonDatapackLoader(ModrinthVersion version) =>
        version.Loaders?.Any(loader => !string.Equals(loader, "datapack", StringComparison.OrdinalIgnoreCase)) == true;

    private static string FormatDisplayToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.ToLowerInvariant() switch
        {
            "legacyfabric" or "legacy-fabric" => "LegacyFabric",
            "liteloader" => "LiteLoader",
            "neoforge" => "NeoForge",
            _ when value.Length == 1 => value.ToUpperInvariant(),
            _ => char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant()
        };
    }

    private static DateTimeOffset ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
    }

    private sealed class MinecraftVersionStringComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == y) return 0;
            if (string.IsNullOrEmpty(x)) return -1;
            if (string.IsNullOrEmpty(y)) return 1;

            var xParts = ParseVersion(x);
            var yParts = ParseVersion(y);

            int maxLength = Math.Max(xParts.Length, yParts.Length);
            for (int i = 0; i < maxLength; i++)
            {
                int xPart = i < xParts.Length ? xParts[i] : 0;
                int yPart = i < yParts.Length ? yParts[i] : 0;

                if (xPart != yPart)
                {
                    return xPart.CompareTo(yPart);
                }
            }

            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }

        private static int[] ParseVersion(string version)
        {
            var match = Regex.Match(version, @"^(\d+)\.(\d+)(?:\.(\d+))?");
            if (!match.Success)
            {
                return [];
            }

            var parts = new List<int>();
            for (int i = 1; i < match.Groups.Count; i++)
            {
                if (match.Groups[i].Success && int.TryParse(match.Groups[i].Value, out int part))
                {
                    parts.Add(part);
                }
            }

            return [.. parts];
        }
    }
}