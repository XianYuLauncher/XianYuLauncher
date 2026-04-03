using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed class SleepToolHandler : IAgentToolHandler
{
    public string ToolName => "sleep";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        $"等待指定秒数后返回。仅用于长任务状态轮询，seconds 可选，整数，默认 {AgentOperationPollingDefaults.DefaultSuggestedPollDelaySeconds} 秒，范围 {AgentOperationPollingDefaults.MinSleepSeconds}-{AgentOperationPollingDefaults.MaxSleepSeconds} 秒。",
        new
        {
            type = "object",
            properties = new
            {
                seconds = new
                {
                    type = "integer",
                    description = $"可选。等待秒数，整数，范围 {AgentOperationPollingDefaults.MinSleepSeconds}-{AgentOperationPollingDefaults.MaxSleepSeconds}；省略时默认 {AgentOperationPollingDefaults.DefaultSuggestedPollDelaySeconds} 秒。"
                }
            },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        JToken? secondsToken = arguments["seconds"] ?? arguments["delay_seconds"];
        int? requestedSeconds = null;

        if (secondsToken != null && secondsToken.Type != JTokenType.Null)
        {
            if (!int.TryParse(secondsToken.ToString(), out int parsedSeconds))
            {
                return AgentToolExecutionResult.FromMessage("sleep 参数无效：seconds 必须是整数秒。");
            }

            requestedSeconds = parsedSeconds;
        }

        int effectiveSeconds = AgentOperationPollingDefaults.NormalizeSleepSeconds(requestedSeconds);
        await Task.Delay(TimeSpan.FromSeconds(effectiveSeconds), cancellationToken);

        string adjustmentMessage = requestedSeconds.HasValue && requestedSeconds.Value != effectiveSeconds
            ? $"（请求值 {requestedSeconds.Value} 已调整为 {effectiveSeconds} 秒）"
            : string.Empty;

        return AgentToolExecutionResult.FromMessage(
            $"已等待 {effectiveSeconds} 秒{adjustmentMessage}。如果之前的长任务仍在进行，现在可以调用 getOperationStatus 复查一次。");
    }
}