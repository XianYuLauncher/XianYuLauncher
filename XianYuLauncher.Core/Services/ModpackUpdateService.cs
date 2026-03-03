using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public class ModpackUpdateService : IModpackUpdateService
{
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;

    public ModpackUpdateService(
        ModrinthService modrinthService,
        CurseForgeService curseForgeService)
    {
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
    }

    public async Task<IReadOnlyList<ModpackVersionItem>> GetAvailableVersionsAsync(
        ModpackUpdateCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return Array.Empty<ModpackVersionItem>();
        }

        var platform = NormalizePlatform(request.Platform);
        var projectId = request.ProjectId.Trim();
        var currentVersionId = request.CurrentVersionId.Trim();

        if (string.IsNullOrWhiteSpace(platform)
            || string.IsNullOrWhiteSpace(projectId)
            || string.IsNullOrWhiteSpace(currentVersionId))
        {
            return Array.Empty<ModpackVersionItem>();
        }

        cancellationToken.ThrowIfCancellationRequested();

        return platform switch
        {
            "modrinth" => await GetModrinthVersionsAsync(projectId, currentVersionId, cancellationToken),
            "curseforge" => await GetCurseForgeVersionsAsync(projectId, currentVersionId, cancellationToken),
            _ => Array.Empty<ModpackVersionItem>()
        };
    }

    public async Task<ModpackUpdateCheckResult> CheckForUpdatesAsync(
        ModpackUpdateCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return ModpackUpdateCheckResult.Failure("请求不能为空");
        }

        var platform = NormalizePlatform(request.Platform);
        var projectId = request.ProjectId.Trim();
        var currentVersionId = request.CurrentVersionId.Trim();

        if (string.IsNullOrWhiteSpace(platform)
            || string.IsNullOrWhiteSpace(projectId)
            || string.IsNullOrWhiteSpace(currentVersionId))
        {
            return ModpackUpdateCheckResult.Failure(
                "平台、项目ID和当前版本号不能为空",
                platform,
                projectId,
                currentVersionId);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var versions = await GetAvailableVersionsAsync(request, cancellationToken);
            if (versions.Count == 0)
            {
                return ModpackUpdateCheckResult.NoUpdate(platform, projectId, currentVersionId);
            }

            var latest = versions[0];
            if (latest.IsCurrentVersion)
            {
                return ModpackUpdateCheckResult.NoUpdate(
                    platform,
                    projectId,
                    currentVersionId,
                    latest.VersionId,
                    latest.DisplayName);
            }

            return new ModpackUpdateCheckResult
            {
                Success = true,
                HasUpdate = true,
                Platform = platform,
                ProjectId = projectId,
                CurrentVersionId = currentVersionId,
                LatestVersionId = latest.VersionId,
                LatestVersionName = latest.DisplayName
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ModpackUpdateCheckResult.Failure(
                $"检查整合包更新失败: {ex.Message}",
                platform,
                projectId,
                currentVersionId);
        }
    }

    private async Task<IReadOnlyList<ModpackVersionItem>> GetModrinthVersionsAsync(
        string projectId,
        string currentVersionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var versions = await _modrinthService.GetProjectVersionsAsync(projectId);

        return versions
            .Select(v =>
            {
                var versionId = !string.IsNullOrWhiteSpace(v.VersionNumber) ? v.VersionNumber : v.Id;
                var versionName = string.IsNullOrWhiteSpace(v.Name) ? versionId : v.Name;
                var primaryFileName = v.Files?.FirstOrDefault(file => file.Primary)?.Filename
                                      ?? v.Files?.FirstOrDefault()?.Filename
                                      ?? string.Empty;
                return new ModpackVersionItem
                {
                    VersionId = versionId,
                    DisplayName = $"{versionName}",
                    FileName = primaryFileName,
                    PublishedAt = ParseDatePublished(v.DatePublished),
                    IsCurrentVersion = IsSameVersion(currentVersionId, v.Id) || IsSameVersion(currentVersionId, v.VersionNumber),
                    GameVersions = v.GameVersions ?? new List<string>(),
                    Loaders = v.Loaders ?? new List<string>()
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.VersionId))
            .OrderByDescending(item => item.PublishedAt)
                .ThenByDescending(item => item.VersionId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<ModpackVersionItem>> GetCurseForgeVersionsAsync(
        string projectId,
        string currentVersionId,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(projectId, out var modId))
        {
            return Array.Empty<ModpackVersionItem>();
        }

        var allFiles = new List<CurseForgeFile>();
        const int pageSize = 50;
        for (var index = 0; ; index += pageSize)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var page = await _curseForgeService.GetModFilesAsync(modId, null, null, index, pageSize);
            if (page.Count == 0)
            {
                break;
            }

            allFiles.AddRange(page);
            if (page.Count < pageSize)
            {
                break;
            }
        }

        return allFiles
            .Where(file => file.DisplayName != null && !file.DisplayName.Contains("Server", StringComparison.OrdinalIgnoreCase))
            .Select(file =>
            {
                var versionId = !string.IsNullOrWhiteSpace(file.DisplayName) ? file.DisplayName : file.Id.ToString();
                var versionName = !string.IsNullOrWhiteSpace(file.DisplayName) ? file.DisplayName : file.Id.ToString();
                var fileName = file.FileName ?? string.Empty;
                
                var loaders = new List<string>();
                var gameVersions = new List<string>();
                
                if (file.GameVersions != null)
                {
                    foreach (var v in file.GameVersions)
                    {
                        if (IsLoader(v))
                        {
                            loaders.Add(v);
                        }
                        else
                        {
                            gameVersions.Add(v);
                        }
                    }
                }

                return new ModpackVersionItem
                {
                    VersionId = versionId,
                    DisplayName = versionName,
                    FileName = fileName,
                    PublishedAt = file.FileDate,
                    IsCurrentVersion = IsSameVersion(currentVersionId, versionId) || IsSameVersion(currentVersionId, file.Id.ToString()),
                    GameVersions = gameVersions,
                    Loaders = loaders
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.VersionId))
            .GroupBy(item => item.VersionId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.PublishedAt).First())
            .OrderByDescending(item => item.PublishedAt)
                .ThenByDescending(item => item.VersionId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    
    private static bool IsLoader(string version)
    {
        return version.Equals("Forge", StringComparison.OrdinalIgnoreCase)
            || version.Equals("Fabric", StringComparison.OrdinalIgnoreCase)
            || version.Equals("Quilt", StringComparison.OrdinalIgnoreCase)
            || version.Equals("NeoForge", StringComparison.OrdinalIgnoreCase)
            || version.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePlatform(string? platform)
    {
        var normalized = NormalizeOptional(platform)?.ToLowerInvariant();
        return normalized switch
        {
            "modrinth" => "modrinth",
            "curseforge" => "curseforge",
            _ => normalized ?? string.Empty
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTimeOffset ParseDatePublished(string? publishedAt)
    {
        return DateTimeOffset.TryParse(publishedAt, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
    }

    private static bool IsSameVersion(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}