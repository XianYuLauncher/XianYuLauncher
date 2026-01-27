using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 将字符串转换为Uri的JSON转换器
/// </summary>
public class StringToUriConverter : JsonConverter<Uri>
{
    public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            string urlString = reader.GetString();
            if (string.IsNullOrEmpty(urlString))
            {
                return null;
            }
            
            if (Uri.TryCreate(urlString, UriKind.Absolute, out Uri uri))
            {
                return uri;
            }
            
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value?.ToString());
    }
}

/// <summary>
/// Modrinth搜索结果
/// </summary>
public class ModrinthSearchResult
{
    /// <summary>
    /// 搜索结果列表
    /// </summary>
    [JsonPropertyName("hits")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ModrinthProject> Hits { get; set; } = new List<ModrinthProject>();

    /// <summary>
    /// 偏移量
    /// </summary>
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    /// <summary>
    /// 返回结果数量
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>
    /// 总结果数
    /// </summary>
    [JsonPropertyName("total_hits")]
    public int TotalHits { get; set; }
}

/// <summary>
    /// Modrinth项目信息
    /// </summary>
    public class ModrinthProject
    {
        /// <summary>
        /// 构造函数，初始化所有集合属性
        /// </summary>
        public ModrinthProject()
        {
            Categories = new List<string>();
            DisplayCategories = new List<string>();
            Versions = new List<string>();
            Gallery = new List<Uri>();
        }

        /// <summary>
        /// 项目ID
        /// </summary>
        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        /// <summary>
        /// 项目类型
        /// </summary>
        [JsonPropertyName("project_type")]
        public string ProjectType { get; set; }

        /// <summary>
        /// 项目标识符
        /// </summary>
        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        /// <summary>
        /// 作者
        /// </summary>
        [JsonPropertyName("author")]
        public string Author { get; set; }

        /// <summary>
        /// 标题
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        /// 显示的标题（自动应用名称翻译）
        /// </summary>
        [JsonIgnore]
        public string DisplayTitle
        {
            get
            {
                if (XianYuLauncher.Core.Services.TranslationService.Instance != null)
                {
                    return XianYuLauncher.Core.Services.TranslationService.Instance.GetTranslatedName(Slug, Title);
                }
                return Title;
            }
        }

        /// <summary>
        /// 描述
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }
        
        /// <summary>
        /// 翻译后的描述（来自MCIM翻译API）
        /// </summary>
        [JsonIgnore]
        public string TranslatedDescription { get; set; }
        
        /// <summary>
        /// 显示的描述（优先使用翻译，如果没有则使用原始描述）
        /// 只有当前语言为中文时才返回翻译
        /// </summary>
        [JsonIgnore]
        public string DisplayDescription
        {
            get
            {
                // 使用 TranslationService 的静态语言检查，避免跨程序集文化信息不同步
                bool isChinese = XianYuLauncher.Core.Services.TranslationService.GetCurrentLanguage().StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                
                // 只有中文时才返回翻译，否则返回原始描述
                if (isChinese && !string.IsNullOrEmpty(TranslatedDescription))
                {
                    return TranslatedDescription;
                }
                
                return Description;
            }
        }

