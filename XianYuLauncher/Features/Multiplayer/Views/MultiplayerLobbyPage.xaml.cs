using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Features.Multiplayer.ViewModels;

namespace XianYuLauncher.Features.Multiplayer.Views;

public sealed partial class MultiplayerLobbyPage : Page, IHostedLocalPage
{
    private EventHandler? _closeRequested;

    public MultiplayerLobbyViewModel ViewModel { get; }

    public IPageHeaderAware HeaderSource => ViewModel;

    public event EventHandler? CloseRequested
    {
        add => _closeRequested += value;
        remove => _closeRequested -= value;
    }

    public MultiplayerLobbyPage()
    {
        ViewModel = App.GetService<MultiplayerLobbyViewModel>();
        DataContext = ViewModel;
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo(e.Parameter);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.OnNavigatedFrom();
    }

    public void ResetEmbeddedVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
    }

    private void ExitRoomButton_Click(object sender, RoutedEventArgs e)
    {
        _closeRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 鼠标悬停时的处理
    /// </summary>
    private void RoomIdBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            // 使用主题资源，适配深色模式
            border.Background = (SolidColorBrush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
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