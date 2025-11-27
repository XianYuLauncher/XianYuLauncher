using Microsoft.UI.Xaml.Controls;
using XMCL2025.ViewModels;

namespace XMCL2025.Views;

public sealed partial class ModLoader选择Page : Page
{
    public ModLoader选择ViewModel ViewModel { get; }

    public ModLoader选择Page()
    {
        ViewModel = App.GetService<ModLoader选择ViewModel>();
        InitializeComponent();
    }
}