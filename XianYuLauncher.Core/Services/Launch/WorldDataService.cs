using System;
using System.IO;
using fNbt;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Minecraft 世界数据服务
/// 用于解析 level.dat 文件中的世界信息
/// </summary>
public class WorldDataService
{
    /// <summary>
    /// 从世界文件夹中读取世界数据
    /// </summary>
    /// <param name="worldFolderPath">世界文件夹路径</param>
    /// <returns>世界数据，如果读取失败返回 null</returns>
    public WorldData? ReadWorldData(string worldFolderPath)
    {
        try
        {
            var levelDatPath = Path.Combine(worldFolderPath, "level.dat");
            
            if (!File.Exists(levelDatPath))
            {
                System.Diagnostics.Debug.WriteLine($"[WorldDataService] level.dat 不存在: {levelDatPath}");
                return null;
            }
            
            // 读取 NBT 文件
            var nbtFile = new NbtFile();
            nbtFile.LoadFromFile(levelDatPath);
            
            var rootTag = nbtFile.RootTag;
            var dataTag = rootTag.Get<NbtCompound>("Data");
            
            if (dataTag == null)
            {
                System.Diagnostics.Debug.WriteLine("[WorldDataService] Data 标签不存在");
                return null;
            }
            
            var worldData = new WorldData();
            
            // 读取种子
            var worldGenSettings = dataTag.Get<NbtCompound>("WorldGenSettings");
            if (worldGenSettings != null)
            {
                var seedTag = worldGenSettings.Get<NbtLong>("seed");
                if (seedTag != null)
                {
                    worldData.Seed = seedTag.Value;
                }
            }
            
            // 读取难度 - 支持新旧两种格式
            // 26.1+ 新格式: difficulty_settings.difficulty (string)
            // 旧格式: Difficulty (byte: 0=和平, 1=简单, 2=普通, 3=困难)
            var difficultySettings = dataTag.Get<NbtCompound>("difficulty_settings");
            if (difficultySettings != null)
            {
                // 26.1+ 新格式
                var difficultyStr = difficultySettings.Get<NbtString>("difficulty")?.Value;
                worldData.Difficulty = difficultyStr?.ToLowerInvariant() switch
                {
                    "peaceful" => DifficultyType.Peaceful,
                    "easy" => DifficultyType.Easy,
                    "normal" => DifficultyType.Normal,
                    "hard" => DifficultyType.Hard,
                    _ => DifficultyType.Unknown
                };
            }
            else
            {
                // 旧格式
                var difficultyTag = dataTag.Get<NbtByte>("Difficulty");
                if (difficultyTag != null)
                {
                    worldData.Difficulty = difficultyTag.Value switch
                    {
                        0 => DifficultyType.Peaceful,
                        1 => DifficultyType.Easy,
                        2 => DifficultyType.Normal,
                        3 => DifficultyType.Hard,
                        _ => DifficultyType.Unknown
                    };
                }
            }
            
            // 读取游戏模式
            // 先检查是否为极限模式
            var hardcoreTag = dataTag.Get<NbtByte>("hardcore");
            if (hardcoreTag != null && hardcoreTag.Value != 0)
            {
                worldData.GameMode = GameModeType.Hardcore;
            }
            else
            {
                // 不是极限模式，读取普通游戏模式 (0=生存, 1=创造, 2=冒险, 3=旁观)
                var gameTypeTag = dataTag.Get<NbtInt>("GameType");
                if (gameTypeTag != null)
                {
                    worldData.GameMode = gameTypeTag.Value switch
                    {
                        0 => GameModeType.Survival,
                        1 => GameModeType.Creative,
                        2 => GameModeType.Adventure,
                        3 => GameModeType.Spectator,
                        _ => GameModeType.Unknown
                    };
                }
            }
            
            // 读取世界名称
            var levelNameTag = dataTag.Get<NbtString>("LevelName");
            if (levelNameTag != null)
            {
                worldData.LevelName = levelNameTag.Value;
            }
            
            // 读取游戏时间（游戏刻）
            var timeTag = dataTag.Get<NbtLong>("Time");
            if (timeTag != null)
            {
                worldData.Time = timeTag.Value;
            }
            
            // 读取是否允许命令
            var allowCommandsTag = dataTag.Get<NbtByte>("allowCommands");
            if (allowCommandsTag != null)
            {
                worldData.AllowCommands = allowCommandsTag.Value != 0;
            }
            
            System.Diagnostics.Debug.WriteLine("[WorldDataService] 成功读取世界数据");
            
            return worldData;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorldDataService] 读取世界数据失败: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// 世界数据模型
/// </summary>
public class WorldData
{
    /// <summary>
    /// 世界种子
    /// </summary>
    public long Seed { get; set; }
    
    /// <summary>
    /// 难度
    /// </summary>
    public DifficultyType Difficulty { get; set; } = DifficultyType.Unknown;
    
    /// <summary>
    /// 游戏模式
    /// </summary>
    public GameModeType GameMode { get; set; } = GameModeType.Unknown;
    
    /// <summary>
    /// 世界名称
    /// </summary>
    public string LevelName { get; set; } = "未知";
    
    /// <summary>
    /// 游戏时间（游戏刻）
    /// </summary>
    public long Time { get; set; }
    
    /// <summary>
    /// 是否允许命令
    /// </summary>
    public bool AllowCommands { get; set; }
}
