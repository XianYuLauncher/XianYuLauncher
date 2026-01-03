using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 将布尔值转换为可见性的转换器
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 转换方法
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">参数</param>
    /// <param name="language">语言</param>
    /// <returns>对应的可见性</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            bool isInverse = parameter?.ToString().Equals("Inverse", StringComparison.OrdinalIgnoreCase) ?? false;
            bool shouldBeVisible = isInverse ? !boolValue : boolValue;
            return shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Visible;
    }
    
    /// <summary>
    /// 反向转换方法
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        
        return false;
    }
}