        /// <summary>
        /// 分类列表
        /// </summary>
        [JsonPropertyName("categories")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Categories { get; set; }

        /// <summary>
        /// 显示分类列表
        /// </summary>
        [JsonPropertyName("display_categories")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> DisplayCategories { get; set; }

        /// <summary>
        /// 支持的Minecraft版本列表
        /// </summary>
        [JsonPropertyName("versions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Versions { get; set; }

        /// <summary>
        /// 下载次数
        /// </summary>
        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }

        /// <summary>
        /// 关注数
        /// </summary>
        [JsonPropertyName("follows")]
        public int Follows { get; set; }

        /// <summary>
        /// 图标URL
        /// </summary>
        [JsonPropertyName("icon_url")]
        [JsonConverter(typeof(StringToUriConverter))]
        public Uri IconUrl { get; set; }

        /// <summary>
        /// 创建日期
        /// </summary>
        [JsonPropertyName("date_created")]
        public string DateCreated { get; set; }

        /// <summary>
        /// 修改日期
        /// </summary>
        [JsonPropertyName("date_modified")]
        public string DateModified { get; set; }

        /// <summary>
        /// 最新版本ID（用于API调用，不适合直接显示）
        /// </summary>
        [JsonPropertyName("latest_version")]
        public string LatestVersionId { get; set; }
        
        /// <summary>
        /// 最新支持的游戏版本（从versions列表中获取最后一项）
        /// </summary>
        public string LatestSupportedVersion
        {
            get
            {
                if (Versions == null || Versions.Count == 0)
                {
                    return string.Empty;
                }
                // 返回versions列表中的最后一项作为最新支持的游戏版本
                return Versions[Versions.Count - 1];
            }
        }

        /// <summary>
        /// 许可证
        /// </summary>
        [JsonPropertyName("license")]
        public string License { get; set; }

        /// <summary>
        /// 客户端支持情况
        /// </summary>
        [JsonPropertyName("client_side")]
        public string ClientSide { get; set; }

        /// <summary>
        /// 服务端支持情况
        /// </summary>
        [JsonPropertyName("server_side")]
        public string ServerSide { get; set; }

        /// <summary>
        /// 图片库
        /// </summary>
        [JsonPropertyName("gallery")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Uri> Gallery { get; set; }

        /// <summary>
        /// 特色图片
        /// </summary>
        [JsonPropertyName("featured_gallery")]
        [JsonConverter(typeof(StringToUriConverter))]
        public Uri FeaturedGallery { get; set; }

        /// <summary>
        /// 支持的模组加载器
        /// </summary>
        public string SupportedLoaders
        {
            get
            {
                if (Categories == null || Categories.Count == 0)
                {
                    return string.Empty;
                }

                // 定义需要识别的加载器列表
                var loaderKeywords = new List<string> { "fabric", "forge", "quilt", "neoforge" };
                var supportedLoaders = new List<string>();

                // 遍历分类，提取加载器信息
                foreach (var category in Categories)
                {
                    if (loaderKeywords.Contains(category.ToLower()))
                    {
                        // 首字母大写处理
                        supportedLoaders.Add(category.Substring(0, 1).ToUpper() + category.Substring(1).ToLower());
                    }
                }

                // 用逗号分隔加载器
                return string.Join(", ", supportedLoaders);
            }
        }
    }

/// <summary>
    /// Modrinth项目详情信息
    /// </summary>
    public class ModrinthProjectDetail
    {
        /// <summary>
        /// 构造函数，初始化所有集合属性
        /// </summary>
        public ModrinthProjectDetail()
        {
            Categories = new List<string>();
            AdditionalCategories = new List<string>();
            DonationUrls = new List<DonationUrl>();
            Versions = new List<string>();
            GameVersions = new List<string>();
            Loaders = new List<string>();
            Gallery = new List<GalleryImage>();
        }

        /// <summary>
        /// 项目标识符
        /// </summary>
        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        /// <summary>
        /// 项目名称
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        /// 项目简介
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// 分类列表
        /// </summary>
        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; }

        /// <summary>
        /// 客户端支持情况
        /// </summary>
        [JsonPropertyName("client_side")]
        public string ClientSide { get; set; }

        /// <summary>
        /// 服务端支持情况
        /// </summary>
        [JsonPropertyName("server_side")]
        public string ServerSide { get; set; }

        /// <summary>
        /// 项目详细描述
        /// </summary>
        [JsonPropertyName("body")]
        public string Body { get; set; }

        /// <summary>
        /// 项目状态
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; }

        /// <summary>
        /// 额外分类列表
        /// </summary>
        [JsonPropertyName("additional_categories")]
        public List<string> AdditionalCategories { get; set; }

        /// <summary>
        /// 问题提交URL
        /// </summary>
        [JsonPropertyName("issues_url")]
        [JsonConverter(typeof(StringToUriConverter))]
        public Uri IssuesUrl { get; set; }

        /// <summary>
        /// 源代码URL
        /// </summary>
        [JsonPropertyName("source_url")]
        [JsonConverter(typeof(StringToUriConverter))]
        public Uri SourceUrl { get; set; }

        /// <summary>
        /// Wiki URL
        /// </summary>
        [JsonPropertyName("wiki_url")]
        [JsonConverter(typeof(StringToUriConverter))]
        public Uri WikiUrl { get; set; }

        /// <summary>
        /// Discord邀请URL
        /// </summary>
        [JsonPropertyName("discord_url")]
        [JsonConverter(typeof(StringToUriConverter))]
        public Uri DiscordUrl { get; set; }

        /// <summary>
        /// 捐赠链接列表
        /// </summary>
        [JsonPropertyName("donation_urls")]
        public List<DonationUrl> DonationUrls { get; set; }

        /// <summary>
        /// 项目类型
        /// </summary>
        [JsonPropertyName("project_type")]
        public string ProjectType { get; set; }

        /// <summary>
        /// 下载次数
        /// </summary>
        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }

        /// <summary>
        /// 图标URL
        /// </summary>
        [JsonPropertyName("icon_url")]
        [JsonConverter(typeof(StringToUriConverter))]
        public Uri IconUrl { get; set; }

        /// <summary>
        /// 项目颜色
        /// </summary>
        [JsonPropertyName("color")]
        public int? Color { get; set; }

        /// <summary>
        /// 项目ID
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// 作者
        /// </summary>
        [JsonPropertyName("author")]
        public string Author { get; set; }

        /// <summary>
        /// 团队ID
        /// </summary>
        [JsonPropertyName("team")]
        public string Team { get; set; }

        /// <summary>
        /// 发布日期
        /// </summary>
        [JsonPropertyName("published")]
        public string Published { get; set; }

        /// <summary>
        /// 更新日期
        /// </summary>
        [JsonPropertyName("updated")]
        public string Updated { get; set; }

        /// <summary>
        /// 审批日期
        /// </summary>
        [JsonPropertyName("approved")]
        public string Approved { get; set; }

        /// <summary>
        /// 关注数
        /// </summary>
        [JsonPropertyName("followers")]
        public int Followers { get; set; }

        /// <summary>
        /// 许可证
        /// </summary>
        [JsonPropertyName("license")]
        public License License { get; set; }

        /// <summary>
        /// 版本ID列表
        /// </summary>
        [JsonPropertyName("versions")]
        public List<string> Versions { get; set; }

        /// <summary>
        /// 支持的游戏版本
        /// </summary>
        [JsonPropertyName("game_versions")]
        public List<string> GameVersions { get; set; }

        /// <summary>
        /// 支持的加载器
        /// </summary>
        [JsonPropertyName("loaders")]
        public List<string> Loaders { get; set; }

        /// <summary>
        /// 图片库
        /// </summary>
        [JsonPropertyName("gallery")]
        public List<GalleryImage> Gallery { get; set; }
    }

/// <summary>
/// 捐赠链接
/// </summary>
public class DonationUrl
{
    /// <summary>
    /// 平台
    /// </summary>
    [JsonPropertyName("platform")]
    public string Platform { get; set; }

