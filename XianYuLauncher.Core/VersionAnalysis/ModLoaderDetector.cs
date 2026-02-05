using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.VersionAnalysis.Models;

namespace XianYuLauncher.Core.VersionAnalysis
{
    /// <summary>
    /// 负责分析 .json 文件中的 libraries 以识别 ModLoader
    /// </summary>
    public class ModLoaderDetector
    {
        private readonly Microsoft.Extensions.Logging.ILogger? _logger;

        public ModLoaderDetector(Microsoft.Extensions.Logging.ILogger? logger = null)
        {
            _logger = logger;
        }

        public (string Type, string Version) Detect(MinecraftVersionManifest? manifest)
        {
            if (manifest == null || manifest.Libraries == null)
            {
                return ("vanilla", string.Empty);
            }

            // 1. Fabric (and Legacy Fabric)
            var fabricLib = manifest.Libraries.FirstOrDefault(l => l.Name.StartsWith("net.fabricmc:fabric-loader"));
            if (fabricLib != null)
            {
                // Check for Legacy Fabric via URL or Library Name in other libraries
                // Legacy Fabric uses standard fabric-loader but includes other libraries from legacyfabric.net
                var isLegacyFabric = manifest.Libraries.Any(l => 
                    (l.Url != null && l.Url.Contains("legacyfabric.net")) || 
                    l.Name.StartsWith("net.legacyfabric"));

                if (isLegacyFabric)
                {
                     _logger?.LogInformation($"[ModLoaderDetector] 命中特征库 (Legacy Fabric detected via dependencies): {fabricLib.Name}");
                     return ("LegacyFabric", ParseVersion(fabricLib.Name));
                }

                _logger?.LogInformation($"[ModLoaderDetector] 命中特征库 (Fabric): {fabricLib.Name}");
                return ("fabric", ParseVersion(fabricLib.Name));
            }

            // 2. Forge (Enhanced Detection)
            // 策略 A: 检查启动参数 (最精准)
            // 新版 Forge (1.13+) 会明确通过参数告诉游戏版本
            string? forgeVerFromArgs = GetForgeVersionFromArguments(manifest);
            if (!string.IsNullOrEmpty(forgeVerFromArgs))
            {
                _logger?.LogInformation($"[ModLoaderDetector] 命中启动参数 (Forge): --fml.forgeVersion {forgeVerFromArgs}");
                return ("forge", forgeVerFromArgs);
            }

            // 策略 B: 检查核心库 fmlloader (次精准)
            // 格式: net.minecraftforge:fmlloader:1.20.1-47.4.16
            var fmlLib = manifest.Libraries.FirstOrDefault(l => l.Name.StartsWith("net.minecraftforge:fmlloader"));
            if (fmlLib != null)
            {
                _logger?.LogInformation($"[ModLoaderDetector] 命中核心库 (Forge FML): {fmlLib.Name}");
                string fullVer = ParseVersion(fmlLib.Name); // 得到 1.20.1-47.4.16
                return ("forge", CleanForgeVersion(fullVer));
            }

            // 策略 C: 检查通用库 forge (兜底)
            // 旧版或通用检测
            var forgeLib = manifest.Libraries.FirstOrDefault(l => l.Name.StartsWith("net.minecraftforge:forge"));
            if (forgeLib != null)
            {
                _logger?.LogInformation($"[ModLoaderDetector] 命中特征库 (Forge): {forgeLib.Name}");
                string fullVer = ParseVersion(forgeLib.Name);
                return ("forge", CleanForgeVersion(fullVer));
            }

            // 3. NeoForge
            // 策略 A: 检查启动参数 (最精准)
            string? neoVerFromArgs = GetNeoForgeVersionFromArguments(manifest);
            if (!string.IsNullOrEmpty(neoVerFromArgs))
            {
                _logger?.LogInformation($"[ModLoaderDetector] 命中启动参数 (NeoForge): --fml.neoForgeVersion {neoVerFromArgs}");
                return ("neoforge", neoVerFromArgs);
            }

            // 策略 B: 检查特征库 (库名通常如 net.neoforged:neoforge:版本)
            var neoLib = manifest.Libraries.FirstOrDefault(l => l.Name.StartsWith("net.neoforged:neoforge"));
            if (neoLib != null)
            {
                _logger?.LogInformation($"[ModLoaderDetector] 命中特征库 (NeoForge): {neoLib.Name}");
                return ("neoforge", ParseVersion(neoLib.Name));
            }

            // 4. Quilt
            var quiltLib = manifest.Libraries.FirstOrDefault(l => l.Name.StartsWith("org.quiltmc:quilt-loader"));
            if (quiltLib != null)
            {
                _logger?.LogInformation($"[ModLoaderDetector] 命中特征库 (Quilt): {quiltLib.Name}");
                return ("quilt", ParseVersion(quiltLib.Name));
            }
            
            // 5. OptiFine (Standalone)
            // 这种通常是通过安装器生成的 version.json
            // Maven 格式: optifine:OptiFine:1.21.1_HD_U_J7_pre10
            var optifineLib = manifest.Libraries.FirstOrDefault(l => l.Name.StartsWith("optifine:OptiFine"));
            if (optifineLib != null)
            {
                 _logger?.LogInformation($"[ModLoaderDetector] 命中特征库 (OptiFine): {optifineLib.Name}");
                 string fullVer = ParseVersion(optifineLib.Name);
                 return ("optifine", CleanOptifineVersion(fullVer));
            }

            return ("vanilla", string.Empty);
        }

