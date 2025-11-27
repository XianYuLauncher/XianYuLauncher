using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace XMCL2025.Helpers
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
            // 始终返回相同的背景色，不管mod是否启用
            // 只使用第一个画刷名称，忽略第二个
            string[] brushNames = parameter?.ToString().Split(',') ?? new string[] { "CardBackgroundFillColorDefaultBrush" };
            string brushName = brushNames.Length > 0 ? brushNames[0] : "CardBackgroundFillColorDefaultBrush";
            
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