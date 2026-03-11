using XianYuLauncher.Core.Models;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.ModDownloadDetail.Services;

public interface IModResourceDownloadOrchestrator
{
    string EnsureDownloadUrl(ModVersionViewModel modVersion);

    Task ProcessDependenciesForResourceAsync(
        string projectType,
        string minecraftPath,
        ModVersionViewModel modVersion,
        string targetDir,
        InstalledGameVersionViewModel? gameVersion,
        Action<string, double, string>? onProgress = null);

    Task StartResourceDownloadAsync(
        string modName,
        string projectType,
        string modIconUrl,
        string downloadUrl,
        string savePath,
        Action initializeTeachingTip);
}