using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services.DownloadSource;

/// <summary>
/// BMCLAPI 模板实现（官方资源镜像）
/// </summary>
public class BmclapiTemplate : DownloadSourceTemplate
{
    public override string TemplateName => "Official";
    public override DownloadSourceTemplateType TemplateType => DownloadSourceTemplateType.Official;

    // Minecraft 官方资源
    public override string GetVersionManifestUrl()
    {
        return "{baseUrl}/mc/game/version_manifest.json";
    }

    public override string GetVersionInfoUrl(string versionId, string originalUrl)
    {
        // BMCLAPI 支持与官方源相同的路径结构，替换域名即可
        return originalUrl
            .Replace("https://piston-meta.mojang.com", "{baseUrl}")
            .Replace("https://piston-data.mojang.com", "{baseUrl}")
            .Replace("https://launchermeta.mojang.com", "{baseUrl}");
    }

    public override string GetResourceUrl(string resourceType, string originalUrl)
    {
        // 资源文件 URL 转换
        return originalUrl
            .Replace("https://piston-meta.mojang.com", "{baseUrl}")
            .Replace("https://piston-data.mojang.com", "{baseUrl}")
            .Replace("https://launchermeta.mojang.com", "{baseUrl}")
            .Replace("https://resources.download.minecraft.net", "{baseUrl}/assets");
    }

    public override string GetClientJarUrl(string versionId, string originalUrl)
    {
        return "{baseUrl}/version/{version}/client";
    }

    public override string GetClientJsonUrl(string versionId, string originalUrl)
    {
        return "{baseUrl}/version/{version}/json";
    }

    public override string GetLibraryUrl(string libraryName, string originalUrl)
    {
        if (!string.IsNullOrEmpty(originalUrl))
        {
            // 转换 Maven URL
            return originalUrl
                .Replace("https://repo1.maven.org/maven2", "{baseUrl}/maven")
                .Replace("https://maven.neoforged.net/releases", "{baseUrl}/maven")
                .Replace("https://maven.neoforged.net", "{baseUrl}/maven")
                .Replace("https://maven.minecraftforge.net", "{baseUrl}/maven")
                .Replace("https://libraries.minecraft.net", "{baseUrl}/maven")
                .Replace("https://maven.quiltmc.org/repository/release", "{baseUrl}/maven")
                .Replace("https://maven.fabricmc.net", "{baseUrl}/maven");
        }
        
        // 如果没有原始 URL，返回空字符串（由 CustomDownloadSource 处理）
        return string.Empty;
    }

    // ModLoader 资源
    public override string GetNeoForgeVersionsUrl(string minecraftVersion)
    {
        return "{baseUrl}/neoforge/list/{version}";
    }

    public override string GetNeoForgeInstallerUrl(string neoForgeVersion)
    {
        return "{baseUrl}/maven/net/neoforged/neoforge/{version}/neoforge-{version}-installer.jar";
    }

    public override string GetForgeVersionsUrl(string minecraftVersion)
    {
        return "{baseUrl}/forge/minecraft/{version}";
    }

    public override string GetForgeInstallerUrl(string minecraftVersion, string forgeVersion)
    {
        return "{baseUrl}/maven/net/minecraftforge/forge/{version}/forge-{version}-installer.jar";
    }

    public override string GetFabricVersionsUrl(string minecraftVersion)
    {
        return "{baseUrl}/fabric-meta/v2/versions/loader/{version}";
    }

    public override string GetFabricProfileUrl(string minecraftVersion, string fabricVersion)
    {
        return "{baseUrl}/fabric-meta/v2/versions/loader/{version}/{loaderVersion}/profile/json";
    }

    public override string GetQuiltVersionsUrl(string minecraftVersion)
    {
        return "{baseUrl}/quilt-meta/v3/versions/loader/{version}";
    }

    public override string GetQuiltProfileUrl(string minecraftVersion, string quiltVersion)
    {
        return "{baseUrl}/quilt-meta/v3/versions/loader/{version}/{loaderVersion}/profile/json";
    }

    public override string GetLegacyFabricVersionsUrl(string minecraftVersion)
    {
        // BMCLAPI 不支持 Legacy Fabric，返回官方源
        return "https://meta.legacyfabric.net/v2/versions/loader/{version}";
    }

    public override string GetLegacyFabricProfileUrl(string minecraftVersion, string modLoaderVersion)
    {
        // BMCLAPI 不支持 Legacy Fabric，返回官方源
        return "https://meta.legacyfabric.net/v2/versions/loader/{version}/{loaderVersion}/profile/json";
    }

    public override string GetLiteLoaderVersionsUrl()
    {
        return "{baseUrl}/maven/com/mumfrey/liteloader/versions.json";
    }

    public override string GetLiteLoaderJarUrl(string relativePath, string? originalBaseUrl)
    {
        return "{baseUrl}/maven/{path}";
    }

    // Modrinth API（BMCLAPI 不支持，返回官方源）
    public override string GetModrinthApiBaseUrl() => "https://api.modrinth.com";
    public override string GetModrinthCdnBaseUrl() => "https://cdn.modrinth.com";
    public override string TransformModrinthApiUrl(string originalUrl) => originalUrl;
    public override string TransformModrinthCdnUrl(string originalUrl) => originalUrl;
    public override string? GetModrinthUserAgent() => null;
    public override bool RequiresModrinthUserAgent => false;

    // CurseForge API（BMCLAPI 不支持，返回官方源）
    public override string GetCurseForgeApiBaseUrl() => "https://api.curseforge.com";
    public override string GetCurseForgeCdnBaseUrl() => "https://edge.forgecdn.net";
    public override string TransformCurseForgeApiUrl(string originalUrl) => originalUrl;
    public override string TransformCurseForgeCdnUrl(string originalUrl) => originalUrl;
    public override string? GetCurseForgeUserAgent() => null;
    public override bool RequiresCurseForgeUserAgent => false;
    public override bool ShouldIncludeCurseForgeApiKey => true;
}
