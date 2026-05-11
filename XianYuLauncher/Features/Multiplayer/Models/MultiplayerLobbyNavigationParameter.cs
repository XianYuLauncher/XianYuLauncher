using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.Multiplayer.Models;

public sealed class MultiplayerLobbyNavigationParameter
{
    public string RoomId { get; init; } = string.Empty;

    public string? Port { get; init; }

    public bool IsGuest { get; init; }

    public string Url { get; init; } = string.Empty;

    public string BreadcrumbRootLabel { get; init; } = string.Empty;

    public LocalNavigationTarget? BreadcrumbRootTarget { get; init; }

    public bool HasBreadcrumbRoot => !string.IsNullOrWhiteSpace(BreadcrumbRootLabel)
        && (BreadcrumbRootTarget?.HasTarget ?? false);
}

public static class MultiplayerNavigationRouteKeys
{
    public const string Root = "MultiplayerRoot";
}