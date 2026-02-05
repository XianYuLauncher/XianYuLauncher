using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.VersionAnalysis;
using XianYuLauncher.Core.VersionAnalysis.Models;

namespace XianYuLauncher.Core.Services; // File-scoped namespace

/// <summary>
/// 版本信息服务 - 新架构实现
/// </summary>
public class VersionInfoService : IVersionInfoService
{
    private readonly ILogger _logger;
    private readonly JarAnalyzer _jarAnalyzer;
    private readonly ModLoaderDetector _modLoaderDetector;

    public VersionInfoService(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<VersionInfoService>();
        _jarAnalyzer = new JarAnalyzer(_logger);
        _modLoaderDetector = new ModLoaderDetector(_logger);
    }

    /// <summary>
    /// 标准实现：获取完整版本信息
    /// </summary>
    public async Task<VersionConfig> GetFullVersionInfoAsync(string versionId, string versionDirectory)
    {
        if (string.IsNullOrWhiteSpace(versionId))
        {
            throw new ArgumentException("versionId cannot be null, empty or whitespace.", nameof(versionId));
        }
        if (string.IsNullOrWhiteSpace(versionDirectory))
        {
            throw new ArgumentException("versionDirectory cannot be null, empty or whitespace.", nameof(versionDirectory));
        }

        var result = new VersionConfig
        {
            CreatedAt = DateTime.Now
        };

        _logger.LogInformation($"[VersionInfoService] 开始深入分析版本: {versionId}");

        // Step 1: 读取 .json (Manifest)
        // 这是分析的基础，无论是 Loader 还是继承关系都在这里
        string jsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
        MinecraftVersionManifest? manifest = null;

        if (File.Exists(jsonPath))
        {
            try
            {
                string jsonContent = await File.ReadAllTextAsync(jsonPath);
                manifest = JsonConvert.DeserializeObject<MinecraftVersionManifest>(jsonContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VersionInfoService] JSON 解析失败: {Message}", ex.Message);
            }
        }

        // Step 2: 确定 Minecraft 核心版本
        string? mcVersion = null;

        if (manifest != null)
        {
            // 1. 继承版本 (Isolation / Official Style)
            // 如果 JSON 声明了 inheritsFrom (如 "1.20.1")，则它就是该版本的一个变体。
            // 此时无需耗费 I/O 去寻找并读取父级 Jar，直接信任配置中的继承源即可。
            // (这也避免了在隔离模式下，当前目录没有 Jar 导致读取失败的问题)
            if (!string.IsNullOrEmpty(manifest.InheritsFrom))
            {
                _logger.LogInformation($"[VersionInfoService] 继承模式: 直接采纳 inheritsFrom = {manifest.InheritsFrom}");
                mcVersion = manifest.InheritsFrom;
            }
            // 2. 独立/合并版本 (Vanilla / Merged Style)
            // 没有 inheritsFrom，说明它是独立的，或者是一个包含了 Jar 的整合版。
            // 直接尝试读取当前目录下的同名 Jar (id.jar) 内的 version.json。
            else
            {
                _logger.LogInformation($"[VersionInfoService] 独立模式: 尝试读取本地 Jar ({versionId}.jar)");
                mcVersion = await _jarAnalyzer.GetMinecraftVersionFromJarAsync(versionDirectory, versionId);
            }
        }
        
        // 如果上述方法都失败了（比如没有下载 JAR），尝试从 JSON ID 猜测（不推荐但也得有）
        if (string.IsNullOrEmpty(mcVersion) && manifest != null && string.IsNullOrEmpty(manifest.InheritsFrom))
        {
            // 对于 Vanilla JSON，ID 通常就是版本号
            mcVersion = manifest.Id ?? "Unknown";
            _logger.LogWarning($"[VersionInfoService] 无法通过常规手段确定版本，回退使用 JSON ID: {mcVersion}");
        }

        result.MinecraftVersion = mcVersion ?? "Unknown";
        _logger.LogInformation($"[VersionInfoService] 最终判定 MC 版本: {result.MinecraftVersion}");


        // Step 3: 确定 ModLoader 类型和版本
        var (loaderType, loaderVer) = _modLoaderDetector.Detect(manifest);
        result.ModLoaderType = loaderType;
        result.ModLoaderVersion = loaderVer;
        _logger.LogInformation($"[VersionInfoService] 最终判定 Loader: {loaderType} {loaderVer}");


        // Step 4: 迁移/读取旧配置 (Configuration)
        // 这里我们保留之前的逻辑，读取 XianYuL.cfg 或迁移 PCL2 配置
        // 但是！！我们只读取里面的 "配置项" (Config)，而忽略它里面可能错误的 "身份项" (Identity)
        // 除非我们的身份识别完全失败（Unknown），才回退到旧配置读取。

        var legacyConfig = await GetLegacyConfigAsync(versionDirectory);
        if (legacyConfig != null)
        {
            // 迁移用户偏好设置
            result.AutoMemoryAllocation = legacyConfig.AutoMemoryAllocation;
            result.InitialHeapMemory = legacyConfig.InitialHeapMemory;
            result.MaximumHeapMemory = legacyConfig.MaximumHeapMemory;
            result.JavaPath = legacyConfig.JavaPath;
            result.UseGlobalJavaSetting = legacyConfig.UseGlobalJavaSetting; // 修复：保留全局Java设置
            result.WindowWidth = legacyConfig.WindowWidth;
            result.WindowHeight = legacyConfig.WindowHeight;
            result.LaunchCount = legacyConfig.LaunchCount;
            result.TotalPlayTimeSeconds = legacyConfig.TotalPlayTimeSeconds;
            result.LastLaunchTime = legacyConfig.LastLaunchTime;

            // 如果分析失败，才使用旧配置的版本信息兜底
            if (result.MinecraftVersion == "Unknown" && !string.IsNullOrEmpty(legacyConfig.MinecraftVersion))
            {
                result.MinecraftVersion = legacyConfig.MinecraftVersion;
            }
            if ((string.IsNullOrEmpty(result.ModLoaderType) || result.ModLoaderType == "vanilla")
                && !string.IsNullOrEmpty(legacyConfig.ModLoaderType) 
                && legacyConfig.ModLoaderType != "vanilla")
            {
                result.ModLoaderType = legacyConfig.ModLoaderType;
                result.ModLoaderVersion = legacyConfig.ModLoaderVersion;
            }
        }
        else
        {
            // 确保默认值正确
            result.AutoMemoryAllocation = true;
            result.UseGlobalJavaSetting = true;
        }

        // Step 5: 保存/更新 XianYuL.cfg
        // 确保这些精准分析的数据被持久化，下次可以直接读 cfg 变快
        // 仅在分析结果具有有效版本信息，或不存在旧配置时，才写入配置文件，避免用不完整数据覆盖已有配置
        var hasMeaningfulAnalysis =
            !(result.MinecraftVersion == "Unknown"
              && result.ModLoaderType == "vanilla");
        if (legacyConfig == null || hasMeaningfulAnalysis)
        {
            await SaveConfigAsync(versionDirectory, result);
        }

        return result;
    }
    
