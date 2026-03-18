using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace XianYuLauncher.Helpers;

/// <summary>
/// IfPresent：value 非 null → Visible；IfAbsent：value 为 null → Visible（用于占位与内容互斥）。
/// </summary>
public sealed class ObjectPresenceToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        var present = value is not null;
        if (string.Equals(parameter as string, "IfAbsent", StringComparison.OrdinalIgnoreCase))
        {
            return present ? Visibility.Collapsed : Visibility.Visible;
        }

        return present ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException();
}
