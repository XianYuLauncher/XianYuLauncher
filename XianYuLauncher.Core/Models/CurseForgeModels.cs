using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// CurseForge搜索结果
/// </summary>
public class CurseForgeSearchResult
{
    [JsonPropertyName("data")]
    public List<CurseForgeMod> Data { get; set; } = new();
    
    [JsonPropertyName("pagination")]
    public CurseForgePagination Pagination { get; set; }
}

/// <summary>
/// CurseForge分页信息
/// </summary>
public class CurseForgePagination
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
    
    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }
    
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}

/// <summary>
/// CurseForge Mod信息
/// </summary>
public class CurseForgeMod
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("gameId")]
    public int GameId { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("slug")]
    public string Slug { get; set; }
    
    [JsonPropertyName("links")]
    public CurseForgeLinks Links { get; set; }
    
    [JsonPropertyName("summary")]
    public string Summary { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("status")]
    public int Status { get; set; }
    
    [JsonPropertyName("downloadCount")]
    public long DownloadCount { get; set; }
    
    [JsonPropertyName("isFeatured")]
    public bool IsFeatured { get; set; }
    
    [JsonPropertyName("primaryCategoryId")]
    public int PrimaryCategoryId { get; set; }
    
    [JsonPropertyName("categories")]
    public List<CurseForgeCategory> Categories { get; set; } = new();
    
    [JsonPropertyName("authors")]
    public List<CurseForgeAuthor> Authors { get; set; } = new();
    
    [JsonPropertyName("logo")]
    public CurseForgeLogo Logo { get; set; }
    
    [JsonPropertyName("screenshots")]
    public List<CurseForgeScreenshot> Screenshots { get; set; } = new();
    
    [JsonPropertyName("mainFileId")]
    public int MainFileId { get; set; }
    
    [JsonPropertyName("latestFiles")]
    public List<CurseForgeFile> LatestFiles { get; set; } = new();
    
    [JsonPropertyName("latestFilesIndexes")]
    public List<CurseForgeFileIndex> LatestFilesIndexes { get; set; } = new();
    
    [JsonPropertyName("dateCreated")]
    public DateTime DateCreated { get; set; }
    
    [JsonPropertyName("dateModified")]
    public DateTime DateModified { get; set; }
    
    [JsonPropertyName("dateReleased")]
    public DateTime DateReleased { get; set; }
    
    [JsonPropertyName("allowModDistribution")]
    public bool? AllowModDistribution { get; set; }
    
    [JsonPropertyName("gamePopularityRank")]
    public int GamePopularityRank { get; set; }
    
    [JsonPropertyName("classId")]
    public int? ClassId { get; set; }
}

/// <summary>
/// CurseForge链接信息
/// </summary>
public class CurseForgeLinks
{
    [JsonPropertyName("websiteUrl")]
    public string WebsiteUrl { get; set; }
    
    [JsonPropertyName("wikiUrl")]
    public string WikiUrl { get; set; }
    
    [JsonPropertyName("issuesUrl")]
    public string IssuesUrl { get; set; }
    
    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; set; }
}

/// <summary>
/// CurseForge分类信息
/// </summary>
public class CurseForgeCategory
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("gameId")]
    public int GameId { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("slug")]
    public string Slug { get; set; }
    
    [JsonPropertyName("url")]
    public string Url { get; set; }
    
    [JsonPropertyName("iconUrl")]
    public string IconUrl { get; set; }
    
    [JsonPropertyName("dateModified")]
    public DateTime DateModified { get; set; }
    
    [JsonPropertyName("isClass")]
    public bool? IsClass { get; set; }
    
    [JsonPropertyName("classId")]
    public int? ClassId { get; set; }
    
    [JsonPropertyName("parentCategoryId")]
    public int? ParentCategoryId { get; set; }
}

/// <summary>
/// CurseForge作者信息
/// </summary>
public class CurseForgeAuthor
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("url")]
    public string Url { get; set; }
}

/// <summary>
/// CurseForge Logo信息
/// </summary>
public class CurseForgeLogo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("modId")]
    public int ModId { get; set; }
    
    [JsonPropertyName("title")]
    public string Title { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("thumbnailUrl")]
    public string ThumbnailUrl { get; set; }
    
    [JsonPropertyName("url")]
    public string Url { get; set; }
}

/// <summary>
/// CurseForge截图信息
/// </summary>
public class CurseForgeScreenshot
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("modId")]
    public int ModId { get; set; }
    
    [JsonPropertyName("title")]
    public string Title { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("thumbnailUrl")]
    public string ThumbnailUrl { get; set; }
    
    [JsonPropertyName("url")]
    public string Url { get; set; }
}

