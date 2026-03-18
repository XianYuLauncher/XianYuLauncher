using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// MCIM翻译服务实现
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, McimTranslationResponse> _translationCache;
    private readonly Dictionary<string, DateTimeOffset> _translationCacheTimestamps;
    private readonly SemaphoreSlim _translationCacheLock = new(1, 1);
    private readonly Dictionary<string, string> _nameTranslationMap = new(StringComparer.OrdinalIgnoreCase);
    // Reverse Map: Key=ChineseName (LowerCased), Value=EnglishName or Slug
    private readonly Dictionary<string, string> _chineseToEnglishMap = new(StringComparer.OrdinalIgnoreCase);
    private bool _isNameTranslationInitialized = false;
    private bool _isTranslationCacheLoaded;
    private const string ModrinthTranslationApiUrl = "https://mod.mcimirror.top/translate/modrinth";
    private const string CurseForgeTranslationApiUrl = "https://mod.mcimirror.top/translate/curseforge";
    private const string TranslationCacheFileName = "translation_cache.json";
    private static readonly TimeSpan TranslationCacheExpiration = TimeSpan.FromDays(3);

    private sealed class TranslationCacheItem
    {
        public DateTimeOffset CachedAt { get; set; }
        public McimTranslationResponse Value { get; set; } = new();
    }

    private sealed class TranslationCacheStore
    {
        public Dictionary<string, TranslationCacheItem> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// 翻译服务单例实例（用于Models中访问）
    /// </summary>
    public static TranslationService Instance { get; private set; } = null!;

    // 添加一个静态属性来存储当前语言设置
    private static string _currentLanguage = "zh-CN";
    
    /// <summary>
    /// 设置当前语言（由LanguageSelectorService调用）
    /// </summary>
    public static void SetCurrentLanguage(string language)
    {
        _currentLanguage = language;
        System.Diagnostics.Debug.WriteLine($"[翻译服务] 语言已设置为: {language}");
    }
    
    /// <summary>
    /// 获取当前语言设置
    /// </summary>
    public static string GetCurrentLanguage()
    {
        return _currentLanguage;
    }
    
    public TranslationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _translationCache = new Dictionary<string, McimTranslationResponse>(StringComparer.OrdinalIgnoreCase);
        _translationCacheTimestamps = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        Instance = this;
    }
    
    /// <summary>
    /// 检查是否应该使用翻译（当前语言是否为中文）
    /// </summary>
    public bool ShouldUseTranslation()
    {
        try
        {
            // 使用静态语言字段而不是 CultureInfo，避免跨程序集的文化信息不同步问题
            bool isChinese = _currentLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            
            // 注释掉频繁输出的日志
            // System.Diagnostics.Debug.WriteLine($"[翻译服务] 当前语言设置: {_currentLanguage}, 是否为中文: {isChinese}");
            
            return isChinese;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 获取Modrinth项目的中文翻译
    /// </summary>
    public async Task<McimTranslationResponse?> GetModrinthTranslationAsync(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            return null;
        }

        await EnsureTranslationCacheLoadedAsync();
        
        // 检查缓存
        var cacheKey = $"modrinth_{projectId}";
        var cachedTranslation = await GetValidCachedTranslationAsync(cacheKey);
        if (cachedTranslation != null)
        {
            return cachedTranslation;
        }
        
        try
        {
            var url = $"{ModrinthTranslationApiUrl}?project_id={projectId}";
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 请求Modrinth翻译: {projectId}");

            // 使用GET请求而不是POST
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[翻译服务] Modrinth翻译失败: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var translation = JsonConvert.DeserializeObject<McimTranslationResponse>(content);

            if (translation != null && !string.IsNullOrEmpty(translation.Translated))
            {
                // 缓存翻译结果
                await SaveTranslationCacheAsync(cacheKey, translation);
                System.Diagnostics.Debug.WriteLine($"[翻译服务] Modrinth翻译成功: {projectId}");
                return translation;
            }

            System.Diagnostics.Debug.WriteLine($"[翻译服务] Modrinth翻译无结果: {projectId}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[翻译服务] Modrinth翻译异常: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 获取CurseForge Mod的中文翻译
    /// </summary>
    public async Task<McimTranslationResponse?> GetCurseForgeTranslationAsync(int modId)
    {
        if (modId <= 0)
        {
            return null;
        }

        await EnsureTranslationCacheLoadedAsync();
        
        // 检查缓存
        var cacheKey = $"curseforge_{modId}";
        var cachedTranslation = await GetValidCachedTranslationAsync(cacheKey);
        if (cachedTranslation != null)
        {
            return cachedTranslation;
        }
        
        try
        {
            var url = $"{CurseForgeTranslationApiUrl}?modId={modId}";
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 请求CurseForge翻译: {modId}");

            // 使用GET请求而不是POST
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[翻译服务] CurseForge翻译失败: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var translation = JsonConvert.DeserializeObject<McimTranslationResponse>(content);

            if (translation != null && !string.IsNullOrEmpty(translation.Translated))
            {
                // 缓存翻译结果
                await SaveTranslationCacheAsync(cacheKey, translation);
                System.Diagnostics.Debug.WriteLine($"[翻译服务] CurseForge翻译成功: {modId}");
                return translation;
            }

            System.Diagnostics.Debug.WriteLine($"[翻译服务] CurseForge翻译无结果: {modId}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[翻译服务] CurseForge翻译异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 初始化Mod名称翻译数据
    /// </summary>
    public async Task InitializeNameTranslationAsync(string dataFilePath)
    {
        if (!System.IO.File.Exists(dataFilePath)) return;

        try 
        {
            // 如果是重新加载，先清空现有数据
            if (_isNameTranslationInitialized)
            {
                _nameTranslationMap.Clear();
                _chineseToEnglishMap.Clear();
            }

            var lines = await System.IO.File.ReadAllLinesAsync(dataFilePath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('|');
                // Format: ModrinthSlug|CurseForgeSlug|ChineseName|EnglishName|Abbreviation
                if (parts.Length < 3) continue;

                var modrinthSlug = parts[0].Trim();
                var curseforgeSlug = parts[1].Trim();
                var chineseName = parts[2].Trim();
                
                if (string.IsNullOrEmpty(chineseName)) continue;

                if (!string.IsNullOrEmpty(modrinthSlug) && !_nameTranslationMap.ContainsKey(modrinthSlug))
                {
                    _nameTranslationMap[modrinthSlug] = chineseName;
                }
                
                if (!string.IsNullOrEmpty(curseforgeSlug) && !_nameTranslationMap.ContainsKey(curseforgeSlug))
                {
                    _nameTranslationMap[curseforgeSlug] = chineseName;
                }

                // Populate Reverse Map for Search (Chinese -> English)
                if (!_chineseToEnglishMap.ContainsKey(chineseName))
                {
                    // Prefer English Name if available, otherwise use Slug
                    string englishKey = "";
                    if (parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]))
                    {
                         englishKey = parts[3].Trim();
                    }
                    else if (!string.IsNullOrEmpty(modrinthSlug))
                    {
                        englishKey = modrinthSlug;
                    } 
                    else if (!string.IsNullOrEmpty(curseforgeSlug))
                    {
                        englishKey = curseforgeSlug;
                    }

                    if (!string.IsNullOrEmpty(englishKey))
                    {
                        _chineseToEnglishMap[chineseName] = englishKey;
                    }
                }
            }
            _isNameTranslationInitialized = true;
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 名称数据加载完成，共 {_nameTranslationMap.Count} 条记录，反向索引 {_chineseToEnglishMap.Count} 条");
        }
        catch (Exception ex)
        {
             System.Diagnostics.Debug.WriteLine($"[翻译服务] 加载名称数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取Mod翻译名称
    /// </summary>
    public string GetTranslatedName(string slug, string originalName)
    {
        if (string.IsNullOrWhiteSpace(slug) || !ShouldUseTranslation()) return originalName;

        if (_nameTranslationMap.TryGetValue(slug, out var chineseName))
        {
            // 如果中文名为空，或与原名相同（不区分大小写），则直接返回原名，避免出现 "Name | Name" 的情况
            if (string.IsNullOrWhiteSpace(chineseName) || 
                chineseName.Equals(originalName, StringComparison.OrdinalIgnoreCase))
            {
                return originalName;
            }

            return $"{chineseName} | {originalName}";
        }
        
        return originalName;
    }

    /// <summary>
    /// 获取用于搜索的英文关键词
    /// </summary>
    public string GetEnglishKeywordForSearch(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return keyword;

        // 1. 精确匹配 (最快，O(1))
        if (_chineseToEnglishMap.TryGetValue(keyword, out var englishName))
        {
            return englishName;
        }

        // 2. 如果包含中文，尝试模糊匹配 (O(N))
        // 只有当找不到精确匹配时才遍历，1.5万条数据遍历很快 (<5ms)，不会造成卡顿
        if (HasChinese(keyword))
        {
            // 优先匹配以关键词开头的
            foreach (var kvp in _chineseToEnglishMap)
            {
                if (kvp.Key.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            // 其次匹配包含关键词的
            foreach (var kvp in _chineseToEnglishMap)
            {
                if (kvp.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
        }

        return keyword;
    }

    private bool HasChinese(string str)
    {
        foreach (char c in str)
        {
            if (c >= 0x4E00 && c <= 0x9FA5) return true;
        }
        return false;
    }

    private async Task EnsureTranslationCacheLoadedAsync()
    {
        if (_isTranslationCacheLoaded)
        {
            return;
        }

        await _translationCacheLock.WaitAsync();
        try
        {
            if (_isTranslationCacheLoaded)
            {
                return;
            }

            var cacheFilePath = GetTranslationCacheFilePath();
            if (File.Exists(cacheFilePath))
            {
                var json = await File.ReadAllTextAsync(cacheFilePath);
                var cacheStore = JsonConvert.DeserializeObject<TranslationCacheStore>(json);
                if (cacheStore?.Entries != null)
                {
                    var now = DateTimeOffset.UtcNow;
                    foreach (var entry in cacheStore.Entries)
                    {
                        var age = now - entry.Value.CachedAt;
                        if (age < TranslationCacheExpiration && entry.Value.Value != null)
                        {
                            _translationCache[entry.Key] = entry.Value.Value;
                            _translationCacheTimestamps[entry.Key] = entry.Value.CachedAt;
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[翻译缓存] 已加载磁盘缓存，共 {_translationCache.Count} 条");
                }
            }

            _isTranslationCacheLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[翻译缓存] 加载磁盘缓存失败: {ex.Message}");
            _isTranslationCacheLoaded = true;
        }
        finally
        {
            _translationCacheLock.Release();
        }
    }

    private async Task<McimTranslationResponse?> GetValidCachedTranslationAsync(string cacheKey)
    {
        await _translationCacheLock.WaitAsync();
        try
        {
            if (!_translationCache.TryGetValue(cacheKey, out var cachedTranslation))
            {
                return null;
            }

            if (!_translationCacheTimestamps.TryGetValue(cacheKey, out var cachedAt))
            {
                return null;
            }

            var age = DateTimeOffset.UtcNow - cachedAt;
            if (age >= TranslationCacheExpiration)
            {
                return null;
            }

            return cachedTranslation;
        }
        finally
        {
            _translationCacheLock.Release();
        }
    }

    private async Task SaveTranslationCacheAsync(string cacheKey, McimTranslationResponse translation)
    {
        Dictionary<string, TranslationCacheItem>? snapshot = null;

        await _translationCacheLock.WaitAsync();
        try
        {
            _translationCache[cacheKey] = translation;
            _translationCacheTimestamps[cacheKey] = DateTimeOffset.UtcNow;

            // 在锁内基于当前内存缓存构建快照，避免锁外直接访问共享状态
            snapshot = new Dictionary<string, TranslationCacheItem>(_translationCache.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _translationCache)
            {
                if (_translationCacheTimestamps.TryGetValue(kvp.Key, out var cachedAt))
                {
                    snapshot[kvp.Key] = new TranslationCacheItem
                    {
                        CachedAt = cachedAt,
                        Value = kvp.Value,
                    };
                }
            }
        }
        finally
        {
            _translationCacheLock.Release();
        }

        if (snapshot != null)
        {
            await PersistTranslationCacheSnapshotAsync(snapshot);
        }
    }

    private async Task PersistTranslationCacheSnapshotAsync(IDictionary<string, TranslationCacheItem> entries)
    {
        try
        {
            var cacheStore = new TranslationCacheStore
            {
                Entries = new Dictionary<string, TranslationCacheItem>(entries, StringComparer.OrdinalIgnoreCase),
            };

            var json = JsonConvert.SerializeObject(cacheStore, Formatting.None);
            var cacheFilePath = GetTranslationCacheFilePath();
            var directory = Path.GetDirectoryName(cacheFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(cacheFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[翻译缓存] 持久化失败: {ex.Message}");
        }
    }

    private static string GetTranslationCacheFilePath()
    {
        return Path.Combine(AppEnvironment.SafeCachePath, TranslationCacheFileName);
    }
}
