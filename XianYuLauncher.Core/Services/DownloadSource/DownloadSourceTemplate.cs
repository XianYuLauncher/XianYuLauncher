using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services.DownloadSource;

/// <summary>
/// 下载源模板基类
/// </summary>
public abstract class DownloadSourceTemplate
{
    public abstract string TemplateName { get; }
    public abstract DownloadSourceTemplateType TemplateType { get; }

    // Minecraft 官方资源
    public abstract string GetVersionManifestUrl();
    public abstract string GetVersionInfoUrl(string versionId, string originalUrl);
    public abstract string GetResourceUrl(string resourceType, string originalUrl);
    public abstract string GetClientJarUrl(string versionId, string originalUrl);
    public abstract string GetClientJsonUrl(string versionId, string originalUrl);
    public abstract string GetLibraryUrl(string libraryName, string originalUrl);

    // ModLoader 资源
    public abstract string GetNeoForgeVersionsUrl(string minecraftVersion);
    public abstract string GetNeoForgeInstallerUrl(string neoForgeVersion);
    public abstract string GetForgeVersionsUrl(string minecraftVersion);
    public abstract string GetForgeInstallerUrl(string minecraftVersion, string forgeVersion);
    public abstract string GetFabricVersionsUrl(string minecraftVersion);
    public abstract string GetFabricProfileUrl(string minecraftVersion, string fabricVersion);
    public abstract string GetQuiltVersionsUrl(string minecraftVersion);
    public abstract string GetQuiltProfileUrl(string minecraftVersion, string quiltVersion);
    public abstract string GetLegacyFabricVersionsUrl(string minecraftVersion);
    public abstract string GetLegacyFabricProfileUrl(string minecraftVersion, string modLoaderVersion);
    public abstract string GetLiteLoaderVersionsUrl();
    public abstract string GetLiteLoaderJarUrl(string relativePath, string? originalBaseUrl);

    // Modrinth API
    public abstract string GetModrinthApiBaseUrl();
    public abstract string GetModrinthCdnBaseUrl();
    public abstract string TransformModrinthApiUrl(string originalUrl);
    public abstract string TransformModrinthCdnUrl(string originalUrl);
    public abstract string? GetModrinthUserAgent();
    public abstract bool RequiresModrinthUserAgent { get; }

    // CurseForge API
    public abstract string GetCurseForgeApiBaseUrl();
    public abstract string GetCurseForgeCdnBaseUrl();
    public abstract string TransformCurseForgeApiUrl(string originalUrl);
    public abstract string TransformCurseForgeCdnUrl(string originalUrl);
    public abstract string? GetCurseForgeUserAgent();
    public abstract bool RequiresCurseForgeUserAgent { get; }
    public abstract bool ShouldIncludeCurseForgeApiKey { get; }
}
