using Microsoft.UI.Xaml.Data;
using System.IO;

namespace XMCL2025.Helpers;

public class PathToFileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string path)
        {
            return Path.GetFileName(path);
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}