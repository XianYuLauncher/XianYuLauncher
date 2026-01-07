using Microsoft.UI.Xaml.Data;
using System;
using Windows.ApplicationModel.Resources;

namespace XianYuLauncher.Helpers;

/// <summary>
/// DateTime 到友好字符串转换器
/// </summary>
public class DateTimeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime dateTime)
        {
            // 使用系统当前区域设置格式化日期
            return dateTime.ToLocalTime().ToString("d"); // 短日期格式，根据系统区域设置自动调整
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
