using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace XianYuLauncher.Helpers;

public class PathToImageSourceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
            {
                return new BitmapImage(absoluteUri);
            }

            if (System.IO.Path.IsPathRooted(path))
            {
                var fileUri = new Uri($"file:///{path.Replace('\\', '/')}?t={DateTime.Now.Ticks}");
                return new BitmapImage(fileUri);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}