/// <summary>
/// CurseForge文件信息
/// </summary>
public class CurseForgeFile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("gameId")]
    public int GameId { get; set; }
    
    [JsonPropertyName("modId")]
    public int ModId { get; set; }
    
    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; }
    
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }
    
    [JsonPropertyName("fileName")]
    public string FileName { get; set; }
    
    [JsonPropertyName("releaseType")]
    public int ReleaseType { get; set; } // 1=Release, 2=Beta, 3=Alpha
    
    [JsonPropertyName("fileStatus")]
    public int FileStatus { get; set; }
    
    [JsonPropertyName("hashes")]
    public List<CurseForgeFileHash> Hashes { get; set; } = new();
    
    [JsonPropertyName("fileDate")]
    public DateTime FileDate { get; set; }
    
    [JsonPropertyName("fileLength")]
    public long FileLength { get; set; }
    
    [JsonPropertyName("downloadCount")]
    public long DownloadCount { get; set; }
    
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; }
    
    [JsonPropertyName("gameVersions")]
    public List<string> GameVersions { get; set; } = new();
    
    [JsonPropertyName("sortableGameVersions")]
    public List<CurseForgeSortableGameVersion> SortableGameVersions { get; set; } = new();
    
    [JsonPropertyName("dependencies")]
    public List<CurseForgeDependency> Dependencies { get; set; } = new();
    
    [JsonPropertyName("alternateFileId")]
    public int? AlternateFileId { get; set; }
    
    [JsonPropertyName("isServerPack")]
    public bool IsServerPack { get; set; }
    
    [JsonPropertyName("fileFingerprint")]
    public long FileFingerprint { get; set; }
    
    [JsonPropertyName("modules")]
    public List<CurseForgeModule> Modules { get; set; } = new();
}

/// <summary>
/// CurseForge文件哈希
/// </summary>
public class CurseForgeFileHash
{
    [JsonPropertyName("value")]
    public string Value { get; set; }
    
    [JsonPropertyName("algo")]
    public int Algo { get; set; } // 1=Sha1, 2=Md5
}

/// <summary>
/// CurseForge可排序游戏版本
/// </summary>
public class CurseForgeSortableGameVersion
{
    [JsonPropertyName("gameVersionName")]
    public string GameVersionName { get; set; }
    
    [JsonPropertyName("gameVersionPadded")]
    public string GameVersionPadded { get; set; }
    
    [JsonPropertyName("gameVersion")]
    public string GameVersion { get; set; }
    
    [JsonPropertyName("gameVersionReleaseDate")]
    public DateTime GameVersionReleaseDate { get; set; }
    
    [JsonPropertyName("gameVersionTypeId")]
    public int? GameVersionTypeId { get; set; }
}

/// <summary>
/// CurseForge依赖信息
/// </summary>
public class CurseForgeDependency
{
    [JsonPropertyName("modId")]
    public int ModId { get; set; }
    
    [JsonPropertyName("relationType")]
    public int RelationType { get; set; } // 1=EmbeddedLibrary, 2=OptionalDependency, 3=RequiredDependency, 4=Tool, 5=Incompatible, 6=Include
}

/// <summary>
/// CurseForge模块信息
/// </summary>
public class CurseForgeModule
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("fingerprint")]
    public long Fingerprint { get; set; }
}

/// <summary>
/// CurseForge文件索引
/// </summary>
public class CurseForgeFileIndex
{
    [JsonPropertyName("gameVersion")]
    public string GameVersion { get; set; }
    
    [JsonPropertyName("fileId")]
    public int FileId { get; set; }
    
    [JsonPropertyName("filename")]
    public string Filename { get; set; }
    
    [JsonPropertyName("releaseType")]
    public int ReleaseType { get; set; }
    
    [JsonPropertyName("gameVersionTypeId")]
    public int? GameVersionTypeId { get; set; }
    
    [JsonPropertyName("modLoader")]
    public int? ModLoader { get; set; }
}

/// <summary>
/// CurseForge Mod详情响应
/// </summary>
public class CurseForgeModDetailResponse
{
    [JsonPropertyName("data")]
    public CurseForgeModDetail Data { get; set; }
}

/// <summary>
/// CurseForge Mod详情
/// </summary>
public class CurseForgeModDetail : CurseForgeMod
{
    // 继承自CurseForgeMod，可以添加额外的详情字段
}

