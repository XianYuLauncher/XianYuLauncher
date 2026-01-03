using Microsoft.UI.Xaml.Data;
using Symbol = Microsoft.UI.Xaml.Controls.Symbol;

namespace XianYuLauncher.Helpers;

public class ModLoaderToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string modLoader)
        {
            switch (modLoader.ToLower())
            {
                case "forge":
                    return Symbol.List;
                case "fabric":
                    return Symbol.Document;
                case "neoforge":
                    return Symbol.Refresh;
                case "quilt":
                    return Symbol.View;
                default:
                    return Symbol.Help;
            }
        }
        return Symbol.Help;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}