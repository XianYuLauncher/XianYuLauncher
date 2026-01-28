using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// MCIM翻译服务实现
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, McimTranslationResponse> _translationCache;
    private readonly Dictionary<string, string> _nameTranslationMap = new(StringComparer.OrdinalIgnoreCase);
    // Reverse Map: Key=ChineseName (LowerCased), Value=EnglishName or Slug
    private readonly Dictionary<string, string> _chineseToEnglishMap = new(StringComparer.OrdinalIgnoreCase);
    private bool _isNameTranslationInitialized = false;
    private const string ModrinthTranslationApiUrl = "https://mod.mcimirror.top/translate/modrinth";
    private const string CurseForgeTranslationApiUrl = "https://mod.mcimirror.top/translate/curseforge";
    
    /// <summary>
    /// 翻译服务单例实例（用于Models中访问）
    /// </summary>
    public static TranslationService Instance { get; private set; }

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
        _translationCache = new Dictionary<string, McimTranslationResponse>();
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
            
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 当前语言设置: {_currentLanguage}, 是否为中文: {isChinese}");
            
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
        
        // 检查缓存
        var cacheKey = $"modrinth_{projectId}";
        if (_translationCache.TryGetValue(cacheKey, out var cachedTranslation))
        {
            return cachedTranslation;
        }
        
        try
        {
            var url = $"{ModrinthTranslationApiUrl}?project_id={projectId}";
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 请求Modrinth翻译: {url}");
            
            // 使用GET请求而不是POST
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[翻译服务] Modrinth翻译请求失败: {response.StatusCode}");
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var translation = JsonConvert.DeserializeObject<McimTranslationResponse>(content);
            
            if (translation != null && !string.IsNullOrEmpty(translation.Translated))
            {
                // 缓存翻译结果
                _translationCache[cacheKey] = translation;
                System.Diagnostics.Debug.WriteLine($"[翻译服务] Modrinth翻译成功: {projectId}");
                return translation;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 获取Modrinth翻译失败: {ex.Message}");
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
        
        // 检查缓存
        var cacheKey = $"curseforge_{modId}";
        if (_translationCache.TryGetValue(cacheKey, out var cachedTranslation))
        {
            return cachedTranslation;
        }
        
        try
        {
            var url = $"{CurseForgeTranslationApiUrl}?modId={modId}";
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 请求CurseForge翻译: {url}");
            
            // 使用GET请求而不是POST
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[翻译服务] CurseForge翻译请求失败: {response.StatusCode}");
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var translation = JsonConvert.DeserializeObject<McimTranslationResponse>(content);
            
            if (translation != null && !string.IsNullOrEmpty(translation.Translated))
            {
                // 缓存翻译结果
                _translationCache[cacheKey] = translation;
                System.Diagnostics.Debug.WriteLine($"[翻译服务] CurseForge翻译成功: {modId}");
                return translation;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[翻译服务] 获取CurseForge翻译失败: {ex.Message}");
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
}
