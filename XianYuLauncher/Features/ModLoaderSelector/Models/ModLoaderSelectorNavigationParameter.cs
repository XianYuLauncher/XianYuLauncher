using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.ModLoaderSelector.Models;

public sealed class ModLoaderSelectorNavigationParameter
{
    public string VersionId { get; init; } = string.Empty;

    public string BreadcrumbRootLabel { get; init; } = string.Empty;

    public LocalNavigationTarget BreadcrumbRootTarget { get; init; } = new();
}