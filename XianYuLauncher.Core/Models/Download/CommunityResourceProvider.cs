namespace XianYuLauncher.Core.Models;

/// <summary>
/// 社区资源提供方，用于将下载队列资源下载接入对应的回退源体系。
/// </summary>
public enum CommunityResourceProvider
{
    Unknown = 0,
    Modrinth,
    CurseForge
}
