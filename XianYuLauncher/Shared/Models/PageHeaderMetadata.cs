using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

namespace XianYuLauncher.Shared.Models;

public partial class PageHeaderMetadata : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private bool _showBreadcrumb;

    [ObservableProperty]
    private ObservableCollection<NavigationBreadcrumbItem> _breadcrumbItems = new();
}