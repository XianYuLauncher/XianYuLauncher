using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Data;
using System;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 布尔值到字体粗细转换器
    /// </summary>
    public class BoolToFontWeightConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值转换为字体粗细
        /// </summary>
        /// <param name="value">布尔值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数</param>
        /// <param name="language">语言</param>
        /// <returns>字体粗细</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool boolValue = value is bool && (bool)value;
            return boolValue ? FontWeights.SemiBold : FontWeights.Normal;
        }

        /// <summary>
        /// 将字体粗细转换为布尔值（未实现）
        /// </summary>
        /// <param name="value">字体粗细</param>
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
