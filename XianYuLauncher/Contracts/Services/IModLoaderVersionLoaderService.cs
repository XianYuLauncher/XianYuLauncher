using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Contracts.Services;

public interface IModLoaderVersionLoaderService
{
    /// <summary>
    /// 加载指定 ModLoader 类型和 Minecraft 版本的可用版本列表
    /// </summary>
    /// <param name="modLoaderType">ModLoader 类型 (forge, fabric, neoforge, quilt, cleanroom, optifine, legacyfabric)</param>
    /// <param name="minecraftVersion">Minecraft 版本号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>版本号字符串列表</returns>
    Task<List<string>> LoadVersionsAsync(string modLoaderType, string minecraftVersion, CancellationToken cancellationToken);

    /// <summary>
    /// 获取缓存的 Fabric 版本详细信息
    /// </summary>
    FabricLoaderVersion? GetFabricVersionInfo(string version);

    /// <summary>
    /// 获取缓存的 Quilt 版本详细信息
    /// </summary>
    QuiltLoaderVersion? GetQuiltVersionInfo(string version);

    /// <summary>
    /// 获取缓存的 OptiFine 版本详细信息
    /// </summary>
    OptifineVersionInfo? GetOptifineVersionInfo(string version);

    /// <summary>
    /// 获取缓存的 LegacyFabric 版本详细信息
    /// </summary>
    FabricLoaderVersion? GetLegacyFabricVersionInfo(string version);
}
