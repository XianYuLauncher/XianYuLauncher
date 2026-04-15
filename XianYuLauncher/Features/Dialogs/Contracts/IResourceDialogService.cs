using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface IResourceDialogService
{
    Task ShowFavoritesImportResultDialogAsync(IEnumerable<XianYuLauncher.Models.FavoritesImportResultItem> results);

    Task<ContentDialogResult> ShowDownloadMethodDialogAsync(
        string title,
        string instruction,
        IEnumerable<object>? dependencyProjects,
        bool isLoadingDependencies,
        Action<string>? onDependencyClick);

    Task<string?> ShowModpackInstallNameDialogAsync(
        string defaultName,
        string? tip = null,
        Func<string, (bool IsValid, string ErrorMessage)>? validateInput = null);

    Task<T?> ShowListSelectionDialogAsync<T>(
        string title,
        string instruction,
        IEnumerable<T> items,
        Func<T, string> displayMemberFunc,
        Func<T, double>? opacityFunc = null,
        string? tip = null,
        string? primaryButtonText = null,
        string? closeButtonText = null,
        Func<T, string>? primaryButtonTextSelector = null) where T : class;

    Task<T?> ShowModVersionSelectionDialogAsync<T>(
        string title,
        string instruction,
        IEnumerable<T> items,
        Func<T, string> versionNumberFunc,
        Func<T, string> versionTypeFunc,
        Func<T, string> releaseDateFunc,
        Func<T, string> fileNameFunc,
        Func<T, string?>? resourceTypeTagFunc = null,
        string? primaryButtonText = null,
        string? closeButtonText = null) where T : class;

    Task<List<XianYuLauncher.Models.UpdatableResourceItem>?> ShowUpdatableResourcesSelectionDialogAsync(IEnumerable<XianYuLauncher.Models.UpdatableResourceItem> availableUpdates);

    Task ShowMoveResultDialogAsync(
        IEnumerable<XianYuLauncher.Features.VersionManagement.ViewModels.MoveModResult> moveResults,
        string title,
        string instruction);

    Task ShowPublishersListDialogAsync(
        IEnumerable<PublisherDialogItem> publishers,
        bool isLoading,
        string? title = null,
        string? closeButtonText = null);
}