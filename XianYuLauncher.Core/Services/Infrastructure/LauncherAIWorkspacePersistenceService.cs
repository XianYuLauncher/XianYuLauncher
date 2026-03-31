using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public sealed class LauncherAIWorkspacePersistenceService : ILauncherAIWorkspacePersistenceService
{
    private readonly IFileService _fileService;
    private readonly ILogger<LauncherAIWorkspacePersistenceService> _logger;
    private readonly Lock _attachmentMapLock = new();
    private readonly Dictionary<string, LauncherAIAttachmentStorageModel> _attachmentSourceMap = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _workspaceRootPath;
    private readonly string _conversationsPath;
    private readonly string _attachmentsPath;

    public LauncherAIWorkspacePersistenceService(
        IFileService fileService,
        ILogger<LauncherAIWorkspacePersistenceService> logger,
        string? workspaceRootPath = null)
    {
        _fileService = fileService;
        _logger = logger;
        _workspaceRootPath = string.IsNullOrWhiteSpace(workspaceRootPath)
            ? Path.Combine(AppEnvironment.SafeAppDataPath, AppDataFileConsts.LauncherAIFolder)
            : workspaceRootPath;
        _conversationsPath = Path.Combine(_workspaceRootPath, AppDataFileConsts.LauncherAIConversationsFolder);
        _attachmentsPath = Path.Combine(_workspaceRootPath, AppDataFileConsts.LauncherAIAttachmentsFolder);
    }

    public async Task<LauncherAIWorkspaceStorageModel?> LoadWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            EnsureWorkspaceDirectories();
            return await _fileService.ReadAsync<LauncherAIWorkspaceStorageModel>(
                _workspaceRootPath,
                AppDataFileConsts.LauncherAIWorkspaceJson,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "加载 Launcher AI 工作区失败。");
            return null;
        }
    }

    public async Task SaveWorkspaceAsync(LauncherAIWorkspaceStorageModel workspace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureWorkspaceDirectories();
        await _fileService.SaveAsync(_workspaceRootPath, AppDataFileConsts.LauncherAIWorkspaceJson, workspace, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LauncherAIConversationStorageModel?> LoadConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            EnsureWorkspaceDirectories();
            return await _fileService.ReadAsync<LauncherAIConversationStorageModel>(
                _conversationsPath,
                GetConversationFileName(conversationId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "加载 Launcher AI 会话失败: {ConversationId}", conversationId);
            return null;
        }
    }

    public async Task SaveConversationAsync(LauncherAIConversationStorageModel conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureWorkspaceDirectories();
        await _fileService.SaveAsync(
            _conversationsPath,
            GetConversationFileName(conversation.ConversationId),
            conversation,
            cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            EnsureWorkspaceDirectories();
            _fileService.Delete(_conversationsPath, GetConversationFileName(conversationId));

            var conversationAttachmentPath = GetConversationAttachmentFolderPath(conversationId);
            if (Directory.Exists(conversationAttachmentPath))
            {
                Directory.Delete(conversationAttachmentPath, recursive: true);
            }

            RemoveConversationAttachmentMappings(conversationId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "删除 Launcher AI 会话失败: {ConversationId}", conversationId);
        }

        return Task.CompletedTask;
    }

    public async Task<LauncherAIAttachmentStorageModel?> PersistAttachmentAsync(
        Guid conversationId,
        ChatImageAttachment attachment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        cancellationToken.ThrowIfCancellationRequested();

        string? targetPath = null;

        try
        {
            if (TryCreateStoredAttachmentModel(conversationId, attachment, out var storedAttachment))
            {
                return storedAttachment;
            }

            if (TryGetCachedAttachment(conversationId, attachment.FilePath, out storedAttachment))
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
            targetPath = Path.Combine(conversationAttachmentPath, storedFileName);

            await using (var sourceStream = new FileStream(attachment.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            await using (var targetStream = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await sourceStream.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
            }

            storedAttachment = new LauncherAIAttachmentStorageModel
            {
                FileName = string.IsNullOrWhiteSpace(attachment.FileName) ? Path.GetFileName(targetPath) : attachment.FileName,
                RelativeFilePath = CreateRelativeAttachmentPath(conversationId, storedFileName),
                ContentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "image/png" : attachment.ContentType
            };

            CacheAttachment(conversationId, attachment.FilePath, storedAttachment);
            return storedAttachment;
        }
        catch (OperationCanceledException)
        {
            if (!string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            throw;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            _logger.LogWarning(ex, "持久化 Launcher AI 附件失败: {ConversationId}, {FileName}", conversationId, attachment.FileName);
            return null;
        }
    }

    public bool TryCreateStoredAttachmentModel(Guid conversationId, ChatImageAttachment attachment, out LauncherAIAttachmentStorageModel attachmentModel)
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

        attachmentModel = new LauncherAIAttachmentStorageModel
        {
            FileName = string.IsNullOrWhiteSpace(attachment.FileName) ? Path.GetFileName(attachment.FilePath) : attachment.FileName,
            RelativeFilePath = relativePath,
            ContentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "image/png" : attachment.ContentType
        };

        CacheAttachment(conversationId, attachment.FilePath, attachmentModel);

        return true;
    }

    public ChatImageAttachment? RestoreAttachment(LauncherAIAttachmentStorageModel attachment)
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

        try
        {
            var attachmentRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(_attachmentsPath));
            var absolutePath = Path.GetFullPath(Path.Combine(_attachmentsPath, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            return absolutePath.StartsWith(attachmentRoot, StringComparison.OrdinalIgnoreCase)
                ? absolutePath
                : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _logger.LogWarning(ex, "解析 Launcher AI 附件路径失败: {RelativePath}", relativePath);
            return null;
        }
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
            || Path.IsPathRooted(normalizedRelativePath))
        {
            normalizedRelativePath = string.Empty;
            return false;
        }

        var segments = normalizedRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        List<string> normalizedSegments = new(segments.Length);

        foreach (var rawSegment in segments)
        {
            var segment = rawSegment.Trim();

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                normalizedRelativePath = string.Empty;
                return false;
            }

            if (string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(segment)
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                normalizedRelativePath = string.Empty;
                return false;
            }

            normalizedSegments.Add(segment);
        }

        if (normalizedSegments.Count == 0)
        {
            normalizedRelativePath = string.Empty;
            return false;
        }

        normalizedRelativePath = string.Join('/', normalizedSegments);

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

    private bool TryGetCachedAttachment(Guid conversationId, string sourceFilePath, out LauncherAIAttachmentStorageModel attachmentModel)
    {
        attachmentModel = null!;

        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return false;
        }

        var cacheKey = BuildAttachmentCacheKey(conversationId, sourceFilePath);
        lock (_attachmentMapLock)
        {
            if (!_attachmentSourceMap.TryGetValue(cacheKey, out var cachedAttachment))
            {
                return false;
            }

            if (RestoreAttachment(cachedAttachment) == null)
            {
                _attachmentSourceMap.Remove(cacheKey);
                return false;
            }

            attachmentModel = cachedAttachment;
            return true;
        }
    }

    private void CacheAttachment(Guid conversationId, string sourceFilePath, LauncherAIAttachmentStorageModel attachmentModel)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return;
        }

        var cacheKey = BuildAttachmentCacheKey(conversationId, sourceFilePath);
        lock (_attachmentMapLock)
        {
            _attachmentSourceMap[cacheKey] = attachmentModel;
        }
    }

    private void RemoveConversationAttachmentMappings(Guid conversationId)
    {
        var conversationPrefix = conversationId.ToString("N") + "|";
        lock (_attachmentMapLock)
        {
            var keysToRemove = _attachmentSourceMap.Keys
                .Where(key => key.StartsWith(conversationPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _attachmentSourceMap.Remove(key);
            }
        }
    }

    private static string BuildAttachmentCacheKey(Guid conversationId, string sourceFilePath)
    {
        return $"{conversationId:N}|{Path.GetFullPath(sourceFilePath)}";
    }
}