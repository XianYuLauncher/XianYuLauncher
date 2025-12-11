namespace XMCL2025.Core.Services.DownloadSource;

/// <summary>
/// 下载源工厂，用于创建和管理不同类型的下载源
/// </summary>
public class DownloadSourceFactory
{
    private readonly Dictionary<string, IDownloadSource> _sources = new();
    private string _defaultSourceKey = "official";
    
    /// <summary>
    /// 初始化下载源工厂
    /// </summary>
    public DownloadSourceFactory()
    {
        // 注册默认下载源
        RegisterSource("official", new OfficialDownloadSource());
        RegisterSource("bmclapi", new BmclapiDownloadSource());
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
            throw new ArgumentException($"不存在标识为{key}的下载源", nameof(key));
        }
        
        _defaultSourceKey = key;
    }
    
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
}