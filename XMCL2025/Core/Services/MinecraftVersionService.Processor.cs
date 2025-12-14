using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Contracts.Services;
using XMCL2025.Core.Services.DownloadSource;
using XMCL2025.ViewModels;
using XMCL2025.Core.Models;

namespace XMCL2025.Core.Services
{
    public partial class MinecraftVersionService
    {
        /// <summary>
        /// 执行单个通用处理器（支持Forge和NeoForge）
        /// </summary>
        private async Task ExecuteProcessor(JObject processor, string installerPath, string modLoaderVersionDirectory, string librariesDirectory, Action<double> progressCallback, string installProfilePath, string extractDirectory, string modLoaderType)
        {
            // 定义变量，以便在catch块中访问
            string jar = string.Empty;
            string mainClass = string.Empty;
            
            try
            {
                _logger.LogInformation("开始执行{ModLoaderType}处理器");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始执行{modLoaderType}处理器: {processor}");
                
                // 获取处理器信息
                jar = processor["jar"]?.ToString() ?? throw new Exception("处理器缺少jar字段");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 处理器jar: {jar}");
                
                JArray classpath = processor["classpath"]?.Value<JArray>() ?? throw new Exception("处理器缺少classpath字段");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 处理器classpath: {string.Join(", ", classpath.Select(cp => cp.ToString()))}");
                
                JArray args = processor["args"]?.Value<JArray>() ?? throw new Exception("处理器缺少args字段");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 处理器原始args: {string.Join(", ", args.Select(a => a.ToString()))}");
                
                _logger.LogInformation("处理器jar: {Jar}", jar);
                
                // 下载installertools
                string installerToolsPath = await DownloadInstallerTools(jar, librariesDirectory);
                
                // 获取主类
                mainClass = GetMainClassFromJar(installerToolsPath);
                _logger.LogInformation("处理器主类: {MainClass}", mainClass);
                
                // 处理参数
                List<string> processedArgs = new List<string>();
                string minecraftPath = Path.GetDirectoryName(librariesDirectory);
                
                string currentParam = null; // 跟踪当前处理的参数名
                bool isNextArgOptional = false;
                
                // 读取install_profile.json中的data字段，获取MC_OFF值
                JObject installProfile = JObject.Parse(File.ReadAllText(installProfilePath));
                JObject data = installProfile["data"]?.Value<JObject>() ?? new JObject();
                JObject mcOff = data["MC_OFF"]?.Value<JObject>() ?? new JObject();
                string mcOffClient = mcOff["client"]?.ToString() ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 从install_profile.json获取MC_OFF client值: {mcOffClient}");
                
                // 解析MC_OFF的Maven坐标，转换为本地路径
                string mcOffLocalPath = string.Empty;
                if (!string.IsNullOrEmpty(mcOffClient) && mcOffClient.StartsWith("[") && mcOffClient.EndsWith("]"))
                {
                    string mavenCoord = mcOffClient.Substring(1, mcOffClient.Length - 2);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 解析MC_OFF Maven坐标: {mavenCoord}");
                    
                    // 处理Maven坐标，转换为本地库路径
                    string[] coordParts = mavenCoord.Split(':');
                    if (coordParts.Length >= 4)
                    {
                        string groupId = coordParts[0].Replace('.', Path.DirectorySeparatorChar);
                        string artifactId = coordParts[1];
                        string version = coordParts[2];
                        string classifier = coordParts[3];
                        
                        string fileName = $"{artifactId}-{version}-{classifier}.jar";
                        mcOffLocalPath = Path.Combine(librariesDirectory, groupId, artifactId, version, fileName);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] MC_OFF转换为本地路径: {mcOffLocalPath}");
                    }
                }
                
                // 正确提取完整的ModLoader版本号（处理带-beta等后缀的情况）
                string minecraftVersion = "";
                string modLoaderVersion = "";
                
                // 优先从配置文件读取版本信息
                VersionConfig config = ReadVersionConfig(modLoaderVersionDirectory);
                if (config != null && config.ModLoaderType == modLoaderType)
                {
                    minecraftVersion = config.MinecraftVersion;
                    modLoaderVersion = config.ModLoaderVersion;
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 从配置文件提取的Minecraft版本: {minecraftVersion}");
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 从配置文件提取的完整{modLoaderType}版本: {modLoaderVersion}");
                }
                // 回退到旧的目录名称分割逻辑
                else
                {
                    string[] versionParts = Path.GetFileName(modLoaderVersionDirectory).Split('-');
                    if (versionParts.Length >= 3)
                    {
                        minecraftVersion = versionParts[1];
                        // 从第2个元素开始，将所有剩余元素合并为完整的ModLoader版本号
                        modLoaderVersion = string.Join("-", versionParts.Skip(2));
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 从目录名称提取的Minecraft版本: {minecraftVersion}");
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 从目录名称提取的完整{modLoaderType}版本: {modLoaderVersion}");
                    }
                }
                
                foreach (JToken argToken in args)
                {
                    string arg = argToken.ToString();
                    
                    // 检查是否为参数名（以--开头）
                    if (arg.StartsWith("--"))
                    {
                        currentParam = arg; // 记录当前参数名
                        processedArgs.Add(arg); // 添加参数名到结果列表
                        
                        // 处理--optional标记
                        if (arg == "--optional")
                        {
                            isNextArgOptional = true;
                        }
                        continue; // 继续处理下一个参数值
                    }
                    
                    // 当前是参数值，根据参数名进行特殊处理
                    string paramValue = arg;
                    
                    // 1. 替换标准占位符
                    paramValue = paramValue.Replace("{INSTALLER}", installerPath);
                    paramValue = paramValue.Replace("{ROOT}", minecraftPath);
                    paramValue = paramValue.Replace("{SIDE}", "client");
                    
                    // 替换MC_OFF占位符
                    if (!string.IsNullOrEmpty(mcOffLocalPath))
                    {
                        paramValue = paramValue.Replace("{MC_OFF}", mcOffLocalPath);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 替换{currentParam}参数中的{{MC_OFF}}为: {mcOffLocalPath}");
                    }
                    
                    if (!string.IsNullOrEmpty(minecraftVersion))
                    {
                        paramValue = paramValue.Replace("{MINECRAFT_JAR}", Path.Combine(modLoaderVersionDirectory, $"{Path.GetFileName(modLoaderVersionDirectory)}.jar"));
                        paramValue = paramValue.Replace("{MOJMAPS}", Path.Combine(librariesDirectory, "net", "minecraft", "client", minecraftVersion, $"client-{minecraftVersion}-mappings.txt"));
                    }
                    
                    // 2. 根据modLoaderType调整特定路径
                    string modLoaderGroup = modLoaderType == "neoforge" ? "net/neoforged" : "net/minecraftforge";
                    string modLoaderPrefix = modLoaderType == "neoforge" ? "neoforge" : "forge";
                    
                    // 替换PATCHED占位符 - 分别处理NeoForge和Forge的不同格式
                    if (modLoaderType == "neoforge")
                    {
                        // NeoForge使用minecraft-client-patched，而非neoforge-client-patched
                        paramValue = paramValue.Replace("{PATCHED}", Path.Combine(librariesDirectory, modLoaderGroup.Replace('/', Path.DirectorySeparatorChar), "minecraft-client-patched", modLoaderVersion, $"minecraft-client-patched-{modLoaderVersion}.jar"));
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] NeoForge PATCHED路径: {paramValue}");
                    }
                    else if (modLoaderType == "forge")
                    {
                        // 优先从install_profile.json的data字段获取PATCHED值
                        string patchedLocalPath = string.Empty;
                        JObject patched = data["PATCHED"]?.Value<JObject>() ?? new JObject();
                        string patchedClient = patched["client"]?.ToString() ?? string.Empty;
                        
                        if (!string.IsNullOrEmpty(patchedClient) && patchedClient.StartsWith("[") && patchedClient.EndsWith("]"))
                        {
                            string mavenCoord = patchedClient.Substring(1, patchedClient.Length - 2);
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] 从install_profile.json获取Forge PATCHED client值: {mavenCoord}");
                            
                            // 解析PATCHED的Maven坐标
                            string[] coordParts = mavenCoord.Split(':');
                            if (coordParts.Length >= 4)
                            {
                                string groupId = coordParts[0].Replace('.', Path.DirectorySeparatorChar);
                                string artifactId = coordParts[1];
                                string fullVersion = coordParts[2];
                                string classifier = coordParts[3];
                                
                                string fileName = $"{artifactId}-{fullVersion}-{classifier}.jar";
                                patchedLocalPath = Path.Combine(librariesDirectory, groupId, artifactId, fullVersion, fileName);
                                System.Diagnostics.Debug.WriteLine($"[DEBUG] 从Maven坐标构建的Forge PATCHED路径: {patchedLocalPath}");
                            }
                        }
                        
                        // 如果从install_profile获取失败，使用原有的解析逻辑作为回退
                        if (string.IsNullOrEmpty(patchedLocalPath))
                        {
                            // Forge使用特定的格式：net/minecraftforge/forge/{MC版本号}-{forge版本号}/forge-{MC版本号}-{forge版本号}-client.jar
                            // 注意：需要从modLoaderVersion中解析出完整的版本号
                            if (!string.IsNullOrEmpty(minecraftVersion) && !string.IsNullOrEmpty(modLoaderVersion))
                            {
                                string fullForgeVersion = $"{minecraftVersion}-{modLoaderVersion}";
                                patchedLocalPath = Path.Combine(librariesDirectory, modLoaderGroup.Replace('/', Path.DirectorySeparatorChar), "forge", fullForgeVersion, $"forge-{fullForgeVersion}-client.jar");
                                System.Diagnostics.Debug.WriteLine($"[DEBUG] 从版本信息构建的Forge PATCHED路径: {patchedLocalPath}");
                            }
                            else
                            {
                                // 最终回退方案
                                patchedLocalPath = paramValue.Replace("{PATCHED}", Path.Combine(librariesDirectory, modLoaderGroup.Replace('/', Path.DirectorySeparatorChar), "forge", modLoaderVersion, $"forge-{modLoaderVersion}-client.jar"));
                                System.Diagnostics.Debug.WriteLine($"[DEBUG] Forge PATCHED路径（最终回退）: {patchedLocalPath}");
                            }
                        }
                        
                        paramValue = paramValue.Replace("{PATCHED}", patchedLocalPath);
                    }
                    else
                    {
                        // 其他ModLoader类型使用默认格式
                        paramValue = paramValue.Replace("{PATCHED}", Path.Combine(librariesDirectory, modLoaderGroup.Replace('/', Path.DirectorySeparatorChar), $"{modLoaderPrefix}-client-patched", modLoaderVersion, $"{modLoaderPrefix}-client-patched-{modLoaderVersion}.jar"));
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 默认ModLoader PATCHED路径: {paramValue}");
                    }
                    
