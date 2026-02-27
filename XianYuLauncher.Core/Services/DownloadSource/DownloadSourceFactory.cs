using System.Linq;

namespace XianYuLauncher.Core.Services.DownloadSource;

/// <summary>
/// 下载源工厂，用于创建和管理不同类型的下载源
/// </summary>
public class DownloadSourceFactory
{
    private readonly Dictionary<string, IDownloadSource> _sources = new();
    private string _defaultSourceKey = "official";
    private string _modrinthSourceKey = "official"; // Modrinth专用下载源
    private string _curseforgeSourceKey = "official"; // CurseForge专用下载源
    private string _versionManifestSourceKey = "official"; // 版本清单专用下载源
    private string _fileDownloadSourceKey = "official"; // 文件下载专用下载源
    private string _forgeSourceKey = "official"; // Forge专用下载源
    private string _fabricSourceKey = "official"; // Fabric专用下载源
    private string _neoforgeSourceKey = "official"; // NeoForge专用下载源
    private string _quiltSourceKey = "official"; // Quilt专用下载源
    private string _liteLoaderSourceKey = "official"; // LiteLoader专用下载源
    private string _legacyFabricSourceKey = "official"; // LegacyFabric专用下载源
    private string _cleanroomSourceKey = "official"; // Cleanroom专用下载源
    private string _optifineSourceKey = "official"; // OptiFine专用下载源
    
    /// <summary>
    /// 初始化下载源工厂
    /// </summary>
    public DownloadSourceFactory()
    {
        // 注册默认下载源
        RegisterSource("official", new OfficialDownloadSource());
        RegisterSource("bmclapi", new BmclapiDownloadSource());
        RegisterSource("mcim", new McimDownloadSource());
    }
    
