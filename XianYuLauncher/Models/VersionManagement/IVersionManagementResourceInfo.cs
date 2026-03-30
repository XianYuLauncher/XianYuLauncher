namespace XianYuLauncher.Models.VersionManagement;

/// <summary>
/// 版本管理资源（Mod/资源包/光影）的共有属性接口，用于泛型基类统一处理。
/// </summary>
public interface IVersionManagementResourceInfo
{
    /// <summary>文件完整路径</summary>
    string FilePath { get; }

    /// <summary>文件名</summary>
    string FileName { get; }

    /// <summary>显示名称</summary>
    string Name { get; }

    /// <summary>描述（已翻译）</summary>
    string? Description { get; set; }

    /// <summary>图标路径</summary>
    string Icon { get; set; }

    /// <summary>来源平台 (Modrinth/CurseForge)</summary>
    string? Source { get; set; }

    /// <summary>项目ID</summary>
    string? ProjectId { get; set; }

    /// <summary>稳定的社区资源实例标识</summary>
    string ResourceInstanceId { get; set; }

    /// <summary>是否有可更新版本</summary>
    bool HasUpdate { get; set; }

    /// <summary>当前版本展示文本</summary>
    string CurrentVersion { get; set; }

    /// <summary>可升级版本展示文本</summary>
    string LatestVersion { get; set; }

    /// <summary>是否选中（多选模式）</summary>
    bool IsSelected { get; set; }

    /// <summary>是否正在加载描述</summary>
    bool IsLoadingDescription { get; set; }
}
