using System;
using System.Collections.Generic;
using System.IO;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

public static class ModDownloadPlanningHelper
{
    public static bool ShouldForceDirectDownload(string? url)
    {
        // TODO(refactor-phase3-after): 目前仅对已确认异常的 host 做最小兜底。
        // 后续应沉淀为可配置的下载能力策略(如 Range 支持探测/缓存)，避免硬编码 host 规则扩散。
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Equals("edge.forgecdn.net", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveDownloadUrl(
        string? existingUrl,
        CurseForgeFile? curseForgeFile,
        string? fallbackFileName,
        Func<long, string, string> constructDownloadUrl)
    {
        if (!string.IsNullOrWhiteSpace(existingUrl))
        {
            return existingUrl;
        }

        if (curseForgeFile == null)
        {
            return string.Empty;
        }

        string fileName = curseForgeFile.FileName ?? fallbackFileName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        return constructDownloadUrl(curseForgeFile.Id, fileName);
    }

    public static string BuildVersionTargetDirectory(string minecraftPath, string versionName, string projectType)
    {
        string versionDir = Path.Combine(minecraftPath, "versions", versionName);
        string targetFolder = ModResourcePathHelper.NormalizeProjectType(projectType) switch
        {
            "resourcepack" => "resourcepacks",
            "shader" => "shaderpacks",
            _ => "mods"
        };

        return Path.Combine(versionDir, targetFolder);
    }

    public static bool ShouldSkipDependencyProcessing(string projectType, string? loaderType, string? gameVersionId)
    {
        if (ModResourcePathHelper.NormalizeProjectType(projectType) == "mod")
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(loaderType) ||
               loaderType.Equals("vanilla", StringComparison.OrdinalIgnoreCase) ||
               loaderType.Equals("minecraft", StringComparison.OrdinalIgnoreCase) ||
               string.IsNullOrWhiteSpace(gameVersionId);
    }

    public static void ApplyNonModDependencyContext(ModrinthVersion version, string loaderType, string gameVersionId)
    {
        version.Loaders = new List<string> { loaderType };
        version.GameVersions = new List<string> { gameVersionId };
    }
}