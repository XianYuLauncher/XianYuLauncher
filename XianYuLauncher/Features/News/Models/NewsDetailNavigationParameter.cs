using XianYuLauncher.Services;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.News.Models;

public sealed class NewsListNavigationParameter
{
    public MinecraftNewsEntry? InitialDetailEntry { get; init; }

    public string BreadcrumbRootLabel { get; init; } = string.Empty;

    public string? BreadcrumbRootPageKey { get; init; }

    public object? BreadcrumbRootNavigationParameter { get; init; }

    public bool HasBreadcrumbRoot => !string.IsNullOrWhiteSpace(BreadcrumbRootLabel)
        && !string.IsNullOrWhiteSpace(BreadcrumbRootPageKey);
}

public sealed class NewsDetailNavigationParameter
{
    public required MinecraftNewsEntry Entry { get; init; }

    public string GlobalBreadcrumbRootLabel { get; init; } = string.Empty;

    public string? GlobalBreadcrumbRootPageKey { get; init; }

    public object? GlobalBreadcrumbRootNavigationParameter { get; init; }

    public string BreadcrumbRootLabel { get; init; } = string.Empty;

    public LocalNavigationTarget? BreadcrumbRootTarget { get; init; }

    public bool HasGlobalBreadcrumbRoot => !string.IsNullOrWhiteSpace(GlobalBreadcrumbRootLabel)
        && !string.IsNullOrWhiteSpace(GlobalBreadcrumbRootPageKey);

    public bool HasBreadcrumbRoot => !string.IsNullOrWhiteSpace(BreadcrumbRootLabel)
        && (BreadcrumbRootTarget?.HasTarget ?? false);
}

public static class NewsNavigationRouteKeys
{
    public const string Root = "NewsListRoot";
}