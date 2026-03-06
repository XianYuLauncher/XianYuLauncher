using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Services;

public class FilePickerService : IFilePickerService
{
    public async Task<string?> PickSingleFilePathAsync(
        IReadOnlyList<string> fileTypeFilters,
        PickerLocationId suggestedStartLocation,
        PickerViewMode? viewMode = null,
        string? settingsIdentifier = null,
        string? commitButtonText = null)
    {
        var openPicker = new FileOpenPicker
        {
            SuggestedStartLocation = suggestedStartLocation
        };

        foreach (var filter in fileTypeFilters)
        {
            openPicker.FileTypeFilter.Add(filter);
        }

        if (viewMode.HasValue)
        {
            openPicker.ViewMode = viewMode.Value;
        }

        if (!string.IsNullOrWhiteSpace(settingsIdentifier))
        {
            openPicker.SettingsIdentifier = settingsIdentifier;
        }

        if (!string.IsNullOrWhiteSpace(commitButtonText))
        {
            openPicker.CommitButtonText = commitButtonText;
        }

        InitializeWithMainWindow(openPicker);
        var file = await openPicker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickSingleFolderPathAsync(PickerLocationId suggestedStartLocation)
    {
        var folderPicker = new FolderPicker
        {
            SuggestedStartLocation = suggestedStartLocation
        };
        folderPicker.FileTypeFilter.Add("*");

        InitializeWithMainWindow(folderPicker);
        var folder = await folderPicker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public async Task<string?> PickSaveFilePathAsync(
        string suggestedFileName,
        IReadOnlyDictionary<string, IReadOnlyList<string>> fileTypeChoices,
        PickerLocationId suggestedStartLocation)
    {
        var savePicker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
            SuggestedStartLocation = suggestedStartLocation
        };

        foreach (var choice in fileTypeChoices)
        {
            savePicker.FileTypeChoices.Add(choice.Key, choice.Value.ToList());
        }

        InitializeWithMainWindow(savePicker);
        var file = await savePicker.PickSaveFileAsync();
        return file?.Path;
    }

    private static void InitializeWithMainWindow(object picker)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }
}
