namespace XianYuLauncher.Core.Models;

/// <summary>
/// ModLoader 选择信息
/// </summary>
public class ModLoaderSelection
{
    /// <summary>
    /// ModLoader 类型（Forge, Fabric, OptiFine 等）
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// ModLoader 版本
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// 安装顺序（数字越小越先安装）
    /// </summary>
    public int InstallOrder { get; init; }

    /// <summary>
    /// 是否作为附加组件安装（如 OptiFine 作为 Mod 安装到 Forge）
    /// </summary>
    public bool IsAddon { get; init; }
}
