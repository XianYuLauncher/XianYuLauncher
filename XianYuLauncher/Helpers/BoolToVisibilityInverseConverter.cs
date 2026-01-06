using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 布尔值到可见性反转转换器（true -> Collapsed, false -> Visible）
/// </summary>
public class BoolToVisibilityInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return false;
    }
}
