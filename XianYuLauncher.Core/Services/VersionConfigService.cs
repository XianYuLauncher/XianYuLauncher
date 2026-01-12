using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 自定义 JSON 转换器，处理 "Auto" 字符串转整数的情况
/// </summary>
public class AutoToIntConverter : JsonConverter<int>
{
    private readonly int _defaultValue;
    
    public AutoToIntConverter(int defaultValue = 0)
    {
        _defaultValue = defaultValue;
    }
    
    public override int ReadJson(JsonReader reader, Type objectType, int existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            var stringValue = reader.Value?.ToString();
            if (string.Equals(stringValue, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return _defaultValue;
            }
            
            if (int.TryParse(stringValue, out int result))
            {
                return result;
            }
            
            return _defaultValue;
        }
        
        if (reader.TokenType == JsonToken.Integer)
        {
            return Convert.ToInt32(reader.Value);
        }
        
        return _defaultValue;
    }
    
    public override void WriteJson(JsonWriter writer, int value, JsonSerializer serializer)
    {
        writer.WriteValue(value);
    }
}

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
            
            // 使用宽松的反序列化设置，处理第三方启动器的配置文件
            var settings = new JsonSerializerSettings
            {
                Error = (sender, args) =>
                {
                    // 记录错误但继续处理
                    System.Diagnostics.Debug.WriteLine($"[VersionConfigService] JSON 反序列化警告: {args.ErrorContext.Error.Message}");
                    args.ErrorContext.Handled = true;
                },
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };
            
            // 首先尝试手动解析 JSON，处理特殊字段
            try
            {
                var jObject = JObject.Parse(json);
                var config = new VersionConfig();
                
                // 手动处理每个字段，提供容错
                if (jObject["ModLoaderType"] != null)
                    config.ModLoaderType = jObject["ModLoaderType"]?.ToString() ?? string.Empty;
                
                if (jObject["ModLoaderVersion"] != null)
                    config.ModLoaderVersion = jObject["ModLoaderVersion"]?.ToString() ?? string.Empty;
                
                if (jObject["MinecraftVersion"] != null)
                    config.MinecraftVersion = jObject["MinecraftVersion"]?.ToString() ?? string.Empty;
                
                if (jObject["OptifineVersion"] != null)
                    config.OptifineVersion = jObject["OptifineVersion"]?.ToString();
                
                if (jObject["AutoMemoryAllocation"] != null && jObject["AutoMemoryAllocation"].Type == JTokenType.Boolean)
                    config.AutoMemoryAllocation = jObject["AutoMemoryAllocation"].Value<bool>();
                
                if (jObject["InitialHeapMemory"] != null && jObject["InitialHeapMemory"].Type == JTokenType.Float)
                    config.InitialHeapMemory = jObject["InitialHeapMemory"].Value<double>();
                
                if (jObject["MaximumHeapMemory"] != null && jObject["MaximumHeapMemory"].Type == JTokenType.Float)
                    config.MaximumHeapMemory = jObject["MaximumHeapMemory"].Value<double>();
                
                if (jObject["JavaPath"] != null)
                    config.JavaPath = jObject["JavaPath"]?.ToString() ?? string.Empty;
                
                if (jObject["UseGlobalJavaSetting"] != null && jObject["UseGlobalJavaSetting"].Type == JTokenType.Boolean)
                    config.UseGlobalJavaSetting = jObject["UseGlobalJavaSetting"].Value<bool>();
                
                // 处理 WindowWidth - 可能是 "Auto" 字符串或整数
                if (jObject["WindowWidth"] != null)
                {
                    var widthToken = jObject["WindowWidth"];
                    if (widthToken.Type == JTokenType.String)
                    {
                        var widthStr = widthToken.ToString();
                        if (string.Equals(widthStr, "Auto", StringComparison.OrdinalIgnoreCase))
                        {
                            config.WindowWidth = 1280; // 默认值
                        }
                        else if (int.TryParse(widthStr, out int width))
                        {
                            config.WindowWidth = width;
                        }
                    }
                    else if (widthToken.Type == JTokenType.Integer)
                    {
                        config.WindowWidth = widthToken.Value<int>();
                    }
                }
                
                // 处理 WindowHeight - 可能是 "Auto" 字符串或整数
                if (jObject["WindowHeight"] != null)
                {
                    var heightToken = jObject["WindowHeight"];
                    if (heightToken.Type == JTokenType.String)
                    {
                        var heightStr = heightToken.ToString();
                        if (string.Equals(heightStr, "Auto", StringComparison.OrdinalIgnoreCase))
                        {
                            config.WindowHeight = 720; // 默认值
                        }
                        else if (int.TryParse(heightStr, out int height))
                        {
                            config.WindowHeight = height;
                        }
                    }
                    else if (heightToken.Type == JTokenType.Integer)
                    {
                        config.WindowHeight = heightToken.Value<int>();
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[VersionConfigService] 成功加载配置: {versionName}");
                System.Diagnostics.Debug.WriteLine($"[VersionConfigService] 窗口大小: {config.WindowWidth}x{config.WindowHeight}");
                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionConfigService] 手动解析失败，尝试标准反序列化: {ex.Message}");
                
                // 回退到标准反序列化
                var config = JsonConvert.DeserializeObject<VersionConfig>(json, settings);
                if (config != null)
                {
                    return config;
                }
            }
            
            return new VersionConfig();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VersionConfigService] 加载配置失败: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[VersionConfigService] 堆栈跟踪: {ex.StackTrace}");
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
