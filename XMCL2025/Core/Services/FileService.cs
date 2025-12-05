using System.IO;
using Windows.Storage;
using XMCL2025.Core.Contracts.Services;
using Newtonsoft.Json;

namespace XMCL2025.Core.Services;

public class FileService : IFileService
{
    private string? _customMinecraftDataPath;
    
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
        // 返回Windows应用程序的本地数据文件夹
        return ApplicationData.Current.LocalFolder.Path;
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
        _customMinecraftDataPath = path;
    }

    public string GetApplicationFolderPath()
    {
        // 返回应用程序自身的文件夹路径
        return AppContext.BaseDirectory;
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
}