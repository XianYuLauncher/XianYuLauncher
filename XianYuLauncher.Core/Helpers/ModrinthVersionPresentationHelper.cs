namespace XianYuLauncher.Core.Helpers;

public static class ModrinthVersionPresentationHelper
{
    public static List<string> SelectRepresentativeGameVersions(IReadOnlyList<string>? versions, int limit)
    {
        if (versions == null || versions.Count == 0 || limit <= 0)
        {
            return [];
        }

        var normalizedVersions = versions
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedVersions.Count <= limit)
        {
            return normalizedVersions;
        }

        return normalizedVersions
            .Skip(normalizedVersions.Count - limit)
            .ToList();
    }
}