        private string CleanOptifineVersion(string rawVersion)
        {
             // 输入: 1.21.1_HD_U_J7_pre10 => 输出: HD_U_J7_pre10
             // 输入: HD_U_G8 => 输出: HD_U_G8
             
             if (string.IsNullOrEmpty(rawVersion)) return string.Empty;

             // 找到第一个下划线
             int index = rawVersion.IndexOf('_');
             if (index > 0 && index < rawVersion.Length - 1)
             {
                 // 检查下划线前面是否像版本号 (包含点)
                 string prefix = rawVersion.Substring(0, index);
                 
                 // 简单的验证：前缀应该是数字开头，并且包含点（例如 1.8.9, 1.12.2）
                 // 避免误判诸如 "My_Version_1.0" 这样的字符串
                 if (char.IsDigit(prefix[0]) && (prefix.Contains(".") || prefix.All(c => char.IsDigit(c) || c == '.')))
                 {
                     return rawVersion.Substring(index + 1);
                 }
             }
             return rawVersion;
        }

        /// <summary>
        /// 从 maven 坐标提取版本: group:name:version
        /// </summary>
        private string ParseVersion(string mavenCoordinate)
        {
            if (string.IsNullOrEmpty(mavenCoordinate))
            {
                _logger?.LogWarning("[ModLoaderDetector] Maven 坐标为空或为 null");
                return string.Empty;
            }

            var parts = mavenCoordinate.Split(':');
            if (parts.Length >= 3)
            {
                return parts[2];
            }
            
            _logger?.LogWarning("[ModLoaderDetector] Maven 坐标格式不正确: {MavenCoordinate}", mavenCoordinate);
            return string.Empty;
        }

        /// <summary>
        /// 从参数列表中提取指定键的值
        /// </summary>
        private string? GetArgumentValue(List<object> args, string key)
        {
            for (int i = 0; i < args.Count - 1; i++)
            {
                // 参数可能是字符串，也可能是对象(规则)。我们只关心字符串形式的参数
                if (args[i] is string argKey && argKey == key)
                {
                    if (args[i + 1] is string val)
                    {
                        return val;
                    }
                }
                // Newtonsoft.Json 可能将字符串解析为 JValue，这里简单通过 ToString 兼容一下
                else if (args[i]?.ToString() == key)
                {
                    return args[i + 1]?.ToString();
                }
            }
            return null;
        }

        /// <summary>
        /// 从参数列表中提取 NeoForge 版本
        /// </summary>
        private string? GetNeoForgeVersionFromArguments(MinecraftVersionManifest manifest)
        {
            if (manifest.Arguments?.Game == null) return null;
            return GetArgumentValue(manifest.Arguments.Game, "--fml.neoForgeVersion");
        }

        /// <summary>
        /// 从参数列表中提取 Forge 版本
        /// </summary>
        private string? GetForgeVersionFromArguments(MinecraftVersionManifest manifest)
        {
            if (manifest.Arguments?.Game == null) return null;
            return GetArgumentValue(manifest.Arguments.Game, "--fml.forgeVersion");
        }

        /// <summary>
        /// 清洗 Forge 版本号
        /// 输入: 1.20.1-47.4.16 => 输出: 47.4.16
        /// 输入: 14.23.5.2854 => 输出: 14.23.5.2854
        /// 输入: 1.7.10-10.13.4.1558-1.7.10 => 输出: 10.13.4.1558
        /// </summary>
        private string CleanForgeVersion(string rawVersion)
        {
            if (string.IsNullOrEmpty(rawVersion)) return string.Empty;
            
            // 简单情况: 没有横杠
            if (!rawVersion.Contains("-")) return rawVersion;

            // 复杂情况: 处理 {mc}-{forge}-{mc} 或 {mc}-{forge} 格式
            var parts = rawVersion.Split('-');
            
            // 如果只有一段，直接返回
            if (parts.Length < 2) return rawVersion;

            string potentialMcVersion = parts[0];
            string result = rawVersion;

            // 1. 尝试去除前缀 {mc}-
            if (result.StartsWith(potentialMcVersion + "-"))
            {
                result = result.Substring(potentialMcVersion.Length + 1);
            }

            // 2. 尝试去除后缀 -{mc} (针对 1.7.10 等旧版)
            if (result.EndsWith("-" + potentialMcVersion))
            {
                result = result.Substring(0, result.Length - (potentialMcVersion.Length + 1));
            }

            return result;
        }
    }
}
