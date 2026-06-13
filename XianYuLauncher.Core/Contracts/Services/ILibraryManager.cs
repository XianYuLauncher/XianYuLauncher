using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 依赖库管理器接口，负责 Minecraft 依赖库的下载、验证和管理
/// </summary>
public interface ILibraryManager
{
    /// <summary>
    /// 下载版本所需的所有依赖库
    /// </summary>
    /// <param name="versionInfo">版本信息</param>
    /// <param name="librariesDirectory">库文件目录</param>
    /// <param name="progressCallback">进度回调（0-100）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DownloadLibrariesAsync(
        VersionInfo versionInfo, 
        string librariesDirectory,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 提取原生库到指定目录
    /// </summary>
    /// <param name="versionInfo">版本信息</param>
    /// <param name="librariesDirectory">库文件目录</param>
    /// <param name="nativesDirectory">原生库目标目录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ExtractNativeLibrariesAsync(
        VersionInfo versionInfo, 
        string librariesDirectory, 
        string nativesDirectory,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检查依赖库是否已下载
    /// </summary>
    /// <param name="library">库信息</param>
    /// <param name="librariesDirectory">库文件目录</param>
    /// <returns>是否已下载且有效</returns>
    bool IsLibraryDownloaded(Library library, string librariesDirectory);
    
    /// <summary>
    /// 获取依赖库的本地文件路径
    /// </summary>
    /// <param name="libraryName">库名称（Maven 坐标格式）</param>
    /// <param name="librariesDirectory">库文件目录</param>
    /// <param name="classifier">分类器（可选，用于原生库）</param>
    /// <returns>本地文件路径</returns>
    string GetLibraryPath(string libraryName, string librariesDirectory, string? classifier = null);
    
    /// <summary>
    /// 获取缺失的依赖库列表
    /// </summary>
    /// <param name="versionInfo">版本信息</param>
    /// <param name="librariesDirectory">库文件目录</param>
    /// <returns>缺失的库列表</returns>
    IEnumerable<Library> GetMissingLibraries(VersionInfo versionInfo, string librariesDirectory);
    
    /// <summary>
    /// 检查库是否适用于当前平台
    /// </summary>
    /// <param name="library">库信息</param>
    /// <returns>是否适用</returns>
    bool IsLibraryApplicable(Library library);
}
