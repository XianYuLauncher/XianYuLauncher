using Microsoft.UI.Xaml.Data;
using System;

namespace XianYuLauncher.Helpers;

/// <summary>
/// 布尔值到不透明度转换器
/// 支持参数格式: "trueOpacity,falseOpacity" (例如 "1.0,0.5")
/// 默认: true -> 1.0, false -> 0.5
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        double trueOpacity = 1.0;
        double falseOpacity = 0.5;
        
        // 解析参数
        if (parameter is string paramStr && !string.IsNullOrEmpty(paramStr))
        {
            var parts = paramStr.Split(',');
            if (parts.Length >= 2)
            {
                if (double.TryParse(parts[0], out double parsedTrue))
                {
                    trueOpacity = parsedTrue;
                }
                if (double.TryParse(parts[1], out double parsedFalse))
                {
                    falseOpacity = parsedFalse;
                }
            }
        }
        
        if (value is bool boolValue)
        {
            return boolValue ? trueOpacity : falseOpacity;
        }
        return falseOpacity;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is double doubleValue)
        {
            return doubleValue > 0.5;
        }
        return false;
    }
}
