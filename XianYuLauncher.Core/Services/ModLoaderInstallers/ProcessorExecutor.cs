using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services.ModLoaderInstallers;

/// <summary>
/// 处理器执行器接口
/// </summary>
public interface IProcessorExecutor
{
    /// <summary>
    /// 执行处理器列表
    /// </summary>
    Task ExecuteProcessorsAsync(
        JArray processors,
        string installerPath,
        string versionDirectory,
        string librariesDirectory,
        string installProfilePath,
        string extractDirectory,
        string modLoaderType,
        VersionConfig versionConfig,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// ModLoader处理器执行器
/// </summary>
public class ProcessorExecutor : IProcessorExecutor
{
    private readonly IDownloadManager _downloadManager;
    private readonly ILibraryManager _libraryManager;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ILogger<ProcessorExecutor> _logger;

    public ProcessorExecutor(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        DownloadSourceFactory downloadSourceFactory,
        ILocalSettingsService localSettingsService,
        ILogger<ProcessorExecutor> logger)
    {
        _downloadManager = downloadManager;
        _libraryManager = libraryManager;
        _downloadSourceFactory = downloadSourceFactory;
        _localSettingsService = localSettingsService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExecuteProcessorsAsync(
        JArray processors,
        string installerPath,
        string versionDirectory,
        string librariesDirectory,
        string installProfilePath,
        string extractDirectory,
        string modLoaderType,
        VersionConfig versionConfig,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (processors == null || processors.Count == 0)
        {
            _logger.LogInformation("没有需要执行的处理器");
            progressCallback?.Invoke(100);
            return;
        }

        // 筛选客户端处理器
        var clientProcessors = FilterClientProcessors(processors);
        _logger.LogInformation("共找到 {Count} 个客户端处理器", clientProcessors.Count);

        if (clientProcessors.Count == 0)
        {
            progressCallback?.Invoke(100);
            return;
        }

        int totalProcessors = clientProcessors.Count;
        int executedProcessors = 0;

        foreach (var processor in clientProcessors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            executedProcessors++;
            _logger.LogInformation("执行处理器 {Current}/{Total}", executedProcessors, totalProcessors);

            await ExecuteSingleProcessorAsync(
                processor,
                installerPath,
                versionDirectory,
                librariesDirectory,
                installProfilePath,
                extractDirectory,
                modLoaderType,
                versionConfig,
                cancellationToken);

            var progress = (double)executedProcessors / totalProcessors * 100;
            progressCallback?.Invoke(progress);
        }

        _logger.LogInformation("所有处理器执行完成");
    }

    private List<JObject> FilterClientProcessors(JArray processors)
    {
        var clientProcessors = new List<JObject>();

        foreach (var processorToken in processors)
        {
            if (processorToken is not JObject processor) continue;

            bool isServerProcessor = false;

            // 检查server字段
            if (processor.ContainsKey("server"))
            {
                isServerProcessor = processor["server"]?.Value<bool>() ?? false;
            }
            // 检查sides字段
            else if (processor.ContainsKey("sides"))
            {
                var sides = processor["sides"] as JArray;
                if (sides != null)
                {
                    isServerProcessor = !sides.Any(side => side.ToString() == "client");
                }
            }

            if (!isServerProcessor)
            {
                clientProcessors.Add(processor);
            }
        }

        return clientProcessors;
    }

    private async Task ExecuteSingleProcessorAsync(
        JObject processor,
        string installerPath,
        string versionDirectory,
        string librariesDirectory,
        string installProfilePath,
        string extractDirectory,
        string modLoaderType,
        VersionConfig versionConfig,
        CancellationToken cancellationToken)
    {
        string jar = string.Empty;
        string mainClass = string.Empty;

        try
        {
            // 获取处理器信息
            jar = processor["jar"]?.ToString() ?? throw new ProcessorExecutionException("处理器缺少jar字段", "jar", null);
            var classpath = processor["classpath"] as JArray ?? throw new ProcessorExecutionException("处理器缺少classpath字段", jar);
            var args = processor["args"] as JArray ?? throw new ProcessorExecutionException("处理器缺少args字段", jar);

            _logger.LogDebug("处理器jar: {Jar}", jar);

            // 下载installer tools
            string installerToolsPath = await DownloadInstallerToolsAsync(jar, librariesDirectory, cancellationToken);

            // 获取主类
            mainClass = GetMainClassFromJar(installerToolsPath);
            _logger.LogDebug("处理器主类: {MainClass}", mainClass);

            // 读取install_profile.json中的data字段
            var installProfile = JObject.Parse(await File.ReadAllTextAsync(installProfilePath, cancellationToken));
            var data = installProfile["data"] as JObject ?? new JObject();

            // 处理参数
            var processedArgs = ProcessArguments(
                args, data, installerPath, versionDirectory, librariesDirectory,
                extractDirectory, modLoaderType, versionConfig);

            // 构建classpath
            var fullClassPath = BuildClassPath(installerToolsPath, classpath, librariesDirectory);

            // 执行Java命令
            await ExecuteJavaCommandAsync(
                mainClass, fullClassPath, processedArgs, librariesDirectory, modLoaderType, jar, cancellationToken);
        }
        catch (ProcessorExecutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行处理器失败: {Jar}", jar);
            throw new ProcessorExecutionException($"执行处理器失败: {ex.Message}", jar, null, null, ex);
        }
    }

    private List<string> ProcessArguments(
        JArray args,
        JObject data,
        string installerPath,
        string versionDirectory,
        string librariesDirectory,
        string extractDirectory,
        string modLoaderType,
        VersionConfig versionConfig)
    {
        var processedArgs = new List<string>();
        string minecraftPath = Path.GetDirectoryName(librariesDirectory) ?? string.Empty;
        string? currentParam = null;
        bool isNextArgOptional = false;

        string minecraftVersion = versionConfig.MinecraftVersion ?? string.Empty;
        string modLoaderVersion = versionConfig.ModLoaderVersion ?? string.Empty;

        foreach (var argToken in args)
        {
            string arg = argToken.ToString();

            // 检查是否为参数名
            if (arg.StartsWith("--"))
            {
                currentParam = arg;
                processedArgs.Add(arg);
                if (arg == "--optional")
                {
                    isNextArgOptional = true;
                }
                continue;
            }

            // 处理参数值
            string paramValue = arg;

            // 替换标准占位符
            paramValue = paramValue.Replace("{INSTALLER}", installerPath);
            paramValue = paramValue.Replace("{ROOT}", minecraftPath);
            paramValue = paramValue.Replace("{SIDE}", "client");

            // 处理通用占位符
            paramValue = ProcessDataPlaceholders(paramValue, data, librariesDirectory);

            // 处理Minecraft JAR路径
            if (!string.IsNullOrEmpty(minecraftVersion))
            {
                var versionDirName = Path.GetFileName(versionDirectory);
                paramValue = paramValue.Replace("{MINECRAFT_JAR}", 
                    Path.Combine(versionDirectory, $"{versionDirName}.jar"));
                paramValue = paramValue.Replace("{MOJMAPS}", 
                    Path.Combine(librariesDirectory, "net", "minecraft", "client", minecraftVersion, $"client-{minecraftVersion}-mappings.txt"));
            }

            // 处理PATCHED占位符
            paramValue = ProcessPatchedPlaceholder(paramValue, data, librariesDirectory, modLoaderType, minecraftVersion, modLoaderVersion);

            // 处理BINPATCH占位符
            paramValue = ProcessBinpatchPlaceholder(paramValue, extractDirectory, librariesDirectory, modLoaderType, modLoaderVersion);

            // 处理Maven坐标格式的参数
            if (paramValue.StartsWith("[") && paramValue.EndsWith("]"))
            {
                paramValue = ResolveMavenCoordinate(paramValue, librariesDirectory);
            }

            // 处理--optional参数值
            if (isNextArgOptional)
            {
                paramValue = File.Exists(paramValue) ? "1" : "0";
                isNextArgOptional = false;
            }

            // Windows路径格式修正
            if (paramValue.Contains("/") || paramValue.Contains("\\"))
            {
                paramValue = paramValue.Replace("/", "\\");
                if (paramValue.EndsWith("\\"))
                {
                    paramValue = paramValue[..^1];
                }
                paramValue = $"\"{paramValue}\"";
            }

            processedArgs.Add(paramValue);
            currentParam = null;
        }

        return processedArgs;
    }

    private string ProcessDataPlaceholders(string paramValue, JObject data, string librariesDirectory)
    {
        var placeholderRegex = new Regex(@"\{(\w+)\}");
        var matches = placeholderRegex.Matches(paramValue);

        foreach (Match match in matches)
        {
            string placeholderName = match.Groups[1].Value;
            string placeholder = match.Value;

            var dataItem = data[placeholderName] as JObject;
            if (dataItem == null) continue;

            string dataClientValue = dataItem["client"]?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(dataClientValue) || !dataClientValue.StartsWith("[") || !dataClientValue.EndsWith("]"))
                continue;

            string localPath = ResolveMavenCoordinate(dataClientValue, librariesDirectory);
            paramValue = paramValue.Replace(placeholder, localPath);
        }

        return paramValue;
    }

    private string ProcessPatchedPlaceholder(
        string paramValue, JObject data, string librariesDirectory,
        string modLoaderType, string minecraftVersion, string modLoaderVersion)
    {
        if (!paramValue.Contains("{PATCHED}")) return paramValue;

        string modLoaderGroup = modLoaderType.ToLower() == "neoforge" ? "net/neoforged" : "net/minecraftforge";

        if (modLoaderType.ToLower() == "neoforge")
        {
            return paramValue.Replace("{PATCHED}", 
                Path.Combine(librariesDirectory, modLoaderGroup.Replace('/', Path.DirectorySeparatorChar), 
                    "minecraft-client-patched", modLoaderVersion, $"minecraft-client-patched-{modLoaderVersion}.jar"));
        }

        // Forge: 优先从data字段获取
        var patched = data["PATCHED"] as JObject;
        string patchedClient = patched?["client"]?.ToString() ?? string.Empty;

        if (!string.IsNullOrEmpty(patchedClient) && patchedClient.StartsWith("[") && patchedClient.EndsWith("]"))
        {
            return paramValue.Replace("{PATCHED}", ResolveMavenCoordinate(patchedClient, librariesDirectory));
        }

        // 回退方案
        if (!string.IsNullOrEmpty(minecraftVersion) && !string.IsNullOrEmpty(modLoaderVersion))
        {
            string fullForgeVersion = $"{minecraftVersion}-{modLoaderVersion}";
            return paramValue.Replace("{PATCHED}", 
                Path.Combine(librariesDirectory, modLoaderGroup.Replace('/', Path.DirectorySeparatorChar), 
                    "forge", fullForgeVersion, $"forge-{fullForgeVersion}-client.jar"));
        }

        return paramValue;
    }

    private string ProcessBinpatchPlaceholder(
        string paramValue, string extractDirectory, string librariesDirectory,
        string modLoaderType, string modLoaderVersion)
    {
        if (!paramValue.Contains("{BINPATCH}")) return paramValue;

        string[] possiblePaths = {
            Path.Combine(extractDirectory, "data", "client.lzma"),
            Path.Combine(extractDirectory, "data", "client", "client.lzma")
        };

        string? clientLzmaPath = possiblePaths.FirstOrDefault(File.Exists);
        if (clientLzmaPath == null)
        {
            throw new ProcessorExecutionException(
                $"client.lzma文件不存在，尝试了以下路径: {string.Join(", ", possiblePaths)}",
                "BINPATCH");
        }

        string modLoaderGroup = modLoaderType.ToLower() == "neoforge" ? "net/neoforged" : "net/minecraftforge";

        paramValue = paramValue.Replace("{BINPATCH}", clientLzmaPath);
        paramValue = paramValue.Replace("{EXTRACT_FILES}", "EXTRACT_FILES");
        paramValue = paramValue.Replace("{EXTRACT_TO}", 
            Path.Combine(librariesDirectory, modLoaderGroup.Replace('/', Path.DirectorySeparatorChar), 
                modLoaderType.ToLower(), modLoaderVersion));

        return paramValue;
    }

    private string ResolveMavenCoordinate(string mavenCoord, string librariesDirectory)
    {
        string coord = mavenCoord.Trim('[', ']');
        string[] parts = coord.Split(':');

        if (parts.Length < 3) return mavenCoord;

        string groupId = parts[0];
        string artifactId = parts[1];
        string version = parts[2];
        string classifier = parts.Length > 3 ? parts[3] : "";
        string extension = "jar";

        // 处理版本号中的@符号
        if (version.Contains('@'))
        {
            var versionParts = version.Split('@');
            version = versionParts[0];
            extension = versionParts[1];
        }

        // 处理classifier中的@符号
        if (!string.IsNullOrEmpty(classifier) && classifier.Contains('@'))
        {
            var classifierParts = classifier.Split('@');
            classifier = classifierParts[0];
            extension = classifierParts[1];
        }

        // 处理$extension占位符
        if (extension.Equals("$extension", StringComparison.OrdinalIgnoreCase))
        {
            extension = artifactId.Equals("mcp_config", StringComparison.OrdinalIgnoreCase) ? "zip" : "jar";
        }

        // 构建文件名
        string fileName = $"{artifactId}-{version}";
        if (!string.IsNullOrEmpty(classifier))
        {
            fileName += $"-{classifier}";
        }
        fileName += $".{extension}";

        // 构建完整路径
        string groupPath = groupId.Replace('.', Path.DirectorySeparatorChar);
        return Path.Combine(librariesDirectory, groupPath, artifactId, version, fileName);
    }

    private List<string> BuildClassPath(string installerToolsPath, JArray classpath, string librariesDirectory)
    {
        var fullClassPath = new List<string> { installerToolsPath };

        foreach (var classpathEntry in classpath)
        {
            string mavenCoord = classpathEntry.ToString();
            string libraryPath = _libraryManager.GetLibraryPath(mavenCoord, librariesDirectory);

            if (File.Exists(libraryPath))
            {
                fullClassPath.Add(libraryPath);
            }
            else
            {
                _logger.LogWarning("classpath文件不存在: {LibraryPath}", libraryPath);
            }
        }

        return fullClassPath;
    }

    private async Task<string> DownloadInstallerToolsAsync(string jarName, string librariesDirectory, CancellationToken cancellationToken)
    {
        string processedJarName = jarName.Replace("$extension", "jar");
        string libraryPath = _libraryManager.GetLibraryPath(processedJarName, librariesDirectory);

        if (File.Exists(libraryPath))
        {
            _logger.LogDebug("installertools已存在: {LibraryPath}", libraryPath);
            return libraryPath;
        }

        // 构建下载URL
        string[] parts = processedJarName.Split(':');
        if (parts.Length < 3)
        {
            throw new ProcessorExecutionException($"无效的jar名称格式: {processedJarName}", jarName);
        }

        string groupId = parts[0];
        string artifactId = parts[1];
        string version = parts[2];
        string classifier = parts.Length >= 4 ? parts[3] : "";

        string fileName = string.IsNullOrEmpty(classifier) 
            ? $"{artifactId}-{version}.jar" 
            : $"{artifactId}-{version}-{classifier}.jar";

        // 获取当前下载源
        var downloadSourceType = await _localSettingsService.ReadSettingAsync<string>("DownloadSource") ?? "Official";
        var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToLower());
        
        // 构建官方源URL
        string officialUrl = $"https://maven.minecraftforge.net/{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";
        
        // 如果是NeoForge相关的库，使用NeoForge Maven
        if (groupId.StartsWith("net.neoforged", StringComparison.OrdinalIgnoreCase))
        {
            officialUrl = $"https://maven.neoforged.net/releases/{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";
        }
        
        // 使用下载源获取URL
        string downloadUrl = downloadSource.GetLibraryUrl(processedJarName, officialUrl);
        
        _logger.LogInformation("使用下载源 {DownloadSource} 下载处理器库: {Url}", downloadSource.Name, downloadUrl);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 使用下载源 {downloadSource.Name} 下载处理器库: {downloadUrl}");

        var result = await _downloadManager.DownloadFileAsync(downloadUrl, libraryPath, null, null, cancellationToken);
        
        // 如果主下载源失败，尝试官方源
        if (!result.Success && downloadUrl != officialUrl)
        {
            _logger.LogWarning("主下载源失败，切换到官方源: {OfficialUrl}", officialUrl);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 主下载源失败: {downloadUrl}，正在切换到备用源: {officialUrl}");
            
            result = await _downloadManager.DownloadFileAsync(officialUrl, libraryPath, null, null, cancellationToken);
        }
        
        if (!result.Success)
        {
            throw new ProcessorExecutionException($"下载installertools失败: {result.ErrorMessage}", jarName, null, null, result.Exception);
        }

        return libraryPath;
    }

    private string GetMainClassFromJar(string jarPath)
    {
        using var archive = ZipFile.OpenRead(jarPath);
        var manifestEntry = archive.GetEntry("META-INF/MANIFEST.MF");
        if (manifestEntry == null)
        {
            throw new ProcessorExecutionException($"jar文件中未找到META-INF/MANIFEST.MF: {jarPath}", jarPath);
        }

        using var stream = manifestEntry.Open();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (line != null && line.StartsWith("Main-Class:", StringComparison.OrdinalIgnoreCase))
            {
                return line["Main-Class:".Length..].Trim();
            }
        }

        throw new ProcessorExecutionException($"jar文件的MANIFEST.MF中未找到Main-Class字段: {jarPath}", jarPath);
    }

