using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.VersionAnalysis.Models;

namespace XianYuLauncher.Core.VersionAnalysis
{
    /// <summary>
    /// 负责分析 .jar 文件中的 version.json 以提取绝对正确的原版信息
    /// </summary>
    public class JarAnalyzer
    {
        private readonly Microsoft.Extensions.Logging.ILogger? _logger;

        public JarAnalyzer(Microsoft.Extensions.Logging.ILogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<string> GetMinecraftVersionFromJarAsync(string versionDirectory, string versionId)
        {
            // 1. 尝试直接从 {id}/{id}.jar 读取
            string jarPath = Path.Combine(versionDirectory, $"{versionId}.jar");
            
            // 2. 如果不存在，检查是否继承了其他版本（这里需要外部逻辑先解析 JSON 拿到 inheritsFrom）
            // 该类专注于处理 JAR，所以如果调用者没给 JAR 路径，我们只能尽力找
            
            if (!File.Exists(jarPath))
            {
                // 如果没有 JAR 文件，这可能是继承版本（如 Fabric）
                // 此时我们应该返回 null，让调用者去处理继承逻辑
                return null;
            }

            return await Task.Run(() => 
            {
                try 
                {
                    using (var archive = ZipFile.OpenRead(jarPath))
                    {
                        var entry = archive.GetEntry("version.json");
                        if (entry != null)
                        {
                            using (var stream = entry.Open())
                            using (var reader = new StreamReader(stream))
                            {
                                string json = reader.ReadToEnd();
                                var info = JsonConvert.DeserializeObject<JarVersionInfo>(json);
                                
                                // version.json 中的 'name' 字段通常是 "1.20.1" 这种版本号
                                if (!string.IsNullOrEmpty(info?.Name))
                                {
                                    _logger?.LogInformation($"[JarAnalyzer] 从 .jar/version.json 提取到版本: {info.Name}");
                                    return info.Name;
                                }
                                // 在旧版本中可能是 id
                                if (!string.IsNullOrEmpty(info?.Id))
                                {
                                    _logger?.LogInformation($"[JarAnalyzer] 从 .jar/version.json 提取到ID: {info.Id}");
                                    return info.Id;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[JarAnalyzer] 分析 .jar 失败: {ex.Message}");
                }

                return null;
            });
        }
    }
}
