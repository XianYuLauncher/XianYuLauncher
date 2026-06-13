using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Helpers;

public static class BreadcrumbNavigationHelper
{
    public static bool ShouldGoBackToGlobalRoot(
        NavigationBreadcrumbItem breadcrumbItem,
        bool canGoBack,
        BreadcrumbNavigationRoot? expectedGlobalRoot = null,
        NavigationBreadcrumbItem? firstBreadcrumbItem = null,
        NavigationBreadcrumbItem? currentBreadcrumbItem = null)
    {
        ArgumentNullException.ThrowIfNull(breadcrumbItem);

        if (!canGoBack || !breadcrumbItem.HasGlobalNavigationTarget)
        {
            return false;
        }

        if (currentBreadcrumbItem is not null && ReferenceEquals(breadcrumbItem, currentBreadcrumbItem))
        {
            return false;
        }

        if (expectedGlobalRoot is not null && !expectedGlobalRoot.MatchesGlobalNavigationTarget(breadcrumbItem))
        {
            return false;
        }

        if (firstBreadcrumbItem is not null && !ReferenceEquals(breadcrumbItem, firstBreadcrumbItem))
        {
            return false;
        }

        return true;
    }
}