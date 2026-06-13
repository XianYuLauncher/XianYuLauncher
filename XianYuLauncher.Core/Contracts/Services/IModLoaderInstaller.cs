using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// ModLoader 安装选项
/// </summary>
public class ModLoaderInstallOptions
{
    /// <summary>
    /// 是否跳过 JAR 下载（用于重新安装加载器时，JAR 已存在的情况）
    /// </summary>
    public bool SkipJarDownload { get; set; } = false;
    
    /// <summary>
    /// 自定义版本名称（可选）
    /// </summary>
    public string? CustomVersionName { get; set; }
    
    /// <summary>
    /// 是否覆盖现有安装
    /// </summary>
    public bool OverwriteExisting { get; set; } = true;
}

/// <summary>
/// ModLoader 安装器接口，定义统一的 ModLoader 安装流程
/// </summary>
public interface IModLoaderInstaller
{
    /// <summary>
    /// ModLoader 类型名称（如 Fabric, Forge, NeoForge, Optifine, Quilt）
    /// </summary>
    string ModLoaderType { get; }
    
    /// <summary>
    /// 安装 ModLoader 版本
    /// </summary>
    /// <param name="minecraftVersionId">Minecraft 版本 ID</param>
    /// <param name="modLoaderVersion">ModLoader 版本</param>
    /// <param name="minecraftDirectory">Minecraft 目录</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="customVersionName">自定义版本名称（可选）</param>
    /// <returns>安装后的版本 ID</returns>
    Task<string> InstallAsync(
        string minecraftVersionId, 
        string modLoaderVersion,
        string minecraftDirectory,
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default,
        string? customVersionName = null);
    
    /// <summary>
    /// 安装 ModLoader 版本（带选项）
    /// </summary>
    /// <param name="minecraftVersionId">Minecraft 版本 ID</param>
    /// <param name="modLoaderVersion">ModLoader 版本</param>
    /// <param name="minecraftDirectory">Minecraft 目录</param>
    /// <param name="options">安装选项</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>安装后的版本 ID</returns>
    Task<string> InstallAsync(
        string minecraftVersionId, 
        string modLoaderVersion,
        string minecraftDirectory,
        ModLoaderInstallOptions options,
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取指定 Minecraft 版本可用的 ModLoader 版本列表
    /// </summary>
    /// <param name="minecraftVersionId">Minecraft 版本 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>可用的 ModLoader 版本列表</returns>
    Task<List<string>> GetAvailableVersionsAsync(
        string minecraftVersionId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检查指定的 ModLoader 版本是否已安装
    /// </summary>
    /// <param name="minecraftVersionId">Minecraft 版本 ID</param>
    /// <param name="modLoaderVersion">ModLoader 版本</param>
    /// <param name="minecraftDirectory">Minecraft 目录</param>
    /// <returns>是否已安装</returns>
    bool IsInstalled(string minecraftVersionId, string modLoaderVersion, string minecraftDirectory);
}

/// <summary>
/// ModLoader 安装器工厂接口
/// </summary>
public interface IModLoaderInstallerFactory
{
    /// <summary>
    /// 获取指定类型的 ModLoader 安装器
    /// </summary>
    /// <param name="modLoaderType">ModLoader 类型</param>
    /// <returns>安装器实例</returns>
    IModLoaderInstaller GetInstaller(string modLoaderType);
    
    /// <summary>
    /// 获取所有支持的 ModLoader 类型
    /// </summary>
    /// <returns>支持的 ModLoader 类型列表</returns>
    IEnumerable<string> GetSupportedModLoaderTypes();
}
