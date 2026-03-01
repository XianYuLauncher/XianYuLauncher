using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace XianYuLauncher.Models;

public partial class UpdatableResourceItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private ImageSource? _iconSource;

    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string NewVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// e.g. "Mod", "Shader", "ResourcePack"
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    public bool HasIconSource => IconSource != null;
    
    /// <summary>
    /// Default icon to show if Logo/IconSource is not available
    /// </summary>
    public string FallbackIconGlyph { get; set; } = "\uE718";

    // Used by backend to store the original dependency instance or mod version wrapper
    public object? OriginalResource { get; set; }

    partial void OnIconSourceChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(HasIconSource));
    }
}