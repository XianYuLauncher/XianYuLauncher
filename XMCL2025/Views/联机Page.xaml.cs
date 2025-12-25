using Microsoft.UI.Xaml.Controls;
using XMCL2025.ViewModels;

namespace XMCL2025.Views;

public sealed partial class 联机Page : Page
{
    public 联机ViewModel ViewModel { get; }

    public 联机Page()
    {
        ViewModel = App.GetService<联机ViewModel>();
        this.InitializeComponent();
    }
}