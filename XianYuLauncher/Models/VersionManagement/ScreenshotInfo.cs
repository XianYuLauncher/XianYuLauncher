namespace XianYuLauncher.Models.VersionManagement;

/// <summary>
/// 截图信息类
/// </summary>
public class ScreenshotInfo
{
    /// <summary>
    /// 截图文件名
    /// </summary>
    public string FileName { get; set; }
    
    /// <summary>
    /// 截图显示名称
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// 截图文件完整路径
    /// </summary>
    public string FilePath { get; set; }
    
    /// <summary>
    /// 截图文件创建时间（用于排序）
    /// </summary>
    public DateTime OriginalCreationTime { get; private set; }
    
    /// <summary>
    /// 格式化后的创建时间字符串
    /// </summary>
    public string CreationTime { get; private set; }
    
    /// <summary>
    /// 截图文件大小
    /// </summary>
    private long _fileSize;
    
    /// <summary>
    /// 格式化后的文件大小字符串
    /// </summary>
    public string FileSize { get; private set; }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public ScreenshotInfo(string filePath)
    {
        // 确保文件路径是完整的，没有被截断
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        
        // 提取显示名称（去掉.png 扩展名）
        string displayName = Path.GetFileNameWithoutExtension(FileName);
        Name = displayName;
        
        // 获取文件信息
        var fileInfo = new FileInfo(filePath);
        OriginalCreationTime = fileInfo.CreationTime;
        _fileSize = fileInfo.Length;
        
        // 格式化创建时间
        CreationTime = OriginalCreationTime.ToString("yyyy-MM-dd HH:mm:ss");
        
        // 格式化文件大小
        if (_fileSize < 1024)
        {
            FileSize = $"{_fileSize} bytes";
        }
        else if (_fileSize < 1024 * 1024)
        {
            FileSize = $"{(_fileSize / 1024):N0} KB";
        }
        else
        {
            FileSize = $"{(_fileSize / (1024 * 1024)):N2} MB";
        }
    }
}
