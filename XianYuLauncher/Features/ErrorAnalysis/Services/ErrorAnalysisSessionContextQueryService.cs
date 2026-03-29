using System.Text;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public interface IErrorAnalysisSessionContextQueryService
{
    Task<string> BuildCrashPromptAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken);

    Task<string> GetLaunchContextAsync(ErrorAnalysisSessionContext context, bool includeClasspath, CancellationToken cancellationToken);

    Task<string> GetLogTailAsync(ErrorAnalysisSessionContext context, int maxChars, CancellationToken cancellationToken);

    Task<string> GetLogChunkAsync(ErrorAnalysisSessionContext context, int startOffset, int maxChars, CancellationToken cancellationToken);
}

public sealed class ErrorAnalysisSessionContextQueryService : IErrorAnalysisSessionContextQueryService
{
    private const int DefaultPromptLogTailChars = 2000;
    private const int DefaultToolLogChars = 2000;
    private const int MaxToolLogChars = 12000;

    private readonly ILogSanitizerService _logSanitizerService;
    private readonly IVersionInfoService _versionInfoService;
    private readonly ILaunchSettingsResolver _launchSettingsResolver;

    public ErrorAnalysisSessionContextQueryService(
        ILogSanitizerService logSanitizerService,
        IVersionInfoService versionInfoService,
        ILaunchSettingsResolver launchSettingsResolver)
    {
        _logSanitizerService = logSanitizerService;
        _versionInfoService = versionInfoService;
        _launchSettingsResolver = launchSettingsResolver;
    }

    public async Task<string> BuildCrashPromptAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var launchSummary = await BuildLaunchSummaryAsync(context, cancellationToken);
        var sanitizedLog = await GetSanitizedLogAsync(context);
        var tail = AiContextFormattingHelper.GetTailSlice(sanitizedLog, DefaultPromptLogTailChars);