    /// <summary>
    /// 捐赠URL
    /// </summary>
    [JsonPropertyName("url")]
    [JsonConverter(typeof(StringToUriConverter))]
    public Uri Url { get; set; }
}

/// <summary>
/// 许可证
/// </summary>
public class License
{
    /// <summary>
    /// 许可证ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// 许可证名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// 许可证URL
    /// </summary>
    [JsonPropertyName("url")]
    [JsonConverter(typeof(StringToUriConverter))]
    public Uri Url { get; set; }
}

/// <summary>
/// 图片库图片
/// </summary>
public class GalleryImage
{
    /// <summary>
    /// 图片URL
    /// </summary>
    [JsonPropertyName("url")]
    [JsonConverter(typeof(StringToUriConverter))]
    public Uri Url { get; set; }

    /// <summary>
    /// 图片标题
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }

    /// <summary>
    /// 图片描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// 是否为主图
    /// </summary>
    [JsonPropertyName("featured")]
    public bool Featured { get; set; }

    /// <summary>
    /// 创建日期
    /// </summary>
    [JsonPropertyName("created")]
    public string Created { get; set; }

    /// <summary>
    /// 排序索引
    /// </summary>
    [JsonPropertyName("ordering")]
    public int Ordering { get; set; }
}

/// <summary>
/// Modrinth版本信息
/// </summary>
/// <summary>
/// 依赖项信息
/// </summary>
public class Dependency
{
    /// <summary>
    /// 版本ID
    /// </summary>
    [JsonPropertyName("version_id")]
    public string VersionId { get; set; }
    