    private async Task ExecuteJavaCommandAsync(
        string mainClass,
        List<string> classPath,
        List<string> args,
        string librariesDirectory,
        string modLoaderType,
        string processorJar,
        CancellationToken cancellationToken)
    {
        string classPathSeparator = Path.PathSeparator.ToString();
        string combinedClassPath = string.Join(classPathSeparator, classPath);

        var javaArgs = new List<string> { "-cp", combinedClassPath, mainClass };
        javaArgs.AddRange(args);

        // 查找Java可执行文件
        string javaPath = FindJavaPath();

        var processStartInfo = new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = string.Join(" ", javaArgs),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(librariesDirectory)
        };

        using var process = new Process { StartInfo = processStartInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
                _logger.LogDebug("处理器输出: {Output}", e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuilder.AppendLine(e.Data);
                _logger.LogWarning("处理器错误: {Error}", e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            string fullCommand = $"{javaPath} {string.Join(" ", javaArgs)}";
            throw new ProcessorExecutionException(
                $"Java命令执行失败，退出代码: {process.ExitCode}\n错误信息: {errorBuilder}",
                processorJar, null, process.ExitCode);
        }

        _logger.LogInformation("处理器执行完成");
    }

    private string FindJavaPath()
    {
        string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            string javaExe = Path.Combine(javaHome, "bin", "java.exe");
            if (File.Exists(javaExe))
            {
                return javaExe;
            }
        }
        return "java";
    }
}
