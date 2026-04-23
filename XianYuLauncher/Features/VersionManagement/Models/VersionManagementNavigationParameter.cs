using XianYuLauncher.Features.VersionList.ViewModels;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.VersionManagement.Models;

public sealed class VersionManagementNavigationParameter
{
    public required VersionListViewModel.VersionInfoItem Version { get; init; }

    public bool IsEmbeddedHostNavigation { get; init; }

    public string BreadcrumbRootLabel { get; init; } = string.Empty;

    public LocalNavigationTarget? BreadcrumbRootTarget { get; init; }
}