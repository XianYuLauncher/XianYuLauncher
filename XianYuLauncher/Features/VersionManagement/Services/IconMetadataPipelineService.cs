using System.Collections.Concurrent;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.VersionManagement.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.Services;

public class IconMetadataPipelineService : IIconMetadataPipelineService
{
    private readonly IFileService _fileService;
    private readonly IDownloadManager _downloadManager;
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly ModInfoService _modInfoService;

    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _sharedSha1Cache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Lazy<Task<uint>>> _sharedCurseForgeFingerprintCache =
        new(StringComparer.OrdinalIgnoreCase);

    public IconMetadataPipelineService(
        IFileService fileService,
        IDownloadManager downloadManager,
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ModInfoService modInfoService)
    {
        _fileService = fileService;
        _downloadManager = downloadManager;
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _modInfoService = modInfoService;
    }

    public string CalculateSHA1(string filePath)
    {
        return VersionManagementFileOps.CalculateSha1(filePath);
    }

    public void ResetSharedHashCache()
    {
        _sharedSha1Cache.Clear();
        _sharedCurseForgeFingerprintCache.Clear();
    }

    public async Task<string> GetSharedSha1Async(string filePath, CancellationToken cancellationToken)
    {
        var normalizedFilePath = NormalizeFilePathForHashCache(filePath);
        var lazyHashTask = _sharedSha1Cache.GetOrAdd(
            normalizedFilePath,
            static path => new Lazy<Task<string>>(
                () => Task.Run(() => VersionManagementFileOps.CalculateSha1(path)),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyHashTask.Value.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            _sharedSha1Cache.TryRemove(normalizedFilePath, out _);
            throw;
        }
    }

    public async Task<uint> GetSharedCurseForgeFingerprintAsync(string filePath, CancellationToken cancellationToken)
    {
        var normalizedFilePath = NormalizeFilePathForHashCache(filePath);
        var lazyFingerprintTask = _sharedCurseForgeFingerprintCache.GetOrAdd(
            normalizedFilePath,
            static path => new Lazy<Task<uint>>(
                () => Task.Run(() => CurseForgeFingerprintHelper.ComputeFingerprint(path)),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyFingerprintTask.Value.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            _sharedCurseForgeFingerprintCache.TryRemove(normalizedFilePath, out _);
            throw;
        }
    }

    public async Task<ModMetadata?> GetResourceMetadataAsync(string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var sha1 = await GetSharedSha1Async(filePath, cancellationToken);
            var fingerprint = await GetSharedCurseForgeFingerprintAsync(filePath, cancellationToken);
            return await _modInfoService.GetModInfoAsync(filePath, sha1, fingerprint, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public async Task<string?> ResolveResourceIconPathAsync(
        string filePath,
        string resourceType,
        bool isModrinthSupported,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var localIcon = GetLocalIconPath(filePath, resourceType);
        if (!string.IsNullOrEmpty(localIcon))
        {
            return localIcon;
        }

        if (!isModrinthSupported)
        {
            return null;
        }

        if (!IsRegularFilePath(filePath))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var modrinthIconUrl = await GetModrinthIconUrlAsync(filePath, cancellationToken);
        if (!string.IsNullOrEmpty(modrinthIconUrl))
        {
            var localModrinthIconPath = await SaveModrinthIconAsync(filePath, modrinthIconUrl, resourceType, cancellationToken);
            if (!string.IsNullOrEmpty(localModrinthIconPath))
            {
                return localModrinthIconPath;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var curseForgeIconUrl = await GetCurseForgeIconUrlAsync(filePath, cancellationToken);
        if (string.IsNullOrEmpty(curseForgeIconUrl))
        {
            return null;
        }

        return await SaveCurseForgeIconAsync(filePath, curseForgeIconUrl, resourceType, cancellationToken);
    }

    private string? GetLocalIconPath(string filePath, string resourceType)
    {
        return VersionManagementFileOps.GetLocalIconPath(
            _fileService.GetLauncherCachePath(),
            filePath,
            resourceType);
    }

    private async Task<string?> GetModrinthIconUrlAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sha1Hash = await GetSharedSha1Async(filePath, cancellationToken);
            var versionMap = await _modrinthService.GetVersionFilesByHashesAsync(new List<string> { sha1Hash });

            if (versionMap == null || !versionMap.TryGetValue(sha1Hash, out var versionInfo) || versionInfo == null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var projectDetail = await _modrinthService.GetProjectDetailAsync(versionInfo.ProjectId);
            if (projectDetail == null || string.IsNullOrEmpty(projectDetail.IconUrl?.ToString()))
            {
                return null;
            }

            return projectDetail.IconUrl.ToString();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetCurseForgeIconUrlAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fingerprint = await GetSharedCurseForgeFingerprintAsync(filePath, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var result = await _curseForgeService.GetFingerprintMatchesAsync(new List<uint> { fingerprint });
            if (result?.ExactMatches == null || result.ExactMatches.Count == 0)
            {
                return null;
            }

            var match = result.ExactMatches[0];

            cancellationToken.ThrowIfCancellationRequested();

            var modDetail = await _curseForgeService.GetModDetailAsync(match.Id);
            if (modDetail?.Logo != null && !string.IsNullOrEmpty(modDetail.Logo.ThumbnailUrl))
            {
                return modDetail.Logo.ThumbnailUrl;
            }

            if (modDetail?.Logo != null && !string.IsNullOrEmpty(modDetail.Logo.Url))
            {
                return modDetail.Logo.Url;
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> SaveModrinthIconAsync(
        string filePath,
        string iconUrl,
        string resourceType,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cachePath = _fileService.GetLauncherCachePath();
            var iconDir = Path.Combine(cachePath, "icons", resourceType);
            Directory.CreateDirectory(iconDir);

            var iconFilePath = BuildIconCacheFilePath(filePath, iconDir, "modrinth");

            var downloadResult = await _downloadManager.DownloadFileAsync(
                iconUrl,
                iconFilePath,
                expectedSha1: null,
                progressCallback: null,
                cancellationToken: cancellationToken);

            if (!downloadResult.Success || !File.Exists(iconFilePath))
            {
                return null;
            }

            return iconFilePath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> SaveCurseForgeIconAsync(
        string filePath,
        string iconUrl,
        string resourceType,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cachePath = _fileService.GetLauncherCachePath();
            var iconDir = Path.Combine(cachePath, "icons", resourceType);
            Directory.CreateDirectory(iconDir);

            var iconFilePath = BuildIconCacheFilePath(filePath, iconDir, "curseforge");

            var downloadResult = await _downloadManager.DownloadFileAsync(
                iconUrl,
                iconFilePath,
                expectedSha1: null,
                progressCallback: null,
                cancellationToken: cancellationToken);

            if (!downloadResult.Success || !File.Exists(iconFilePath))
            {
                return null;
            }

            return iconFilePath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildIconCacheFilePath(string filePath, string iconDir, string sourcePrefix)
    {
        var fileName = Path.GetFileName(filePath);
        if (fileName.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName.Substring(0, fileName.Length - FileExtensionConsts.Disabled.Length);
        }

        var fileBaseName = Path.GetFileNameWithoutExtension(fileName);
        var iconFileName = $"{sourcePrefix}_{fileBaseName}_icon.png";
        return Path.Combine(iconDir, iconFileName);
    }

    private static string NormalizeFilePathForHashCache(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        return Path.GetFullPath(filePath).Trim();
    }

    private static bool IsRegularFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var attributes = File.GetAttributes(filePath);
            return (attributes & FileAttributes.Directory) == 0;
        }
        catch
        {
            return false;
        }
    }
}
