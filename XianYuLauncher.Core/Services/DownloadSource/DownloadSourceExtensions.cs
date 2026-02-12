namespace XianYuLauncher.Core.Services.DownloadSource;

/// <summary>
/// 下载源扩展方法
/// </summary>
public static class DownloadSourceExtensions
{
    /// <summary>
    /// 判断下载源是否需要 BMCLAPI User-Agent
    /// 通过检查源的 Key、Name 或实际 URL 来判断
    /// </summary>
    /// <param name="source">下载源</param>
    /// <param name="url">可选的 URL，用于更精确的判断</param>
    /// <returns>如果需要 BMCLAPI UA 则返回 true</returns>
    public static bool RequiresBmclapiUserAgent(this IDownloadSource source, string? url = null)
    {
        // 1. 检查 Key（内置 BMCLAPI 源）
        if (source.Key.Equals("bmclapi", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        // 2. 检查 Name（兼容旧代码）
        if (source.Name.Equals("BMCLAPI", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        // 3. 如果提供了 URL，检查 URL 中是否包含 bmclapi 域名
        if (!string.IsNullOrEmpty(url))
        {
            var lowerUrl = url.ToLowerInvariant();
            if (lowerUrl.Contains("bmclapi") || lowerUrl.Contains("bangbang93.com"))
            {
                return true;
            }
        }
        
        // 4. 对于自定义源，尝试通过版本清单 URL 判断
        try
        {
            var manifestUrl = source.GetVersionManifestUrl().ToLowerInvariant();
            if (manifestUrl.Contains("bmclapi") || manifestUrl.Contains("bangbang93.com"))
            {
                return true;
            }
        }
        catch
        {
            // 忽略异常，继续判断
        }
        
        return false;
    }
}
