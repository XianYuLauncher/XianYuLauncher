using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Mod 元数据信息
/// </summary>
public class ModMetadata
{
    /// <summary>
    /// Mod 名称
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Mod 描述
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Mod 图标 URL
    /// </summary>
    public string? IconUrl { get; set; }
    
    /// <summary>
    /// 来源平台（Modrinth/CurseForge）
    /// </summary>
    public string? Source { get; set; }
    
    /// <summary>
    /// 项目 ID（用于翻译服务）
    /// </summary>
    public string? ProjectId { get; set; }
    
    /// <summary>
    /// CurseForge Mod ID（用于翻译服务）
    /// </summary>
    public int CurseForgeModId { get; set; }
    
    /// <summary>
    /// 缓存时间
    /// </summary>
    public DateTime CachedAt { get; set; }
    
    /// <summary>
    /// 是否为失败结果（用于缓存失败的查询）
    /// </summary>
    public bool IsFailed { get; set; }
}

/// <summary>
/// Mod 信息服务，用于从 Modrinth/CurseForge 获取 Mod 描述信息
/// </summary>
public class ModInfoService
{
    private readonly HttpClient _httpClient;
    private readonly ITranslationService _translationService;
    private readonly CurseForgeService _curseForgeService;
    
    // 内存缓存
    private readonly Dictionary<string, ModMetadata?> _memoryCache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    
    // API 限流
    private readonly SemaphoreSlim _apiThrottle = new(3, 3);
    
    // 缓存过期时间（7天）
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromDays(7);
    
