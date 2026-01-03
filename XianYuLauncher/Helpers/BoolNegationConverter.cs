using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 布尔值取反转换器
    /// </summary>
    public class BoolNegationConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值转换为相反的值
        /// </summary>
        /// <param name="value">要转换的布尔值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="language">语言信息</param>
        /// <returns>转换后的布尔值</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }

        /// <summary>
        /// 将布尔值转换回原始值
        /// </summary>
        /// <param name="value">要转换的布尔值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="language">语言信息</param>
        /// <returns>转换后的布尔值</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }
    }
}