using Microsoft.UI.Xaml.Data;
using System;

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
            var now = DateTime.Now;
            var diff = now - dateTime;

            if (diff.TotalMinutes < 1)
                return "刚刚";
            if (diff.TotalHours < 1)
                return $"{(int)diff.TotalMinutes}分钟前";
            if (diff.TotalDays < 1)
                return $"{(int)diff.TotalHours}小时前";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays}天前";
            if (dateTime.Year == now.Year)
                return dateTime.ToString("MM月dd日");
            
            return dateTime.ToString("yyyy年MM月dd日");
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
