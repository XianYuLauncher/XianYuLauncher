using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Serilog;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Controls;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ModLoaderSelector.Models;
using XianYuLauncher.Features.ModLoaderSelector.ViewModels;
using XianYuLauncher.Features.ModLoaderSelector.Views;
using XianYuLauncher.Features.ResourceDownload.ViewModels;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.ResourceDownload.Views;

public sealed partial class ResourceDownloadPage : Page, INavigationAware, ILocalNavigationHost
{
    public static int TargetTabIndex { get; set; }
    private const string HostedDetailBreadcrumbItemTemplateKey = "HostedDetailBreadcrumbItemTemplate";

    private readonly string _rootHeaderTitle;
    private readonly string _rootHeaderSubtitle;
    private bool _isInnerContentFrameInitialized;
    private ModLoaderSelectorPage? _activeInnerDetailPage;
    private ResourceDownloadRootPage? _activeInnerRootPage;

    public ResourceDownloadViewModel ViewModel { get; }

    public event EventHandler? LocalNavigationStateChanged;

    public bool CanGoBackLocally => _activeInnerDetailPage != null && ResourceDownloadInnerContentFrame.CanGoBack;

    public ResourceDownloadPage()
    {
        ViewModel = App.GetService<ResourceDownloadViewModel>();
        _rootHeaderTitle = ViewModel.HeaderMetadata.Title;
        _rootHeaderSubtitle = ViewModel.HeaderMetadata.Subtitle;
        DataContext = ViewModel;
        ViewModel.ModLoaderSelectorRequested += ViewModel_ModLoaderSelectorRequested;
        InitializeComponent();
        EnsureInnerContentFrame();
        ShowRootPageState();
    }

    public void OnNavigatedTo(object parameter)
    {
        EnsureInnerContentFrame();
        ResetLocalNavigation();
        ApplyProtocolNavigationParameter(parameter);
        _activeInnerRootPage?.ApplyPendingNavigationState();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        OnNavigatedTo(e.Parameter);
    }

    public void OnNavigatedFrom()
    {
        // 外层壳不主动打断 inner Frame 的 journal，回场时由 ResetLocalNavigation 统一收口。
    }

    public bool TryGoBackLocally()
    {
        if (!CanGoBackLocally)
        {
            return false;
        }

        ReturnToRootContent();
        return true;
    }

    public void ResetLocalNavigation(bool useReturnTransition = false)
    {
        if (!CanGoBackLocally && ResourceDownloadInnerContentFrame.Content is ResourceDownloadRootPage)
        {
            _activeInnerRootPage?.ApplyPendingNavigationState();
            ShowRootPageState();
            return;
        }

        if (useReturnTransition && CanGoBackLocally)
        {
            ReturnToRootContent();
            return;
        }

        DetachInnerDetailPage();
        ResetInnerContentFrameVisualState();

        if (ResourceDownloadInnerContentFrame.Content is not ResourceDownloadRootPage || ResourceDownloadInnerContentFrame.CanGoBack)
        {
            ResourceDownloadInnerContentFrame.Navigate(typeof(ResourceDownloadRootPage), ViewModel, new SuppressNavigationTransitionInfo());
            ResourceDownloadInnerContentFrame.BackStack.Clear();
            ResourceDownloadInnerContentFrame.ForwardStack.Clear();
            return;
        }

        _activeInnerRootPage?.ApplyPendingNavigationState();
        ShowRootPageState();
    }

    public ListViewSelectionMode GetSelectionMode(bool isSelectionMode)
    {
        return isSelectionMode ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
    }

    private void EnsureInnerContentFrame()
    {
        if (_isInnerContentFrameInitialized)
        {
            return;
        }

        ResourceDownloadInnerContentFrame.Navigated += ResourceDownloadInnerContentFrame_Navigated;
        ResourceDownloadInnerContentFrame.Navigate(typeof(ResourceDownloadRootPage), ViewModel, new SuppressNavigationTransitionInfo());
        _isInnerContentFrameInitialized = true;
    }

    private void ShowRootPageState()
    {
        FavoritesDropArea.Visibility = Visibility.Visible;
        ApplyRootHeaderState();
        NotifyLocalNavigationStateChanged();
    }

