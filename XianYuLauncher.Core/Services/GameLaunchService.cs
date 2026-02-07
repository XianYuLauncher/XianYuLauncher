using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 游戏启动服务实现
/// </summary>
public class GameLaunchService : IGameLaunchService
{
    private readonly IJavaRuntimeService _javaRuntimeService;
    private readonly IVersionConfigService _versionConfigService;
    private readonly IFileService _fileService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IVersionInfoService _versionInfoService;
    private readonly ILaunchSettingsResolver _launchSettingsResolver;
    private readonly ILogger<GameLaunchService> _logger;
    
    private const string EnableVersionIsolationKey = "EnableVersionIsolation";
    
    // 默认窗口大小
    private int _windowWidth = 1280;
    private int _windowHeight = 720;
    
    private IAuthlibInjectorCallback? _authlibCallback;
    
    public GameLaunchService(
        IJavaRuntimeService javaRuntimeService,
        IVersionConfigService versionConfigService,
        IFileService fileService,
        ILocalSettingsService localSettingsService,
        IMinecraftVersionService minecraftVersionService,
        IVersionInfoService versionInfoService,
        ILaunchSettingsResolver launchSettingsResolver,
        ILogger<GameLaunchService> logger)
    {
        _javaRuntimeService = javaRuntimeService;
        _versionConfigService = versionConfigService;
        _fileService = fileService;
        _localSettingsService = localSettingsService;
        _minecraftVersionService = minecraftVersionService;
        _versionInfoService = versionInfoService;
        _launchSettingsResolver = launchSettingsResolver;
        _logger = logger;
    }
    
    /// <summary>
    /// 设置外置登录回调
    /// </summary>
    public void SetAuthlibInjectorCallback(IAuthlibInjectorCallback callback)
    {
        _authlibCallback = callback;
    }
    
