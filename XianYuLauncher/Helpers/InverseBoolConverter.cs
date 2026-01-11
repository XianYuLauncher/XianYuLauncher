using Microsoft.UI.Xaml.Data;
using System;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 反转布尔值转换器（true -> false, false -> true）
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}