                    // 3. 直接使用临时目录中的client.lzma文件
                    // 尝试两种可能的路径: data/client.lzma (新路径) 和 data/client/client.lzma (旧路径)
                    string[] possibleClientLzmaPaths = {
                        Path.Combine(extractDirectory, "data", "client.lzma"),
                        Path.Combine(extractDirectory, "data", "client", "client.lzma")
                    };
                    string tempClientLzmaPath = possibleClientLzmaPaths.FirstOrDefault(p => File.Exists(p));
                    if (tempClientLzmaPath == null)
                    {
                        throw new Exception($"client.lzma文件不存在于临时目录，尝试了以下路径: {string.Join(", ", possibleClientLzmaPaths)}");
                    }
                    paramValue = paramValue.Replace("{BINPATCH}", tempClientLzmaPath);
                    paramValue = paramValue.Replace("{EXTRACT_FILES}", "EXTRACT_FILES");
                    paramValue = paramValue.Replace("{EXTRACT_TO}", Path.Combine(librariesDirectory, modLoaderGroup.Replace('/', Path.DirectorySeparatorChar), modLoaderType, modLoaderVersion));
                    
                    // 4. 处理Maven坐标格式的参数
                    if (paramValue.StartsWith("[") && paramValue.EndsWith("]"))
                    {
                        string mavenCoord = paramValue.Substring(1, paramValue.Length - 2);
                        string[] mavenParts = mavenCoord.Split('@');
                        string mainCoord = mavenParts[0];
                        string extension = mavenParts.Length > 1 ? mavenParts[1] : "jar";
                        
                        string[] coordParts = mainCoord.Split(':');
                        if (coordParts.Length >= 3)
                        {
                            string groupId = coordParts[0].Replace('.', Path.DirectorySeparatorChar);
                            string artifactId = coordParts[1];
                            string version = coordParts[2];
                            string classifier = coordParts.Length > 3 ? coordParts[3] : "";
                            
                            string fileName = $"{artifactId}-{version}";
                            if (!string.IsNullOrEmpty(classifier))
                            {
                                fileName += $"-{classifier}";
                            }
                            fileName += $".$extension";
                            
                            string fullPath = Path.Combine(librariesDirectory, groupId, artifactId, version, fileName);
                            paramValue = fullPath;
                        }
                    }
                    
