using XianYuLauncher.Features.VersionList.ViewModels;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.VersionManagement.Models;

public sealed class VersionManagementNavigationParameter
{
    private BreadcrumbNavigationRoot _breadcrumbRoot = BreadcrumbNavigationRoot.Empty;

    public required VersionListViewModel.VersionInfoItem Version { get; init; }

    public bool IsEmbeddedHostNavigation { get; init; }

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

    public LocalNavigationTarget? BreadcrumbRootTarget
    {
        get => _breadcrumbRoot.LocalTarget;
        init => _breadcrumbRoot = _breadcrumbRoot with { LocalTarget = value };
    }

    public bool HasBreadcrumbRoot => _breadcrumbRoot.HasLabel && _breadcrumbRoot.HasLocalNavigationTarget;
}