    /// <summary>
    /// 生成启动命令（不实际启动游戏）
    /// </summary>
    public async Task<string> GenerateLaunchCommandAsync(
        string versionName,
        MinecraftProfile profile,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. 验证输入
            if (string.IsNullOrEmpty(versionName))
            {
                throw new ArgumentException("版本名称不能为空", nameof(versionName));
            }
            
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile), "角色信息不能为空");
            }
            
            // 2. 获取路径
            string minecraftPath = _fileService.GetMinecraftDataPath();
            string versionsDir = Path.Combine(minecraftPath, "versions");
            string versionDir = Path.Combine(versionsDir, versionName);
            string jarPath = Path.Combine(versionDir, $"{versionName}.jar");
            string jsonPath = Path.Combine(versionDir, $"{versionName}.json");
            string librariesPath = Path.Combine(minecraftPath, "libraries");
            string assetsPath = Path.Combine(minecraftPath, "assets");
            
            // 3. 检查版本隔离设置
            bool? versionIsolationValue = await _localSettingsService.ReadSettingAsync<bool?>(EnableVersionIsolationKey);
            bool enableVersionIsolation = versionIsolationValue ?? true;
            string gameDir = enableVersionIsolation 
                ? Path.Combine(minecraftPath, "versions", versionName) 
                : minecraftPath;
            
            // 4. 检查必要文件
            if (!File.Exists(jarPath))
            {
                throw new FileNotFoundException($"游戏JAR文件不存在: {jarPath}");
            }
            
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"游戏JSON文件不存在: {jsonPath}");
            }
            
            // 5. 读取版本信息 (使用 MinecraftVersionService 以支持继承和深度分析)
            // 原有代码: string versionJson = await File.ReadAllTextAsync(jsonPath, cancellationToken);
            // 原有代码: var versionInfo = JsonConvert.DeserializeObject<VersionInfo>(versionJson);
            var versionInfo = await _minecraftVersionService.GetVersionInfoAsync(versionName, null, allowNetwork: false).ConfigureAwait(false);
            
            if (versionInfo == null)
            {
                throw new InvalidOperationException("无法从本地文件读取版本元数据 (VersionInfo 为空)，请确保版本文件存在且完整");
            }
            // 针对旧版 Forge 处理: 如果没有 mainClass，尝试从 inheritsFrom 补全，或者报错
            if (string.IsNullOrEmpty(versionInfo.MainClass))
            {
                 // 注意：GetVersionInfoAsync 应该已经处理了继承合并。如果还是空，说明真的没有。
                 throw new InvalidOperationException("无法获取主启动类 (MainClass)，请检查版本文件完整性");
            }
            
            // 6. 加载版本配置并通过 Resolver 合并全局/版本设置
            var config = await _versionConfigService.LoadConfigAsync(versionName);
            int requiredJavaVersion = versionInfo?.JavaVersion?.MajorVersion ?? 8;
            var effectiveSettings = await _launchSettingsResolver.ResolveAsync(config, requiredJavaVersion);
            
            _windowWidth = effectiveSettings.WindowWidth;
            _windowHeight = effectiveSettings.WindowHeight;
            
            // 7. 使用解析后的 Java 路径
            string? javaPath = effectiveSettings.JavaPath;
            
            if (string.IsNullOrEmpty(javaPath))
            {
                throw new InvalidOperationException("未找到Java运行时环境，请先安装Java");
            }
            
            // 8. 构建启动参数
            var launchArgs = await BuildLaunchArgumentsAsync(
                versionInfo, profile, config, effectiveSettings, versionName, versionDir, gameDir,
                jarPath, librariesPath, assetsPath, javaPath, minecraftPath, null);
            
            // 9. 生成完整命令
            string javaExecutable = javaPath;
            if (javaPath.EndsWith("java.exe", StringComparison.OrdinalIgnoreCase))
            {
                string javawPath = javaPath.Substring(0, javaPath.Length - 8) + "javaw.exe";
                if (File.Exists(javawPath))
                {
                    javaExecutable = javawPath;
                }
            }
            
            string processedArgs = string.Join(" ", launchArgs.Select(a => 
                (a.Contains('"') || !a.Contains(' ')) ? a : $"\"{a}\""));
            
            return $"\"{javaExecutable}\" {processedArgs}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameLaunchService] 生成启动命令失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 启动游戏
    /// </summary>
    public async Task<GameLaunchResult> LaunchGameAsync(
        string versionName,
        MinecraftProfile profile,
        Action<double>? progressCallback = null,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default,
        string? overrideJavaPath = null,
        string? quickPlaySingleplayer = null,
        string? quickPlayServer = null,
        int? quickPlayPort = null)
    {
        _logger.LogInformation("=== GameLaunchService.LaunchGameAsync 开始 ===");
        _logger.LogInformation("版本名称: {VersionName}", versionName);
        _logger.LogInformation("角色名称: {ProfileName}, 类型: {ProfileType}", 
            profile?.Name ?? "null", 
            profile?.IsOffline == true ? "离线" : "在线");
        if (!string.IsNullOrEmpty(quickPlayServer))
        {
             _logger.LogInformation("快速启动服务器: {Server}:{Port}", quickPlayServer, quickPlayPort);
        }
        
        try
        {
            // 1. 验证输入
            _logger.LogInformation("步骤 1: 验证输入参数");
            if (string.IsNullOrEmpty(versionName))
            {
                _logger.LogError("版本名称为空");
                return new GameLaunchResult { Success = false, ErrorMessage = "版本名称不能为空" };
            }
            
            if (profile == null)
            {
                _logger.LogError("角色信息为空");
                return new GameLaunchResult { Success = false, ErrorMessage = "角色信息不能为空" };
            }
            
            statusCallback?.Invoke("正在准备启动...");
            
            // 2. 获取路径
            _logger.LogInformation("步骤 2: 获取游戏路径");
            string minecraftPath = _fileService.GetMinecraftDataPath();
            _logger.LogInformation("Minecraft 根目录: {MinecraftPath}", minecraftPath);
            
            string versionsDir = Path.Combine(minecraftPath, "versions");
            string versionDir = Path.Combine(versionsDir, versionName);
            string jarPath = Path.Combine(versionDir, $"{versionName}.jar");
            string jsonPath = Path.Combine(versionDir, $"{versionName}.json");
            string librariesPath = Path.Combine(minecraftPath, "libraries");
            string assetsPath = Path.Combine(minecraftPath, "assets");
            
            _logger.LogInformation("版本目录: {VersionDir}", versionDir);
            _logger.LogInformation("JAR 路径: {JarPath}", jarPath);
            _logger.LogInformation("JSON 路径: {JsonPath}", jsonPath);
            
            // 3. 检查版本隔离设置
            _logger.LogInformation("步骤 3: 检查版本隔离设置");
            bool? versionIsolationValue = await _localSettingsService.ReadSettingAsync<bool?>(EnableVersionIsolationKey);
            bool enableVersionIsolation = versionIsolationValue ?? true;
            _logger.LogInformation("版本隔离: {EnableVersionIsolation}", enableVersionIsolation);
            
            string gameDir = enableVersionIsolation 
                ? Path.Combine(minecraftPath, "versions", versionName) 
                : minecraftPath;
            _logger.LogInformation("游戏目录: {GameDir}", gameDir);
            
            // 4. 确保目录存在
            _logger.LogInformation("步骤 4: 确保目录存在");
            if (enableVersionIsolation && !Directory.Exists(gameDir))
            {
                _logger.LogInformation("创建游戏目录: {GameDir}", gameDir);
                Directory.CreateDirectory(gameDir);
            }
            
            // 5. 创建 options.txt（设置默认语言为简体中文）
            _logger.LogInformation("步骤 5: 检查 options.txt");
            string optionsPath = Path.Combine(gameDir, "options.txt");
            if (!File.Exists(optionsPath))
            {
                try
                {
                    await File.WriteAllTextAsync(optionsPath, "lang:zh_cn\n", cancellationToken);
                    _logger.LogInformation("已创建默认游戏设置文件: {OptionsPath}", optionsPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "创建 options.txt 失败");
                }
            }
            
            // 6. 检查必要文件
            _logger.LogInformation("步骤 6: 检查必要文件");
            if (!File.Exists(jarPath))
            {
                _logger.LogError("游戏 JAR 文件不存在: {JarPath}", jarPath);
                return new GameLaunchResult { Success = false, ErrorMessage = $"游戏JAR文件不存在: {jarPath}" };
            }
            _logger.LogInformation("JAR 文件存在");
            
            if (!File.Exists(jsonPath))
            {
                _logger.LogError("游戏 JSON 文件不存在: {JsonPath}", jsonPath);
                return new GameLaunchResult { Success = false, ErrorMessage = $"游戏JSON文件不存在: {jsonPath}" };
            }
            _logger.LogInformation("JSON 文件存在");
            
            // 7. 读取版本信息
            _logger.LogInformation("步骤 7: 读取版本信息");
            statusCallback?.Invoke("正在读取版本信息...");
            string versionJson = await File.ReadAllTextAsync(jsonPath, cancellationToken);
            var versionInfo = JsonConvert.DeserializeObject<VersionInfo>(versionJson);
            
            if (versionInfo == null)
            {
                _logger.LogError("解析版本信息失败");
                return new GameLaunchResult { Success = false, ErrorMessage = "解析版本信息失败" };
            }
            _logger.LogInformation("版本信息解析成功，主类: {MainClass}", versionInfo.MainClass);
            
            if (string.IsNullOrEmpty(versionInfo.MainClass))
            {
                _logger.LogError("主类信息为空");
                return new GameLaunchResult { Success = false, ErrorMessage = "无法获取主类信息" };
            }
            
            // 8. 加载版本配置并通过 Resolver 合并全局/版本设置
            _logger.LogInformation("步骤 8: 加载版本配置");
            var config = await _versionConfigService.LoadConfigAsync(versionName);
            int requiredJavaVersion = versionInfo?.JavaVersion?.MajorVersion ?? 8;
            _logger.LogInformation("需要 Java 版本: {RequiredVersion}", requiredJavaVersion);
            
            var effectiveSettings = await _launchSettingsResolver.ResolveAsync(config, requiredJavaVersion);
            _windowWidth = effectiveSettings.WindowWidth;
            _windowHeight = effectiveSettings.WindowHeight;
            _logger.LogInformation("版本配置加载完成，窗口大小: {Width}x{Height}", _windowWidth, _windowHeight);
            
            // 9. 选择 Java 运行时（优先使用临时覆盖路径）
            _logger.LogInformation("步骤 9: 选择 Java 运行时");
            statusCallback?.Invoke("正在查找Java运行时环境...");
            
            string? javaPath;
            if (!string.IsNullOrEmpty(overrideJavaPath) && File.Exists(overrideJavaPath))
            {
                _logger.LogInformation("使用临时 Java 覆盖路径: {JavaPath}", overrideJavaPath);
                javaPath = overrideJavaPath;
            }
            else
            {
                javaPath = effectiveSettings.JavaPath;
                _logger.LogInformation("使用解析后的 Java 路径: {JavaPath}", javaPath);
            }
            
            if (string.IsNullOrEmpty(javaPath))
            {
                _logger.LogError("未找到 Java 运行时环境");
                return new GameLaunchResult { Success = false, ErrorMessage = "未找到Java运行时环境，请先安装Java" };
            }
            _logger.LogInformation("选中 Java 路径: {JavaPath}", javaPath);
            
            // 10. 确保版本依赖完整
            _logger.LogInformation("步骤 10: 确保版本依赖完整");
            statusCallback?.Invoke("正在检查版本依赖...");
            
            try
            {
                await _minecraftVersionService.EnsureVersionDependenciesAsync(
                    versionName, 
                    minecraftPath, 
                    progress =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        progressCallback?.Invoke(progress);
                        statusCallback?.Invoke($"正在准备游戏文件... {progress:F1}%");
                    },
                    currentHash =>
                    {
                        // 传递当前下载的资源文件 hash 信息
                        statusCallback?.Invoke($"正在准备游戏文件... {currentHash}");
                    });
                _logger.LogInformation("版本依赖检查完成");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("用户取消了下载");
                return new GameLaunchResult { Success = false, ErrorMessage = "已取消下载" };
            }
            
            // 11. 构建启动参数
            _logger.LogInformation("步骤 11: 构建启动参数");
            statusCallback?.Invoke("正在构建启动参数...");
            var launchArgs = await BuildLaunchArgumentsAsync(
                versionInfo, profile, config, effectiveSettings, versionName, versionDir, gameDir,
                jarPath, librariesPath, assetsPath, javaPath, minecraftPath, quickPlaySingleplayer, quickPlayServer, quickPlayPort);
            _logger.LogInformation("启动参数构建完成，共 {Count} 个参数", launchArgs.Count);
            
            // 12. 创建进程
            _logger.LogInformation("步骤 12: 创建游戏进程");
            string javaExecutable = javaPath;
            if (javaPath.EndsWith("java.exe", StringComparison.OrdinalIgnoreCase))
            {
                string javawPath = javaPath.Substring(0, javaPath.Length - 8) + "javaw.exe";
                if (File.Exists(javawPath))
                {
                    _logger.LogInformation("使用 javaw.exe 替代 java.exe");
                    javaExecutable = javawPath;
                }
            }
            
            string processedArgs = string.Join(" ", launchArgs.Select(a => 
                (a.Contains('"') || !a.Contains(' ')) ? a : $"\"{a}\""));
            string fullCommand = $"\"{javaExecutable}\" {processedArgs}";
            
            _logger.LogInformation("完整启动命令长度: {Length} 字符", fullCommand.Length);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = javaExecutable,
                Arguments = processedArgs,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = versionDir,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            
            var gameProcess = new Process { StartInfo = startInfo };
            
            // 13. 启动进程
            _logger.LogInformation("步骤 13: 启动游戏进程");
            statusCallback?.Invoke("正在启动游戏...");
            gameProcess.Start();
            gameProcess.EnableRaisingEvents = true;
            
            _logger.LogInformation("游戏进程启动成功，PID: {ProcessId}", gameProcess.Id);
            _logger.LogInformation("=== GameLaunchService.LaunchGameAsync 成功完成 ===");
            
            return new GameLaunchResult
            {
                Success = true,
                GameProcess = gameProcess,
                LaunchCommand = fullCommand,
                UsedJavaPath = javaPath
            };
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "启动已取消");
            return new GameLaunchResult { Success = false, ErrorMessage = "启动已取消" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动失败: {Message}", ex.Message);
            _logger.LogError("异常类型: {ExceptionType}", ex.GetType().FullName);
            _logger.LogError("堆栈跟踪: {StackTrace}", ex.StackTrace);
            
            if (ex.InnerException != null)
            {
                _logger.LogError("内部异常: {InnerMessage}", ex.InnerException.Message);
            }
            
            return new GameLaunchResult { Success = false, ErrorMessage = $"启动失败: {ex.Message}" };
        }
    }

    /// <summary>
    /// 构建启动参数
    /// </summary>
    private async Task<List<string>> BuildLaunchArgumentsAsync(
        VersionInfo versionInfo,
        MinecraftProfile profile,
        VersionConfig config,
        EffectiveLaunchSettings effectiveSettings,
        string versionName,
        string versionDir,
        string gameDir,
        string jarPath,
        string librariesPath,
        string assetsPath,
        string javaPath,
        string minecraftPath,
        string? quickPlaySingleplayer = null,
        string? quickPlayServer = null,
        int? quickPlayPort = null)
    {
        var args = new List<string>();

        int currentJavaMajorVersion = 8;
        var javaVersionInfo = await _javaRuntimeService.GetJavaVersionInfoAsync(javaPath);
        if (javaVersionInfo != null)
        {
            currentJavaMajorVersion = javaVersionInfo.MajorVersion;
        }
        
        // Java 9+ 编码参数
        if (currentJavaMajorVersion > 8)
        {
            args.Add("-Dstderr.encoding=UTF-8");
            args.Add("-Dstdout.encoding=UTF-8");
        }
        
        // 基础 JVM 参数
        args.Add("-XX:+UseG1GC");
        args.Add("-XX:-UseAdaptiveSizePolicy");
        args.Add("-XX:-OmitStackTraceInFastThrow");
        args.Add("-Djdk.lang.Process.allowAmbiguousCommands=true");
        args.Add("-Dlog4j2.formatMsgNoLookups=true");
        args.Add("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump");
        
        // 内存参数（使用合并后的 EffectiveLaunchSettings）
        if (!effectiveSettings.AutoMemoryAllocation)
        {
            args.Add(GetHeapParam("-Xms", effectiveSettings.InitialHeapMemory));
            args.Add(GetHeapParam("-Xmx", effectiveSettings.MaximumHeapMemory));
        }
        
        // 构建 Classpath
        string classpath = await BuildClasspathAsync(versionInfo, versionName, jarPath, librariesPath, minecraftPath);
        
        // 处理 JVM 参数（带状态机支持 -p 参数）
        bool hasClasspath = false;
        bool isNextArgPValue = false; // 状态变量：标记下一个参数是否是-p的值
        
        if (versionInfo.Arguments?.Jvm != null)
        {
            foreach (var jvmArg in versionInfo.Arguments.Jvm)
            {
                if (jvmArg is string argStr)
                {
                    string processedArg = ProcessJvmArgument(argStr, versionName, versionDir, librariesPath, classpath, ref isNextArgPValue);
                    args.Add(processedArg);
                    
                    if (processedArg.Contains("-cp") || processedArg.Contains("-classpath"))
                    {
                        hasClasspath = true;
                    }
                }
            }
        }
        
        // 确保添加 classpath
        if (!hasClasspath)
        {
            args.Add($"-cp \"{classpath}\"");
            args.Add($"-Djava.library.path={Path.Combine(versionDir, $"{versionName}-natives")}");
            args.Add("-Dminecraft.launcher.brand=XianYuLauncher");
            args.Add("-Dminecraft.launcher.version=1.0");
        }
        
        // 外置登录 authlib-injector 支持
        bool isExternalLogin = profile != null && !string.IsNullOrEmpty(profile.AuthServer) && profile.TokenType == "external";
        if (isExternalLogin && _authlibCallback != null)
        {
            System.Diagnostics.Debug.WriteLine("[GameLaunchService] 检测到外置登录角色，添加authlib-injector参数");
            var externalJvmArgs = await _authlibCallback.GetJvmArgumentsAsync(profile.AuthServer);
            args.InsertRange(0, externalJvmArgs);
        }
        
        // 确定 userType
        string userType = isExternalLogin ? "mojang" : (profile.IsOffline ? "offline" : "msa");
        
        // 添加主类
        args.Add(versionInfo.MainClass);
        
        // 处理游戏参数
        await AddGameArgumentsAsync(args, versionInfo, profile, config, versionName, gameDir, assetsPath, userType, quickPlaySingleplayer, quickPlayServer, quickPlayPort);
        
        // 分辨率参数
        args.Add("--width");
        args.Add(_windowWidth.ToString());
        args.Add("--height");
        args.Add(_windowHeight.ToString());
        
        return args;
    }

    /// <summary>
    /// 添加游戏参数
    /// </summary>
    private async Task AddGameArgumentsAsync(
        List<string> args,
        VersionInfo versionInfo,
        MinecraftProfile profile,
        VersionConfig config,
        string versionName,
        string gameDir,
        string assetsPath,
        string userType,
        string? quickPlaySingleplayer = null,
        string? quickPlayServer = null,
        int? quickPlayPort = null)
    {
        await Task.CompletedTask;
        
        string assetIndex = versionInfo.AssetIndex?.Id ?? versionName;
        
        // 先添加基本游戏参数（与 LaunchViewModel 一致）
        args.Add("--version");
        args.Add(versionName);
        args.Add("--gameDir");
        args.Add(gameDir);
        args.Add("--assetsDir");
        args.Add(assetsPath);
        args.Add("--assetIndex");
        args.Add(assetIndex);
        args.Add("--username");
        args.Add(profile.Name);
        args.Add("--uuid");
        args.Add(profile.Id);
        args.Add("--accessToken");
        args.Add(string.IsNullOrEmpty(profile.AccessToken) ? "0" : profile.AccessToken);
        args.Add("--userType");
        args.Add(userType);
        args.Add("--versionType");
        args.Add("XianYuLauncher");

        // 获取真实的原版版本号 (从Config或InheritsFrom)
        string realVersion = versionName;
        if (config != null && !string.IsNullOrEmpty(config.MinecraftVersion))
        {
            realVersion = config.MinecraftVersion;
        }
        else if (!string.IsNullOrEmpty(versionInfo.InheritsFrom))
        {
            realVersion = versionInfo.InheritsFrom;
        }
        else
        {
            var extracted = _versionInfoService.ExtractVersionConfigFromName(versionName);
            if (extracted != null && !string.IsNullOrEmpty(extracted.MinecraftVersion))
            {
                realVersion = extracted.MinecraftVersion;
            }
        }
        
        // 直连服务器支持
        if (!string.IsNullOrEmpty(quickPlayServer))
        {
            if (IsSupportQuickPlay(realVersion))
            {
                // 1.20+ 使用 quickPlayMultiplayer
                 _logger.LogInformation("添加快速启动多人游戏参数: --quickPlayMultiplayer ***** (BaseVersion: {Version})", realVersion);
                 string target = quickPlayServer;
                 if (quickPlayPort.HasValue && quickPlayPort.Value != 25565)
                 {
                     target = $"{quickPlayServer}:{quickPlayPort.Value}";
                 }
                 args.Add("--quickPlayMultiplayer");
                 args.Add(target);
            }
            else
            {
                // 旧版本降级使用 --server
                args.Add("--server");
                args.Add(quickPlayServer);
                args.Add("--port");
                args.Add(quickPlayPort.HasValue ? quickPlayPort.Value.ToString() : "25565");
            }
        }
        
        // 快速启动存档支持 (1.20+)
        if (!string.IsNullOrEmpty(quickPlaySingleplayer) && IsSupportQuickPlay(realVersion))
        {
            _logger.LogInformation("添加快速启动参数: --quickPlaySingleplayer ***** (BaseVersion: {Version})", realVersion);
            args.Add("--quickPlaySingleplayer");
            args.Add(quickPlaySingleplayer);
        }

        // 1.9 以下版本需要 userProperties
        if (IsVersionBelow1_9(versionName))
        {
            args.Add("--userProperties");
            args.Add("{}");
        }
        
        // 早期版本需要 AlphaVanillaTweaker
        if (NeedsAlphaVanillaTweaker(versionName))
        {
            args.Add("--tweakClass");
            args.Add("net.minecraft.launchwrapper.AlphaVanillaTweaker");
        }
        
        // 检查是否有 minecraftArguments（旧版 Forge 格式）
        if (!string.IsNullOrEmpty(versionInfo.MinecraftArguments))
        {
            // 使用 minecraftArguments 构建额外游戏参数
            string minecraftArgs = versionInfo.MinecraftArguments
                .Replace("${auth_player_name}", profile.Name)
                .Replace("${version_name}", versionName)
                .Replace("${game_directory}", gameDir)
                .Replace("${assets_root}", assetsPath)
                .Replace("${assets_index_name}", assetIndex)
                .Replace("${auth_uuid}", profile.Id)
                .Replace("${auth_access_token}", string.IsNullOrEmpty(profile.AccessToken) ? "0" : profile.AccessToken)
                .Replace("${auth_session}", string.IsNullOrEmpty(profile.AccessToken) ? "0" : profile.AccessToken)
                .Replace("${user_type}", userType)
                .Replace("${user_properties}", "{}")
                .Replace("${version_type}", "XianYuLauncher")
                .Replace("${auth_xuid}", "")
                .Replace("${clientid}", "0");
            
            // 解析参数
            var parsedArgs = ParseArguments(minecraftArgs);
            
            // 收集已经添加的参数键
            var addedArgKeys = new HashSet<string>();
            foreach (var arg in args)
            {
                if (arg.StartsWith("--"))
                {
                    addedArgKeys.Add(arg);
                }
            }
            
            // 添加解析后的参数，跳过已经存在的参数
            for (int i = 0; i < parsedArgs.Count; i++)
            {
                string arg = parsedArgs[i];
                
                // 跳过 --versionType，由启动器统一设置
                if (arg == "--versionType")
                {
                    i++;
                    continue;
                }
                
                // 跳过已添加的参数
                if (arg.StartsWith("--") && addedArgKeys.Contains(arg))
                {
                    i++;
                    continue;
                }
                
                args.Add(arg);
                
                if (i + 1 < parsedArgs.Count && !parsedArgs[i + 1].StartsWith("--"))
                {
                    i++;
                    args.Add(parsedArgs[i]);
                }
            }
        }
        // 处理 Arguments.Game（NeoForge 等 ModLoader）
        else if (versionInfo.Arguments?.Game != null)
        {
            var gameArgs = versionInfo.Arguments.Game;
            for (int i = 0; i < gameArgs.Count; i++)
            {
                var gameArg = gameArgs[i];
                if (gameArg is string argStr)
                {
                    // 检查是否为已手动添加的基本参数
                    if (IsBasicGameArgument(argStr))
                    {
                        // 如果是基本参数的Key，且下一个元素也是字符串且不是参数Key，则认为是Value，一并跳过
                        // 注意：IsBasicGameArgument 中列出的所有参数(--version, --username等)实际上都是必须带值的
                        // 所以这里跳过下一个元素是安全的逻辑
                        if (i + 1 < gameArgs.Count && gameArgs[i + 1] is string nextArg && !nextArg.StartsWith("--"))
                        {
                            i++; // 跳过下一个元素（Value）
                        }
                        continue; // 跳过当前元素（Key）
                    }
                    
                    string processedArg = argStr
                        .Replace("${auth_player_name}", profile.Name)
                        .Replace("${version_name}", versionName)
                        .Replace("${game_directory}", gameDir)
                        .Replace("${assets_root}", assetsPath)
                        .Replace("${assets_index_name}", assetIndex)
                        .Replace("${auth_uuid}", profile.Id)
                        .Replace("${auth_access_token}", string.IsNullOrEmpty(profile.AccessToken) ? "0" : profile.AccessToken)
                        .Replace("${auth_xuid}", "")
                        .Replace("${clientid}", "0")
                        .Replace("${version_type}", versionInfo.Type ?? "release");
                    
                    args.Add(processedArg);
                }
            }
        }
    }
    
    /// <summary>
    /// 检查是否为基本游戏参数
    /// </summary>
    private bool IsBasicGameArgument(string arg)
    {
        return arg.StartsWith("--version") || 
               arg.StartsWith("--gameDir") || 
               arg.StartsWith("--assetsDir") || 
               arg.StartsWith("--assetIndex") || 
               arg.StartsWith("--username") || 
               arg.StartsWith("--uuid") || 
               arg.StartsWith("--accessToken") || 
               arg.StartsWith("--userType") || 
               arg == "--userProperties" || 
               arg == "{}";
    }

    /// <summary>
    /// 构建 Classpath
    /// </summary>
    private async Task<string> BuildClasspathAsync(
        VersionInfo versionInfo,
        string versionName,
        string jarPath,
        string librariesPath,
        string minecraftPath)
    {
        await Task.CompletedTask;
        
        var classpathEntries = new HashSet<string>();
        classpathEntries.Add(jarPath);
        
        if (versionInfo.Libraries == null)
        {
            return string.Join(";", classpathEntries);
        }
        
        // 判断是否为 Fabric 版本
        bool isFabricVersion = await IsFabricVersionAsync(versionName, versionInfo, minecraftPath);
        
        // 收集 ASM 库版本（用于 Fabric 版本冲突处理）
        var asmLibraryVersions = new Dictionary<string, string>();
        if (isFabricVersion)
        {
            foreach (var library in versionInfo.Libraries)
            {
                if (library.Name.StartsWith("org.ow2.asm:asm:"))
                {
                    string[] parts = library.Name.Split(':');
                    if (parts.Length >= 3)
                    {
                        asmLibraryVersions[library.Name] = parts[2];
                    }
                }
            }
        }
        
        // 找出最新的 ASM 版本
        string latestAsmVersion = "0.0";
        if (isFabricVersion && asmLibraryVersions.Count > 0)
        {
            foreach (var kvp in asmLibraryVersions)
            {
                if (string.Compare(kvp.Value, latestAsmVersion, StringComparison.Ordinal) > 0)
                {
                    latestAsmVersion = kvp.Value;
                }
            }
            System.Diagnostics.Debug.WriteLine($"[GameLaunchService] 检测到Fabric版本，最新ASM版本: {latestAsmVersion}");
        }
        
        // 添加库到 classpath
        foreach (var library in versionInfo.Libraries)
        {
            // 检查规则
            bool isAllowed = true;
            if (library.Rules != null)
            {
                isAllowed = library.Rules.Any(r => r.Action == "allow" && (r.Os == null || r.Os.Name == "windows"));
                if (isAllowed && library.Rules.Any(r => r.Action == "disallow" && (r.Os == null || r.Os.Name == "windows")))
                {
                    isAllowed = false;
                }
            }
            
            if (!isAllowed) continue;
            
            // Fabric ASM 版本冲突处理：跳过旧版 ASM 库
            if (isFabricVersion && library.Name.StartsWith("org.ow2.asm:asm:"))
            {
                string[] parts = library.Name.Split(':');
                if (parts.Length >= 3 && parts[2] != latestAsmVersion)
                {
                    System.Diagnostics.Debug.WriteLine($"[GameLaunchService] 跳过旧版ASM库: {library.Name}");
                    continue;
                }
            }
            
            // 跳过原生库
            bool hasClassifier = library.Name.Count(c => c == ':') > 2;
            bool isNativeLibrary = hasClassifier && library.Name.Contains("natives-", StringComparison.OrdinalIgnoreCase);
            if (isNativeLibrary) continue;
            
            // 跳过 neoforge-universal 和 installertools
            if (library.Name.Contains("neoforge", StringComparison.OrdinalIgnoreCase) && 
                (library.Name.Contains("universal", StringComparison.OrdinalIgnoreCase) || 
                 library.Name.Contains("installertools", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            
            string? classifier = hasClassifier ? library.Name.Split(':')[3] : null;
            string libPath = GetLibraryFilePath(library.Name, librariesPath, classifier);
            
            if (File.Exists(libPath))
            {
                classpathEntries.Add(libPath);
            }
            else
            {
                // 尝试不带 classifier 的文件名
                string libPathWithoutClassifier = GetLibraryFilePath(library.Name, librariesPath, null);
                if (File.Exists(libPathWithoutClassifier))
                {
                    classpathEntries.Add(libPathWithoutClassifier);
                }
            }
        }
        
        return string.Join(";", classpathEntries);
    }
    
    /// <summary>
    /// 判断是否为 Fabric 版本
    /// </summary>
    private async Task<bool> IsFabricVersionAsync(string versionName, VersionInfo versionInfo, string minecraftPath)
    {
        // 1. 优先使用统一版本信息服务判断
        try
        {
            string versionDirectory = Path.Combine(minecraftPath, "versions", versionName);
            var versionConfig = await _versionInfoService.GetFullVersionInfoAsync(versionName, versionDirectory);
            
            if (versionConfig != null && !string.IsNullOrEmpty(versionConfig.ModLoaderType))
            {
                return versionConfig.ModLoaderType.Equals("fabric", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameLaunchService] 使用统一版本信息服务判断Fabric版本失败: {ex.Message}");
        }
        
        // 2. 回退到旧的判断逻辑
        return versionName.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase) || 
               (versionName.IndexOf("-fabric", StringComparison.OrdinalIgnoreCase) >= 0 && 
                !versionName.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase) && 
                versionInfo.Libraries != null && 
                versionInfo.Libraries.Any(l => l.Name.StartsWith("net.fabricmc:fabric-loader:")));
    }

    /// <summary>
    /// 处理 JVM 参数（带状态机支持 -p 参数）
    /// </summary>
    private string ProcessJvmArgument(string argStr, string versionName, string versionDir, string librariesPath, string classpath, ref bool isNextArgPValue)
    {
        // 替换占位符
        string processedArg = argStr
            .Replace("${natives_directory}", Path.Combine(versionDir, $"{versionName}-natives"))
            .Replace("${launcher_name}", "XianYuLauncher")
            .Replace("${launcher_version}", "1.0")
            .Replace("${classpath}", classpath)
            .Replace("${classpath_separator}", ";")
            .Replace("${version_name}", versionName);
        
        // 检查是否是-p参数的标记
        if (processedArg == "-p")
        {
            // 这是-p参数标记，下一个参数是路径值
            isNextArgPValue = true;
            return processedArg; // 直接返回-p参数
        }
        
        // 检查是否是-p参数的值
        if (isNextArgPValue)
        {
            // 这是-p参数的值，如"${library_directory}/path/to/file.jar;..."
            // 替换${library_directory}为实际路径
            processedArg = processedArg.Replace("${library_directory}", librariesPath);
            
            // 替换所有/为反斜杠
            processedArg = processedArg.Replace("/", Path.DirectorySeparatorChar.ToString());
            
            // 移除末尾的空格
            processedArg = processedArg.Trim();
            
            // 重置状态变量
            isNextArgPValue = false;
            return processedArg;
        }
        
        // 处理-D参数（如-DlibraryDirectory）
        if (processedArg.StartsWith("-D"))
        {
            // 检查是否包含=${library_directory}
            if (processedArg.Contains("=${library_directory}"))
            {
                // 替换${library_directory}为实际路径
                processedArg = processedArg.Replace("${library_directory}", librariesPath);
            }
        }
        // 处理其他包含${library_directory}的参数
        else if (processedArg.Contains("${library_directory}"))
        {
            // 替换${library_directory}为实际路径
            processedArg = processedArg.Replace("${library_directory}", librariesPath);
            
            // 只在路径中替换/为\，不在JVM模块参数中替换
            if (processedArg.Contains(".jar") || processedArg.Contains(".zip"))
            {
                processedArg = processedArg.Replace("/", Path.DirectorySeparatorChar.ToString());
            }
        }
        else
        {
            // 其他参数，替换${library_directory}但不替换/为\
            processedArg = processedArg.Replace("${library_directory}", librariesPath);
        }
        
        return processedArg;
    }
    
    /// <summary>
    /// 获取堆内存参数
    /// </summary>
    private string GetHeapParam(string prefix, double memoryGB)
    {
        if (memoryGB >= 1.0 && memoryGB == Math.Floor(memoryGB))
        {
            return $"{prefix}{(int)memoryGB}G";
        }
        else
        {
            return $"{prefix}{(int)(memoryGB * 1024)}M";
        }
    }
    
    /// <summary>
    /// 获取库文件路径（完整实现）
    /// </summary>
    private string GetLibraryFilePath(string libraryName, string librariesDirectory, string? classifier)
    {
        var parts = libraryName.Split(':');
        if (parts.Length < 3)
        {
            return string.Empty;
        }

        string groupId = parts[0];
        string artifactId = parts[1];
        string version = parts[2];
        string? detectedClassifier = null;
        string? detectedExtension = null;
        
        // 检查版本号是否包含@符号（extension信息）
        if (version.Contains('@'))
        {
            string[] versionParts = version.Split('@');
            if (versionParts.Length == 2)
            {
                version = versionParts[0];
                detectedExtension = versionParts[1];
            }
        }

        // 如果库名称中包含分类器（4个或更多部分）
        if (parts.Length >= 4)
        {
            detectedClassifier = parts[3];
        }

        // 优先使用方法参数传入的分类器
        string? finalClassifier = !string.IsNullOrEmpty(classifier) ? classifier : detectedClassifier;

        // 处理分类器中的特殊字符
        if (!string.IsNullOrEmpty(finalClassifier))
        {
            finalClassifier = finalClassifier.Replace('@', '.');
            if (finalClassifier.Equals("$extension", StringComparison.OrdinalIgnoreCase))
            {
                finalClassifier = "zip";
            }
        }

        // 将 groupId 中的点替换为目录分隔符
        string groupPath = groupId.Replace('.', Path.DirectorySeparatorChar);

        // 构建文件名
        string fileName = $"{artifactId}-{version}";
        if (!string.IsNullOrEmpty(finalClassifier))
        {
            fileName += $"-{finalClassifier}";
        }
        
        // 确定文件扩展名
        string extension = ".jar";
        
        // 特殊处理 neoform 文件
        if (artifactId.Equals("neoform", StringComparison.OrdinalIgnoreCase))
        {
            extension = detectedExtension != null ? "." + detectedExtension : ".zip";
        }
        // 特殊处理 mcp_config 文件
        else if (artifactId.Equals("mcp_config", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".zip";
        }
        // 如果从版本号中提取到了 extension
        else if (detectedExtension != null)
        {
            extension = "." + detectedExtension;
        }
        
        // 只有当文件名不包含已知扩展名时才添加
        var knownExtensions = new[] { ".jar", ".zip", ".lzma", ".tsrg" };
        bool hasKnownExtension = knownExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        
        if (!hasKnownExtension)
        {
            fileName += extension;
        }

        return Path.Combine(librariesDirectory, groupPath, artifactId, version, fileName);
    }
    
    /// <summary>
    /// 解析参数字符串
    /// </summary>
    private List<string> ParseArguments(string args)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        
        foreach (char c in args)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }
        
        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }
        
        return result;
    }
    
    private bool IsSupportQuickPlay(string versionName)
    {
        if (string.IsNullOrEmpty(versionName)) return false;
        // 简单正则匹配 1.20 及以上版本
        // 匹配格式: 1.20, 1.20.1, 1.21, 1.21.4 等
        var match = System.Text.RegularExpressions.Regex.Match(versionName, @"^1\.(\d+)(?:\.(\d+))?");
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int minor))
            {
                 return minor >= 20;
            }
        }
        return false;
    }

    /// <summary>
    /// 检查版本是否低于 1.9（完整实现）
    /// </summary>
    private bool IsVersionBelow1_9(string versionName)
    {
        if (string.IsNullOrEmpty(versionName)) return false;
        
        // 简单判断：检查版本号是否以1.0到1.8开头
        if (versionName.StartsWith("1.0") || versionName.StartsWith("1.1") || versionName.StartsWith("1.2") ||
            versionName.StartsWith("1.3") || versionName.StartsWith("1.4") || versionName.StartsWith("1.5") ||
            versionName.StartsWith("1.6") || versionName.StartsWith("1.7") || versionName.StartsWith("1.8"))
        {
            return true;
        }
        
        // 对于其他可能的版本格式，使用Version类进行比较
        try
        {
            // 处理"1.8.9"这样的格式
            string versionStr = versionName;
            if (versionStr.Contains("-")) // 处理带有后缀的版本，如"1.8.9-forge1.8.9-11.15.1.2318-1.8.9"
            {
                versionStr = versionStr.Split('-')[0];
            }
            
            Version version = new Version(versionStr);
            Version version1_9 = new Version("1.9");
            return version < version1_9;
        }
        catch (Exception)
        {
            // 如果版本号格式无法解析，默认返回false
            return false;
        }
    }
    
    /// <summary>
    /// 检查是否需要 AlphaVanillaTweaker（完整实现）
    /// </summary>
    private bool NeedsAlphaVanillaTweaker(string versionName)
    {
        if (string.IsNullOrEmpty(versionName)) return false;
        
        // 需要添加--tweakClass参数的特定版本列表
        string[] versionsNeedingTweaker = {
            "c0.0.11a",
            "c0.0.13a_03",
            "c0.0.13a",
            "c0.30.01c",
            "inf-20100618",
            "a1.0.4",
            "a1.0.5_01"
        };
        
        // 检查当前版本是否在需要添加参数的列表中
        return versionsNeedingTweaker.Any(v => versionName.StartsWith(v));
    }
}
