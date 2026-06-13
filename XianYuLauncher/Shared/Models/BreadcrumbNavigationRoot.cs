namespace XianYuLauncher.Shared.Models;

public sealed record BreadcrumbNavigationRoot
{
    public static BreadcrumbNavigationRoot Empty { get; } = new();

    public string Label { get; init; } = string.Empty;

    public string? PageKey { get; init; }

    public object? NavigationParameter { get; init; }

    public LocalNavigationTarget? LocalTarget { get; init; }

    public bool HasLabel => !string.IsNullOrWhiteSpace(Label);

    public bool HasGlobalNavigationTarget => !string.IsNullOrWhiteSpace(PageKey);

    public bool HasLocalNavigationTarget => LocalTarget?.HasTarget == true;

    public bool HasTarget => HasGlobalNavigationTarget || HasLocalNavigationTarget;

    public bool HasBreadcrumb => HasLabel && HasTarget;

    public static BreadcrumbNavigationRoot CreateGlobal(string label, string pageKey, object? navigationParameter = null)
    {
        return new BreadcrumbNavigationRoot
        {
            Label = RequireNonEmpty(label, nameof(label)),
            PageKey = RequireNonEmpty(pageKey, nameof(pageKey)),
            NavigationParameter = navigationParameter,
        };
    }

    public static BreadcrumbNavigationRoot CreateLocal(string label, LocalNavigationTarget localTarget)
    {
        ArgumentNullException.ThrowIfNull(localTarget);

        if (!localTarget.HasTarget)
        {
            throw new ArgumentException("导航参数缺少有效的本地导航目标。", nameof(localTarget));
        }

        return new BreadcrumbNavigationRoot
        {
            Label = RequireNonEmpty(label, nameof(label)),
            LocalTarget = localTarget,
        };
    }

    public NavigationBreadcrumbItem ToBreadcrumbItem(bool isCurrent = false, bool isInteractiveCurrent = false)
    {
        return new NavigationBreadcrumbItem
        {
            DisplayText = Label,
            PageKey = PageKey,
            NavigationParameter = NavigationParameter,
            LocalNavigationTarget = LocalTarget,
            IsCurrent = isCurrent,
            IsInteractiveCurrent = isInteractiveCurrent,
        };
    }

    public bool MatchesGlobalNavigationTarget(NavigationBreadcrumbItem breadcrumbItem)
    {
        ArgumentNullException.ThrowIfNull(breadcrumbItem);

        return HasGlobalNavigationTarget
            && breadcrumbItem.HasGlobalNavigationTarget
            && string.Equals(PageKey, breadcrumbItem.PageKey, StringComparison.Ordinal);
    }

    private static string RequireNonEmpty(string? value, string paramName)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new ArgumentException("导航参数缺少必需的非空字符串值。", paramName);
    }
}