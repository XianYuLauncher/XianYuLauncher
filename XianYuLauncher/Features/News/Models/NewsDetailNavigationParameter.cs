using XianYuLauncher.Services;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.News.Models;

public sealed class NewsListNavigationParameter
{
    private BreadcrumbNavigationRoot _breadcrumbRoot = BreadcrumbNavigationRoot.Empty;

    public MinecraftNewsEntry? InitialDetailEntry { get; init; }

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

    public string? BreadcrumbRootPageKey
    {
        get => _breadcrumbRoot.PageKey;
        init => _breadcrumbRoot = _breadcrumbRoot with { PageKey = value };
    }

    public object? BreadcrumbRootNavigationParameter
    {
        get => _breadcrumbRoot.NavigationParameter;
        init => _breadcrumbRoot = _breadcrumbRoot with { NavigationParameter = value };
    }

    public bool HasBreadcrumbRoot => _breadcrumbRoot.HasLabel
        && _breadcrumbRoot.HasGlobalNavigationTarget;
}

public sealed class NewsDetailNavigationParameter
{
    private BreadcrumbNavigationRoot _globalBreadcrumbRoot = BreadcrumbNavigationRoot.Empty;
    private BreadcrumbNavigationRoot _breadcrumbRoot = BreadcrumbNavigationRoot.Empty;

    public required MinecraftNewsEntry Entry { get; init; }

    public BreadcrumbNavigationRoot GlobalBreadcrumbRoot
    {
        get => _globalBreadcrumbRoot;
        init => _globalBreadcrumbRoot = value ?? BreadcrumbNavigationRoot.Empty;
    }

    public string GlobalBreadcrumbRootLabel
    {
        get => _globalBreadcrumbRoot.Label;
        init => _globalBreadcrumbRoot = _globalBreadcrumbRoot with { Label = value ?? string.Empty };
    }

    public string? GlobalBreadcrumbRootPageKey
    {
        get => _globalBreadcrumbRoot.PageKey;
        init => _globalBreadcrumbRoot = _globalBreadcrumbRoot with { PageKey = value };
    }

    public object? GlobalBreadcrumbRootNavigationParameter
    {
        get => _globalBreadcrumbRoot.NavigationParameter;
        init => _globalBreadcrumbRoot = _globalBreadcrumbRoot with { NavigationParameter = value };
    }

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

    public bool HasGlobalBreadcrumbRoot => _globalBreadcrumbRoot.HasLabel
        && _globalBreadcrumbRoot.HasGlobalNavigationTarget;

    public bool HasBreadcrumbRoot => _breadcrumbRoot.HasLabel
        && _breadcrumbRoot.HasLocalNavigationTarget;
}

public static class NewsNavigationRouteKeys
{
    public const string Root = "NewsListRoot";
}