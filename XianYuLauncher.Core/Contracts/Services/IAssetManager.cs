using System;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 资源管理器接口，负责 Minecraft 游戏资源的下载和管理
/// </summary>
public interface IAssetManager
{
    /// <summary>
    /// 确保资源索引文件存在且有效
    /// </summary>
    /// <param name="versionId">版本 ID</param>
    /// <param name="versionInfo">版本信息</param>
    /// <param name="minecraftDirectory">Minecraft 目录</param>
    /// <param name="progressCallback">进度回调（0-100）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task EnsureAssetIndexAsync(
        string versionId, 
        VersionInfo versionInfo,
        string minecraftDirectory,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 下载所有资源对象
    /// </summary>
    /// <param name="versionId">版本 ID</param>
    /// <param name="minecraftDirectory">Minecraft 目录</param>
    /// <param name="progressCallback">进度回调（0-100）</param>
    /// <param name="currentDownloadCallback">当前下载文件回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DownloadAllAssetObjectsAsync(
        string versionId, 
        string minecraftDirectory,
        Action<double>? progressCallback = null,
        Action<string>? currentDownloadCallback = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取资源索引
    /// </summary>
    /// <param name="assetIndexId">资源索引 ID</param>
    /// <param name="minecraftDirectory">Minecraft 目录</param>
    /// <returns>资源索引数据</returns>
    Task<AssetIndexJson?> GetAssetIndexAsync(string assetIndexId, string minecraftDirectory);
    
    /// <summary>
    /// 获取缺失的资源对象数量
    /// </summary>
    /// <param name="assetIndexId">资源索引 ID</param>
    /// <param name="minecraftDirectory">Minecraft 目录</param>
    /// <returns>缺失的资源数量</returns>
    Task<int> GetMissingAssetCountAsync(string assetIndexId, string minecraftDirectory);
}
