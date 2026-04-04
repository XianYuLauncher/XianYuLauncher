using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.Features.Launch.ViewModels;
using XianYuLauncher.Features.ModDownloadDetail.ViewModels;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed class SearchModrinthProjectToolHandler : IAgentToolHandler
{
    private readonly IAgentToolSupportService _toolSupportService;

    public SearchModrinthProjectToolHandler(IAgentToolSupportService toolSupportService)
    {
        _toolSupportService = toolSupportService;
    }

    public string ToolName => "searchModrinthProject";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "在 Modrinth 搜索 Mod/资源包/光影，用于查找缺失的依赖或推荐替代品",
        new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "搜索关键词" },
                projectType = new { type = "string", description = "资源类型: mod, resourcepack, shader", @enum = new[] { "mod", "resourcepack", "shader" } },
                loader = new { type = "string", description = "ModLoader 类型: fabric, forge, neoforge, quilt" }
            },
            required = new[] { "query" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => true;

    public async Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var query = arguments["query"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return AgentToolExecutionResult.FromMessage("请提供搜索关键词。");
        }

        Dictionary<string, string> parameters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["query"] = query
        };

        if (arguments["projectType"] != null)
        {
            parameters["projectType"] = arguments["projectType"]!.ToString();
        }

        if (arguments["loader"] != null)
        {
            var requestedLoader = NormalizeLoader(arguments["loader"]!.ToString());
            if (!string.IsNullOrWhiteSpace(requestedLoader))
            {
                parameters["loader"] = requestedLoader;
            }
        }
        else
        {
            var currentLoader = NormalizeLoader(await _toolSupportService.GetCurrentLoaderTypeAsync(cancellationToken));
            if (!string.IsNullOrWhiteSpace(currentLoader))
            {
                parameters["loader"] = currentLoader;
            }
        }

        var proposal = new AgentActionProposal
        {
            ActionType = ToolName,
            ButtonText = $"搜索 {query}",
            PermissionLevel = AgentToolPermissionLevel.ConfirmationRequired,
            Parameters = parameters
        };

        return AgentToolExecutionResult.FromActionProposal($"已准备搜索 \"{query}\"，用户可点击按钮执行搜索并查看结果。", proposal);
    }

    private static string NormalizeLoader(string? loader)
    {
        if (string.IsNullOrWhiteSpace(loader))
        {
            return string.Empty;
        }

        var normalized = loader.Trim().ToLowerInvariant();
        return normalized is "vanilla" or "auto" ? string.Empty : normalized;
    }
}

public sealed class DeleteModToolHandler : IAgentToolHandler
{
    public string ToolName => "deleteMod";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "删除指定的 Mod 文件（会弹窗让用户确认）",
        new
        {
            type = "object",
            properties = new
            {
                modId = new { type = "string", description = "Mod ID 或文件名" }
            },
            required = new[] { "modId" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => !string.IsNullOrWhiteSpace(context.MinecraftPath);

    public Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var modId = arguments["modId"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(modId))
        {
            return Task.FromResult(AgentToolExecutionResult.FromMessage("请提供要删除的 Mod ID 或文件名。"));
        }

        modId = Path.GetFileName(modId);
        var proposal = new AgentActionProposal
        {
            ActionType = ToolName,
            ButtonText = $"删除 {modId}",
            PermissionLevel = AgentToolPermissionLevel.ConfirmationRequired,
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["modId"] = modId
            }
        };

        return Task.FromResult(AgentToolExecutionResult.FromActionProposal($"已准备删除 Mod \"{modId}\"，等待用户确认。", proposal));
    }
}

public sealed class ToggleModToolHandler : IAgentToolHandler
{
    public string ToolName => "toggleMod";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "启用或禁用指定 Mod（通过重命名 .jar <-> .jar.disabled），比删除更安全",
        new
        {
            type = "object",
            properties = new
            {
                fileName = new { type = "string", description = "Mod 的 jar 文件名" },
                enabled = new { type = "boolean", description = "true=启用, false=禁用" }
            },
            required = new[] { "fileName", "enabled" }
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) => !string.IsNullOrWhiteSpace(context.MinecraftPath);

    public Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var fileName = arguments["fileName"]?.ToString() ?? string.Empty;
        var enabled = arguments["enabled"]?.Value<bool>() ?? true;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Task.FromResult(AgentToolExecutionResult.FromMessage("请提供 Mod 文件名。"));
        }

        fileName = Path.GetFileName(fileName);
        var proposal = new AgentActionProposal
        {
            ActionType = ToolName,
            ButtonText = $"{(enabled ? "启用" : "禁用")} {fileName}",
            PermissionLevel = AgentToolPermissionLevel.ConfirmationRequired,
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fileName"] = fileName,
                ["enabled"] = enabled.ToString()
            }
        };

        return Task.FromResult(AgentToolExecutionResult.FromActionProposal($"已准备{(enabled ? "启用" : "禁用")} Mod \"{fileName}\"，用户可点击按钮确认执行操作。", proposal));
    }
}