    /// <summary>
    /// 注册新的下载源
    /// </summary>
    /// <param name="key">下载源唯一标识</param>
    /// <param name="source">下载源实例</param>
    public void RegisterSource(string key, IDownloadSource source)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");
        }
        
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source), "下载源实例不能为空");
        }
        
        _sources[key] = source;
    }
    
    /// <summary>
    /// 注销下载源
    /// </summary>
    /// <param name="key">下载源标识</param>
    /// <returns>是否成功注销</returns>
    public bool UnregisterSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }
        
        // 不允许注销内置源
        if (key == "official" || key == "bmclapi" || key == "mcim")
        {
            return false;
        }
        
        return _sources.Remove(key);
    }
    
    /// <summary>
    /// 获取指定标识的下载源
    /// </summary>
    /// <param name="key">下载源标识</param>
    /// <returns>下载源实例，如果不存在则返回默认下载源</returns>
    public IDownloadSource GetSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return GetDefaultSource();
        }
        
        return _sources.TryGetValue(key, out var source) ? source : GetDefaultSource();
    }
    
    /// <summary>
    /// 获取默认下载源
    /// </summary>
    /// <returns>默认下载源实例</returns>
    public IDownloadSource GetDefaultSource()
    {
        return _sources[_defaultSourceKey];
    }
    
    /// <summary>
    /// 设置默认下载源
    /// </summary>
    /// <param name="key">下载源标识</param>
    public void SetDefaultSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");
        }
        
        if (!_sources.ContainsKey(key))
        {
            System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] 错误：尝试设置不存在的下载源为默认源: {key}");
            System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] 当前已注册的源: {string.Join(", ", _sources.Keys)}");
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));
        }
        
        _defaultSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] 默认下载源已设置为: {key} ({_sources[key].Name})");
    }
    
    /// <summary>
    /// 获取Modrinth专用下载源
    /// </summary>
    /// <returns>Modrinth下载源实例</returns>
    public IDownloadSource GetModrinthSource()
    {
        return _sources.TryGetValue(_modrinthSourceKey, out var source) ? source : GetDefaultSource();
    }
    
    /// <summary>
    /// 设置Modrinth专用下载源
    /// </summary>
    /// <param name="key">下载源标识（official/mcim）</param>
    public void SetModrinthSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");
        }
        
        if (!_sources.ContainsKey(key))
        {
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));
        }
        
        _modrinthSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] Modrinth下载源已设置为: {key}");
    }
    
    /// <summary>
    /// 获取当前Modrinth下载源标识
    /// </summary>
    public string GetModrinthSourceKey() => _modrinthSourceKey;

    /// <summary>
    /// 获取CurseForge专用下载源
    /// </summary>
    /// <returns>CurseForge下载源实例</returns>
    public IDownloadSource GetCurseForgeSource()
    {
        return _sources.TryGetValue(_curseforgeSourceKey, out var source) ? source : GetDefaultSource();
    }

    /// <summary>
    /// 设置CurseForge专用下载源
    /// </summary>
    /// <param name="key">下载源标识（任意已注册的下载源，例如 official/bmclapi/mcim 或自定义源）</param>
    public void SetCurseForgeSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");
        }

        if (!_sources.ContainsKey(key))
        {
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));
        }

        _curseforgeSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] CurseForge下载源已设置为: {key}");
    }

    /// <summary>
    /// 获取当前CurseForge下载源标识
    /// </summary>
    public string GetCurseForgeSourceKey() => _curseforgeSourceKey;

    /// <summary>
    /// 获取Forge专用下载源
    /// </summary>
    public IDownloadSource GetForgeSource()
    {
        return _sources.TryGetValue(_forgeSourceKey, out var source) ? source : GetDefaultSource();
    }

    /// <summary>
    /// 设置Forge专用下载源
    /// </summary>
    public void SetForgeSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");

        if (!_sources.ContainsKey(key))
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));

        _forgeSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] Forge下载源已设置为: {key}");
    }

    /// <summary>
    /// 获取当前Forge下载源标识
    /// </summary>
    public string GetForgeSourceKey() => _forgeSourceKey;

    /// <summary>
    /// 获取Fabric专用下载源
    /// </summary>
    public IDownloadSource GetFabricSource()
    {
        return _sources.TryGetValue(_fabricSourceKey, out var source) ? source : GetDefaultSource();
    }

    /// <summary>
    /// 设置Fabric专用下载源
    /// </summary>
    public void SetFabricSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");

        if (!_sources.ContainsKey(key))
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));

        _fabricSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] Fabric下载源已设置为: {key}");
    }

    /// <summary>
    /// 获取当前Fabric下载源标识
    /// </summary>
    public string GetFabricSourceKey() => _fabricSourceKey;

    /// <summary>
    /// 获取NeoForge专用下载源
    /// </summary>
    public IDownloadSource GetNeoForgeSource()
    {
        return _sources.TryGetValue(_neoforgeSourceKey, out var source) ? source : GetDefaultSource();
    }

    /// <summary>
    /// 设置NeoForge专用下载源
    /// </summary>
    public void SetNeoForgeSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");

        if (!_sources.ContainsKey(key))
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));

        _neoforgeSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] NeoForge下载源已设置为: {key}");
    }

    /// <summary>
    /// 获取当前NeoForge下载源标识
    /// </summary>
    public string GetNeoForgeSourceKey() => _neoforgeSourceKey;

    /// <summary>
    /// 获取Quilt专用下载源
    /// </summary>
    public IDownloadSource GetQuiltSource()
    {
        return _sources.TryGetValue(_quiltSourceKey, out var source) ? source : GetDefaultSource();
    }

    /// <summary>
    /// 设置Quilt专用下载源
    /// </summary>
    public void SetQuiltSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");

        if (!_sources.ContainsKey(key))
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));

        _quiltSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] Quilt下载源已设置为: {key}");
    }

    /// <summary>
    /// 获取当前Quilt下载源标识
    /// </summary>
    public string GetQuiltSourceKey() => _quiltSourceKey;

    /// <summary>
    /// 获取OptiFine专用下载源
    /// </summary>
    public IDownloadSource GetOptifineSource()
    {
        if (_sources.TryGetValue(_optifineSourceKey, out var source) && source.SupportsOptifine)
        {
            return source;
        }

        // 当前选择的源不支持 OptiFine，回退到 BMCLAPI
        if (_sources.TryGetValue("bmclapi", out var bmclapiSource))
        {
            System.Diagnostics.Debug.WriteLine("[DownloadSourceFactory] 当前 OptiFine 源不支持，回退到 BMCLAPI");
            return bmclapiSource;
        }

        // 没有 BMCLAPI，返回默认源
        return GetDefaultSource();
    }

    /// <summary>
    /// 设置OptiFine专用下载源
    /// </summary>
    public void SetOptifineSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");

        if (!_sources.ContainsKey(key))
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));

        _optifineSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] OptiFine下载源已设置为: {key}");
    }

    /// <summary>
    /// 获取当前OptiFine下载源标识
    /// </summary>
    public string GetOptifineSourceKey() => _optifineSourceKey;

    /// <summary>
    /// 获取版本清单专用下载源
    /// </summary>
    public IDownloadSource GetVersionManifestSource()
    {
        return _sources.TryGetValue(_versionManifestSourceKey, out var source) ? source : GetDefaultSource();
    }

    /// <summary>
    /// 设置版本清单专用下载源
    /// </summary>
    public void SetVersionManifestSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");

        if (!_sources.ContainsKey(key))
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));

        _versionManifestSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] 版本清单下载源已设置为: {key}");
    }

    /// <summary>
    /// 获取当前版本清单下载源标识
    /// </summary>
    public string GetVersionManifestSourceKey() => _versionManifestSourceKey;

    /// <summary>
    /// 获取文件下载专用下载源
    /// </summary>
    public IDownloadSource GetFileDownloadSource()
    {
        return _sources.TryGetValue(_fileDownloadSourceKey, out var source) ? source : GetDefaultSource();
    }

    /// <summary>
    /// 设置文件下载专用下载源
    /// </summary>
    public void SetFileDownloadSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");

        if (!_sources.ContainsKey(key))
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));

        _fileDownloadSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] 文件下载源已设置为: {key}");
    }

    /// <summary>
    /// 获取当前文件下载源标识
    /// </summary>
    public string GetFileDownloadSourceKey() => _fileDownloadSourceKey;

    /// <summary>
    /// 获取LiteLoader专用下载源
    /// </summary>
    public IDownloadSource GetLiteLoaderSource()
    {
        return _sources.TryGetValue(_liteLoaderSourceKey, out var source) ? source : GetDefaultSource();
    }

    /// <summary>
    /// 设置LiteLoader专用下载源
    /// </summary>
    public void SetLiteLoaderSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");

        if (!_sources.ContainsKey(key))
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));

        _liteLoaderSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] LiteLoader下载源已设置为: {key}");
    }

    /// <summary>
    /// 获取当前LiteLoader下载源标识
    /// </summary>
    public string GetLiteLoaderSourceKey() => _liteLoaderSourceKey;

    /// <summary>
    /// 获取LegacyFabric专用下载源
    /// </summary>
    public IDownloadSource GetLegacyFabricSource()
    {
        if (_sources.TryGetValue(_legacyFabricSourceKey, out var source) && source.SupportsLegacyFabric)
        {
            return source;
        }

        return _sources["official"];
    }

    /// <summary>
    /// 设置LegacyFabric专用下载源
    /// </summary>
    public void SetLegacyFabricSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");

        if (!_sources.ContainsKey(key))
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));

        if (!_sources[key].SupportsLegacyFabric)
        {
            _legacyFabricSourceKey = "official";
            System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] LegacyFabric 下载源 {key} 不受支持，已回退到 official");
            return;
        }

        _legacyFabricSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] LegacyFabric下载源已设置为: {key}");
    }

    /// <summary>
    /// 获取当前LegacyFabric下载源标识
    /// </summary>
    public string GetLegacyFabricSourceKey()
    {
        return _sources.TryGetValue(_legacyFabricSourceKey, out var source) && source.SupportsLegacyFabric
            ? _legacyFabricSourceKey
            : "official";
    }

    /// <summary>
    /// 获取Cleanroom专用下载源
    /// </summary>
    public IDownloadSource GetCleanroomSource()
    {
        return _sources.TryGetValue(_cleanroomSourceKey, out var source) ? source : GetDefaultSource();
    }

    /// <summary>
    /// 设置Cleanroom专用下载源
    /// </summary>
    public void SetCleanroomSource(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "下载源标识不能为空");

        if (!_sources.ContainsKey(key))
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));

        _cleanroomSourceKey = key;
        System.Diagnostics.Debug.WriteLine($"[DownloadSourceFactory] Cleanroom下载源已设置为: {key}");
    }

    /// <summary>
    /// 获取当前Cleanroom下载源标识
    /// </summary>
    public string GetCleanroomSourceKey() => _cleanroomSourceKey;

    /// <summary>
    /// 获取所有注册的下载源
    /// </summary>
    /// <returns>下载源字典</returns>
    public IReadOnlyDictionary<string, IDownloadSource> GetAllSources()
    {
        return _sources;
    }
    
    /// <summary>
    /// 获取指定类型的下载源
    /// </summary>
    /// <typeparam name="TSource">下载源类型</typeparam>
    /// <returns>下载源实例，如果不存在则返回null</returns>
    public TSource GetSourceByType<TSource>() where TSource : IDownloadSource
    {
        return _sources.Values.OfType<TSource>().FirstOrDefault();
    }
    
    /// <summary>
    /// 根据下载源名称获取下载源标识
    /// </summary>
    /// <param name="sourceName">下载源名称</param>
    /// <returns>下载源标识，如果不存在则返回默认标识</returns>
    public string GetKeyByName(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return _defaultSourceKey;
        }

        var source = _sources.FirstOrDefault(s => s.Value.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase));
        return source.Key ?? _defaultSourceKey;
    }

    /// <summary>
    /// 内置源显示名称映射
    /// </summary>
    private static readonly Dictionary<string, string> BuiltInSourceDisplayNames = new()
    {
        { "official", "官方源" },
        { "bmclapi", "BMCLAPI 镜像" },
        { "mcim", "MCIM 镜像" }
    };

    /// <summary>
    /// 获取支持游戏资源的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForGameResources()
    {
        return _sources.Values
            .Where(s => s.SupportsGameResources)
            .ToList();
    }

    /// <summary>
    /// 获取支持版本清单的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForVersionManifest()
    {
        return _sources.Values
            .Where(s => s.SupportsVersionManifest)
            .ToList();
    }

    /// <summary>
    /// 获取支持文件下载的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForFileDownload()
    {
        return _sources.Values
            .Where(s => s.SupportsFileDownload)
            .ToList();
    }

    /// <summary>
    /// 获取支持 Modrinth 的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForModrinth()
    {
        return _sources.Values
            .Where(s => s.SupportsModrinth)
            .ToList();
    }

    /// <summary>
    /// 获取支持 CurseForge 的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForCurseForge()
    {
        return _sources.Values
            .Where(s => s.SupportsCurseForge)
            .ToList();
    }

    #region ModLoader 支持

    /// <summary>
    /// 根据 ModLoader 类型获取支持的下载源
    /// </summary>
    /// <param name="loaderType">ModLoader 类型（forge/fabric/neoforge/quilt/liteloader/legacyfabric/cleanroom/optifine）</param>
    public IReadOnlyList<IDownloadSource> GetSourcesForModLoader(string loaderType)
    {
        return loaderType.ToLowerInvariant() switch
        {
            "forge" => GetSourcesForForge(),
            "fabric" => GetSourcesForFabric(),
            "neoforge" => GetSourcesForNeoForge(),
            "quilt" => GetSourcesForQuilt(),
            "liteloader" => GetSourcesForLiteLoader(),
            "legacyfabric" => GetSourcesForLegacyFabric(),
            "cleanroom" => GetSourcesForCleanroom(),
            "optifine" => GetSourcesForOptifine(),
            _ => new List<IDownloadSource>()
        };
    }

    /// <summary>
    /// 获取支持 Forge 的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForForge()
    {
        return _sources.Values.Where(s => s.SupportsForge).ToList();
    }

    /// <summary>
    /// 获取支持 Fabric 的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForFabric()
    {
        return _sources.Values.Where(s => s.SupportsFabric).ToList();
    }

    /// <summary>
    /// 获取支持 NeoForge 的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForNeoForge()
    {
        return _sources.Values.Where(s => s.SupportsNeoForge).ToList();
    }

    /// <summary>
    /// 获取支持 Quilt 的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForQuilt()
    {
        return _sources.Values.Where(s => s.SupportsQuilt).ToList();
    }

    /// <summary>
    /// 获取支持 LiteLoader 的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForLiteLoader()
    {
        return _sources.Values.Where(s => s.SupportsLiteLoader).ToList();
    }

    /// <summary>
    /// 获取支持 Legacy Fabric 的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForLegacyFabric()
    {
        return _sources.Values.Where(s => s.SupportsLegacyFabric).ToList();
    }

    /// <summary>
    /// 获取支持 Cleanroom 的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForCleanroom()
    {
        return _sources.Values.Where(s => s.SupportsCleanroom).ToList();
    }

    /// <summary>
    /// 获取支持 OptiFine 的所有下载源
    /// </summary>
    public IReadOnlyList<IDownloadSource> GetSourcesForOptifine()
    {
        return _sources.Values.Where(s => s.SupportsOptifine).ToList();
    }

    #endregion

    /// <summary>
    /// 获取下载源的显示名称
    /// </summary>
    /// <param name="key">下载源标识</param>
    /// <returns>显示名称</returns>
    public string GetDisplayName(string key)
    {
        if (BuiltInSourceDisplayNames.TryGetValue(key, out var displayName))
            return displayName;

        if (_sources.TryGetValue(key, out var source))
            return source.Name;

        return key;
    }
}
