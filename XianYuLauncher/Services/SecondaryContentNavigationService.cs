using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Services;

public sealed class SecondaryContentNavigationService : ISecondaryContentNavigationService
{
    private Canvas? _overlayCanvas;
    private Border? _overlayHost;
    private Frame? _overlayFrame;
    private FrameworkElement? _coordinateRoot;
    private FrameworkElement? _activeHost;
    private bool _isClosing;

    public event EventHandler? StateChanged;

    public bool IsActive => _overlayFrame?.Content is not null;

    public FrameworkElement? ActiveHost => _activeHost;

    public void Initialize(Canvas overlayCanvas, Border overlayHost, Frame overlayFrame, FrameworkElement coordinateRoot)
    {
        if (_overlayFrame != null)
        {
            _overlayFrame.Navigated -= OverlayFrame_Navigated;
        }

        DetachHost();

        _overlayCanvas = overlayCanvas;
        _overlayHost = overlayHost;
        _overlayFrame = overlayFrame;
        _coordinateRoot = coordinateRoot;
        _overlayFrame.Navigated += OverlayFrame_Navigated;

        HideOverlay(clearContent: true);
    }

    public bool Navigate(FrameworkElement hostElement, Type pageType, object? parameter = null, NavigationTransitionInfo? transitionInfo = null)
    {
        if (_overlayCanvas is null || _overlayHost is null || _overlayFrame is null || _coordinateRoot is null)
        {
            return false;
        }

        if (!hostElement.IsLoaded || hostElement.XamlRoot is null)
        {
            return false;
        }

        if (_activeHost != hostElement)
        {
            HideOverlay(clearContent: true);
            AttachHost(hostElement);
        }
        else
        {
            UpdateOverlayBounds();
        }

        ShowOverlay();

        return _overlayFrame.Navigate(pageType, parameter, transitionInfo ?? new DrillInNavigationTransitionInfo());
    }

    public bool GoBack(NavigationTransitionInfo? transitionInfo = null)
    {
        if (_overlayFrame?.Content is null)
        {
            return false;
        }

        if (_isClosing)
        {
            return true;
        }

        _ = PlayCloseAnimationAndCleanupAsync();
        return true;
    }

    public TViewModel? GetCurrentViewModel<TViewModel>(FrameworkElement hostElement) where TViewModel : class
    {
        if (_activeHost != hostElement || _overlayFrame is null)
        {
            return null;
        }

        return _overlayFrame.GetPageViewModel() as TViewModel;
    }

    public void Close()
    {
        _isClosing = false;

        if (_overlayFrame?.GetPageViewModel() is INavigationAware navigationAware)
        {
            navigationAware.OnNavigatedFrom();
        }

        HideOverlay(clearContent: true);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowOverlay()
    {
        if (_overlayCanvas is null || _overlayHost is null || _overlayFrame is null)
        {
            return;
        }

        ResetOverlayVisualState();
        UpdateOverlayBounds();
        _overlayCanvas.Visibility = Visibility.Visible;
        _overlayCanvas.IsHitTestVisible = true;
        _overlayHost.Visibility = Visibility.Visible;
        _overlayFrame.IsHitTestVisible = true;
    }

    private void HideOverlay(bool clearContent)
    {
        if (_overlayCanvas != null)
        {
            _overlayCanvas.Visibility = Visibility.Collapsed;
            _overlayCanvas.IsHitTestVisible = false;
        }

        if (_overlayHost != null)
        {
            _overlayHost.Visibility = Visibility.Collapsed;
            _overlayHost.Width = 0;
            _overlayHost.Height = 0;
            Canvas.SetLeft(_overlayHost, 0);
            Canvas.SetTop(_overlayHost, 0);
        }

        if (_overlayFrame != null)
        {
            _overlayFrame.IsHitTestVisible = false;
            _overlayFrame.Opacity = 1;

            if (clearContent)
            {
                _overlayFrame.Content = null;
                _overlayFrame.BackStack.Clear();
            }
        }

        ResetOverlayVisualState();

        DetachHost();
    }

    private async Task PlayCloseAnimationAndCleanupAsync()
    {
        if (_overlayHost is null || _overlayFrame is null)
        {
            Close();
            return;
        }

        _isClosing = true;
        _overlayFrame.IsHitTestVisible = false;

        try
        {
            await RunCloseAnimationAsync(_overlayHost);
        }
        finally
        {
            _isClosing = false;
            Close();
        }
    }

    private static Task RunCloseAnimationAsync(FrameworkElement target)
    {
        var visual = ElementCompositionPreview.GetElementVisual(target);
        var compositor = visual.Compositor;

        visual.CenterPoint = new System.Numerics.Vector3((float)(target.ActualWidth / 2d), (float)(target.ActualHeight / 2d), 0f);

        var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(0f, new System.Numerics.Vector3(1f, 1f, 1f));
        scaleAnimation.InsertKeyFrame(1f, new System.Numerics.Vector3(0.965f, 0.965f, 1f), compositor.CreateCubicBezierEasingFunction(
            new System.Numerics.Vector2(0.2f, 0f), new System.Numerics.Vector2(0f, 1f)));
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(220);

        var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(0f, 1f);
        opacityAnimation.InsertKeyFrame(1f, 0f);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(180);

        var batch = compositor.CreateScopedBatch(Microsoft.UI.Composition.CompositionBatchTypes.Animation);
        visual.StartAnimation(nameof(visual.Scale), scaleAnimation);
        visual.StartAnimation(nameof(visual.Opacity), opacityAnimation);

        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        batch.Completed += (_, _) => completionSource.TrySetResult();
        batch.End();

        return completionSource.Task;
    }

    private void ResetOverlayVisualState()
    {
        if (_overlayHost is null)
        {
            return;
        }

        var visual = ElementCompositionPreview.GetElementVisual(_overlayHost);
        visual.StopAnimation(nameof(visual.Scale));
        visual.StopAnimation(nameof(visual.Opacity));
        visual.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
        visual.Opacity = 1f;
    }

    private void AttachHost(FrameworkElement hostElement)
    {
        DetachHost();

        _activeHost = hostElement;
        _activeHost.SizeChanged += ActiveHost_SizeChanged;
        _activeHost.LayoutUpdated += ActiveHost_LayoutUpdated;
        _activeHost.Unloaded += ActiveHost_Unloaded;

        UpdateOverlayBounds();
    }

    private void DetachHost()
    {
        if (_activeHost == null)
        {
            return;
        }

        _activeHost.SizeChanged -= ActiveHost_SizeChanged;
        _activeHost.LayoutUpdated -= ActiveHost_LayoutUpdated;
        _activeHost.Unloaded -= ActiveHost_Unloaded;
        _activeHost = null;
    }

    private void ActiveHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateOverlayBounds();
    }

    private void ActiveHost_LayoutUpdated(object? sender, object e)
    {
        UpdateOverlayBounds();
    }

    private void ActiveHost_Unloaded(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateOverlayBounds()
    {
        if (_activeHost is null || _overlayHost is null || _coordinateRoot is null)
        {
            return;
        }

        if (_activeHost.ActualWidth <= 0 || _activeHost.ActualHeight <= 0)
        {
            return;
        }

        Rect bounds = _activeHost
            .TransformToVisual(_coordinateRoot)
            .TransformBounds(new Rect(0, 0, _activeHost.ActualWidth, _activeHost.ActualHeight));

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _overlayHost.Width = bounds.Width;
        _overlayHost.Height = bounds.Height;
        Canvas.SetLeft(_overlayHost, bounds.Left);
        Canvas.SetTop(_overlayHost, bounds.Top);
    }

    private void OverlayFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}