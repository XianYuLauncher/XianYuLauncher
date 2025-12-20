using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using XMCL2025.ViewModels;

namespace XMCL2025.Views
{
    /// <summary>
    /// 错误分析系统页面
    /// </summary>
    public sealed partial class 错误分析系统Page : Page
    {
        public 错误分析系统ViewModel ViewModel { get; }

        public 错误分析系统Page()
        {
            ViewModel = App.GetService<错误分析系统ViewModel>();
            this.InitializeComponent();
        }

        /// <summary>
        /// 处理导航到页面事件
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 接收导航参数（如果有）
            if (e.Parameter is Tuple<string, List<string>, List<string>> logData)
            {
                ViewModel.SetLogData(logData.Item1, logData.Item2, logData.Item3);
            }
        }

        /// <summary>
        /// 返回按钮点击事件
        /// </summary>
        private void BackButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}