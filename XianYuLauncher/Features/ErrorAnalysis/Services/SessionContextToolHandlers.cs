using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed class GetLaunchContextToolHandler : IAgentToolHandler
{
    private readonly IErrorAnalysisSessionContextQueryService _sessionContextQueryService;

    public GetLaunchContextToolHandler(IErrorAnalysisSessionContextQueryService sessionContextQueryService)
    {
        _sessionContextQueryService = sessionContextQueryService;
    }

    public string ToolName => "getLaunchContext";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "返回当前错误分析会话的已脱敏启动参数。include_classpath 可选，默认 false；为 false 时会省略 raw classpath（-cp/-classpath）。",
        new
        {
            type = "object",
            properties = new
            {
                include_classpath = new { type = "boolean", description = "可选，默认 false。true 时保留 raw classpath；仅在日志明确指向类加载或缺库问题时再开启。" }
            },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var includeClasspath = arguments.Value<bool?>("include_classpath") ?? false;
        var message = await _sessionContextQueryService.GetLaunchContextAsync(context, includeClasspath, cancellationToken);
        return AgentToolExecutionResult.FromMessage(message);
    }
}

public sealed class GetLogTailToolHandler : IAgentToolHandler
{
    private readonly IErrorAnalysisSessionContextQueryService _sessionContextQueryService;

    public GetLogTailToolHandler(IErrorAnalysisSessionContextQueryService sessionContextQueryService)
    {
        _sessionContextQueryService = sessionContextQueryService;
    }

    public string ToolName => "getLogTail";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "返回当前错误分析会话已脱敏日志的尾部片段。默认返回最近 2000 个字符。",
        new
        {
            type = "object",
            properties = new
            {
                max_chars = new { type = "integer", description = "可选，默认 2000，最大 12000。" }
            },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var maxChars = arguments.Value<int?>("max_chars") ?? 2000;
        var message = await _sessionContextQueryService.GetLogTailAsync(context, maxChars, cancellationToken);
        return AgentToolExecutionResult.FromMessage(message);
    }
}

public sealed class GetLogChunkToolHandler : IAgentToolHandler
{
    private readonly IErrorAnalysisSessionContextQueryService _sessionContextQueryService;

    public GetLogChunkToolHandler(IErrorAnalysisSessionContextQueryService sessionContextQueryService)
    {
        _sessionContextQueryService = sessionContextQueryService;
    }

    public string ToolName => "getLogChunk";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "返回当前错误分析会话已脱敏日志的指定字符区间。start_offset 为 0-based 字符偏移。",
        new
        {
            type = "object",
            properties = new
            {
                start_offset = new { type = "integer", description = "必填。0-based 字符偏移。" },
                max_chars = new { type = "integer", description = "可选，默认 2000，最大 12000。" }
            },
            required = new[] { "start_offset" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var startOffsetToken = arguments["start_offset"];
        if (startOffsetToken == null || startOffsetToken.Type == JTokenType.Null)
        {
            return AgentToolExecutionResult.FromMessage("缺少 start_offset 参数。");
        }

        var startOffsetRaw = startOffsetToken.Type == JTokenType.String
            ? startOffsetToken.Value<string>()
            : startOffsetToken.ToString(Formatting.None);
        if (!int.TryParse(startOffsetRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startOffset))
        {
            return AgentToolExecutionResult.FromMessage("start_offset 必须为整数。");
        }

        if (startOffset < 0)
        {
            return AgentToolExecutionResult.FromMessage("start_offset 不能小于 0。");
        }

        var maxChars = arguments.Value<int?>("max_chars") ?? 2000;
        var message = await _sessionContextQueryService.GetLogChunkAsync(context, startOffset, maxChars, cancellationToken);
        return AgentToolExecutionResult.FromMessage(message);
    }
}