using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.System;
using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Services;

public class ApplicationLifecycleService : IApplicationLifecycleService
{
    private readonly IUiDispatcher _uiDispatcher;

    public ApplicationLifecycleService(IUiDispatcher uiDispatcher)
    {
        _uiDispatcher = uiDispatcher;
    }

    public async Task RestartApplicationAsync()
    {
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(exePath))
        {
            System.Diagnostics.Process.Start(exePath);
        }

        await ShutdownApplicationAsync();
    }

    public Task ShutdownApplicationAsync()
    {
        return _uiDispatcher.RunOnUiThreadAsync(() => Application.Current.Exit());
    }

    public async Task<bool> OpenFolderAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return false;
        }

        var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
        return await Launcher.LaunchFolderAsync(folder);
    }

    public bool OpenFolderInExplorer(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        System.Diagnostics.Process.Start("explorer.exe", folderPath);
        return true;
    }

    public Task<bool> OpenUriAsync(Uri uri)
    {
        return Launcher.LaunchUriAsync(uri).AsTask();
    }
}
