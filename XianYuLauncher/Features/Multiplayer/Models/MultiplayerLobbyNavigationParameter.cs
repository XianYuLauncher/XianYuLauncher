using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.Multiplayer.Models;

public sealed class MultiplayerLobbyNavigationParameter
{
    private BreadcrumbNavigationRoot _breadcrumbRoot = BreadcrumbNavigationRoot.Empty;

    public string RoomId { get; init; } = string.Empty;

    public string? Port { get; init; }

    public bool IsGuest { get; init; }

    public string Url { get; init; } = string.Empty;

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

public static class MultiplayerNavigationRouteKeys
{
    public const string Root = "MultiplayerRoot";
}