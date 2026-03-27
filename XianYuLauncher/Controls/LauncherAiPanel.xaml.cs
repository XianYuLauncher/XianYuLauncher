using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Controls;

public sealed partial class LauncherAiPanel : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ErrorAnalysisViewModel),
        typeof(LauncherAiPanel),
        new PropertyMetadata(null));

    public static readonly DependencyProperty SectionTitleProperty = DependencyProperty.Register(
        nameof(SectionTitle),
        typeof(string),
        typeof(LauncherAiPanel),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShowEmptyPlaceholderProperty = DependencyProperty.Register(
        nameof(ShowEmptyPlaceholder),
        typeof(bool),
        typeof(LauncherAiPanel),
        new PropertyMetadata(false));

    public static readonly DependencyProperty EmptyPlaceholderTextProperty = DependencyProperty.Register(
        nameof(EmptyPlaceholderText),
        typeof(string),
        typeof(LauncherAiPanel),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessagesMaxHeightProperty = DependencyProperty.Register(
        nameof(MessagesMaxHeight),
        typeof(double),
        typeof(LauncherAiPanel),
        new PropertyMetadata(double.PositiveInfinity));

    public static readonly DependencyProperty PopOutButtonVisibilityProperty = DependencyProperty.Register(
        nameof(PopOutButtonVisibility),
        typeof(Visibility),
        typeof(LauncherAiPanel),
        new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty HeaderVisibilityProperty = DependencyProperty.Register(
        nameof(HeaderVisibility),
        typeof(Visibility),
        typeof(LauncherAiPanel),
        new PropertyMetadata(Visibility.Visible));

    public LauncherAiPanel()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler? PopOutRequested;

    public ErrorAnalysisViewModel? ViewModel
    {
        get => (ErrorAnalysisViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public string SectionTitle
    {
        get => (string)GetValue(SectionTitleProperty);
        set => SetValue(SectionTitleProperty, value);
    }

    public bool ShowEmptyPlaceholder
    {
        get => (bool)GetValue(ShowEmptyPlaceholderProperty);
        set => SetValue(ShowEmptyPlaceholderProperty, value);
    }

    public string EmptyPlaceholderText
    {
        get => (string)GetValue(EmptyPlaceholderTextProperty);
        set => SetValue(EmptyPlaceholderTextProperty, value);
    }

    public double MessagesMaxHeight
    {
        get => (double)GetValue(MessagesMaxHeightProperty);
        set => SetValue(MessagesMaxHeightProperty, value);
    }

    public Visibility PopOutButtonVisibility
    {
        get => (Visibility)GetValue(PopOutButtonVisibilityProperty);
        set => SetValue(PopOutButtonVisibilityProperty, value);
    }

    public Visibility HeaderVisibility
    {
        get => (Visibility)GetValue(HeaderVisibilityProperty);
        set => SetValue(HeaderVisibilityProperty, value);
    }

    private void PopOutButton_Click(object sender, RoutedEventArgs e)
    {
        PopOutRequested?.Invoke(this, e);
    }
}