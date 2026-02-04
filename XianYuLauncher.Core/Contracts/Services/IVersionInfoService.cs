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
        /// <returns>完整的版本配置信息</returns>
        Task<VersionConfig> GetFullVersionInfoAsync(string versionId, string versionDirectory);

        /// <summary>
        /// 从版本名称提取版本配置信息
        /// </summary>
        /// <param name="versionId">版本ID</param>
        /// <returns>提取的版本配置信息</returns>
        VersionConfig ExtractVersionConfigFromName(string versionId);
    }
}