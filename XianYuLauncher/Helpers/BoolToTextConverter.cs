using Microsoft.UI.Xaml.Data;
using System;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 布尔值到文本转换器
    /// </summary>
    public class BoolToTextConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值转换为文本
        /// </summary>
        /// <param name="value">布尔值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数，格式为"TrueText,FalseText"</param>
        /// <param name="language">语言</param>
        /// <returns>文本</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                string[] texts = parameter?.ToString()?.Split(',') ?? new string[] { "True", "False" };
                string trueText = texts.Length > 0 ? texts[0].GetLocalized() : "True";
                string falseText = texts.Length > 1 ? texts[1].GetLocalized() : "False";
                return boolValue ? trueText : falseText;
            }
            return "Unknown";
        }

        /// <summary>
        /// 将文本转换为布尔值（未实现）
        /// </summary>
        /// <param name="value">文本</param>
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