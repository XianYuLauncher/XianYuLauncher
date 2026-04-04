using System.Text;
using System.Net;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public class ErrorAnalysisAIOrchestrator : IErrorAnalysisAIOrchestrator
{
    private const int MaxToolCallRounds = 16;
    private const int MaxHistoricalToolTraceChars = 50000;

    private readonly ILanguageSelectorService _languageSelectorService;
    private readonly ILogSanitizerService _logSanitizerService;
    private readonly IErrorAnalysisSessionContextQueryService _sessionContextQueryService;
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly IAISettingsDomainService _aiSettingsDomainService;
    private readonly ICrashAnalyzer _crashAnalyzer;
    private readonly IAgentActionExecutor _actionExecutor;
    private readonly IAgentToolDispatcher _toolDispatcher;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly ErrorAnalysisSessionState _sessionState;

    public ErrorAnalysisAIOrchestrator(
        ILanguageSelectorService languageSelectorService,
        ILogSanitizerService logSanitizerService,
        IErrorAnalysisSessionContextQueryService sessionContextQueryService,
        IAIAnalysisService aiAnalysisService,
        IAISettingsDomainService aiSettingsDomainService,
        ICrashAnalyzer crashAnalyzer,
        IAgentActionExecutor actionExecutor,
        IAgentToolDispatcher toolDispatcher,
        IUiDispatcher uiDispatcher,
        ErrorAnalysisSessionState sessionState)
    {
        _languageSelectorService = languageSelectorService;
        _logSanitizerService = logSanitizerService;
        _sessionContextQueryService = sessionContextQueryService;
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
        if (!_sessionState.Context.IsGameCrashed)
        {
            return;
        }

        var preserveExistingConversation = _sessionState.ChatMessages.Any(message => message.IsUser);

        try
        {
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                if (!preserveExistingConversation)
                {
                    _sessionState.ChatMessages.Clear();
                }

                _sessionState.AIAnalysisResult = "正在分析崩溃原因...\n\n";
                _sessionState.ResetFixActions();
                _sessionState.ClearPendingToolContinuation();
            });

            await Task.Delay(50, cancellationToken);

            var settings = await _aiSettingsDomainService.LoadAsync();
            if (settings.IsEnabled)
            {
                await AnalyzeWithExternalAIAsync(settings, cancellationToken);
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
                var msg = "ErrorAnalysis_AnalysisFailedText".GetLocalized(ex.Message);
                if (_sessionState.ChatMessages.Any())
                {
                    _sessionState.ChatMessages.Add(new UiChatMessage("assistant", $"\n\n{msg}"));
                }

                _sessionState.AIAnalysisResult += $"\n\n{msg}";
            });
        }
    }

    public async Task SendMessageAsync(string userMessage, IReadOnlyList<ChatImageAttachment> imageAttachments, CancellationToken cancellationToken)
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _sessionState.ResetFixActions();
            _sessionState.ClearPendingToolContinuation();
            _sessionState.ChatMessages.Add(new UiChatMessage("user", userMessage, imageAttachments: imageAttachments));
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
        catch (Exception ex)
        {
            await SetLastAssistantMessageAsync("ErrorAnalysis_AnalysisFailedText".GetLocalized(ex.Message));
        }
    }

    public async Task ApproveActionAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var pendingContinuation = _sessionState.PeekPendingToolContinuation();
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
        var pendingContinuation = _sessionState.PeekPendingToolContinuation();
        await RunActionDecisionAsync(
            pendingContinuation,
            proposal: null,
            approved: false,
            rejectedActionText,
            _ => Task.FromResult(BuildRejectedActionMessage(rejectedActionText)),
            cancellationToken);
    }

    private async Task AnalyzeWithExternalAIAsync(
        AISettingsState settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                const string msg = "未配置 API Key，无法进行 AI 分析。";
                _sessionState.ChatMessages.Add(new UiChatMessage("assistant", msg));
                _sessionState.AIAnalysisResult = msg;
            });
            return;
        }

        var crashPrompt = await _sessionContextQueryService.BuildCrashPromptAsync(_sessionState.Context, cancellationToken);

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _sessionState.AIAnalysisResult = "正在进行外部 AI 分析...\n\n";
            _sessionState.ResetFixActions();
            _sessionState.ClearPendingToolContinuation();
            _sessionState.ChatMessages.Add(new UiChatMessage("user", crashPrompt));
            _sessionState.ChatMessages.Add(new UiChatMessage("assistant", "..."));
        });

        await Task.Delay(50, cancellationToken);
        await StreamResponseToLastMessageAsync(settings, cancellationToken);
    }

    private async Task AnalyzeWithKnowledgeBaseAsync(CancellationToken cancellationToken)
    {
        var (gameOutput, gameError) = _sessionState.CreateLogSnapshot();
        var crashPrompt = await _sessionContextQueryService.BuildCrashPromptAsync(_sessionState.Context, cancellationToken);

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            _sessionState.ChatMessages.Add(new UiChatMessage("user", crashPrompt));
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
                            SetAssistantMessageContent(lastMsg, string.Empty);
                        }

                        isFirstChunk = false;
                    }

                    AppendAssistantMessageContent(lastMsg, text);
                    if (_sessionState.ChatMessages.Count <= 2)
                    {
                        _sessionState.AIAnalysisResult = lastMsg.Content;
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

                AppendAssistantMessageContent(lastMsg, remaining);
                if (_sessionState.ChatMessages.Count <= 2)
                {
                    _sessionState.AIAnalysisResult = lastMsg.Content;
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

    private async Task StreamResponseToLastMessageAsync(
        AISettingsState settings,
        CancellationToken cancellationToken,
        List<ChatMessage>? initialApiMessages = null,
        bool hasRetriedWithoutImages = false)
    {
        var apiMessages = initialApiMessages ?? await BuildApiMessagesAsync(cancellationToken);
        var tools = _toolDispatcher.GetAvailableTools();
        try
        {
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
                                        SetAssistantMessageContent(lastMsg, string.Empty);
                                    }

                                    isFirstChunk = false;
                                }

                                AppendAssistantMessageContent(lastMsg, textToAppend);
                                if (_sessionState.ChatMessages.Count <= 3)
                                {
                                    _sessionState.AIAnalysisResult = lastMsg.Content;
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
                            SetAssistantMessageContent(lastMsg, string.Empty);
                        }

                        AppendAssistantMessageContent(lastMsg, finalText);
                        if (_sessionState.ChatMessages.Count <= 3)
                        {
                            _sessionState.AIAnalysisResult = lastMsg.Content;
                        }
                    });
                }

                if (contentBuilder.Length == 0 && (pendingToolCalls == null || pendingToolCalls.Count == 0))
                {
                    await SetEmptyAssistantResponseMessageAsync();
                    break;
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
                    lastAssistant.AIHistoryContent = string.IsNullOrWhiteSpace(lastAssistant.Content) ? null : lastAssistant.Content;
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
                            AIHistoryContent = string.Empty,
                            ToolInputContent = toolCall.Arguments
                        };
                        _sessionState.ChatMessages.Add(toolUiMessage);
                    });

                    var result = await _toolDispatcher.ExecuteAsync(toolCall, cancellationToken);
                    await _uiDispatcher.RunOnUiThreadAsync(() =>
                    {
                        if (toolUiMessage != null)
                        {
                            toolUiMessage.AIHistoryContent = result.Message;
                            toolUiMessage.ToolOutputContent = result.Message;
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
        catch (AiAnalysisRequestException ex) when (!hasRetriedWithoutImages)
        {
            var fallback = await PrepareTextOnlyRetryAsync(apiMessages, ex);
            if (!fallback.Handled)
            {
                throw;
            }

            if (fallback.RetryMessages == null)
            {
                return;
            }

            await Task.Delay(50, cancellationToken);
            await StreamResponseToLastMessageAsync(settings, cancellationToken, fallback.RetryMessages, hasRetriedWithoutImages: true);
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
                await _uiDispatcher.RunOnUiThreadAsync(_sessionState.ResetFixActions);
                await AppendStandaloneActionResultAsync(actionResult);
                return;
            }

            var continuationUserMessage = BuildContinuationUserMessage(approved, proposal, rejectedActionText, actionResult);
            AgentActionProposal? nextProposal = null;
            int remainingProposalCount = 0;
            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                _sessionState.AppendPendingToolContinuationUserMessage(new ChatMessage("user", continuationUserMessage));
                nextProposal = _sessionState.CompleteCurrentActionProposal();
                remainingProposalCount = _sessionState.PendingActionProposalCount;
            });

            if (nextProposal != null)
            {
                var assistantPrefix = string.IsNullOrWhiteSpace(actionResult)
                    ? approved && proposal != null
                        ? $"已执行：{proposal.ButtonText}"
                        : BuildRejectedActionMessage(rejectedActionText)
                    : ExtractDisplayMessage(actionResult);

                var nextPendingMessage = BuildPendingActionDisplayMessage(nextProposal, remainingProposalCount);
                await SetLastAssistantMessageAsync($"{assistantPrefix}\n\n{nextPendingMessage}");
                return;
            }

            pendingContinuation = _sessionState.TakePendingToolContinuation();
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
            SetAssistantMessageContent(
                lastMessage,
                string.IsNullOrWhiteSpace(assistantPrefix)
                    ? "..."
                    : assistantPrefix + "\n\n");
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

    private async Task<List<ChatMessage>> BuildApiMessagesAsync(CancellationToken cancellationToken)
    {
        List<ChatMessage> apiMessages = [];
        string languageForAi = _languageSelectorService.Language == "zh-CN" ? "Simplified Chinese" : "English";
        var settings = await _aiSettingsDomainService.LoadAsync();
        apiMessages.Add(new ChatMessage("system", BuildSystemPrompt(settings, languageForAi)));

        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            int count = Math.Max(0, _sessionState.ChatMessages.Count - 1);
            var historyMessages = _sessionState.ChatMessages
                .Take(count)
                .Where(msg => msg.Role != "system"
                    && (msg.IncludeInAIHistory
                        || msg.IsTool
                        || (msg.IsAssistant && msg.ToolCalls != null && msg.ToolCalls.Count > 0)))
                .ToList();
            var trimmedTraceIndices = GetTrimmedToolTraceMessageIndices(historyMessages);
            HashSet<string> validToolCallIds = [];

            for (int index = 0; index < historyMessages.Count; index++)
            {
                var msg = historyMessages[index];

                if (trimmedTraceIndices.Contains(index))
                {
                    if (msg.IsAssistant && msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                    {
                        var preservedContent = string.IsNullOrWhiteSpace(msg.AIHistoryContent) ? null : msg.AIHistoryContent;
                        if (!string.IsNullOrWhiteSpace(preservedContent))
                        {
                            apiMessages.Add(new ChatMessage("assistant", preservedContent));
                        }
                    }

                    continue;
                }

                if (msg.IsAssistant && msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    var content = string.IsNullOrWhiteSpace(msg.AIHistoryContent) ? null : msg.AIHistoryContent;
                    var clonedToolCalls = CloneToolCalls(msg.ToolCalls) ?? [];
                    apiMessages.Add(new ChatMessage("assistant", content, clonedToolCalls));

                    foreach (var toolCall in clonedToolCalls)
                    {
                        if (!string.IsNullOrWhiteSpace(toolCall.Id))
                        {
                            validToolCallIds.Add(toolCall.Id);
                        }
                    }

                    continue;
                }

                var historyContent = GetMessageHistoryContent(msg);
                var historyAttachments = CloneImageAttachments(msg.AIHistoryImageAttachments ?? msg.ImageAttachments);

                if (msg.IsTool)
                {
                    if (string.IsNullOrWhiteSpace(msg.ToolCallId)
                        || !validToolCallIds.Contains(msg.ToolCallId))
                    {
                        continue;
                    }

                    var toolResultContent = msg.AIHistoryContent ?? msg.Content ?? string.Empty;
                    apiMessages.Add(ChatMessage.ToolResult(msg.ToolCallId, toolResultContent));
                    validToolCallIds.Remove(msg.ToolCallId);
                    continue;
                }

                if (msg.IsUser
                    && string.IsNullOrWhiteSpace(historyContent)
                    && historyAttachments.Count == 0)
                {
                    continue;
                }

                apiMessages.Add(new ChatMessage(msg.Role, historyContent)
                {
                    ImageAttachments = historyAttachments
                });
            }
        });

        await EnsureImageDataUrlsAsync(apiMessages, cancellationToken);

        return apiMessages;
    }

    private static async Task EnsureImageDataUrlsAsync(IEnumerable<ChatMessage> apiMessages, CancellationToken cancellationToken)
    {
        foreach (var message in apiMessages)
        {
            if (message.ImageAttachments.Count == 0)
            {
                continue;
            }

            foreach (var attachment in message.ImageAttachments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrWhiteSpace(attachment.DataUrl)
                    || string.IsNullOrWhiteSpace(attachment.FilePath)
                    || !File.Exists(attachment.FilePath))
                {
                    continue;
                }

                try
                {
                    var bytes = await File.ReadAllBytesAsync(attachment.FilePath, cancellationToken);
                    var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                        ? GetImageContentTypeFromPath(attachment.FilePath)
                        : attachment.ContentType;
                    attachment.ContentType = contentType;
                    attachment.DataUrl = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LauncherAIPersistence] 读取图片附件失败: {attachment.FilePath}, {ex.Message}");
                }
            }
        }
    }

    private static string GetImageContentTypeFromPath(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "image/png"
        };
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
        return (message.AIHistoryContent ?? message.Content ?? string.Empty).Length;
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
                _sessionState.AIAnalysisResult = displayMessage;
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
        lastMessage.SuppressContentRendering = true;
        lastMessage.IncludeInAIHistory = false;

        if (_sessionState.ChatMessages.Count <= 3)
        {
            _sessionState.AIAnalysisResult = lastMessage.Content;
        }
    }

    private static string BuildPendingActionDisplayMessage(
        IReadOnlyList<AgentActionProposal> actionProposals,
        IReadOnlyList<string> actionProposalMessages)
    {
        if (actionProposals.Count == 0)
        {
            return "已准备待确认操作，等待用户确认。";
        }

        var firstMessage = actionProposalMessages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(ExtractDisplayMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        var firstProposal = actionProposals[0];
        if (!string.IsNullOrWhiteSpace(firstMessage) && string.IsNullOrWhiteSpace(firstProposal.DisplayMessage))
        {
            firstProposal = new AgentActionProposal
            {
                ActionType = firstProposal.ActionType,
                ButtonText = firstProposal.ButtonText,
                DisplayMessage = firstMessage,
                PermissionLevel = firstProposal.PermissionLevel,
                Parameters = new Dictionary<string, string>(firstProposal.Parameters, StringComparer.OrdinalIgnoreCase)
            };
        }

        return BuildPendingActionDisplayMessage(firstProposal, actionProposals.Count);
    }

    private static string BuildPendingActionDisplayMessage(AgentActionProposal proposal, int pendingActionCount)
    {
        var displayMessage = ExtractDisplayMessage(proposal.DisplayMessage);
        if (string.IsNullOrWhiteSpace(displayMessage))
        {
            displayMessage = $"已准备执行“{proposal.ButtonText}”，等待用户确认。";
        }

        if (pendingActionCount <= 1)
        {
            return displayMessage;
        }

        return $"{displayMessage}\n\n当前操作处理后，还会继续逐项确认剩余 {pendingActionCount - 1} 个操作。";
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
                SetAssistantMessageContent(lastAssistant, content);
            }
        });
    }

    private async Task AppendCancellationMessageAsync()
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            if (_sessionState.TryConsumeCancellationMessageSuppression())
            {
                return;
            }

            var lastAssistant = GetLastAssistantMessage();
            if (lastAssistant != null)
            {
                AppendAssistantMessageContent(lastAssistant, "\n\n" + "ErrorAnalysis_AnalysisCanceledText".GetLocalized());
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

    private static List<ChatImageAttachment> CloneImageAttachments(IEnumerable<ChatImageAttachment>? imageAttachments)
    {
        return imageAttachments?.Select(image => new ChatImageAttachment
        {
            FileName = image.FileName,
            FilePath = image.FilePath,
            ContentType = image.ContentType,
            DataUrl = image.DataUrl
        }).ToList() ?? [];
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

    private async Task<ImageFallbackPreparationResult> PrepareTextOnlyRetryAsync(List<ChatMessage> apiMessages, AiAnalysisRequestException exception)
    {
        if (!ContainsImageAttachments(apiMessages) || !IsImageInputUnsupportedError(exception))
        {
            return ImageFallbackPreparationResult.NotHandled;
        }

        bool canRetry = true;
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            foreach (var message in _sessionState.ChatMessages.Where(message => message.IsUser && message.HasImageAttachments))
            {
                message.AIHistoryImageAttachments = [];
            }

            var latestUserMessage = _sessionState.ChatMessages.LastOrDefault(message => message.IsUser);
            var latestUserText = latestUserMessage?.AIHistoryContent ?? latestUserMessage?.Content;
            canRetry = !string.IsNullOrWhiteSpace(latestUserText);

            var lastAssistant = GetLastAssistantMessage();
            if (lastAssistant != null)
            {
                lastAssistant.Content = (canRetry
                        ? "ErrorAnalysis_ImageFallbackRetryText"
                        : "ErrorAnalysis_ImageFallbackRequiresUserText")
                    .GetLocalized();
                lastAssistant.IncludeInAIHistory = false;
                lastAssistant.AIHistoryContent = null;
            }

            if (canRetry)
            {
                EnsureTrailingAssistantMessage();
            }
        });

        if (!canRetry)
        {
            return new ImageFallbackPreparationResult(true, null);
        }

        return new ImageFallbackPreparationResult(true, StripImageAttachments(apiMessages));
    }

    private static bool ContainsImageAttachments(IEnumerable<ChatMessage> apiMessages)
    {
        return apiMessages.Any(message => message.ImageAttachments.Count > 0);
    }

    private static bool IsImageInputUnsupportedError(AiAnalysisRequestException exception)
    {
        if (exception.StatusCode != HttpStatusCode.BadRequest)
        {
            return false;
        }

        var body = exception.ResponseBody ?? exception.Message;
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        return body.Contains("image_url", StringComparison.OrdinalIgnoreCase)
            || body.Contains("expected `text`", StringComparison.OrdinalIgnoreCase)
            || body.Contains("does not support image", StringComparison.OrdinalIgnoreCase)
            || body.Contains("multimodal", StringComparison.OrdinalIgnoreCase)
            || body.Contains("vision", StringComparison.OrdinalIgnoreCase);
    }

    private static List<ChatMessage> StripImageAttachments(IEnumerable<ChatMessage> apiMessages)
    {
        List<ChatMessage> strippedMessages = [];
        foreach (var message in apiMessages)
        {
            var cloned = new ChatMessage(message.Role, message.Content, message.ToolCalls != null ? CloneToolCalls(message.ToolCalls) : null)
            {
                ToolCallId = message.ToolCallId
            };

            if (message.Role == "user")
            {
                cloned.ImageAttachments = [];
                if (string.IsNullOrWhiteSpace(cloned.Content))
                {
                    continue;
                }
            }
            else
            {
                cloned.ImageAttachments = CloneImageAttachments(message.ImageAttachments);
            }

            strippedMessages.Add(cloned);
        }

        return strippedMessages;
    }

    private readonly record struct ImageFallbackPreparationResult(bool Handled, List<ChatMessage>? RetryMessages)
    {
        public static ImageFallbackPreparationResult NotHandled => new(false, null);
    }

    private async Task SetEmptyAssistantResponseMessageAsync()
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            var lastAssistant = GetLastAssistantMessage();
            if (lastAssistant == null)
            {
                return;
            }

            var emptyResponseMessage = "ErrorAnalysis_EmptyAssistantResponseText".GetLocalized();
            if (string.IsNullOrWhiteSpace(lastAssistant.Content) || lastAssistant.Content == "...")
            {
                SetAssistantMessageContent(lastAssistant, emptyResponseMessage);
            }
            else if (!lastAssistant.Content.Contains(emptyResponseMessage, StringComparison.Ordinal))
            {
                SetAssistantMessageContent(lastAssistant, $"{lastAssistant.Content.TrimEnd()}\n\n{emptyResponseMessage}");
            }

            if (_sessionState.ChatMessages.Count <= 3)
            {
                _sessionState.AIAnalysisResult = lastAssistant.Content;
            }
        });
    }

    private static string GetMessageHistoryContent(UiChatMessage message)
    {
        if (message.IsAssistant && (message.ToolCalls == null || message.ToolCalls.Count == 0))
        {
            return message.Content ?? string.Empty;
        }

        return message.AIHistoryContent ?? message.Content ?? string.Empty;
    }

    private static void SetAssistantMessageContent(UiChatMessage message, string content)
    {
        message.Content = content;

        if (!message.IncludeInAIHistory)
        {
            return;
        }

        if (message.ToolCalls != null && message.ToolCalls.Count > 0)
        {
            return;
        }

        message.AIHistoryContent = content;
    }

    private static void AppendAssistantMessageContent(UiChatMessage message, string content)
    {
        SetAssistantMessageContent(message, (message.Content ?? string.Empty) + content);
    }

    private static string BuildSystemPrompt(AISettingsState settings, string language)
    {
        return string.IsNullOrWhiteSpace(settings.SystemPrompt)
            ? BuildDefaultFunctionCallingSystemPrompt(language)
            : settings.SystemPrompt.Trim();
    }

    private static string BuildDefaultFunctionCallingSystemPrompt(string language)
    {
        return
            "You are an expert Minecraft technical support agent built into XianYu Launcher. " +
            "XianYu Launcher is the product's global brand name — always refer to it as \"XianYu Launcher\", never translate or localize this name. " +
            $"Analyze crash logs and help users fix issues. Respond in {language}. " +
            "Be concise and reference specific mods or config files if they are responsible. " +
            "\n\n" +
            "You may use the provided tools when helpful.\n\n" +
            "CRITICAL RULES:\n" +
            "1. Start from the provided launch summary and truncated log tail. Call 'getLaunchContext' only when launch parameters are needed. Its include_classpath parameter defaults to false and should only be set true when the log clearly points to class-loading or missing-dependency issues.\n" +
            "2. If the current log excerpt is insufficient, call 'getLogTail' or 'getLogChunk' before concluding. Do not pretend to have seen the full log if you have not requested it.\n" +
            "3. ONLY use the 'switchJava' tool if the crash log explicitly indicates a Java version mismatch or runtime error. Do not guess.\n" +
            "4. For instance-local community resource management, prefer 'getInstances' -> 'getInstanceCommunityResources' -> 'checkInstanceCommunityResourceUpdates' -> 'updateInstanceCommunityResources'. For existing modpack instances, prefer 'getInstances' or 'getVersionConfig' to confirm is_modpack and modpack metadata first, then use 'checkModpackUpdate' -> 'updateModpack'. If a long-running tool returns operation_id, prefer 'sleep' before the next 'getOperationStatus' instead of querying status again immediately. If the user wants to search remote resources by category, call 'getCommunityResourceTags' first, then pass categories[*].token into 'searchCommunityResources.category_tokens'. If the user wants to browse what is currently popular, you may call 'searchCommunityResources' without query. When the user wants to inspect a remote resource's detailed categories, loaders, compatibility, or description before deciding, call 'getCommunityResourceProjectDetail'. Continue with 'getCommunityResourceFiles' -> 'installCommunityResource' only after the project choice is clear. For datapack installs, explicitly follow 'getInstances' -> 'getInstanceCommunityResources(resource_types=[\"world\"])' -> 'getCommunityResourceFiles' -> 'installCommunityResource(resource_type=datapack, target_version_name or target_version_path, target_world_resource_id)'; if you need progress afterwards, use 'sleep' -> 'getOperationStatus', and always use the selected world item's resource_instance_id as target_world_resource_id. For world installs, confirm the target instance explicitly via target_version_name or target_version_path before execution. For modpacks, continue with 'getCommunityResourceFiles' -> 'installModpack'; its target_version_name must be a NEW instance name instead of an existing instance. Only use 'searchModrinthProject' when the goal is to open the UI detail page.\n" +
            "5. The 'searchModrinthProject' tool is strictly for opening MOD / SHADER / RESOURCE PACK detail pages. It CANNOT search for Mod Loaders (Forge, Fabric, NeoForge, Quilt). Explain manual installation for loaders if needed.\n" +
            "6. If the user explicitly asks to install a specific Minecraft version, call 'getGameManifest' with queryType='list' and searchText set to that version string before 'installGame'. Use latest_release/latest_snapshot only when the user explicitly asks for latest release or latest snapshot.\n" +
            "7. 'installCommunityResource' V1 supports mod, resourcepack, shader, world, and datapack; modpack installation should use 'installModpack'. World installation requires an explicit target instance via target_version_name or target_version_path and installs into the instance saves directory with auto-rename on name conflicts. Datapack installation requires both the target instance and target_world_resource_id, where target_world_resource_id must come from a world entry returned by 'getInstanceCommunityResources'; treat world_name as display-only instead of a stable selector. 'getInstanceCommunityResources' can list world and datapack entries, but 'checkInstanceCommunityResourceUpdates' and 'updateInstanceCommunityResources' only execute updates for mod, shader, and resourcepack; world/datapack should be explained as unsupported instead of hidden. 'updateModpack' only applies to existing modpack instances, and when choosing a non-latest target version it only accepts 'checkModpackUpdate' returned candidates[*].source_version_id.\n" +
            "8. If you cannot fix the issue via tools, provide clear manual instructions. If the problem persists, advise the user to click the 'Contact Author' (联系作者) button at the top.\n" +
            "9. Never fabricate tool calls, tool execution, or tool results. Only describe a tool as executed, succeeded, failed, rejected, cancelled, or completed when you have the real tool result for that exact tool call; otherwise say you do not have the result yet.\n" +
            "10. Before changing launch settings, read the relevant snapshot first: use 'getGlobalLaunchSettings' for global settings; use 'getInstances' and 'getVersionConfig' for instance settings; use 'getEffectiveLaunchSettings' only when you need the final applied values or source.\n" +
            "11. For settings tools, prefer stable IDs over raw paths whenever available: use java_id, path_id, and target_version_name before absolute paths.\n" +
            "12. 'switchMinecraftPath', 'patchGlobalLaunchSettings', and 'patchInstanceLaunchSettings' only create confirmation proposals. After one of these tools returns a proposal, stop requesting further confirmation-required changes until the user approves or rejects it.\n" +
            "13. For long-running operations that are still non-terminal (for example queued, downloading, or running), do not spam 'getOperationStatus'. Use 'sleep' once before the next status query. If the operation is still non-terminal after that follow-up, tell the user it continues in the background and stop automatic polling in the current turn.";
    }
}