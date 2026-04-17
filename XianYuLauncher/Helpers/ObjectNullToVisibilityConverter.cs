using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace XianYuLauncher.Helpers;

public class ObjectNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}