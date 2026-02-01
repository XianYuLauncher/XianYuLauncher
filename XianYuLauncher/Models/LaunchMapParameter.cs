namespace XianYuLauncher.Models;

public class LaunchMapParameter
{
    public string VersionId { get; set; }
    public string WorldFolder { get; set; }
    
    // Server Join Parameters
    public string ServerAddress { get; set; }
    public int? ServerPort { get; set; }
    
    /// <summary>
    /// 标记该启动请求是否已被处理，防止页面回退时重复触发
    /// </summary>
    public bool IsHandled { get; set; }
}
