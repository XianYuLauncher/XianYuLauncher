using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// MCIM翻译服务接口
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// 获取Modrinth项目的中文翻译
    /// </summary>
    /// <param name="projectId">Modrinth项目ID</param>
    /// <returns>翻译响应，如果翻译不可用则返回null</returns>
    Task<McimTranslationResponse?> GetModrinthTranslationAsync(string projectId);
    
    /// <summary>
    /// 获取CurseForge Mod的中文翻译
    /// </summary>
    /// <param name="modId">CurseForge Mod ID</param>
    /// <returns>翻译响应，如果翻译不可用则返回null</returns>
    Task<McimTranslationResponse?> GetCurseForgeTranslationAsync(int modId);

    /// <summary>
    /// 初始化Mod名称翻译数据
    /// </summary>
    /// <param name="dataFilePath">数据文件路径</param>
    Task InitializeNameTranslationAsync(string dataFilePath);

    /// <summary>
    /// 获取Mod翻译名称
    /// </summary>
    /// <param name="slug">Mod的Slug（Modrinth或CurseForge）</param>
    /// <param name="originalName">原始英文名</param>
    /// <returns>格式化后的名称（如：中文名 | 英文名）</returns>
    string GetTranslatedName(string slug, string originalName);
    
    /// <summary>
    /// 检查是否应该使用翻译（当前语言是否为中文）
    /// </summary>
    bool ShouldUseTranslation();
}
