using System;
using System.Collections.Generic;
using System.Linq;

namespace XianYuLauncher.Features.ErrorAnalysis.Models;

public sealed class ErrorAnalysisNavigationParameter
{
    public static ErrorAnalysisNavigationParameter CreateWithGlobalBreadcrumbRoot(
        string breadcrumbRootLabel,
        string breadcrumbRootPageKey,
        object? breadcrumbRootNavigationParameter = null)
    {
        return new ErrorAnalysisNavigationParameter
        {
            BreadcrumbRootLabel = RequireNonEmpty(breadcrumbRootLabel, nameof(breadcrumbRootLabel)),
            BreadcrumbRootPageKey = RequireNonEmpty(breadcrumbRootPageKey, nameof(breadcrumbRootPageKey)),
            BreadcrumbRootNavigationParameter = breadcrumbRootNavigationParameter,
        };
    }

    public string LaunchCommand { get; init; } = string.Empty;

    public IReadOnlyList<string> GameOutput { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> GameError { get; init; } = Array.Empty<string>();

    public string BreadcrumbRootLabel { get; init; } = string.Empty;

    public string? BreadcrumbRootPageKey { get; init; }

    public object? BreadcrumbRootNavigationParameter { get; init; }

    public bool HasBreadcrumbRoot => !string.IsNullOrWhiteSpace(BreadcrumbRootLabel)
        && !string.IsNullOrWhiteSpace(BreadcrumbRootPageKey);

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
            BreadcrumbRootLabel = BreadcrumbRootLabel,
            BreadcrumbRootPageKey = BreadcrumbRootPageKey,
            BreadcrumbRootNavigationParameter = BreadcrumbRootNavigationParameter,
        };
    }

    private static string RequireNonEmpty(string? value, string paramName)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new ArgumentException("导航参数缺少必需的非空字符串值。", paramName);
    }

    private static IReadOnlyList<string> CloneLogs(IReadOnlyList<string>? logs)
    {
        return logs?.ToArray() ?? Array.Empty<string>();
    }
}