                    // 5. 处理--neoform-data参数中的$extension占位符（NeoForge专用）
                    if (currentParam == "--neoform-data")
                    {
                        // 替换$extension占位符，确保只使用一个小数点
                        paramValue = paramValue.Replace(".$extension", ".tsrg.lzma");
                        paramValue = paramValue.Replace("$extension", ".tsrg.lzma");
                    }
                    
                    // 6. 处理--optional参数值
                    if (isNextArgOptional)
                    {
                        bool fileExists = File.Exists(paramValue);
                        paramValue = fileExists ? "1" : "0";
                        isNextArgOptional = false;
                    }
                    
                    // 7. Windows路径格式修正
                    if (paramValue.Contains("/") || paramValue.Contains("\\"))
                    {
                        // 将所有正斜杠转换为反斜杠
                        paramValue = paramValue.Replace("/", "\\");
                        // 移除路径末尾的反斜杠，避免ModLoader处理器错误
                        if (paramValue.EndsWith("\\"))
                        {
                            paramValue = paramValue.Substring(0, paramValue.Length - 1);
                        }
                        // 为路径添加双引号，避免空格和特殊字符问题
                        paramValue = $"\"{paramValue}\"";
                    }
                    
                    processedArgs.Add(paramValue); // 添加处理后的参数值
                    currentParam = null; // 重置当前参数名
                }
                
