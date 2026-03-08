using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 收藏夹持久化服务，负责 Modrinth 资源收藏列表的加载与保存。
/// </summary>
public interface IFavoritesService
{
    /// <summary>
    /// 从本地加载收藏列表。文件不存在或为空时返回空列表。
    /// </summary>
    List<ModrinthProject> Load();

    /// <summary>
    /// 将收藏列表持久化到本地。
    /// </summary>
    void Save(IEnumerable<ModrinthProject> items);
}
