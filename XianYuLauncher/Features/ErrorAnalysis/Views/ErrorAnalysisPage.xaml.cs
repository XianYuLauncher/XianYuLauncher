using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.Features.ErrorAnalysis.ViewModels;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Views
{
    /// <summary>
    /// 错误分析系统页面
    /// </summary>
    public sealed partial class ErrorAnalysisPage : Page
    {
        private const string HostedDetailReadOnlyBreadcrumbItemTemplateKey = "HostedDetailReadOnlyBreadcrumbItemTemplate";

        private readonly INavigationService _navigationService;
        public ErrorAnalysisViewModel ViewModel { get; }
        public LauncherAIViewModel LauncherAIWorkspaceViewModel { get; }
        private readonly IUiDispatcher _uiDispatcher;
        private bool _isScrollPending = false;

        public ErrorAnalysisPage()
        {
            _navigationService = App.GetService<INavigationService>();
            ViewModel = App.GetService<ErrorAnalysisViewModel>();
            LauncherAIWorkspaceViewModel = App.GetService<LauncherAIViewModel>();
            _uiDispatcher = App.GetService<IUiDispatcher>();
            this.InitializeComponent();
            Unloaded += ErrorAnalysisPage_Unloaded;
            
            // 订阅LogLines集合变化事件，实现自动滚动到底部
            ViewModel.LogLines.CollectionChanged += LogLines_CollectionChanged;

            // 添加键盘快捷键支持
            LogListView.KeyDown += LogListView_KeyDown;
        }

        private void ErrorAnalysisPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.LogLines.CollectionChanged -= LogLines_CollectionChanged;
            LogListView.KeyDown -= LogListView_KeyDown;
            Unloaded -= ErrorAnalysisPage_Unloaded;
            LauncherAIWorkspaceViewModel.SetErrorAnalysisPageOpen(false);
            ViewModel.Dispose();
        }
        
        /// <summary>
        /// 处理键盘快捷键
        /// </summary>
        private void LogListView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Ctrl+C 复制选中的日志行
            if (e.Key == VirtualKey.C && 
                (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)))
            {
                CopySelectedLogLines();
                e.Handled = true;
            }
            // Ctrl+A 全选
            else if (e.Key == VirtualKey.A && 
                     (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)))
            {
                LogListView.SelectAll();
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// 复制选中的日志行到剪贴板
        /// </summary>
        private void CopySelectedLogLines()
        {
            try
            {
                if (LogListView.SelectedItems.Count > 0)
                {
                    var sb = new StringBuilder();
                    foreach (var item in LogListView.SelectedItems)
                    {
                        if (item is string line)
                        {
                            sb.AppendLine(line);
                        }
                    }
                    
                    var dataPackage = new DataPackage();
                    dataPackage.SetText(sb.ToString());
                    Clipboard.SetContent(dataPackage);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"复制日志失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 日志集合变化时自动滚动到底部
        /// </summary>
        private void LogLines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 只在添加新项时滚动
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                // 使用防抖机制减少频繁滚动导致的UI卡顿
                if (!_isScrollPending)
                {
                    _isScrollPending = true;

                    // 使用延迟执行滚动操作，避免频繁滚动导致UI卡顿
                    _ = ScrollLogToBottomAsync();
                }
            }
        }
        
        private async System.Threading.Tasks.Task ScrollLogToBottomAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(100);
                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    if (LogListView.Items.Count > 0)
                    {
                        LogListView.ScrollIntoView(LogListView.Items[LogListView.Items.Count - 1]);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"滚动到底部失败: {ex.Message}");
            }
            finally
            {
                _isScrollPending = false;
            }
        }

        /// <summary>
        /// 处理导航到页面事件
        /// </summary>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var navigationParameter = NormalizeNavigationParameter(e.Parameter);
            ViewModel.ApplyNavigationContext(navigationParameter);
            ApplyHeaderPresentationMode(ViewModel.HeaderPresentationMode);

            LauncherAIWorkspaceViewModel.SetErrorAnalysisPageOpen(true);
            await LauncherAIWorkspaceViewModel.InitializeAsync(ensureDefaultConversation: false);
            LauncherAIWorkspaceViewModel.ActivateConversationForEmbeddedSurface();

            // 每次进入页面先清理修复按钮状态，避免残留
            ViewModel.ResetFixActionState();

            if (navigationParameter?.HasLogPayload == true)
            {
                ViewModel.SetLogData(
                    navigationParameter.LaunchCommand,
                    navigationParameter.GameOutput.ToList(),
                    navigationParameter.GameError.ToList());
                ViewModel.SetGameCrashStatus(true);
            }
        }

        private static ErrorAnalysisNavigationParameter? NormalizeNavigationParameter(object? parameter)
        {
            return parameter switch
            {
                ErrorAnalysisNavigationParameter typedNavigationParameter => typedNavigationParameter,
                Tuple<string, List<string>, List<string>> legacyLogData => ErrorAnalysisNavigationParameter.CreateCrashPayload(
                    legacyLogData.Item1,
                    legacyLogData.Item2,
                    legacyLogData.Item3),
                _ => null,
            };
        }

        private void ApplyHeaderPresentationMode(PageHeaderPresentationMode headerPresentationMode)
        {
            switch (headerPresentationMode)
            {
                case PageHeaderPresentationMode.ProminentBreadcrumb:
                    ErrorAnalysisPageHeader.ShowPrimaryHeading = false;
                    ErrorAnalysisPageHeader.BreadcrumbFontSize = 28;
                    ErrorAnalysisPageHeader.BreadcrumbMargin = new Thickness(-2, -11, 0, 12);
                    ErrorAnalysisPageHeader.BreadcrumbItemTemplate = Resources[HostedDetailReadOnlyBreadcrumbItemTemplateKey] as DataTemplate;
                    return;
            }

            ErrorAnalysisPageHeader.ShowPrimaryHeading = true;
            ErrorAnalysisPageHeader.BreadcrumbFontSize = 15;
            ErrorAnalysisPageHeader.BreadcrumbMargin = new Thickness(0, 0, 0, 12);
            ErrorAnalysisPageHeader.BreadcrumbItemTemplate = null;
        }

        private void PageHeader_BreadcrumbItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            if (args.Item is not NavigationBreadcrumbItem breadcrumbItem || !breadcrumbItem.CanNavigate)
            {
                return;
            }

            if (ShouldGoBackToGlobalRoot(breadcrumbItem))
            {
                _navigationService.GoBack(new DrillInNavigationTransitionInfo());
                return;
            }

            if (breadcrumbItem.HasGlobalNavigationTarget)
            {
                _navigationService.NavigateTo(breadcrumbItem.PageKey!, breadcrumbItem.NavigationParameter);
            }
        }

        private bool ShouldGoBackToGlobalRoot(NavigationBreadcrumbItem breadcrumbItem)
        {
            return breadcrumbItem.HasGlobalNavigationTarget
                && _navigationService.CanGoBack
                && ViewModel.HasGlobalBreadcrumbRoot
                && string.Equals(breadcrumbItem.PageKey, ViewModel.GlobalBreadcrumbRootPageKey, StringComparison.Ordinal);
        }

        /// <summary>
        /// 弹出独立聊天窗口
        /// </summary>
        private void PopOutChat_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            LauncherAIWindow.ShowOrActivate();
        }
    }
}