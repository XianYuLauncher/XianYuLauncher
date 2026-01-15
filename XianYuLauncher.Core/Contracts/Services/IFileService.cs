using System.IO;

namespace XianYuLauncher.Core.Contracts.Services;

public interface IFileService
{
    event EventHandler<string>? MinecraftPathChanged;
    string ReadText(string filePath);
    void WriteText(string filePath, string content);
    bool FileExists(string filePath);
    void CreateDirectory(string directoryPath);
    string GetAppDataPath();
    string GetMinecraftDataPath();
    void SetMinecraftDataPath(string path);
    string GetApplicationFolderPath();
    /// <summary>
    /// 获取启动器缓存目录路径（用于存储新闻、Modrinth、CurseForge等缓存）
    /// </summary>
    string GetLauncherCachePath();
    T Read<T>(string folderPath, string fileName);
    void Save<T>(string folderPath, string fileName, T content);
    void Delete(string folderPath, string fileName);
}
