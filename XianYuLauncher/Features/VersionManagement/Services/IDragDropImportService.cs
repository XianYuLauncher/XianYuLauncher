using Windows.Storage;

namespace XianYuLauncher.Features.VersionManagement.Services;

public interface IDragDropImportService
{
    Task<DragDropImportResult> ImportByTabAsync(
        IReadOnlyList<IStorageItem> storageItems,
        string selectedVersionPath,
        int selectedTabIndex);
}