/// <summary>
/// CurseForge文件列表响应
/// </summary>
public class CurseForgeFilesResponse
{
    [JsonPropertyName("data")]
    public List<CurseForgeFile> Data { get; set; } = new();
    
    [JsonPropertyName("pagination")]
    public CurseForgePagination Pagination { get; set; }
}

/// <summary>
/// CurseForge批量获取Mod响应
/// </summary>
public class CurseForgeModsResponse
{
    [JsonPropertyName("data")]
    public List<CurseForgeMod> Data { get; set; } = new();
}

/// <summary>
/// CurseForge类别列表响应
/// </summary>
public class CurseForgeCategoriesResponse
{
    [JsonPropertyName("data")]
    public List<CurseForgeCategory> Data { get; set; } = new();
}

/// <summary>
/// CurseForge批量获取文件响应
/// </summary>
public class CurseForgeFilesListResponse
{
    [JsonPropertyName("data")]
    public List<CurseForgeFile> Data { get; set; } = new();
}

/// <summary>
/// CurseForge单个文件响应
/// </summary>
public class CurseForgeFileResponse
{
    [JsonPropertyName("data")]
    public CurseForgeFile Data { get; set; }
}

#region CurseForge 整合包 Manifest 模型

/// <summary>
/// CurseForge整合包manifest.json根对象
/// </summary>
public class CurseForgeManifest
{
    [JsonPropertyName("minecraft")]
    public CurseForgeManifestMinecraft Minecraft { get; set; }
    
    [JsonPropertyName("manifestType")]
    public string ManifestType { get; set; }
    
    [JsonPropertyName("manifestVersion")]
    public int ManifestVersion { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("version")]
    public string Version { get; set; }
    
    [JsonPropertyName("author")]
    public string Author { get; set; }
    
    [JsonPropertyName("files")]
    public List<CurseForgeManifestFile> Files { get; set; } = new();
    
    [JsonPropertyName("overrides")]
    public string Overrides { get; set; }
}

/// <summary>
/// CurseForge整合包Minecraft信息
/// </summary>
public class CurseForgeManifestMinecraft
{
    [JsonPropertyName("version")]
    public string Version { get; set; }
    
    [JsonPropertyName("modLoaders")]
    public List<CurseForgeManifestModLoader> ModLoaders { get; set; } = new();
}

/// <summary>
/// CurseForge整合包ModLoader信息
/// </summary>
public class CurseForgeManifestModLoader
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("primary")]
    public bool Primary { get; set; }
}

/// <summary>
/// CurseForge整合包文件引用
/// </summary>
public class CurseForgeManifestFile
{
    [JsonPropertyName("projectID")]
    public int ProjectId { get; set; }
    
    [JsonPropertyName("fileID")]
    public int FileId { get; set; }
    
    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

#endregion


#region CurseForge Fingerprint 模型

/// <summary>
/// CurseForge Fingerprint 匹配响应
/// </summary>
public class CurseForgeFingerprintMatchesResponse
{
    [JsonPropertyName("data")]
    public CurseForgeFingerprintMatchesResult Data { get; set; }
}

/// <summary>
/// CurseForge Fingerprint 匹配结果
/// </summary>
public class CurseForgeFingerprintMatchesResult
{
    [JsonPropertyName("isCacheBuilt")]
    public bool IsCacheBuilt { get; set; }
    
    [JsonPropertyName("exactMatches")]
    public List<CurseForgeFingerprintMatch> ExactMatches { get; set; } = new();
    
    [JsonPropertyName("exactFingerprints")]
    public List<uint> ExactFingerprints { get; set; } = new();
    
    [JsonPropertyName("partialMatches")]
    public List<CurseForgeFingerprintMatch> PartialMatches { get; set; } = new();
    
    [JsonPropertyName("partialMatchFingerprints")]
    public Dictionary<string, List<uint>> PartialMatchFingerprints { get; set; } = new();
    
    [JsonPropertyName("installedFingerprints")]
    public List<uint> InstalledFingerprints { get; set; } = new();
    
    [JsonPropertyName("unmatchedFingerprints")]
    public List<uint> UnmatchedFingerprints { get; set; } = new();
}

/// <summary>
/// CurseForge Fingerprint 匹配项
/// </summary>
public class CurseForgeFingerprintMatch
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("file")]
    public CurseForgeFile File { get; set; }
    
    [JsonPropertyName("latestFiles")]
    public List<CurseForgeFile> LatestFiles { get; set; } = new();
}

#endregion