    /// <summary>
    /// 兼容旧接口，内部调用新异步方法并等待
    /// </summary>

    
    // 兼容旧接口
    public VersionConfig ExtractVersionConfigFromName(string versionId)
    {
        // 简单实现，不再推荐使用
        return new VersionConfig { CreatedAt = DateTime.Now };
    }

    /// <inheritdoc/>
    [Obsolete("Use GetFullVersionInfoAsync instead.")]
    public VersionConfig GetFullVersionInfo(string versionId, string versionDirectory)
    {
        return GetFullVersionInfoAsync(versionId, versionDirectory).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    [Obsolete("Use GetFullVersionInfoAsync or ExtractVersionConfigFromName instead.")]
    public VersionConfig GetVersionConfigFromDirectory(string versionDirectory)
    {
        // 只能尝试从目录名推断 ID，不太准确
        string versionId = new DirectoryInfo(versionDirectory).Name;
        return GetFullVersionInfoAsync(versionId, versionDirectory).GetAwaiter().GetResult();
    }

    private async Task<VersionConfig?> GetLegacyConfigAsync(string dir)
    {
        var config = await ReadXianYuLConfigAsync(dir);
        if (config != null) return config;
        return await ReadPCL2ConfigAsync(dir);
    }
    
    private Task SaveConfigAsync(string dir, VersionConfig config)
    {
        return CreateOrUpdateXianYuLConfigAsync(dir, config);
    }


        
        /// <summary>
        /// 读取XianYuL.cfg配置文件 (异步版本)
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>版本配置信息，如果读取失败则返回null</returns>
        private async Task<VersionConfig?> ReadXianYuLConfigAsync(string versionDirectory)
        {
            try
            {
                string configPath = Path.Combine(versionDirectory, "XianYuL.cfg");
                System.Diagnostics.Debug.WriteLine("[VersionInfoService]   检查XianYuL.cfg配置文件");
                
                if (File.Exists(configPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   找到XianYuL.cfg配置文件");
                    
                    string configContent = await File.ReadAllTextAsync(configPath);
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取XianYuL.cfg配置文件内容成功");
                    
                    var config = JsonConvert.DeserializeObject<VersionConfig>(configContent);
                    if (config != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   解析XianYuL.cfg配置文件成功");
                        return config;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   解析XianYuL.cfg配置文件返回null");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   XianYuL.cfg配置文件不存在");
                }
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取XianYuL.cfg文件IO错误: {ex.Message}");
                _logger.LogWarning(ex, "读取XianYuL.cfg文件IO错误");
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   解析XianYuL.cfg文件JSON错误: {ex.Message}");
                _logger.LogWarning(ex, "解析XianYuL.cfg文件JSON错误");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取XianYuL.cfg文件未知错误: {ex.Message}");
                _logger.LogWarning(ex, "读取XianYuL.cfg文件未知错误");
            }
            
            return null;
        }
        
        /// <summary>
        /// 读取XianYuL.cfg配置文件 (已弃用，使用 ReadXianYuLConfigAsync)
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>版本配置信息，如果读取失败则返回null</returns>
        [Obsolete("Use ReadXianYuLConfigAsync instead.")]
        private VersionConfig ReadXianYuLConfig(string versionDirectory)
        {
            try
            {
                string configPath = Path.Combine(versionDirectory, "XianYuL.cfg");
                System.Diagnostics.Debug.WriteLine("[VersionInfoService]   检查XianYuL.cfg配置文件");
                
                if (File.Exists(configPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   找到XianYuL.cfg配置文件");
                    
                    string configContent = File.ReadAllText(configPath);
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取XianYuL.cfg配置文件内容成功");
                    
                    var config = JsonConvert.DeserializeObject<VersionConfig>(configContent);
                    if (config != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   解析XianYuL.cfg配置文件成功");
                        return config;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   解析XianYuL.cfg配置文件返回null");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   XianYuL.cfg配置文件不存在");
                }
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取XianYuL.cfg文件IO错误: {ex.Message}");
                _logger.LogWarning(ex, "读取XianYuL.cfg文件IO错误");
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   解析XianYuL.cfg文件JSON错误: {ex.Message}");
                _logger.LogWarning(ex, "解析XianYuL.cfg文件JSON错误");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取XianYuL.cfg文件未知错误: {ex.Message}");
                _logger.LogWarning(ex, "读取XianYuL.cfg文件未知错误");
            }
            
            return null;
        }
        
        /// <summary>
        /// 读取MultiMC配置文件
        /// </summary>
        /// <remarks>
        /// MultiMC 使用完全不同的目录结构（instances/实例名/），
        /// 与标准 .minecraft/versions/ 结构不兼容，无法适配。
        /// </remarks>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>始终返回 null</returns>
        private VersionConfig ReadMultiMCConfig(string versionDirectory)
        {
            // MultiMC 目录结构不兼容，不支持
            return null;
        }
        
        /// <summary>
        /// 读取HMCL配置文件
        /// </summary>
        /// <remarks>
        /// HMCL 的 hmclversion.cfg 只存储启动配置（内存、窗口大小、Java路径等），
        /// 不包含 ModLoader 类型和版本信息。HMCL 通过解析 version.json 的 inheritsFrom 
        /// 和库依赖来判断 ModLoader，因此此方法无法提供有效的版本配置信息。
        /// </remarks>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>始终返回 null</returns>
        private VersionConfig ReadHMCLConfig(string versionDirectory)
        {
            // HMCL 不单独存储 ModLoader 信息，无法从其配置文件获取
            return null;
        }
        
        /// <summary>
        /// 读取PCL2配置文件（Setup.ini）(异步版本)
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>版本配置信息，如果读取失败则返回null</returns>
        private async Task<VersionConfig?> ReadPCL2ConfigAsync(string versionDirectory)
        {
            try
            {
                // PCL2配置文件位于版本目录\PCL\Setup.ini
                string configPath = Path.Combine(versionDirectory, "PCL", "Setup.ini");
                System.Diagnostics.Debug.WriteLine("[VersionInfoService]   检查PCL2配置文件");
                
                if (!File.Exists(configPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   PCL2配置文件不存在");
                    return null;
                }
                
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   找到PCL2配置文件");
                
                // 读取配置文件内容
                string configContent = await File.ReadAllTextAsync(configPath);
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取PCL2配置文件内容成功");
                
                // 解析INI格式配置
                Dictionary<string, string> pclConfig = ParseIniConfig(configContent);
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   解析PCL2配置文件成功，共 {pclConfig.Count} 个键值对");
                
                // 从VersionOriginal获取MC版本号
                if (!pclConfig.TryGetValue("VersionOriginal", out var minecraftVersion) || string.IsNullOrEmpty(minecraftVersion))
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   未能从VersionOriginal获取MC版本号");
                    return null;
                }
                
                System.Diagnostics.Debug.WriteLine("[VersionInfoService]   从VersionOriginal获取MC版本号");
                
                // 确定加载器类型和版本
                string modLoaderType = "vanilla";
                string modLoaderVersion = string.Empty;
                
                // 检查Fabric
                if (pclConfig.TryGetValue("VersionFabric", out var fabricVersion) && !string.IsNullOrEmpty(fabricVersion))
                {
                    modLoaderType = "fabric";
                    modLoaderVersion = fabricVersion;
                    System.Diagnostics.Debug.WriteLine("[VersionInfoService]   检测到Fabric版本");
                }
                // 检查Forge
                else if (pclConfig.TryGetValue("VersionForge", out var forgeVersion) && !string.IsNullOrEmpty(forgeVersion))
                {
                    modLoaderType = "forge";
                    modLoaderVersion = forgeVersion;
                    System.Diagnostics.Debug.WriteLine("[VersionInfoService]   检测到Forge版本");
                }
                // 检查NeoForge
                else if (pclConfig.TryGetValue("VersionNeoForge", out var neoForgeVersion) && !string.IsNullOrEmpty(neoForgeVersion))
                {
                    modLoaderType = "neoforge";
                    modLoaderVersion = neoForgeVersion;
                    System.Diagnostics.Debug.WriteLine("[VersionInfoService]   检测到NeoForge版本");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   检测到Vanilla版本");
                }
                
                // 检查Optifine
                if (!pclConfig.TryGetValue("VersionOptiFine", out var optifineVersion))
                {
                    optifineVersion = string.Empty;
                }
                
                if (!string.IsNullOrEmpty(optifineVersion))
                {
                    System.Diagnostics.Debug.WriteLine("[VersionInfoService]   检测到Optifine版本");
                }
                
                // 创建并返回VersionConfig对象
                VersionConfig result = new VersionConfig
                {
                    ModLoaderType = modLoaderType,
                    ModLoaderVersion = modLoaderVersion,
                    MinecraftVersion = minecraftVersion,
                    OptifineVersion = optifineVersion,
                    CreatedAt = DateTime.Now
                };
                
                System.Diagnostics.Debug.WriteLine("[VersionInfoService]   成功创建VersionConfig对象");
                
                return result;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取PCL2配置文件IO错误: {ex.Message}");
                _logger.LogWarning(ex, "读取PCL2配置文件IO错误");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取PCL2配置文件未知错误: {ex.Message}");
                _logger.LogWarning(ex, "读取PCL2配置文件未知错误");
            }
            
            return null;
        }
        
        /// <summary>
        /// 读取PCL2配置文件（Setup.ini）(已弃用，使用 ReadPCL2ConfigAsync)
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>版本配置信息，如果读取失败则返回null</returns>

        private VersionConfig ReadPCL2Config(string versionDirectory)
        {
            try
            {
                // PCL2配置文件位于版本目录\PCL\Setup.ini
                string configPath = Path.Combine(versionDirectory, "PCL", "Setup.ini");
                System.Diagnostics.Debug.WriteLine("[VersionInfoService]   检查PCL2配置文件");
                
                if (!File.Exists(configPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   PCL2配置文件不存在");
                    return null;
                }
                
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   找到PCL2配置文件");
                
                // 读取配置文件内容
                string configContent = File.ReadAllText(configPath);
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取PCL2配置文件内容成功");
                
                // 解析INI格式配置
                Dictionary<string, string> pclConfig = ParseIniConfig(configContent);
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   解析PCL2配置文件成功，共 {pclConfig.Count} 个键值对");
                
                // 从VersionOriginal获取MC版本号
                if (!pclConfig.TryGetValue("VersionOriginal", out var minecraftVersion) || string.IsNullOrEmpty(minecraftVersion))
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   未能从VersionOriginal获取MC版本号");
                    return null;
                }
                
                System.Diagnostics.Debug.WriteLine("[VersionInfoService]   从VersionOriginal获取MC版本号");
                
                // 确定加载器类型和版本
                string modLoaderType = "vanilla";
                string modLoaderVersion = string.Empty;
                
                // 检查Fabric
                if (pclConfig.TryGetValue("VersionFabric", out var fabricVersion) && !string.IsNullOrEmpty(fabricVersion))
                {
                    modLoaderType = "fabric";
                    modLoaderVersion = fabricVersion;
                    System.Diagnostics.Debug.WriteLine("[VersionInfoService]   检测到Fabric版本");
                }
                // 检查Forge
                else if (pclConfig.TryGetValue("VersionForge", out var forgeVersion) && !string.IsNullOrEmpty(forgeVersion))
                {
                    modLoaderType = "forge";
                    modLoaderVersion = forgeVersion;
                    System.Diagnostics.Debug.WriteLine("[VersionInfoService]   检测到Forge版本");
                }
                // 检查NeoForge
                else if (pclConfig.TryGetValue("VersionNeoForge", out var neoForgeVersion) && !string.IsNullOrEmpty(neoForgeVersion))
                {
                    modLoaderType = "neoforge";
                    modLoaderVersion = neoForgeVersion;
                    System.Diagnostics.Debug.WriteLine("[VersionInfoService]   检测到NeoForge版本");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   检测到Vanilla版本");
                }
                
                // 检查Optifine
                if (!pclConfig.TryGetValue("VersionOptiFine", out var optifineVersion))
                {
                    optifineVersion = string.Empty;
                }
                
                if (!string.IsNullOrEmpty(optifineVersion))
                {
                    System.Diagnostics.Debug.WriteLine("[VersionInfoService]   检测到Optifine版本");
                }
                
                // 创建并返回VersionConfig对象
                VersionConfig result = new VersionConfig
                {
                    ModLoaderType = modLoaderType,
                    ModLoaderVersion = modLoaderVersion,
                    MinecraftVersion = minecraftVersion,
                    OptifineVersion = optifineVersion,
                    CreatedAt = DateTime.Now
                };
                
                System.Diagnostics.Debug.WriteLine("[VersionInfoService]   成功创建VersionConfig对象");
                
                return result;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取PCL2配置文件IO错误: {ex.Message}");
                _logger.LogWarning(ex, "读取PCL2配置文件IO错误");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取PCL2配置文件未知错误: {ex.Message}");
                _logger.LogWarning(ex, "读取PCL2配置文件未知错误");
            }
            
            return null;
        }
        
        /// <summary>
        /// 解析INI格式配置文件
        /// </summary>
        /// <param name="content">INI配置文件内容</param>
        /// <returns>解析后的配置键值对</returns>
        private Dictionary<string, string> ParseIniConfig(string content)
        {
            Dictionary<string, string> config = new Dictionary<string, string>();
            
            // 按行解析
            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (string line in lines)
            {
                // 跳过空行和注释行
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#") || trimmedLine.StartsWith(";"))
                {
                    continue;
                }
                
                // 查找等号分隔符
                int equalsIndex = trimmedLine.IndexOf(':');
                if (equalsIndex > 0)
                {
                    string key = trimmedLine.Substring(0, equalsIndex).Trim();
                    string value = trimmedLine.Substring(equalsIndex + 1).Trim();
                    
                    // 只添加非空键
                    if (!string.IsNullOrEmpty(key))
                    {
                        config[key] = value;
                    }
                }
            }
            
            return config;
        }
        
        /// <summary>
        /// 创建或更新标准格式的XianYuL.cfg文件 (异步版本)
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <param name="config">版本配置信息</param>
        private async Task CreateOrUpdateXianYuLConfigAsync(string versionDirectory, VersionConfig config)
        {
            try
            {
                string configPath = Path.Combine(versionDirectory, "XianYuL.cfg");
                
                // 增加详细的调试日志
                _logger.LogInformation($"[VersionInfoService]   开始创建/更新XianYuL.cfg文件: {configPath}");
                _logger.LogInformation($"[VersionInfoService]   >> 传入参数 config 中内存设置: Initial={config?.InitialHeapMemory}GB, Max={config?.MaximumHeapMemory}GB, Auto={config?.AutoMemoryAllocation}");
                
                // 确保配置信息完整
                if (config == null)
                {
                    _logger.LogWarning($"[VersionInfoService]   ❌ 配置信息为空，跳过创建/更新");
                    return;
                }
                
                // 如果文件已存在，读取现有配置以保留统计数据
                VersionConfig? existingConfig = null;
                if (File.Exists(configPath))
                {
                    try
                    {
                        var existingJson = await File.ReadAllTextAsync(configPath);
                        existingConfig = JsonConvert.DeserializeObject<VersionConfig>(existingJson);
                        _logger.LogInformation($"[VersionInfoService]   >> 读取到旧文件 config 中内存设置: Initial={existingConfig?.InitialHeapMemory}GB, Max={existingConfig?.MaximumHeapMemory}GB");
                    }
                    catch (IOException ioEx)
                    {
                        _logger.LogWarning($"[VersionInfoService]   ⚠ 读取现有配置文件失败（IO异常），将使用默认配置。路径: {configPath}, 错误: {ioEx}");
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogWarning($"[VersionInfoService]   ⚠ 解析现有配置文件失败（JSON异常），将使用默认配置。路径: {configPath}, 错误: {jsonEx}");
                    }
                }
                else 
                {
                    _logger.LogInformation($"[VersionInfoService]   >> 旧文件不存在，将创建新文件");
                }
                
                // 准备标准格式的配置内容，保留现有统计数据
                var standardConfig = new VersionConfig
                {
                    ModLoaderType = config.ModLoaderType ?? "vanilla",
                    ModLoaderVersion = config.ModLoaderVersion ?? string.Empty,
                    MinecraftVersion = config.MinecraftVersion ?? string.Empty,
                    OptifineVersion = config.OptifineVersion ?? string.Empty,
                    CreatedAt = existingConfig?.CreatedAt ?? DateTime.Now,
                    
                    // 使用传入的 config 中的设置（因为 config 已经融合了 legacyConfig 的用户设置）
                    // 之前的写法会再次读取 existingConfig，如果读取失败就会导致配置重置为默认值 (12G/6G)
                    AutoMemoryAllocation = config.AutoMemoryAllocation,
                    InitialHeapMemory = config.InitialHeapMemory,
                    MaximumHeapMemory = config.MaximumHeapMemory,
                    JavaPath = config.JavaPath,
                    UseGlobalJavaSetting = config.UseGlobalJavaSetting,
                    WindowWidth = config.WindowWidth,
                    WindowHeight = config.WindowHeight,

                    // 保留统计数据
                    LaunchCount = config.LaunchCount,
                    TotalPlayTimeSeconds = config.TotalPlayTimeSeconds,
                    LastLaunchTime = config.LastLaunchTime
                };

                _logger.LogInformation($"[VersionInfoService]   >> 最终写入文件的内存设置: Initial={standardConfig.InitialHeapMemory}GB, Max={standardConfig.MaximumHeapMemory}GB");
                
                // 序列化配置为JSON格式
                
                // 序列化配置为JSON格式
                string jsonContent = JsonConvert.SerializeObject(standardConfig, Formatting.Indented);
                
                // 写入文件
                await File.WriteAllTextAsync(configPath, jsonContent);
                
                _logger.LogInformation($"[VersionInfoService]   ✅ 成功创建/更新XianYuL.cfg文件");
                _logger.LogInformation($"[VersionInfoService]     ModLoaderType: {standardConfig.ModLoaderType}");
                _logger.LogInformation($"[VersionInfoService]     ModLoaderVersion: {standardConfig.ModLoaderVersion}");
                _logger.LogInformation($"[VersionInfoService]     MinecraftVersion: {standardConfig.MinecraftVersion}");
                _logger.LogInformation($"[VersionInfoService]     OptifineVersion: {standardConfig.OptifineVersion}");
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, $"[VersionInfoService]   ❌ 创建/更新XianYuL.cfg文件IO错误: {ex.Message}");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, $"[VersionInfoService]   ❌ 序列化XianYuL.cfg配置JSON错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"[VersionInfoService]   ❌ 创建/更新XianYuL.cfg文件未知错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 创建或更新标准格式的XianYuL.cfg文件 (已弃用，使用 CreateOrUpdateXianYuLConfigAsync)
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <param name="config">版本配置信息</param>

        private void CreateOrUpdateXianYuLConfig(string versionDirectory, VersionConfig config)
        {
            try
            {
                string configPath = Path.Combine(versionDirectory, "XianYuL.cfg");
                
                // 增加详细的调试日志
                _logger.LogInformation($"[VersionInfoService]   开始创建/更新XianYuL.cfg文件: {configPath}");
                _logger.LogInformation($"[VersionInfoService]   >> 传入参数 config 中内存设置: Initial={config?.InitialHeapMemory}GB, Max={config?.MaximumHeapMemory}GB, Auto={config?.AutoMemoryAllocation}");
                
                // 确保配置信息完整
                if (config == null)
                {
                    _logger.LogWarning($"[VersionInfoService]   ❌ 配置信息为空，跳过创建/更新");
                    return;
                }
                
                // 如果文件已存在，读取现有配置以保留统计数据
                VersionConfig? existingConfig = null;
                if (File.Exists(configPath))
                {
                    try
                    {
                        var existingJson = File.ReadAllText(configPath);
                        existingConfig = JsonConvert.DeserializeObject<VersionConfig>(existingJson);
                        _logger.LogInformation($"[VersionInfoService]   >> 读取到旧文件 config 中内存设置: Initial={existingConfig?.InitialHeapMemory}GB, Max={existingConfig?.MaximumHeapMemory}GB");
                    }
                    catch (IOException ioEx)
                    {
                        _logger.LogWarning($"[VersionInfoService]   ⚠ 读取现有配置文件失败（IO异常），将使用默认配置。路径: {configPath}, 错误: {ioEx}");
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogWarning($"[VersionInfoService]   ⚠ 解析现有配置文件失败（JSON异常），将使用默认配置。路径: {configPath}, 错误: {jsonEx}");
                    }
                }
                else 
                {
                    _logger.LogInformation($"[VersionInfoService]   >> 旧文件不存在，将创建新文件");
                }
                
                // 准备标准格式的配置内容，保留现有统计数据
                var standardConfig = new VersionConfig
                {
                    ModLoaderType = config.ModLoaderType ?? "vanilla",
                    ModLoaderVersion = config.ModLoaderVersion ?? string.Empty,
                    MinecraftVersion = config.MinecraftVersion ?? string.Empty,
                    OptifineVersion = config.OptifineVersion ?? string.Empty,
                    CreatedAt = existingConfig?.CreatedAt ?? DateTime.Now,
                    
                    // 使用传入的 config 中的设置（因为 config 已经融合了 legacyConfig 的用户设置）
                    // 之前的写法会再次读取 existingConfig，如果读取失败就会导致配置重置为默认值 (12G/6G)
                    AutoMemoryAllocation = config.AutoMemoryAllocation,
                    InitialHeapMemory = config.InitialHeapMemory,
                    MaximumHeapMemory = config.MaximumHeapMemory,
                    JavaPath = config.JavaPath,
                    UseGlobalJavaSetting = config.UseGlobalJavaSetting,
                    WindowWidth = config.WindowWidth,
                    WindowHeight = config.WindowHeight,

                    // 保留统计数据
                    LaunchCount = config.LaunchCount,
                    TotalPlayTimeSeconds = config.TotalPlayTimeSeconds,
                    LastLaunchTime = config.LastLaunchTime
                };

                _logger.LogInformation($"[VersionInfoService]   >> 最终写入文件的内存设置: Initial={standardConfig.InitialHeapMemory}GB, Max={standardConfig.MaximumHeapMemory}GB");

                // 序列化配置为JSON格式
                
                // 序列化配置为JSON格式
                string jsonContent = JsonConvert.SerializeObject(standardConfig, Formatting.Indented);
                
                // 写入文件
                File.WriteAllText(configPath, jsonContent);
                
                _logger.LogInformation($"[VersionInfoService]   ✅ 成功创建/更新XianYuL.cfg文件");
                _logger.LogInformation($"[VersionInfoService]     ModLoaderType: {standardConfig.ModLoaderType}");
                _logger.LogInformation($"[VersionInfoService]     ModLoaderVersion: {standardConfig.ModLoaderVersion}");
                _logger.LogInformation($"[VersionInfoService]     MinecraftVersion: {standardConfig.MinecraftVersion}");
                _logger.LogInformation($"[VersionInfoService]     OptifineVersion: {standardConfig.OptifineVersion}");
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, $"[VersionInfoService]   ❌ 创建/更新XianYuL.cfg文件IO错误: {ex.Message}");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, $"[VersionInfoService]   ❌ 序列化XianYuL.cfg配置JSON错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"[VersionInfoService]   ❌ 创建/更新XianYuL.cfg文件未知错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 读取其他常见启动器配置文件
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>版本配置信息，如果读取失败则返回null</returns>
        private VersionConfig ReadOtherLauncherConfigs(string versionDirectory)
        {
            // 这里可以添加对其他启动器配置文件的支持
            // 目前仅返回null，作为扩展点
            return null;
        }
    }