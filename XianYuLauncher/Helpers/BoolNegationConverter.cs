using Microsoft.UI.Xaml.Data;

namespace XMCL2025.Helpers;

/// <summary>
/// 将布尔值取反的转换器
/// </summary>
public class BoolNegationConverter : IValueConverter
{
    /// <summary>
    /// 转换方法
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">参数</param>
    /// <param name="language">语言</param>
    /// <returns>取反后的布尔值</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        
        return false;
    }
    
    /// <summary>
    /// 反向转换方法
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        
        return false;
    }
}