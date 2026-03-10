using System;
using System.Collections.Generic;
using System.IO;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

public static class ModDownloadPlanningHelper
{
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