public sealed class SwitchJavaForVersionToolHandler : IAgentToolHandler
{
    public string ToolName => "switchJavaForVersion";

    public AiToolDefinition ToolDefinition => AiToolDefinition.Create(
        ToolName,
        "自动检测当前版本所需的 Java 版本并切换到最合适的已安装 Java",
        new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        });

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public bool IsAvailable(ErrorAnalysisSessionContext context) =>
        !string.IsNullOrWhiteSpace(context.VersionId) && !string.IsNullOrWhiteSpace(context.MinecraftPath);

    public Task<AgentToolExecutionResult> ExecuteAsync(ErrorAnalysisSessionContext context, JObject arguments, CancellationToken cancellationToken)
    {
        var proposal = new AgentActionProposal
        {
            ActionType = ToolName,
            ButtonText = "自动切换 Java 版本",
            PermissionLevel = AgentToolPermissionLevel.ConfirmationRequired,
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        return Task.FromResult(AgentToolExecutionResult.FromActionProposal("已准备自动切换 Java 版本，等待用户点击按钮执行。", proposal));
    }
}

public sealed class SearchModrinthProjectActionHandler : IAgentActionHandler
{
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly INavigationService _navigationService;
    private readonly ICommonDialogService _dialogService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly ErrorAnalysisSessionState _sessionState;

    public SearchModrinthProjectActionHandler(
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        INavigationService navigationService,
        ICommonDialogService dialogService,
        IUiDispatcher uiDispatcher,
        ErrorAnalysisSessionState sessionState)
    {
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _uiDispatcher = uiDispatcher;
        _sessionState = sessionState;
    }

    public string ActionType => "searchModrinthProject";

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public async Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        if (!proposal.Parameters.TryGetValue("query", out var query) || string.IsNullOrWhiteSpace(query))
        {
            return "未提供搜索关键词。";
        }

        var projectType = proposal.Parameters.TryGetValue("projectType", out var projectTypeValue)
            ? projectTypeValue
            : "mod";

        var loader = proposal.Parameters.TryGetValue("loader", out var loaderValue)
            ? NormalizeLoader(loaderValue)
            : string.Empty;

        List<List<string>>? facets = null;
        if (!string.IsNullOrWhiteSpace(loader))
        {
            facets =
            [
                new List<string> { $"categories:{loader}" }
            ];
        }

        var result = await _modrinthService.SearchModsAsync(query, facets, "relevance", 0, 5, projectType);
        cancellationToken.ThrowIfCancellationRequested();
        if (result?.Hits == null || result.Hits.Count == 0)
        {
            var curseForgeResult = await TrySearchCurseForgeAsync(query, loader, cancellationToken);
            if (curseForgeResult.Found)
            {
                return curseForgeResult.Message;
            }

            if (!string.IsNullOrWhiteSpace(curseForgeResult.Message))
            {
                return curseForgeResult.Message;
            }

            await ShowNotFoundDialogAsync(query);
            return $"未在 Modrinth 或 CurseForge 找到与 '{query}' 对应的项目。";
        }

        var normalizedQuery = NormalizeSlug(query);
        var bestMatch = result.Hits.FirstOrDefault(h => NormalizeSlug(h.Slug) == normalizedQuery)
                        ?? result.Hits.FirstOrDefault(h => NormalizeSlug(h.Title) == normalizedQuery)
                        ?? result.Hits.First();

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, new Tuple<ModrinthProject, string>(bestMatch, "Modrinth"));
        });

        return $"已打开 {bestMatch.Title} 的项目详情页（来源：Modrinth）。";
    }

    private async Task<(bool Found, string Message)> TrySearchCurseForgeAsync(string query, string loader, CancellationToken cancellationToken)
    {
        try
        {
            int? modLoaderType = loader switch
            {
                "forge" => 1,
                "fabric" => 4,
                "quilt" => 5,
                "neoforge" => 6,
                _ => null
            };

            var cfResult = await _curseForgeService.SearchModsAsync(query, null, modLoaderType, null, 0, 5);
            cancellationToken.ThrowIfCancellationRequested();
            if (cfResult?.Data == null || cfResult.Data.Count == 0)
            {
                return (false, string.Empty);
            }

            var normalizedQuery = NormalizeSlug(query);
            var best = cfResult.Data.FirstOrDefault(h => NormalizeSlug(h.Slug) == normalizedQuery)
                       ?? cfResult.Data.FirstOrDefault(h => NormalizeSlug(h.Name) == normalizedQuery)
                       ?? cfResult.Data.First();

            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, $"curseforge-{best.Id}");
            });
            return (true, $"已打开 {best.Name} 的项目详情页（来源：CurseForge）。");
        }
        catch (Exception ex)
        {
            return (false, $"CurseForge 搜索失败: {ex.Message}");
        }
    }

    private async Task ShowNotFoundDialogAsync(string query)
    {
        await _uiDispatcher.RunOnUiThreadAsync(async () =>
        {
            await _dialogService.ShowMessageDialogAsync(
                "未找到",
                $"未在 Modrinth 或 CurseForge 找到与 '{query}' 对应的项目。",
                "确定");
        });
    }

    private async Task AppendAnalysisMessageAsync(string message)
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _sessionState.AIAnalysisResult += $"\n\n{message}";
        });
    }

    private static string NormalizeLoader(string? loader)
    {
        if (string.IsNullOrWhiteSpace(loader))
        {
            return string.Empty;
        }

        var normalized = loader.Trim().ToLowerInvariant();
        return normalized is "auto" or "vanilla" ? string.Empty : normalized;
    }

    private static string NormalizeSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray());
        return normalized.ToLowerInvariant();
    }
}

