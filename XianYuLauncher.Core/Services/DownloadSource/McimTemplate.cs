using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services.DownloadSource;

/// <summary>
/// MCIM 模板实现（社区资源镜像：Modrinth、CurseForge）
/// </summary>
public class McimTemplate : DownloadSourceTemplate
{
    public override string TemplateName => "MCIM";
    public override DownloadSourceTemplateType TemplateType => DownloadSourceTemplateType.Mcim;

    // Minecraft 官方资源（MCIM 不支持，返回官方源）
    public override string GetVersionManifestUrl()
    {
        return "https://piston-meta.mojang.com/mc/game/version_manifest.json";
    }

    public override string GetVersionInfoUrl(string versionId, string originalUrl)
    {
        return originalUrl;
    }

    public override string GetResourceUrl(string resourceType, string originalUrl)
    {
        return originalUrl;
    }

    public override string GetClientJarUrl(string versionId, string originalUrl)
    {
        return originalUrl;
    }

    public override string GetClientJsonUrl(string versionId, string originalUrl)
    {
        return originalUrl;
    }

    public override string GetLibraryUrl(string libraryName, string originalUrl)
    {
        return originalUrl ?? string.Empty;
    }

    // ModLoader 资源（MCIM 不支持，返回官方源）
    public override string GetNeoForgeVersionsUrl(string minecraftVersion)
    {
        return "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";
    }

    public override string GetNeoForgeInstallerUrl(string neoForgeVersion)
    {
        return $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoForgeVersion}/neoforge-{neoForgeVersion}-installer.jar";
    }

    public override string GetForgeVersionsUrl(string minecraftVersion)
    {
        return "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";
    }

    public override string GetForgeInstallerUrl(string minecraftVersion, string forgeVersion)
    {
        return $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{minecraftVersion}-{forgeVersion}/forge-{minecraftVersion}-{forgeVersion}-installer.jar";
    }

    public override string GetFabricVersionsUrl(string minecraftVersion)
    {
        return $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}";
    }

    public override string GetFabricProfileUrl(string minecraftVersion, string fabricVersion)
    {
        return $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}/{fabricVersion}/profile/json";
    }

    public override string GetQuiltVersionsUrl(string minecraftVersion)
    {
        return $"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}";
    }

    public override string GetQuiltProfileUrl(string minecraftVersion, string quiltVersion)
    {
        return $"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}/{quiltVersion}/profile/json";
    }

    public override string GetLegacyFabricVersionsUrl(string minecraftVersion)
    {
        return $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftVersion}";
    }

    public override string GetLegacyFabricProfileUrl(string minecraftVersion, string modLoaderVersion)
    {
        return $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftVersion}/{modLoaderVersion}/profile/json";
    }

    public override string GetLiteLoaderVersionsUrl()
    {
        return "http://dl.liteloader.com/versions/versions.json";
    }

    public override string GetLiteLoaderJarUrl(string relativePath, string? originalBaseUrl)
    {
        if (!string.IsNullOrEmpty(originalBaseUrl))
        {
            return (originalBaseUrl.EndsWith("/") ? originalBaseUrl : originalBaseUrl + "/") + relativePath;
        }
        return "https://repo.mumfrey.com/content/repositories/snapshots/" + relativePath;
    }

    // Modrinth API（MCIM 支持）
    public override string GetModrinthApiBaseUrl()
    {
        return "{baseUrl}/modrinth";
    }

    public override string GetModrinthCdnBaseUrl()
    {
        return "{baseUrl}";
    }

    public override string TransformModrinthApiUrl(string originalUrl)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
            return originalUrl;

        return originalUrl.Replace("https://api.modrinth.com", "{baseUrl}/modrinth");
    }

    public override string TransformModrinthCdnUrl(string originalUrl)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
            return originalUrl;

        return originalUrl.Replace("https://cdn.modrinth.com", "{baseUrl}");
    }

    public override string? GetModrinthUserAgent()
    {
        return VersionHelper.GetUserAgent();
    }

    public override bool RequiresModrinthUserAgent => true;

    // CurseForge API（MCIM 支持）
    public override string GetCurseForgeApiBaseUrl()
    {
        return "{baseUrl}/curseforge";
    }

    public override string GetCurseForgeCdnBaseUrl()
    {
        return "{baseUrl}";
    }

    public override string TransformCurseForgeApiUrl(string originalUrl)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
            return originalUrl;

        return originalUrl.Replace("https://api.curseforge.com", "{baseUrl}/curseforge");
    }

    public override string TransformCurseForgeCdnUrl(string originalUrl)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
            return originalUrl;

        // 只转换 edge.forgecdn.net，不转换 mediafilez.forgecdn.net
        if (originalUrl.Contains("mediafilez.forgecdn.net"))
            return originalUrl;

        return originalUrl.Replace("https://edge.forgecdn.net", "{baseUrl}");
    }

    public override string? GetCurseForgeUserAgent()
    {
        return VersionHelper.GetUserAgent();
    }

    public override bool RequiresCurseForgeUserAgent => true;

    public override bool ShouldIncludeCurseForgeApiKey => false;
}
