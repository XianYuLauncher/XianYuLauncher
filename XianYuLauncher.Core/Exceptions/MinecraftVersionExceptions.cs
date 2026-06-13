using System;

namespace XianYuLauncher.Core.Exceptions;

/// <summary>
/// Minecraft 版本服务相关异常的基类
/// </summary>
public class MinecraftVersionException : Exception
{
    public MinecraftVersionException(string message) : base(message) { }
    
    public MinecraftVersionException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// 下载相关异常
/// </summary>
public class DownloadException : MinecraftVersionException
{
    /// <summary>
    /// 下载 URL
    /// </summary>
    public string? Url { get; init; }
    
    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; init; }
    
    /// <summary>
    /// HTTP 状态码（如果适用）
    /// </summary>
    public int? HttpStatusCode { get; init; }
    
    public DownloadException(string message) : base(message) { }
    
    public DownloadException(string message, Exception innerException) 
        : base(message, innerException) { }
    
    public DownloadException(string message, string url, int retryCount = 0, Exception? innerException = null) 
        : base(message, innerException!)
    {
        Url = url;
        RetryCount = retryCount;
    }
}

/// <summary>
/// 文件哈希验证失败异常
/// </summary>
public class HashVerificationException : DownloadException
{
    /// <summary>
    /// 预期的哈希值
    /// </summary>
    public string? ExpectedHash { get; init; }
    
    /// <summary>
    /// 实际的哈希值
    /// </summary>
    public string? ActualHash { get; init; }
    
    public HashVerificationException(string message) : base(message) { }
    
    public HashVerificationException(string message, string expectedHash, string actualHash) 
        : base(message)
    {
        ExpectedHash = expectedHash;
        ActualHash = actualHash;
    }
}


/// <summary>
/// 依赖库未找到异常
/// </summary>
public class LibraryNotFoundException : MinecraftVersionException
{
    /// <summary>
    /// 库名称（Maven 坐标格式）
    /// </summary>
    public string? LibraryName { get; init; }
    
    /// <summary>
    /// 预期的文件路径
    /// </summary>
    public string? ExpectedPath { get; init; }
    
    public LibraryNotFoundException(string message) : base(message) { }
    
    public LibraryNotFoundException(string message, string libraryName) 
        : base(message)
    {
        LibraryName = libraryName;
    }
    
    public LibraryNotFoundException(string message, string libraryName, string expectedPath) 
        : base(message)
    {
        LibraryName = libraryName;
        ExpectedPath = expectedPath;
    }
}

/// <summary>
/// 版本未找到异常
/// </summary>
public class VersionNotFoundException : MinecraftVersionException
{
    /// <summary>
    /// 版本 ID
    /// </summary>
    public string? VersionId { get; init; }
    
    /// <summary>
    /// 是否为本地版本
    /// </summary>
    public bool IsLocalVersion { get; init; }
    
    public VersionNotFoundException(string message) : base(message) { }
    
    public VersionNotFoundException(string message, Exception innerException) 
        : base(message, innerException) { }
    
    public VersionNotFoundException(string message, string versionId, bool isLocalVersion = false) 
        : base(message)
    {
        VersionId = versionId;
        IsLocalVersion = isLocalVersion;
    }
}

/// <summary>
/// ModLoader 安装异常
/// </summary>
public class ModLoaderInstallException : MinecraftVersionException
{
    /// <summary>
    /// ModLoader 类型（如 Fabric, Forge, NeoForge）
    /// </summary>
    public string? ModLoaderType { get; init; }
    
    /// <summary>
    /// ModLoader 版本
    /// </summary>
    public string? ModLoaderVersion { get; init; }
    
    /// <summary>
    /// Minecraft 版本
    /// </summary>
    public string? MinecraftVersion { get; init; }
    
    /// <summary>
    /// 安装阶段（如 下载、解压、处理器执行）
    /// </summary>
    public string? InstallPhase { get; init; }
    
    public ModLoaderInstallException(string message) : base(message) { }
    
    public ModLoaderInstallException(string message, Exception innerException) 
        : base(message, innerException) { }
    
    public ModLoaderInstallException(
        string message, 
        string modLoaderType, 
        string modLoaderVersion, 
        string minecraftVersion,
        string? installPhase = null,
        Exception? innerException = null) 
        : base(message, innerException!)
    {
        ModLoaderType = modLoaderType;
        ModLoaderVersion = modLoaderVersion;
        MinecraftVersion = minecraftVersion;
        InstallPhase = installPhase;
    }
}

/// <summary>
/// 资源下载异常
/// </summary>
public class AssetDownloadException : MinecraftVersionException
{
    /// <summary>
    /// 资源索引 ID
    /// </summary>
    public string? AssetIndexId { get; init; }
    
    /// <summary>
    /// 失败的资源数量
    /// </summary>
    public int FailedCount { get; init; }
    
    /// <summary>
    /// 总资源数量
    /// </summary>
    public int TotalCount { get; init; }
    
    public AssetDownloadException(string message) : base(message) { }
    
    public AssetDownloadException(string message, Exception innerException) 
        : base(message, innerException) { }
    
    public AssetDownloadException(
        string message, 
        string assetIndexId, 
        int failedCount, 
        int totalCount) 
        : base(message)
    {
        AssetIndexId = assetIndexId;
        FailedCount = failedCount;
        TotalCount = totalCount;
    }
}

/// <summary>
/// 处理器执行异常
/// </summary>
public class ProcessorExecutionException : MinecraftVersionException
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public string? ProcessorName { get; init; }
    
    /// <summary>
    /// 处理器 JAR 路径
    /// </summary>
    public string? ProcessorJarPath { get; init; }
    
    /// <summary>
    /// 退出代码
    /// </summary>
    public int? ExitCode { get; init; }
    
    public ProcessorExecutionException(string message) : base(message) { }
    
    public ProcessorExecutionException(string message, Exception innerException) 
        : base(message, innerException) { }
    
    public ProcessorExecutionException(
        string message, 
        string processorName, 
        string? processorJarPath = null,
        int? exitCode = null,
        Exception? innerException = null) 
        : base(message, innerException!)
    {
        ProcessorName = processorName;
        ProcessorJarPath = processorJarPath;
        ExitCode = exitCode;
    }
}