                _logger.LogInformation("处理器参数: {Args}", string.Join(" ", processedArgs));
                
                // 构建完整的classpath，包括所有依赖库
                List<string> fullClassPath = new List<string>();
                
                // 添加installerTools到classpath
                fullClassPath.Add(installerToolsPath);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 添加installerTools到classpath: {installerToolsPath}");
                
                // 添加classpath数组中的所有依赖库
                foreach (var classpathEntry in classpath)
                {
                    string classpathMavenCoord = classpathEntry.ToString();
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 处理classpath条目: {classpathMavenCoord}");
                    
                    // 将Maven坐标转换为本地文件路径
                    string libraryPath = GetLibraryFilePath(classpathMavenCoord, librariesDirectory);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 转换为本地路径: {libraryPath}");
                    
                    // 检查文件是否存在
                    if (File.Exists(libraryPath))
                    {
                        fullClassPath.Add(libraryPath);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 添加到classpath: {libraryPath}");
                    }
                    else
                    {
                        _logger.LogWarning("classpath文件不存在: {LibraryPath}，坐标: {ClasspathMavenCoord}", libraryPath, classpathMavenCoord);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 警告: classpath文件不存在: {libraryPath}，坐标: {classpathMavenCoord}");
                    }
                }
                
                // 使用平台特定的路径分隔符
                string classPathSeparator = Path.PathSeparator.ToString();
                string combinedClassPath = string.Join(classPathSeparator, fullClassPath);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 完整classpath: {combinedClassPath}");
                
                // 构建Java命令
                List<string> javaArgs = new List<string>();
                javaArgs.Add("-cp");
                javaArgs.Add(combinedClassPath);
                javaArgs.Add(mainClass);
                javaArgs.AddRange(processedArgs);
                
                // 查找Java可执行文件
                string javaPath = "java";
                try
                {
                    // 尝试从环境变量中获取Java路径
                    javaPath = Path.Combine(Environment.GetEnvironmentVariable("JAVA_HOME") ?? string.Empty, "bin", "java.exe");
                    if (!File.Exists(javaPath))
                    {
                        // 如果环境变量中没有，使用系统默认的java
                        javaPath = "java";
                    }
                }
                catch { }
                
