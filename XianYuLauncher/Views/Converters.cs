using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace XianYuLauncher.Views
{
    // 布尔值取反转换器
    public class BoolNegationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }
    }

    // 布尔值到多种类型转换器（支持图标、颜色、透明度等）
    public class BoolToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isTrue = false;
            if (value is bool boolValue)
            {
                isTrue = boolValue;
            }

            string[] values = parameter?.ToString().Split(',') ?? new string[] { "", "" };
            if (values.Length < 2)
            {
                values = new string[] { "", "" };
            }

            string result = isTrue ? values[0] : values[1];

            // 根据目标类型返回不同的值
            if (targetType == typeof(double))
            {
                if (double.TryParse(result, out double doubleValue))
                {
                    return doubleValue;
                }
            }
            else if (targetType == typeof(Color))
            {
                if (TryParseHexColor(result, out Color colorValue))
                {
                    return colorValue;
                }
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        // 自定义十六进制颜色解析方法
        private bool TryParseHexColor(string hexString, out Color color)
        {
            color = Color.FromArgb(255, 0, 0, 0);
            
            try
            {
                // 移除#符号
                hexString = hexString.TrimStart('#');
                
                if (hexString.Length == 6)
                {
                    // RGB格式
                    byte r = byte.Parse(hexString.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    byte g = byte.Parse(hexString.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    byte b = byte.Parse(hexString.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    color = Color.FromArgb(255, r, g, b);
                    return true;
                }
                else if (hexString.Length == 8)
                {
                    // ARGB格式
                    byte a = byte.Parse(hexString.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    byte r = byte.Parse(hexString.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    byte g = byte.Parse(hexString.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    byte b = byte.Parse(hexString.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                    color = Color.FromArgb(a, r, g, b);
                    return true;
                }
            }
            catch { }
            
            return false;
        }
    }

    // 首字母大写转换器
    public class FirstLetterToUpperConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string str && !string.IsNullOrEmpty(str))
            {
                return str.Substring(0, 1).ToUpper() + str.Substring(1).ToLower();
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}