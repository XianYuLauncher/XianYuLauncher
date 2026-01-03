using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 将Uri转换为ImageSource的转换器
    /// </summary>
    public class UriToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Uri uri && uri != null)
            {
                try
                {
                    return new BitmapImage(uri);
                }
                catch (Exception)
                {
                    // 如果Uri无效，返回null
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}