    /// <summary>
    /// 项目ID
    /// </summary>
    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; }
    
    /// <summary>
    /// 文件名
    /// </summary>
    [JsonPropertyName("file_name")]
    public string FileName { get; set; }
    
    /// <summary>
    /// 依赖类型
    /// </summary>
    [JsonPropertyName("dependency_type")]
    public string DependencyType { get; set; }
}

/// <summary>
/// Modrinth版本信息
/// </summary>
public class ModrinthVersion
{
    /// <summary>
    /// 构造函数，初始化所有集合属性
    /// </summary>
    public ModrinthVersion()
    {
        GameVersions = new List<string>();
        Loaders = new List<string>();
        Files = new List<ModrinthVersionFile>();
        Dependencies = new List<Dependency>();
    }

    /// <summary>
    /// 版本ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    [JsonPropertyName("version_number")]
    public string VersionNumber { get; set; }

    /// <summary>
    /// 版本标题
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// 版本描述
    /// </summary>
    [JsonPropertyName("changelog")]
    public string Changelog { get; set; }

    /// <summary>
    /// 版本类型
    /// </summary>
    [JsonPropertyName("version_type")]
    public string VersionType { get; set; }

    /// <summary>
    /// 项目ID
    /// </summary>
    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; }

    /// <summary>
    /// 支持的游戏版本
    /// </summary>
    [JsonPropertyName("game_versions")]
    public List<string> GameVersions { get; set; }

    /// <summary>
    /// 支持的加载器
    /// </summary>
    [JsonPropertyName("loaders")]
    public List<string> Loaders { get; set; }

    /// <summary>
    /// 发布日期
    /// </summary>
    [JsonPropertyName("date_published")]
    public string DatePublished { get; set; }

    /// <summary>
    /// 下载次数
    /// </summary>
    [JsonPropertyName("downloads")]
    public int Downloads { get; set; }

    /// <summary>
    /// 是否为预发布版本
    /// </summary>
    [JsonPropertyName("is_prerelease")]
    public bool IsPrerelease { get; set; }

    /// <summary>
    /// 文件列表
    /// </summary>
    [JsonPropertyName("files")]
    public List<ModrinthVersionFile> Files { get; set; }
    
    /// <summary>
    /// 依赖项列表
    /// </summary>
    [JsonPropertyName("dependencies")]
    public List<Dependency> Dependencies { get; set; }
}

/// <summary>
/// Modrinth版本文件信息
/// </summary>
public class ModrinthVersionFile
{
    /// <summary>
    /// 文件ID
    /// </summary>
    [JsonPropertyName("hashes")]
    public Dictionary<string, string> Hashes { get; set; }

    /// <summary>
    /// 文件URL
    /// </summary>
    [JsonPropertyName("url")]
    [JsonConverter(typeof(StringToUriConverter))]
    public Uri Url { get; set; }

    /// <summary>
    /// 文件名
    /// </summary>
    [JsonPropertyName("filename")]
    public string Filename { get; set; }

    /// <summary>
    /// 文件大小
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// 是否为主文件
    /// </summary>
    [JsonPropertyName("primary")]
    public bool Primary { get; set; }

    /// <summary>
    /// 文件类型
    /// </summary>
    [JsonPropertyName("file_type")]
    public string FileType { get; set; }
}
