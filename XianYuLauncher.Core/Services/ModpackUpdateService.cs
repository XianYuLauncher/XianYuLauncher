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

            return platform switch
            {
                "modrinth" => await CheckModrinthAsync(request, platform, projectId, currentVersionId, cancellationToken),
                "curseforge" => await CheckCurseForgeAsync(request, platform, projectId, currentVersionId, cancellationToken),
                _ => ModpackUpdateCheckResult.Failure($"不支持的平台: {request.Platform}", platform, projectId, currentVersionId)
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

    private async Task<ModpackUpdateCheckResult> CheckModrinthAsync(
        ModpackUpdateCheckRequest request,
        string platform,
        string projectId,
        string currentVersionId,
        CancellationToken cancellationToken)
    {
        var loaders = BuildModrinthLoaders(request.ModLoaderType);
        var gameVersions = BuildGameVersions(request.MinecraftVersion);

        List<ModrinthVersion> versions = new();

        if (loaders != null && gameVersions != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            versions = await _modrinthService.GetProjectVersionsAsync(projectId, loaders, gameVersions);
        }

        if (versions.Count == 0 && gameVersions != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            versions = await _modrinthService.GetProjectVersionsAsync(projectId, null, gameVersions);
        }

        if (versions.Count == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            versions = await _modrinthService.GetProjectVersionsAsync(projectId);
        }

        if (versions.Count == 0)
        {
            return ModpackUpdateCheckResult.NoUpdate(platform, projectId, currentVersionId);
        }

        var latest = versions
            .OrderByDescending(v => ParseDatePublished(v.DatePublished))
            .First();

        var latestVersionId = !string.IsNullOrWhiteSpace(latest.VersionNumber)
            ? latest.VersionNumber
            : latest.Id;

        if (IsSameVersion(currentVersionId, latest.Id)
            || IsSameVersion(currentVersionId, latest.VersionNumber))
        {
            return ModpackUpdateCheckResult.NoUpdate(
                platform,
                projectId,
                currentVersionId,
                latestVersionId,
                latest.Name);
        }

        return new ModpackUpdateCheckResult
        {
            Success = true,
            HasUpdate = true,
            Platform = platform,
            ProjectId = projectId,
            CurrentVersionId = currentVersionId,
            LatestVersionId = latestVersionId,
            LatestVersionName = latest.Name
        };
    }

    private async Task<ModpackUpdateCheckResult> CheckCurseForgeAsync(
        ModpackUpdateCheckRequest request,
        string platform,
        string projectId,
        string currentVersionId,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(projectId, out var modId))
        {
            return ModpackUpdateCheckResult.Failure(
                "CurseForge 项目ID不是有效数字",
                platform,
                projectId,
                currentVersionId);
        }

        var gameVersion = NormalizeOptional(request.MinecraftVersion);
        var modLoaderType = MapCurseForgeModLoaderType(request.ModLoaderType);

        List<CurseForgeFile> files = new();

        if (!string.IsNullOrWhiteSpace(gameVersion) && modLoaderType.HasValue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            files = await _curseForgeService.GetModFilesAsync(modId, gameVersion, modLoaderType);
        }

        if (files.Count == 0 && !string.IsNullOrWhiteSpace(gameVersion))
        {
            cancellationToken.ThrowIfCancellationRequested();
            files = await _curseForgeService.GetModFilesAsync(modId, gameVersion, null);
        }

        if (files.Count == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            files = await _curseForgeService.GetModFilesAsync(modId);
        }

        if (files.Count == 0)
        {
            return ModpackUpdateCheckResult.NoUpdate(platform, projectId, currentVersionId);
        }

        var latest = files
            .Where(file => file.IsAvailable && !file.IsServerPack)
            .OrderByDescending(file => file.FileDate)
            .FirstOrDefault()
            ?? files.OrderByDescending(file => file.FileDate).First();

        var latestVersionId = !string.IsNullOrWhiteSpace(latest.DisplayName)
            ? latest.DisplayName
            : latest.Id.ToString();

        var latestVersionName = !string.IsNullOrWhiteSpace(latest.FileName)
            ? latest.FileName
            : latest.DisplayName;

        if (IsSameVersion(currentVersionId, latest.DisplayName)
            || IsSameVersion(currentVersionId, latest.Id.ToString()))
        {
            return ModpackUpdateCheckResult.NoUpdate(
                platform,
                projectId,
                currentVersionId,
                latestVersionId,
                latestVersionName);
        }

        return new ModpackUpdateCheckResult
        {
            Success = true,
            HasUpdate = true,
            Platform = platform,
            ProjectId = projectId,
            CurrentVersionId = currentVersionId,
            LatestVersionId = latestVersionId,
            LatestVersionName = latestVersionName
        };
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

    private static List<string>? BuildGameVersions(string? minecraftVersion)
    {
        var version = NormalizeOptional(minecraftVersion);
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        return new List<string> { version };
    }

    private static List<string>? BuildModrinthLoaders(string? modLoaderType)
    {
        var loader = NormalizeOptional(modLoaderType)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(loader))
        {
            return null;
        }

        return loader switch
        {
            "forge" => new List<string> { "forge" },
            "fabric" => new List<string> { "fabric" },
            "quilt" => new List<string> { "quilt" },
            "neoforge" => new List<string> { "neoforge" },
            "vanilla" => new List<string> { "vanilla" },
            _ => null
        };
    }

    private static int? MapCurseForgeModLoaderType(string? modLoaderType)
    {
        return NormalizeOptional(modLoaderType)?.ToLowerInvariant() switch
        {
            "forge" => 1,
            "fabric" => 4,
            "quilt" => 5,
            "neoforge" => 6,
            _ => null
        };
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