public sealed class DeleteModActionHandler : IAgentActionHandler
{
    private readonly IAgentToolSupportService _toolSupportService;
    private readonly ICommonDialogService _dialogService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly ErrorAnalysisSessionState _sessionState;

    public DeleteModActionHandler(
        IAgentToolSupportService toolSupportService,
        ICommonDialogService dialogService,
        IUiDispatcher uiDispatcher,
        ErrorAnalysisSessionState sessionState)
    {
        _toolSupportService = toolSupportService;
        _dialogService = dialogService;
        _uiDispatcher = uiDispatcher;
        _sessionState = sessionState;
    }

    public string ActionType => "deleteMod";

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public async Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        if (!TryResolveModId(proposal.Parameters, out var modId))
        {
            return "未提供 Mod 标识。";
        }

        modId = Path.GetFileName(modId);
        var modFilePath = await _toolSupportService.FindModFileByIdAsync(modId);
        if (string.IsNullOrWhiteSpace(modFilePath))
        {
            return $"未找到 Mod 文件：{modId}";
        }

        bool shouldDelete = false;
        await _uiDispatcher.RunOnUiThreadAsync(async () =>
        {
            shouldDelete = await _dialogService.ShowConfirmationDialogAsync(
                "删除 Mod",
                $"确定要删除该 Mod 吗？\n\n文件名：{Path.GetFileName(modFilePath)}\n路径：{modFilePath}\n\n注意：如果这是依赖库，可能会影响其它 Mod。",
                "删除",
                "取消");
        });

        if (!shouldDelete)
        {
            return $"已取消删除 Mod：{Path.GetFileName(modFilePath)}";
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            File.Delete(modFilePath);
            return $"已删除 Mod：{Path.GetFileName(modFilePath)}";
        }
        catch (Exception ex)
        {
            return $"删除 Mod 失败：{ex.Message}";
        }
    }

    private static bool TryResolveModId(IReadOnlyDictionary<string, string> parameters, out string modId)
    {
        if (parameters.TryGetValue("modId", out modId!) && !string.IsNullOrWhiteSpace(modId))
        {
            return true;
        }

        if (parameters.TryGetValue("modFile", out modId!) && !string.IsNullOrWhiteSpace(modId))
        {
            return true;
        }

        return parameters.TryGetValue("fileName", out modId!) && !string.IsNullOrWhiteSpace(modId);
    }

    private async Task AppendAnalysisMessageAsync(string message)
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _sessionState.AIAnalysisResult += $"\n\n{message}";
        });
    }
}

public sealed class ToggleModActionHandler : IAgentActionHandler
{
    private readonly IAgentToolSupportService _toolSupportService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly ErrorAnalysisSessionState _sessionState;

    public ToggleModActionHandler(
        IAgentToolSupportService toolSupportService,
        IUiDispatcher uiDispatcher,
        ErrorAnalysisSessionState sessionState)
    {
        _toolSupportService = toolSupportService;
        _uiDispatcher = uiDispatcher;
        _sessionState = sessionState;
    }

    public string ActionType => "toggleMod";

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public async Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        if (!proposal.Parameters.TryGetValue("fileName", out var fileName) || string.IsNullOrWhiteSpace(fileName))
        {
            return "未提供 Mod 文件名。";
        }

        bool enabled = !proposal.Parameters.TryGetValue("enabled", out var enabledText) || bool.Parse(enabledText);

