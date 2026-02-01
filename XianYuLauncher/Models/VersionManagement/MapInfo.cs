using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XianYuLauncher.Models.VersionManagement;

/// <summary>
/// 地图信息类（仅用于列表展示）
/// </summary>
public partial class MapInfo : ObservableObject
{
    /// <summary>
    /// 地图文件名
    /// </summary>
    public string FileName { get; set; }
    
    /// <summary>
    /// 地图显示名称
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    ///     地图文件完整路径
    /// </summary>
    public string FilePath { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; private set; }
    
    /// <summary>
    /// 地图图标路径
    /// </summary>
    [ObservableProperty]
    private string _icon;
    
    /// <summary>
    /// 地图大小（格式化字符串，如 "125 MB"）
    /// </summary>
    public string Size { get; set; }
    
    /// <summary>
    /// 地图大小（字节）
    /// </summary>
    public long SizeInBytes { get; set; }
    
    /// <summary>
    /// 最后修改时间（用作最后游玩时间）
    /// </summary>
    public DateTime LastPlayedTime { get; set; }
    
    /// <summary>
    /// 格式化的最后游玩时间
    /// </summary>
    public string FormattedLastPlayedTime => LastPlayedTime == DateTime.MinValue ? "" : LastPlayedTime.ToString("yyyy-MM-dd HH:mm:ss");
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public MapInfo(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        IsEnabled = !FileName.EndsWith(".disabled");
        
        // 提取显示名称（去掉.disabled后缀）
        string displayName = FileName;
        if (displayName.EndsWith(".disabled"))
        {
            displayName = displayName.Substring(0, displayName.Length - ".disabled".Length);
        }
        Name = displayName;
        
        // 设置默认值
        Size = "计算中...";
        SizeInBytes = 0;
        LastPlayedTime = DateTime.MinValue;
    }
    
    /// <summary>
    /// 异步加载基本信息（大小和时间）
    /// </summary>
    public async Task LoadBasicInfoAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                var dirInfo = new DirectoryInfo(FilePath);
                if (dirInfo.Exists)
                {
                    // 计算文件夹大小
                    SizeInBytes = CalculateDirectorySize(dirInfo);
                    Size = FormatFileSize(SizeInBytes);
                    
                    // 获取最后修改时间
                    LastPlayedTime = dirInfo.LastWriteTime;
                }
            }
            catch
            {
                Size = "未知";
                SizeInBytes = 0;
            }
        });
    }
    
    /// <summary>
    /// 计算文件夹大小
    /// </summary>
    private static long CalculateDirectorySize(DirectoryInfo directory)
    {
        long size = 0;
        try
        {
            foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
            {
                size += file.Length;
            }
        }
        catch
        {
            // 忽略权限错误
        }
        return size;
    }
    
    /// <summary>
    /// 格式化文件大小
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        else if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        else if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        else
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
