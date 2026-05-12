using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.ModLoaderSelector.Models;

public sealed class ModLoaderSelectorNavigationParameter
{
    private BreadcrumbNavigationRoot _breadcrumbRoot = BreadcrumbNavigationRoot.Empty;

    public string VersionId { get; init; } = string.Empty;

    public BreadcrumbNavigationRoot BreadcrumbRoot
    {
        get => _breadcrumbRoot;
        init => _breadcrumbRoot = value ?? BreadcrumbNavigationRoot.Empty;
    }

    public string BreadcrumbRootLabel
    {
        get => _breadcrumbRoot.Label;
        init => _breadcrumbRoot = _breadcrumbRoot with { Label = value ?? string.Empty };
    }

    public LocalNavigationTarget BreadcrumbRootTarget
    {
        get => _breadcrumbRoot.LocalTarget ?? new LocalNavigationTarget();
        init => _breadcrumbRoot = _breadcrumbRoot with { LocalTarget = value };
    }

    public bool HasBreadcrumbRoot => _breadcrumbRoot.HasLabel && _breadcrumbRoot.HasLocalNavigationTarget;
}