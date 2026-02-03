using Microsoft.Extensions.Options;
using Newtonsoft.Json;

using Windows.ApplicationModel;
using Windows.Storage;

using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models;

namespace XianYuLauncher.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private const string _defaultApplicationDataFolder = "ApplicationData";
    private const string _defaultLocalSettingsFile = "LocalSettings.json";

    private readonly IFileService _fileService;
    private readonly LocalSettingsOptions _options;

    private readonly string _applicationDataFolder;
    private readonly string _localsettingsFile;

    private IDictionary<string, object> _settings;

    private bool _isInitialized;

    public LocalSettingsService(IFileService fileService, IOptions<LocalSettingsOptions> options)
    {
        _fileService = fileService;
        _options = options.Value;

        // 使用安全路径，避免 MSIX 虚拟化问题
        _applicationDataFolder = Path.Combine(AppEnvironment.SafeAppDataPath, _options.ApplicationDataFolder ?? _defaultApplicationDataFolder);
        _localsettingsFile = _options.LocalSettingsFile ?? _defaultLocalSettingsFile;

        _settings = new Dictionary<string, object>();
    }

    private async Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _settings = await Task.Run(() => _fileService.Read<IDictionary<string, object>>(_applicationDataFolder, _localsettingsFile)) ?? new Dictionary<string, object>();

            _isInitialized = true;
        }
    }

    public async Task<T?> ReadSettingAsync<T>(string key)
    {
        if (RuntimeHelper.IsMSIX)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
            {
                // 特殊处理：自动修复 JavaSelectionMode 的错误格式
                if (key == "JavaSelectionMode" && obj is string strValue)
                {
                    var underlyingType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                    
                    // 检查目标类型是否为 int 或 int?
                    if (underlyingType == typeof(int))
                    {
                        // 先尝试反序列化 JSON 字符串（可能是 "\"Auto\"" 格式）
                        string actualValue = strValue;
                        try
                        {
                            actualValue = JsonConvert.DeserializeObject<string>(strValue) ?? strValue;
                        }
                        catch
                        {
                            // 如果反序列化失败，使用原始值
                        }
                        
                        // 如果是字符串 "Auto" 或 "Manual"，转换为整数并重新保存
                        if (string.Equals(actualValue, "Auto", StringComparison.OrdinalIgnoreCase))
                        {
                            await SaveSettingAsync(key, 0); // Auto = 0
                            return (T)(object)(typeof(T) == typeof(int?) ? (int?)0 : 0);
                        }
                        else if (string.Equals(actualValue, "Manual", StringComparison.OrdinalIgnoreCase))
                        {
                            await SaveSettingAsync(key, 1); // Manual = 1
                            return (T)(object)(typeof(T) == typeof(int?) ? (int?)1 : 1);
                        }
                    }
                }
                
                return await Json.ToObjectAsync<T>((string)obj);
            }
        }
        else
        {
            await InitializeAsync();

            if (_settings != null && _settings.TryGetValue(key, out var obj))
            {
                // 特殊处理：自动修复 JavaSelectionMode 的错误格式（非 MSIX 模式）
                if (key == "JavaSelectionMode" && obj is string strValue)
                {
                    var underlyingType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                    
                    // 检查目标类型是否为 int 或 int?
                    if (underlyingType == typeof(int))
                    {
                        // 如果是字符串 "Auto" 或 "Manual"，转换为整数并重新保存
                        if (string.Equals(strValue, "Auto", StringComparison.OrdinalIgnoreCase))
                        {
                            await SaveSettingAsync(key, 0); // Auto = 0
                            return (T)(object)(typeof(T) == typeof(int?) ? (int?)0 : 0);
                        }
                        else if (string.Equals(strValue, "Manual", StringComparison.OrdinalIgnoreCase))
                        {
                            await SaveSettingAsync(key, 1); // Manual = 1
                            return (T)(object)(typeof(T) == typeof(int?) ? (int?)1 : 1);
                        }
                    }
                }
                
                // 调试：检查读取的Java版本数据
                if (key == "JavaVersions")
                {
                    Console.WriteLine($"读取Java版本列表，类型: {obj.GetType().Name}");
                    if (obj is Newtonsoft.Json.Linq.JArray jArray)
                    {
                        Console.WriteLine($"  JArray元素数量: {jArray.Count}");
                        foreach (var item in jArray)
                        {
                            Console.WriteLine($"  - JObject: {item}");
                        }
                    }
                    else if (obj is List<object> list)
                    {
                        Console.WriteLine($"  List元素数量: {list.Count}");
                        foreach (var item in list)
                        {
                            Console.WriteLine($"  - {item.GetType().Name}: {item}");
                        }
                    }
                    else if (obj is List<XianYuLauncher.ViewModels.JavaVersionInfo> typedList)
                    {
                        Console.WriteLine($"  类型化List元素数量: {typedList.Count}");
                        foreach (var item in typedList)
                        {
                            Console.WriteLine($"  - {item}");
                        }
                    }
                }
                
                // 对于复杂类型，需要重新序列化再反序列化以确保类型正确
                if (obj is not T && obj != null)
                {
                    Console.WriteLine($"  类型不匹配，需要转换: {obj.GetType().Name} -> {typeof(T).Name}");
                    
                    try
                    {
                        var json = Newtonsoft.Json.JsonConvert.SerializeObject(obj, new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore });
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
                    }
                    catch (JsonException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LocalSettingsService] 类型转换失败 '{key}': {ex.Message}");
                        
                        // 尝试智能转换
                        if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
                        {
                            var objStr = obj?.ToString();
                            if (string.Equals(objStr, "Auto", StringComparison.OrdinalIgnoreCase))
                            {
                                System.Diagnostics.Debug.WriteLine($"[LocalSettingsService] 将 'Auto' 转换为 null (int?)");
                                return default;
                            }
                            
                            if (int.TryParse(objStr, out int intValue))
                            {
                                return (T)(object)intValue;
                            }
                        }
                        
                        return default;
                    }
                }
                return (T)obj;
            }
        }

        return default;
    }

    public async Task SaveSettingAsync<T>(string key, T value)
    {
        if (RuntimeHelper.IsMSIX)
        {
            ApplicationData.Current.LocalSettings.Values[key] = await Json.StringifyAsync(value);
        }
        else
        {
            await InitializeAsync();

            _settings[key] = value;
            
            // 调试：检查保存的Java版本数量
            if (key == "JavaVersions" && value is List<object> javaList)
            {
                Console.WriteLine($"保存Java版本列表，数量: {javaList.Count}");
                foreach (var item in javaList)
                {
                    Console.WriteLine($"  - {item.GetType().Name}: {item}");
                }
            }
            else if (key == "JavaVersions" && value is List<XianYuLauncher.ViewModels.JavaVersionInfo> typedList)
            {
                Console.WriteLine($"保存Java版本列表，数量: {typedList.Count}");
                foreach (var item in typedList)
                {
                    Console.WriteLine($"  - {item}");
                }
            }

            await Task.Run(() => _fileService.Save(_applicationDataFolder, _localsettingsFile, _settings));
        }
    }
}
