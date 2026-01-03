using Microsoft.UI.Xaml.Data;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 将布尔值转换为对应的透明度值
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    /// <summary>
    /// 转换方法
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">参数，格式为"true值,false值"，例如"1.0,0.5"</param>
    /// <param name="language">语言</param>
    /// <returns>对应的透明度值</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            // 默认值：true为1.0，false为0.5
            double trueOpacity = 1.0;
            double falseOpacity = 0.5;

            // 如果提供了参数，解析参数获取自定义值
            if (parameter is string paramString)
            {
                string[] parts = paramString.Split(',');
                if (parts.Length == 2)
                {
                    if (double.TryParse(parts[0], out double trueVal))
                    {
                        trueOpacity = trueVal;
                    }
                    if (double.TryParse(parts[1], out double falseVal))
                    {
                        falseOpacity = falseVal;
                    }
                }
            }

            // 返回对应的透明度值
            return boolValue ? trueOpacity : falseOpacity;
        }
        
        // 默认返回不透明
        return 1.0;
    }
    
    /// <summary>
    /// 反向转换方法（未使用）
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}