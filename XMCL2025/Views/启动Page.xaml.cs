using Microsoft.UI.Xaml.Controls;

using XMCL2025.ViewModels;

namespace XMCL2025.Views;

public sealed partial class 启动Page : Page
{
    public 启动ViewModel ViewModel
    {
        get;
    }

    public 启动Page()
    {
        ViewModel = App.GetService<启动ViewModel>();
        InitializeComponent();
    }
}
