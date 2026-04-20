using System.Collections.Generic;

using XianYuLauncher.Core.Contracts.Navigation;

namespace XianYuLauncher.Core.Helpers;

public static class LocalBreadcrumbNavigationPlanner
{
    public static T? FindLocalRootBreadcrumb<T>(IReadOnlyList<T> breadcrumbItems)
        where T : class, ILocalBreadcrumbNavigationItem
    {
        if (breadcrumbItems.Count == 0)
        {
            return null;
        }

        var currentIndex = GetCurrentBreadcrumbIndex(breadcrumbItems);
        for (var index = 0; index < currentIndex; index++)
        {
            if (!breadcrumbItems[index].HasLocalNavigationTarget)
            {
                continue;
            }

            return breadcrumbItems[index];
        }

        return null;
    }

    public static T? FindPreviousLocalBreadcrumb<T>(IReadOnlyList<T> breadcrumbItems)
        where T : class, ILocalBreadcrumbNavigationItem
    {
        if (breadcrumbItems.Count == 0)
        {
            return null;
        }

        var currentIndex = GetCurrentBreadcrumbIndex(breadcrumbItems);
        for (var index = currentIndex - 1; index >= 0; index--)
        {
            if (!breadcrumbItems[index].HasLocalNavigationTarget)
            {
                continue;
            }

            return breadcrumbItems[index];
        }

        return null;
    }

    public static bool TryCreateBackPlan<T>(
        IReadOnlyList<T> breadcrumbItems,
        T breadcrumbItem,
        out int backSteps,
        out bool destinationIsLocalRoot)
        where T : class, ILocalBreadcrumbNavigationItem
    {
        backSteps = 0;
        destinationIsLocalRoot = false;

        if (breadcrumbItems.Count == 0 || !breadcrumbItem.HasLocalNavigationTarget)
        {
            return false;
        }

        var currentIndex = GetCurrentBreadcrumbIndex(breadcrumbItems);
        var targetIndex = IndexOfBreadcrumbItem(breadcrumbItems, breadcrumbItem);
        if (targetIndex < 0 || targetIndex >= currentIndex)
        {
            return false;
        }

        backSteps = currentIndex - targetIndex;
        destinationIsLocalRoot = IsLocalRootBreadcrumbIndex(breadcrumbItems, targetIndex);
        return backSteps > 0;
    }

    private static int GetCurrentBreadcrumbIndex<T>(IReadOnlyList<T> breadcrumbItems)
        where T : class, ILocalBreadcrumbNavigationItem
    {
        for (var index = breadcrumbItems.Count - 1; index >= 0; index--)
        {
            if (breadcrumbItems[index].IsCurrent)
            {
                return index;
            }
        }

        return breadcrumbItems.Count - 1;
    }

    private static int IndexOfBreadcrumbItem<T>(IReadOnlyList<T> breadcrumbItems, T breadcrumbItem)
        where T : class, ILocalBreadcrumbNavigationItem
    {
        for (var index = 0; index < breadcrumbItems.Count; index++)
        {
            if (ReferenceEquals(breadcrumbItems[index], breadcrumbItem))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsLocalRootBreadcrumbIndex<T>(IReadOnlyList<T> breadcrumbItems, int targetIndex)
        where T : class, ILocalBreadcrumbNavigationItem
    {
        for (var index = targetIndex - 1; index >= 0; index--)
        {
            if (breadcrumbItems[index].HasLocalNavigationTarget)
            {
                return false;
            }
        }

        return true;
    }
}