        try
        {
            var filePath = await _toolSupportService.FindModFileByIdAsync(fileName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                var candidateNames = new List<string>();
                if (enabled)
                {
                    candidateNames.Add(fileName + FileExtensionConsts.Disabled);
                }
                else if (fileName.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase))
                {
                    candidateNames.Add(fileName[..^FileExtensionConsts.Disabled.Length]);
                }
                else
                {
                    candidateNames.Add(fileName + FileExtensionConsts.Disabled);
                }

                foreach (var candidateName in candidateNames)
                {
                    filePath = await _toolSupportService.FindModFileByIdAsync(candidateName);
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return $"未找到 Mod 文件：{fileName}";
            }

            cancellationToken.ThrowIfCancellationRequested();

            var currentName = Path.GetFileName(filePath);
            var dir = Path.GetDirectoryName(filePath)!;
            string newName = enabled
                ? currentName.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase)
                    ? currentName[..^FileExtensionConsts.Disabled.Length]
                    : currentName
                : currentName.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase)
                    ? currentName
                    : currentName + FileExtensionConsts.Disabled;

            if (newName != currentName)
            {
                File.Move(filePath, Path.Combine(dir, newName));
                return $"已{(enabled ? "启用" : "禁用")} Mod：{currentName} → {newName}";
            }

            return $"Mod {currentName} 已经是{(enabled ? "启用" : "禁用")}状态。";
        }
        catch (Exception ex)
        {
            return $"操作失败：{ex.Message}";
        }
    }

    private async Task AppendAnalysisMessageAsync(string message)
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _sessionState.AIAnalysisResult += $"\n\n{message}";
        });
    }
}

public sealed class SwitchJavaForVersionActionHandler : IAgentActionHandler
{
    private readonly IVersionInfoManager _versionInfoManager;
    private readonly IJavaRuntimeService _javaRuntimeService;
    private readonly IAgentToolSupportService _toolSupportService;
    private readonly INavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly ErrorAnalysisSessionState _sessionState;

    public SwitchJavaForVersionActionHandler(
        IVersionInfoManager versionInfoManager,
        IJavaRuntimeService javaRuntimeService,
        IAgentToolSupportService toolSupportService,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        IUiDispatcher uiDispatcher,
        ErrorAnalysisSessionState sessionState)
    {
        _versionInfoManager = versionInfoManager;
        _javaRuntimeService = javaRuntimeService;
        _toolSupportService = toolSupportService;
        _navigationService = navigationService;
        _serviceProvider = serviceProvider;
        _uiDispatcher = uiDispatcher;
        _sessionState = sessionState;
    }

    public string ActionType => "switchJavaForVersion";

    public AgentToolPermissionLevel PermissionLevel => AgentToolPermissionLevel.ConfirmationRequired;

    public async Task<string> ExecuteAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        var versionId = _sessionState.Context.VersionId;
        var minecraftPath = _sessionState.Context.MinecraftPath;
        if (string.IsNullOrWhiteSpace(versionId) || string.IsNullOrWhiteSpace(minecraftPath))
        {
            return "未找到当前版本信息，无法自动切换 Java。";
        }

        VersionInfo? versionInfo;
        try
        {
            versionInfo = await _versionInfoManager.GetVersionInfoAsync(versionId, minecraftPath, allowNetwork: false, cancellationToken: cancellationToken);
        }
        catch
        {
            try
            {
                versionInfo = await _versionInfoManager.GetVersionInfoAsync(versionId, minecraftPath, allowNetwork: true, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                return $"读取版本信息失败：{ex.Message}";
            }
        }

        int requiredMajorVersion = versionInfo?.JavaVersion?.MajorVersion ?? 8;
        var javaVersions = await _javaRuntimeService.DetectJavaVersionsAsync(true);
        cancellationToken.ThrowIfCancellationRequested();
        var bestJava = _toolSupportService.SelectBestJava(javaVersions, requiredMajorVersion);
        if (bestJava == null || string.IsNullOrWhiteSpace(bestJava.Path))
        {
            return $"未找到可用的 Java {requiredMajorVersion} 版本，请先安装对应版本后再重试。";
        }

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            var launchViewModel = _serviceProvider.GetRequiredService<LaunchViewModel>();
            if (!string.Equals(launchViewModel.SelectedVersion, versionId, StringComparison.OrdinalIgnoreCase))
            {
                launchViewModel.SelectedVersion = versionId;
            }

            launchViewModel.SetTemporaryJavaOverride(bestJava.Path);
            _navigationService.NavigateTo(typeof(LaunchViewModel).FullName!);
            launchViewModel.LaunchGameCommand.Execute(null);
        });

        return $"已为版本 {versionId} 临时切换到 Java {bestJava.MajorVersion}，并开始启动游戏。";
    }

    private async Task AppendAnalysisMessageAsync(string message)
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _sessionState.AIAnalysisResult += $"\n\n{message}";
        });
    }
}