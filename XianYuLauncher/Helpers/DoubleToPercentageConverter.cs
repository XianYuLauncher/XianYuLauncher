using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 将Double值转换为百分比字符串的转换器
/// </summary>
public class DoubleToPercentageConverter : IValueConverter
{
    /// <summary>
    /// 转换方法
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double doubleValue)
        {
            return $"{doubleValue:F1}%";
        }
        return "0.0%";
    }
    
    /// <summary>
    /// 反向转换方法
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}