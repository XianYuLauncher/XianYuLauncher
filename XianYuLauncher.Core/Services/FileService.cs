using System.IO;
using System.Text;
using XianYuLauncher.Core.Contracts.Services;
using Newtonsoft.Json;

namespace XianYuLauncher.Core.Services;

public class FileService : IFileService
{
    private string? _customMinecraftDataPath;
    private const string MinecraftPathKey = "MinecraftPath";
    private const string _defaultApplicationDataFolder = "XianYuLauncher/ApplicationData";
    private const string _defaultLocalSettingsFile = "LocalSettings.json";
    
    private readonly string _localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _applicationDataFolder;
    private readonly string _localsettingsFile;
    
    public event EventHandler<string>? MinecraftPathChanged;
    
    public FileService()
    {
        _applicationDataFolder = Path.Combine(_localApplicationData, _defaultApplicationDataFolder);
        _localsettingsFile = _defaultLocalSettingsFile;
        // 在构造函数中从本地设置加载保存的Minecraft路径
        LoadMinecraftPathFromSettings();
    }
    
    public string ReadText(string filePath)
    {
        return File.ReadAllText(filePath);
    }

    public void WriteText(string filePath, string content)
    {
        File.WriteAllText(filePath, content);
    }

    public bool FileExists(string filePath)
    {
        return File.Exists(filePath);
    }

    public void CreateDirectory(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
    }

    public string GetAppDataPath()
    {
        // 返回应用程序的本地数据文件夹 - 使用 LocalApplicationData 替代 Windows.Storage
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XianYuLauncher");
    }

    public string GetMinecraftDataPath()
    {
        // 如果有自定义路径，使用自定义路径，否则使用默认路径
        if (!string.IsNullOrEmpty(_customMinecraftDataPath))
        {
            return _customMinecraftDataPath;
        }
        
        // 返回应用程序本地数据文件夹下的.minecraft文件夹路径
        string appDataPath = GetAppDataPath();
        return Path.Combine(appDataPath, ".minecraft");
    }
    
    public void SetMinecraftDataPath(string path)
    {
        if (_customMinecraftDataPath != path)
        {
            _customMinecraftDataPath = path;
            SaveMinecraftPathToSettings(path);
            // 触发路径变化事件
            MinecraftPathChanged?.Invoke(this, path);
        }
    }
    
    private void LoadMinecraftPathFromSettings()
    {
        try
        {
            // 对于所有部署类型，都从本地应用数据文件夹加载设置
            // 这样可以确保在开发环境和生产环境中都能正确加载设置
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsPath = Path.Combine(localAppDataPath, "XianYuLauncher", "ApplicationData", "LocalSettings.json");
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                // 使用Newtonsoft.Json.Linq.JObject来解析JSON，这样可以处理任何JSON对象类型
                Newtonsoft.Json.Linq.JObject jObject = Newtonsoft.Json.Linq.JObject.Parse(json);
                if (jObject.TryGetValue(MinecraftPathKey, out var jToken))
                {
                    _customMinecraftDataPath = jToken.ToString();
                    System.Diagnostics.Debug.WriteLine($"成功加载Minecraft路径: {_customMinecraftDataPath}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"设置文件不存在: {settingsPath}");
            }
        }
        catch (Exception ex)
        {
            // 加载失败时使用默认路径
            System.Diagnostics.Debug.WriteLine($"加载Minecraft路径失败: {ex.Message}");
        }
    }
    
    private void SaveMinecraftPathToSettings(string path)
    {
        try
        {
            // 对于所有部署类型，都保存到本地应用数据文件夹
            // 这样可以确保在开发环境和生产环境中都能正确保存设置
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsFolder = Path.Combine(localAppDataPath, "XianYuLauncher", "ApplicationData");
            string settingsPath = Path.Combine(settingsFolder, "LocalSettings.json");
            
            Newtonsoft.Json.Linq.JObject jObject = new Newtonsoft.Json.Linq.JObject();
            
            // 如果文件已存在，读取现有设置
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                jObject = Newtonsoft.Json.Linq.JObject.Parse(json);
            }
            
            // 更新Minecraft路径
            jObject[MinecraftPathKey] = path;
            System.Diagnostics.Debug.WriteLine($"成功保存Minecraft路径: {path} 到 {settingsPath}");
            
            // 确保目录存在
            Directory.CreateDirectory(settingsFolder);
            
            // 保存到文件
            File.WriteAllText(settingsPath, jObject.ToString(Newtonsoft.Json.Formatting.Indented));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存Minecraft路径失败: {ex.Message}");
        }
    }

    public string GetApplicationFolderPath()
    {
        // 返回应用程序自身的文件夹路径
        return AppContext.BaseDirectory;
    }

    public string GetLauncherCachePath()
    {
        // 返回启动器专用的缓存目录，位于 LocalApplicationData/XianYuLauncher/Cache
        string appDataPath = GetAppDataPath();
        string cachePath = Path.Combine(appDataPath, "Cache");
        
        // 确保目录存在
        if (!Directory.Exists(cachePath))
        {
            Directory.CreateDirectory(cachePath);
        }
        
        return cachePath;
    }

    public T Read<T>(string folderPath, string fileName)
    {
        var filePath = Path.Combine(folderPath, fileName);
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<T>(json);
        }
        return default;
    }

    public void Save<T>(string folderPath, string fileName, T content)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        var filePath = Path.Combine(folderPath, fileName);
        var json = JsonConvert.SerializeObject(content);
        File.WriteAllText(filePath, json);
    }

    public void Delete(string folderPath, string fileName)
    {
        if (fileName != null && File.Exists(Path.Combine(folderPath, fileName)))
        {
            File.Delete(Path.Combine(folderPath, fileName));
        }
    }
}
