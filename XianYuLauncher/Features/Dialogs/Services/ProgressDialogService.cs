using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Features.Dialogs.Contracts;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class ProgressDialogService : IProgressDialogService
{
    private readonly IContentDialogHostService _dialogHostService;
    private readonly IDialogThemePaletteService _dialogThemePaletteService;

    public ProgressDialogService(
        IContentDialogHostService dialogHostService,
        IDialogThemePaletteService dialogThemePaletteService)
    {
        _dialogHostService = dialogHostService ?? throw new ArgumentNullException(nameof(dialogHostService));
        _dialogThemePaletteService = dialogThemePaletteService ?? throw new ArgumentNullException(nameof(dialogThemePaletteService));
    }

    public async Task ShowProgressDialogAsync(
        string title,
        string message,
        Func<IProgress<double>, IProgress<string>, CancellationToken, Task> workCallback,
        string? closeButtonText = "取消")
    {
        var progressBar = new ProgressBar { Maximum = 100, Value = 0, MinHeight = 4, Margin = new Thickness(0, 10, 0, 10), IsIndeterminate = true };
        var statusText = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };

        var contentPanel = new StackPanel();
        contentPanel.Children.Add(statusText);
        contentPanel.Children.Add(progressBar);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = contentPanel,
            DefaultButton = ContentDialogButton.None,
        };

        if (!string.IsNullOrEmpty(closeButtonText))
        {
            dialog.CloseButtonText = closeButtonText;
        }

        var cts = new CancellationTokenSource();
        Task? backgroundWork = null;
        var allowDialogClose = false;
        var allowUserCancel = !string.IsNullOrEmpty(closeButtonText);

        dialog.Closing += (_, args) =>
        {
            if (!allowDialogClose && !allowUserCancel)
            {
                args.Cancel = true;
            }
        };

        if (allowUserCancel)
        {
            dialog.CloseButtonClick += (_, _) =>
            {
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
            };
        }

        var progress = new Progress<double>(p =>
        {
            progressBar.IsIndeterminate = false;
            progressBar.Value = p;
        });

        IProgress<string> statusProgress = new Progress<string>(status => statusText.Text = status);

        dialog.Opened += (_, _) =>
        {
            backgroundWork = Task.Run(async () =>
            {
                try
                {
                    await workCallback(progress, statusProgress, cts.Token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    statusProgress.Report($"操作失败: {ex.Message}");
                    await Task.Delay(2000);
                }
                finally
                {
                    dialog.DispatcherQueue.TryEnqueue(() =>
                    {
                        allowDialogClose = true;
                        try
                        {
                            dialog.Hide();
                        }
                        catch
                        {
                        }
                    });
                }
            });
        };

        await _dialogHostService.ShowAsync(dialog);

        if (backgroundWork != null)
        {
            try
            {
                await backgroundWork;
            }
            catch
            {
            }
        }
    }

    public async Task<T> ShowProgressCallbackDialogAsync<T>(string title, string message, Func<IProgress<double>, Task<T>> workCallback)
    {
        var progressBar = new ProgressBar { Maximum = 100, Value = 0, MinHeight = 4, Margin = new Thickness(0, 10, 0, 10), Width = 300 };
        var statusText = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        var contentPanel = new StackPanel();
        contentPanel.Children.Add(statusText);
        contentPanel.Children.Add(progressBar);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = contentPanel,
            DefaultButton = ContentDialogButton.None,
        };

        var progress = new Progress<double>(p =>
        {
            dialog.DispatcherQueue?.TryEnqueue(() =>
            {
                progressBar.IsIndeterminate = false;
                progressBar.Value = p;
            });
        });

        Task<T>? backgroundWork = null;
        dialog.Opened += (_, _) =>
        {
            backgroundWork = Task.Run(async () =>
            {
                try
                {
                    return await workCallback(progress);
                }
                finally
                {
                    dialog.DispatcherQueue?.TryEnqueue(() =>
                    {
                        try
                        {
                            dialog.Hide();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to hide dialog in ShowProgressCallbackDialogAsync: {ex}");
                        }
                    });
                }
            });
        };

        await _dialogHostService.ShowAsync(dialog);

        if (backgroundWork != null)
        {
            return await backgroundWork;
        }

        throw new InvalidOperationException("ShowProgressCallbackDialogAsync: dialog was closed before Opened event fired.");
    }

    public async Task<ContentDialogResult> ShowObservableProgressDialogAsync(
        string title,
        Func<string> getStatus,
        Func<double> getProgress,
        Func<string> getProgressText,
        System.ComponentModel.INotifyPropertyChanged propertyChanged,
        string? primaryButtonText = null,
        string? closeButtonText = "取消",
        Task? autoCloseWhen = null,
        Func<string>? getSpeed = null)
    {
        var secondaryTextBrush = _dialogThemePaletteService.GetSecondaryTextBrush();
        var statusText = new TextBlock { Text = getStatus(), FontSize = 16, TextWrapping = TextWrapping.WrapWholeWords };
        var progressBar = new ProgressBar { Value = getProgress(), Minimum = 0, Maximum = 100, Height = 8, CornerRadius = new CornerRadius(4) };

        var progressInfoPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 12 };
        var progressText = new TextBlock
        {
            Text = getProgressText(),
            FontSize = 14,
            Foreground = secondaryTextBrush,
        };
        progressInfoPanel.Children.Add(progressText);

        TextBlock? speedText = null;
        if (getSpeed != null)
        {
            speedText = new TextBlock
            {
                Text = getSpeed(),
                FontSize = 14,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
            };
            progressInfoPanel.Children.Add(speedText);
        }

        var panel = new StackPanel { Spacing = 16, Width = 400 };
        panel.Children.Add(statusText);
        panel.Children.Add(progressBar);
        panel.Children.Add(progressInfoPanel);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            DefaultButton = ContentDialogButton.None,
        };

        if (!string.IsNullOrEmpty(primaryButtonText))
        {
            dialog.PrimaryButtonText = primaryButtonText;
        }

        if (!string.IsNullOrEmpty(closeButtonText))
        {
            dialog.CloseButtonText = closeButtonText;
        }

        void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            dialog.DispatcherQueue?.TryEnqueue(() =>
            {
                statusText.Text = getStatus();
                progressBar.Value = getProgress();
                progressText.Text = getProgressText();

                if (speedText != null && getSpeed != null)
                {
                    speedText.Text = getSpeed();
                }
            });
        }

        propertyChanged.PropertyChanged += OnPropertyChanged;

        if (autoCloseWhen != null)
        {
            _ = autoCloseWhen.ContinueWith(_ =>
            {
                dialog.DispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        dialog.Hide();
                    }
                    catch
                    {
                    }
                });
            }, TaskScheduler.Default);
        }

        try
        {
            return await _dialogHostService.ShowAsync(dialog);
        }
        finally
        {
            propertyChanged.PropertyChanged -= OnPropertyChanged;
        }
    }
}