using System.Text;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed class ListInstalledModsToolHandler : IErrorAnalysisToolHandler
{
    private readonly IErrorAnalysisToolSupportService _toolSupportService;

    public ListInstalledModsToolHandler(IErrorAnalysisToolSupportService toolSupportService)
    {
        _toolSupportService = toolSupportService;
    }

    public string ToolName => "listInstalledMods";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "列出当前游戏版本已安装的所有 Mod，返回名称、版本、文件名、是否启用",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });

    public ErrorAnalysisToolPermissionLevel PermissionLevel => ErrorAnalysisToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => !string.IsNullOrWhiteSpace(context.MinecraftPath);

    public Task<ErrorAnalysisToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var files = _toolSupportService.GetInstalledModFiles();
        if (files.Count == 0)
        {
            return Task.FromResult(ErrorAnalysisToolExecutionResult.FromMessage("mods 目录为空，没有安装任何 Mod。"));
        }

        var sb = new StringBuilder();
        sb.AppendLine($"共 {files.Count} 个 Mod 文件：");
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(file);
            var enabled = !fileName.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase);
            sb.AppendLine($"- {fileName} [{(enabled ? "启用" : "禁用")}] ");
        }

        return Task.FromResult(ErrorAnalysisToolExecutionResult.FromMessage(sb.ToString()));
    }
}

public sealed class GetVersionConfigToolHandler : IErrorAnalysisToolHandler
{
    private readonly IVersionInfoService _versionInfoService;

    public GetVersionConfigToolHandler(IVersionInfoService versionInfoService)
    {
        _versionInfoService = versionInfoService;
    }

    public string ToolName => "getVersionConfig";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "获取当前游戏版本的配置信息，包括 Minecraft 版本号、ModLoader 类型和版本、Java 路径、内存设置等",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });

    public ErrorAnalysisToolPermissionLevel PermissionLevel => ErrorAnalysisToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) =>
        !string.IsNullOrWhiteSpace(context.VersionId) && !string.IsNullOrWhiteSpace(context.MinecraftPath);

    public async Task<ErrorAnalysisToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        try
        {
            var versionDirectory = Path.Combine(context.MinecraftPath, MinecraftPathConsts.Versions, context.VersionId);
            var config = await _versionInfoService.GetFullVersionInfoAsync(context.VersionId, versionDirectory, preferCache: true);
            cancellationToken.ThrowIfCancellationRequested();

            var sb = new StringBuilder();
            sb.AppendLine($"版本 ID: {context.VersionId}");
            sb.AppendLine($"Minecraft 版本: {config.MinecraftVersion}");
            sb.AppendLine($"ModLoader: {config.ModLoaderType ?? "vanilla"} {config.ModLoaderVersion ?? string.Empty}");
            sb.AppendLine($"Java 路径: {(string.IsNullOrEmpty(config.JavaPath) ? "使用全局设置" : config.JavaPath)}");
            sb.AppendLine($"内存设置: 自动={config.AutoMemoryAllocation}, 初始={config.InitialHeapMemory}GB, 最大={config.MaximumHeapMemory}GB");
            return ErrorAnalysisToolExecutionResult.FromMessage(sb.ToString());
        }
        catch (Exception ex)
        {
            return ErrorAnalysisToolExecutionResult.FromMessage($"读取版本配置失败: {ex.Message}");
        }
    }
}

public sealed class CheckJavaVersionsToolHandler : IErrorAnalysisToolHandler
{
    private readonly IJavaRuntimeService _javaRuntimeService;

    public CheckJavaVersionsToolHandler(IJavaRuntimeService javaRuntimeService)
    {
        _javaRuntimeService = javaRuntimeService;
    }

    public string ToolName => "checkJavaVersions";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "列出本机已安装的所有 Java 版本，返回版本号和路径",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });

    public ErrorAnalysisToolPermissionLevel PermissionLevel => ErrorAnalysisToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<ErrorAnalysisToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        try
        {
            var javaVersions = await _javaRuntimeService.DetectJavaVersionsAsync(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (javaVersions.Count == 0)
            {
                return ErrorAnalysisToolExecutionResult.FromMessage("未检测到任何已安装的 Java 版本。");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"检测到 {javaVersions.Count} 个 Java 版本：");
            foreach (var javaVersion in javaVersions)
            {
                sb.AppendLine($"- Java {javaVersion.MajorVersion} ({javaVersion.FullVersion}) - {javaVersion.Path}");
            }

            return ErrorAnalysisToolExecutionResult.FromMessage(sb.ToString());
        }
        catch (Exception ex)
        {
            return ErrorAnalysisToolExecutionResult.FromMessage($"检测 Java 版本失败: {ex.Message}");
        }
    }
}

