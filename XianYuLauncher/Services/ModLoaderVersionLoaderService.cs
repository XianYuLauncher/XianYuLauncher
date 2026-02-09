using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Services;

public class ModLoaderVersionLoaderService : IModLoaderVersionLoaderService
{
    private readonly ILogger<ModLoaderVersionLoaderService> _logger;
    private readonly FabricService _fabricService;
    private readonly ForgeService _forgeService;
    private readonly NeoForgeService _neoForgeService;
    private readonly QuiltService _quiltService;
    private readonly OptifineService _optifineService;
    private readonly CleanroomService _cleanroomService;
    private readonly LegacyFabricService _legacyFabricService;

    // 缓存完整的版本信息对象，用于后续安装
    private readonly Dictionary<string, FabricLoaderVersion> _fabricVersionMap = new();
    private readonly Dictionary<string, QuiltLoaderVersion> _quiltVersionMap = new();
    private readonly Dictionary<string, OptifineVersionInfo> _optifineVersionMap = new();
    private readonly Dictionary<string, FabricLoaderVersion> _legacyFabricVersionMap = new();

    public ModLoaderVersionLoaderService(
        ILogger<ModLoaderVersionLoaderService> logger,
        FabricService fabricService,
        ForgeService forgeService,
        NeoForgeService neoForgeService,
        QuiltService quiltService,
        OptifineService optifineService,
        CleanroomService cleanroomService,
        LegacyFabricService legacyFabricService)
    {
        _logger = logger;
        _fabricService = fabricService;
        _forgeService = forgeService;
        _neoForgeService = neoForgeService;
        _quiltService = quiltService;
        _optifineService = optifineService;
        _cleanroomService = cleanroomService;
        _legacyFabricService = legacyFabricService;
    }

