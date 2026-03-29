using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

public sealed class AgentJavaInventoryEntry
{
    public string JavaId { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public int MajorVersion { get; init; }

    public string FullVersion { get; init; } = string.Empty;

    public bool IsJdk { get; init; }

    public bool Is64Bit { get; init; }

    public bool MatchesSelectedJavaPath { get; init; }
}

public static class AgentJavaInventoryHelper
{
    private const string JavaIdParameterNames = "java_id / selected_java_id";
    private const string JavaPathParameterNames = "java_path / selected_java_path";

    public static IReadOnlyList<AgentJavaInventoryEntry> NormalizeJavaVersions(
        string? selectedJavaPath,
        IReadOnlyList<JavaVersion> javaVersions)
    {
        var normalizedSelectedPath = NormalizePath(selectedJavaPath);

        return javaVersions
            .OrderByDescending(javaVersion => IsSelectedJavaPath(normalizedSelectedPath, javaVersion.Path))
            .ThenByDescending(javaVersion => javaVersion.MajorVersion)
            .ThenByDescending(javaVersion => javaVersion.IsJDK)
            .ThenBy(javaVersion => javaVersion.FullVersion, StringComparer.OrdinalIgnoreCase)
            .ThenBy(javaVersion => javaVersion.Path, StringComparer.OrdinalIgnoreCase)
            .Select((javaVersion, index) => new AgentJavaInventoryEntry
            {
                JavaId = $"java_{index + 1}",
                Path = NormalizePath(javaVersion.Path),
                MajorVersion = javaVersion.MajorVersion,
                FullVersion = javaVersion.FullVersion,
                IsJdk = javaVersion.IsJDK,
                Is64Bit = javaVersion.Is64Bit,
                MatchesSelectedJavaPath = IsSelectedJavaPath(normalizedSelectedPath, javaVersion.Path)
            })
            .ToList();
    }

    public static bool TryResolveJava(
        string? requestedJavaId,
        string? requestedJavaPath,
        IReadOnlyList<AgentJavaInventoryEntry> inventory,
        out AgentJavaInventoryEntry? javaEntry,
        out string errorMessage)
    {
        javaEntry = null;
        errorMessage = string.Empty;

        var normalizedRequestedJavaId = NormalizeText(requestedJavaId);
        var normalizedRequestedJavaPath = NormalizePath(requestedJavaPath);
        if (string.IsNullOrWhiteSpace(normalizedRequestedJavaId) && string.IsNullOrWhiteSpace(normalizedRequestedJavaPath))
        {
            errorMessage = $"请提供 {JavaIdParameterNames} 或 {JavaPathParameterNames}。";
            return false;
        }

        AgentJavaInventoryEntry? javaEntryById = null;
        if (!string.IsNullOrWhiteSpace(normalizedRequestedJavaId))
        {
            javaEntryById = inventory.FirstOrDefault(entry =>
                string.Equals(entry.JavaId, normalizedRequestedJavaId, StringComparison.OrdinalIgnoreCase));
        }

        AgentJavaInventoryEntry? javaEntryByPath = null;
        if (!string.IsNullOrWhiteSpace(normalizedRequestedJavaPath))
        {
            javaEntryByPath = inventory.FirstOrDefault(entry =>
                string.Equals(entry.Path, normalizedRequestedJavaPath, StringComparison.OrdinalIgnoreCase));
        }

        if (javaEntryById != null && javaEntryByPath != null
            && !string.Equals(javaEntryById.Path, javaEntryByPath.Path, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = $"{JavaIdParameterNames} 中提供的值 \"{normalizedRequestedJavaId}\" 与 {JavaPathParameterNames} 中提供的值 \"{normalizedRequestedJavaPath}\" 指向不同 Java，请只保留一个，或确保两者一致。";
            return false;
        }

        javaEntry = javaEntryById ?? javaEntryByPath;
        if (javaEntry != null)
        {
            return true;
        }

        errorMessage = !string.IsNullOrWhiteSpace(normalizedRequestedJavaId)
            ? $"未找到 Java ID \"{normalizedRequestedJavaId}\"。请先调用 checkJavaVersions，并使用返回的 {JavaIdParameterNames}。"
            : $"未在当前 Java 清单中找到路径：{normalizedRequestedJavaPath}";
        return false;
    }

    private static bool IsSelectedJavaPath(string? selectedJavaPath, string javaPath)
    {
        return !string.IsNullOrWhiteSpace(selectedJavaPath)
            && string.Equals(selectedJavaPath, NormalizePath(javaPath), StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
    }
}