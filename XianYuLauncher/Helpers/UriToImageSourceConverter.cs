using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 将 Uri 转换为 ImageSource 的转换器
    /// </summary>
    public class UriToImageSourceConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, string language)
        {
            if (value is Uri uri)
            {
                try
                {
                    return new BitmapImage(uri);
                }
                catch (Exception)
                {
                    // 如果 Uri 无效，返回 null
                    return null;
                }
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}