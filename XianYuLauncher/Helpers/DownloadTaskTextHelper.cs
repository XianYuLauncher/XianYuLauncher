using System;
using System.Collections.Generic;

using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Helpers;

public static class DownloadTaskTextHelper
{
    public static string GetLocalizedDisplayName(DownloadTaskInfo taskInfo)
    {
        ArgumentNullException.ThrowIfNull(taskInfo);

        var displayName = string.IsNullOrWhiteSpace(taskInfo.TaskName) ? taskInfo.VersionName : taskInfo.TaskName;
        return LocalizeDisplayName(displayName);
    }

    public static string GetLocalizedStatusMessage(DownloadTaskInfo taskInfo)
    {
        ArgumentNullException.ThrowIfNull(taskInfo);

        if (taskInfo.State == DownloadTaskState.Queued && taskInfo.QueuePosition is int queuePosition)
        {
            return "DownloadQueue_Status_QueuedWithPosition".GetLocalized(queuePosition);
        }

        if (!string.IsNullOrWhiteSpace(taskInfo.StatusResourceKey))
        {
            var localizedArguments = LocalizeStatusArguments(taskInfo.StatusResourceKey, taskInfo.StatusResourceArguments);
            return taskInfo.StatusResourceKey.GetLocalized(localizedArguments);
        }

        if (!string.IsNullOrWhiteSpace(taskInfo.StatusMessage))
        {
            return taskInfo.StatusMessage;
        }

        return taskInfo.State switch
        {
            DownloadTaskState.Queued => "DownloadQueue_Status_Waiting".GetLocalized(),
            DownloadTaskState.Downloading => "DownloadQueue_Status_Downloading".GetLocalized(),
            DownloadTaskState.Completed => "DownloadQueue_Status_Completed".GetLocalized(),
            DownloadTaskState.Failed => "DownloadQueue_Status_Failed".GetLocalized(),
            DownloadTaskState.Cancelled => "DownloadQueue_Status_Cancelled".GetLocalized(),
            _ => "DownloadQueue_Status_Generic".GetLocalized()
        };
    }

    private static object[] LocalizeStatusArguments(string resourceKey, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return Array.Empty<object>();
        }

        var localizedArguments = new object[arguments.Count];
        for (var index = 0; index < arguments.Count; index++)
        {
            localizedArguments[index] = ShouldLocalizeDisplayNameArgument(resourceKey, index)
                ? LocalizeDisplayName(arguments[index])
                : arguments[index];
        }

        return localizedArguments;
    }

    private static bool ShouldLocalizeDisplayNameArgument(string resourceKey, int argumentIndex)
    {
        return resourceKey switch
        {
            "DownloadQueue_Status_DownloadingNamed" => argumentIndex == 0,
            "DownloadQueue_Status_DownloadingNamedWithProgress" => argumentIndex == 0,
            "DownloadQueue_Status_NameWithProgress" => argumentIndex == 0,
            "DownloadQueue_Status_DownloadingDependency" => argumentIndex == 0,
            "DownloadQueue_Status_DownloadingDependencyWithProgress" => argumentIndex == 0,
            "DownloadQueue_Status_DownloadingDependencyResource" => argumentIndex == 0,
            "DownloadQueue_Status_ExtractingTo" => argumentIndex == 0,
            _ => false
        };
    }

    private static string LocalizeDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (string.Equals(value, "收藏夹导入", StringComparison.Ordinal)
            || string.Equals(value, "favorite-import", StringComparison.OrdinalIgnoreCase))
        {
            return "DownloadQueue_DisplayName_FavoriteImport".GetLocalized();
        }

        if (TryExtractSuffix(value, "客户端 ", out var clientVersion))
        {
            return "DownloadQueue_DisplayName_Client".GetLocalized(clientVersion);
        }

        if (TryExtractSuffix(value, "服务端 ", out var serverVersion))
        {
            return "DownloadQueue_DisplayName_Server".GetLocalized(serverVersion);
        }

        return value;
    }

    private static bool TryExtractSuffix(string value, string prefix, out string suffix)
    {
        suffix = string.Empty;
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        suffix = value[prefix.Length..].Trim();
        return suffix.Length > 0;
    }
}
