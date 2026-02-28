using Windows.Storage;

namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class DragDropImportService : IDragDropImportService
{
    public async Task<DragDropImportResult> ImportByTabAsync(
        IReadOnlyList<IStorageItem> storageItems,
        string selectedVersionPath,
        int selectedTabIndex)
    {
        var folderType = GetFolderTypeBySelectedTab(selectedTabIndex);
        var targetFolderPath = Path.Combine(selectedVersionPath, folderType);
        Directory.CreateDirectory(targetFolderPath);

        var successCount = 0;
        var errorCount = 0;

        foreach (var item in storageItems)
        {
            try
            {
                var imported = await ImportSingleItemAsync(item, targetFolderPath);
                if (imported)
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                }
            }
            catch
            {
                errorCount++;
            }
        }

        return new DragDropImportResult
        {
            SuccessCount = successCount,
            ErrorCount = errorCount,
            FolderType = folderType
        };
    }

    private static async Task<bool> ImportSingleItemAsync(IStorageItem item, string targetFolderPath)
    {
        if (item is StorageFile file)
        {
            return await ImportFileAsync(file, targetFolderPath);
        }

        if (item is StorageFolder folder)
        {
            return await ImportFolderAsync(folder, targetFolderPath);
        }

        return false;
    }

    private static async Task<bool> ImportFileAsync(StorageFile file, string targetFolderPath)
    {
        var extension = file.FileType.ToLowerInvariant();
        if (extension != ".jar" && extension != ".zip")
        {
            return false;
        }

        var targetFolder = await StorageFolder.GetFolderFromPathAsync(targetFolderPath);
        await file.CopyAsync(targetFolder, file.Name, NameCollisionOption.ReplaceExisting);
        return true;
    }

    private static async Task<bool> ImportFolderAsync(StorageFolder sourceFolder, string targetFolderPath)
    {
        var destinationFolder = await StorageFolder.GetFolderFromPathAsync(targetFolderPath);
        await CopyFolderAsync(sourceFolder, destinationFolder);
        return true;
    }

    private static string GetFolderTypeBySelectedTab(int selectedTabIndex)
    {
        return selectedTabIndex switch
        {
            2 => "mods",
            3 => "shaderpacks",
            4 => "resourcepacks",
            5 => "screenshots",
            6 => "saves",
            _ => "mods"
        };
    }

    private static async Task CopyFolderAsync(StorageFolder sourceFolder, StorageFolder destinationFolder)
    {
        var targetFolder = await destinationFolder.CreateFolderAsync(sourceFolder.Name, CreationCollisionOption.ReplaceExisting);

        var files = await sourceFolder.GetFilesAsync();
        foreach (var file in files)
        {
            await file.CopyAsync(targetFolder, file.Name, NameCollisionOption.ReplaceExisting);
        }

        var subfolders = await sourceFolder.GetFoldersAsync();
        foreach (var subfolder in subfolders)
        {
            await CopyFolderAsync(subfolder, targetFolder);
        }
    }
}