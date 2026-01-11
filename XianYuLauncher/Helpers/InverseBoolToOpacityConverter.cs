using Microsoft.UI.Xaml.Data;
using System;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 反转布尔值到不透明度转换器（true -> 0.0, false -> 1.0）
/// </summary>
public class InverseBoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return boolValue ? 0.0 : 1.0;
        }
        return 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is double doubleValue)
        {
            return doubleValue <= 0.5;
        }
        return false;
    }
}
