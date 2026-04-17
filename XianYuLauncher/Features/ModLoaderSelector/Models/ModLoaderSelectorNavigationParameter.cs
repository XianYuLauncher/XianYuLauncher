namespace XianYuLauncher.Features.ModLoaderSelector.Models;

public sealed class ModLoaderSelectorNavigationParameter
{
    public string VersionId { get; init; } = string.Empty;

    public string BreadcrumbRootLabel { get; init; } = string.Empty;

    public string ReturnPageKey { get; init; } = string.Empty;

    public string ReturnTabKey { get; init; } = "version";
}