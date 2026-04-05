using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Globalization;

using Windows.ApplicationModel;
using Windows.Storage;

using Serilog;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models;
using XianYuLauncher.Shared.Models;

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

        System.Diagnostics.Debug.WriteLine($"[LocalSettingsService] Settings path: {Path.Combine(_applicationDataFolder, _localsettingsFile)}");
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
                LogJavaSelectionModeRead<T>(key, obj, storageMode: "MSIX", stage: "RawRead");

                // 特殊处理：兼容读取 JavaSelectionMode 的历史存储格式，但不在读取时改写存储。
                if (TryReadJavaSelectionModeCompatibilityValue(key, obj, out T? compatibilityValue, out var compatibilityResolution))
                {
                    Log.Warning("[LocalSettings.JavaSelectionMode] Compatibility read applied. StorageMode=MSIX; Resolution={Resolution}; RequestedType={RequestedType}; RawType={RawType}; RawValue={RawValue}", compatibilityResolution, typeof(T).FullName, obj.GetType().FullName, obj);
                    LogJavaSelectionModeResolved(key, typeof(T), compatibilityValue, storageMode: "MSIX", resolution: compatibilityResolution);
                    return compatibilityValue;
                }

                if (TryReadDirectValue(obj, out T? directValue))
                {
                    LogJavaSelectionModeResolved(key, typeof(T), directValue, storageMode: "MSIX", resolution: "DirectValue");
                    return directValue;
                }
                
                if (obj is string jsonString)
                {
                    var parsedValue = await Json.ToObjectAsync<T>(jsonString);
                    LogJavaSelectionModeResolved(key, typeof(T), parsedValue, storageMode: "MSIX", resolution: "JsonString");
                    return parsedValue;
                }

                LogJavaSelectionModeUnresolved<T>(key, obj, storageMode: "MSIX");
            }
        }
        else
        {
            await InitializeAsync();

            if (_settings != null && _settings.TryGetValue(key, out var obj))
            {
                LogJavaSelectionModeRead<T>(key, obj, storageMode: "File", stage: "RawRead");

                // 特殊处理：兼容读取 JavaSelectionMode 的历史存储格式，但不在读取时改写存储。
                if (TryReadJavaSelectionModeCompatibilityValue(key, obj, out T? compatibilityValue, out var compatibilityResolution))
                {
                    Log.Warning("[LocalSettings.JavaSelectionMode] Compatibility read applied. StorageMode=File; Resolution={Resolution}; RequestedType={RequestedType}; RawType={RawType}; RawValue={RawValue}", compatibilityResolution, typeof(T).FullName, obj.GetType().FullName, obj);
                    LogJavaSelectionModeResolved(key, typeof(T), compatibilityValue, storageMode: "File", resolution: compatibilityResolution);
                    return compatibilityValue;
                }

                if (TryReadDirectValue(obj, out T? directValue))
                {
                    LogJavaSelectionModeResolved(key, typeof(T), directValue, storageMode: "File", resolution: "DirectValue");
                    return directValue;
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
                    else if (obj is List<JavaVersionInfo> typedList)
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
                        var convertedValue = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
                        LogJavaSelectionModeResolved(key, typeof(T), convertedValue, storageMode: "File", resolution: "JsonRoundtrip");
                        return convertedValue;
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
                if (obj is T typedValue)
                {
                    LogJavaSelectionModeResolved(key, typeof(T), typedValue, storageMode: "File", resolution: "TypedValue");
                    return typedValue;
                }

                LogJavaSelectionModeUnresolved<T>(key, obj, storageMode: "File");
                return default;
            }
        }

        return default;
    }

    public async Task SaveSettingAsync<T>(string key, T value)
    {
        LogJavaSelectionModeSave(key, value, RuntimeHelper.IsMSIX ? "MSIX" : "File");

        if (RuntimeHelper.IsMSIX)
        {
            if (TryCreateDirectStorageValue(value, out var directStorageValue))
            {
                ApplicationData.Current.LocalSettings.Values[key] = directStorageValue;
                return;
            }

            ApplicationData.Current.LocalSettings.Values[key] = await Json.StringifyAsync(value!);
        }
        else
        {
            await InitializeAsync();

            _settings[key] = value!;
            
            // 调试：检查保存的Java版本数量
            if (key == "JavaVersions" && value is List<object> javaList)
            {
                Console.WriteLine($"保存Java版本列表，数量: {javaList.Count}");
                foreach (var item in javaList)
                {
                    Console.WriteLine($"  - {item.GetType().Name}: {item}");
                }
            }
            else if (key == "JavaVersions" && value is List<JavaVersionInfo> typedList)
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

    private static bool TryReadDirectValue<T>(object obj, out T? value)
    {
        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var isNullableTarget = Nullable.GetUnderlyingType(typeof(T)) is not null;

        if (targetType == typeof(string) && obj is string stringObject)
        {
            if (string.Equals(stringObject, "null", StringComparison.Ordinal))
            {
                value = default;
                return true;
            }

            value = (T?)(object?)UnwrapStoredString(stringObject);
            return true;
        }

        if (obj is T typedValue)
        {
            value = typedValue;
            return true;
        }

        if (targetType == typeof(bool) && obj is bool directBoolValue)
        {
            object boxed = isNullableTarget ? (bool?)directBoolValue : directBoolValue;
            value = (T?)boxed;
            return true;
        }

        if (targetType == typeof(int) && obj is int directIntValue)
        {
            object boxed = isNullableTarget ? (int?)directIntValue : directIntValue;
            value = (T?)boxed;
            return true;
        }

        if (targetType == typeof(double) && obj is double directDoubleValue)
        {
            object boxed = isNullableTarget ? (double?)directDoubleValue : directDoubleValue;
            value = (T?)boxed;
            return true;
        }

        if (obj is string stringValue)
        {
            if (targetType == typeof(bool) && TryParseStoredBool(stringValue, out var boolValue))
            {
                object boxed = isNullableTarget ? (bool?)boolValue : boolValue;
                value = (T?)boxed;
                return true;
            }

            if (targetType == typeof(int) && TryParseStoredInt(stringValue, out var intValue))
            {
                object boxed = isNullableTarget ? (int?)intValue : intValue;
                value = (T?)boxed;
                return true;
            }

            if (targetType == typeof(double) && TryParseStoredDouble(stringValue, out var doubleValue))
            {
                object boxed = isNullableTarget ? (double?)doubleValue : doubleValue;
                value = (T?)boxed;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryCreateDirectStorageValue<T>(T value, out object directStorageValue)
    {
        switch (value)
        {
            case null:
                directStorageValue = "null";
                return true;
            case string stringValue:
                directStorageValue = ShouldStoreStringAsJson(stringValue)
                    ? JsonConvert.ToString(stringValue)
                    : stringValue;
                return true;
            case bool boolValue:
                directStorageValue = boolValue;
                return true;
            case int intValue:
                directStorageValue = intValue;
                return true;
            case double doubleValue:
                directStorageValue = doubleValue;
                return true;
            default:
                directStorageValue = null!;
                return false;
        }
    }

    private static bool ShouldStoreStringAsJson(string value)
    {
        return string.Equals(value, "null", StringComparison.Ordinal)
            || (value.Length >= 2 && value[0] == '"' && value[^1] == '"');
    }

    private static string UnwrapStoredString(string rawValue)
    {
        return LocalSettingsStoredStringCompatibilityHelper.UnwrapStoredString(rawValue);
    }

    private static bool TryParseStoredBool(string rawValue, out bool value)
    {
        return bool.TryParse(UnwrapStoredString(rawValue), out value);
    }

    private static bool TryParseStoredInt(string rawValue, out int value)
    {
        return int.TryParse(UnwrapStoredString(rawValue), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseStoredDouble(string rawValue, out double value)
    {
        return double.TryParse(UnwrapStoredString(rawValue), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadJavaSelectionModeCompatibilityValue<T>(string key, object rawValue, out T? value, out string resolution)
    {
        value = default;
        resolution = string.Empty;

        if (key != "JavaSelectionMode")
        {
            return false;
        }

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var isNullableTarget = Nullable.GetUnderlyingType(typeof(T)) is not null;

        if (targetType == typeof(int) && rawValue is string stringValue)
        {
            var normalizedValue = UnwrapStoredString(stringValue);
            if (TryMapJavaSelectionModeNameToInt(normalizedValue, out var numericValue))
            {
                object boxedValue = isNullableTarget ? (int?)numericValue : numericValue;
                value = (T?)boxedValue;
                resolution = $"StringNameToInt:{normalizedValue}";
                return true;
            }
        }

        if (targetType == typeof(string) && rawValue is int intValue)
        {
            if (TryMapJavaSelectionModeIntToName(intValue, out var modeName))
            {
                value = (T?)(object?)modeName;
                resolution = $"IntToStringName:{intValue}";
                return true;
            }
        }

        if (targetType == typeof(string) && rawValue is string rawStringValue)
        {
            var normalizedValue = UnwrapStoredString(rawStringValue);
            if (TryParseStoredInt(normalizedValue, out var numericValue)
                && TryMapJavaSelectionModeIntToName(numericValue, out var modeName))
            {
                value = (T?)(object?)modeName;
                resolution = $"NumericStringToStringName:{normalizedValue}";
                return true;
            }
        }

        return false;
    }

    private static bool TryMapJavaSelectionModeNameToInt(string rawValue, out int numericValue)
    {
        if (string.Equals(rawValue, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            numericValue = 0;
            return true;
        }

        if (string.Equals(rawValue, "Manual", StringComparison.OrdinalIgnoreCase))
        {
            numericValue = 1;
            return true;
        }

        numericValue = default;
        return false;
    }

    private static bool TryMapJavaSelectionModeIntToName(int rawValue, out string modeName)
    {
        modeName = rawValue switch
        {
            0 => "Auto",
            1 => "Manual",
            _ => string.Empty,
        };

        return !string.IsNullOrEmpty(modeName);
    }

    private static void LogJavaSelectionModeRead<T>(string key, object rawValue, string storageMode, string stage)
    {
        if (key != "JavaSelectionMode")
        {
            return;
        }

        Log.Information(
            "[LocalSettings.JavaSelectionMode] {Stage}. StorageMode={StorageMode}; RequestedType={RequestedType}; RawType={RawType}; RawValue={RawValue}",
            stage,
            storageMode,
            typeof(T).FullName,
            rawValue.GetType().FullName,
            rawValue);
    }

    private static void LogJavaSelectionModeResolved(string key, Type requestedType, object? resolvedValue, string storageMode, string resolution)
    {
        if (key != "JavaSelectionMode")
        {
            return;
        }

        Log.Information(
            "[LocalSettings.JavaSelectionMode] Resolved. StorageMode={StorageMode}; RequestedType={RequestedType}; Resolution={Resolution}; ResolvedValue={ResolvedValue}",
            storageMode,
            requestedType.FullName,
            resolution,
            resolvedValue ?? "(null)");
    }

    private static void LogJavaSelectionModeUnresolved<T>(string key, object? rawValue, string storageMode)
    {
        if (key != "JavaSelectionMode")
        {
            return;
        }

        Log.Warning(
            "[LocalSettings.JavaSelectionMode] Unresolved value. StorageMode={StorageMode}; RequestedType={RequestedType}; RawType={RawType}; RawValue={RawValue}. Returning default.",
            storageMode,
            typeof(T).FullName,
                rawValue?.GetType().FullName ?? "(null)",
                rawValue ?? "(null)");
    }

    private static void LogJavaSelectionModeSave<T>(string key, T value, string storageMode)
    {
        if (key != "JavaSelectionMode")
        {
            return;
        }

        Log.Information(
            "[LocalSettings.JavaSelectionMode] Save requested. StorageMode={StorageMode}; ValueType={ValueType}; Value={Value}",
            storageMode,
            value?.GetType().FullName ?? typeof(T).FullName,
            value ?? (object)"(null)");
    }
}
