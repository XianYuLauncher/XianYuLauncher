using System;
using System.Collections.Generic;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

public static class VersionLibraryMergeHelper
{
    public static List<Library> MergeLibraries(
        params IEnumerable<Library>?[] libraryGroups)
    {
        var mergedLibraries = new List<Library>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var libraryGroup in libraryGroups)
        {
            AddLibraries(mergedLibraries, seenKeys, libraryGroup);
        }

        return mergedLibraries;
    }

    public static string GetLibraryConflictKey(Library? library)
    {
        return GetLibraryConflictKey(library?.Name);
    }

    public static string GetLibraryConflictKey(string? libraryName)
    {
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return string.Empty;
        }

        var coordinateWithoutExtension = libraryName.Trim();
        var extensionIndex = coordinateWithoutExtension.LastIndexOf('@');
        if (extensionIndex >= 0)
        {
            coordinateWithoutExtension = coordinateWithoutExtension[..extensionIndex];
        }

        var parts = coordinateWithoutExtension.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return coordinateWithoutExtension;
        }

        var conflictKey = $"{parts[0]}:{parts[1]}";
        if (parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[3]))
        {
            conflictKey += $":{parts[3]}";
        }

        return conflictKey;
    }

    private static void AddLibraries(
        ICollection<Library> mergedLibraries,
        ISet<string> seenKeys,
        IEnumerable<Library>? libraries)
    {
        if (libraries == null)
        {
            return;
        }

        foreach (var library in libraries)
        {
            var conflictKey = GetLibraryConflictKey(library);
            if (string.IsNullOrEmpty(conflictKey) || !seenKeys.Add(conflictKey))
            {
                continue;
            }

            mergedLibraries.Add(library);
        }
    }
}