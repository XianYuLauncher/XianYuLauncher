using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace XianYuLauncher.Views;

public sealed partial class WorldOverviewPage : Page
{
    public WorldManagementViewModel? ViewModel { get; private set; }

    public WorldOverviewPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        // 从导航参数获取 ViewModel
        if (e.Parameter is WorldManagementViewModel viewModel)
        {
            ViewModel = viewModel;
        }
    }

    private void CopySeedButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel?.Seed != null)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(ViewModel.Seed);
            Clipboard.SetContent(dataPackage);
            
            // 可选：显示一个提示
            var button = sender as Button;
            if (button != null)
            {
                var originalToolTip = ToolTipService.GetToolTip(button);
                ToolTipService.SetToolTip(button, "已复制！");
                
                // 2秒后恢复原始提示
                var timer = new System.Threading.Timer(_ =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ToolTipService.SetToolTip(button, originalToolTip);
                    });
                }, null, 2000, System.Threading.Timeout.Infinite);
            }
        }
    }
}
