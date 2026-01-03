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
    T Read<T>(string folderPath, string fileName);
    void Save<T>(string folderPath, string fileName, T content);
}