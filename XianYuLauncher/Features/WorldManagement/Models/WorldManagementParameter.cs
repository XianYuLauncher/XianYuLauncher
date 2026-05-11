namespace XianYuLauncher.Features.WorldManagement.Models;

public class WorldManagementParameter
{
    public string WorldPath { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    public bool IsEmbeddedHostNavigation { get; set; }
    public string BreadcrumbRootLabel { get; set; } = string.Empty;
    public XianYuLauncher.Shared.Models.LocalNavigationTarget? BreadcrumbRootTarget { get; set; }
}
