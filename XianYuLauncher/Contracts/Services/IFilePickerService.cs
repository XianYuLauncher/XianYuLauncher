using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace XianYuLauncher.Contracts.Services;

/// <summary>
/// 文件/目录选择器抽象，屏蔽 WinUI Window Handle 初始化细节。
/// </summary>
public interface IFilePickerService
{
    Task<string?> PickSingleFilePathAsync(
        IReadOnlyList<string> fileTypeFilters,
        PickerLocationId suggestedStartLocation,
        PickerViewMode? viewMode = null,
        string? settingsIdentifier = null,
        string? commitButtonText = null);

    Task<IReadOnlyList<string>> PickMultipleFilePathsAsync(
        IReadOnlyList<string> fileTypeFilters,
        PickerLocationId suggestedStartLocation,
        PickerViewMode? viewMode = null,
        string? settingsIdentifier = null,
        string? commitButtonText = null);

    Task<string?> PickSingleFolderPathAsync(PickerLocationId suggestedStartLocation);

    Task<string?> PickSaveFilePathAsync(
        string suggestedFileName,
        IReadOnlyDictionary<string, IReadOnlyList<string>> fileTypeChoices,
        PickerLocationId suggestedStartLocation);
}
