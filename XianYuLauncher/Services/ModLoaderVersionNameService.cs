using System.IO;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Services;

public class ModLoaderVersionNameService : IModLoaderVersionNameService
{
    private readonly IFileService _fileService;

    public ModLoaderVersionNameService(IFileService fileService)
    {
        _fileService = fileService;
    }

    public string GenerateVersionName(string minecraftVersion, string? modLoaderType, string? modLoaderVersion, bool isOptifineSelected, string? optifineVersion)
    {
        if (string.IsNullOrEmpty(minecraftVersion))
        {
            return "";
        }

        string baseName = minecraftVersion;
        
        // 1. 添加 ModLoader 部分
        if (!string.IsNullOrEmpty(modLoaderType) && !string.IsNullOrEmpty(modLoaderVersion) && modLoaderType.ToLower() != "vanilla")
        {
            string shortVersion = modLoaderVersion;
            
            // 针对不同 Loader 的版本号简化逻辑
            // Forge/NeoForge 通常比较长，这里保持原逻辑直接拼接，或者根据需要截断
            // 原 ViewModel 逻辑似乎是直接拼接
            
            baseName += $"-{modLoaderType}-{shortVersion}";
        }
        
        // 2. 添加 Optifine 部分
        if (isOptifineSelected && !string.IsNullOrEmpty(optifineVersion))
        {
            baseName += $"-Optifine-{optifineVersion}";
        }

        return baseName;
    }

    public (bool IsValid, string ErrorMessage) ValidateVersionName(string versionName)
    {
        if (string.IsNullOrWhiteSpace(versionName))
        {
            return (false, "ModLoaderSelector_VersionNameError_Empty".GetLocalized());
        }

        // 检查版本目录是否已存在
        try
        {
            string minecraftDirectory = _fileService.GetMinecraftDataPath();
            string versionsDirectory = Path.Combine(minecraftDirectory, "versions");
            string versionDirectory = Path.Combine(versionsDirectory, versionName);

            if (Directory.Exists(versionDirectory))
            {
                return (false, string.Format("ModLoaderSelector_VersionNameError_Exists".GetLocalized(), versionName));
            }
            
            // 还可以增加文件名非法字符检测
            if (versionName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                 return (false, "版本名称包含非法字符");
            }
        }
        catch
        {
            // 忽略路径检查异常
        }

        return (true, "");
    }
}
