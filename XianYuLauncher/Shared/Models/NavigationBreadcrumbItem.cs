using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using XianYuLauncher.Models;

namespace XianYuLauncher.Shared.Models;

public partial class NavigationBreadcrumbItem : ObservableObject
{
    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _iconPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<VersionIconOption>? _availableIcons;

    [ObservableProperty]
    private string? _pageKey;

    [ObservableProperty]
    private object? _navigationParameter;

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private bool _isInteractiveCurrent;

    public bool CanNavigate => !string.IsNullOrWhiteSpace(PageKey) && !IsCurrent;

    public bool CanShowInlineAction => IsCurrent && IsInteractiveCurrent;

    public override string ToString()
    {
        return DisplayText;
    }

    partial void OnPageKeyChanged(string? value)
    {
        OnPropertyChanged(nameof(CanNavigate));
    }

    partial void OnIsCurrentChanged(bool value)
    {
        OnPropertyChanged(nameof(CanNavigate));
        OnPropertyChanged(nameof(CanShowInlineAction));
    }

    partial void OnIsInteractiveCurrentChanged(bool value)
    {
        OnPropertyChanged(nameof(CanShowInlineAction));
    }
}