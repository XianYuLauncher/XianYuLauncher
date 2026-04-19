namespace XianYuLauncher.Shared.Models;

public class PageNavigationTarget
{
    public string PageKey { get; init; } = string.Empty;

    public object? Parameter { get; init; }
}