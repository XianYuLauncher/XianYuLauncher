using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO.Compression;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 整合包安装服务，支持 Modrinth (.mrpack) 和 CurseForge (manifest.json) 两种格式
/// </summary>
public class ModpackInstallationService : IModpackInstallationService
{
    private readonly IDownloadManager _downloadManager;
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly CurseForgeService _curseForgeService;
    private readonly ILocalSettingsService _localSettingsService;

    public ModpackInstallationService(
        IDownloadManager downloadManager,
        IMinecraftVersionService minecraftVersionService,
        CurseForgeService curseForgeService,
        ILocalSettingsService localSettingsService)
    {
        _downloadManager = downloadManager;
        _minecraftVersionService = minecraftVersionService;
        _curseForgeService = curseForgeService;
        _localSettingsService = localSettingsService;
    }

    public async Task<ModpackInstallResult> InstallModpackAsync(
        string downloadUrl,
        string fileName,
        string modpackDisplayName,
        string minecraftPath,
        IProgress<ModpackInstallProgress> progress,
        CancellationToken cancellationToken = default)
    {
        string tempDir = string.Empty;

        try
        {
            // 1. 下载整合包文件
            tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string mrpackPath = Path.Combine(tempDir, fileName);
            Directory.CreateDirectory(tempDir);

            await DownloadModpackFileAsync(downloadUrl, mrpackPath, progress, cancellationToken);

            Report(progress, 30, "30%", "下载完成，正在解压整合包...");

            // 2. 解压
            string extractDir = Path.Combine(tempDir, "extract");
            Directory.CreateDirectory(extractDir);
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(mrpackPath);
                archive.ExtractToDirectory(extractDir);
            }, cancellationToken);

            Report(progress, 40, "40%", "解压完成，正在解析整合包信息...");

            // 3. 检测格式并分派
            string curseForgeManifestPath = Path.Combine(extractDir, "manifest.json");
            string modrinthIndexPath = Path.Combine(extractDir, "modrinth.index.json");

            if (File.Exists(curseForgeManifestPath))
            {
                Debug.WriteLine("[整合包安装] 检测到CurseForge整合包格式");
                return await InstallCurseForgeModpackCoreAsync(
                    extractDir, curseForgeManifestPath, modpackDisplayName, minecraftPath, progress, cancellationToken);
            }

            if (File.Exists(modrinthIndexPath))
            {
                Debug.WriteLine("[整合包安装] 检测到Modrinth整合包格式");
                return await InstallModrinthModpackCoreAsync(
                    extractDir, modrinthIndexPath, modpackDisplayName, minecraftPath, progress, cancellationToken);
            }

