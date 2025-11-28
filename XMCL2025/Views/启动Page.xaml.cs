using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

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

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        // 根据彩蛋标志控制控件可见性
        if (App.ShowEasterEgg)
        {
            StatusTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            MicrosoftAuthTestButton.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
    }
}
