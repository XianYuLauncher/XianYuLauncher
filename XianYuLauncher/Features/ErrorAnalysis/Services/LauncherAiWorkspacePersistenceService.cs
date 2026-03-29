using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

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

public sealed class LauncherAiWorkspacePersistenceService : ILauncherAiWorkspacePersistenceService
{
    private readonly IFileService _fileService;
    private readonly ILogger<LauncherAiWorkspacePersistenceService> _logger;

    private readonly string _workspaceRootPath;
    private readonly string _conversationsPath;
    private readonly string _attachmentsPath;

    public LauncherAiWorkspacePersistenceService(
        IFileService fileService,
        ILogger<LauncherAiWorkspacePersistenceService> logger)
    {
        _fileService = fileService;
        _logger = logger;
        _workspaceRootPath = Path.Combine(AppEnvironment.SafeAppDataPath, AppDataFileConsts.LauncherAiFolder);
        _conversationsPath = Path.Combine(_workspaceRootPath, AppDataFileConsts.LauncherAiConversationsFolder);
        _attachmentsPath = Path.Combine(_workspaceRootPath, AppDataFileConsts.LauncherAiAttachmentsFolder);
    }

    public Task<LauncherAiWorkspaceStorageModel?> LoadWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                EnsureWorkspaceDirectories();
                return _fileService.Read<LauncherAiWorkspaceStorageModel>(_workspaceRootPath, AppDataFileConsts.LauncherAiWorkspaceJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载 Launcher AI 工作区失败。");
                return null;
            }
        }, cancellationToken);
    }

    public Task SaveWorkspaceAsync(LauncherAiWorkspaceStorageModel workspace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        return Task.Run(() =>
        {
            EnsureWorkspaceDirectories();
            _fileService.Save(_workspaceRootPath, AppDataFileConsts.LauncherAiWorkspaceJson, workspace);
        }, cancellationToken);
    }

    public Task<LauncherAiConversationStorageModel?> LoadConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                EnsureWorkspaceDirectories();
                return _fileService.Read<LauncherAiConversationStorageModel>(_conversationsPath, GetConversationFileName(conversationId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载 Launcher AI 会话失败: {ConversationId}", conversationId);
                return null;
            }
        }, cancellationToken);
    }

    public Task SaveConversationAsync(LauncherAiConversationStorageModel conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        return Task.Run(() =>
        {
            EnsureWorkspaceDirectories();
            _fileService.Save(_conversationsPath, GetConversationFileName(conversation.ConversationId), conversation);
        }, cancellationToken);
    }

    public Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                EnsureWorkspaceDirectories();
                _fileService.Delete(_conversationsPath, GetConversationFileName(conversationId));

                var conversationAttachmentPath = GetConversationAttachmentFolderPath(conversationId);
                if (Directory.Exists(conversationAttachmentPath))
                {
                    Directory.Delete(conversationAttachmentPath, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "删除 Launcher AI 会话失败: {ConversationId}", conversationId);
            }
        }, cancellationToken);
    }

    public Task<LauncherAiAttachmentStorageModel?> PersistAttachmentAsync(
        Guid conversationId,
        ChatImageAttachment attachment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        return Task.Run<LauncherAiAttachmentStorageModel?>(() =>
        {
            try
            {
                if (TryCreateStoredAttachmentModel(conversationId, attachment, out var storedAttachment))
                {
                    return storedAttachment;
                }

                if (string.IsNullOrWhiteSpace(attachment.FilePath) || !File.Exists(attachment.FilePath))
                {
                    return null;
                }

                var conversationAttachmentPath = GetConversationAttachmentFolderPath(conversationId);
                Directory.CreateDirectory(conversationAttachmentPath);

                var extension = GetAttachmentExtension(attachment);
                var storedFileName = $"{Guid.NewGuid():N}{extension}";
                var targetPath = Path.Combine(conversationAttachmentPath, storedFileName);
                File.Copy(attachment.FilePath, targetPath, overwrite: false);

                return new LauncherAiAttachmentStorageModel
                {
                    FileName = string.IsNullOrWhiteSpace(attachment.FileName) ? Path.GetFileName(targetPath) : attachment.FileName,
                    RelativeFilePath = CreateRelativeAttachmentPath(conversationId, storedFileName),
                    ContentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "image/png" : attachment.ContentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "持久化 Launcher AI 附件失败: {ConversationId}, {FileName}", conversationId, attachment.FileName);
                return null;
            }
        }, cancellationToken);
    }

    public bool TryCreateStoredAttachmentModel(Guid conversationId, ChatImageAttachment attachment, out LauncherAiAttachmentStorageModel attachmentModel)
    {
        attachmentModel = null!;

        if (attachment == null || string.IsNullOrWhiteSpace(attachment.FilePath))
        {
            return false;
        }

        if (!TryGetStoredAttachmentRelativePath(conversationId, attachment.FilePath, out var relativePath))
        {
            return false;
        }

        attachmentModel = new LauncherAiAttachmentStorageModel
        {
            FileName = string.IsNullOrWhiteSpace(attachment.FileName) ? Path.GetFileName(attachment.FilePath) : attachment.FileName,
            RelativeFilePath = relativePath,
            ContentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "image/png" : attachment.ContentType
        };

        return true;
    }

    public ChatImageAttachment? RestoreAttachment(LauncherAiAttachmentStorageModel attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        var absolutePath = GetAttachmentAbsolutePath(attachment.RelativeFilePath);
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            return null;
        }

        return new ChatImageAttachment
        {
            FileName = string.IsNullOrWhiteSpace(attachment.FileName) ? Path.GetFileName(absolutePath) : attachment.FileName,
            FilePath = absolutePath,
            ContentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "image/png" : attachment.ContentType,
            DataUrl = string.Empty
        };
    }

    private void EnsureWorkspaceDirectories()
    {
        Directory.CreateDirectory(_workspaceRootPath);
        Directory.CreateDirectory(_conversationsPath);
        Directory.CreateDirectory(_attachmentsPath);
    }

    private string GetConversationFileName(Guid conversationId)
    {
        return $"{conversationId:N}.json";
    }

    private string GetConversationAttachmentFolderPath(Guid conversationId)
    {
        return Path.Combine(_attachmentsPath, conversationId.ToString("N"));
    }

    private string CreateRelativeAttachmentPath(Guid conversationId, string storedFileName)
    {
        return $"{conversationId:N}/{storedFileName}";
    }

    private string? GetAttachmentAbsolutePath(string relativePath)
    {
        if (!TryNormalizeRelativeAttachmentPath(relativePath, out var normalizedRelativePath))
        {
            return null;
        }

        return Path.Combine(_attachmentsPath, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private bool TryGetStoredAttachmentRelativePath(Guid conversationId, string absolutePath, out string relativePath)
    {
        relativePath = string.Empty;

        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(absolutePath);
        var conversationAttachmentRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(GetConversationAttachmentFolderPath(conversationId)));
        if (!fullPath.StartsWith(conversationAttachmentRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!File.Exists(fullPath))
        {
            return false;
        }

        relativePath = Path.GetRelativePath(_attachmentsPath, fullPath).Replace('\\', '/');
        return TryNormalizeRelativeAttachmentPath(relativePath, out relativePath);
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static bool TryNormalizeRelativeAttachmentPath(string relativePath, out string normalizedRelativePath)
    {
        normalizedRelativePath = relativePath?.Trim().Replace('\\', '/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedRelativePath)
            || Path.IsPathRooted(normalizedRelativePath)
            || normalizedRelativePath.Contains("..", StringComparison.Ordinal))
        {
            normalizedRelativePath = string.Empty;
            return false;
        }

        return true;
    }

    private static string GetAttachmentExtension(ChatImageAttachment attachment)
    {
        var extension = Path.GetExtension(attachment.FileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension;
        }

        extension = Path.GetExtension(attachment.FilePath);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension;
        }

        return attachment.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            _ => ".png"
        };
    }
}