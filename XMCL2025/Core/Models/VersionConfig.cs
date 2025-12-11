using System;

namespace XMCL2025.Core.Models
{
    /// <summary>
    /// 版本配置文件模型，存储版本的ModLoader信息
    /// </summary>
    public class VersionConfig
    {
        /// <summary>
        /// ModLoader类型（fabric, neoforge, forge）
        /// </summary>
        public string ModLoaderType { get; set; }
        
        /// <summary>
        /// ModLoader版本号（完整版本，如21.11.0-beta）
        /// </summary>
        public string ModLoaderVersion { get; set; }
        
        /// <summary>
        /// Minecraft版本号
        /// </summary>
        public string MinecraftVersion { get; set; }
        
        /// <summary>
        /// 配置文件创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}