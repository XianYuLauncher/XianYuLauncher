namespace XianYuLauncher.Models.VersionManagement;

/// <summary>
/// level.dat 文件信息
/// </summary>
public class LevelDatInfo
{
    /// <summary>
    /// 存档名称
    /// </summary>
    public string LevelName { get; set; } = string.Empty;
    
    /// <summary>
    /// 游戏模式 (0=生存, 1=创造, 2=冒险, 3=旁观)
    /// </summary>
    public int GameType { get; set; }
    
    /// <summary>
    /// 最后游玩时间（Unix时间戳毫秒）
    /// </summary>
    public long LastPlayed { get; set; }
}
