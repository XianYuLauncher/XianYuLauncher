using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 将Uri转换为Visibility的转换器
    /// </summary>
    public class UriToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // 如果Uri不为空，则显示；否则隐藏
            bool isVisible = value is Uri uri && uri != null;
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}