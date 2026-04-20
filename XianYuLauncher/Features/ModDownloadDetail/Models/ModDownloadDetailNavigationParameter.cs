using System;
using System.Collections.Generic;

using XianYuLauncher.Core.Models;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.ModDownloadDetail.Models;

public sealed class ModDownloadDetailNavigationParameter
{
    public string ProjectId { get; init; } = string.Empty;

    public ModrinthProject? Project { get; init; }

    public string? SourceType { get; init; }

    public string BreadcrumbRootLabel { get; init; } = string.Empty;

    public string? BreadcrumbRootPageKey { get; init; }

    public object? BreadcrumbRootNavigationParameter { get; init; }

    public LocalNavigationTarget? BreadcrumbRootTarget { get; init; }

    public IReadOnlyList<ModDownloadDetailBreadcrumbSegment> LocalBreadcrumbTrail { get; init; } = Array.Empty<ModDownloadDetailBreadcrumbSegment>();

    public bool HasBreadcrumbRoot => !string.IsNullOrWhiteSpace(BreadcrumbRootLabel)
        && ((BreadcrumbRootTarget?.HasTarget ?? false) || !string.IsNullOrWhiteSpace(BreadcrumbRootPageKey));

    public ModDownloadDetailNavigationParameter CreateChildParameter(string currentDisplayText, string nextProjectId)
    {
        var breadcrumbTrail = new List<ModDownloadDetailBreadcrumbSegment>(LocalBreadcrumbTrail)
        {
            new()
            {
                DisplayText = string.IsNullOrWhiteSpace(currentDisplayText) ? ProjectId : currentDisplayText,
            }
        };

        return new ModDownloadDetailNavigationParameter
        {
            ProjectId = nextProjectId,
            SourceType = SourceType,
            BreadcrumbRootLabel = BreadcrumbRootLabel,
            BreadcrumbRootPageKey = BreadcrumbRootPageKey,
            BreadcrumbRootNavigationParameter = BreadcrumbRootNavigationParameter,
            BreadcrumbRootTarget = BreadcrumbRootTarget,
            LocalBreadcrumbTrail = breadcrumbTrail,
        };
    }
}

public sealed class ModDownloadDetailBreadcrumbSegment
{
    public string DisplayText { get; init; } = string.Empty;
}

public sealed class ModDownloadDetailNavigationRequestedEventArgs : EventArgs
{
    public ModDownloadDetailNavigationRequestedEventArgs(ModDownloadDetailNavigationParameter navigationParameter)
    {
        NavigationParameter = navigationParameter;
    }

    public ModDownloadDetailNavigationParameter NavigationParameter { get; }
}