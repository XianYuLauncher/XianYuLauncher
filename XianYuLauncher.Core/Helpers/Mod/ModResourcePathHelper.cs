using System.IO;

namespace XianYuLauncher.Core.Helpers;

public static class ModResourcePathHelper
{
    public static string NormalizeProjectType(string? projectType)
    {
        if (string.IsNullOrEmpty(projectType))
        {
            return "mod";
        }

        return projectType.ToLower() switch
        {
            "shaderpack" => "shader",
            _ => projectType.ToLower()
        };
    }

    public static string MapCurseForgeClassIdToProjectType(int? classId) =>
        classId switch
        {
            12 => "resourcepack",
            17 => "world",
            4471 => "modpack",
            6552 => "shader",
            6945 => "datapack",
            _ => "mod"
        };

    public static string GetDependencyTargetDir(string minecraftPath, string? versionName, string projectType)
    {
        string baseDir = string.IsNullOrEmpty(versionName)
            ? minecraftPath
            : Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionName);

        string targetFolder = NormalizeProjectType(projectType) switch
        {
            "resourcepack" => MinecraftPathConsts.ResourcePacks,
            "shader" => MinecraftPathConsts.ShaderPacks,
            "datapack" => MinecraftPathConsts.Datapacks,
            "world" => MinecraftPathConsts.Mods,
            _ => MinecraftPathConsts.Mods
        };

        return Path.Combine(baseDir, targetFolder);
    }

    /// <summary>
    /// 基于已解析的 gameDir 获取依赖目标目录（推荐）。
    /// </summary>
    public static string GetDependencyTargetDir(string gameDir, string projectType)
    {
        string targetFolder = NormalizeProjectType(projectType) switch
        {
            "resourcepack" => MinecraftPathConsts.ResourcePacks,
            "shader" => MinecraftPathConsts.ShaderPacks,
            "datapack" => MinecraftPathConsts.Datapacks,
            "world" => MinecraftPathConsts.Mods,
            _ => MinecraftPathConsts.Mods
        };

        return Path.Combine(gameDir, targetFolder);
    }

    public static string GetUniqueDirectoryPath(string parentDir, string baseName)
    {
        string targetPath = Path.Combine(parentDir, baseName);
        if (!Directory.Exists(targetPath))
        {
            return targetPath;
        }

        int counter = 1;
        while (true)
        {
            string newPath = Path.Combine(parentDir, $"{baseName}_{counter}");
            if (!Directory.Exists(newPath))
            {
                return newPath;
            }
            counter++;
        }
    }

    public static string GenerateModrinthUrl(string projectType, string slug)
    {
        string typeSegment = projectType switch
        {
            "shaderpack" => "shader",
            _ => projectType
        };

        return $"https://modrinth.com/{typeSegment}/{slug}";
    }
}
