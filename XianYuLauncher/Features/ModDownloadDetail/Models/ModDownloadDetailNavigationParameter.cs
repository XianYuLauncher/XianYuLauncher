using System;
using System.Collections.Generic;

using XianYuLauncher.Core.Models;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.ModDownloadDetail.Models;

public sealed class ModDownloadDetailNavigationParameter
{
    private BreadcrumbNavigationRoot _breadcrumbRoot = BreadcrumbNavigationRoot.Empty;

    public static ModDownloadDetailNavigationParameter CreateWithGlobalBreadcrumbRoot(
        ModrinthProject project,
        string breadcrumbRootLabel,
        string breadcrumbRootPageKey,
        object? breadcrumbRootNavigationParameter = null,
        string? sourceType = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        var projectId = RequireNonEmpty(project.ProjectId, nameof(project.ProjectId));
        var rootLabel = RequireNonEmpty(breadcrumbRootLabel, nameof(breadcrumbRootLabel));
        var rootPageKey = RequireNonEmpty(breadcrumbRootPageKey, nameof(breadcrumbRootPageKey));

        return new ModDownloadDetailNavigationParameter
        {
            ProjectId = projectId,
            Project = project,
            DisplayTitleHint = project.DisplayTitle ?? string.Empty,
            SourceType = sourceType,
            BreadcrumbRoot = BreadcrumbNavigationRoot.CreateGlobal(rootLabel, rootPageKey, breadcrumbRootNavigationParameter),
        };
    }

    public string ProjectId { get; init; } = string.Empty;

    public ModrinthProject? Project { get; init; }

    public string DisplayTitleHint { get; init; } = string.Empty;

    public string? SourceType { get; init; }

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

    public LocalNavigationTarget? BreadcrumbRootTarget
    {
        get => _breadcrumbRoot.LocalTarget;
        init => _breadcrumbRoot = _breadcrumbRoot with { LocalTarget = value };
    }

    public IReadOnlyList<ModDownloadDetailBreadcrumbSegment> LocalBreadcrumbTrail { get; init; } = Array.Empty<ModDownloadDetailBreadcrumbSegment>();

    public bool HasBreadcrumbRoot => _breadcrumbRoot.HasBreadcrumb;

    private static string RequireNonEmpty(string? value, string paramName)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new ArgumentException("导航参数缺少必需的非空字符串值。", paramName);
    }

    public ModDownloadDetailNavigationParameter WithProjectId(string projectId)
    {
        return new ModDownloadDetailNavigationParameter
        {
            ProjectId = projectId,
            Project = Project,
            DisplayTitleHint = DisplayTitleHint,
            SourceType = SourceType,
            BreadcrumbRoot = BreadcrumbRoot,
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
            BreadcrumbRoot = BreadcrumbRoot,
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