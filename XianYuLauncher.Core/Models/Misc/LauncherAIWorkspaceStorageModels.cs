namespace XianYuLauncher.Core.Models;

public sealed class LauncherAIWorkspaceStorageModel
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public Guid? SelectedConversationId { get; init; }

    public Guid? ActiveErrorAnalysisConversationId { get; init; }

    public int NextConversationNumber { get; init; } = 1;

    public List<LauncherAIConversationIndexEntryStorageModel> Conversations { get; init; } = [];
}

public sealed class LauncherAIConversationIndexEntryStorageModel
{
    public Guid ConversationId { get; init; }

    public bool IsErrorAnalysisConversation { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset LastUpdatedAtUtc { get; init; }
}

public sealed class LauncherAIConversationStorageModel
{
    public int SchemaVersion { get; init; } = LauncherAIWorkspaceStorageModel.CurrentSchemaVersion;

    public Guid ConversationId { get; init; }

    public bool IsErrorAnalysisConversation { get; init; }

    public string Title { get; init; } = string.Empty;

    public string ToolTip { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset LastUpdatedAtUtc { get; init; }

    public LauncherAIConversationInterruptionStorageModel? Interruption { get; init; }

    public LauncherAISessionStorageModel Session { get; init; } = new();
}

public sealed class LauncherAIConversationInterruptionStorageModel
{
    public string Kind { get; init; } = string.Empty;

    public DateTimeOffset InterruptedAtUtc { get; init; }

    public string? Message { get; init; }
}

public sealed class LauncherAISessionStorageModel
{
    public string ChatInput { get; init; } = string.Empty;

    public bool IsChatEnabled { get; init; }

    public List<LauncherAIAttachmentStorageModel> PendingImageAttachments { get; init; } = [];

    public List<LauncherAIChatMessageStorageModel> ChatMessages { get; init; } = [];

    public List<LauncherAIActionProposalStorageModel> ActionProposals { get; init; } = [];
}

public sealed class LauncherAIChatMessageStorageModel
{
    public string Role { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public bool IncludeInAIHistory { get; init; } = true;

    public bool ShowRoleHeader { get; init; } = true;

    public string DisplayRoleText { get; init; } = string.Empty;

    public string? AIHistoryContent { get; init; }

    public string? ToolCallId { get; init; }

    public List<ToolCallInfo>? ToolCalls { get; init; }

    public List<LauncherAIAttachmentStorageModel> ImageAttachments { get; init; } = [];

    public List<LauncherAIAttachmentStorageModel>? AIHistoryImageAttachments { get; init; }

    public bool SuppressContentRendering { get; init; }
}

public sealed class LauncherAIAttachmentStorageModel
{
    public string FileName { get; init; } = string.Empty;

    public string RelativeFilePath { get; init; } = string.Empty;

    public string ContentType { get; init; } = "image/png";
}

public sealed class LauncherAIActionProposalStorageModel
{
    public string ActionType { get; init; } = string.Empty;

    public string ButtonText { get; init; } = string.Empty;

    public string DisplayMessage { get; init; } = string.Empty;

    public string PermissionLevel { get; init; } = "ConfirmationRequired";

    public Dictionary<string, string> Parameters { get; init; } = [];
}