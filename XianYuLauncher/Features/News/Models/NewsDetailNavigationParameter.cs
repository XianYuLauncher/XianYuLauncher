using XianYuLauncher.Services;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.News.Models;

public sealed class NewsDetailNavigationParameter
{
    public required MinecraftNewsEntry Entry { get; init; }

    public string BreadcrumbRootLabel { get; init; } = string.Empty;

    public LocalNavigationTarget? BreadcrumbRootTarget { get; init; }

    public bool HasBreadcrumbRoot => !string.IsNullOrWhiteSpace(BreadcrumbRootLabel)
        && (BreadcrumbRootTarget?.HasTarget ?? false);
}

public static class NewsNavigationRouteKeys
{
    public const string Root = "NewsListRoot";
}