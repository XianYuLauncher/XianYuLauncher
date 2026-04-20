using System.Diagnostics.CodeAnalysis;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Services;

// For more information on navigation between pages see
// https://github.com/microsoft/TemplateStudio/blob/main/docs/WinUI/navigation.md
public class NavigationService : INavigationService
{
    private readonly IPageService _pageService;
    private readonly ICommonDialogService _dialogService;
    private object? _lastParameterUsed;
    private Frame? _frame;
    private ILocalNavigationHost? _localNavigationHost;

    public event NavigatedEventHandler? Navigated;

    public event EventHandler? NavigationStateChanged;

    public Frame? Frame
    {
        get
        {
            if (_frame == null)
            {
                _frame = App.MainWindow.Content as Frame;
                RegisterFrameEvents();
            }

            return _frame;
        }

        set
        {
            UnregisterFrameEvents();
            _frame = value;
            RegisterFrameEvents();
        }
    }

    [MemberNotNullWhen(true, nameof(Frame), nameof(_frame))]
    public bool CanGoBack => (_localNavigationHost?.CanGoBackLocally ?? false) || (Frame != null && Frame.CanGoBack);

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
            UpdateLocalNavigationHost();
        }
    }

    private void UnregisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated -= OnNavigated;
        }

        UpdateLocalNavigationHost(null);
    }

    public bool GoBack()
    {
        UpdateLocalNavigationHost();

        // 先让当前一级页消费自己的局部返回，再决定是否回退 Shell 主 Frame。
        if (_localNavigationHost?.CanGoBackLocally == true)
        {
            return _localNavigationHost.TryGoBackLocally();
        }

        if (CanGoBack)
        {
            var vmBeforeNavigation = _frame.GetPageViewModel();
            _frame.GoBack();
            if (vmBeforeNavigation is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }

            return true;
        }

        return false;
    }

    public bool NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false)
    {
        try
        {
            var pageType = _pageService.GetPageType(pageKey);
            var currentLocalNavigationHost = _frame?.Content as ILocalNavigationHost;

            if (_frame != null
                && _frame.Content?.GetType() == pageType
                && (parameter == null || parameter.Equals(_lastParameterUsed)))
            {
                // 重复导航到同一一级页时，优先把该页的局部 detail 还原到 root，而不是直接忽略这次导航。
                if (currentLocalNavigationHost?.CanGoBackLocally == true)
                {
                    currentLocalNavigationHost.ResetLocalNavigation(useReturnTransition: true);
                    return true;
                }

                return false;
            }

            if (_frame != null && (_frame.Content?.GetType() != pageType || (parameter != null && !parameter.Equals(_lastParameterUsed))))
            {
                _frame.Tag = clearNavigation;
                var vmBeforeNavigation = _frame.GetPageViewModel();
                var navigated = _frame.Navigate(pageType, parameter);
                if (navigated)
                {
                    _lastParameterUsed = parameter;
                    if (vmBeforeNavigation is INavigationAware navigationAware)
                    {
                        navigationAware.OnNavigatedFrom();
                    }
                }

                return navigated;
            }

            return false;
        }
        catch (Exception ex)
        {
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
            UpdateLocalNavigationHost(frame.Content as ILocalNavigationHost);

            // 每次导航（含 GoBack）后同步 _lastParameterUsed，否则返回后再点同一依赖项会因参数相同被误判为“已在目标页”而不导航
            _lastParameterUsed = e.Parameter;

            var clearNavigation = frame.Tag is bool b && b;
            if (clearNavigation)
            {
                frame.BackStack.Clear();
            }

            if (frame.GetPageViewModel() is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedTo(e.Parameter);
            }

            Navigated?.Invoke(sender, e);
            NavigationStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateLocalNavigationHost(ILocalNavigationHost? newHost = null)
    {
        newHost ??= _frame?.Content as ILocalNavigationHost;
        if (ReferenceEquals(_localNavigationHost, newHost))
        {
            return;
        }

        // 当前一级页切换后要及时换绑宿主，否则 Shell 的返回状态会继续盯着旧页面的局部导航。
        if (_localNavigationHost != null)
        {
            _localNavigationHost.LocalNavigationStateChanged -= LocalNavigationHost_LocalNavigationStateChanged;
        }

        _localNavigationHost = newHost;

        if (_localNavigationHost != null)
        {
            _localNavigationHost.LocalNavigationStateChanged += LocalNavigationHost_LocalNavigationStateChanged;
        }
    }

    private void LocalNavigationHost_LocalNavigationStateChanged(object? sender, EventArgs e)
    {
        NavigationStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