                // 设置进程启动信息
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = string.Join(" ", javaArgs),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(librariesDirectory)
                };
                
                // 保存命令到日志文件
                string tempDirectory = Path.GetTempPath();
                string logFileName = $"{modLoaderType}-processor-{DateTime.Now:yyyyMMdd-HHmmss}.log";
                string logFilePath = Path.Combine(tempDirectory, logFileName);
                
                // 记录完整的执行上下文
                string fullContext = $"[{modLoaderType}处理器执行上下文]\n" +
                                   $"执行时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                   $"处理器Jar: {jar}\n" +
                                   $"主类: {mainClass}\n" +
                                   $"Java路径: {javaPath}\n" +
                                   $"工作目录: {processStartInfo.WorkingDirectory}\n" +
                                   $"完整参数列表:\n";
                
                for (int i = 0; i < javaArgs.Count; i++)
                {
                    fullContext += $"  [{i}]: {javaArgs[i]}\n";
                }
                
                fullContext += $"原始install_profile.json路径: {installProfilePath}\n" +
                            $"安装器路径: {installerPath}\n" +
                            $"{modLoaderType}版本目录: {modLoaderVersionDirectory}\n" +
                            $"库目录: {librariesDirectory}\n";
                
                // 创建进程并执行
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo = processStartInfo;
                    
                    // 捕获输出
                    var outputBuilder = new System.Text.StringBuilder();
                    var errorBuilder = new System.Text.StringBuilder();
                    
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                            _logger.LogInformation("处理器输出: {Output}", e.Data);
                        }
                    };
                    
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            errorBuilder.AppendLine(e.Data);
                            _logger.LogError("处理器错误: {Error}", e.Data);
                        }
                    };
                    
                    // 启动进程
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    
                    // 等待进程完成
                    process.WaitForExit();
                    
                    // 获取输出和错误
                    string output = outputBuilder.ToString();
                    string error = errorBuilder.ToString();
                    int exitCode = process.ExitCode;
                    
                    // 创建完整的日志内容
                    string logContent = fullContext +
                                       $"\n[{modLoaderType}处理器执行结果]\n" +
                                       $"退出代码: {exitCode}\n" +
                                       $"标准输出:\n{output}\n" +
                                       $"标准错误:\n{error}\n" +
                                       $"执行结果: {(exitCode == 0 ? "成功" : "失败")}\n";
                    
                    // 写入日志文件
                    File.WriteAllText(logFilePath, logContent);
                    _logger.LogInformation("处理器执行日志已保存到: {LogFilePath}", logFilePath);
                    
                    // 检查执行结果
                    if (exitCode != 0)
                    {
                        // 构建完整命令字符串
                        string fullCommand = $"{javaPath} {string.Join(" ", javaArgs)}";
                        
                        _logger.LogError("处理器执行失败，完整日志已保存到: {LogFilePath}", logFilePath);
                        _logger.LogError("完整执行命令: {FullCommand}", fullCommand);
                        
                        // 抛出包含完整命令的异常
                        throw new Exception($"Java命令执行失败，退出代码: {exitCode}\n" +
                                          $"完整命令: {fullCommand}\n" +
                                          $"详细日志已保存到: {logFilePath}\n" +
                                          $"错误信息: {error}");
                    }
                }
                
                _logger.LogInformation("Java命令执行完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行{ModLoaderType}处理器失败", modLoaderType);
                
                // 保存失败日志
                string tempDirectory = Path.GetTempPath();
                string logFileName = $"{modLoaderType}-processor-{DateTime.Now:yyyyMMdd-HHmmss}-error.log";
                string logFilePath = Path.Combine(tempDirectory, logFileName);
                
                // 创建失败日志内容
                string logContent = $"[{modLoaderType}处理器执行日志]\n" +
                                   $"执行时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                   $"处理器Jar: {jar}\n" +
                                   $"主类: {mainClass}\n" +
                                   $"执行结果: 失败\n" +
                                   $"错误信息: {ex.Message}\n" +
                                   $"堆栈跟踪: {ex.StackTrace}\n";
                
                // 写入日志文件
                File.WriteAllText(logFilePath, logContent);
                _logger.LogInformation("处理器执行失败日志已保存到: {LogFilePath}", logFilePath);
                
                throw new Exception($"执行{modLoaderType}处理器失败", ex);
            }
        }
        
        /// <summary>
        /// 下载installertools
        /// </summary>
        private async Task<string> DownloadInstallerTools(string jarName, string librariesDirectory)
        {
            // 定义变量，以便在catch块中访问
            string downloadSourceName = string.Empty;
            string downloadUrl = string.Empty;
            string mavenCoordinate = string.Empty;
            
            try
            {
                _logger.LogInformation("开始下载installertools: {JarName}", jarName);
                
                // 添加Debug输出
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始下载installertools: {jarName}");
                
                // 处理jar名称中的$extension占位符
                string processedJarName = jarName.Replace("$extension", "jar");
                
                // 解析jar名称：groupId:artifactId:version:classifier
                string[] parts = processedJarName.Split(':');
                string groupId = string.Empty;
                string artifactId = string.Empty;
                string version = string.Empty;
                string classifier = string.Empty;
                
                // 处理不同的jar名称格式
                if (parts.Length < 3)
                {
                    throw new Exception($"无效的jar名称格式: {processedJarName}，格式应为groupId:artifactId:version[:classifier]");
                }
                
                groupId = parts[0];
                artifactId = parts[1];
                version = parts[2];
                classifier = parts.Length >= 4 ? parts[3] : "";
                
                // 构建本地文件路径
                string libraryPath = GetLibraryFilePath(processedJarName, librariesDirectory, classifier);
                
                // 如果文件已存在，直接返回
                if (File.Exists(libraryPath))
                {
                    _logger.LogInformation("installertools已存在: {LibraryPath}", libraryPath);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] installertools已存在: {libraryPath}");
                    return libraryPath;
                }
                
                // 构建Maven坐标
                mavenCoordinate = parts.Length >= 4 
                    ? $"{groupId}:{artifactId}:{version}:{classifier}" 
                    : $"{groupId}:{artifactId}:{version}";
                
                // 获取当前下载源设置
                var downloadSourceType = await _localSettingsService.ReadSettingAsync<ViewModels.SettingsViewModel.DownloadSourceType>("DownloadSource");
                var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToString().ToLower());
                downloadSourceName = downloadSource.Name;
                
                // 使用下载源获取正确的下载URL
                downloadUrl = downloadSource.GetLibraryUrl(mavenCoordinate);
                
                // 修复URL中的$extension占位符
                downloadUrl = downloadUrl.Replace("$extension", "jar");
                
                _logger.LogInformation("使用下载源 {DownloadSource} 下载installertools，URL: {DownloadUrl}", downloadSourceName, downloadUrl);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 使用下载源 {downloadSourceName} 下载installertools: {downloadUrl}");
                
                // 下载文件
                await DownloadLibraryFileAsync(new DownloadFile { Url = downloadUrl }, libraryPath, mavenCoordinate);
                
                _logger.LogInformation("installertools下载完成: {LibraryPath}", libraryPath);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] installertools下载完成: {libraryPath}");
                return libraryPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下载installertools失败: {JarName}，处理后名称: {ProcessedJarName}，下载源: {DownloadSource}，URL: {DownloadUrl}，Maven坐标: {MavenCoordinate}", 
                    jarName, jarName.Replace("$extension", "jar"), downloadSourceName, downloadUrl, mavenCoordinate);
                
                throw new Exception($"下载installertools失败: {jarName}\n" +
                                  $"详细信息:\n" +
                                  $"  原始jar名称: {jarName}\n" +
                                  $"  处理后jar名称: {jarName.Replace("$extension", "jar")}\n" +
                                  $"  下载源: {downloadSourceName}\n" +
                                  $"  Maven坐标: {mavenCoordinate}\n" +
                                  $"  完整URL: {downloadUrl}\n" +
                                  $"  错误类型: {ex.GetType().Name}\n" +
                                  $"  错误信息: {ex.Message}\n" +
                                  $"  堆栈跟踪: {ex.StackTrace}", ex);
            }
        }
        
        /// <summary>
        /// 检查ZIP文件是否有效
        /// </summary>
        private bool IsZipFileValid(string zipFilePath)
        {
            try
            {
                if (!File.Exists(zipFilePath))
                {
                    return false;
                }
                
                // 尝试打开ZIP文件并读取其条目
                using (var archive = ZipFile.OpenRead(zipFilePath))
                {
                    // 检查是否有至少一个条目
                    return archive.Entries.Count > 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ZIP文件无效: {ZipFilePath}", zipFilePath);
                return false;
            }
        }
        
        /// <summary>
        /// 从jar文件中获取主类
        /// </summary>
        private string GetMainClassFromJar(string jarPath)
        {
            try
            {
                _logger.LogInformation("开始从jar文件中获取主类: {JarPath}", jarPath);
                
                using (var archive = ZipFile.OpenRead(jarPath))
                {
                    var manifestEntry = archive.GetEntry("META-INF/MANIFEST.MF");
                    if (manifestEntry == null)
                    {
                        throw new Exception($"jar文件中未找到META-INF/MANIFEST.MF: {jarPath}");
                    }
                    
                    using (var stream = manifestEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        while (!reader.EndOfStream)
                        {
                            string line = reader.ReadLine();
                            if (line != null && line.StartsWith("Main-Class:", StringComparison.OrdinalIgnoreCase))
                            {
                                string mainClass = line.Substring("Main-Class:".Length).Trim();
                                _logger.LogInformation("获取到主类: {MainClass}", mainClass);
                                return mainClass;
                            }
                        }
                    }
                }
                
                throw new Exception($"jar文件的MANIFEST.MF中未找到Main-Class字段: {jarPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从jar文件中获取主类失败: {JarPath}", jarPath);
                throw new Exception($"从jar文件中获取主类失败: {jarPath}", ex);
            }
        }
    }
}