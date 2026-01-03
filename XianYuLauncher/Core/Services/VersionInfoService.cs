using System;using System.IO;using System.Text.RegularExpressions;using Microsoft.Extensions.Logging;using Newtonsoft.Json;using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services
{
    /// <summary>
    /// 版本信息服务实现，提供统一的版本信息获取方法
    /// </summary>
    public class VersionInfoService : IVersionInfoService
    {
        private readonly ILogger _logger;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public VersionInfoService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<VersionInfoService>();
        }
        
        /// <summary>
        /// 从版本目录获取版本配置信息，支持从多个来源读取
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>版本配置信息，如果无法获取则返回null</returns>
        public VersionConfig GetVersionConfigFromDirectory(string versionDirectory)
        {
            if (string.IsNullOrEmpty(versionDirectory))
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 版本目录路径为空");
                return null;
            }
            
            if (!Directory.Exists(versionDirectory))
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 版本目录不存在: {versionDirectory}");
                return null;
            }
            
            System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 开始获取版本配置，目录: {versionDirectory}");
            
            VersionConfig config = null;
            bool isFromThirdParty = false;
            string configSource = "";
            
            // 1. 优先尝试读取XianYuL.cfg
            System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 1. 尝试读取XianYuL.cfg配置文件");
            config = ReadXianYuLConfig(versionDirectory);
            if (config != null)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService] ✅ 成功读取XianYuL.cfg配置文件");
                configSource = "XianYuL.cfg";
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService] ❌ 未能读取XianYuL.cfg配置文件");
                
                // 2. 尝试读取PCL2配置文件
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 2. 尝试读取PCL2配置文件");
                config = ReadPCL2Config(versionDirectory);
                if (config != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService] ✅ 成功读取PCL2配置文件");
                    isFromThirdParty = true;
                    configSource = "PCL2 Setup.ini";
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService] ❌ 未能读取PCL2配置文件");
                    
                    // 3. 尝试读取MultiMC配置文件
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 3. 尝试读取MultiMC配置文件");
                    config = ReadMultiMCConfig(versionDirectory);
                    if (config != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VersionInfoService] ✅ 成功读取MultiMC配置文件");
                        isFromThirdParty = true;
                        configSource = "MultiMC config";
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[VersionInfoService] ❌ 未能读取MultiMC配置文件");
                        
                        // 4. 尝试读取HMCL配置文件
                        System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 4. 尝试读取HMCL配置文件");
                        config = ReadHMCLConfig(versionDirectory);
                        if (config != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[VersionInfoService] ✅ 成功读取HMCL配置文件");
                            isFromThirdParty = true;
                            configSource = "HMCL config";
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[VersionInfoService] ❌ 未能读取HMCL配置文件");
                            
                            // 5. 尝试读取其他常见启动器配置文件
                            System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 5. 尝试读取其他启动器配置文件");
                            config = ReadOtherLauncherConfigs(versionDirectory);
                            if (config != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[VersionInfoService] ✅ 成功读取其他启动器配置文件");
                                isFromThirdParty = true;
                                configSource = "Other launcher config";
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[VersionInfoService] ❌ 未能读取任何配置文件");
                            }
                        }
                    }
                }
            }
            
            // 如果从第三方启动器读取到配置，创建或更新XianYuL.cfg文件
            if (config != null && isFromThirdParty)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 📝 从{configSource}读取到配置，开始创建/更新XianYuL.cfg文件");
                CreateOrUpdateXianYuLConfig(versionDirectory, config);
            }
            else if (config != null)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 🔍 配置来自{configSource}，无需更新XianYuL.cfg");
            }
            
            System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 所有配置文件读取完成，返回配置: {config?.ModLoaderType}");
            return config;
        }
        
        /// <summary>
        /// 从版本名称提取版本配置信息
        /// </summary>
        /// <param name="versionId">版本ID</param>
        /// <returns>提取的版本配置信息</returns>
        public VersionConfig ExtractVersionConfigFromName(string versionId)
        {
            if (string.IsNullOrEmpty(versionId))
            {
                return null;
            }
            
            string minecraftVersion = string.Empty;
            string modLoaderType = "vanilla";
            string modLoaderVersion = string.Empty;
            
            // 处理不同格式的版本名称
            if (versionId.Contains("fabric-"))
            {
                modLoaderType = "fabric";
                var parts = versionId.Split('-');
                if (parts.Length >= 3)
                {
                    minecraftVersion = parts[1];
                    modLoaderVersion = parts[2];
                }
            }
            else if (versionId.Contains("forge-"))
            {
                modLoaderType = "forge";
                var parts = versionId.Split('-');
                if (parts.Length >= 3)
                {
                    minecraftVersion = parts[1];
                    modLoaderVersion = string.Join("-", parts.Skip(2));
                }
            }
            else if (versionId.Contains("neoforge-"))
            {
                modLoaderType = "neoforge";
                var parts = versionId.Split('-');
                if (parts.Length >= 3)
                {
                    minecraftVersion = parts[1];
                    modLoaderVersion = string.Join("-", parts.Skip(2));
                }
            }
            else if (versionId.Contains("quilt-"))
            {
                modLoaderType = "quilt";
                var parts = versionId.Split('-');
                if (parts.Length >= 3)
                {
                    minecraftVersion = parts[1];
                    modLoaderVersion = string.Join("-", parts.Skip(2));
                }
            }
            else
            {
                // 尝试从版本名中提取Minecraft版本号
                var versionMatch = Regex.Match(versionId, @"^(\d+\.\d+(\.\d+)?)");
                if (versionMatch.Success)
                {
                    minecraftVersion = versionMatch.Value;
                }
            }
            
            return new VersionConfig
            {
                ModLoaderType = modLoaderType,
                ModLoaderVersion = modLoaderVersion,
                MinecraftVersion = minecraftVersion,
                CreatedAt = DateTime.Now
            };
        }
        
        /// <summary>
        /// 获取完整的版本信息，包括从配置文件和版本名提取的信息
        /// </summary>
        /// <param name="versionId">版本ID</param>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>完整的版本配置信息</returns>
        public VersionConfig GetFullVersionInfo(string versionId, string versionDirectory)
        {
            // 快速路径：如果已有XianYuL.cfg文件，直接读取
            string xianYuLConfigPath = Path.Combine(versionDirectory, "XianYuL.cfg");
            if (File.Exists(xianYuLConfigPath))
            {
                return ReadXianYuLConfig(versionDirectory);
            }
            
            // 完整读取逻辑
            System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 开始获取完整版本信息，版本ID: {versionId}");
            
            // 1. 先尝试从配置文件读取
            System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 1. 尝试从配置文件读取版本信息");
            VersionConfig config = GetVersionConfigFromDirectory(versionDirectory);
            if (config != null)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 从配置文件成功获取版本信息");
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   最终版本信息: ModLoaderType={config.ModLoaderType}, ModLoaderVersion={config.ModLoaderVersion}, MinecraftVersion={config.MinecraftVersion}");
                return config;
            }
            
            // 2. 如果配置文件读取失败，从版本名提取
            System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 2. 配置文件读取失败，尝试从版本名提取");
            config = ExtractVersionConfigFromName(versionId);
            if (config != null)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 从版本名成功提取版本信息");
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   最终版本信息: ModLoaderType={config.ModLoaderType}, ModLoaderVersion={config.ModLoaderVersion}, MinecraftVersion={config.MinecraftVersion}");
                return config;
            }
            
            // 3. 如果所有方法都失败，返回默认配置
            System.Diagnostics.Debug.WriteLine($"[VersionInfoService] 3. 所有方法都失败，返回默认配置");
            var defaultConfig = new VersionConfig
            {
                ModLoaderType = "vanilla",
                ModLoaderVersion = string.Empty,
                MinecraftVersion = string.Empty,
                CreatedAt = DateTime.Now
            };
            
            System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   最终版本信息: 默认配置 (vanilla)");
            return defaultConfig;
        }
        
        /// <summary>
        /// 异步获取完整的版本信息，包括从配置文件和版本名提取的信息
        /// </summary>
        /// <param name="versionId">版本ID</param>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>完整的版本配置信息</returns>
        public async Task<VersionConfig> GetFullVersionInfoAsync(string versionId, string versionDirectory)
        {
            // 在后台线程执行IO密集型操作，避免阻塞UI线程
            return await Task.Run(() => GetFullVersionInfo(versionId, versionDirectory));
        }
        
        /// <summary>
        /// 读取XianYuL.cfg配置文件
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>版本配置信息，如果读取失败则返回null</returns>
        private VersionConfig ReadXianYuLConfig(string versionDirectory)
        {
            try
            {
                string configPath = Path.Combine(versionDirectory, "XianYuL.cfg");
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   检查XianYuL.cfg配置文件路径: {configPath}");
                
                if (File.Exists(configPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   找到XianYuL.cfg配置文件");
                    
                    string configContent = File.ReadAllText(configPath);
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取XianYuL.cfg配置文件内容成功");
                    
                    var config = JsonConvert.DeserializeObject<VersionConfig>(configContent);
                    if (config != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   解析XianYuL.cfg配置文件成功");
                        System.Diagnostics.Debug.WriteLine($"[VersionInfoService]     ModLoaderType: {config.ModLoaderType}");
                        System.Diagnostics.Debug.WriteLine($"[VersionInfoService]     ModLoaderVersion: {config.ModLoaderVersion}");
                        System.Diagnostics.Debug.WriteLine($"[VersionInfoService]     MinecraftVersion: {config.MinecraftVersion}");
                        System.Diagnostics.Debug.WriteLine($"[VersionInfoService]     OptifineVersion: {config.OptifineVersion}");
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
                _logger.LogWarning(ex, "读取XianYuL.cfg文件IO错误: {VersionDirectory}", versionDirectory);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   解析XianYuL.cfg文件JSON错误: {ex.Message}");
                _logger.LogWarning(ex, "解析XianYuL.cfg文件JSON错误: {VersionDirectory}", versionDirectory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取XianYuL.cfg文件未知错误: {ex.Message}");
                _logger.LogWarning(ex, "读取XianYuL.cfg文件未知错误: {VersionDirectory}", versionDirectory);
            }
            
            return null;
        }
        
        /// <summary>
        /// 读取MultiMC配置文件
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>版本配置信息，如果读取失败则返回null</returns>
        private VersionConfig ReadMultiMCConfig(string versionDirectory)
        {
            try
            {
                // MultiMC配置文件通常不在版本目录中，这里仅作为示例
                // 实际实现需要根据MultiMC的配置文件位置和格式进行调整
                string configPath = Path.Combine(versionDirectory, "instance.cfg");
                if (File.Exists(configPath))
                {
                    // 读取并解析MultiMC配置文件
                    // 这里仅作为示例，实际实现需要根据MultiMC的配置文件格式进行调整
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取MultiMC配置文件错误: {VersionDirectory}", versionDirectory);
            }
            
            return null;
        }
        
        /// <summary>
        /// 读取HMCL配置文件
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>版本配置信息，如果读取失败则返回null</returns>
        private VersionConfig ReadHMCLConfig(string versionDirectory)
        {
            try
            {
                // HMCL配置文件通常不在版本目录中，这里仅作为示例
                // 实际实现需要根据HMCL的配置文件位置和格式进行调整
                string configPath = Path.Combine(versionDirectory, "version.json");
                if (File.Exists(configPath))
                {
                    // 读取并解析HMCL配置文件
                    // 这里仅作为示例，实际实现需要根据HMCL的配置文件格式进行调整
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取HMCL配置文件错误: {VersionDirectory}", versionDirectory);
            }
            
            return null;
        }
        
        /// <summary>
        /// 读取PCL2配置文件（Setup.ini）
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>版本配置信息，如果读取失败则返回null</returns>
        private VersionConfig ReadPCL2Config(string versionDirectory)
        {
            try
            {
                // PCL2配置文件位于版本目录\PCL\Setup.ini
                string configPath = Path.Combine(versionDirectory, "PCL", "Setup.ini");
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   检查PCL2配置文件路径: {configPath}");
                
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
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   解析PCL2配置文件成功，共{ pclConfig.Count}个键值对");
                
                // 输出所有解析到的键值对，便于调试
                foreach (var kvp in pclConfig)
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]     {kvp.Key}: {kvp.Value}");
                }
                
                // 从VersionOriginal获取MC版本号
                string minecraftVersion = pclConfig.ContainsKey("VersionOriginal") ? pclConfig["VersionOriginal"] : string.Empty;
                if (string.IsNullOrEmpty(minecraftVersion))
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   未能从VersionOriginal获取MC版本号");
                    return null;
                }
                
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   从VersionOriginal获取MC版本号: {minecraftVersion}");
                
                // 确定加载器类型和版本
                string modLoaderType = "vanilla";
                string modLoaderVersion = string.Empty;
                
                // 检查Fabric
                if (pclConfig.ContainsKey("VersionFabric") && !string.IsNullOrEmpty(pclConfig["VersionFabric"]))
                {
                    modLoaderType = "fabric";
                    modLoaderVersion = pclConfig["VersionFabric"];
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   检测到Fabric版本: {modLoaderVersion}");
                }
                // 检查Forge
                else if (pclConfig.ContainsKey("VersionForge") && !string.IsNullOrEmpty(pclConfig["VersionForge"]))
                {
                    modLoaderType = "forge";
                    modLoaderVersion = pclConfig["VersionForge"];
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   检测到Forge版本: {modLoaderVersion}");
                }
                // 检查NeoForge
                else if (pclConfig.ContainsKey("VersionNeoForge") && !string.IsNullOrEmpty(pclConfig["VersionNeoForge"]))
                {
                    modLoaderType = "neoforge";
                    modLoaderVersion = pclConfig["VersionNeoForge"];
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   检测到NeoForge版本: {modLoaderVersion}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   检测到Vanilla版本");
                }
                
                // 检查Optifine
                string optifineVersion = pclConfig.ContainsKey("VersionOptiFine") ? pclConfig["VersionOptiFine"] : string.Empty;
                if (!string.IsNullOrEmpty(optifineVersion))
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   检测到Optifine版本: {optifineVersion}");
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
                
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   成功创建VersionConfig对象: ModLoaderType={result.ModLoaderType}, ModLoaderVersion={result.ModLoaderVersion}, MinecraftVersion={result.MinecraftVersion}");
                
                return result;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取PCL2配置文件IO错误: {ex.Message}");
                _logger.LogWarning(ex, "读取PCL2配置文件IO错误: {VersionDirectory}", versionDirectory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   读取PCL2配置文件未知错误: {ex.Message}");
                _logger.LogWarning(ex, "读取PCL2配置文件未知错误: {VersionDirectory}", versionDirectory);
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
        /// 创建或更新标准格式的XianYuL.cfg文件
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <param name="config">版本配置信息</param>
        private void CreateOrUpdateXianYuLConfig(string versionDirectory, VersionConfig config)
        {
            try
            {
                string configPath = Path.Combine(versionDirectory, "XianYuL.cfg");
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   开始创建/更新XianYuL.cfg文件: {configPath}");
                
                // 确保配置信息完整
                if (config == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   ❌ 配置信息为空，跳过创建/更新");
                    return;
                }
                
                // 准备标准格式的配置内容
                var standardConfig = new
                {
                    ModLoaderType = config.ModLoaderType ?? "vanilla",
                    ModLoaderVersion = config.ModLoaderVersion ?? string.Empty,
                    MinecraftVersion = config.MinecraftVersion ?? string.Empty,
                    OptifineVersion = config.OptifineVersion ?? string.Empty,
                    CreatedAt = DateTime.Now,
                    // 保留原有配置的AutoMemoryAllocation等字段（如果存在）
                    AutoMemoryAllocation = true,
                    InitialHeapMemory = 6.0,
                    MaximumHeapMemory = 12.0,
                    JavaPath = string.Empty,
                    UseGlobalJavaSetting = true,
                    WindowWidth = 1920,
                    WindowHeight = 1080
                };
                
                // 序列化配置为JSON格式
                string jsonContent = JsonConvert.SerializeObject(standardConfig, Formatting.Indented);
                
                // 写入文件
                File.WriteAllText(configPath, jsonContent);
                
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   ✅ 成功创建/更新XianYuL.cfg文件");
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]     ModLoaderType: {standardConfig.ModLoaderType}");
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]     ModLoaderVersion: {standardConfig.ModLoaderVersion}");
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]     MinecraftVersion: {standardConfig.MinecraftVersion}");
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]     OptifineVersion: {standardConfig.OptifineVersion}");
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   ❌ 创建/更新XianYuL.cfg文件IO错误: {ex.Message}");
                _logger.LogWarning(ex, "创建/更新XianYuL.cfg文件IO错误: {VersionDirectory}", versionDirectory);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   ❌ 序列化XianYuL.cfg配置JSON错误: {ex.Message}");
                _logger.LogWarning(ex, "序列化XianYuL.cfg配置JSON错误: {VersionDirectory}", versionDirectory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionInfoService]   ❌ 创建/更新XianYuL.cfg文件未知错误: {ex.Message}");
                _logger.LogWarning(ex, "创建/更新XianYuL.cfg文件未知错误: {VersionDirectory}", versionDirectory);
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
}