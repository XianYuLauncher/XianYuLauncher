using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

internal static class AgentOperationFollowUpMessageHelper
{
    public static string BuildSleepThenStatusHint(bool mentionTeachingTip = false)
    {
        string hint = $"如需复查，请先调用 sleep(seconds={AgentOperationPollingDefaults.DefaultSuggestedPollDelaySeconds}) 再用 getOperationStatus 查询状态，避免短时间重复轮询。";
        return mentionTeachingTip
            ? $"{hint} 下载队列 TeachingTip 也会同步显示。"
            : hint;
    }

    public static string AppendOperationStatusHint(string message, string operationId, bool mentionTeachingTip = false)
    {
        return $"{message}\noperation_id: {operationId}\n{BuildSleepThenStatusHint(mentionTeachingTip)}";
    }
}