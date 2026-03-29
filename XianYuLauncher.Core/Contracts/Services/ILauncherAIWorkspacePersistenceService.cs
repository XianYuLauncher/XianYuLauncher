using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

public interface ILauncherAIWorkspacePersistenceService
{
    Task<LauncherAIWorkspaceStorageModel?> LoadWorkspaceAsync(CancellationToken cancellationToken = default);

    Task SaveWorkspaceAsync(LauncherAIWorkspaceStorageModel workspace, CancellationToken cancellationToken = default);

    Task<LauncherAIConversationStorageModel?> LoadConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task SaveConversationAsync(LauncherAIConversationStorageModel conversation, CancellationToken cancellationToken = default);

    Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<LauncherAIAttachmentStorageModel?> PersistAttachmentAsync(Guid conversationId, ChatImageAttachment attachment, CancellationToken cancellationToken = default);

    bool TryCreateStoredAttachmentModel(Guid conversationId, ChatImageAttachment attachment, out LauncherAIAttachmentStorageModel attachmentModel);

    ChatImageAttachment? RestoreAttachment(LauncherAIAttachmentStorageModel attachment);
}