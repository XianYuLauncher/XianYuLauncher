using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XianYuLauncher.Core.Helpers;

public sealed class AgentMinecraftPathEntry
{
    public string PathId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public sealed class AgentMinecraftPathSelectionResult
{
    public string CurrentPathId { get; init; } = string.Empty;

    public string CurrentPathName { get; init; } = string.Empty;

    public string CurrentPath { get; init; } = string.Empty;

    public string TargetPathId { get; init; } = string.Empty;

    public string TargetPathName { get; init; } = string.Empty;

    public string TargetPath { get; init; } = string.Empty;

    public bool TargetAlreadyActive { get; init; }

    public IReadOnlyList<AgentMinecraftPathEntry> Paths { get; init; } = [];

    public string UpdatedPathsJson { get; init; } = "[]";
}

public static class AgentMinecraftPathHelper
{
    private const string CurrentPathFallbackName = "当前活动目录";

    public static IReadOnlyList<AgentMinecraftPathEntry> NormalizePaths(string currentMinecraftPath, string? pathsJson)
    {
        var normalizedCurrentPath = NormalizePath(currentMinecraftPath);
        var storedPaths = TryDeserializeMinecraftPaths(pathsJson)
            .Where(path => !string.IsNullOrWhiteSpace(path.Path))
            .ToList();

        if (storedPaths.Count == 0)
        {
            return
            [
                new AgentMinecraftPathEntry
                {
                    PathId = "mcdir_1",
                    Name = CurrentPathFallbackName,
                    Path = normalizedCurrentPath,
                    IsActive = true
                }
            ];
        }

        if (!string.IsNullOrWhiteSpace(normalizedCurrentPath)
            && storedPaths.All(path => !PathEquals(path.Path, normalizedCurrentPath)))
        {
            storedPaths.Insert(0, new StoredMinecraftPath
            {
                Name = CurrentPathFallbackName,
                Path = normalizedCurrentPath,
                IsActive = true
            });
        }

        var activePath = ResolveActivePath(storedPaths, normalizedCurrentPath);

        return storedPaths
            .Select((path, index) => new AgentMinecraftPathEntry
            {
                PathId = $"mcdir_{index + 1}",
                Name = string.IsNullOrWhiteSpace(path.Name) ? $"Minecraft 目录 {index + 1}" : path.Name.Trim(),
                Path = NormalizePath(path.Path),
                IsActive = PathEquals(path.Path, activePath)
            })
            .ToList();
    }

    public static bool TryResolveSelection(
        string currentMinecraftPath,
        string? pathsJson,
        string? requestedPathId,
        string? requestedPath,
        out AgentMinecraftPathSelectionResult? result,
        out string errorMessage)
    {
        result = null;
        errorMessage = string.Empty;

        var normalizedRequestedPathId = requestedPathId?.Trim();
        var normalizedRequestedPath = NormalizePath(requestedPath);
        if (string.IsNullOrWhiteSpace(normalizedRequestedPathId) && string.IsNullOrWhiteSpace(normalizedRequestedPath))
        {
            errorMessage = "请提供 pathId 或 path。建议先调用 getMinecraftPaths，再使用返回的 path_id。";
            return false;
        }

        var normalizedPaths = NormalizePaths(currentMinecraftPath, pathsJson);
        var currentEntry = normalizedPaths.First(path => path.IsActive);

        AgentMinecraftPathEntry? pathEntryById = null;
        if (!string.IsNullOrWhiteSpace(normalizedRequestedPathId))
        {
            pathEntryById = normalizedPaths.FirstOrDefault(path =>
                string.Equals(path.PathId, normalizedRequestedPathId, StringComparison.OrdinalIgnoreCase));
        }

        AgentMinecraftPathEntry? pathEntryByPath = null;
        if (!string.IsNullOrWhiteSpace(normalizedRequestedPath))
        {
            pathEntryByPath = normalizedPaths.FirstOrDefault(path => PathEquals(path.Path, normalizedRequestedPath));
        }

        if (pathEntryById != null && pathEntryByPath != null && !PathEquals(pathEntryById.Path, pathEntryByPath.Path))
        {
            errorMessage = $"pathId \"{normalizedRequestedPathId}\" 与 path \"{normalizedRequestedPath}\" 指向不同目录，请只保留一个，或确保两者一致。";
            return false;
        }

        var targetEntry = pathEntryById ?? pathEntryByPath;
        if (targetEntry is null)
        {
            errorMessage = !string.IsNullOrWhiteSpace(normalizedRequestedPathId)
                ? $"未找到目录 ID \"{normalizedRequestedPathId}\"。请先调用 getMinecraftPaths 并使用返回的 path_id。"
                : $"未在已保存的 Minecraft 目录列表中找到路径：{normalizedRequestedPath}";
            return false;
        }

        result = new AgentMinecraftPathSelectionResult
        {
            CurrentPathId = currentEntry.PathId,
            CurrentPathName = currentEntry.Name,
            CurrentPath = currentEntry.Path,
            TargetPathId = targetEntry.PathId,
            TargetPathName = targetEntry.Name,
            TargetPath = targetEntry.Path,
            TargetAlreadyActive = PathEquals(currentEntry.Path, targetEntry.Path),
            Paths = normalizedPaths,
            UpdatedPathsJson = BuildUpdatedPathsJson(normalizedPaths, targetEntry.Path)
        };

        return true;
    }

    private static string ResolveActivePath(IReadOnlyList<StoredMinecraftPath> storedPaths, string normalizedCurrentPath)
    {
        var currentPathMatch = !string.IsNullOrWhiteSpace(normalizedCurrentPath)
            ? storedPaths.FirstOrDefault(path => PathEquals(path.Path, normalizedCurrentPath))
            : null;

        if (currentPathMatch != null)
        {
            return NormalizePath(currentPathMatch.Path);
        }

        var storedActivePath = storedPaths.FirstOrDefault(path => path.IsActive);
        if (storedActivePath != null)
        {
            return NormalizePath(storedActivePath.Path);
        }

        return NormalizePath(storedPaths[0].Path);
    }

    private static string BuildUpdatedPathsJson(IReadOnlyList<AgentMinecraftPathEntry> normalizedPaths, string targetPath)
    {
        var storedPaths = normalizedPaths
            .Select(path => new StoredMinecraftPath
            {
                Name = path.Name,
                Path = path.Path,
                IsActive = PathEquals(path.Path, targetPath)
            })
            .ToList();

        return JsonConvert.SerializeObject(storedPaths, Formatting.None);
    }

    private static IReadOnlyList<StoredMinecraftPath> TryDeserializeMinecraftPaths(string? pathsJson)
    {
        if (string.IsNullOrWhiteSpace(pathsJson))
        {
            return [];
        }

        try
        {
            var token = JToken.Parse(pathsJson);
            if (token.Type == JTokenType.String)
            {
                var nestedJson = token.Value<string>();
                if (string.IsNullOrWhiteSpace(nestedJson))
                {
                    return [];
                }

                token = JToken.Parse(nestedJson);
            }

            return token.Type == JTokenType.Array
                ? token.ToObject<List<StoredMinecraftPath>>() ?? []
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool PathEquals(string? left, string? right)
    {
        return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        return trimmed.Length > 3
            ? trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : trimmed;
    }

    private sealed class StoredMinecraftPath
    {
        public string Name { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}