    // 缓存文件路径
    private string CacheFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XianYuLauncher",
        "Cache",
        "mod_info_cache.json"
    );
    
    public ModInfoService(
        HttpClient httpClient,
        ITranslationService translationService,
        CurseForgeService curseForgeService)
    {
        _httpClient = httpClient;
        _translationService = translationService;
        _curseForgeService = curseForgeService;
        
        // 启动时加载持久化缓存
        _ = LoadCacheFromDiskAsync();
    }

    /// <summary>
    /// 获取 Mod 信息（优先 Modrinth，其次 CurseForge）
    /// </summary>
    /// <param name="modFilePath">Mod 文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Mod 元数据，如果未找到则返回 null</returns>
    public async Task<ModMetadata?> GetModInfoAsync(string modFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(modFilePath) || !File.Exists(modFilePath))
        {
            return null;
        }
        
        // 使用文件路径作为缓存键
        var cacheKey = modFilePath;
        
        // 检查内存缓存
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out var cached))
            {
                // 检查缓存是否过期
                if (cached != null && (DateTime.Now - cached.CachedAt) < CacheExpiration)
                {
                    // 如果是失败结果，返回 null 但不重新请求
                    return cached.IsFailed ? null : cached;
                }
                else
                {
                    // 缓存过期，移除
                    _memoryCache.Remove(cacheKey);
                }
            }
        }
        finally
        {
            _cacheLock.Release();
        }
        
        // 限流
        await _apiThrottle.WaitAsync(cancellationToken);
        try
        {
            // 1. 计算 SHA1 哈希（用于 Modrinth）
            var sha1Hash = await CalculateSHA1Async(modFilePath, cancellationToken);
            
            // 2. 尝试从 Modrinth 获取
            var modrinthInfo = await GetModrinthInfoBySHA1Async(sha1Hash, cancellationToken);
            if (modrinthInfo != null)
            {
                // 尝试获取翻译
                await TryTranslateDescriptionAsync(modrinthInfo, cancellationToken);
                modrinthInfo.CachedAt = DateTime.Now;
                await CacheResultAsync(cacheKey, modrinthInfo);
                return modrinthInfo;
            }
            
            // 3. 尝试从 CurseForge 获取
            var fingerprint = await Task.Run(() => CurseForgeFingerprintHelper.ComputeFingerprint(modFilePath), cancellationToken);
            var curseforgeInfo = await GetCurseForgeInfoByFingerprintAsync(fingerprint, cancellationToken);
            if (curseforgeInfo != null)
            {
                // 尝试获取翻译
                await TryTranslateDescriptionAsync(curseforgeInfo, cancellationToken);
                curseforgeInfo.CachedAt = DateTime.Now;
                await CacheResultAsync(cacheKey, curseforgeInfo);
                return curseforgeInfo;
            }
            
            // 未找到，缓存失败结果避免重复查询
            var failedResult = new ModMetadata
            {
                IsFailed = true,
                CachedAt = DateTime.Now
            };
            await CacheResultAsync(cacheKey, failedResult);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // 静默失败，不输出 Debug 信息
            return null;
        }
        finally
        {
            _apiThrottle.Release();
        }
    }
    
    /// <summary>
    /// 从 Modrinth 通过 SHA1 获取 Mod 信息
    /// </summary>
    private async Task<ModMetadata?> GetModrinthInfoBySHA1Async(string sha1, CancellationToken cancellationToken)
    {
        try
        {
            // 获取版本信息
            var versionUrl = $"https://api.modrinth.com/v2/version_file/{sha1}?algorithm=sha1";
            using var versionRequest = new HttpRequestMessage(HttpMethod.Get, versionUrl);
            versionRequest.Headers.Add("User-Agent", "XianYuLauncher/1.0");
            
            var versionResponse = await _httpClient.SendAsync(versionRequest, cancellationToken);
            if (!versionResponse.IsSuccessStatusCode)
            {
                return null;
            }
            
            var versionJson = await versionResponse.Content.ReadAsStringAsync(cancellationToken);
            using var versionDoc = JsonDocument.Parse(versionJson);
            var projectId = versionDoc.RootElement.GetProperty("project_id").GetString();
            
            if (string.IsNullOrEmpty(projectId))
            {
                return null;
            }
            
            // 获取项目详情
            var projectUrl = $"https://api.modrinth.com/v2/project/{projectId}";
            using var projectRequest = new HttpRequestMessage(HttpMethod.Get, projectUrl);
            projectRequest.Headers.Add("User-Agent", "XianYuLauncher/1.0");
            
            var projectResponse = await _httpClient.SendAsync(projectRequest, cancellationToken);
            if (!projectResponse.IsSuccessStatusCode)
            {
                return null;
            }
            
            var projectJson = await projectResponse.Content.ReadAsStringAsync(cancellationToken);
            using var projectDoc = JsonDocument.Parse(projectJson);
            var root = projectDoc.RootElement;
            
            return new ModMetadata
            {
                Name = root.TryGetProperty("title", out var title) ? title.GetString() : null,
                Description = root.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                IconUrl = root.TryGetProperty("icon_url", out var icon) ? icon.GetString() : null,
                Source = "Modrinth",
                ProjectId = projectId
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从 CurseForge 通过 Fingerprint 获取 Mod 信息
    /// </summary>
    private async Task<ModMetadata?> GetCurseForgeInfoByFingerprintAsync(uint fingerprint, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _curseForgeService.GetFingerprintMatchesAsync(new List<uint> { fingerprint });
            
            if (result?.ExactMatches == null || result.ExactMatches.Count == 0)
            {
                return null;
            }
            
            var match = result.ExactMatches[0];
            var modId = match.Id;
            
            // 获取完整的 Mod 信息（包括描述和 Logo）
            var mod = await _curseForgeService.GetModDetailAsync(modId);
            if (mod == null)
            {
                return null;
            }
            
            return new ModMetadata
            {
                Name = mod.Name,
                Description = mod.Summary, // CurseForge 使用 Summary 作为简短描述
                IconUrl = mod.Logo?.ThumbnailUrl,
                Source = "CurseForge",
                CurseForgeModId = modId
            };
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// 尝试翻译描述
    /// </summary>
    private async Task TryTranslateDescriptionAsync(ModMetadata metadata, CancellationToken cancellationToken)
    {
        try
        {
            if (!_translationService.ShouldUseTranslation())
            {
                return;
            }
            
            if (metadata.Source == "Modrinth" && !string.IsNullOrEmpty(metadata.ProjectId))
            {
                var translation = await _translationService.GetModrinthTranslationAsync(metadata.ProjectId);
                if (translation != null && !string.IsNullOrEmpty(translation.Translated))
                {
                    metadata.Description = translation.Translated;
                }
            }
            else if (metadata.Source == "CurseForge" && metadata.CurseForgeModId > 0)
            {
                var translation = await _translationService.GetCurseForgeTranslationAsync(metadata.CurseForgeModId);
                if (translation != null && !string.IsNullOrEmpty(translation.Translated))
                {
                    metadata.Description = translation.Translated;
                }
            }
        }
        catch
        {
            // 翻译失败不影响主流程
        }
    }
    
    /// <summary>
    /// 计算文件的 SHA1 哈希
    /// </summary>
    private async Task<string> CalculateSHA1Async(string filePath, CancellationToken cancellationToken)
    {
        using var sha1 = SHA1.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha1.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
    
    /// <summary>
    /// 缓存查询结果
    /// </summary>
    private async Task CacheResultAsync(string filePath, ModMetadata? metadata)
    {
        await _cacheLock.WaitAsync();
        try
        {
            _memoryCache[filePath] = metadata;
        }
        finally
        {
            _cacheLock.Release();
        }
        
        // 异步保存到磁盘（不等待）
        _ = SaveCacheToDiskAsync();
    }
    
    /// <summary>
    /// 加载持久化缓存
    /// </summary>
    private async Task LoadCacheFromDiskAsync()
    {
        try
        {
            if (!File.Exists(CacheFilePath))
            {
                return;
            }
            
            var json = await File.ReadAllTextAsync(CacheFilePath);
            var cache = JsonSerializer.Deserialize<Dictionary<string, ModMetadata>>(json);
            
            if (cache != null)
            {
                await _cacheLock.WaitAsync();
                try
                {
                    foreach (var kvp in cache)
                    {
                        _memoryCache[kvp.Key] = kvp.Value;
                    }
                }
                finally
                {
                    _cacheLock.Release();
                }
            }
        }
        catch
        {
            // 加载失败不影响程序运行
        }
    }
    
    /// <summary>
    /// 保存缓存到磁盘
    /// </summary>
    private async Task SaveCacheToDiskAsync()
    {
        try
        {
            await _cacheLock.WaitAsync();
            Dictionary<string, ModMetadata> cacheToSave;
            try
            {
                cacheToSave = new Dictionary<string, ModMetadata>(_memoryCache.Where(kvp => kvp.Value != null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value!));
            }
            finally
            {
                _cacheLock.Release();
            }
            
            var directory = Path.GetDirectoryName(CacheFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var json = JsonSerializer.Serialize(cacheToSave, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(CacheFilePath, json);
        }
        catch
        {
            // 保存失败不影响程序运行
        }
    }
    
    /// <summary>
    /// 清除缓存
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            _memoryCache.Clear();
        }
        finally
        {
            _cacheLock.Release();
        }
        
        try
        {
            if (File.Exists(CacheFilePath))
            {
                File.Delete(CacheFilePath);
            }
        }
        catch
        {
            // 删除失败不影响程序运行
        }
    }
}
