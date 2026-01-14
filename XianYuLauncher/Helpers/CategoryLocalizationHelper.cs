using System;
using System.Collections.Generic;
using Microsoft.Windows.ApplicationModel.Resources;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 类别本地化辅助类
/// </summary>
public static class CategoryLocalizationHelper
{
    private static readonly ResourceLoader _resourceLoader = new ResourceLoader();
    
    /// <summary>
    /// CurseForge类别名称到本地化键的映射
    /// </summary>
    private static readonly Dictionary<string, string> CurseForgeCategoryKeyMap = new()
    {
        // Mods类别
        { "Armor, Tools, and Weapons", "Category_Equipment" },
        { "Adventure and RPG", "Category_Adventure" },
        { "API and Library", "Category_Library" },
        { "Automation", "Category_Automation" },
        { "Biomes", "Category_Biomes" },
        { "Bug Fixes", "Category_BugFixes" },
        { "Cosmetic", "Category_Decoration" },
        { "Cursed", "Category_Cursed" },
        { "Dimensions", "Category_Dimensions" },
        { "Economy", "Category_Economy" },
        { "Education", "Category_Education" },
        { "Energy, Fluid, and Item Transport", "Category_Transport" },
        { "Equipment", "Category_Equipment" },
        { "Farming", "Category_Farming" },
        { "Food", "Category_Food" },
        { "Game Mechanics", "Category_GameMechanics" },
        { "Library", "Category_Library" },
        { "Magic", "Category_Magic" },
        { "Management", "Category_Management" },
        { "Map and Information", "Category_Map" },
        { "Minigame", "Category_Minigame" },
        { "Mobs", "Category_Mobs" },
        { "Optimization", "Category_Optimization" },
        { "Performance", "Category_Optimization" },
        { "Redstone", "Category_Redstone" },
        { "Server Utility", "Category_ServerUtility" },
        { "Social", "Category_Social" },
        { "Storage", "Category_Storage" },
        { "Structures", "Category_Structures" },
        { "Technology", "Category_Technology" },
        { "Transportation", "Category_Transportation" },
        { "Twitch Integration", "Category_Twitch" },
        { "Utility", "Category_Utility" },
        { "Utility & QoL", "Category_UtilityQoL" },
        { "World Gen", "Category_Worldgen" },
        { "Worldgen", "Category_Worldgen" },
        { "Ores and Resources", "Category_Ores" },
        { "Player Transport", "Category_PlayerTransport" },
        { "Processing", "Category_Processing" },
        { "Energy", "Category_Energy" },
        { "Genetics", "Category_Genetics" },
        { "MCreator", "Category_MCreator" },
        { "ModJam 2025", "Category_ModJam2025" },
        { "CreativeMode", "Category_CreativeMode" },
        
        // Shaders类别
        { "Realistic", "Category_Realistic" },
        { "Fantasy", "Category_Fantasy" },
        { "Vanilla", "Category_Vanilla" },
        { "Cartoon", "Category_Cartoon" },
        { "Semi-Realistic", "Category_SemiRealistic" },
        { "Simplistic", "Category_Simplistic" },
        { "Atmospheric", "Category_Atmospheric" },
        { "Vibrant", "Category_Vibrant" },
        
        // Resource Packs类别
        { "16x", "Category_16x" },
        { "32x", "Category_32x" },
        { "64x", "Category_64x" },
        { "128x", "Category_128x" },
        { "256x", "Category_256x" },
        { "512x and Higher", "Category_512xPlus" },
        { "Animated", "Category_Animated" },
        { "Combat", "Category_Combat" },
        { "Font Packs", "Category_FontPacks" },
        { "Medieval", "Category_Medieval" },
        { "Miscellaneous", "Category_Miscellaneous" },
        { "Modern", "Category_Modern" },
        { "Mod Support", "Category_ModSupport" },
        { "Photo Realistic", "Category_PhotoRealistic" },
        { "Steampunk", "Category_Steampunk" },
        { "Traditional", "Category_Traditional" },
        { "Tweaks", "Category_Tweaks" },
        
        // Modpacks类别
        { "Combat / PvP", "Category_CombatPvP" },
        { "Expert", "Category_Expert" },
        { "Exploration", "Category_Exploration" },
        { "Extra Large", "Category_ExtraLarge" },
        { "FTB Official Pack", "Category_FTBOfficial" },
        { "Hardcore", "Category_Hardcore" },
        { "Horror", "Category_Horror" },
        { "Map Based", "Category_MapBased" },
        { "Mini Game", "Category_Minigame" },
        { "Multiplayer", "Category_Multiplayer" },
        { "Quests", "Category_Quests" },
        { "Sci-Fi", "Category_SciFi" },
        { "Skyblock", "Category_Skyblock" },
        { "Small / Light", "Category_SmallLight" },
        { "Tech", "Category_Tech" },
        { "Vanilla+", "Category_VanillaPlus" },
        
        // Data Packs类别
        { "Adventure", "Category_Adventure" },
    };
    
    /// <summary>
    /// Modrinth类别标签到本地化键的映射
    /// </summary>
    private static readonly Dictionary<string, string> ModrinthCategoryKeyMap = new()
    {
        { "all", "Category_All" },
        { "adventure", "Category_Adventure" },
        { "cursed", "Category_Cursed" },
        { "decoration", "Category_Decoration" },
        { "economy", "Category_Economy" },
        { "equipment", "Category_Equipment" },
        { "food", "Category_Food" },
        { "game-mechanics", "Category_GameMechanics" },
        { "library", "Category_Library" },
        { "magic", "Category_Magic" },
        { "management", "Category_Management" },
        { "minigame", "Category_Minigame" },
        { "mobs", "Category_Mobs" },
        { "optimization", "Category_Optimization" },
        { "social", "Category_Social" },
        { "storage", "Category_Storage" },
        { "technology", "Category_Technology" },
        { "transportation", "Category_Transportation" },
        { "utility", "Category_Utility" },
        { "worldgen", "Category_Worldgen" },
        { "cartoon", "Category_Cartoon" },
        { "fantasy", "Category_Fantasy" },
        { "realistic", "Category_Realistic" },
        { "vanilla-like", "Category_Vanilla" },
        { "combat", "Category_Combat" },
        { "core-shaders", "Category_CoreShaders" },
        { "high-performance", "Category_HighPerformance" },
        { "potato", "Category_Potato" },
        { "screenshot", "Category_Screenshot" },
        { "themed", "Category_Themed" },
        { "tweaks", "Category_Tweaks" },
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
        
        if (CurseForgeCategoryKeyMap.TryGetValue(categoryName, out var resourceKey))
        {
            try
            {
                var localizedName = _resourceLoader.GetString(resourceKey);
                return string.IsNullOrEmpty(localizedName) ? categoryName : localizedName;
            }
            catch
            {
                return categoryName;
            }
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
        if (string.IsNullOrEmpty(tag))
        {
            return tag;
        }
        
        if (ModrinthCategoryKeyMap.TryGetValue(tag, out var resourceKey))
        {
            try
            {
                var localizedName = _resourceLoader.GetString(resourceKey);
                return string.IsNullOrEmpty(localizedName) ? tag : localizedName;
            }
            catch
            {
                return tag;
            }
        }
        
        return tag;
    }
}
