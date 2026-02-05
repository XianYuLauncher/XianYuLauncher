using System;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services
{
    /// <summary>
    /// 版本信息服务接口，提供统一的版本信息获取方法
    /// </summary>
    public interface IVersionInfoService
    {
        /// <summary>
        /// 深度分析获取完整版本信息 (支持 JSON/Models/Jar 深度扫描)
        /// </summary>
        /// <param name="versionId">版本ID (文件夹名称)</param>
        /// <param name="versionDirectory">版本物理路径</param>
        /// <param name="preferCache">是否优先使用缓存配置(XianYuL.cfg)，如果为true且缓存存在，则跳过深度扫描。默认为false。</param>
        /// <returns>完整的版本配置信息</returns>
        Task<VersionConfig> GetFullVersionInfoAsync(string versionId, string versionDirectory, bool preferCache = false);

        /// <summary>
        /// 深度分析获取完整版本信息的同步版本。
        /// 建议迁移到 <see cref="GetFullVersionInfoAsync(string, string)"/>。
        /// </summary>
        /// <param name="versionId">版本ID (文件夹名称)</param>
        /// <param name="versionDirectory">版本物理路径</param>
        /// <returns>完整的版本配置信息</returns>
        [Obsolete("Use GetFullVersionInfoAsync instead.")]
        VersionConfig GetFullVersionInfo(string versionId, string versionDirectory);

        /// <summary>
        /// 从版本名称提取版本配置信息
        /// </summary>
        /// <param name="versionId">版本ID</param>
        /// <returns>提取的版本配置信息</returns>
        VersionConfig ExtractVersionConfigFromName(string versionId);

        /// <summary>
        /// 从版本目录提取版本配置信息。
        /// 建议迁移到 <see cref="GetFullVersionInfoAsync(string, string)"/> 或 <see cref="ExtractVersionConfigFromName(string)"/>。
        /// </summary>
        /// <param name="versionDirectory">版本物理路径</param>
        /// <returns>提取的版本配置信息</returns>
        [Obsolete("Use GetFullVersionInfoAsync or ExtractVersionConfigFromName instead.")]
        VersionConfig GetVersionConfigFromDirectory(string versionDirectory);
    }
}