public sealed class SearchKnowledgeBaseToolHandler : IErrorAnalysisToolHandler
{
    private readonly ICrashAnalyzer _crashAnalyzer;

    public SearchKnowledgeBaseToolHandler(ICrashAnalyzer crashAnalyzer)
    {
        _crashAnalyzer = crashAnalyzer;
    }

    public string ToolName => "searchKnowledgeBase";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "在内置错误知识库中搜索匹配的错误规则，返回匹配的错误标题、分析和修复建议",
        new
        {
            type = "object",
            properties = new
            {
                keyword = new { type = "string", description = "搜索关键词，如错误类名或关键日志片段" }
            },
            required = new[] { "keyword" }
        });

    public ErrorAnalysisToolPermissionLevel PermissionLevel => ErrorAnalysisToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<ErrorAnalysisToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var keyword = arguments["keyword"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return ErrorAnalysisToolExecutionResult.FromMessage("请提供搜索关键词。");
        }

        try
        {
            var fakeOutput = new List<string> { keyword };
            var result = await _crashAnalyzer.AnalyzeCrashAsync(0, fakeOutput, fakeOutput);
            cancellationToken.ThrowIfCancellationRequested();

            if (result.Type == CrashType.Unknown)
            {
                return ErrorAnalysisToolExecutionResult.FromMessage($"知识库中未找到与 \"{keyword}\" 匹配的错误规则。");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"匹配到错误: {result.Title}");
            sb.AppendLine($"类型: {result.Type}");
            sb.AppendLine($"分析: {result.Analysis}");
            if (result.Suggestions.Count > 0)
            {
                sb.AppendLine("建议:");
                foreach (var suggestion in result.Suggestions)
                {
                    sb.AppendLine($"  - {suggestion}");
                }
            }

            return ErrorAnalysisToolExecutionResult.FromMessage(sb.ToString());
        }
        catch (Exception ex)
        {
            return ErrorAnalysisToolExecutionResult.FromMessage($"搜索知识库失败: {ex.Message}");
        }
    }
}

public sealed class ReadModInfoToolHandler : IErrorAnalysisToolHandler
{
    private readonly IErrorAnalysisToolSupportService _toolSupportService;

    public ReadModInfoToolHandler(IErrorAnalysisToolSupportService toolSupportService)
    {
        _toolSupportService = toolSupportService;
    }

    public string ToolName => "readModInfo";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "读取指定 Mod 文件的元数据（fabric.mod.json 或 mods.toml），返回 modId、名称、版本、依赖列表",
        new
        {
            type = "object",
            properties = new
            {
                fileName = new { type = "string", description = "Mod 的 jar 文件名" }
            },
            required = new[] { "fileName" }
        });

    public ErrorAnalysisToolPermissionLevel PermissionLevel => ErrorAnalysisToolPermissionLevel.ReadOnly;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => !string.IsNullOrWhiteSpace(context.MinecraftPath);

    public async Task<ErrorAnalysisToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var fileName = arguments["fileName"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return ErrorAnalysisToolExecutionResult.FromMessage("请提供 Mod 文件名。");
        }

        fileName = Path.GetFileName(fileName);
        var filePath = await _toolSupportService.FindModFileByIdAsync(fileName);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ErrorAnalysisToolExecutionResult.FromMessage($"未找到 Mod 文件: {fileName}");
        }

        try
        {
            var modId = await _toolSupportService.TryGetFabricModIdAsync(filePath);
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(modId))
            {
                return ErrorAnalysisToolExecutionResult.FromMessage($"Mod文件: {fileName}\nFabric Mod ID: {modId}\n(完整元数据需解压 jar 读取 fabric.mod.json)");
            }

            return ErrorAnalysisToolExecutionResult.FromMessage($"Mod文件: {fileName}\n无法解析元数据（可能不是 Fabric mod，或 jar 格式异常）");
        }
        catch (Exception ex)
        {
            return ErrorAnalysisToolExecutionResult.FromMessage($"读取 Mod 信息失败: {ex.Message}");
        }
    }
}