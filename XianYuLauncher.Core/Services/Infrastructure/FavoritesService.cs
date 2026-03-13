using System.Collections.Generic;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 收藏夹持久化服务实现，使用 favorites.json 存储于 AppData。
/// </summary>
public class FavoritesService : IFavoritesService
{
    private readonly IFileService _fileService;

    public FavoritesService(IFileService fileService)
    {
        _fileService = fileService;
    }

    /// <inheritdoc />
    public List<ModrinthProject> Load()
    {
        try
        {
            var folder = _fileService.GetAppDataPath();
            var data = _fileService.Read<List<ModrinthProject>>(folder, AppDataFileConsts.FavoritesJson);
            return data ?? new List<ModrinthProject>();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading favorites: {ex.Message}");
            return new List<ModrinthProject>();
        }
    }

    /// <inheritdoc />
    public void Save(IEnumerable<ModrinthProject> items)
    {
        try
        {
            var folder = _fileService.GetAppDataPath();
            var list = items as ICollection<ModrinthProject> ?? new List<ModrinthProject>(items ?? []);
            _fileService.Save(folder, AppDataFileConsts.FavoritesJson, list);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving favorites: {ex.Message}");
        }
    }
}
