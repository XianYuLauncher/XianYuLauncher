using Microsoft.Win32;
using System.Diagnostics;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Java 运行时管理服务实现
/// </summary>
public class JavaRuntimeService : IJavaRuntimeService
{
    private readonly ILocalSettingsService _localSettingsService;
    
    private const string JavaPathKey = "JavaPath";
    private const string JavaSelectionModeKey = "JavaSelectionMode";
    private const string JavaVersionsKey = "JavaVersions";
    private const string SelectedJavaVersionKey = "SelectedJavaVersion";

    public JavaRuntimeService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    /// <summary>
    /// 检测系统中所有已安装的 Java 版本
    /// </summary>
    /// <param name="forceRefresh">是否强制刷新（重新执行 java -version），默认 false 使用缓存</param>
    public async Task<List<JavaVersion>> DetectJavaVersionsAsync(bool forceRefresh = false)
    {
        // 如果不强制刷新，先尝试从缓存加载
        if (!forceRefresh)
        {
            var cachedVersions = await _localSettingsService.ReadSettingAsync<List<JavaVersion>>(JavaVersionsKey);
            if (cachedVersions != null && cachedVersions.Count > 0)
            {
                // 验证缓存的 Java 路径是否仍然存在
                var validVersions = cachedVersions.Where(j => File.Exists(j.Path)).ToList();
                if (validVersions.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 从缓存加载 {validVersions.Count} 个 Java 版本");
                    return validVersions;
                }
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 开始检测 Java 版本（执行 java -version）...");
        var javaVersions = new List<JavaVersion>();
        
        // 1. 从注册表检测 JRE（使用 java -version 获取真实版本）
        await DetectFromRegistryAsync(javaVersions, @"SOFTWARE\JavaSoft\Java Runtime Environment", false);
        
        // 2. 从注册表检测 JDK（使用 java -version 获取真实版本）
        await DetectFromRegistryAsync(javaVersions, @"SOFTWARE\JavaSoft\Java Development Kit", true);
        
        // 3. 从环境变量检测（使用 java -version 获取真实版本）
        await DetectFromEnvironmentVariableAsync(javaVersions);
        
        // 4. 从常见路径检测（使用 java -version 获取真实版本）
        await DetectFromCommonPathsAsync(javaVersions);
        
        // 5. 保存检测结果到缓存
        if (javaVersions.Count > 0)
        {
            await _localSettingsService.SaveSettingAsync(JavaVersionsKey, javaVersions);
            System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 已缓存 {javaVersions.Count} 个 Java 版本");
        }
        
        return javaVersions;
    }

    /// <summary>
    /// 根据版本要求选择最佳 Java 运行时
    /// </summary>
    public async Task<string?> SelectBestJavaAsync(int requiredMajorVersion, string? versionSpecificPath = null)
    {
        System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 开始选择 Java，需要版本: {requiredMajorVersion}");
        
        // 1. 如果指定了版本特定路径且有效，优先使用
        if (!string.IsNullOrEmpty(versionSpecificPath) && File.Exists(versionSpecificPath))
        {
            System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 使用版本特定路径: {versionSpecificPath}");
            return versionSpecificPath;
        }
        
        // 2. 获取 Java 选择模式
        var javaSelectionMode = await _localSettingsService.ReadSettingAsync<int?>(JavaSelectionModeKey);
        var isAutoMode = javaSelectionMode == null || javaSelectionMode == 0; // 0 = Auto, 1 = Manual
        System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] Java 选择模式: {(isAutoMode ? "自动" : "手动")} (值: {javaSelectionMode})");
        
        // 3. 检测所有 Java 版本
        var javaVersions = await DetectJavaVersionsAsync();
        System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 检测到 {javaVersions.Count} 个 Java 版本:");
        foreach (var jv in javaVersions)
        {
            System.Diagnostics.Debug.WriteLine($"  - Java {jv.MajorVersion} ({jv.FullVersion}) {(jv.IsJDK ? "JDK" : "JRE")} - {jv.Path}");
        }
        
        if (isAutoMode)
        {
            System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 自动模式：正在选择最匹配的 Java {requiredMajorVersion}...");
            
            // 自动模式：选择最匹配的版本
            // 排除版本解析失败的 Java (MajorVersion = 0)
            var matchingJava = javaVersions
                .Where(j => File.Exists(j.Path) && j.MajorVersion > 0) // 排除解析失败的版本
                .OrderByDescending(j => j.MajorVersion == requiredMajorVersion) // 优先完全匹配
                .ThenByDescending(j => j.IsJDK) // 然后优先 JDK
                .ThenBy(j => Math.Abs(j.MajorVersion - requiredMajorVersion)) // 然后选择最接近的版本
                .FirstOrDefault();
            
            if (matchingJava != null)
            {
                System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] ✓ 自动选择: Java {matchingJava.MajorVersion} - {matchingJava.Path}");
                return matchingJava.Path;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] ✗ 自动模式未找到匹配的 Java");
            }
        }
        else
        {
            // 手动模式：使用用户选择的版本
            var selectedJavaPath = await _localSettingsService.ReadSettingAsync<string>(SelectedJavaVersionKey);
            System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 手动模式：用户选择的路径: {selectedJavaPath ?? "(未设置)"}");
            
            if (!string.IsNullOrEmpty(selectedJavaPath))
            {
                var selectedJava = javaVersions.FirstOrDefault(j => j.Path.Equals(selectedJavaPath, StringComparison.OrdinalIgnoreCase));
                if (selectedJava != null && File.Exists(selectedJava.Path))
                {
                    System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] ✓ 使用手动选择: Java {selectedJava.MajorVersion} - {selectedJava.Path}");
                    return selectedJava.Path;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] ✗ 手动选择的 Java 不存在或未在检测列表中");
                }
            }
        }
        
        // 4. 兼容旧版：检查用户自定义的 Java 路径
        var customJavaPath = await _localSettingsService.ReadSettingAsync<string>(JavaPathKey);
        if (!string.IsNullOrEmpty(customJavaPath))
        {
            System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 检查旧版自定义路径: {customJavaPath}");
            if (File.Exists(customJavaPath))
            {
                System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] ✓ 使用旧版自定义路径");
                return customJavaPath;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] ✗ 旧版自定义路径不存在");
            }
        }
        
        // 5. 从环境变量获取
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var javaPath = Path.Combine(javaHome, "bin", "java.exe");
            System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 检查 JAVA_HOME 环境变量: {javaPath}");
            if (File.Exists(javaPath))
            {
                System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] ✓ 使用 JAVA_HOME");
                return javaPath;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] ✗ JAVA_HOME 路径不存在");
            }
        }
        
        // 6. 未找到合适的 Java
        System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] ✗ 未找到任何可用的 Java");
        return null;
    }

    /// <summary>
    /// 验证 Java 路径是否有效
    /// </summary>
    public Task<bool> ValidateJavaPathAsync(string javaPath)
    {
        if (string.IsNullOrEmpty(javaPath))
        {
            return Task.FromResult(false);
        }
        
        // 检查文件是否存在且是 java.exe 或 javaw.exe
        var fileName = Path.GetFileName(javaPath).ToLowerInvariant();
        var isValid = File.Exists(javaPath) && (fileName == "java.exe" || fileName == "javaw.exe");
        
        return Task.FromResult(isValid);
    }

    /// <summary>
    /// 获取指定路径的 Java 版本信息
    /// </summary>
    public async Task<JavaVersion?> GetJavaVersionInfoAsync(string javaPath)
    {
        if (!await ValidateJavaPathAsync(javaPath))
        {
            return null;
        }

        try
        {
            // 执行 java -version 获取版本信息
            var startInfo = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = "-version",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            var output = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // 解析版本信息
            var lines = output.Split('\n');
            if (lines.Length > 0)
            {
                var versionLine = lines[0];
                // 示例: java version "1.8.0_301" 或 openjdk version "17.0.1"
                var match = System.Text.RegularExpressions.Regex.Match(versionLine, "\"(.+?)\"");
                if (match.Success)
                {
                    var fullVersion = match.Groups[1].Value;
                    TryParseJavaVersion(fullVersion, out int majorVersion);

                    var javaHome = Path.GetDirectoryName(Path.GetDirectoryName(javaPath));
                    var isJDK = javaHome != null && Directory.Exists(Path.Combine(javaHome, "lib"));
                    var is64Bit = output.Contains("64-Bit") || Environment.Is64BitOperatingSystem;

                    return new JavaVersion
                    {
                        Path = javaPath,
                        FullVersion = fullVersion,
                        MajorVersion = majorVersion,
                        IsJDK = isJDK,
                        Is64Bit = is64Bit
                    };
                }
            }
        }
        catch
        {
            // 忽略执行错误
        }

        return null;
    }

    /// <summary>
    /// 解析 Java 版本号
    /// </summary>
    public bool TryParseJavaVersion(string versionString, out int majorVersion)
    {
        majorVersion = 0;
        
        if (string.IsNullOrEmpty(versionString))
        {
            return false;
        }
        
        try
        {
            // 格式1: 1.8.0_301 (旧版本格式)
            if (versionString.StartsWith("1."))
            {
                var parts = versionString.Split('.');
                if (parts.Length >= 2)
                {
                    var success = int.TryParse(parts[1], out majorVersion);
                    System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 解析版本 '{versionString}' (旧格式) -> MajorVersion={majorVersion}");
                    return success;
                }
            }
            // 格式2: 17.0.1 或 17 (新版本格式)
            else
            {
                var parts = versionString.Split('.');
                var success = int.TryParse(parts[0], out majorVersion);
                System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 解析版本 '{versionString}' (新格式) -> MajorVersion={majorVersion}");
                return success;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 解析版本 '{versionString}' 失败: {ex.Message}");
        }
        
        return false;
    }

    #region Private Helper Methods

    /// <summary>
    /// 从注册表检测 Java 版本
    /// </summary>
    private async Task DetectFromRegistryAsync(List<JavaVersion> javaVersions, string registryPath, bool isJDK)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var javaKey = baseKey.OpenSubKey(registryPath);
            
            if (javaKey == null) return;
            
            var versions = javaKey.GetSubKeyNames();
            foreach (var version in versions)
            {
                using var versionKey = javaKey.OpenSubKey(version);
                if (versionKey == null) continue;
                
                var javaHomePath = versionKey.GetValue("JavaHome") as string;
                
                if (string.IsNullOrEmpty(javaHomePath)) continue;
                
                var javaPath = Path.Combine(javaHomePath, "bin", "java.exe");
                if (!File.Exists(javaPath)) continue;
                
                // 避免重复添加
                if (javaVersions.Any(j => j.Path.Equals(javaPath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                
                // 通过执行 java -version 获取真实版本信息
                var javaVersion = await GetJavaVersionInfoAsync(javaPath);
                if (javaVersion != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 从注册表检测到 Java: {javaVersion.MajorVersion} ({javaVersion.FullVersion}) - {javaPath}");
                    javaVersions.Add(javaVersion);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 注册表检测出错 {registryPath}: {ex.Message}");
        }
    }

    /// <summary>
    /// 从环境变量检测 Java
    /// </summary>
    private async Task DetectFromEnvironmentVariableAsync(List<JavaVersion> javaVersions)
    {
        try
        {
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (string.IsNullOrEmpty(javaHome)) return;
            
            var javaPath = Path.Combine(javaHome, "bin", "java.exe");
            if (!File.Exists(javaPath)) return;
            
            // 避免重复添加
            if (javaVersions.Any(j => j.Path.Equals(javaPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            
            // 通过执行 java -version 获取真实版本信息
            var javaVersion = await GetJavaVersionInfoAsync(javaPath);
            if (javaVersion != null)
            {
                System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 从 JAVA_HOME 检测到 Java: {javaVersion.MajorVersion} ({javaVersion.FullVersion}) - {javaPath}");
                javaVersions.Add(javaVersion);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] JAVA_HOME 检测出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 从常见路径检测 Java
    /// </summary>
    private async Task DetectFromCommonPathsAsync(List<JavaVersion> javaVersions)
    {
        var commonBasePaths = new[]
        {
            @"C:\Program Files\Java",
            @"C:\Program Files (x86)\Java",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdks") // IntelliJ IDEA 的 JDK 目录
        };
        
        foreach (var basePath in commonBasePaths)
        {
            if (!Directory.Exists(basePath)) continue;
            
            try
            {
                var directories = Directory.GetDirectories(basePath);
                foreach (var dir in directories)
                {
                    var javaPath = Path.Combine(dir, "bin", "java.exe");
                    if (!File.Exists(javaPath)) continue;
                    
                    // 避免重复添加
                    if (javaVersions.Any(j => j.Path.Equals(javaPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                    
                    // 通过执行 java -version 获取真实版本信息
                    var javaVersion = await GetJavaVersionInfoAsync(javaPath);
                    if (javaVersion != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 检测到 Java: {javaVersion.MajorVersion} ({javaVersion.FullVersion}) - {javaPath}");
                        javaVersions.Add(javaVersion);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 无法获取版本信息: {javaPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JavaRuntimeService] 检测路径出错 {basePath}: {ex.Message}");
            }
        }
    }

    #endregion
}
