using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

/// <summary>
/// XianYu Fixer 独立聊天页面 — 复用 ErrorAnalysisViewModel（Singleton）
/// </summary>
public sealed partial class FixerChatPage : Page
{
    public ErrorAnalysisViewModel ViewModel { get; }

    public FixerChatPage()
    {
        ViewModel = App.GetService<ErrorAnalysisViewModel>();
        this.InitializeComponent();
        Unloaded += FixerChatPage_Unloaded;
    }

    private void FixerChatPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Unloaded -= FixerChatPage_Unloaded;
        ViewModel.Dispose();
    }
}
