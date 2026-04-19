using System.Collections;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace XianYuLauncher.Controls;

public sealed partial class PageHeader : UserControl
{
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
        new PropertyMetadata(15d));

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

    public event TypedEventHandler<BreadcrumbBar, BreadcrumbBarItemClickedEventArgs>? BreadcrumbItemClicked;

    public PageHeader()
    {
        InitializeComponent();
    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        BreadcrumbItemClicked?.Invoke(sender, args);
    }
}