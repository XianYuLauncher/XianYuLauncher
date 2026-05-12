using System;
using System.Collections.Generic;
using System.Linq;

using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Models;

public sealed class ErrorAnalysisNavigationParameter
{
    private BreadcrumbNavigationRoot _breadcrumbRoot = BreadcrumbNavigationRoot.Empty;

    public static ErrorAnalysisNavigationParameter CreateCrashPayload(
        string launchCommand,
        IReadOnlyList<string>? gameOutput,
        IReadOnlyList<string>? gameError)
    {
        return new ErrorAnalysisNavigationParameter().WithCrashPayload(launchCommand, gameOutput, gameError);
    }

    public static ErrorAnalysisNavigationParameter CreateWithGlobalBreadcrumbRoot(
        string breadcrumbRootLabel,
        string breadcrumbRootPageKey,
        object? breadcrumbRootNavigationParameter = null)
    {
        return new ErrorAnalysisNavigationParameter
        {
            BreadcrumbRoot = BreadcrumbNavigationRoot.CreateGlobal(
                breadcrumbRootLabel,
                breadcrumbRootPageKey,
                breadcrumbRootNavigationParameter),
        };
    }

    public string LaunchCommand { get; init; } = string.Empty;

    public IReadOnlyList<string> GameOutput { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> GameError { get; init; } = Array.Empty<string>();

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

    public bool HasLogPayload => !string.IsNullOrWhiteSpace(LaunchCommand)
        || GameOutput.Count > 0
        || GameError.Count > 0;

    public ErrorAnalysisNavigationParameter WithCrashPayload(
        string launchCommand,
        IReadOnlyList<string>? gameOutput,
        IReadOnlyList<string>? gameError)
    {
        return new ErrorAnalysisNavigationParameter
        {
            LaunchCommand = launchCommand ?? string.Empty,
            GameOutput = CloneLogs(gameOutput),
            GameError = CloneLogs(gameError),
            BreadcrumbRoot = BreadcrumbRoot,
        };
    }

    private static IReadOnlyList<string> CloneLogs(IReadOnlyList<string>? logs)
    {
        return logs?.ToArray() ?? Array.Empty<string>();
    }
}