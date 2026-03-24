using System.Text;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public class ErrorAnalysisAiOrchestrator : IErrorAnalysisAiOrchestrator
{
    private const int MaxToolCallRounds = 8;

    private readonly ILanguageSelectorService _languageSelectorService;
    private readonly ILogSanitizerService _logSanitizerService;
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly IAiSettingsDomainService _aiSettingsDomainService;
    private readonly ICrashAnalyzer _crashAnalyzer;
    private readonly IAgentToolDispatcher _toolDispatcher;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly ErrorAnalysisSessionState _sessionState;

    public ErrorAnalysisAiOrchestrator(
        ILanguageSelectorService languageSelectorService,
        ILogSanitizerService logSanitizerService,
        IAIAnalysisService aiAnalysisService,
        IAiSettingsDomainService aiSettingsDomainService,
        ICrashAnalyzer crashAnalyzer,
        IAgentToolDispatcher toolDispatcher,
        IUiDispatcher uiDispatcher,
        ErrorAnalysisSessionState sessionState)
    {
        _languageSelectorService = languageSelectorService;
        _logSanitizerService = logSanitizerService;
        _aiAnalysisService = aiAnalysisService;
        _aiSettingsDomainService = aiSettingsDomainService;
        _crashAnalyzer = crashAnalyzer;
        _toolDispatcher = toolDispatcher;
        _uiDispatcher = uiDispatcher;
        _sessionState = sessionState;
    }

    public async Task AnalyzeCrashAsync(CancellationToken cancellationToken)
    {
        if (!_sessionState.Context.IsGameCrashed || _sessionState.IsAiAnalyzing)
        {
            return;
        }

        try
        {
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                _sessionState.IsAiAnalyzing = true;
                _sessionState.IsChatEnabled = false;
                _sessionState.ChatMessages.Clear();
                _sessionState.AiAnalysisResult = "正在分析崩溃原因...\n\n";
                _sessionState.ResetFixActions();
            });

            await Task.Delay(50, cancellationToken);

            var settings = await _aiSettingsDomainService.LoadAsync();
            if (settings.IsEnabled)
            {
                await AnalyzeWithExternalAiAsync(settings, cancellationToken);
                return;
            }

            await AnalyzeWithKnowledgeBaseAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await AppendCancellationMessageAsync();
        }
        catch (Exception ex)
        {
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                var msg = string.Format(GetLocalizedString("ErrorAnalysis_AnalysisFailed.Text"), ex.Message);
                if (_sessionState.ChatMessages.Any())
                {
                    _sessionState.ChatMessages.Add(new UiChatMessage("assistant", $"\n\n{msg}"));
                }

                _sessionState.AiAnalysisResult += $"\n\n{msg}";
            });
        }
        finally
        {
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                _sessionState.IsAiAnalyzing = false;
                _sessionState.IsChatEnabled = true;
            });
        }
    }

    public async Task SendMessageAsync(string userMessage, CancellationToken cancellationToken)
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _sessionState.IsAiAnalyzing = true;
            _sessionState.IsChatEnabled = false;
            _sessionState.ResetFixActions();
            _sessionState.ChatMessages.Add(new UiChatMessage("user", userMessage));
            _sessionState.ChatMessages.Add(new UiChatMessage("assistant", "..."));
        });

        await Task.Delay(50, cancellationToken);

        try
        {
            var settings = await _aiSettingsDomainService.LoadAsync();
            if (!settings.IsEnabled)
            {
                await SetLastAssistantMessageAsync("当前为知识库模式，无法进行 AI 对话。请在设置中开启 AI 分析。");
                return;
            }

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                await StreamResponseToLastMessageAsync(settings, cancellationToken);
            }
            else
            {
                await SetLastAssistantMessageAsync("API Key Missing.");
            }
        }
        catch (OperationCanceledException)
        {
            await AppendCancellationMessageAsync();
        }
        finally
        {
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                _sessionState.IsAiAnalyzing = false;
                _sessionState.IsChatEnabled = true;
            });
        }
    }

    private async Task AnalyzeWithExternalAiAsync(
        AiSettingsState settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                const string msg = "未配置 API Key，无法进行 AI 分析。";
                _sessionState.ChatMessages.Add(new UiChatMessage("assistant", msg));
                _sessionState.AiAnalysisResult = msg;
            });
            return;
        }

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _sessionState.AiAnalysisResult = "正在进行外部 AI 分析...\n\n";
            _sessionState.ResetFixActions();
        });

        var sanitizedLog = await BuildSanitizedCrashLogAsync();

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _sessionState.ChatMessages.Add(new UiChatMessage("user", sanitizedLog));
            _sessionState.ChatMessages.Add(new UiChatMessage("assistant", "..."));
        });

        await Task.Delay(50, cancellationToken);
        await StreamResponseToLastMessageAsync(settings, cancellationToken);
    }

    private async Task AnalyzeWithKnowledgeBaseAsync(CancellationToken cancellationToken)
    {
        var (gameOutput, gameError) = _sessionState.CreateLogSnapshot();

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _sessionState.ChatMessages.Add(new UiChatMessage("assistant", "..."));
        });

        var buffered = new StringBuilder();
        bool isFirstChunk = true;
        var lastFlush = DateTime.UtcNow;
        const int flushIntervalMs = 10;
        const int flushSize = 80;

        await foreach (var chunk in _crashAnalyzer.GetStreamingAnalysisAsync(0, gameOutput, gameError))
        {
            cancellationToken.ThrowIfCancellationRequested();

            buffered.Append(chunk);

            var now = DateTime.UtcNow;
            if (buffered.Length >= flushSize || (now - lastFlush).TotalMilliseconds >= flushIntervalMs)
            {
                var text = buffered.ToString();
                buffered.Clear();
                lastFlush = now;

                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    if (!_sessionState.ChatMessages.Any())
                    {
                        return;
                    }

                    var lastMsg = _sessionState.ChatMessages.Last();
                    if (isFirstChunk)
                    {
                        if (lastMsg.Content == "...")
                        {
                            lastMsg.Content = string.Empty;
                        }

                        isFirstChunk = false;
                    }

                    lastMsg.Content += text;
                    if (_sessionState.ChatMessages.Count <= 1)
                    {
                        _sessionState.AiAnalysisResult = _sessionState.ChatMessages.Last().Content;
                    }
                });
            }
        }

        if (buffered.Length > 0)
        {
            var remaining = buffered.ToString();
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                if (!_sessionState.ChatMessages.Any())
                {
                    return;
                }

                _sessionState.ChatMessages.Last().Content += remaining;
                if (_sessionState.ChatMessages.Count <= 1)
                {
                    _sessionState.AiAnalysisResult = _sessionState.ChatMessages.Last().Content;
                }
            });
        }

        cancellationToken.ThrowIfCancellationRequested();

        var analysisResult = await _crashAnalyzer.AnalyzeCrashAsync(0, gameOutput, gameError);
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            var actions = analysisResult.FixActions ?? [];
            if (actions.Count == 0 && analysisResult.FixAction != null)
            {
                actions = [analysisResult.FixAction];
            }

            var proposals = actions
                .Select(AgentActionProposal.FromCrashFixAction)
                .ToList();
            _sessionState.ApplyActionProposals(proposals);
        });
    }

    private async Task<string> BuildSanitizedCrashLogAsync()
    {
        var sanitizedLog = await _logSanitizerService.SanitizeAsync(_sessionState.FullLog);
        if (!string.IsNullOrWhiteSpace(_sessionState.Context.LaunchCommand))
        {
            var sanitizedCmd = await _logSanitizerService.SanitizeAsync(_sessionState.Context.LaunchCommand);
            sanitizedLog = $"=== Launch Command ===\n{sanitizedCmd}\n\n=== Game Log ===\n{sanitizedLog}";
        }

        if (sanitizedLog.Length <= 15000)
        {
            return sanitizedLog;
        }

        const int keepHeadResponse = 2000;
        const int keepTailResponse = 13000;
        if (sanitizedLog.Length > keepHeadResponse + keepTailResponse)
        {
            string head = sanitizedLog[..keepHeadResponse];
            string tail = sanitizedLog[^keepTailResponse..];
            return head + "\n\n[...Log Truncated (Middle)...]\n\n" + tail;
        }

        sanitizedLog = sanitizedLog[^15000..];
        return "[...Log Truncated...] " + sanitizedLog;
    }

    private async Task StreamResponseToLastMessageAsync(
        AiSettingsState settings,
        CancellationToken cancellationToken)
    {
        var apiMessages = await BuildApiMessagesAsync();
        var tools = _toolDispatcher.GetAvailableTools();

        for (int round = 0; round < MaxToolCallRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool isFirstChunk = true;
            var contentBuilder = new StringBuilder();
            var updateBuffer = new StringBuilder();
            var lastUpdate = DateTime.UtcNow;
            var uiUpdateInterval = TimeSpan.FromMilliseconds(50);
            List<ToolCallInfo>? pendingToolCalls = null;

            await foreach (var chunk in _aiAnalysisService.StreamChatWithToolsAsync(apiMessages, tools, settings.ApiKey, settings.ApiEndpoint, settings.Model))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (chunk.IsContent)
                {
                    contentBuilder.Append(chunk.ContentDelta);
                    updateBuffer.Append(chunk.ContentDelta);

                    var now = DateTime.UtcNow;
                    if ((now - lastUpdate) > uiUpdateInterval || updateBuffer.Length > 20)
                    {
                        var textToAppend = updateBuffer.ToString();
                        updateBuffer.Clear();
                        lastUpdate = now;

                        await _uiDispatcher.RunOnUiThreadAsync(() =>
                        {
                            if (!_sessionState.ChatMessages.Any())
                            {
                                return;
                            }

                            var lastMsg = _sessionState.ChatMessages.Last();
                            if (isFirstChunk)
                            {
                                if (lastMsg.Content == "...")
                                {
                                    lastMsg.Content = string.Empty;
                                }

                                isFirstChunk = false;
                            }

                            lastMsg.Content += textToAppend;
                            if (_sessionState.ChatMessages.Count <= 3)
                            {
                                _sessionState.AiAnalysisResult = lastMsg.Content;
                            }
                        });
                    }
                }

                if (chunk.IsToolCall)
                {
                    pendingToolCalls = chunk.ToolCalls;
                }
            }

            if (updateBuffer.Length > 0)
            {
                var finalText = updateBuffer.ToString();
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    if (!_sessionState.ChatMessages.Any())
                    {
                        return;
                    }

                    var lastMsg = _sessionState.ChatMessages.Last();
                    if (isFirstChunk && lastMsg.Content == "...")
                    {
                        lastMsg.Content = string.Empty;
                    }

                    lastMsg.Content += finalText;
                    if (_sessionState.ChatMessages.Count <= 3)
                    {
                        _sessionState.AiAnalysisResult = lastMsg.Content;
                    }
                });
            }

            if (pendingToolCalls == null || pendingToolCalls.Count == 0)
            {
                break;
            }

            apiMessages.Add(new ChatMessage("assistant", contentBuilder.Length > 0 ? contentBuilder.ToString() : null, pendingToolCalls));

            List<AgentActionProposal> actionProposals = [];
            foreach (var toolCall in pendingToolCalls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    if (!_sessionState.ChatMessages.Any())
                    {
                        return;
                    }

                    var lastMsg = _sessionState.ChatMessages.Last();
                    if (string.IsNullOrEmpty(lastMsg.Content) || lastMsg.Content == "...")
                    {
                        lastMsg.Content = $"正在调用 {toolCall.FunctionName}...";
                    }
                    else
                    {
                        lastMsg.Content += $"\n\n正在调用 {toolCall.FunctionName}...";
                    }
                });

                var result = await _toolDispatcher.ExecuteAsync(toolCall, cancellationToken);
                if (result.ActionProposal != null)
                {
                    actionProposals.Add(result.ActionProposal);
                }

                apiMessages.Add(ChatMessage.ToolResult(toolCall.Id, result.Message));
            }

            if (actionProposals.Count > 0)
            {
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    _sessionState.ApplyActionProposals(actionProposals);
                });
            }

            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                _sessionState.ChatMessages.Add(new UiChatMessage("assistant", "..."));
            });

            await Task.Delay(50, cancellationToken);
        }
    }

    private async Task<List<ChatMessage>> BuildApiMessagesAsync()
    {
        List<ChatMessage> apiMessages = [];
        string languageForAi = _languageSelectorService.Language == "zh-CN" ? "Simplified Chinese" : "English";
        apiMessages.Add(new ChatMessage("system", BuildFunctionCallingSystemPrompt(languageForAi)));

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            int count = Math.Max(0, _sessionState.ChatMessages.Count - 1);
            foreach (var msg in _sessionState.ChatMessages.Take(count))
            {
                if (msg.Role == "system")
                {
                    continue;
                }

                apiMessages.Add(new ChatMessage(msg.Role, msg.Content));
            }
        });

        return apiMessages;
    }

    private async Task SetLastAssistantMessageAsync(string content)
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            if (_sessionState.ChatMessages.Any())
            {
                _sessionState.ChatMessages.Last().Content = content;
            }
        });
    }

    private async Task AppendCancellationMessageAsync()
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            if (_sessionState.ChatMessages.Any())
            {
                _sessionState.ChatMessages.Last().Content += $"\n\n{GetLocalizedString("ErrorAnalysis_AnalysisCanceled.Text")}";
            }
        });
    }

    private string GetLocalizedString(string resourceKey)
    {
        var isChinese = _languageSelectorService.Language == "zh-CN";
        return resourceKey switch
        {
            "ErrorAnalysis_AnalysisCanceled.Text" => isChinese ? "分析已取消。" : "Analysis canceled.",
            "ErrorAnalysis_AnalysisFailed.Text" => isChinese ? "分析失败: {0}" : "Analysis failed: {0}",
            _ => resourceKey
        };
    }

    private static string BuildFunctionCallingSystemPrompt(string language)
    {
        return
            "You are an expert Minecraft technical support agent built into XianYu Launcher. " +
            "XianYu Launcher is the product's global brand name — always refer to it as \"XianYu Launcher\", never translate or localize this name. " +
            $"Analyze crash logs and help users fix issues. Respond in {language}. " +
            "Be concise and reference specific mods or config files if they are responsible. " +
            "\n\n" +
            "You have access to tools that can query the user's game environment (installed mods, Java versions, config, etc.) " +
            "and perform fix actions (search mods, toggle mods, switch Java). " +
            "Use query tools FIRST to gather information before suggesting fixes. " +
            "For destructive actions (delete/toggle mods), always explain what you're doing and why. " +
            "For search actions (searchModrinthProject), prefer using the tool so the user gets a direct download link.\n\n" +
            "CRITICAL RULES:\n" +
            "1. CHECK THE LAUNCH COMMAND FIRST! If the user has set invalid JVM arguments (e.g., nonsense in -Djava.library.path or -Xmx), TELL THEM TO FIX IT MANUALLY in the settings. Do NOT switch Java versions for bad arguments.\n" +
            "2. ONLY use the 'switchJava' tool if the crash log explicitly indicates a Java version mismatch or runtime error. Do not guess.\n" +
            "3. The 'searchModrinthProject' tool is stricterly for searching MODS, SHADERS, or RESOURCE PACKS. It CANNOT search for Mod Loaders (Forge, Fabric, NeoForge, Quilt). Explain manual installation for loaders if needed.\n" +
            "4. If you cannot fix the issue via tools, provide clear manual instructions. If the problem persists, advise the user to click the 'Contact Author' (联系作者) button at the top.\n" +
            "5. For 'toggleMod' or 'deleteMod', you MUST inform the user that a button has been created for them to confirm the action.";
    }
}