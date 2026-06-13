using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.UI.Xaml.Controls;

using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Helpers;

public readonly record struct HostedLocalNavigationBackPlan(int BackSteps, bool DestinationIsLocalRoot);

public sealed class HostedLocalNavigationCoordinator
{
    private readonly Frame _innerFrame;
    private readonly Func<IHostedLocalPage?> _getActiveHostedLocalPage;

    public HostedLocalNavigationCoordinator(Frame innerFrame, Func<IHostedLocalPage?> getActiveHostedLocalPage)
    {
        _innerFrame = innerFrame;
        _getActiveHostedLocalPage = getActiveHostedLocalPage;
    }

    public bool CanGoBackLocally => _getActiveHostedLocalPage() is not null && _innerFrame.CanGoBack;

    public bool TryGetLocalRootBreadcrumbItem([NotNullWhen(true)] out NavigationBreadcrumbItem? rootBreadcrumbItem)
    {
        if (!TryGetCurrentBreadcrumbItems(out var breadcrumbItems))
        {
            rootBreadcrumbItem = null;
            return false;
        }

        rootBreadcrumbItem = LocalBreadcrumbNavigationPlanner.FindLocalRootBreadcrumb(breadcrumbItems);
        return rootBreadcrumbItem is not null;
    }

    public bool TryGetPreviousLocalBreadcrumbItem([NotNullWhen(true)] out NavigationBreadcrumbItem? previousBreadcrumbItem)
    {
        if (!TryGetCurrentBreadcrumbItems(out var breadcrumbItems))
        {
            previousBreadcrumbItem = null;
            return false;
        }

        previousBreadcrumbItem = LocalBreadcrumbNavigationPlanner.FindPreviousLocalBreadcrumb(breadcrumbItems);
        return previousBreadcrumbItem is not null;
    }

    public bool TryGetBackPlan(NavigationBreadcrumbItem breadcrumbItem, out HostedLocalNavigationBackPlan backPlan)
    {
        backPlan = default;

        if (!breadcrumbItem.HasLocalNavigationTarget || !_innerFrame.CanGoBack || !TryGetCurrentBreadcrumbItems(out var breadcrumbItems))
        {
            return false;
        }

        if (!LocalBreadcrumbNavigationPlanner.TryCreateBackPlan(breadcrumbItems, breadcrumbItem, out var backSteps, out var destinationIsLocalRoot))
        {
            return false;
        }

        backPlan = new HostedLocalNavigationBackPlan(backSteps, destinationIsLocalRoot);
        return true;
    }

    public bool TryGetCurrentBreadcrumbItems([NotNullWhen(true)] out IReadOnlyList<NavigationBreadcrumbItem>? breadcrumbItems)
    {
        if (_getActiveHostedLocalPage() is not { } hostedLocalPage)
        {
            breadcrumbItems = null;
            return false;
        }

        breadcrumbItems = hostedLocalPage.HeaderSource.HeaderMetadata.BreadcrumbItems;
        return breadcrumbItems.Count > 0;
    }
}