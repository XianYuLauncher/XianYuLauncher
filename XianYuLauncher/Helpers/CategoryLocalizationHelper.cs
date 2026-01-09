using System;
using System.Collections.Generic;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 类别本地化辅助类
/// </summary>
public static class CategoryLocalizationHelper
{
    /// <summary>
    /// CurseForge类别名称到本地化键的映射
    /// </summary>
    private static readonly Dictionary<string, string> CurseForgeCategoryMap = new()
    {
        // Mods类别
        { "Armor, Tools, and Weapons", "装备" },
        { "Adventure and RPG", "冒险" },
        { "API and Library", "前置" },
        { "Automation", "自动化" },
        { "Biomes", "生物群系" },
        { "Bug Fixes", "修复" },
        { "Cosmetic", "装饰" },
        { "Cursed", "整活" },
        { "Dimensions", "维度" },
        { "Economy", "经济" },
        { "Education", "教育" },
        { "Energy, Fluid, and Item Transport", "传输" },
        { "Equipment", "装备" },
        { "Farming", "农业" },
        { "Food", "食物" },
        { "Game Mechanics", "机制" },
        { "Library", "前置" },
        { "Magic", "魔法" },
        { "Management", "管理" },
        { "Map and Information", "地图" },
        { "Minigame", "迷你游戏" },
        { "Mobs", "生物" },
        { "Optimization", "性能" },
        { "Performance", "性能" },
        { "Redstone", "红石" },
        { "Server Utility", "服务器" },
        { "Social", "社交" },
        { "Storage", "存储" },
        { "Structures", "结构" },
        { "Technology", "科技" },
        { "Transportation", "交通" },
        { "Twitch Integration", "Twitch" },
        { "Utility", "实用" },
        { "Utility & QoL", "实用QoL" },
        { "World Gen", "世界生成" },
        { "Worldgen", "世界生成WG" },
        { "Ores and Resources", "矿物" },
        { "Player Transport", "玩家传送" },
        { "Processing", "加工" },
        { "Energy", "能源" },
        { "Genetics", "遗传" },
        { "MCreator", "MCreator" },
        { "ModJam 2025", "ModJam 2025" },
        { "CreativeMode", "创造模式" },
        
        // Shaders类别
        { "Realistic", "真实" },
        { "Fantasy", "幻想" },
        { "Vanilla", "原版" },
        { "Cartoon", "卡通" },
        { "Semi-Realistic", "半真实" },
        { "Simplistic", "简约" },
        { "Atmospheric", "氛围" },
        { "Vibrant", "鲜艳" },
        
        // Resource Packs类别
        { "16x", "16x" },
        { "32x", "32x" },
        { "64x", "64x" },
        { "128x", "128x" },
        { "256x", "256x" },
        { "512x and Higher", "512x+" },
        { "Animated", "动画" },
        { "Combat", "战斗" },
        { "Font Packs", "字体" },
        { "Medieval", "中世纪" },
        { "Miscellaneous", "杂项" },
        { "Modern", "现代" },
        { "Mod Support", "模组支持" },
        { "Photo Realistic", "照片级" },
        { "Steampunk", "蒸汽朋克" },
        { "Traditional", "传统" },
        { "Tweaks", "调整" },
        
        // Modpacks类别
        { "Combat / PvP", "战斗PvP" },
        { "Expert", "专家" },
        { "Exploration", "探索" },
        { "Extra Large", "超大型" },
        { "FTB Official Pack", "FTB官方" },
        { "Hardcore", "硬核" },
        { "Horror", "恐怖" },
        { "Map Based", "地图" },
        { "Mini Game", "迷你游戏" },
        { "Multiplayer", "多人" },
        { "Quests", "任务" },
        { "Sci-Fi", "科幻" },
        { "Skyblock", "空岛" },
        { "Small / Light", "轻量" },
        { "Tech", "科技" },
        { "Vanilla+", "原版+" },
        
        // Data Packs类别 (使用不同的键名避免冲突)
        { "Adventure", "冒险DP" },
    };
    
    /// <summary>
    /// 获取CurseForge类别的本地化名称
    /// </summary>
    /// <param name="categoryName">CurseForge类别名称</param>
    /// <returns>本地化名称</returns>
    public static string GetLocalizedCategoryName(string categoryName)
    {
        if (string.IsNullOrEmpty(categoryName))
        {
            return categoryName;
        }
        
        if (CurseForgeCategoryMap.TryGetValue(categoryName, out var localizedName))
        {
            return localizedName;
        }
        
        // 如果没有映射，返回原名称
        return categoryName;
    }
    
    /// <summary>
    /// 获取Modrinth类别的本地化名称（从资源文件）
    /// </summary>
    /// <param name="tag">Modrinth类别标签</param>
    /// <returns>本地化名称</returns>
    public static string GetModrinthCategoryName(string tag)
    {
        // 这里可以从资源文件读取，暂时返回映射
        var modrinthMap = new Dictionary<string, string>
        {
            { "all", "所有类别" },
            { "adventure", "冒险" },
            { "cursed", "整活" },
            { "decoration", "装饰" },
            { "economy", "经济" },
            { "equipment", "装备" },
            { "food", "食物" },
            { "game-mechanics", "机制" },
            { "library", "前置" },
            { "magic", "魔法" },
            { "management", "管理" },
            { "minigame", "迷你游戏" },
            { "mobs", "生物" },
            { "optimization", "性能" },
            { "social", "社交" },
            { "storage", "存储" },
            { "technology", "科技" },
            { "transportation", "交通" },
            { "utility", "实用" },
            { "worldgen", "世界生成" },
            { "cartoon", "卡通" },
            { "fantasy", "幻想" },
            { "realistic", "真实" },
            { "vanilla-like", "原版" },
            { "combat", "战斗" },
            { "core-shaders", "核心" },
            { "high-performance", "高性能" },
            { "potato", "低配" },
            { "screenshot", "截图" },
            { "themed", "主题" },
            { "tweaks", "调整" },
        };
        
        if (modrinthMap.TryGetValue(tag, out var name))
        {
            return name;
        }
        
        return tag;
    }
}
