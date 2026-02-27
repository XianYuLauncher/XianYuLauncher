using Microsoft.Extensions.Logging;

namespace XianYuLauncher.Core.Services.DownloadSource;

/// <summary>
/// 自定义下载源实现
/// 支持基于模板的 URL 映射和覆盖规则
/// </summary>
public class CustomDownloadSource : IDownloadSource
{
    private readonly string _key;
    private readonly string _name;
    private readonly string _baseUrl;
    private readonly DownloadSourceTemplate _template;
    private readonly Dictionary<string, string> _overrides;
    private readonly ILogger<CustomDownloadSource>? _logger;
    private readonly int _priority;

    public string Name => _name;
    public string Key => _key;

    /// <inheritdoc />
    public string Host
    {
        get
        {
            // 从 BaseUrl 提取主机名和端口
            var url = _baseUrl.TrimEnd('/');
            if (url.StartsWith("http://"))
                url = url.Substring(7);
            else if (url.StartsWith("https://"))
                url = url.Substring(8);

            var slashIndex = url.IndexOf('/');
            if (slashIndex >= 0)
                url = url.Substring(0, slashIndex);

            // 添加默认端口
            if (!url.Contains(':'))
                url = url + ":443";

            return url;
        }
    }

    /// <summary>
    /// 模板名称（official/community）
    /// </summary>
    public string TemplateName
    {
        get
        {
            // BmclapiTemplate 对应 official，McimTemplate 对应 community
            return _template switch
            {
                BmclapiTemplate => "official",
                McimTemplate => "community",
                _ => "community"
            };
        }
    }

    /// <inheritdoc />
    public bool SupportsGameResources => TemplateName == "official";

    /// <inheritdoc />
    public bool SupportsVersionManifest => TemplateName == "official";

    /// <inheritdoc />
    public bool SupportsFileDownload => TemplateName == "official";

    /// <inheritdoc />
    public bool SupportsModrinth => TemplateName == "community";

    /// <inheritdoc />
    public bool SupportsCurseForge => TemplateName == "community";

    #region ModLoader 支持

    /// <inheritdoc />
    public bool SupportsForge => TemplateName == "official";

    /// <inheritdoc />
    public bool SupportsFabric => TemplateName == "official";

    /// <inheritdoc />
    public bool SupportsNeoForge => TemplateName == "official";

    /// <inheritdoc />
    public bool SupportsQuilt => TemplateName == "official";

    /// <inheritdoc />
    public bool SupportsLiteLoader => TemplateName == "official";

    /// <inheritdoc />
    public bool SupportsLegacyFabric => TemplateName == "official";

    /// <inheritdoc />
    public bool SupportsCleanroom => TemplateName == "official";

    /// <inheritdoc />
    public bool SupportsOptifine => TemplateName == "official";

    #endregion

