using Microsoft.UI.Xaml.Data;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 字符串转布尔值转换器，用于比较字符串值与参数是否相等
/// </summary>
public class StringToBooleanConverter : IValueConverter
{
    public StringToBooleanConverter()
    {
    }

    /// <summary>
    /// 将字符串值转换为布尔值
    /// </summary>
    /// <param name="value">要转换的字符串值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">比较的字符串参数</param>
    /// <param name="language">语言</param>
    /// <returns>如果值与参数相等返回true，否则返回false</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is string paramString && value is string stringValue)
        {
            return string.Equals(stringValue, paramString, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// 将布尔值转换回字符串
    /// </summary>
    /// <param name="value">要转换的布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">比较的字符串参数</param>
    /// <param name="language">语言</param>
    /// <returns>如果布尔值为true返回参数字符串，否则返回null</returns>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (parameter is string paramString && value is bool boolValue && boolValue)
        {
            return paramString;
        }

        return null;
    }
}
