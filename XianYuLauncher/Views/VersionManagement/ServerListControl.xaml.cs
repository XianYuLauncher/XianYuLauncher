using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Features.VersionManagement.ViewModels;

namespace XianYuLauncher.Views.VersionManagement;

public sealed partial class ServerListControl : UserControl
{
    // 提供强类型的 ViewModel 访问，方便 x:Bind
    public ServersViewModel ViewModel => (ServersViewModel)DataContext;

    public ServerListControl()
    {
        this.InitializeComponent();
    }
}
