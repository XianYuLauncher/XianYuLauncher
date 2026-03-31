using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models;

namespace XianYuLauncher.Services;

public sealed class DownloadTaskPresentationService : IDownloadTaskPresentationService
{
    private const string DefaultIconGlyph = "\xE896";

    public DownloadTaskPresentation Resolve(DownloadTaskInfo taskInfo)
    {
        ArgumentNullException.ThrowIfNull(taskInfo);

        var displayName = ResolveDisplayName(taskInfo);
        var taskTypeResourceKey = ResolveTaskTypeResourceKey(taskInfo);

        return new DownloadTaskPresentation(
            displayName,
            ResolveStatusMessage(taskInfo, displayName),
            taskTypeResourceKey.GetLocalized(),
            ResolveIconGlyph(taskTypeResourceKey));
    }

    private static string ResolveDisplayName(DownloadTaskInfo taskInfo)
    {
        if (!string.IsNullOrWhiteSpace(taskInfo.DisplayNameResourceKey))
        {
            return taskInfo.DisplayNameResourceKey!.GetLocalized(ToLocalizedArguments(taskInfo.DisplayNameResourceArguments));
        }

        return string.IsNullOrWhiteSpace(taskInfo.TaskName) ? taskInfo.VersionName : taskInfo.TaskName;
    }

    private static string ResolveStatusMessage(DownloadTaskInfo taskInfo, string displayName)
    {
        if (taskInfo.State == DownloadTaskState.Queued && taskInfo.QueuePosition is int queuePosition)
        {
            return "DownloadQueue_Status_QueuedWithPosition".GetLocalized(queuePosition);
        }

        if (!string.IsNullOrWhiteSpace(taskInfo.StatusResourceKey))
        {
            return taskInfo.StatusResourceKey!.GetLocalized(ToStatusArguments(taskInfo, displayName));
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

    private static object[] ToStatusArguments(DownloadTaskInfo taskInfo, string displayName)
    {
        if (taskInfo.StatusResourceArguments.Length == 0)
        {
            return [];
        }

        var arguments = new object[taskInfo.StatusResourceArguments.Length];
        for (var index = 0; index < taskInfo.StatusResourceArguments.Length; index++)
        {
            arguments[index] = ShouldUseTaskDisplayName(taskInfo, index)
                ? displayName
                : taskInfo.StatusResourceArguments[index];
        }

        return arguments;
    }

    private static object[] ToLocalizedArguments(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return [];
        }

        var localizedArguments = new object[arguments.Count];
        for (var index = 0; index < arguments.Count; index++)
        {
            localizedArguments[index] = arguments[index];
        }

        return localizedArguments;
    }

    private static bool ShouldUseTaskDisplayName(DownloadTaskInfo taskInfo, int argumentIndex)
    {
        return argumentIndex == 0
            && !string.IsNullOrWhiteSpace(taskInfo.DisplayNameResourceKey)
            && taskInfo.StatusResourceKey is "DownloadQueue_Status_DownloadingNamed"
                or "DownloadQueue_Status_DownloadingNamedWithProgress"
                or "DownloadQueue_Status_NameWithProgress";
    }

    private static string ResolveTaskTypeResourceKey(DownloadTaskInfo taskInfo)
    {
        if (!string.IsNullOrWhiteSpace(taskInfo.TaskTypeResourceKey))
        {
            return taskInfo.TaskTypeResourceKey!;
        }

        return taskInfo.TaskCategory switch
        {
            DownloadTaskCategory.GameInstall => "DownloadQueue_TaskType_GameInstall",
            DownloadTaskCategory.ModDownload => "DownloadQueue_TaskType_ModDownload",
            DownloadTaskCategory.ResourcePackDownload => "DownloadQueue_TaskType_ResourcePackDownload",
            DownloadTaskCategory.ShaderDownload => "DownloadQueue_TaskType_ShaderDownload",
            DownloadTaskCategory.DataPackDownload => "DownloadQueue_TaskType_DataPackDownload",
            DownloadTaskCategory.WorldDownload => "DownloadQueue_TaskType_WorldDownload",
            DownloadTaskCategory.ModpackDownload => "DownloadQueue_TaskType_ModpackDownload",
            DownloadTaskCategory.ModpackInstallFile => "DownloadQueue_TaskType_ModpackInstallFile",
            DownloadTaskCategory.CommunityResourceUpdateBatch => "DownloadQueue_TaskType_CommunityResourceUpdateBatch",
            DownloadTaskCategory.CommunityResourceUpdateFile => "DownloadQueue_TaskType_CommunityResourceUpdateFile",
            DownloadTaskCategory.FileDownload => "DownloadQueue_TaskType_FileDownload",
            _ => "DownloadQueue_TaskType_Generic"
        };
    }

    private static string ResolveIconGlyph(string taskTypeResourceKey)
    {
        return taskTypeResourceKey switch
        {
            "DownloadQueue_TaskType_GameInstall" => "\xE7FC",
            "DownloadQueue_TaskType_ModpackDownload" => "\xE7B8",
            "DownloadQueue_TaskType_ModpackInstallFile" => "\xE8A5",
            "DownloadQueue_TaskType_CommunityResourceUpdateBatch" => "\xE72C",
            "DownloadQueue_TaskType_CommunityResourceUpdateFile" => "\xE8A5",
            _ => DefaultIconGlyph
        };
    }
}
