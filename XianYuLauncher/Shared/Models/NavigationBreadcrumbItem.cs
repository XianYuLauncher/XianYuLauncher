using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using XianYuLauncher.Core.Contracts.Navigation;
using XianYuLauncher.Models;

namespace XianYuLauncher.Shared.Models;

public partial class NavigationBreadcrumbItem : ObservableObject, ILocalBreadcrumbNavigationItem
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
    private LocalNavigationTarget? _localNavigationTarget;

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private bool _isInteractiveCurrent;

    public bool HasGlobalNavigationTarget => !string.IsNullOrWhiteSpace(PageKey);

    public bool HasLocalNavigationTarget => LocalNavigationTarget?.HasTarget == true;

    public bool CanNavigate => (HasGlobalNavigationTarget || HasLocalNavigationTarget) && !IsCurrent;

    public bool CanShowInlineAction => IsCurrent && IsInteractiveCurrent;

    public override string ToString()
    {
        return DisplayText;
    }

    partial void OnPageKeyChanged(string? value)
    {
        OnPropertyChanged(nameof(HasGlobalNavigationTarget));
        OnPropertyChanged(nameof(CanNavigate));
    }

    partial void OnLocalNavigationTargetChanged(LocalNavigationTarget? value)
    {
        OnPropertyChanged(nameof(HasLocalNavigationTarget));
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