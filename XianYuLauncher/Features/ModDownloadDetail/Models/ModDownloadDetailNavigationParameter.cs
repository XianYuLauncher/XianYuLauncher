using System;
using System.Collections.Generic;

using XianYuLauncher.Core.Models;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.ModDownloadDetail.Models;

public sealed class ModDownloadDetailNavigationParameter
{
    public static ModDownloadDetailNavigationParameter CreateWithGlobalBreadcrumbRoot(
        ModrinthProject project,
        string breadcrumbRootLabel,
        string breadcrumbRootPageKey,
        object? breadcrumbRootNavigationParameter = null,
        string? sourceType = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        return new ModDownloadDetailNavigationParameter
        {
            ProjectId = project.ProjectId,
            Project = project,
            DisplayTitleHint = project.DisplayTitle,
            SourceType = sourceType,
            BreadcrumbRootLabel = breadcrumbRootLabel,
            BreadcrumbRootPageKey = breadcrumbRootPageKey,
            BreadcrumbRootNavigationParameter = breadcrumbRootNavigationParameter,
        };
    }

    public string ProjectId { get; init; } = string.Empty;

    public ModrinthProject? Project { get; init; }

    public string DisplayTitleHint { get; init; } = string.Empty;

    public string? SourceType { get; init; }

    public string BreadcrumbRootLabel { get; init; } = string.Empty;

    public string? BreadcrumbRootPageKey { get; init; }

    public object? BreadcrumbRootNavigationParameter { get; init; }

    public LocalNavigationTarget? BreadcrumbRootTarget { get; init; }

    public IReadOnlyList<ModDownloadDetailBreadcrumbSegment> LocalBreadcrumbTrail { get; init; } = Array.Empty<ModDownloadDetailBreadcrumbSegment>();

    public bool HasBreadcrumbRoot => !string.IsNullOrWhiteSpace(BreadcrumbRootLabel)
        && ((BreadcrumbRootTarget?.HasTarget ?? false) || !string.IsNullOrWhiteSpace(BreadcrumbRootPageKey));

    public ModDownloadDetailNavigationParameter WithProjectId(string projectId)
    {
        return new ModDownloadDetailNavigationParameter
        {
            ProjectId = projectId,
            Project = Project,
            DisplayTitleHint = DisplayTitleHint,
            SourceType = SourceType,
            BreadcrumbRootLabel = BreadcrumbRootLabel,
            BreadcrumbRootPageKey = BreadcrumbRootPageKey,
            BreadcrumbRootNavigationParameter = BreadcrumbRootNavigationParameter,
            BreadcrumbRootTarget = BreadcrumbRootTarget,
            LocalBreadcrumbTrail = LocalBreadcrumbTrail,
        };
    }

    public ModDownloadDetailNavigationParameter CreateChildParameter(string currentDisplayText, string nextProjectId, string? nextDisplayTitle = null)
    {
        var breadcrumbTrail = new List<ModDownloadDetailBreadcrumbSegment>(LocalBreadcrumbTrail)
        {
            new()
            {
                DisplayText = string.IsNullOrWhiteSpace(currentDisplayText)
                    ? (string.IsNullOrWhiteSpace(DisplayTitleHint) ? ProjectId : DisplayTitleHint)
                    : currentDisplayText,
            }
        };

        return new ModDownloadDetailNavigationParameter
        {
            ProjectId = nextProjectId,
            DisplayTitleHint = nextDisplayTitle ?? string.Empty,
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