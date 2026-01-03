using Microsoft.UI.Xaml.Data;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 将字符串的首字母转换为大写的转换器
/// </summary>
public class FirstLetterToUpperConverter : IValueConverter
{
    /// <summary>
    /// 转换方法
    /// </summary>
    /// <param name="value">输入的字符串值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">参数</param>
    /// <param name="language">语言参数</param>
    /// <returns>首字母大写后的字符串</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string strValue && !string.IsNullOrEmpty(strValue))
        {
            return char.ToUpper(strValue[0]) + strValue.Substring(1);
        }
        
        return value;
    }
    
    /// <summary>
    /// 反向转换方法（未实现）
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}