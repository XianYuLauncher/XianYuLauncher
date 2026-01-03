using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace XianYuLauncher.Helpers;

public class ModLoaderToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string modLoader)
        {
            switch (modLoader.ToLower())
            {
                case "forge":
                    return new SolidColorBrush(Colors.Orange);
                case "fabric":
                    return new SolidColorBrush(Colors.Blue);
                case "neoforge":
                    return new SolidColorBrush(Colors.Purple);
                case "quilt":
                    return new SolidColorBrush(Colors.Green);
                default:
                    return new SolidColorBrush(Colors.Gray);
            }
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}