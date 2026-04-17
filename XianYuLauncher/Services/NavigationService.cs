using System.Diagnostics.CodeAnalysis;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Helpers;
using Serilog;

namespace XianYuLauncher.Services;

// For more information on navigation between pages see
// https://github.com/microsoft/TemplateStudio/blob/main/docs/WinUI/navigation.md
public class NavigationService : INavigationService
{
    private readonly IPageService _pageService;
    private readonly ICommonDialogService _dialogService;
    private object? _lastParameterUsed;
    private Frame? _frame;

    public event NavigatedEventHandler? Navigated;

    public Frame? Frame
    {
        get
        {
            if (_frame == null)
            {
                _frame = App.MainWindow.Content as Frame;
                Log.Information("[NavigationService] Frame lazily resolved from MainWindow.Content. frameType={FrameType}", _frame?.GetType().Name ?? "<null>");
                RegisterFrameEvents();
            }

            return _frame;
        }

        set
        {
            UnregisterFrameEvents();
            Log.Information("[NavigationService] Frame updated. oldFrameType={OldFrameType}, newFrameType={NewFrameType}", _frame?.GetType().Name ?? "<null>", value?.GetType().Name ?? "<null>");
            _frame = value;
            RegisterFrameEvents();
        }
    }

    [MemberNotNullWhen(true, nameof(Frame), nameof(_frame))]
    public bool CanGoBack => Frame != null && Frame.CanGoBack;

    public NavigationService(IPageService pageService, ICommonDialogService dialogService)
    {
        _pageService = pageService;
        _dialogService = dialogService;
    }

    private void RegisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated += OnNavigated;
            Log.Information("[NavigationService] Registered frame navigation events. currentPage={CurrentPage}, backStackDepth={BackStackDepth}", _frame.Content?.GetType().Name ?? "<null>", _frame.BackStack.Count);
        }
    }

    private void UnregisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated -= OnNavigated;
            Log.Information("[NavigationService] Unregistered frame navigation events. currentPage={CurrentPage}, backStackDepth={BackStackDepth}", _frame.Content?.GetType().Name ?? "<null>", _frame.BackStack.Count);
        }
    }

    public bool GoBack()
    {
        Log.Information("[NavigationService] GoBack requested. canGoBack={CanGoBack}, currentPage={CurrentPage}, backStackDepth={BackStackDepth}", CanGoBack, _frame?.Content?.GetType().Name ?? "<null>", _frame?.BackStack.Count ?? 0);

        if (CanGoBack)
        {
            var vmBeforeNavigation = _frame.GetPageViewModel();
            _frame.GoBack();
            if (vmBeforeNavigation is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }

            Log.Information("[NavigationService] GoBack dispatched. newCurrentPage={CurrentPage}, backStackDepth={BackStackDepth}", _frame.Content?.GetType().Name ?? "<null>", _frame.BackStack.Count);

            return true;
        }

        Log.Warning("[NavigationService] GoBack ignored because frame cannot go back. currentPage={CurrentPage}, backStackDepth={BackStackDepth}", _frame?.Content?.GetType().Name ?? "<null>", _frame?.BackStack.Count ?? 0);

        return false;
    }

    public bool NavigateTo(
        string pageKey,
        object? parameter = null,
        bool clearNavigation = false,
        NavigationTransitionInfo? transitionInfo = null)
    {
        try
        {
            var pageType = _pageService.GetPageType(pageKey);
            Log.Information(
                "[NavigationService] NavigateTo requested. pageKey={PageKey}, pageType={PageType}, clearNavigation={ClearNavigation}, currentPage={CurrentPage}, backStackDepth={BackStackDepth}, parameter={Parameter}, transition={Transition}",
                pageKey,
                pageType.Name,
                clearNavigation,
                _frame?.Content?.GetType().Name ?? "<null>",
                _frame?.BackStack.Count ?? 0,
                DescribeParameter(parameter),
                transitionInfo?.GetType().Name ?? "<default>");

            if (_frame != null && (_frame.Content?.GetType() != pageType || (parameter != null && !parameter.Equals(_lastParameterUsed))))
            {
                _frame.Tag = clearNavigation;
                var vmBeforeNavigation = _frame.GetPageViewModel();
                var navigated = transitionInfo == null
                    ? _frame.Navigate(pageType, parameter)
                    : _frame.Navigate(pageType, parameter, transitionInfo);
                if (navigated)
                {
                    _lastParameterUsed = parameter;
                    if (vmBeforeNavigation is INavigationAware navigationAware)
                    {
                        navigationAware.OnNavigatedFrom();
                    }

                    Log.Information("[NavigationService] NavigateTo dispatched successfully. targetPage={TargetPage}, backStackDepth={BackStackDepth}, frameTag={FrameTag}", pageType.Name, _frame.BackStack.Count, _frame.Tag);
                }

                if (!navigated)
                {
                    Log.Warning("[NavigationService] NavigateTo returned false. targetPage={TargetPage}, currentPage={CurrentPage}, backStackDepth={BackStackDepth}", pageType.Name, _frame.Content?.GetType().Name ?? "<null>", _frame.BackStack.Count);
                }

                return navigated;
            }

            Log.Information("[NavigationService] NavigateTo skipped because target matches current page and parameter. pageType={PageType}, parameter={Parameter}", pageType.Name, DescribeParameter(parameter));

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[NavigationService] NavigateTo failed. pageKey={PageKey}, parameter={Parameter}", pageKey, DescribeParameter(parameter));
            _ = _dialogService.ShowMessageDialogAsync(
                "导航错误",
                $"无法导航到页面: {pageKey}\n\n错误信息: {ex.Message}\n\n堆栈跟踪: {ex.StackTrace}",
                "确定");
            return false;
        }
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        if (sender is Frame frame)
        {
            // 每次导航（含 GoBack）后同步 _lastParameterUsed，否则返回后再点同一依赖项会因参数相同被误判为“已在目标页”而不导航
            _lastParameterUsed = e.Parameter;

            var clearNavigation = frame.Tag is bool b && b;
            var backStackDepthBeforeClear = frame.BackStack.Count;
            if (clearNavigation)
            {
                frame.BackStack.Clear();
                frame.Tag = false;
            }

            if (frame.GetPageViewModel() is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedTo(e.Parameter);
            }

            Log.Information(
                "[NavigationService] Frame navigated. sourcePage={SourcePage}, navMode={NavigationMode}, clearNavigation={ClearNavigation}, backStackBeforeClear={BackStackBeforeClear}, backStackAfterClear={BackStackAfterClear}, parameter={Parameter}",
                e.SourcePageType?.Name ?? "<null>",
                e.NavigationMode,
                clearNavigation,
                backStackDepthBeforeClear,
                frame.BackStack.Count,
                DescribeParameter(e.Parameter));

            Navigated?.Invoke(sender, e);
        }
    }

    private static string DescribeParameter(object? parameter)
    {
        return parameter switch
        {
            null => "<null>",
            string text => $"string:{text}",
            _ => parameter.GetType().Name,
        };
    }
}