    public async Task<List<string>> LoadVersionsAsync(string modLoaderType, string minecraftVersion, CancellationToken cancellationToken)
    {
        try
        {
            return modLoaderType.ToLower() switch
            {
                "forge" => await LoadForgeVersionsAsync(minecraftVersion, cancellationToken),
                "fabric" => await LoadFabricVersionsAsync(minecraftVersion, cancellationToken),
                "legacyfabric" => await LoadLegacyFabricVersionsAsync(minecraftVersion, cancellationToken),
                "neoforge" => await LoadNeoForgeVersionsAsync(minecraftVersion, cancellationToken),
                "quilt" => await LoadQuiltVersionsAsync(minecraftVersion, cancellationToken),
                "cleanroom" => await LoadCleanroomVersionsAsync(minecraftVersion, cancellationToken),
                "optifine" => await LoadOptifineVersionsAsync(minecraftVersion, cancellationToken),
                _ => new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load versions for {ModLoaderType} on MC {MinecraftVersion}", modLoaderType, minecraftVersion);
            // 保持原逻辑：除 404 外抛出异常，这里简化为统一抛出，由 ViewModel 决定是否 catch
            // 但根据需求，部分服务可能内部处理了 404。
            throw;
        }
    }

    private async Task<List<string>> LoadFabricVersionsAsync(string mcVersion, CancellationToken token)
    {
        try
        {
            var versions = await _fabricService.GetFabricLoaderVersionsAsync(mcVersion);
            if (versions == null) return new List<string>();

            lock (_fabricVersionMap)
            {
                _fabricVersionMap.Clear();
                foreach (var v in versions)
                {
                    if (v?.Loader?.Version != null)
                    {
                        _fabricVersionMap[v.Loader.Version] = v;
                    }
                }
            }

            return versions.Select(v => v.Loader.Version).ToList();
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Fabric versions not found for MC {Version}", mcVersion);
            return new List<string>();
        }
    }

    private async Task<List<string>> LoadLegacyFabricVersionsAsync(string mcVersion, CancellationToken token)
    {
        try
        {
            var versions = await _legacyFabricService.GetLegacyFabricLoaderVersionsAsync(mcVersion);
            if (versions == null) return new List<string>();

            lock (_legacyFabricVersionMap)
            {
                _legacyFabricVersionMap.Clear();
                foreach (var v in versions)
                {
                    if (v?.Loader?.Version != null)
                    {
                        _legacyFabricVersionMap[v.Loader.Version] = v;
                    }
                }
            }

            return versions.Select(v => v.Loader.Version).ToList();
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("LegacyFabric versions not found for MC {Version}", mcVersion);
            return new List<string>();
        }
    }

    private async Task<List<string>> LoadQuiltVersionsAsync(string mcVersion, CancellationToken token)
    {
        try
        {
            var versions = await _quiltService.GetQuiltLoaderVersionsAsync(mcVersion);
            if (versions == null) return new List<string>();

            lock (_quiltVersionMap)
            {
                _quiltVersionMap.Clear();
                foreach (var v in versions)
                {
                    if (v?.Loader?.Version != null)
                    {
                        _quiltVersionMap[v.Loader.Version] = v;
                    }
                }
            }

            return versions.Select(v => v.Loader.Version).ToList();
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Quilt versions not found for MC {Version}", mcVersion);
            return new List<string>();
        }
    }

    private async Task<List<string>> LoadForgeVersionsAsync(string mcVersion, CancellationToken token)
    {
        try
        {
            var versions = await _forgeService.GetForgeVersionsAsync(mcVersion);
            return versions?.ToList() ?? new List<string>();
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Forge versions not found for MC {Version}", mcVersion);
            return new List<string>();
        }
    }

    private async Task<List<string>> LoadNeoForgeVersionsAsync(string mcVersion, CancellationToken token)
    {
        try
        {
            var versions = await _neoForgeService.GetNeoForgeVersionsAsync(mcVersion);
            return versions?.ToList() ?? new List<string>();
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // NeoForge 在旧版本本来就没有，这很正常，不当做错误
            _logger.LogDebug("NeoForge versions not found for MC {Version}", mcVersion);
            return new List<string>();
        }
    }
    
    private async Task<List<string>> LoadCleanroomVersionsAsync(string mcVersion, CancellationToken token)
    {
        // Cleanroom 仅支持 1.12.2
        if (mcVersion != "1.12.2")
        {
            return new List<string>();
        }

        try
        {
            var versions = await _cleanroomService.GetCleanroomVersionsAsync(mcVersion);
            return versions?.ToList() ?? new List<string>();
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Cleanroom versions not found");
            return new List<string>();
        }
    }

    private async Task<List<string>> LoadOptifineVersionsAsync(string mcVersion, CancellationToken token)
    {
        try
        {
            var versions = await _optifineService.GetOptifineVersionsAsync(mcVersion);
            if (versions == null) return new List<string>();

            var result = new List<string>();

            lock (_optifineVersionMap)
            {
                _optifineVersionMap.Clear();
                foreach (var v in versions)
                {
                    if (v?.Type != null)
                    {
                        var versionName = $"{v.Type}_{v.Patch}";
                        var info = new OptifineVersionInfo
                        {
                            VersionName = versionName,
                            CompatibleForgeVersion = v.Forge,
                            FullVersion = v
                        };
                        _optifineVersionMap[versionName] = info;
                        result.Add(versionName);
                    }
                }
            }

            return result;
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Optifine versions not found for MC {Version}", mcVersion);
            return new List<string>();
        }
    }

    public FabricLoaderVersion? GetFabricVersionInfo(string version)
    {
        lock (_fabricVersionMap)
        {
            return _fabricVersionMap.TryGetValue(version, out var v) ? v : null;
        }
    }

    public FabricLoaderVersion? GetLegacyFabricVersionInfo(string version)
    {
        lock (_legacyFabricVersionMap)
        {
            return _legacyFabricVersionMap.TryGetValue(version, out var v) ? v : null;
        }
    }

    public QuiltLoaderVersion? GetQuiltVersionInfo(string version)
    {
        lock (_quiltVersionMap)
        {
            return _quiltVersionMap.TryGetValue(version, out var v) ? v : null;
        }
    }

    public OptifineVersionInfo? GetOptifineVersionInfo(string version)
    {
        lock (_optifineVersionMap)
        {
            return _optifineVersionMap.TryGetValue(version, out var v) ? v : null;
        }
    }
}
