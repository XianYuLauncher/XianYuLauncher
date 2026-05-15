using XianYuLauncher.Core.Models;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.Accounts.Models;

public sealed class AccountManagementNavigationParameter
{
    private BreadcrumbNavigationRoot _breadcrumbRoot = BreadcrumbNavigationRoot.Empty;

    public required MinecraftAccount Profile { get; init; }

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

    public bool HasBreadcrumbRoot => _breadcrumbRoot.HasLabel
        && _breadcrumbRoot.HasLocalNavigationTarget;
}

public static class AccountNavigationRouteKeys
{
    public const string Root = "CharacterRoot";
}