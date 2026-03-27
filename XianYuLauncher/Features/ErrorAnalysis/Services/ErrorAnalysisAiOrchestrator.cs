using System.Text;
using Newtonsoft.Json.Linq;
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
    private const int MaxHistoricalToolTraceChars = 10000;

    private readonly ILanguageSelectorService _languageSelectorService;
    private readonly ILogSanitizerService _logSanitizerService;
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly IAiSettingsDomainService _aiSettingsDomainService;
    private readonly ICrashAnalyzer _crashAnalyzer;
    private readonly IAgentActionExecutor _actionExecutor;
    private readonly IAgentToolDispatcher _toolDispatcher;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly ErrorAnalysisSessionState _sessionState;

    public ErrorAnalysisAiOrchestrator(
        ILanguageSelectorService languageSelectorService,
        ILogSanitizerService logSanitizerService,
        IAIAnalysisService aiAnalysisService,
        IAiSettingsDomainService aiSettingsDomainService,
        ICrashAnalyzer crashAnalyzer,
        IAgentActionExecutor actionExecutor,
        IAgentToolDispatcher toolDispatcher,
        IUiDispatcher uiDispatcher,
        ErrorAnalysisSessionState sessionState)
    {
        _languageSelectorService = languageSelectorService;
        _logSanitizerService = logSanitizerService;
        _aiAnalysisService = aiAnalysisService;
        _aiSettingsDomainService = aiSettingsDomainService;
        _crashAnalyzer = crashAnalyzer;
        _actionExecutor = actionExecutor;
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
                _sessionState.ClearPendingToolContinuation();
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
            _sessionState.ClearPendingToolContinuation();
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

    public async Task ApproveActionAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var pendingContinuation = _sessionState.TakePendingToolContinuation();
        await RunActionDecisionAsync(
            pendingContinuation,
            proposal,
            approved: true,
            rejectedActionText: null,
            async token =>
            {
                try
                {
                    return await _actionExecutor.ExecuteAsync(proposal, token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return $"执行 {proposal.ButtonText} 失败：{ex.Message}";
                }
            },
            cancellationToken);
    }

    public async Task RejectPendingActionAsync(string? rejectedActionText, CancellationToken cancellationToken)
    {
        var pendingContinuation = _sessionState.TakePendingToolContinuation();
        await RunActionDecisionAsync(
            pendingContinuation,
            proposal: null,
            approved: false,
            rejectedActionText,
            _ => Task.FromResult(BuildRejectedActionMessage(rejectedActionText)),
            cancellationToken);
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
            _sessionState.ClearPendingToolContinuation();
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
                    var lastMsg = GetLastAssistantMessage();
                    if (lastMsg == null)
                    {
                        return;
                    }

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
                        _sessionState.AiAnalysisResult = lastMsg.Content;
                    }
                });
            }
        }

        if (buffered.Length > 0)
        {
            var remaining = buffered.ToString();
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                var lastMsg = GetLastAssistantMessage();
                if (lastMsg == null)
                {
                    return;
                }

                lastMsg.Content += remaining;
                if (_sessionState.ChatMessages.Count <= 1)
                {
                    _sessionState.AiAnalysisResult = lastMsg.Content;
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
        CancellationToken cancellationToken,
        List<ChatMessage>? initialApiMessages = null)
    {
        var apiMessages = initialApiMessages ?? await BuildApiMessagesAsync();
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
                            var lastMsg = GetLastAssistantMessage();
                            if (lastMsg == null)
                            {
                                return;
                            }

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
                    var lastMsg = GetLastAssistantMessage();
                    if (lastMsg == null)
                    {
                        return;
                    }

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

            var assistantToolCallMessage = new ChatMessage("assistant", contentBuilder.Length > 0 ? contentBuilder.ToString() : null, pendingToolCalls);
            apiMessages.Add(assistantToolCallMessage);

            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                var lastAssistant = GetLastAssistantMessage();
                if (lastAssistant == null)
                {
                    return;
                }

                lastAssistant.ToolCalls = CloneToolCalls(pendingToolCalls);
                lastAssistant.AiHistoryContent = string.IsNullOrWhiteSpace(lastAssistant.Content) ? null : lastAssistant.Content;
            });

            List<AgentActionProposal> actionProposals = [];
            List<string> actionProposalMessages = [];
            foreach (var toolCall in pendingToolCalls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                UiChatMessage? toolUiMessage = null;
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    RemoveTrailingAssistantPlaceholderIfNeeded();
                    toolUiMessage = new UiChatMessage("tool", toolCall.FunctionName)
                    {
                        ToolCallId = toolCall.Id,
                        AiHistoryContent = string.Empty
                    };
                    _sessionState.ChatMessages.Add(toolUiMessage);
                });

                var result = await _toolDispatcher.ExecuteAsync(toolCall, cancellationToken);
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    if (toolUiMessage != null)
                    {
                        toolUiMessage.AiHistoryContent = result.Message;
                    }
                });

                if (result.ActionProposal != null)
                {
                    actionProposals.Add(result.ActionProposal);
                    if (!string.IsNullOrWhiteSpace(result.Message))
                    {
                        actionProposalMessages.Add(result.Message);
                    }
                }

                apiMessages.Add(ChatMessage.ToolResult(toolCall.Id, result.Message));
            }

            if (actionProposals.Count > 0)
            {
                var pendingActionMessage = BuildPendingActionDisplayMessage(actionProposals, actionProposalMessages);
                assistantToolCallMessage.Content = null;

                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    EnsureTrailingAssistantMessage();
                    SetPendingActionMessageOnLastAssistant(pendingActionMessage);
                    _sessionState.ApplyActionProposals(actionProposals);
                    _sessionState.SetPendingToolContinuation(apiMessages);
                });

                return;
            }

            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                EnsureTrailingAssistantMessage();
            });

            await Task.Delay(50, cancellationToken);
        }
    }

    private async Task RunActionDecisionAsync(
        AgentConversationContinuation? pendingContinuation,
        AgentActionProposal? proposal,
        bool approved,
        string? rejectedActionText,
        Func<CancellationToken, Task<string>> actionExecutor,
        CancellationToken cancellationToken)
    {
        try
        {
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                _sessionState.IsAiAnalyzing = true;
                _sessionState.IsChatEnabled = false;
                _sessionState.ResetFixActions();

                if (pendingContinuation != null)
                {
                    _sessionState.ChatMessages.Add(new UiChatMessage(
                        "assistant",
                        approved && proposal != null
                            ? $"正在执行：{proposal.ButtonText}..."
                            : "正在根据你的选择继续分析..."));
                }
            });

            await Task.Delay(50, cancellationToken);
            var actionResult = await actionExecutor(cancellationToken);

            if (pendingContinuation == null)
            {
                await AppendStandaloneActionResultAsync(actionResult);
                return;
            }

            await ContinuePendingConversationAsync(
                pendingContinuation,
                proposal,
                approved,
                rejectedActionText,
                actionResult,
                cancellationToken);
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

    private async Task ContinuePendingConversationAsync(
        AgentConversationContinuation pendingContinuation,
        AgentActionProposal? proposal,
        bool approved,
        string? rejectedActionText,
        string actionResult,
        CancellationToken cancellationToken)
    {
        var assistantPrefix = string.IsNullOrWhiteSpace(actionResult)
            ? approved && proposal != null
                ? $"已执行：{proposal.ButtonText}"
                : BuildRejectedActionMessage(rejectedActionText)
            : ExtractDisplayMessage(actionResult);

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            if (!_sessionState.ChatMessages.Any())
            {
                return;
            }

            var lastMessage = _sessionState.ChatMessages.Last();
            lastMessage.Content = string.IsNullOrWhiteSpace(assistantPrefix)
                ? "..."
                : assistantPrefix + "\n\n";
        });

        var settings = await _aiSettingsDomainService.LoadAsync();
        if (!settings.IsEnabled)
        {
            await SetLastAssistantMessageAsync($"{assistantPrefix}\n\n当前为知识库模式，无法继续 AI 对话。请在设置中开启 AI 分析。");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            await SetLastAssistantMessageAsync($"{assistantPrefix}\n\nAPI Key Missing.");
            return;
        }

        pendingContinuation.ApiMessages.Add(new ChatMessage(
            "user",
            BuildContinuationUserMessage(approved, proposal, rejectedActionText, actionResult)));

        await Task.Delay(50, cancellationToken);
        await StreamResponseToLastMessageAsync(settings, cancellationToken, pendingContinuation.ApiMessages);
    }

    private async Task<List<ChatMessage>> BuildApiMessagesAsync()
    {
        List<ChatMessage> apiMessages = [];
        string languageForAi = _languageSelectorService.Language == "zh-CN" ? "Simplified Chinese" : "English";
        apiMessages.Add(new ChatMessage("system", BuildFunctionCallingSystemPrompt(languageForAi)));

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            int count = Math.Max(0, _sessionState.ChatMessages.Count - 1);
            var historyMessages = _sessionState.ChatMessages
                .Take(count)
                .Where(msg => msg.Role != "system" && msg.IncludeInAiHistory)
                .ToList();
            var trimmedTraceIndices = GetTrimmedToolTraceMessageIndices(historyMessages);

            for (int index = 0; index < historyMessages.Count; index++)
            {
                var msg = historyMessages[index];

                if (trimmedTraceIndices.Contains(index))
                {
                    if (msg.IsAssistant && msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                    {
                        var preservedContent = string.IsNullOrWhiteSpace(msg.AiHistoryContent) ? null : msg.AiHistoryContent;
                        if (!string.IsNullOrWhiteSpace(preservedContent))
                        {
                            apiMessages.Add(new ChatMessage("assistant", preservedContent));
                        }
                    }

                    continue;
                }

                if (msg.IsAssistant && msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    var content = string.IsNullOrWhiteSpace(msg.AiHistoryContent) ? null : msg.AiHistoryContent;
                    apiMessages.Add(new ChatMessage("assistant", content, CloneToolCalls(msg.ToolCalls)));
                    continue;
                }

                if (msg.IsTool)
                {
                    if (string.IsNullOrWhiteSpace(msg.ToolCallId))
                    {
                        continue;
                    }

                    var toolResultContent = msg.AiHistoryContent ?? msg.Content ?? string.Empty;
                    apiMessages.Add(ChatMessage.ToolResult(msg.ToolCallId, toolResultContent));
                    continue;
                }

                apiMessages.Add(new ChatMessage(msg.Role, msg.Content ?? string.Empty));
            }
        });

        return apiMessages;
    }

    private static HashSet<int> GetTrimmedToolTraceMessageIndices(IReadOnlyList<UiChatMessage> historyMessages)
    {
        var trimmedIndices = new HashSet<int>();
        int currentTurnStartIndex = FindLastUserMessageIndex(historyMessages);
        if (currentTurnStartIndex <= 0)
        {
            return trimmedIndices;
        }

        Queue<ToolTraceRange> traceQueue = [];
        int totalToolChars = 0;

        for (int index = 0; index < currentTurnStartIndex; index++)
        {
            var message = historyMessages[index];
            if (!message.IsAssistant || message.ToolCalls == null || message.ToolCalls.Count == 0)
            {
                continue;
            }

            int traceEndIndex = index;
            int traceToolChars = 0;
            for (int toolIndex = index + 1; toolIndex < currentTurnStartIndex; toolIndex++)
            {
                if (!historyMessages[toolIndex].IsTool)
                {
                    break;
                }

                traceEndIndex = toolIndex;
                traceToolChars += GetToolHistoryContentLength(historyMessages[toolIndex]);
            }

            traceQueue.Enqueue(new ToolTraceRange(index, traceEndIndex, traceToolChars));
            totalToolChars += traceToolChars;
            index = traceEndIndex;
        }

        while (totalToolChars > MaxHistoricalToolTraceChars && traceQueue.Count > 0)
        {
            var trimmedTrace = traceQueue.Dequeue();
            for (int index = trimmedTrace.StartIndex; index <= trimmedTrace.EndIndex; index++)
            {
                trimmedIndices.Add(index);
            }

            totalToolChars -= trimmedTrace.ToolContentLength;
        }

        return trimmedIndices;
    }

    private static int FindLastUserMessageIndex(IReadOnlyList<UiChatMessage> historyMessages)
    {
        for (int index = historyMessages.Count - 1; index >= 0; index--)
        {
            if (historyMessages[index].IsUser)
            {
                return index;
            }
        }

        return -1;
    }

    private static int GetToolHistoryContentLength(UiChatMessage message)
    {
        return (message.AiHistoryContent ?? message.Content ?? string.Empty).Length;
    }

    private async Task AppendStandaloneActionResultAsync(string actionResult)
    {
        if (string.IsNullOrWhiteSpace(actionResult))
        {
            return;
        }

        var displayMessage = ExtractDisplayMessage(actionResult);

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _sessionState.ChatMessages.Add(new UiChatMessage("assistant", displayMessage));
            if (_sessionState.ChatMessages.Count <= 3)
            {
                _sessionState.AiAnalysisResult = displayMessage;
            }
        });
    }

    private void SetPendingActionMessageOnLastAssistant(string pendingActionMessage)
    {
        if (!_sessionState.ChatMessages.Any())
        {
            return;
        }

        var lastMessage = GetLastAssistantMessage();
        if (lastMessage == null)
        {
            return;
        }

        lastMessage.Content = string.IsNullOrWhiteSpace(pendingActionMessage)
            ? "已创建待确认操作，等待用户确认。"
            : pendingActionMessage;
        lastMessage.IncludeInAiHistory = false;

        if (_sessionState.ChatMessages.Count <= 3)
        {
            _sessionState.AiAnalysisResult = lastMessage.Content;
        }
    }

    private static string BuildPendingActionDisplayMessage(
        IReadOnlyList<AgentActionProposal> actionProposals,
        IReadOnlyList<string> actionProposalMessages)
    {
        var safeMessages = actionProposalMessages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(ExtractDisplayMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();

        if (safeMessages.Count > 0)
        {
            return string.Join("\n\n", safeMessages);
        }

        if (actionProposals.Count == 1)
        {
            return $"已准备执行“{actionProposals[0].ButtonText}”，等待用户确认。";
        }

        return "已准备多个待确认操作，等待用户确认。";
    }

    private static string ExtractDisplayMessage(string message)
    {
        var trimmed = message.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        if (trimmed[0] is not '{' and not '[')
        {
            return trimmed;
        }

        try
        {
            if (JToken.Parse(trimmed) is JObject obj)
            {
                var displayMessage = obj["message"]?.ToString();
                if (!string.IsNullOrWhiteSpace(displayMessage))
                {
                    return displayMessage.Trim();
                }
            }
        }
        catch
        {
        }

        return trimmed;
    }

    private static string BuildRejectedActionMessage(string? rejectedActionText)
    {
        return string.IsNullOrWhiteSpace(rejectedActionText)
            ? "已拒绝执行本轮待确认操作。"
            : $"已拒绝执行：{rejectedActionText.Trim()}";
    }

    private static string BuildContinuationUserMessage(
        bool approved,
        AgentActionProposal? proposal,
        string? rejectedActionText,
        string actionResult)
    {
        if (approved)
        {
            var buttonText = proposal?.ButtonText ?? "该操作";
            return string.IsNullOrWhiteSpace(actionResult)
                ? $"我已确认执行你刚才申请的操作“{buttonText}”。请继续。注意：这次确认只对这一个具体操作生效，不代表我同意你后续的其他操作。后面如果你还想调用任何需要确认的工具，仍然必须重新发起新的确认。"
                : $"我已确认执行你刚才申请的操作“{buttonText}”。操作结果如下：\n{actionResult}\n请基于这个结果继续。注意：这次确认只对这一个具体操作生效，不代表我同意你后续的其他操作。后面如果你还想调用任何需要确认的工具，仍然必须重新发起新的确认。";
        }

        var rejectedTarget = string.IsNullOrWhiteSpace(rejectedActionText)
            ? "本轮待确认操作"
            : rejectedActionText.Trim();
        return $"我拒绝了你刚才申请的操作“{rejectedTarget}”。请在不执行该操作的前提下继续给出下一步建议。";
    }

    private async Task SetLastAssistantMessageAsync(string content)
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            var lastAssistant = GetLastAssistantMessage();
            if (lastAssistant != null)
            {
                lastAssistant.Content = content;
            }
        });
    }

    private async Task AppendCancellationMessageAsync()
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            var lastAssistant = GetLastAssistantMessage();
            if (lastAssistant != null)
            {
                lastAssistant.Content += $"\n\n{GetLocalizedString("ErrorAnalysis_AnalysisCanceled.Text")}";
            }
        });
    }

    private UiChatMessage? GetLastAssistantMessage()
    {
        return _sessionState.ChatMessages.LastOrDefault(message => message.IsAssistant);
    }

    private static List<ToolCallInfo> CloneToolCalls(IEnumerable<ToolCallInfo> toolCalls)
    {
        return toolCalls
            .Select(toolCall => new ToolCallInfo
            {
                Id = toolCall.Id,
                FunctionName = toolCall.FunctionName,
                Arguments = toolCall.Arguments
            })
            .ToList();
    }

            private readonly record struct ToolTraceRange(int StartIndex, int EndIndex, int ToolContentLength);

    private void EnsureTrailingAssistantMessage()
    {
        if (_sessionState.ChatMessages.LastOrDefault()?.IsAssistant == true)
        {
            return;
        }

        _sessionState.ChatMessages.Add(new UiChatMessage("assistant", "..."));
    }

    private void RemoveTrailingAssistantPlaceholderIfNeeded()
    {
        if (_sessionState.ChatMessages.LastOrDefault() is not UiChatMessage lastAssistant
            || !lastAssistant.IsAssistant)
        {
            return;
        }

        if (lastAssistant.Content == "...")
        {
            _sessionState.ChatMessages.Remove(lastAssistant);
        }
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
            "You may use the provided tools when helpful.\n\n" +
            "CRITICAL RULES:\n" +
            "1. CHECK THE LAUNCH COMMAND FIRST! If the user has set invalid JVM arguments (e.g., nonsense in -Djava.library.path or -Xmx), TELL THEM TO FIX IT MANUALLY in the settings. Do NOT switch Java versions for bad arguments.\n" +
            "2. ONLY use the 'switchJava' tool if the crash log explicitly indicates a Java version mismatch or runtime error. Do not guess.\n" +
            "3. For AI-driven community resource installation, prefer the read-only tool chain 'search_community_resources' -> 'get_community_resource_files' -> 'get_instances' -> 'install_community_resource'. Only use 'searchModrinthProject' when the goal is to open the UI detail page, not when preparing a silent install.\n" +
            "4. The 'searchModrinthProject' tool is stricterly for opening MOD / SHADER / RESOURCE PACK detail pages. It CANNOT search for Mod Loaders (Forge, Fabric, NeoForge, Quilt). Explain manual installation for loaders if needed.\n" +
            "5. If the user explicitly asks to install a specific Minecraft version, call 'get_game_manifest' with queryType='list' and searchText set to that version string before 'install_game'. Use latest_release/latest_snapshot only when the user explicitly asks for latest release or latest snapshot.\n" +
            "6. 'install_community_resource' V1 only supports mod, resourcepack, and shader. Datapack / world / modpack installs are out of scope until dedicated selection tools exist.\n" +
            "7. If you cannot fix the issue via tools, provide clear manual instructions. If the problem persists, advise the user to click the 'Contact Author' (联系作者) button at the top.\n" +
            "8. Never fabricate tool calls, tool execution, or tool results. Only describe a tool as executed, succeeded, failed, rejected, cancelled, or completed when you have the real tool result for that exact tool call; otherwise say you do not have the result yet.";
    }
}