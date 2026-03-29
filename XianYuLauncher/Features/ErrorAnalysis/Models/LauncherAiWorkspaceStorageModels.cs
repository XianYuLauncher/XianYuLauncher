namespace XianYuLauncher.Features.ErrorAnalysis.Models;

public sealed class LauncherAiWorkspaceStorageModel
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public Guid? SelectedConversationId { get; init; }

    public Guid? ActiveErrorAnalysisConversationId { get; init; }

    public int NextConversationNumber { get; init; } = 1;

    public List<LauncherAiConversationIndexEntryStorageModel> Conversations { get; init; } = [];
}

public sealed class LauncherAiConversationIndexEntryStorageModel
{
    public Guid ConversationId { get; init; }

    public bool IsErrorAnalysisConversation { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset LastUpdatedAtUtc { get; init; }
}

public sealed class LauncherAiConversationStorageModel
{
    public int SchemaVersion { get; init; } = LauncherAiWorkspaceStorageModel.CurrentSchemaVersion;

    public Guid ConversationId { get; init; }

    public bool IsErrorAnalysisConversation { get; init; }

    public string Title { get; init; } = string.Empty;

    public string ToolTip { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset LastUpdatedAtUtc { get; init; }

    public LauncherAiConversationInterruptionStorageModel? Interruption { get; init; }

    public LauncherAiSessionStorageModel Session { get; init; } = new();
}

public sealed class LauncherAiConversationInterruptionStorageModel
{
    public string Kind { get; init; } = string.Empty;

    public DateTimeOffset InterruptedAtUtc { get; init; }

    public string? Message { get; init; }
}

public sealed class LauncherAiSessionStorageModel
{
    public string ChatInput { get; init; } = string.Empty;

    public bool IsChatEnabled { get; init; }

    public List<LauncherAiAttachmentStorageModel> PendingImageAttachments { get; init; } = [];

    public List<LauncherAiChatMessageStorageModel> ChatMessages { get; init; } = [];

    public List<LauncherAiActionProposalStorageModel> ActionProposals { get; init; } = [];
}

public sealed class LauncherAiChatMessageStorageModel
{
    public string Role { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public bool IncludeInAiHistory { get; init; } = true;

    public bool ShowRoleHeader { get; init; } = true;

    public string DisplayRoleText { get; init; } = string.Empty;

    public string? AiHistoryContent { get; init; }

    public string? ToolCallId { get; init; }

    public List<ToolCallInfo>? ToolCalls { get; init; }

    public List<LauncherAiAttachmentStorageModel> ImageAttachments { get; init; } = [];

    public List<LauncherAiAttachmentStorageModel>? AiHistoryImageAttachments { get; init; }

    public bool SuppressContentRendering { get; init; }
}

public sealed class LauncherAiAttachmentStorageModel
{
    public string FileName { get; init; } = string.Empty;

    public string RelativeFilePath { get; init; } = string.Empty;

    public string ContentType { get; init; } = "image/png";
}

public sealed class LauncherAiActionProposalStorageModel
{
    public string ActionType { get; init; } = string.Empty;

    public string ButtonText { get; init; } = string.Empty;

    public string DisplayMessage { get; init; } = string.Empty;

    public AgentToolPermissionLevel PermissionLevel { get; init; } = AgentToolPermissionLevel.ConfirmationRequired;

    public Dictionary<string, string> Parameters { get; init; } = [];
}