        StringBuilder builder = new();
        builder.AppendLine("游戏刚刚崩溃了。请先根据下面的启动摘要和日志尾部分析原因。");
        builder.AppendLine("如果证据不足，可继续查看启动上下文或查看更多日志片段；只有在日志明确指向类加载或缺库问题时，才需要查看包含 classpath 的完整启动参数。");
        builder.AppendLine();
        builder.AppendLine("=== 启动摘要 ===");
        builder.AppendLine(launchSummary);
        builder.AppendLine();
        builder.AppendLine(tail.WasTruncated
            ? $"=== 日志尾部（最近 {tail.Content.Length} 个字符 / 总长 {tail.TotalLength}，已截断） ==="
            : "=== 日志尾部 ===");
        builder.AppendLine(string.IsNullOrWhiteSpace(tail.Content) ? "当前会话没有可用日志。" : tail.Content);
        return builder.ToString().TrimEnd();
    }

    public async Task<string> GetLaunchContextAsync(ErrorAnalysisSessionContext context, bool includeClasspath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(context.LaunchCommand))
        {
            return "当前会话没有可用启动参数。";
        }

        var launchCommand = includeClasspath
            ? context.LaunchCommand
            : AiContextFormattingHelper.RemoveClassPathArguments(context.LaunchCommand, out _);
        var sanitizedLaunchCommand = await _logSanitizerService.SanitizeAsync(launchCommand);

        StringBuilder builder = new();
        builder.AppendLine($"version_id: {DisplayValue(context.VersionId)}");
        builder.AppendLine($"minecraft_path: {DisplayValue(context.MinecraftPath)}");
        builder.AppendLine($"include_classpath: {includeClasspath}");
        if (!includeClasspath)
        {
            builder.AppendLine("note: 当前默认省略 raw classpath；只有在日志明确指向类加载或缺库问题时，才建议改为 include_classpath=true。" );
        }

        builder.AppendLine("launch_command:");
        builder.Append(sanitizedLaunchCommand);
        return builder.ToString().TrimEnd();
    }

    public async Task<string> GetLogTailAsync(ErrorAnalysisSessionContext context, int maxChars, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedChars = NormalizeRequestedChars(maxChars);
        var sanitizedLog = await GetSanitizedLogAsync(context);
        var slice = AiContextFormattingHelper.GetTailSlice(sanitizedLog, normalizedChars);
        return FormatLogSlice(
            title: "当前会话日志尾部",
            requestedChars: normalizedChars,
            slice,
            emptyMessage: "当前会话没有可用日志。");
    }

    public async Task<string> GetLogChunkAsync(ErrorAnalysisSessionContext context, int startOffset, int maxChars, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedChars = NormalizeRequestedChars(maxChars);
        var sanitizedLog = await GetSanitizedLogAsync(context);
        var slice = AiContextFormattingHelper.GetSlice(sanitizedLog, startOffset, normalizedChars);
        var emptyMessage = slice.TotalLength == 0
            ? "当前会话没有可用日志。"
            : startOffset >= slice.TotalLength
                ? $"start_offset 超出日志范围。total_chars: {slice.TotalLength}"
                : "当前会话没有可用日志。";
        return FormatLogSlice(
            title: "当前会话日志分片",
            requestedChars: normalizedChars,
            slice,
            emptyMessage: emptyMessage);
    }

    private async Task<string> BuildLaunchSummaryAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken)
    {
        List<string> lines =
        [
            $"版本 ID: {DisplayValue(context.VersionId)}",
            $"游戏根目录: {DisplayValue(context.MinecraftPath)}"
        ];

        if (!string.IsNullOrWhiteSpace(context.LaunchCommand))
        {
            var javaExecutable = AiContextFormattingHelper.TryGetJavaExecutable(context.LaunchCommand);
            if (!string.IsNullOrWhiteSpace(javaExecutable))
            {
                lines.Add($"Java 可执行文件: {javaExecutable}");
            }

            lines.Add("启动参数详情: 可继续查看完整启动上下文（默认省略 classpath）");
        }
        else
        {
            lines.Add("启动参数详情: 当前会话不可用");
        }

        var versionConfig = await TryGetVersionConfigAsync(context, cancellationToken);
        if (versionConfig != null)
        {
            var effectiveLaunchSettings = await TryGetEffectiveLaunchSettingsAsync(versionConfig, cancellationToken);
            lines.Add($"Minecraft 版本: {DisplayValue(versionConfig.MinecraftVersion)}");
            lines.Add($"加载器: {FormatLoader(versionConfig)}");
            lines.Add($"Java 设置: {(versionConfig.UseGlobalJavaSetting ? "跟随全局" : "版本独立")}");
            lines.Add($"自定义 JVM 参数: {FormatJvmArgumentsState(versionConfig, effectiveLaunchSettings)}");
            lines.Add(versionConfig.OverrideMemory
                ? $"内存设置: 版本独立，最大 {versionConfig.MaximumHeapMemory:0.##} GB"
                : "内存设置: 跟随全局");
        }

        return await _logSanitizerService.SanitizeAsync(string.Join(Environment.NewLine, lines));
    }

    private async Task<VersionConfig?> TryGetVersionConfigAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.VersionId) || string.IsNullOrWhiteSpace(context.MinecraftPath))
        {
            return null;
        }

        try
        {
            var versionDirectory = Path.Combine(context.MinecraftPath, MinecraftPathConsts.Versions, context.VersionId);
            var versionConfig = await _versionInfoService.GetFullVersionInfoAsync(context.VersionId, versionDirectory, preferCache: true);
            cancellationToken.ThrowIfCancellationRequested();
            return versionConfig;
        }
        catch
        {
            return null;
        }
    }

    private async Task<EffectiveLaunchSettings?> TryGetEffectiveLaunchSettingsAsync(VersionConfig versionConfig, CancellationToken cancellationToken)
    {
        try
        {
            var effectiveLaunchSettings = await _launchSettingsResolver.ResolveAsync(versionConfig);
            cancellationToken.ThrowIfCancellationRequested();
            return effectiveLaunchSettings;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> GetSanitizedLogAsync(ErrorAnalysisSessionContext context)
    {
        return await _logSanitizerService.SanitizeAsync(context.OriginalLog ?? string.Empty);
    }

    private static int NormalizeRequestedChars(int maxChars)
    {
        if (maxChars <= 0)
        {
            return DefaultToolLogChars;
        }

        return Math.Min(maxChars, MaxToolLogChars);
    }

    private static string FormatLoader(VersionConfig versionConfig)
    {
        if (string.IsNullOrWhiteSpace(versionConfig.ModLoaderType))
        {
            return "vanilla";
        }

        return string.IsNullOrWhiteSpace(versionConfig.ModLoaderVersion)
            ? versionConfig.ModLoaderType
            : $"{versionConfig.ModLoaderType} {versionConfig.ModLoaderVersion}";
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "未知" : value;
    }

    private static string FormatJvmArgumentsState(VersionConfig versionConfig, EffectiveLaunchSettings? effectiveLaunchSettings)
    {
        if (effectiveLaunchSettings == null || string.IsNullOrWhiteSpace(effectiveLaunchSettings.CustomJvmArguments))
        {
            return "未配置";
        }

        return IsUsingGlobalJvmArguments(versionConfig)
            ? "已配置（全局）"
            : "已配置（版本独立）";
    }

    private static bool IsUsingGlobalJvmArguments(VersionConfig versionConfig)
    {
        return versionConfig.UseGlobalJavaSetting
            && !versionConfig.OverrideMemory
            && !versionConfig.OverrideResolution;
    }

    private static string FormatLogSlice(string title, int requestedChars, AiContextTextSlice slice, string emptyMessage)
    {
        if (string.IsNullOrEmpty(slice.Content))
        {
            return emptyMessage;
        }

        StringBuilder builder = new();
        builder.AppendLine($"title: {title}");
        builder.AppendLine($"requested_chars: {requestedChars}");
        builder.AppendLine($"total_chars: {slice.TotalLength}");
        builder.AppendLine($"start_offset: {slice.StartOffset}");
        builder.AppendLine($"end_offset: {slice.EndOffset}");
        builder.AppendLine($"truncated: {slice.WasTruncated}");
        builder.AppendLine("content:");
        builder.Append(slice.Content);
        return builder.ToString().TrimEnd();
    }
}