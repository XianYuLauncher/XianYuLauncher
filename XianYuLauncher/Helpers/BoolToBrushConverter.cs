using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 布尔值到画刷转换器
    /// </summary>
    public class BoolToBrushConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值转换为画刷
        /// </summary>
        /// <param name="value">布尔值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数，格式为"TrueBrushName,FalseBrushName"</param>
        /// <param name="language">语言</param>
        /// <returns>画刷</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool boolValue = value is bool && (bool)value;
            
            // 解析参数，获取true和false对应的画刷名称
            string trueBrushName = "CardBackgroundFillColorSelectedBrush";
            string falseBrushName = "CardBackgroundFillColorDefaultBrush";
            
            if (parameter != null)
            {
                string paramStr = parameter.ToString();
                if (!string.IsNullOrEmpty(paramStr))
                {
                    // 检查是否包含两个参数
                    if (paramStr.Contains(","))
                    {
                        string[] parts = paramStr.Split(',');
                        if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                        {
                            trueBrushName = parts[0].Trim();
                        }
                        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                        {
                            falseBrushName = parts[1].Trim();
                        }
                    }
                    else
                    {
                        // 只有一个参数时，同时用作true和false的画刷
                        trueBrushName = paramStr.Trim();
                        falseBrushName = paramStr.Trim();
                    }
                }
            }
            
            // 根据布尔值选择画刷名称
            string brushName = boolValue ? trueBrushName : falseBrushName;
            
            try
            {
                // 使用更安全的方式访问资源
                if (App.Current.Resources.TryGetValue(brushName, out object resourceValue) && resourceValue is SolidColorBrush brush)
                {
                    return brush;
                }
                
                // 如果资源不存在，返回默认的白色画刷
                return new SolidColorBrush(Microsoft.UI.Colors.White);
            }
            catch
            {
                // 发生异常时，返回默认的白色画刷
                return new SolidColorBrush(Microsoft.UI.Colors.White);
            }
        }

        /// <summary>
        /// 将画刷转换为布尔值（未实现）
        /// </summary>
        /// <param name="value">画刷</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数</param>
        /// <param name="language">语言</param>
        /// <returns>布尔值</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}