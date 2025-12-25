using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Core;
using XMCL2025.ViewModels;

namespace XMCL2025.Views;

public sealed partial class 联机大厅Page : Page
{
    public 联机大厅ViewModel ViewModel { get; }

    public 联机大厅Page()
    {
        ViewModel = App.GetService<联机大厅ViewModel>();
        this.InitializeComponent();
    }

    /// <summary>
    /// 鼠标悬停时的处理
    /// </summary>
    private void RoomIdBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = (SolidColorBrush)Application.Current.Resources["SystemControlHighlightListLowBrush"];
            // 设置手型光标
            if (Window.Current != null)
            {
                Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Hand, 0);
            }
        }
    }

    /// <summary>
    /// 鼠标离开时的处理
    /// </summary>
    private void RoomIdBorder_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new SolidColorBrush(Colors.Transparent);
            // 恢复默认光标
            if (Window.Current != null)
            {
                Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            }
        }
    }

    /// <summary>
    /// 点击时复制房间号
    /// </summary>
    private void RoomIdBorder_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(RoomIdTextBlock.Text))
        {
            // 创建数据传输对象
            var dataPackage = new DataPackage();
            dataPackage.SetText(RoomIdTextBlock.Text);
            
            // 复制到剪贴板
            Clipboard.SetContent(dataPackage);
            
            // 可以添加一个提示，告诉用户已复制成功
            // 例如显示一个短暂的消息
        }
    }
}