using XianYuLauncher.Core.Models;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.Accounts.Models;

public sealed class CharacterManagementNavigationParameter
{
    public required MinecraftProfile Profile { get; init; }

    public string BreadcrumbRootLabel { get; init; } = string.Empty;

    public LocalNavigationTarget? BreadcrumbRootTarget { get; init; }

    public bool HasBreadcrumbRoot => !string.IsNullOrWhiteSpace(BreadcrumbRootLabel)
        && (BreadcrumbRootTarget?.HasTarget ?? false);
}

public static class CharacterNavigationRouteKeys
{
    public const string Root = "CharacterRoot";
}