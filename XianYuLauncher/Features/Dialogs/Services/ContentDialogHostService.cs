using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.ViewManagement;
using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class ContentDialogHostService : Contracts.IContentDialogHostService
{
    private readonly SemaphoreSlim _dialogSemaphore = new(1, 1);
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly UISettings _uiSettings = new();
    private ContentDialog? _activeDialog;

    public ContentDialogHostService(IThemeSelectorService themeSelectorService, IUiDispatcher uiDispatcher)
    {
        _themeSelectorService = themeSelectorService ?? throw new ArgumentNullException(nameof(themeSelectorService));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _uiSettings.ColorValuesChanged += OnSystemColorValuesChanged;
    }

    public async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        await _dialogSemaphore.WaitAsync();

        try
        {
            if (dialog.XamlRoot is null)
            {
                var root = App.MainWindow?.Content?.XamlRoot;
                if (root is null)
                {
                    return ContentDialogResult.None;
                }

                dialog.XamlRoot = root;
            }

            if (dialog.Style is null)
            {
                dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            }

            dialog.RequestedTheme = GetEffectiveDialogTheme();
            _activeDialog = dialog;

            try
            {
                return await dialog.ShowAsync();
            }
            catch (COMException ex) when ((uint)ex.HResult == 0x80000019)
            {
                await Task.Delay(300);
                return await dialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContentDialogHostService] 弹窗显示异常: {ex.Message}");
            return ContentDialogResult.None;
        }
        finally
        {
            _activeDialog = null;
            _dialogSemaphore.Release();
        }
    }

    private void OnSystemColorValuesChanged(UISettings sender, object args)
    {
        _uiDispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            if (_activeDialog != null && _themeSelectorService.Theme == ElementTheme.Default)
            {
                _activeDialog.RequestedTheme = GetEffectiveDialogTheme();
            }
        });
    }

    private ElementTheme GetEffectiveDialogTheme()
    {
        var theme = _themeSelectorService.Theme;
        if (theme != ElementTheme.Default)
        {
            return theme;
        }

        var background = _uiSettings.GetColorValue(UIColorType.Background);
        return background.R == 255 && background.G == 255 && background.B == 255
            ? ElementTheme.Light
            : ElementTheme.Dark;
    }
}