            return ModpackInstallResult.Failed("整合包格式不支持：未找到manifest.json（CurseForge）或modrinth.index.json（Modrinth）");
        }
        catch (OperationCanceledException)
        {
            Report(progress, 0, "0%", "安装已取消");
            return ModpackInstallResult.Failed("安装已取消");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[整合包安装] 安装失败: {ex}");
            return ModpackInstallResult.Failed(ex.Message);
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }

    #region Modrinth 整合包

    private async Task<ModpackInstallResult> InstallModrinthModpackCoreAsync(
        string extractDir,
        string indexPath,
        string modpackDisplayName,
        string minecraftPath,
        IProgress<ModpackInstallProgress> progress,
        CancellationToken cancellationToken)
    {
        string indexJson = await File.ReadAllTextAsync(indexPath, cancellationToken);
        var indexData = JObject.Parse(indexJson);

        // 提取依赖信息
        string minecraftVersion = indexData["dependencies"]?["minecraft"]?.ToString()
            ?? throw new Exception("整合包中缺少Minecraft版本依赖信息");

        var (modLoaderType, modLoaderName, modLoaderVersion) = ParseModrinthDependencies(indexData);

        Report(progress, 50, "50%", $"正在下载Minecraft {minecraftVersion} 和 {modLoaderType} {modLoaderVersion}...");

        // 构建版本名称
        string sanitizedName = modpackDisplayName.Replace(" ", "-");
        string modpackVersionId = $"{sanitizedName}-{minecraftVersion}-{modLoaderName}";

        // 下载 MC + Mod Loader
        await _minecraftVersionService.DownloadModLoaderVersionAsync(
            minecraftVersion, modLoaderType, modLoaderVersion, minecraftPath,
            status =>
            {
                double p = 50 + (status.Percent / 100) * 30;
                Report(progress, p, $"{p:F1}%", $"正在下载Minecraft {minecraftVersion} 和 {modLoaderType} {modLoaderVersion}...", status.SpeedText);
            },
            cancellationToken, modpackVersionId);

        Report(progress, 80, "80%", "版本下载完成，正在部署整合包文件...");

        string modpackVersionDir = Path.Combine(minecraftPath, "versions", modpackVersionId);

        // 复制 overrides
        string overridesDir = Path.Combine(extractDir, "overrides");
        if (Directory.Exists(overridesDir))
        {
            await Task.Run(() => CopyDirectory(overridesDir, modpackVersionDir), cancellationToken);
        }

        // 下载 files 列表中的文件
        var files = indexData["files"] as JArray;
        if (files != null && files.Count > 0)
        {
            await DownloadModrinthFilesAsync(files, modpackVersionDir, progress, cancellationToken);
        }

        Report(progress, 100, "100%", "整合包安装完成！");
        return ModpackInstallResult.Succeeded(modpackDisplayName, modpackVersionId);
    }

    private static (string Type, string Name, string Version) ParseModrinthDependencies(JObject indexData)
    {
        var deps = indexData["dependencies"]
            ?? throw new Exception("整合包中缺少Mod Loader依赖信息");

        if (deps["fabric-loader"] != null)
            return ("Fabric", "fabric", deps["fabric-loader"]!.ToString());

        if (deps["forge"] != null)
            return ("Forge", "forge", deps["forge"]!.ToString());

        if (deps["neoforge"] != null)
            return ("NeoForge", "neoforge", deps["neoforge"]!.ToString());

        if (deps["quilt-loader"] != null)
            return ("Quilt", "quilt", deps["quilt-loader"]!.ToString());

        throw new Exception("整合包中缺少Mod Loader依赖信息");
    }

    private async Task DownloadModrinthFilesAsync(
        JArray files,
        string modpackVersionDir,
        IProgress<ModpackInstallProgress> progress,
        CancellationToken cancellationToken)
    {
        Report(progress, 80, "80%", "正在下载整合包文件...");

        int totalFiles = files.Count;
        int downloadedFiles = 0;

        var threadCount = await _localSettingsService.ReadSettingAsync<int?>("DownloadThreadCount") ?? 32;
        Debug.WriteLine($"[Modrinth整合包] 开始多线程下载，线程数: {threadCount}，文件总数: {totalFiles}");

        using var semaphore = new SemaphoreSlim(threadCount);
        var downloadTasks = new List<Task>();

        // 预先创建目录
        foreach (var fileItem in files)
        {
            var path = fileItem["path"]?.ToString();
            if (!string.IsNullOrEmpty(path))
            {
                string targetPath = Path.Combine(modpackVersionDir, path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            }
        }

        foreach (var fileItem in files)
        {
            var downloads = fileItem["downloads"] as JArray;
            var path = fileItem["path"]?.ToString();

            if (downloads == null || downloads.Count == 0 || string.IsNullOrEmpty(path))
                continue;

            string downloadUrl = downloads[0]!.ToString();
            string targetPath = Path.Combine(modpackVersionDir, path.Replace('/', Path.DirectorySeparatorChar));
            string fileDisplayName = Path.GetFileName(path);

            var downloadTask = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Debug.WriteLine($"[Modrinth整合包] 开始下载: {fileDisplayName}");

                    await _downloadManager.DownloadFileAsync(downloadUrl, targetPath, null, null, cancellationToken);

                    var completed = Interlocked.Increment(ref downloadedFiles);
                    double p = 80 + ((double)completed / totalFiles) * 20;
                    Report(progress, p, $"{p:F1}%", $"正在下载整合包文件 ({completed}/{totalFiles})...");
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            downloadTasks.Add(downloadTask);
        }

        await Task.WhenAll(downloadTasks);
        Debug.WriteLine($"[Modrinth整合包] 所有文件下载完成，共 {downloadedFiles} 个");
    }

    #endregion

    #region CurseForge 整合包

    private async Task<ModpackInstallResult> InstallCurseForgeModpackCoreAsync(
        string extractDir,
        string manifestPath,
        string modpackDisplayName,
        string minecraftPath,
        IProgress<ModpackInstallProgress> progress,
        CancellationToken cancellationToken)
    {
        string manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        var manifest = System.Text.Json.JsonSerializer.Deserialize<CurseForgeManifest>(manifestJson,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("无法解析CurseForge整合包manifest.json");

        Debug.WriteLine($"[CurseForge整合包] 名称: {manifest.Name}, 版本: {manifest.Version}");

        string minecraftVersion = manifest.Minecraft?.Version
            ?? throw new Exception("整合包中缺少Minecraft版本信息");

        var (modLoaderType, modLoaderName, modLoaderVersion) = ParseCurseForgeDependencies(manifest);

        string sanitizedName = (manifest.Name ?? modpackDisplayName).Replace(" ", "-");
        string modpackVersionId = $"{sanitizedName}-{minecraftVersion}-{modLoaderName}";

        Report(progress, 45, "45%", $"正在下载Minecraft {minecraftVersion} 和 {modLoaderType} {modLoaderVersion}...");

        // 下载 MC + Mod Loader
        await _minecraftVersionService.DownloadModLoaderVersionAsync(
            minecraftVersion, modLoaderType, modLoaderVersion, minecraftPath,
            status =>
            {
                double p = 45 + (status.Percent / 100) * 15;
                Report(progress, p, $"{p:F1}%", $"正在下载Minecraft {minecraftVersion} 和 {modLoaderType} {modLoaderVersion}...", status.SpeedText);
            },
            cancellationToken, modpackVersionId);

        Report(progress, 60, "60%", "版本下载完成，正在部署整合包文件...");

        string modpackVersionDir = Path.Combine(minecraftPath, "versions", modpackVersionId);

        // 复制 overrides
        string overridesFolderName = manifest.Overrides ?? "overrides";
        string overridesDir = Path.Combine(extractDir, overridesFolderName);
        if (Directory.Exists(overridesDir))
        {
            Report(progress, 60, "60%", "正在复制覆盖文件...");
            await Task.Run(() => CopyDirectory(overridesDir, modpackVersionDir), cancellationToken);
        }

        Report(progress, 65, "65%", "正在获取资源信息...");

        // 下载整合包中的文件
        if (manifest.Files != null && manifest.Files.Count > 0)
        {
            await DownloadCurseForgeFilesAsync(manifest, modpackVersionDir, progress, cancellationToken);
        }

        Report(progress, 100, "100%", "整合包安装完成！");
        return ModpackInstallResult.Succeeded(manifest.Name ?? modpackDisplayName, modpackVersionId);
    }

    private static (string Type, string Name, string Version) ParseCurseForgeDependencies(CurseForgeManifest manifest)
    {
        var primaryModLoader = manifest.Minecraft?.ModLoaders?.FirstOrDefault(ml => ml.Primary)
            ?? manifest.Minecraft?.ModLoaders?.FirstOrDefault()
            ?? throw new Exception("整合包中缺少ModLoader信息");

        if (string.IsNullOrEmpty(primaryModLoader.Id))
            throw new Exception("整合包中缺少ModLoader信息");

        var loaderParts = primaryModLoader.Id.Split('-', 2);
        if (loaderParts.Length < 2)
            throw new Exception($"无法解析ModLoader信息: {primaryModLoader.Id}");

        string loaderPrefix = loaderParts[0].ToLower();
        string version = loaderParts[1];

        return loaderPrefix switch
        {
            "forge" => ("Forge", "forge", version),
            "fabric" => ("Fabric", "fabric", version),
            "quilt" => ("Quilt", "quilt", version),
            "neoforge" => ("NeoForge", "neoforge", version),
            _ => throw new Exception($"不支持的Mod Loader类型: {loaderPrefix}")
        };
    }

    private async Task DownloadCurseForgeFilesAsync(
        CurseForgeManifest manifest,
        string modpackVersionDir,
        IProgress<ModpackInstallProgress> progress,
        CancellationToken cancellationToken)
    {
        // 获取项目 classId 信息
        var projectIds = manifest.Files.Select(f => f.ProjectId).Distinct().ToList();
        var projectClassIdMap = new Dictionary<int, int>();

        try
        {
            var modInfos = await _curseForgeService.GetModsByIdsAsync(projectIds);
            foreach (var mod in modInfos)
            {
                if (mod.ClassId.HasValue)
                    projectClassIdMap[mod.Id] = mod.ClassId.Value;
            }
            Debug.WriteLine($"[CurseForge整合包] 获取到 {projectClassIdMap.Count} 个项目的classId信息");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CurseForge整合包] 获取项目classId失败: {ex.Message}");
        }

        // 获取文件详情
        var fileIds = manifest.Files.Select(f => f.FileId).ToList();
        List<CurseForgeFile> fileDetails;
        try
        {
            fileDetails = await _curseForgeService.GetFilesByIdsAsync(fileIds);
        }
        catch
        {
            fileDetails = new List<CurseForgeFile>();
            foreach (var mf in manifest.Files)
            {
                try
                {
                    var file = await _curseForgeService.GetFileAsync(mf.ProjectId, mf.FileId);
                    if (file != null) fileDetails.Add(file);
                }
                catch { /* skip failed file lookups */ }
            }
        }

        Report(progress, 70, "70%", "正在下载整合包文件...");

        // 预先创建目录
        string modsDir = Path.Combine(modpackVersionDir, "mods");
        string resourcePacksDir = Path.Combine(modpackVersionDir, "resourcepacks");
        string shaderPacksDir = Path.Combine(modpackVersionDir, "shaderpacks");
        string dataPacksDir = Path.Combine(modpackVersionDir, "datapacks");
        Directory.CreateDirectory(modsDir);
        Directory.CreateDirectory(resourcePacksDir);
        Directory.CreateDirectory(shaderPacksDir);
        Directory.CreateDirectory(dataPacksDir);

        int totalFiles = fileDetails.Count;
        int downloadedFiles = 0;

        var threadCount = await _localSettingsService.ReadSettingAsync<int?>("DownloadThreadCount") ?? 32;
        Debug.WriteLine($"[CurseForge整合包] 开始多线程下载，线程数: {threadCount}，文件总数: {totalFiles}");

        using var semaphore = new SemaphoreSlim(threadCount);
        var downloadTasks = new List<Task>();

        foreach (var file in fileDetails)
        {
            string fileDisplayName = file.FileName ?? $"file_{file.Id}";

            if (string.IsNullOrEmpty(file.DownloadUrl))
                continue;

            string targetDir = ResolveTargetDir(file.ModId, projectClassIdMap,
                modsDir, resourcePacksDir, shaderPacksDir, dataPacksDir);
            string targetPath = Path.Combine(targetDir, fileDisplayName);
            string downloadUrl = file.DownloadUrl;

            var downloadTask = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Debug.WriteLine($"[CurseForge整合包] 开始下载: {fileDisplayName}");

                    await _curseForgeService.DownloadFileAsync(downloadUrl, targetPath, null, cancellationToken);

                    var completed = Interlocked.Increment(ref downloadedFiles);
                    double p = 70 + ((double)completed / totalFiles) * 30;
                    Report(progress, p, $"{p:F1}%", $"正在下载整合包文件 ({completed}/{totalFiles})...");
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            downloadTasks.Add(downloadTask);
        }

        await Task.WhenAll(downloadTasks);
        Debug.WriteLine($"[CurseForge整合包] 所有文件下载完成，共 {downloadedFiles} 个");
    }

    private static string ResolveTargetDir(
        int modId,
        Dictionary<int, int> projectClassIdMap,
        string modsDir, string resourcePacksDir, string shaderPacksDir, string dataPacksDir)
    {
        if (!projectClassIdMap.TryGetValue(modId, out int classId))
            return modsDir;

        return classId switch
        {
            6 => modsDir,
            12 => resourcePacksDir,
            6552 => shaderPacksDir,
            6945 => dataPacksDir,
            _ => modsDir
        };
    }

    #endregion

    #region 公共辅助方法

    private async Task DownloadModpackFileAsync(
        string downloadUrl,
        string targetPath,
        IProgress<ModpackInstallProgress> progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(downloadUrl))
            throw new Exception("下载链接为空，无法下载整合包");

        if (downloadUrl.StartsWith("http://") || downloadUrl.StartsWith("https://"))
        {
            await _downloadManager.DownloadFileAsync(
                downloadUrl, targetPath, null,
                status =>
                {
                    double p = status.Percent * 0.3;
                    Report(progress, p, $"{p:F1}%", "正在下载整合包...", status.SpeedText);
                },
                cancellationToken);
        }
        else
        {
            // 本地文件复制
            Report(progress, 0, "0%", "正在复制本地整合包文件...");
            long totalBytes = new FileInfo(downloadUrl).Length;
            long totalRead = 0;

            using var sourceStream = new FileStream(downloadUrl, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalRead += bytesRead;

                double p = (double)totalRead / totalBytes * 30;
                Report(progress, p, $"{p:F1}%", "正在复制本地整合包文件...");
            }
        }
    }

    private static void Report(IProgress<ModpackInstallProgress> progress, double percent, string percentText, string status, string speed = "")
    {
        progress.Report(new ModpackInstallProgress
        {
            Progress = percent,
            ProgressText = percentText,
            Status = status,
            Speed = speed
        });
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    private static void CleanupTempDir(string tempDir)
    {
        if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
        {
            try { Directory.Delete(tempDir, true); }
            catch { /* best effort cleanup */ }
        }
    }

    #endregion
}
