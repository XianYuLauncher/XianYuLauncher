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
        /// 从版本目录获取版本配置信息
        /// </summary>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>版本配置信息，如果无法获取则返回null</returns>
        VersionConfig GetVersionConfigFromDirectory(string versionDirectory);
        
        /// <summary>
        /// 从版本名称提取版本配置信息
        /// </summary>
        /// <param name="versionId">版本ID</param>
        /// <returns>提取的版本配置信息</returns>
        VersionConfig ExtractVersionConfigFromName(string versionId);
        
        /// <summary>
        /// 获取完整的版本信息，包括从配置文件和版本名提取的信息
        /// </summary>
        /// <param name="versionId">版本ID</param>
        /// <param name="versionDirectory">版本目录路径</param>
        /// <returns>完整的版本配置信息</returns>
        VersionConfig GetFullVersionInfo(string versionId, string versionDirectory);
    }
}