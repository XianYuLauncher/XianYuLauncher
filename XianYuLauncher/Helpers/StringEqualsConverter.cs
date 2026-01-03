using System;
using Microsoft.UI.Xaml.Data;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 字符串相等比较转换器
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        /// <summary>
        /// 将字符串转换为可见性
        /// </summary>
        /// <param name="value">要转换的字符串</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">比较参数</param>
        /// <param name="language">语言信息</param>
        /// <returns>如果字符串相等则返回Visible，否则返回Collapsed</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string strValue && parameter is string strParameter)
            {
                return strValue.Equals(strParameter, StringComparison.OrdinalIgnoreCase) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            }
            return Microsoft.UI.Xaml.Visibility.Collapsed;
        }

        /// <summary>
        /// 将可见性转换回字符串（未实现）
        /// </summary>
        /// <param name="value">要转换的可见性</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">比较参数</param>
        /// <param name="language">语言信息</param>
        /// <returns>未实现</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}