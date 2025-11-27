using Microsoft.UI.Xaml.Controls;
using XMCL2025.ViewModels;

namespace XMCL2025.Views;

public sealed partial class 版本管理Page : Page
{
    public 版本管理ViewModel ViewModel { get; }

    public 版本管理Page()
    {
        ViewModel = App.GetService<版本管理ViewModel>();
        this.DataContext = ViewModel;
        InitializeComponent();
        
        // 监听SelectedVersion变化，更新页面标题
        ViewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.SelectedVersion))
            {
                UpdatePageTitle();
            }
        };
        
        // 初始更新标题
        UpdatePageTitle();
    }

    /// <summary>
    /// 更新页面标题
    /// </summary>
    private void UpdatePageTitle()
    {
        if (ViewModel.SelectedVersion != null)
        {
            PageTitle.Text = $"版本管理 - {ViewModel.SelectedVersion.Name}";
            PageSubtitle.Text = $"管理版本 {ViewModel.SelectedVersion.Name} 的组件和资源";
        }
        else
        {
            PageTitle.Text = "版本管理";
            PageSubtitle.Text = "请选择一个版本进行管理";
        }
    }
    

}