using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 版本信息管理器接口，负责版本信息的获取、缓存和管理
/// </summary>
public interface IVersionInfoManager
{
    /// <summary>
    /// 获取Minecraft版本清单
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>版本清单</returns>
    Task<VersionManifest> GetVersionManifestAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取指定版本的详细信息
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="minecraftDirectory">Minecraft目录（可选）</param>
    /// <param name="allowNetwork">是否允许网络请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>版本信息</returns>
    Task<VersionInfo> GetVersionInfoAsync(
        string versionId, 
        string? minecraftDirectory = null,
        bool allowNetwork = true,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取指定版本的原始JSON内容
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="minecraftDirectory">Minecraft目录（可选）</param>
    /// <param name="allowNetwork">是否允许网络请求</param>
    /// <param name="preferLocal">是否优先使用本地版本JSON。为 false 时将跳过本地文件，直接回退到网络清单。</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>JSON字符串</returns>
    Task<string> GetVersionInfoJsonAsync(
        string versionId, 
        string? minecraftDirectory = null,
        bool allowNetwork = true,
        bool preferLocal = true,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取已安装的版本列表
    /// </summary>
    /// <param name="minecraftDirectory">Minecraft目录（可选）</param>
    /// <returns>已安装的版本ID列表</returns>
    Task<List<string>> GetInstalledVersionsAsync(string? minecraftDirectory = null);
    
    /// <summary>
    /// 获取版本配置
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <returns>版本配置</returns>
    Task<VersionConfig?> GetVersionConfigAsync(string versionId, string minecraftDirectory);
    
    /// <summary>
    /// 保存版本配置
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <param name="config">版本配置</param>
    Task SaveVersionConfigAsync(string versionId, string minecraftDirectory, VersionConfig config);
    
}
