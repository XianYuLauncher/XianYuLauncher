using Microsoft.Extensions.Options;
using Newtonsoft.Json;

using Windows.ApplicationModel;
using Windows.Storage;

using XMCL2025.Contracts.Services;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Core.Helpers;
using XMCL2025.Helpers;
using XMCL2025.Models;

namespace XMCL2025.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private const string _defaultApplicationDataFolder = "XianYuLauncher/ApplicationData";
    private const string _defaultLocalSettingsFile = "LocalSettings.json";

    private readonly IFileService _fileService;
    private readonly LocalSettingsOptions _options;

    private readonly string _localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _applicationDataFolder;
    private readonly string _localsettingsFile;

    private IDictionary<string, object> _settings;

    private bool _isInitialized;

    public LocalSettingsService(IFileService fileService, IOptions<LocalSettingsOptions> options)
    {
        _fileService = fileService;
        _options = options.Value;

        _applicationDataFolder = Path.Combine(_localApplicationData, _options.ApplicationDataFolder ?? _defaultApplicationDataFolder);
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
                return await Json.ToObjectAsync<T>((string)obj);
            }
        }
        else
        {
            await InitializeAsync();

            if (_settings != null && _settings.TryGetValue(key, out var obj))
            {
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
                    else if (obj is List<XMCL2025.ViewModels.JavaVersionInfo> typedList)
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
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(obj);
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
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
            else if (key == "JavaVersions" && value is List<XMCL2025.ViewModels.JavaVersionInfo> typedList)
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
