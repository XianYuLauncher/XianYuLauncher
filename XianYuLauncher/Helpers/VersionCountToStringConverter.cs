using Microsoft.UI.Xaml.Data;
using System;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 将版本数量转换为本地化的 "X 个版本" 字符串
/// </summary>
public class VersionCountToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count)
        {
            var suffix = "ModDownloadDetailPage_VersionsCountSuffix".GetLocalized();
            return $"{count}{suffix}";
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