    private void ViewModel_ModLoaderSelectorRequested(object? sender, ModLoaderSelectorNavigationParameter e)
    {
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();
        DetachInnerDetailPage();
        FavoritesDropArea.Visibility = Visibility.Collapsed;
        ResourceDownloadInnerContentFrame.Navigate(typeof(ModLoaderSelectorPage), e, new DrillInNavigationTransitionInfo());
    }

    private void ResourceDownloadInnerContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.Content is ResourceDownloadRootPage rootPage)
        {
            DetachInnerDetailPage();
            _activeInnerRootPage = rootPage;
            rootPage.ApplyPendingNavigationState();
            ShowRootPageState();
            return;
        }

        if (e.Content is not ModLoaderSelectorPage detailPage)
        {
            DetachInnerDetailPage();
            _activeInnerDetailPage = null;
            return;
        }

        DetachInnerDetailPage();
        _activeInnerDetailPage = detailPage;
        detailPage.ResetEmbeddedVisualState();
        detailPage.ViewModel.CloseRequested += DetailPage_CloseRequested;
        detailPage.ViewModel.HeaderMetadata.PropertyChanged += ActiveDetailHeaderMetadata_PropertyChanged;
        ApplyHostedPageHeaderState(detailPage.ViewModel);
        FavoritesDropArea.Visibility = Visibility.Collapsed;
        NotifyLocalNavigationStateChanged();
    }

    private void ReturnToRootContent()
    {
        if (!ResourceDownloadInnerContentFrame.CanGoBack)
        {
            _activeInnerRootPage?.ApplyPendingNavigationState();
            ShowRootPageState();
            return;
        }

        ApplyRootHeaderState();
        FavoritesDropArea.Visibility = Visibility.Visible;
        NotifyLocalNavigationStateChanged();
        ResourceDownloadInnerContentFrame.GoBack();
    }

    private void DetachInnerDetailPage()
    {
        if (_activeInnerDetailPage == null)
        {
            return;
        }

        _activeInnerDetailPage.ViewModel.CloseRequested -= DetailPage_CloseRequested;
        _activeInnerDetailPage.ViewModel.HeaderMetadata.PropertyChanged -= ActiveDetailHeaderMetadata_PropertyChanged;
        _activeInnerDetailPage = null;
    }

    private void DetailPage_CloseRequested(object? sender, EventArgs e)
    {
        ReturnToRootContent();
    }

    private void ActiveDetailHeaderMetadata_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!TryGetActiveDetailPage(out var detailPage))
        {
            return;
        }

        ApplyHostedPageHeaderState(detailPage.ViewModel);
    }

    private void ApplyRootHeaderState()
    {
        ResourceDownloadPageHeader.Title = _rootHeaderTitle;
        ResourceDownloadPageHeader.Subtitle = _rootHeaderSubtitle;
        ResourceDownloadPageHeader.ShowBreadcrumb = false;
        ResourceDownloadPageHeader.BreadcrumbItems = null;
        ApplyHeaderPresentationMode(ViewModel.HeaderPresentationMode);
    }

    private void ApplyHostedPageHeaderState(IPageHeaderAware pageHeaderAware)
    {
        ResourceDownloadPageHeader.Title = pageHeaderAware.HeaderMetadata.Title;
        ResourceDownloadPageHeader.Subtitle = pageHeaderAware.HeaderMetadata.Subtitle;
        ResourceDownloadPageHeader.ShowBreadcrumb = pageHeaderAware.HeaderMetadata.ShowBreadcrumb;
        ResourceDownloadPageHeader.BreadcrumbItems = pageHeaderAware.HeaderMetadata.BreadcrumbItems;
        ApplyHeaderPresentationMode(pageHeaderAware.HeaderPresentationMode);
    }

    private void ApplyHeaderPresentationMode(PageHeaderPresentationMode headerPresentationMode)
    {
        switch (headerPresentationMode)
        {
            case PageHeaderPresentationMode.ProminentBreadcrumb:
                ResourceDownloadPageHeader.ShowPrimaryHeading = false;
                ResourceDownloadPageHeader.BreadcrumbFontSize = 28;
                ResourceDownloadPageHeader.BreadcrumbMargin = new Thickness(-2, -11, 0, 12);
                ResourceDownloadPageHeader.BreadcrumbItemTemplate = Resources[HostedDetailBreadcrumbItemTemplateKey] as DataTemplate;
                return;
        }

        ResourceDownloadPageHeader.ShowPrimaryHeading = true;
        ResourceDownloadPageHeader.BreadcrumbFontSize = 15;
        ResourceDownloadPageHeader.BreadcrumbMargin = new Thickness(0, 0, 0, 12);
        ResourceDownloadPageHeader.BreadcrumbItemTemplate = null;
    }

    private void ResetInnerContentFrameVisualState()
    {
        ResourceDownloadInnerContentFrame.Opacity = 1;
        if (!TryGetActiveDetailPage(out var detailPage))
        {
            return;
        }

        detailPage.ResetEmbeddedVisualState();
    }

    private void NotifyLocalNavigationStateChanged()
    {
        LocalNavigationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DetailHeader_BuiltInIconSelected(object? sender, VersionIconSelectedEventArgs e)
    {
        if (!TryGetActiveDetailPage(out var detailPage))
        {
            return;
        }

        detailPage.ApplyBuiltInIcon(e.IconOption);
    }

    private async void DetailHeader_CustomIconRequested(object? sender, EventArgs e)
    {
        if (!TryGetActiveDetailPage(out var detailPage))
        {
            return;
        }

        await detailPage.RequestCustomIconAsync();
    }

    private bool TryGetActiveDetailPage([NotNullWhen(true)] out ModLoaderSelectorPage? detailPage)
    {
        detailPage = _activeInnerDetailPage;
        return detailPage is not null;
    }

    private void PageHeader_BreadcrumbItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Item is not NavigationBreadcrumbItem breadcrumbItem || !breadcrumbItem.CanNavigate)
        {
            return;
        }

        ReturnToRootContent();
    }

    private void ApplyProtocolNavigationParameter(object parameter)
    {
        if (!ProtocolNavigationParameterHelper.TryGetStringParameter(parameter, "tab", out var tab)
            || !TryMapTabToIndex(tab, out var tabIndex))
        {
            if (parameter is not null)
            {
                Log.Warning("[Protocol.ResourceDownload] parameter found but tab is missing/invalid.");
            }

            return;
        }

        Log.Information("[Protocol.ResourceDownload] Apply tab='{Tab}', index={Index}.", tab, tabIndex);
        ViewModel.SelectedTabIndex = tabIndex;
        TargetTabIndex = tabIndex;
        _activeInnerRootPage?.ApplyPendingNavigationState();
    }

    private static bool TryMapTabToIndex(string tab, out int index)
    {
        index = tab.Trim().ToLowerInvariant() switch
        {
            "version" => 0,
            "mod" => 1,
            "shaderpack" => 2,
            "resourcepack" => 3,
            "datapack" => 4,
            "modpack" => 5,
            "world" => 6,
            _ => -1,
        };

        return index >= 0;
    }

    private void FavoritesDropArea_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "加入收藏夹";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsContentVisible = true;
        e.DragUIOverride.IsGlyphVisible = true;

        if (sender is Control control)
        {
            control.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        }
    }

    private void FavoritesDropArea_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Control control)
        {
            control.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private void FavoritesDropArea_Drop(object sender, DragEventArgs e)
    {
        if (sender is Control control)
        {
            control.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        if (e.DataView.Properties.TryGetValue("DraggedItem", out var item) && item is ModrinthProject project)
        {
            ViewModel.AddToFavoritesCommand.Execute(project);
        }
    }

    private void FavoritesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedFavorites.Clear();

        if (sender is not ListView listView)
        {
            return;
        }

        foreach (var item in listView.SelectedItems)
        {
            if (item is ModrinthProject project)
            {
                ViewModel.SelectedFavorites.Add(project);
            }
        }
    }

    private async void FavoritesListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ViewModel.IsFavoritesSelectionMode || e.ClickedItem is not ModrinthProject project)
        {
            return;
        }

        switch (project.ProjectType?.ToLowerInvariant() ?? "mod")
        {
            case "resourcepack":
                await ViewModel.DownloadResourcePackCommand.ExecuteAsync(project);
                break;
            case "shader":
            case "shaderpack":
                await ViewModel.DownloadShaderPackCommand.ExecuteAsync(project);
                break;
            case "modpack":
                await ViewModel.DownloadModpackCommand.ExecuteAsync(project);
                break;
            case "datapack":
                await ViewModel.DownloadDatapackCommand.ExecuteAsync(project);
                break;
            case "world":
                await ViewModel.NavigateToWorldDetailCommand.ExecuteAsync(project);
                break;
            default:
                await ViewModel.DownloadModCommand.ExecuteAsync(project);
                break;
        }
    }
}