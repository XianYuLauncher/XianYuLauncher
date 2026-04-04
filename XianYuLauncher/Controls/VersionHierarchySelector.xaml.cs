using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Features.VersionManagement.ViewModels;

namespace XianYuLauncher.Controls;

public sealed partial class VersionHierarchySelector : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(ObservableCollection<ModpackGameVersionViewModel>),
        typeof(VersionHierarchySelector),
        new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedVersionProperty = DependencyProperty.Register(
        nameof(SelectedVersion),
        typeof(ModpackVersionViewModel),
        typeof(VersionHierarchySelector),
        new PropertyMetadata(null, OnSelectedVersionChanged));

    public ObservableCollection<ModpackGameVersionViewModel>? ItemsSource
    {
        get => (ObservableCollection<ModpackGameVersionViewModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ModpackVersionViewModel? SelectedVersion
    {
        get => (ModpackVersionViewModel?)GetValue(SelectedVersionProperty);
        set => SetValue(SelectedVersionProperty, value);
    }

    public event EventHandler<ModpackVersionViewModel>? SelectedVersionChanged;

    public VersionHierarchySelector()
    {
        InitializeComponent();
    }

    private static void OnSelectedVersionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VersionHierarchySelector selector)
        {
            selector.ApplySelectionToItems(e.NewValue as ModpackVersionViewModel);
            if (e.NewValue is ModpackVersionViewModel selected)
            {
                selector.SelectedVersionChanged?.Invoke(selector, selected);
            }
        }
    }

    private void SelectRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radio && radio.Tag is ModpackVersionViewModel versionItem)
        {
            SelectedVersion = versionItem;
        }
    }

    private void ApplySelectionToItems(ModpackVersionViewModel? selected)
    {
        if (ItemsSource == null)
        {
            return;
        }

        foreach (var game in ItemsSource)
        {
            foreach (var loader in game.Loaders)
            {
                foreach (var version in loader.Versions)
                {
                    version.IsSelected = ReferenceEquals(version, selected);
                }
            }
        }
    }
}
