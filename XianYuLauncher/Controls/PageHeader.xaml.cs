using System.Collections;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace XianYuLauncher.Controls;

public sealed partial class PageHeader : UserControl
{
    private CancellationTokenSource? _deferredRevealCancellationTokenSource;

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(PageHeader),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle),
        typeof(string),
        typeof(PageHeader),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MaxContentWidthProperty = DependencyProperty.Register(
        nameof(MaxContentWidth),
        typeof(double),
        typeof(PageHeader),
        new PropertyMetadata(1064d));

    public static readonly DependencyProperty ShowBreadcrumbProperty = DependencyProperty.Register(
        nameof(ShowBreadcrumb),
        typeof(bool),
        typeof(PageHeader),
        new PropertyMetadata(false));

    public static readonly DependencyProperty ShowPrimaryHeadingProperty = DependencyProperty.Register(
        nameof(ShowPrimaryHeading),
        typeof(bool),
        typeof(PageHeader),
        new PropertyMetadata(true));

    public static readonly DependencyProperty BreadcrumbFontSizeProperty = DependencyProperty.Register(
        nameof(BreadcrumbFontSize),
        typeof(double),
        typeof(PageHeader),
        new PropertyMetadata(14d));

    public static readonly DependencyProperty BreadcrumbMarginProperty = DependencyProperty.Register(
        nameof(BreadcrumbMargin),
        typeof(Thickness),
        typeof(PageHeader),
        new PropertyMetadata(new Thickness(0, 0, 0, 12)));

    public static readonly DependencyProperty BreadcrumbItemsProperty = DependencyProperty.Register(
        nameof(BreadcrumbItems),
        typeof(IEnumerable),
        typeof(PageHeader),
        new PropertyMetadata(null));

    public static readonly DependencyProperty BreadcrumbItemTemplateProperty = DependencyProperty.Register(
        nameof(BreadcrumbItemTemplate),
        typeof(DataTemplate),
        typeof(PageHeader),
        new PropertyMetadata(null));

    public static readonly DependencyProperty TrailingActionsProperty = DependencyProperty.Register(
        nameof(TrailingActions),
        typeof(object),
        typeof(PageHeader),
        new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderSupplementalContentProperty = DependencyProperty.Register(
        nameof(HeaderSupplementalContent),
        typeof(object),
        typeof(PageHeader),
        new PropertyMetadata(null));

    public static readonly DependencyProperty EnableDeferredRevealProperty = DependencyProperty.Register(
        nameof(EnableDeferredReveal),
        typeof(bool),
        typeof(PageHeader),
        new PropertyMetadata(false, OnDeferredRevealPropertyChanged));

    public static readonly DependencyProperty DeferredRevealMillisecondsProperty = DependencyProperty.Register(
        nameof(DeferredRevealMilliseconds),
        typeof(int),
        typeof(PageHeader),
        new PropertyMetadata(180, OnDeferredRevealPropertyChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public double MaxContentWidth
    {
        get => (double)GetValue(MaxContentWidthProperty);
        set => SetValue(MaxContentWidthProperty, value);
    }

    public bool ShowBreadcrumb
    {
        get => (bool)GetValue(ShowBreadcrumbProperty);
        set => SetValue(ShowBreadcrumbProperty, value);
    }

    public bool ShowPrimaryHeading
    {
        get => (bool)GetValue(ShowPrimaryHeadingProperty);
        set => SetValue(ShowPrimaryHeadingProperty, value);
    }

    public double BreadcrumbFontSize
    {
        get => (double)GetValue(BreadcrumbFontSizeProperty);
        set => SetValue(BreadcrumbFontSizeProperty, value);
    }

    public Thickness BreadcrumbMargin
    {
        get => (Thickness)GetValue(BreadcrumbMarginProperty);
        set => SetValue(BreadcrumbMarginProperty, value);
    }

    public IEnumerable? BreadcrumbItems
    {
        get => (IEnumerable?)GetValue(BreadcrumbItemsProperty);
        set => SetValue(BreadcrumbItemsProperty, value);
    }

    public DataTemplate? BreadcrumbItemTemplate
    {
        get => (DataTemplate?)GetValue(BreadcrumbItemTemplateProperty);
        set => SetValue(BreadcrumbItemTemplateProperty, value);
    }

    public object? TrailingActions
    {
        get => GetValue(TrailingActionsProperty);
        set => SetValue(TrailingActionsProperty, value);
    }

    public object? HeaderSupplementalContent
    {
        get => GetValue(HeaderSupplementalContentProperty);
        set => SetValue(HeaderSupplementalContentProperty, value);
    }

    public bool EnableDeferredReveal
    {
        get => (bool)GetValue(EnableDeferredRevealProperty);
        set => SetValue(EnableDeferredRevealProperty, value);
    }

    public int DeferredRevealMilliseconds
    {
        get => (int)GetValue(DeferredRevealMillisecondsProperty);
        set => SetValue(DeferredRevealMillisecondsProperty, value);
    }

    public event TypedEventHandler<BreadcrumbBar, BreadcrumbBarItemClickedEventArgs>? BreadcrumbItemClicked;

    public PageHeader()
    {
        InitializeComponent();
        Loaded += PageHeader_Loaded;
        Unloaded += PageHeader_Unloaded;
    }

    public void ShowImmediately()
    {
        CancelDeferredReveal();
        ApplyVisibilityState(true);
    }

    public void HideImmediately()
    {
        CancelDeferredReveal();
        ApplyVisibilityState(false);
    }

    private static void OnDeferredRevealPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PageHeader pageHeader || !pageHeader.IsLoaded)
        {
            return;
        }

        pageHeader.StartDeferredRevealIfNeeded();
    }

    private void PageHeader_Loaded(object sender, RoutedEventArgs e)
    {
        StartDeferredRevealIfNeeded();
    }

    private void PageHeader_Unloaded(object sender, RoutedEventArgs e)
    {
        CancelDeferredReveal();
        ApplyVisibilityState(true);
    }

    private void StartDeferredRevealIfNeeded()
    {
        CancelDeferredReveal();

        if (!EnableDeferredReveal)
        {
            ApplyVisibilityState(true);
            return;
        }

        ApplyVisibilityState(false);

        if (DeferredRevealMilliseconds <= 0)
        {
            ApplyVisibilityState(true);
            return;
        }

        _deferredRevealCancellationTokenSource = new CancellationTokenSource();
        _ = RevealAsync(_deferredRevealCancellationTokenSource.Token);
    }

    private void CancelDeferredReveal()
    {
        _deferredRevealCancellationTokenSource?.Cancel();
        _deferredRevealCancellationTokenSource?.Dispose();
        _deferredRevealCancellationTokenSource = null;
    }

    private async Task RevealAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DeferredRevealMilliseconds, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        ApplyVisibilityState(true);
    }

    private void ApplyVisibilityState(bool isVisible)
    {
        RootGrid.Opacity = isVisible ? 1 : 0;
        RootGrid.IsHitTestVisible = isVisible;
    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        BreadcrumbItemClicked?.Invoke(sender, args);
    }
}