    /// <summary>
    /// 优先级（数值越大优先级越高）
    /// </summary>
    public int Priority => _priority;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="key">唯一标识键</param>
    /// <param name="name">显示名称</param>
    /// <param name="baseUrl">基础 URL</param>
    /// <param name="template">下载源模板</param>
    /// <param name="overrides">覆盖规则（可选）</param>
    /// <param name="priority">优先级（可选，默认 100）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public CustomDownloadSource(
        string key,
        string name,
        string baseUrl,
        DownloadSourceTemplate template,
        Dictionary<string, string>? overrides = null,
        int priority = 100,
        ILogger<CustomDownloadSource>? logger = null)
    {
        _key = key;
        _name = name;
        _baseUrl = baseUrl.TrimEnd('/');
        _template = template;
        _overrides = overrides ?? new Dictionary<string, string>();
        _priority = priority;
        _logger = logger;
    }

    #region Minecraft 官方资源

    public string GetVersionManifestUrl()
    {
        return ApplyTemplate("version_manifest", _template.GetVersionManifestUrl());
    }

    public string GetVersionInfoUrl(string versionId, string originalUrl)
    {
        var context = new Dictionary<string, string> { { "version", versionId } };
        return ApplyTemplate("version_info", _template.GetVersionInfoUrl(versionId, originalUrl), context);
    }

    public string GetResourceUrl(string resourceType, string originalUrl)
    {
        return ApplyTemplate($"resource_{resourceType}", _template.GetResourceUrl(resourceType, originalUrl));
    }

    public string GetClientJarUrl(string versionId, string originalUrl)
    {
        var context = new Dictionary<string, string> { { "version", versionId } };
        return ApplyTemplate("client_jar", _template.GetClientJarUrl(versionId, originalUrl), context);
    }

    public string GetClientJsonUrl(string versionId, string originalUrl)
    {
        var context = new Dictionary<string, string> { { "version", versionId } };
        return ApplyTemplate("client_json", _template.GetClientJsonUrl(versionId, originalUrl), context);
    }

    public string GetLibraryUrl(string libraryName, string originalUrl)
    {
        return ApplyTemplate("library", _template.GetLibraryUrl(libraryName, originalUrl));
    }

    #endregion

    #region ModLoader 资源

    public string GetNeoForgeVersionsUrl(string minecraftVersion)
    {
        var context = new Dictionary<string, string> { { "version", minecraftVersion } };
        return ApplyTemplate("neoforge_versions", _template.GetNeoForgeVersionsUrl(minecraftVersion), context);
    }

    public string GetNeoForgeInstallerUrl(string neoForgeVersion)
    {
        var context = new Dictionary<string, string> { { "version", neoForgeVersion } };
        return ApplyTemplate("neoforge_installer", _template.GetNeoForgeInstallerUrl(neoForgeVersion), context);
    }

    public string GetForgeVersionsUrl(string minecraftVersion)
    {
        var context = new Dictionary<string, string> { { "version", minecraftVersion } };
        return ApplyTemplate("forge_versions", _template.GetForgeVersionsUrl(minecraftVersion), context);
    }

    public string GetForgeInstallerUrl(string minecraftVersion, string forgeVersion)
    {
        var context = new Dictionary<string, string> 
        { 
            { "version", $"{minecraftVersion}-{forgeVersion}" },
            { "mcVersion", minecraftVersion },
            { "forgeVersion", forgeVersion }
        };
        return ApplyTemplate("forge_installer", _template.GetForgeInstallerUrl(minecraftVersion, forgeVersion), context);
    }

    public string GetFabricVersionsUrl(string minecraftVersion)
    {
        var context = new Dictionary<string, string> { { "version", minecraftVersion } };
        return ApplyTemplate("fabric_versions", _template.GetFabricVersionsUrl(minecraftVersion), context);
    }

    public string GetFabricProfileUrl(string minecraftVersion, string fabricVersion)
    {
        var context = new Dictionary<string, string> 
        { 
            { "version", minecraftVersion },
            { "loaderVersion", fabricVersion }
        };
        return ApplyTemplate("fabric_profile", _template.GetFabricProfileUrl(minecraftVersion, fabricVersion), context);
    }

    public string GetQuiltVersionsUrl(string minecraftVersion)
    {
        var context = new Dictionary<string, string> { { "version", minecraftVersion } };
        return ApplyTemplate("quilt_versions", _template.GetQuiltVersionsUrl(minecraftVersion), context);
    }

    public string GetQuiltProfileUrl(string minecraftVersion, string quiltVersion)
    {
        var context = new Dictionary<string, string> 
        { 
            { "version", minecraftVersion },
            { "loaderVersion", quiltVersion }
        };
        return ApplyTemplate("quilt_profile", _template.GetQuiltProfileUrl(minecraftVersion, quiltVersion), context);
    }

    public string GetLegacyFabricVersionsUrl(string minecraftVersion)
    {
        var context = new Dictionary<string, string> { { "version", minecraftVersion } };
        return ApplyTemplate("legacy_fabric_versions", _template.GetLegacyFabricVersionsUrl(minecraftVersion), context);
    }

    public string GetLegacyFabricProfileUrl(string minecraftVersion, string modLoaderVersion)
    {
        var context = new Dictionary<string, string>
        {
            { "version", minecraftVersion },
            { "loaderVersion", modLoaderVersion }
        };
        return ApplyTemplate("legacy_fabric_profile", _template.GetLegacyFabricProfileUrl(minecraftVersion, modLoaderVersion), context);
    }

    public string GetOptifineVersionsUrl(string minecraftVersion)
    {
        var context = new Dictionary<string, string> { { "mcVersion", minecraftVersion } };
        return ApplyTemplate("optifine_versions", _template.GetOptifineVersionsUrl(minecraftVersion), context);
    }

    public string GetOptifineDownloadUrl(string minecraftVersion, string optifineVersion)
    {
        // 解析 OptiFine 版本后缀（如 1.19.2-HD_U_H9 → type=HD_U, patch=H9）
        Dictionary<string, string> context;
        if (OptifineVersionParser.TryParse(optifineVersion, minecraftVersion, out var type, out var patch))
        {
            context = new Dictionary<string, string>
            {
                { "mcVersion", minecraftVersion },
                { "type", type! },
                { "patch", patch! }
            };
        }
        else
        {
            // 无法解析 type/patch（如预发布版），使用 optifineVersion 作为兜底路径段
            _logger?.LogWarning("无法解析 OptiFine 版本格式 '{Version}'，type/patch 占位符将无法替换", optifineVersion);
            context = new Dictionary<string, string>
            {
                { "mcVersion", minecraftVersion },
                { "type", optifineVersion },
                { "patch", string.Empty }
            };
        }

        return ApplyTemplate("optifine_download", _template.GetOptifineDownloadUrl(minecraftVersion, optifineVersion), context);
    }

    public string GetLiteLoaderVersionsUrl()
    {
        return ApplyTemplate("liteloader_versions", _template.GetLiteLoaderVersionsUrl());
    }

    public string GetLiteLoaderJarUrl(string relativePath, string? originalBaseUrl)
    {
        var context = new Dictionary<string, string> { { "path", relativePath } };
        return ApplyTemplate("liteloader_jar", _template.GetLiteLoaderJarUrl(relativePath, originalBaseUrl), context);
    }

    #endregion

    #region Modrinth API

    public string GetModrinthApiBaseUrl()
    {
        return ApplyTemplate("modrinth_api_base", _template.GetModrinthApiBaseUrl());
    }

    public string GetModrinthCdnBaseUrl()
    {
        return ApplyTemplate("modrinth_cdn_base", _template.GetModrinthCdnBaseUrl());
    }

    public string TransformModrinthApiUrl(string originalUrl)
    {
        return ApplyTemplate("modrinth_api_transform", _template.TransformModrinthApiUrl(originalUrl));
    }

    public string TransformModrinthCdnUrl(string originalUrl)
    {
        return ApplyTemplate("modrinth_cdn_transform", _template.TransformModrinthCdnUrl(originalUrl));
    }

    public string? GetModrinthUserAgent()
    {
        return _template.GetModrinthUserAgent();
    }

    public bool RequiresModrinthUserAgent => _template.RequiresModrinthUserAgent;

    #endregion

    #region CurseForge API

    public string GetCurseForgeApiBaseUrl()
    {
        return ApplyTemplate("curseforge_api_base", _template.GetCurseForgeApiBaseUrl());
    }

    public string GetCurseForgeCdnBaseUrl()
    {
        return ApplyTemplate("curseforge_cdn_base", _template.GetCurseForgeCdnBaseUrl());
    }

    public string TransformCurseForgeApiUrl(string originalUrl)
    {
        return ApplyTemplate("curseforge_api_transform", _template.TransformCurseForgeApiUrl(originalUrl));
    }

    public string TransformCurseForgeCdnUrl(string originalUrl)
    {
        return ApplyTemplate("curseforge_cdn_transform", _template.TransformCurseForgeCdnUrl(originalUrl));
    }

    public string? GetCurseForgeUserAgent()
    {
        return _template.GetCurseForgeUserAgent();
    }

    public bool RequiresCurseForgeUserAgent => _template.RequiresCurseForgeUserAgent;

    public bool ShouldIncludeCurseForgeApiKey => _template.ShouldIncludeCurseForgeApiKey;

    #endregion

    #region 私有方法

    /// <summary>
    /// 应用模板并进行变量替换
    /// </summary>
    /// <param name="resourceType">资源类型标识</param>
    /// <param name="templateUrl">模板 URL</param>
    /// <param name="context">上下文变量（可选）</param>
    /// <returns>最终 URL</returns>
    private string ApplyTemplate(string resourceType, string templateUrl, Dictionary<string, string>? context = null)
    {
        // 1. 检查是否有覆盖规则
        if (_overrides.TryGetValue(resourceType, out var overrideUrl))
        {
            _logger?.LogDebug("使用覆盖规则: {ResourceType} -> {OverrideUrl}", resourceType, overrideUrl);
            return ReplaceVariables(overrideUrl, context);
        }

        // 2. 使用模板 URL
        return ReplaceVariables(templateUrl, context);
    }

    /// <summary>
    /// 替换 URL 中的变量
    /// </summary>
    /// <param name="url">包含变量的 URL</param>
    /// <param name="context">上下文变量（可选）</param>
    /// <returns>替换后的 URL</returns>
    private string ReplaceVariables(string url, Dictionary<string, string>? context = null)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        var result = url;

        // 替换 {baseUrl}
        result = result.Replace("{baseUrl}", _baseUrl);

        // 替换上下文变量
        if (context != null)
        {
            foreach (var kvp in context)
            {
                var placeholder = $"{{{kvp.Key}}}";
                if (result.Contains(placeholder))
                {
                    result = result.Replace(placeholder, kvp.Value);
                }
            }
        }

        // 检查是否还有未替换的变量
        if (result.Contains("{") && result.Contains("}"))
        {
            _logger?.LogWarning("URL 中存在未替换的变量: {Url}", result);
        }

        return result;
    }

    #endregion
}
