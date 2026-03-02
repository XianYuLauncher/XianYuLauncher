using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Models;

namespace XianYuLauncher.Controls;

public sealed partial class VersionIconPickerFlyoutContent : UserControl
{
    public static readonly DependencyProperty AvailableIconsProperty = DependencyProperty.Register(
        nameof(AvailableIcons),
        typeof(ObservableCollection<VersionIconOption>),
        typeof(VersionIconPickerFlyoutContent),
        new PropertyMetadata(null));

    public ObservableCollection<VersionIconOption>? AvailableIcons
    {
        get => (ObservableCollection<VersionIconOption>?)GetValue(AvailableIconsProperty);
        set => SetValue(AvailableIconsProperty, value);
    }

    public event EventHandler<VersionIconSelectedEventArgs>? BuiltInIconSelected;
    public event EventHandler? CustomIconRequested;

    public VersionIconPickerFlyoutContent()
    {
        InitializeComponent();
    }

    private void BuiltInIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is VersionIconOption option)
        {
            BuiltInIconSelected?.Invoke(this, new VersionIconSelectedEventArgs(option));
        }
    }

    private void CustomIconButton_Click(object sender, RoutedEventArgs e)
    {
        CustomIconRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class VersionIconSelectedEventArgs : EventArgs
{
    public VersionIconOption IconOption { get; }

    public VersionIconSelectedEventArgs(VersionIconOption iconOption)
    {
        IconOption = iconOption;
    }
}