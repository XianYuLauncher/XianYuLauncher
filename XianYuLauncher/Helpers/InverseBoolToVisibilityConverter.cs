using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;
using System;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 反转布尔值到可见性转换器
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值转换为相反的可见性
        /// </summary>
        /// <param name="value">布尔值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数</param>
        /// <param name="language">语言</param>
        /// <returns>可见性</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool boolValue = value is bool && (bool)value;
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// 将可见性转换为布尔值（未实现）
        /// </summary>
        /// <param name="value">可见性</param>
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
