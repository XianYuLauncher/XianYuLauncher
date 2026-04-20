namespace XianYuLauncher.Shared.Models;

public sealed class LocalNavigationTarget
{
    public string RouteKey { get; init; } = string.Empty;

    public object? Parameter { get; init; }

    public bool HasTarget => !string.IsNullOrWhiteSpace(RouteKey);
}