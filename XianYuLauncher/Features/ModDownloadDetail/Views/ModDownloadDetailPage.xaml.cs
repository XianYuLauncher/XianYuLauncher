using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using Serilog;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Features.ModDownloadDetail.Models;
using XianYuLauncher.Features.ResourceDownload.ViewModels;
using XianYuLauncher.Helpers;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.ModDownloadDetail.Views
{
    /// <summary>
    /// Mod 下载详情宿主页，统一承载 Header 与页内多层导航。
    /// </summary>
    public sealed partial class ModDownloadDetailPage : Page, INavigationAware, ILocalNavigationHost
    {
        private const string HostedDetailBreadcrumbItemTemplateKey = "HostedDetailBreadcrumbItemTemplate";
        private readonly INavigationService _navigationService;
        private bool _isInnerContentFrameInitialized;
        private IHostedLocalPage? _activeHostedLocalPage;

        public event EventHandler? LocalNavigationStateChanged;

        public bool CanGoBackLocally => _activeHostedLocalPage != null && ModDownloadDetailInnerContentFrame.CanGoBack;

        public ModDownloadDetailPage()
        {
            _navigationService = App.GetService<INavigationService>();
            InitializeComponent();
            EnsureInnerContentFrame();
        }

        public void OnNavigatedTo(object parameter)
        {
            EnsureInnerContentFrame();
            TryNavigateToRootContent(parameter, suppressTransition: true);
        }

        public void OnNavigatedFrom()
        {
            DetachHostedLocalPage();
        }

        public bool TryGoBackLocally()
        {
            if (!TryGetPreviousLocalBreadcrumbItem(out var previousBreadcrumbItem))
            {
                return false;
            }

            return TryNavigateLocally(previousBreadcrumbItem, useReturnTransition: true);
        }

        public bool CanNavigateLocally(NavigationBreadcrumbItem breadcrumbItem)
        {
            return TryGetLocalNavigationBackPlan(breadcrumbItem, out _);
        }

        public bool TryNavigateLocally(NavigationBreadcrumbItem breadcrumbItem, bool useReturnTransition = false)
        {
            if (!TryGetLocalNavigationBackPlan(breadcrumbItem, out var backSteps))
            {
                return false;
            }

            return NavigateBackLocally(backSteps, useReturnTransition);
        }

        public void ResetLocalNavigation(bool useReturnTransition = false)
        {
            if (!CanGoBackLocally)
            {
                return;
            }

            if (useReturnTransition && TryReturnToLocalRoot(useReturnTransition: true))
            {
                return;
            }

            NavigateBackLocally(ModDownloadDetailInnerContentFrame.BackStack.Count, useReturnTransition);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.Back && ModDownloadDetailInnerContentFrame.Content is not null)
            {
                return;
            }

            EnsureInnerContentFrame();
            TryNavigateToRootContent(e.Parameter, suppressTransition: true);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            OnNavigatedFrom();
        }

        private void EnsureInnerContentFrame()
        {
            if (_isInnerContentFrameInitialized)
            {
                return;
            }

            ModDownloadDetailInnerContentFrame.Navigated += ModDownloadDetailInnerContentFrame_Navigated;
            _isInnerContentFrameInitialized = true;
        }

        private void NavigateToRootContent(ModDownloadDetailNavigationParameter parameter, bool suppressTransition)
        {
            ResetInnerContentFrameVisualState();
            DetachHostedLocalPage();

            NavigationTransitionInfo transition = suppressTransition
                ? new SuppressNavigationTransitionInfo()
                : new EntranceNavigationTransitionInfo();

            ModDownloadDetailInnerContentFrame.Navigate(typeof(ModDownloadDetailContentPage), parameter, transition);
            ModDownloadDetailInnerContentFrame.BackStack.Clear();
            ModDownloadDetailInnerContentFrame.ForwardStack.Clear();
        }

        private bool TryNavigateToRootContent(object? parameter, bool suppressTransition)
        {
            if (!TryNormalizeNavigationParameter(parameter, out var navigationParameter, out var errorMessage))
            {
                HandleInvalidNavigationParameter(parameter, errorMessage);
                return false;
            }

            NavigateToRootContent(navigationParameter, suppressTransition);
            return true;
        }

        private void HandleInvalidNavigationParameter(object? parameter, string errorMessage)
        {
            DetachHostedLocalPage();
            ClearHeaderState();
            ClearInnerContentFrameState();

            Log.Warning(
                "[Navigation.ModDownloadDetail] Ignore invalid navigation parameter. ParameterType={ParameterType}; Reason={Reason}",
                parameter?.GetType().FullName ?? "(null)",
                errorMessage);

            var dispatcherQueue = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            if (dispatcherQueue == null)
            {
                if (_navigationService.CanGoBack)
                {
                    _navigationService.GoBack();
                }

                return;
            }

            dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                if (_navigationService.CanGoBack)
                {
                    _navigationService.GoBack();
                }
            });
        }

        private void ClearInnerContentFrameState()
        {
            ModDownloadDetailInnerContentFrame.Content = null;
            ModDownloadDetailInnerContentFrame.BackStack.Clear();
            ModDownloadDetailInnerContentFrame.ForwardStack.Clear();
            NotifyLocalNavigationStateChanged();
        }

        private void ModDownloadDetailInnerContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            DetachHostedLocalPage();

            if (e.Content is not IHostedLocalPage hostedLocalPage)
            {
                ClearHeaderState();
                NotifyLocalNavigationStateChanged();
                return;
            }

            _activeHostedLocalPage = hostedLocalPage;
            hostedLocalPage.ResetEmbeddedVisualState();
            hostedLocalPage.CloseRequested += HostedLocalPage_CloseRequested;
            hostedLocalPage.HeaderSource.HeaderMetadata.PropertyChanged += ActiveHostedHeaderMetadata_PropertyChanged;

            if (hostedLocalPage is ModDownloadDetailContentPage detailContentPage)
            {
                detailContentPage.DetailNavigationRequested += DetailContentPage_DetailNavigationRequested;
            }

            ApplyHostedPageHeaderState(hostedLocalPage.HeaderSource);
            NotifyLocalNavigationStateChanged();
        }

        private void DetachHostedLocalPage()
        {
            if (_activeHostedLocalPage == null)
            {
                return;
            }

            _activeHostedLocalPage.CloseRequested -= HostedLocalPage_CloseRequested;
            _activeHostedLocalPage.HeaderSource.HeaderMetadata.PropertyChanged -= ActiveHostedHeaderMetadata_PropertyChanged;

            if (_activeHostedLocalPage is ModDownloadDetailContentPage detailContentPage)
            {
                detailContentPage.DetailNavigationRequested -= DetailContentPage_DetailNavigationRequested;
            }

            _activeHostedLocalPage = null;
        }

        private void DetailContentPage_DetailNavigationRequested(object? sender, ModDownloadDetailNavigationRequestedEventArgs e)
        {
            EnsureInnerContentFrame();
            ResetInnerContentFrameVisualState();
            ModDownloadDetailInnerContentFrame.Navigate(
                typeof(ModDownloadDetailContentPage),
                e.NavigationParameter,
                new DrillInNavigationTransitionInfo());
        }

        private void HostedLocalPage_CloseRequested(object? sender, EventArgs e)
        {
            if (TryGoBackLocally())
            {
                return;
            }

            if (_navigationService.CanGoBack)
            {
                _navigationService.GoBack();
            }
        }

        private void ActiveHostedHeaderMetadata_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!TryGetActiveHostedLocalPage(out var hostedLocalPage))
            {
                return;
            }

            ApplyHostedPageHeaderState(hostedLocalPage.HeaderSource);
        }

        private void ApplyHostedPageHeaderState(IPageHeaderAware pageHeaderAware)
        {
            ModDownloadDetailPageHeader.Title = pageHeaderAware.HeaderMetadata.Title;
            ModDownloadDetailPageHeader.Subtitle = pageHeaderAware.HeaderMetadata.Subtitle;
            ModDownloadDetailPageHeader.ShowBreadcrumb = pageHeaderAware.HeaderMetadata.ShowBreadcrumb;
            ModDownloadDetailPageHeader.BreadcrumbItems = pageHeaderAware.HeaderMetadata.BreadcrumbItems;
            ApplyHeaderPresentationMode(pageHeaderAware.HeaderPresentationMode);
        }

        private void ClearHeaderState()
        {
            ModDownloadDetailPageHeader.Title = string.Empty;
            ModDownloadDetailPageHeader.Subtitle = string.Empty;
            ModDownloadDetailPageHeader.ShowBreadcrumb = false;
            ModDownloadDetailPageHeader.BreadcrumbItems = null;
            ApplyHeaderPresentationMode(PageHeaderPresentationMode.Standard);
        }

        private void ApplyHeaderPresentationMode(PageHeaderPresentationMode headerPresentationMode)
        {
            switch (headerPresentationMode)
            {
                case PageHeaderPresentationMode.ProminentBreadcrumb:
                    ModDownloadDetailPageHeader.ShowPrimaryHeading = false;
                    ModDownloadDetailPageHeader.BreadcrumbFontSize = 28;
                    ModDownloadDetailPageHeader.BreadcrumbMargin = new Thickness(-2, -11, 0, 12);
                    ModDownloadDetailPageHeader.BreadcrumbItemTemplate = Resources[HostedDetailBreadcrumbItemTemplateKey] as DataTemplate;
                    return;
            }

            ModDownloadDetailPageHeader.ShowPrimaryHeading = true;
            ModDownloadDetailPageHeader.BreadcrumbFontSize = 15;
            ModDownloadDetailPageHeader.BreadcrumbMargin = new Thickness(0, 0, 0, 12);
            ModDownloadDetailPageHeader.BreadcrumbItemTemplate = null;
        }

        private void ResetInnerContentFrameVisualState()
        {
            ModDownloadDetailInnerContentFrame.Opacity = 1;

            if (!TryGetActiveHostedLocalPage(out var hostedLocalPage))
            {
                return;
            }

            hostedLocalPage.ResetEmbeddedVisualState();
        }

        private void NotifyLocalNavigationStateChanged()
        {
            LocalNavigationStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool TryGetActiveHostedLocalPage([NotNullWhen(true)] out IHostedLocalPage? hostedLocalPage)
        {
            hostedLocalPage = _activeHostedLocalPage;
            return hostedLocalPage is not null;
        }

        private void PageHeader_BreadcrumbItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            if (args.Item is not NavigationBreadcrumbItem breadcrumbItem || !breadcrumbItem.CanNavigate)
            {
                return;
            }

            if (breadcrumbItem.HasLocalNavigationTarget && TryNavigateLocally(breadcrumbItem, useReturnTransition: true))
            {
                return;
            }

            if (breadcrumbItem.HasGlobalNavigationTarget)
            {
                _navigationService.NavigateTo(breadcrumbItem.PageKey!, breadcrumbItem.NavigationParameter);
            }
        }

        private bool TryReturnToLocalRoot(bool useReturnTransition)
        {
            if (TryGetLocalRootBreadcrumbItem(out var rootBreadcrumbItem))
            {
                return TryNavigateLocally(rootBreadcrumbItem, useReturnTransition);
            }

            if (!CanGoBackLocally)
            {
                return false;
            }

            return NavigateBackLocally(ModDownloadDetailInnerContentFrame.BackStack.Count, useReturnTransition);
        }

        private bool TryGetLocalRootBreadcrumbItem([NotNullWhen(true)] out NavigationBreadcrumbItem? rootBreadcrumbItem)
        {
            if (!TryGetCurrentBreadcrumbItems(out var breadcrumbItems))
            {
                rootBreadcrumbItem = null;
                return false;
            }

            rootBreadcrumbItem = LocalBreadcrumbNavigationPlanner.FindLocalRootBreadcrumb(breadcrumbItems);
            return rootBreadcrumbItem is not null;
        }

        private bool TryGetPreviousLocalBreadcrumbItem([NotNullWhen(true)] out NavigationBreadcrumbItem? previousBreadcrumbItem)
        {
            if (!TryGetCurrentBreadcrumbItems(out var breadcrumbItems))
            {
                previousBreadcrumbItem = null;
                return false;
            }

            previousBreadcrumbItem = LocalBreadcrumbNavigationPlanner.FindPreviousLocalBreadcrumb(breadcrumbItems);
            return previousBreadcrumbItem is not null;
        }

        private bool TryGetLocalNavigationBackPlan(NavigationBreadcrumbItem breadcrumbItem, out int backSteps)
        {
            backSteps = 0;

            if (!breadcrumbItem.HasLocalNavigationTarget || !TryGetCurrentBreadcrumbItems(out var breadcrumbItems))
            {
                return false;
            }

            return LocalBreadcrumbNavigationPlanner.TryCreateBackPlan(breadcrumbItems, breadcrumbItem, out backSteps, out _)
                && ModDownloadDetailInnerContentFrame.CanGoBack;
        }

        private bool TryGetCurrentBreadcrumbItems([NotNullWhen(true)] out IReadOnlyList<NavigationBreadcrumbItem>? breadcrumbItems)
        {
            if (!TryGetActiveHostedLocalPage(out var hostedLocalPage))
            {
                breadcrumbItems = null;
                return false;
            }

            breadcrumbItems = hostedLocalPage.HeaderSource.HeaderMetadata.BreadcrumbItems;
            return breadcrumbItems.Count > 0;
        }

        private bool NavigateBackLocally(int backSteps, bool useReturnTransition)
        {
            if (backSteps <= 0 || !ModDownloadDetailInnerContentFrame.CanGoBack)
            {
                return false;
            }

            ResetInnerContentFrameVisualState();
            NotifyLocalNavigationStateChanged();

            var suppressTransition = !useReturnTransition;

            for (var step = 0; step < backSteps && ModDownloadDetailInnerContentFrame.CanGoBack; step++)
            {
                if (suppressTransition)
                {
                    ModDownloadDetailInnerContentFrame.GoBack(new SuppressNavigationTransitionInfo());
                    continue;
                }

                ModDownloadDetailInnerContentFrame.GoBack();
            }

            return true;
        }

        private static bool TryNormalizeNavigationParameter(
            object? parameter,
            [NotNullWhen(true)] out ModDownloadDetailNavigationParameter? navigationParameter,
            out string errorMessage)
        {
            try
            {
                navigationParameter = NormalizeNavigationParameter(parameter);
                errorMessage = string.Empty;
                return true;
            }
            catch (ArgumentException ex)
            {
                navigationParameter = null;
                errorMessage = ex.Message;
                return false;
            }
        }

        private static ModDownloadDetailNavigationParameter NormalizeNavigationParameter(object? parameter)
        {
            if (parameter is ModDownloadDetailNavigationParameter navigationParameter)
            {
                var normalizedProjectId = NormalizeProjectId(
                    navigationParameter.ProjectId,
                    navigationParameter.Project?.ProjectId,
                    nameof(parameter));

                if (string.Equals(normalizedProjectId, navigationParameter.ProjectId, StringComparison.Ordinal))
                {
                    return navigationParameter;
                }

                return new ModDownloadDetailNavigationParameter
                {
                    ProjectId = normalizedProjectId,
                    Project = navigationParameter.Project,
                    DisplayTitleHint = navigationParameter.DisplayTitleHint,
                    SourceType = navigationParameter.SourceType,
                    BreadcrumbRootLabel = navigationParameter.BreadcrumbRootLabel,
                    BreadcrumbRootPageKey = navigationParameter.BreadcrumbRootPageKey,
                    BreadcrumbRootNavigationParameter = navigationParameter.BreadcrumbRootNavigationParameter,
                    BreadcrumbRootTarget = navigationParameter.BreadcrumbRootTarget,
                    LocalBreadcrumbTrail = navigationParameter.LocalBreadcrumbTrail,
                };
            }

            if (parameter is Tuple<XianYuLauncher.Core.Models.ModrinthProject, string> tuple)
            {
                return CreateLegacyNavigationParameter(
                    NormalizeProjectId(tuple.Item1.ProjectId, null, nameof(parameter)),
                    tuple.Item1,
                    tuple.Item2);
            }

            if (parameter is XianYuLauncher.Core.Models.ModrinthProject mod)
            {
                return CreateLegacyNavigationParameter(
                    NormalizeProjectId(mod.ProjectId, null, nameof(parameter)),
                    mod,
                    null);
            }

            if (parameter is string modId)
            {
                return CreateLegacyNavigationParameter(
                    NormalizeProjectId(modId, null, nameof(parameter)),
                    null,
                    null);
            }

            throw new ArgumentException("无法识别的 Mod 下载详情导航参数，且无法解析出有效的项目 ID。", nameof(parameter));
        }

        private static string NormalizeProjectId(string? projectId, string? fallbackProjectId, string parameterName)
        {
            if (!string.IsNullOrWhiteSpace(projectId))
            {
                return projectId;
            }

            if (!string.IsNullOrWhiteSpace(fallbackProjectId))
            {
                return fallbackProjectId;
            }

            throw new ArgumentException("导航参数缺少有效的项目 ID。", parameterName);
        }

        private static ModDownloadDetailNavigationParameter CreateLegacyNavigationParameter(
            string projectId,
            XianYuLauncher.Core.Models.ModrinthProject? project,
            string? sourceType)
        {
            var navigationParameter = new ModDownloadDetailNavigationParameter
            {
                ProjectId = projectId,
                Project = project,
                DisplayTitleHint = project?.DisplayTitle ?? string.Empty,
                SourceType = sourceType,
            };

            if (!TryCreateResourceDownloadRoot(sourceType, out var rootLabel, out var rootPageKey, out var rootNavigationParameter))
            {
                return navigationParameter;
            }

            return new ModDownloadDetailNavigationParameter
            {
                ProjectId = projectId,
                Project = project,
                DisplayTitleHint = project?.DisplayTitle ?? string.Empty,
                SourceType = sourceType,
                BreadcrumbRootLabel = rootLabel,
                BreadcrumbRootPageKey = rootPageKey,
                BreadcrumbRootNavigationParameter = rootNavigationParameter,
            };
        }

        private static bool TryCreateResourceDownloadRoot(
            string? sourceType,
            out string rootLabel,
            out string? rootPageKey,
            out object? rootNavigationParameter)
        {
            rootLabel = string.Empty;
            rootPageKey = null;
            rootNavigationParameter = null;

            if (!TryMapSourceTypeToResourceDownloadTab(sourceType, out var tabKey))
            {
                return false;
            }

            rootLabel = "ResourceDownloadPage_HeaderTitle".GetLocalized();
            rootPageKey = typeof(ResourceDownloadViewModel).FullName!;
            rootNavigationParameter = new Dictionary<string, string>
            {
                ["tab"] = tabKey,
            };
            return true;
        }

        private static bool TryMapSourceTypeToResourceDownloadTab(string? sourceType, out string tabKey)
        {
            tabKey = sourceType?.Trim().ToLowerInvariant() switch
            {
                "mod" => "mod",
                "shader" => "shaderpack",
                "resourcepack" => "resourcepack",
                "datapack" => "datapack",
                "modpack" => "modpack",
                "world" => "world",
                _ => string.Empty,
            };

            return !string.IsNullOrWhiteSpace(tabKey);
        }
    }
}
