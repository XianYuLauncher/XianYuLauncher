namespace XianYuLauncher.Features.WorldManagement.Models;

public class WorldManagementParameter
{
    private XianYuLauncher.Shared.Models.BreadcrumbNavigationRoot _breadcrumbRoot = XianYuLauncher.Shared.Models.BreadcrumbNavigationRoot.Empty;

    public string WorldPath { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    public bool IsEmbeddedHostNavigation { get; set; }

    public XianYuLauncher.Shared.Models.BreadcrumbNavigationRoot BreadcrumbRoot
    {
        get => _breadcrumbRoot;
        set => _breadcrumbRoot = value ?? XianYuLauncher.Shared.Models.BreadcrumbNavigationRoot.Empty;
    }

    public string BreadcrumbRootLabel
    {
        get => _breadcrumbRoot.Label;
        set => _breadcrumbRoot = _breadcrumbRoot with { Label = value ?? string.Empty };
    }

    public XianYuLauncher.Shared.Models.LocalNavigationTarget? BreadcrumbRootTarget
    {
        get => _breadcrumbRoot.LocalTarget;
        set => _breadcrumbRoot = _breadcrumbRoot with { LocalTarget = value };
    }

    public bool HasBreadcrumbRoot => _breadcrumbRoot.HasLabel && _breadcrumbRoot.HasLocalNavigationTarget;
}
