using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 版本配置服务实现
/// </summary>
public class VersionConfigService : IVersionConfigService
{
    private readonly IFileService _fileService;
    
    public VersionConfigService(IFileService fileService)
    {
        _fileService = fileService;
    }
    
    /// <summary>
    /// 加载版本配置
    /// </summary>
    public async Task<VersionConfig> LoadConfigAsync(string versionName)
    {
        try
        {
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var configPath = Path.Combine(minecraftPath, "versions", versionName, "XianYuL.cfg");
            
            if (!File.Exists(configPath))
            {
                System.Diagnostics.Debug.WriteLine($"[VersionConfigService] 配置文件不存在，返回默认配置: {configPath}");
                return new VersionConfig();
            }
            
            var json = await File.ReadAllTextAsync(configPath);
            
            // 首先尝试直接反序列化
            var config = JsonConvert.DeserializeObject<VersionConfig>(json);
            if (config != null)
            {
                // 使用 dynamic 处理可能的属性名大小写不一致问题（兼容旧配置文件）
                try
                {
                    var dynamicSettings = JsonConvert.DeserializeObject<dynamic>(json);
                    if (dynamicSettings != null)
                    {
                        // 尝试不同的属性名大小写
                        bool? useGlobalSetting = null;
                        string? configJavaPath = null;
                        
                        try { useGlobalSetting = dynamicSettings.UseGlobalJavaSetting; } catch { }
                        if (!useGlobalSetting.HasValue) try { useGlobalSetting = dynamicSettings.useGlobalJavaSetting; } catch { }
                        if (!useGlobalSetting.HasValue) try { useGlobalSetting = dynamicSettings.useglobaljavasetting; } catch { }
                        
                        try { configJavaPath = dynamicSettings.JavaPath; } catch { }
                        if (configJavaPath == null) try { configJavaPath = dynamicSettings.javaPath; } catch { }
                        if (configJavaPath == null) try { configJavaPath = dynamicSettings.javapath; } catch { }
                        
                        // 使用获取到的值覆盖默认值
                        if (useGlobalSetting.HasValue)
                        {
                            config.UseGlobalJavaSetting = useGlobalSetting.Value;
                        }
                        if (!string.IsNullOrEmpty(configJavaPath))
                        {
                            config.JavaPath = configJavaPath;
                        }
                    }
                }
                catch
                {
                    // 忽略 dynamic 解析错误，使用直接反序列化的结果
                }
                
                System.Diagnostics.Debug.WriteLine($"[VersionConfigService] 成功加载配置: {versionName}");
                return config;
            }
            
            return new VersionConfig();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VersionConfigService] 加载配置失败: {ex.Message}");
            return new VersionConfig();
        }
    }
    
    /// <summary>
    /// 保存版本配置
    /// </summary>
    public async Task SaveConfigAsync(string versionName, VersionConfig config)
    {
        try
        {
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var versionDir = Path.Combine(minecraftPath, "versions", versionName);
            
            if (!Directory.Exists(versionDir))
            {
                Directory.CreateDirectory(versionDir);
            }
            
            var configPath = Path.Combine(versionDir, "XianYuL.cfg");
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            await File.WriteAllTextAsync(configPath, json);
            
            System.Diagnostics.Debug.WriteLine($"[VersionConfigService] 成功保存配置: {versionName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VersionConfigService] 保存配置失败: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 验证配置
    /// </summary>
    public ValidationResult ValidateConfig(VersionConfig config)
    {
        var result = new ValidationResult { IsValid = true };
        
        // 验证内存设置
        if (config.InitialHeapMemory < 0.5 || config.InitialHeapMemory > 64)
        {
            result.Warnings.Add($"初始堆内存设置异常: {config.InitialHeapMemory} GB");
        }
        
        if (config.MaximumHeapMemory < 1 || config.MaximumHeapMemory > 64)
        {
            result.Warnings.Add($"最大堆内存设置异常: {config.MaximumHeapMemory} GB");
        }
        
        if (config.InitialHeapMemory > config.MaximumHeapMemory)
        {
            result.Errors.Add("初始堆内存不能大于最大堆内存");
            result.IsValid = false;
        }
        
        // 验证窗口大小
        if (config.WindowWidth < 640 || config.WindowWidth > 7680)
        {
            result.Warnings.Add($"窗口宽度设置异常: {config.WindowWidth}");
        }
        
        if (config.WindowHeight < 480 || config.WindowHeight > 4320)
        {
            result.Warnings.Add($"窗口高度设置异常: {config.WindowHeight}");
        }
        
        return result;
    }
}
