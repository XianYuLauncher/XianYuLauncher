using Microsoft.UI.Xaml.Controls;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface IProgressDialogService
{
    Task ShowProgressDialogAsync(string title, string message, Func<IProgress<double>, IProgress<string>, CancellationToken, Task> workCallback);

    Task<T> ShowProgressCallbackDialogAsync<T>(string title, string message, Func<IProgress<double>, Task<T>> workCallback);

    Task<ContentDialogResult> ShowObservableProgressDialogAsync(
        string title,
        Func<string> getStatus,
        Func<double> getProgress,
        Func<string> getProgressText,
        System.ComponentModel.INotifyPropertyChanged propertyChanged,
        string? primaryButtonText = null,
        string? closeButtonText = "取消",
        Task? autoCloseWhen = null,
        Func<string>? getSpeed = null);
}