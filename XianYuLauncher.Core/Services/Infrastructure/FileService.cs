using System.Collections.Concurrent;
using System.IO;
using System.Text;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using Newtonsoft.Json;
using Serilog;

namespace XianYuLauncher.Core.Services;

public class FileService : IFileService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private const int SharingViolationHResult = unchecked((int)0x80070020);
    private const int MaxSharingViolationRetries = 3;

    private string? _customMinecraftDataPath;
    private const string MinecraftPathKey = "MinecraftPath";
    private const string _defaultApplicationDataFolder = "ApplicationData";
    private const string _defaultLocalSettingsFile = "LocalSettings.json";
    
    private readonly string _applicationDataFolder;
    private readonly string _localsettingsFile;
    
    public event EventHandler<string>? MinecraftPathChanged;
    
    public FileService()
    {
        // 使用安全路径，避免 MSIX 虚拟化问题
        _applicationDataFolder = Path.Combine(AppEnvironment.SafeAppDataPath, _defaultApplicationDataFolder);
        _localsettingsFile = _defaultLocalSettingsFile;
        
        // 确保目录存在
        if (!Directory.Exists(_applicationDataFolder))
        {
            Directory.CreateDirectory(_applicationDataFolder);
        }
        
        // 在构造函数中从本地设置加载保存的Minecraft路径
        LoadMinecraftPathFromSettings();
    }
    
    public string ReadText(string filePath)
    {
        var fileLock = GetFileLock(filePath);
        fileLock.Wait();

        try
        {
            return File.ReadAllText(filePath);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public void WriteText(string filePath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory);

        var fileLock = GetFileLock(filePath);
        fileLock.Wait();

        try
        {
            WriteAllTextAtomically(filePath, content);
        }
        finally
        {
            fileLock.Release();
        }
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
        // 使用安全路径，MSIX 环境下返回 LocalState，非 MSIX 返回 LocalAppData\XianYuLauncher
        return AppEnvironment.SafeAppDataPath;
    }

    public string GetMinecraftDataPath()
    {
        // 如果有自定义路径，使用自定义路径，否则使用默认路径
        if (!string.IsNullOrEmpty(_customMinecraftDataPath))
        {
            return _customMinecraftDataPath;
        }
        
        // 默认返回桌面的.minecraft文件夹路径
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ".minecraft");
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
            // 使用安全路径加载设置
            string settingsPath = Path.Combine(_applicationDataFolder, _localsettingsFile);
            var fileLock = GetFileLock(settingsPath);
            fileLock.Wait();

            try
            {
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
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
            finally
            {
                fileLock.Release();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载Minecraft路径失败: {ex.Message}");
        }
    }
    
    private void SaveMinecraftPathToSettings(string path)
    {
        try
        {
            string settingsPath = Path.Combine(_applicationDataFolder, _localsettingsFile);
            var fileLock = GetFileLock(settingsPath);
            fileLock.Wait();
            
            try
            {
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
                Directory.CreateDirectory(_applicationDataFolder);
                
                // 保存到文件
                WriteAllTextAtomically(settingsPath, jObject.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            finally
            {
                fileLock.Release();
            }
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
        // 使用安全缓存路径
        return AppEnvironment.SafeCachePath;
    }

    public T? Read<T>(string folderPath, string fileName)
    {
        var filePath = Path.Combine(folderPath, fileName);
        var fileLock = GetFileLock(filePath);
        fileLock.Wait();

        try
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return TryDeserialize<T>(json, filePath);
            }

            return default;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<T?> ReadAsync<T>(string folderPath, string fileName, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(folderPath, fileName);
        var fileLock = GetFileLock(filePath);
        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!File.Exists(filePath))
            {
                return default;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return TryDeserialize<T>(json, filePath);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public void Save<T>(string folderPath, string fileName, T content)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        var filePath = Path.Combine(folderPath, fileName);
        var json = JsonConvert.SerializeObject(content);
        var fileLock = GetFileLock(filePath);
        fileLock.Wait();

        try
        {
            WriteAllTextAtomically(filePath, json);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task SaveAsync<T>(string folderPath, string fileName, T content, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var filePath = Path.Combine(folderPath, fileName);
        var json = JsonConvert.SerializeObject(content);
        var fileLock = GetFileLock(filePath);
        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await WriteAllTextAtomicallyAsync(filePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public void Delete(string folderPath, string fileName)
    {
        if (fileName != null)
        {
            var filePath = Path.Combine(folderPath, fileName);
            var fileLock = GetFileLock(filePath);
            fileLock.Wait();

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            finally
            {
                fileLock.Release();
            }
        }
    }

    private static SemaphoreSlim GetFileLock(string filePath)
    {
        return FileLocks.GetOrAdd(Path.GetFullPath(filePath), static _ => new SemaphoreSlim(1, 1));
    }

    private static void WriteAllTextAtomically(string filePath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory);

        for (var attempt = 0; ; attempt++)
        {
            var tempFilePath = CreateTemporaryFilePath(filePath);

            try
            {
                File.WriteAllText(tempFilePath, content, Utf8WithoutBom);
                File.Move(tempFilePath, filePath, overwrite: true);
                return;
            }
            catch (IOException ex) when (IsSharingViolation(ex) && attempt < MaxSharingViolationRetries - 1)
            {
                TryDeleteTemporaryFile(tempFilePath);
                Thread.Sleep(TimeSpan.FromMilliseconds(50 * (attempt + 1)));
            }
            catch
            {
                TryDeleteTemporaryFile(tempFilePath);
                throw;
            }
        }
    }

    private static async Task WriteAllTextAtomicallyAsync(string filePath, string content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory);

        for (var attempt = 0; ; attempt++)
        {
            var tempFilePath = CreateTemporaryFilePath(filePath);

            try
            {
                await File.WriteAllTextAsync(tempFilePath, content, Utf8WithoutBom, cancellationToken).ConfigureAwait(false);
                File.Move(tempFilePath, filePath, overwrite: true);
                return;
            }
            catch (IOException ex) when (IsSharingViolation(ex) && attempt < MaxSharingViolationRetries - 1)
            {
                TryDeleteTemporaryFile(tempFilePath);
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                TryDeleteTemporaryFile(tempFilePath);
                throw;
            }
        }
    }

    private static string CreateTemporaryFilePath(string filePath)
    {
        return $"{filePath}.{Guid.NewGuid():N}.tmp";
    }

    private static bool IsSharingViolation(IOException exception)
    {
        return exception.HResult == SharingViolationHResult;
    }

    private static void TryDeleteTemporaryFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
        }
    }

    private static T? TryDeserialize<T>(string json, string filePath)
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (JsonException ex)
        {
            var fileName = Path.GetFileName(filePath);
            Log.Warning(ex, "读取 JSON 文件失败: {FileName}", string.IsNullOrWhiteSpace(fileName) ? "<unknown>" : fileName);
            return default;
        }
    }
}
