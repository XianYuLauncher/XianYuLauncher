using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

public interface ILauncherAiWorkspacePersistenceService
{
    Task<LauncherAiWorkspaceStorageModel?> LoadWorkspaceAsync(CancellationToken cancellationToken = default);

    Task SaveWorkspaceAsync(LauncherAiWorkspaceStorageModel workspace, CancellationToken cancellationToken = default);

    Task<LauncherAiConversationStorageModel?> LoadConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task SaveConversationAsync(LauncherAiConversationStorageModel conversation, CancellationToken cancellationToken = default);

    Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<LauncherAiAttachmentStorageModel?> PersistAttachmentAsync(Guid conversationId, ChatImageAttachment attachment, CancellationToken cancellationToken = default);

    bool TryCreateStoredAttachmentModel(Guid conversationId, ChatImageAttachment attachment, out LauncherAiAttachmentStorageModel attachmentModel);

    ChatImageAttachment? RestoreAttachment(LauncherAiAttachmentStorageModel attachment);
}