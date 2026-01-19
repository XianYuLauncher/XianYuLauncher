using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.System;
using Microsoft.UI.Xaml.Media;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Helpers;
using XianYuLauncher.Services;
using System.Collections.Specialized;
using System.Text;

namespace XianYuLauncher.ViewModels;

public partial class LaunchViewModel : ObservableRecipient
{
    // Win32 API 用于隐藏控制台窗口
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
    
    private const int SW_HIDE = 0;
    
    // 分辨率设置字段
    private int _windowWidth = 1280;
    private int _windowHeight = 720;
    private async Task ShowJavaNotFoundMessageAsync()
    {
        // 创建并显示消息对话框
        var dialog = new ContentDialog
        {
            Title = "Java运行时环境未找到",
            Content = "未找到适用于当前游戏版本的Java运行时环境，请先安装相应版本的Java。\n\n游戏版本需要Java " + GetRequiredJavaVersionText() + "\n\n在下载完Java后,将Java.exe文件加入到设置-Java设置中!",
            PrimaryButtonText = "下载",
            CloseButtonText = "确定",
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        // 处理下载按钮点击事件
        dialog.PrimaryButtonClick += async (sender, args) =>
        {
            string javaVersion = GetRequiredJavaVersionText();
            string downloadUrl = string.Empty;
            
            // 根据Java版本选择下载链接
            if (javaVersion.Contains("8"))
            {
                downloadUrl = "https://www.java.com/zh-CN/download/";
            }
            else if (javaVersion.Contains("17"))
            {
                downloadUrl = "https://www.oracle.com/cn/java/technologies/downloads/#java17";
            }
            else if (javaVersion.Contains("21"))
            {
                downloadUrl = "https://www.oracle.com/cn/java/technologies/downloads/#java21";
            }
            
            // 启动浏览器打开下载页面
            if (!string.IsNullOrEmpty(downloadUrl))
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(downloadUrl));
            }
        };
        
        await dialog.ShowAsync();
    }
    
    /// <summary>
    /// 将初始堆内存转换为JVM参数格式
    /// </summary>
    /// <param name="memoryGB">内存大小（GB）</param>
    /// <returns>格式化后的初始堆参数，如"-Xms6G"或"-Xms8192M"</returns>
    private string GetInitialHeapParam(double memoryGB)
    {
        if (memoryGB % 1 == 0)
        {
            // 整数GB，直接使用GB单位
            return $"-Xms{(int)memoryGB}G";
        }
        else
        {
            // 小数GB，转换为MB
            int memoryMB = (int)(memoryGB * 1024);
            return $"-Xms{memoryMB}M";
        }
    }
    
    /// <summary>
    /// 将最大堆内存转换为JVM参数格式
    /// </summary>
    /// <param name="memoryGB">内存大小（GB）</param>
    /// <returns>格式化后的最大堆参数，如"-Xmx12G"或"-Xmx16384M"</returns>
    private string GetMaximumHeapParam(double memoryGB)
    {
        if (memoryGB % 1 == 0)
        {
            // 整数GB，直接使用GB单位
            return $"-Xmx{(int)memoryGB}G";
        }
        else
        {
            // 小数GB，转换为MB
            int memoryMB = (int)(memoryGB * 1024);
            return $"-Xmx{memoryMB}M";
        }
    }

    /// <summary>
    /// 异步监控游戏进程退出状态
    /// </summary>
    /// <param name="process">游戏进程</param>
    /// <param name="launchCommand">启动命令</param>
    private async Task MonitorGameProcessExitAsync(Process process, string launchCommand)
    {
        // 使用 GameProcessMonitor 服务进行监控
        await _gameProcessMonitor.MonitorProcessAsync(process, launchCommand);
    }

    /// <summary>
    /// 异步读取进程输出（已由 GameProcessMonitor 事件处理，保留方法签名以兼容现有代码）
    /// </summary>
    /// <param name="process">游戏进程</param>
    private Task ReadProcessOutputAsync(Process process)
    {
        // 输出读取已由 GameProcessMonitor 的事件处理
        // 此方法保留以兼容现有代码结构
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// 显示错误分析弹窗
    /// </summary>
    /// <param name="exitCode">进程退出代码</param>
    /// <param name="launchCommand">启动命令</param>
    /// <param name="gameOutput">游戏输出日志副本</param>
    /// <param name="gameError">游戏错误日志副本</param>
    private async Task ShowErrorAnalysisDialog(int exitCode, string launchCommand, List<string> gameOutput, List<string> gameError)
    {
        // 等待其他 ContentDialog 关闭
        await _dialogSemaphore.WaitAsync();
        
        try
        {
            // 如果已经有 ContentDialog 打开，等待它关闭
            while (_isContentDialogOpen)
            {
                System.Diagnostics.Debug.WriteLine("[LaunchViewModel] 检测到其他 ContentDialog 正在显示，等待关闭...");
                await Task.Delay(500);
            }
            
            _isContentDialogOpen = true;
            
            // 分析崩溃原因（异步执行，不阻塞）
            var crashResult = await AnalyzeCrash(gameOutput, gameError);
            string errorTitle = crashResult.Title;
            string errorAnalysis = crashResult.Analysis;
        
        // 合并日志，移除输出日志字段
        List<string> allLogs = new List<string>();
        allLogs.Add("=== 游戏崩溃报告 ===");
        allLogs.Add($"崩溃时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        allLogs.Add($"退出代码: {exitCode}");
        allLogs.Add($"崩溃分析: {errorAnalysis}");
        allLogs.Add("");
        allLogs.Add("=== 游戏错误日志 ===");
        allLogs.AddRange(gameError);
        allLogs.Add("");
        
        // 创建完整的日志文本
        string fullLog = string.Join(Environment.NewLine, allLogs);
        
        // 使用系统错误色（自动适配主题）
        var errorRedColor = Windows.UI.Color.FromArgb(255, 196, 43, 28);
        var errorBgColor = Windows.UI.Color.FromArgb(30, 232, 17, 35);
        
        // 创建 Fluent Design 风格的崩溃提示内容
        var warningPanel = new StackPanel
        {
            Spacing = 20,
            Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 0)
        };
        
        // 顶部警告卡片（Fluent Design 风格）
        var warningCard = new Border
        {
            Background = new SolidColorBrush(errorBgColor),
            BorderBrush = new SolidColorBrush(errorRedColor),
            BorderThickness = new Microsoft.UI.Xaml.Thickness(1),
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8),
            Padding = new Microsoft.UI.Xaml.Thickness(20, 16, 20, 16)
        };
        
        var warningCardContent = new StackPanel { Spacing = 12 };
        
        // 标题行（图标 + 文字）
        var headerStack = new StackPanel
        {
            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal,
            Spacing = 12
        };
        
        var warningIcon = new FontIcon
        {
            Glyph = "\uE7BA", // 警告图标
            FontSize = 24,
            Foreground = new SolidColorBrush(errorRedColor)
        };
        
        // 标题显示分析结果（如果有的话）
        string titleText = string.IsNullOrWhiteSpace(errorTitle)
            ? "游戏意外退出"
            : $"游戏意外退出：{errorTitle}";
        
        var warningTitle = new TextBlock
        {
            Text = titleText,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(errorRedColor),
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
        };
        
        headerStack.Children.Add(warningIcon);
        headerStack.Children.Add(warningTitle);
        warningCardContent.Children.Add(headerStack);
        
        // 提示文字（不设置 Foreground，使用系统默认文字色）
        var hintText = new TextBlock
        {
            Text = "为了快速解决问题，请导出完整的崩溃日志，而不是截图。",
            FontSize = 14,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
        };
        
        // 检查彩蛋模式
        var localSettingsService = App.GetService<ILocalSettingsService>();
        var isEasterEggMode = await localSettingsService.ReadSettingAsync<bool?>("EasterEggMode") ?? false;
        
        if (isEasterEggMode)
        {
            // 彩蛋模式：添加文字缩放动画
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleXAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 5.15,
                Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(500)),
                AutoReverse = true,
                RepeatBehavior = Microsoft.UI.Xaml.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.SineEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut }
            };
            
            // 设置 RenderTransform
            hintText.RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform();
            hintText.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleXAnimation, hintText);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleXAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
            
            var scaleYAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 5.15,
                Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(500)),
                AutoReverse = true,
                RepeatBehavior = Microsoft.UI.Xaml.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.SineEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleYAnimation, hintText);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleYAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");
            
            scaleAnimation.Children.Add(scaleXAnimation);
            scaleAnimation.Children.Add(scaleYAnimation);
            
            // 在 hintText 加载后启动动画
            hintText.Loaded += (s, e) => scaleAnimation.Begin();
        }
        
        warningCardContent.Children.Add(hintText);
        
        warningCard.Child = warningCardContent;
        warningPanel.Children.Add(warningCard);
        
        // 操作指引卡片（使用 CardBackgroundFillColorDefaultBrush 自动适配主题）
        var instructionCard = new Border
        {
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8),
            Padding = new Microsoft.UI.Xaml.Thickness(20, 16, 20, 16)
        };
        instructionCard.SetValue(Border.BackgroundProperty, 
            Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]);
        instructionCard.SetValue(Border.BorderBrushProperty,
            Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"]);
        instructionCard.BorderThickness = new Microsoft.UI.Xaml.Thickness(1);
        
        var instructionStack = new StackPanel { Spacing = 10 };
        
        var instructionTitle = new TextBlock
        {
            Text = "正确的求助步骤",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 4)
        };
        instructionStack.Children.Add(instructionTitle);
        
        var step1 = new TextBlock
        {
            Text = "1. 点击下方「导出崩溃日志」按钮",
            FontSize = 14,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            Opacity = 0.9
        };
        instructionStack.Children.Add(step1);
        
        var step2 = new TextBlock
        {
            Text = "2. 将导出的 ZIP 文件发送给技术支持",
            FontSize = 14,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            Opacity = 0.9
        };
        instructionStack.Children.Add(step2);
        
        var step3 = new TextBlock
        {
            Text = "日志文件包含启动器日志、游戏日志等信息，能帮助快速定位问题",
            FontSize = 13,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            Opacity = 0.7,
            Margin = new Microsoft.UI.Xaml.Thickness(0, 4, 0, 0)
        };
        instructionStack.Children.Add(step3);
        
        instructionCard.Child = instructionStack;
        warningPanel.Children.Add(instructionCard);
        
        // 日志预览（可折叠）
        var logExpander = new Microsoft.UI.Xaml.Controls.Expander
        {
            Header = "查看日志预览",
            IsExpanded = false,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch
        };
        
        var logPreviewText = new TextBlock
        {
            Text = fullLog,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 11,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            Opacity = 0.7
        };
        
        var logScroller = new ScrollViewer
        {
            Content = logPreviewText,
            MaxHeight = 200,
            VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto,
            Margin = new Microsoft.UI.Xaml.Thickness(0, 8, 0, 0)
        };
        
        logExpander.Content = logScroller;
        warningPanel.Children.Add(logExpander);
        
        // 创建错误分析弹窗
        var dialog = new ContentDialog
        {
            Title = "游戏崩溃",
            Content = warningPanel,
            PrimaryButtonText = "导出崩溃日志",
            SecondaryButtonText = "查看详细日志",
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        // 处理按钮点击事件
        dialog.PrimaryButtonClick += async (sender, args) =>
        {
            // 导出崩溃日志按钮
            var navigationService = App.GetService<INavigationService>();
            navigationService.NavigateTo(typeof(ErrorAnalysisViewModel).FullName!, Tuple.Create(launchCommand, gameOutput, gameError));
            
            // 延迟一下，确保页面加载完成
            await Task.Delay(500);
            
            // 自动触发导出
            var errorAnalysisViewModel = App.GetService<ErrorAnalysisViewModel>();
            await errorAnalysisViewModel.ExportErrorLogsCommand.ExecuteAsync(null);
        };
        
        dialog.SecondaryButtonClick += (sender, args) =>
        {
            // 查看详细日志按钮
            var navigationService = App.GetService<INavigationService>();
            navigationService.NavigateTo(typeof(ErrorAnalysisViewModel).FullName!, Tuple.Create(launchCommand, gameOutput, gameError));
        };
        
        // 彩蛋模式：窗口摇晃效果
        CancellationTokenSource? shakeTokenSource = null;
        if (isEasterEggMode)
        {
            shakeTokenSource = new CancellationTokenSource();
            var shakeToken = shakeTokenSource.Token;
            
            // 启动窗口摇晃
            _ = Task.Run(async () =>
            {
                var random = new Random();
                var originalPosition = new Windows.Graphics.PointInt32();
                bool gotOriginalPosition = false;
                
                while (!shakeToken.IsCancellationRequested)
                {
                    try
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                var appWindow = App.MainWindow.AppWindow;
                                if (!gotOriginalPosition)
                                {
                                    originalPosition = appWindow.Position;
                                    gotOriginalPosition = true;
                                }
                                
                                // 随机偏移
                                int offsetX = random.Next(-15, 6);
                                int offsetY = random.Next(-15, 6);
                                
                                appWindow.Move(new Windows.Graphics.PointInt32(
                                    originalPosition.X + offsetX,
                                    originalPosition.Y + offsetY
                                ));
                            }
                            catch { }
                        });
                        
                        await Task.Delay(50, shakeToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                
                // 恢复原始位置
                if (gotOriginalPosition)
                {
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            App.MainWindow.AppWindow.Move(originalPosition);
                        }
                        catch { }
                    });
                }
            }, shakeToken);
        }
        
        dialog.Closed += (s, e) =>
        {
            // 停止摇晃
            shakeTokenSource?.Cancel();
        };
        
            await dialog.ShowAsync();
        }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x80000019))
        {
            // 捕获 "Only a single ContentDialog can be open at any time" 异常
            System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] ContentDialog 冲突: {ex.Message}");
            
            // 直接导航到 ErrorAnalysisPage，不显示弹窗
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                var navigationService = App.GetService<INavigationService>();
                navigationService.NavigateTo(typeof(ErrorAnalysisViewModel).FullName!, Tuple.Create(launchCommand, gameOutput, gameError));
            });
        }
        finally
        {
            _isContentDialogOpen = false;
            _dialogSemaphore.Release();
        }
    }
    
    /// <summary>
    /// 分析崩溃原因
    /// </summary>
    /// <param name="gameOutput">游戏输出日志</param>
    /// <param name="gameError">游戏错误日志</param>
    /// <returns>崩溃分析结果</returns>
    private async Task<CrashAnalysisResult> AnalyzeCrash(List<string> gameOutput, List<string> gameError)
    {
        // 使用 CrashAnalyzer 服务进行分析
        return await _crashAnalyzer.AnalyzeCrashAsync(0, gameOutput, gameError);
    }
    
    /// <summary>
    /// 导出崩溃日志
    /// </summary>
    /// <param name="launchCommand">启动命令</param>
    /// <param name="gameOutput">游戏输出日志副本</param>
    /// <param name="gameError">游戏错误日志副本</param>
    private async Task ExportCrashLogsAsync(string launchCommand, List<string> gameOutput, List<string> gameError)
    {
        try
        {
            // 获取桌面路径
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string zipFileName = $"minecraft_crash_{timestamp}.zip";
            string zipFilePath = Path.Combine(desktopPath, zipFileName);
            
            // 创建临时文件夹用于存放日志文件
            string tempFolder = Path.Combine(Path.GetTempPath(), $"minecraft_crash_temp_{timestamp}");
            Directory.CreateDirectory(tempFolder);
            
            try
            {
                // 生成启动参数.bat文件
                string batFilePath = Path.Combine(tempFolder, "启动参数.bat");
                await File.WriteAllTextAsync(batFilePath, launchCommand);
                
                // 生成输出日志.txt文件
                string logFilePath = Path.Combine(tempFolder, "输出日志.txt");
                List<string> allLogs = new List<string>();
                allLogs.Add("=== 游戏崩溃报告 ===");
                allLogs.Add($"崩溃时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                allLogs.Add("");
                allLogs.Add("=== 游戏输出日志 ===");
                allLogs.AddRange(gameOutput);
                allLogs.Add("");
                allLogs.Add("=== 游戏错误日志 ===");
                allLogs.AddRange(gameError);
                await File.WriteAllTextAsync(logFilePath, string.Join(Environment.NewLine, allLogs));
                
                // 如果文件已存在，先删除
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }

                // 打包为zip文件
                ZipFile.CreateFromDirectory(tempFolder, zipFilePath);
                
                // 显示导出成功提示
                App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    var successDialog = new ContentDialog
                    {
                        Title = "导出成功",
                        Content = $"崩溃日志已成功导出到桌面：{zipFileName}",
                        PrimaryButtonText = "确定",
                        XamlRoot = App.MainWindow.Content.XamlRoot
                    };
                    await successDialog.ShowAsync();
                });
            }
            finally
            {
                // 清理临时文件夹
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"导出日志失败：{ex.Message}");
            
            // 显示导出失败提示
            App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                var errorDialog = new ContentDialog
                {
                    Title = "导出失败",
                    Content = $"导出崩溃日志失败：{ex.Message}",
                    PrimaryButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            });
        }
    }
    
    private string GetRequiredJavaVersionText()
    {
        if (string.IsNullOrEmpty(SelectedVersion)) return "8";
        string versionStr = SelectedVersion;
        if (versionStr.StartsWith("1.12") || versionStr.StartsWith("1.11") || versionStr.StartsWith("1.10") || versionStr.StartsWith("1.9") || versionStr.StartsWith("1.8"))
        {
            return "8 (jre-legacy)";
        }
        else if (versionStr.StartsWith("1.17") || versionStr.StartsWith("1.18"))
        {
            return "17";
        }
        else if (versionStr.StartsWith("1.19") || versionStr.StartsWith("1.20") || versionStr.StartsWith("1.21"))
        {
            return "17 或 21";
        }
        return "8 或更高版本";
    }
    
    /// <summary>
    /// 判断Minecraft版本是否低于1.9
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <returns>如果版本低于1.9则返回true，否则返回false</returns>
    private bool IsVersionBelow1_9(string versionId)
    {
        if (string.IsNullOrEmpty(versionId)) return false;
        
        // 简单判断：检查版本号是否以1.0到1.8开头
        if (versionId.StartsWith("1.0") || versionId.StartsWith("1.1") || versionId.StartsWith("1.2") ||
            versionId.StartsWith("1.3") || versionId.StartsWith("1.4") || versionId.StartsWith("1.5") ||
            versionId.StartsWith("1.6") || versionId.StartsWith("1.7") || versionId.StartsWith("1.8"))
        {
            return true;
        }
        
        // 对于其他可能的版本格式，使用Version类进行比较
        try
        {
            // 处理"1.8.9"这样的格式
            string versionStr = versionId;
            if (versionStr.Contains("-")) // 处理带有后缀的版本，如"1.8.9-forge1.8.9-11.15.1.2318-1.8.9"
            {
                versionStr = versionStr.Split('-')[0];
            }
            
            Version version = new Version(versionStr);
            Version version1_9 = new Version("1.9");
            return version < version1_9;
        }
        catch (Exception)
        {
            // 如果版本号格式无法解析，默认返回false
            return false;
        }
    }

    /// <summary>
    /// 判断Minecraft版本是否需要添加AlphaVanillaTweaker启动参数
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <returns>如果版本需要添加参数则返回true，否则返回false</returns>
    private bool NeedsAlphaVanillaTweaker(string versionId)
    {
        if (string.IsNullOrEmpty(versionId)) return false;
        
        // 需要添加--tweakClass参数的特定版本列表
        string[] versionsNeedingTweaker = {
            "c0.0.11a",
            "c0.0.13a_03",
            "c0.0.13a",
            "c0.30.01c",
            "inf-20100618",
            "a1.0.4",
            "a1.0.5_01"
        };
        
        // 检查当前版本是否在需要添加参数的列表中
        return versionsNeedingTweaker.Any(v => versionId.StartsWith(v));
    }
    
    /// <summary>
    /// 解析命令行参数，考虑引号内的空格情况
    /// </summary>
    /// <param name="argsString">命令行参数字符串</param>
    /// <returns>解析后的参数列表</returns>
    private List<string> ParseArguments(string argsString)
    {
        var args = new List<string>();
        var currentArg = new StringBuilder();
        bool inQuotes = false;
        
        for (int i = 0; i < argsString.Length; i++)
        {
            char c = argsString[i];
            
            if (c == '"')
            {
                // 切换引号状态
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                // 空格分隔符，且不在引号内
                if (currentArg.Length > 0)
                {
                    args.Add(currentArg.ToString());
                    currentArg.Clear();
                }
            }
            else
            {
                currentArg.Append(c);
            }
        }
        
        // 添加最后一个参数
        if (currentArg.Length > 0)
        {
            args.Add(currentArg.ToString());
        }
        
        return args;
    }
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IFileService _fileService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly MicrosoftAuthService _microsoftAuthService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<LaunchViewModel> _logger;
    private readonly AuthlibInjectorService _authlibInjectorService;
    private readonly IJavaRuntimeService _javaRuntimeService;
    
    // 新增：Phase 5 重构服务
    private readonly IGameLaunchService _gameLaunchService;
    private readonly IGameProcessMonitor _gameProcessMonitor;
    private readonly ICrashAnalyzer _crashAnalyzer;
    private readonly IRegionValidator _regionValidator;
    private readonly ITokenRefreshService _tokenRefreshService;
    private readonly IVersionConfigService _versionConfigService;
    
    // 保存游戏输出日志
    private List<string> _gameOutput = new List<string>();
    private List<string> _gameError = new List<string>();
    private string _launchCommand = string.Empty;
    
    // 游戏启动时间（用于计算游戏时长）
    private DateTime _gameStartTime;
    private string _currentLaunchedVersion = string.Empty;
    
    // 实时日志开关状态
    private bool _isRealTimeLogsEnabled = false;
    private const string JavaPathKey = "JavaPath";
    private const string JavaSelectionModeKey = "JavaSelectionMode";
    private const string JavaVersionsKey = "JavaVersions";
    private const string SelectedJavaVersionKey = "SelectedJavaVersion";
    private const string OfflineLaunchCountKey = "OfflineLaunchCount";
    private const string EnableVersionIsolationKey = "EnableVersionIsolation";
    private const string SelectedVersionKey = "SelectedMinecraftVersion";

    [ObservableProperty]
    private ObservableCollection<string> _installedVersions = new();

    [ObservableProperty]
    private string _selectedVersion = "";

    /// <summary>
    /// 页面标题，显示当前选中的版本或默认文本
    /// </summary>
    public string PageTitle => string.IsNullOrEmpty(SelectedVersion) 
        ? "LaunchPage_DefaultTitle".GetLocalized() 
        : SelectedVersion;

    /// <summary>
    /// 页面标题字体大小，根据文本长度自适应
    /// </summary>
    public double PageTitleFontSize
    {
        get
        {
            var title = PageTitle;
            if (string.IsNullOrEmpty(title))
                return 48;
            
            // 根据文本长度调整字体大小
            if (title.Length <= 10)
                return 48; // 短文本，使用大字体
            else if (title.Length <= 20)
                return 40; // 中等长度
            else if (title.Length <= 30)
                return 32; // 较长文本
            else
                return 28; // 很长的文本
        }
    }

    /// <summary>
    /// 版本选择按钮显示文本
    /// </summary>
    public string SelectedVersionDisplay => string.IsNullOrEmpty(SelectedVersion) 
        ? "LaunchPage_SelectVersionText".GetLocalized() 
        : SelectedVersion;

    [ObservableProperty]
    private bool _isOfflineMode = true;

    [ObservableProperty]
    private string _username = "Player";

    [ObservableProperty]
    private bool _isLaunching = false;

    [ObservableProperty]
    private string _launchStatus = "准备启动";

    [ObservableProperty]
    private double _downloadProgress = 0;

    /// <summary>
    /// 角色列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<MinecraftProfile> _profiles = new ObservableCollection<MinecraftProfile>();

    /// <summary>
    /// 当前选中角色
    /// </summary>
    [ObservableProperty]
    private MinecraftProfile _selectedProfile;
    
    /// <summary>
    /// 角色选择按钮显示文本
    /// </summary>
    public string SelectedProfileDisplay => SelectedProfile == null || string.IsNullOrEmpty(SelectedProfile.Name)
        ? "LaunchPage_SelectCharacterText".GetLocalized() 
        : SelectedProfile.Name;
    
    /// <summary>
    /// 当 SelectedProfile 变化时通知 UI 更新显示文本
    /// </summary>
    partial void OnSelectedProfileChanged(MinecraftProfile value)
    {
        OnPropertyChanged(nameof(SelectedProfileDisplay));
    }

    /// <summary>
    /// 启动成功消息，用于InfoBar显示
    /// </summary>
    [ObservableProperty]
    private string _launchSuccessMessage = string.Empty;
    
    /// <summary>
    /// 当前下载项信息，用于InfoBar显示
    /// </summary>
    [ObservableProperty]
    private string _currentDownloadItem = string.Empty;
    
    /// <summary>
    /// 启动成功InfoBar是否打开
    /// </summary>
    [ObservableProperty]
    private bool _isLaunchSuccessInfoBarOpen = false;
    
    /// <summary>
    /// 游戏是否正在运行（持久化状态）
    /// </summary>
    [ObservableProperty]
    private bool _isGameRunning = false;
    
    /// <summary>
    /// "查看日志"按钮是否可见（基于实时日志设置）
    /// </summary>
    [ObservableProperty]
    private bool _isViewLogsButtonVisible = false;
    
    /// <summary>
    /// InfoBar是否应该显示（准备阶段或游戏运行中）
    /// </summary>
    [ObservableProperty]
    private bool _isInfoBarOpen = false;
    
    /// <summary>
    /// 更新InfoBar显示状态
    /// </summary>
    private void UpdateInfoBarOpenState()
    {
        bool newState = IsLaunchSuccessInfoBarOpen || IsGameRunning;
        System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] UpdateInfoBarOpenState: IsLaunchSuccessInfoBarOpen={IsLaunchSuccessInfoBarOpen}, IsGameRunning={IsGameRunning}, newState={newState}");
        IsInfoBarOpen = newState;
    }
    
    /// <summary>
    /// Minecraft 最新新闻标题
    /// </summary>
    [ObservableProperty]
    private string _latestMinecraftNews = "加载中...";
    
    /// <summary>
    /// 最新新闻的完整数据（用于点击跳转）
    /// </summary>
    private MinecraftNewsEntry? _latestNewsEntry;
    
    /// <summary>
    /// 推荐 Mod 的完整数据（用于点击跳转）
    /// </summary>
    private ModrinthRandomProject? _recommendedMod;
    
    /// <summary>
    /// 推荐 Mod 标题
    /// </summary>
    [ObservableProperty]
    private string _recommendedModTitle = "加载中...";
    
    /// <summary>
    /// 新闻服务
    /// </summary>
    private MinecraftNewsService? _newsService;
    
    /// <summary>
    /// Modrinth 推荐服务
    /// </summary>
    private ModrinthRecommendationService? _recommendationService;
    
    /// <summary>
    /// 下载源工厂
    /// </summary>
    private readonly XianYuLauncher.Core.Services.DownloadSource.DownloadSourceFactory _downloadSourceFactory;
    
    /// <summary>
    /// 当前游戏进程
    /// </summary>
    private Process? _currentGameProcess = null;
    
    /// <summary>
    /// 下载取消令牌源
    /// </summary>
    private CancellationTokenSource? _downloadCancellationTokenSource = null;
    
    /// <summary>
    /// 是否正在下载/准备中
    /// </summary>
    private bool _isPreparingGame = false;
    
    /// <summary>
    /// 当前是否有 ContentDialog 正在显示
    /// </summary>
    private bool _isContentDialogOpen = false;
    
    /// <summary>
    /// ContentDialog 互斥锁
    /// </summary>
    private readonly SemaphoreSlim _dialogSemaphore = new SemaphoreSlim(1, 1);
    
    /// <summary>
    /// 当前版本路径，用于彩蛋显示
    /// </summary>
    public string CurrentVersionPath
    {
        get
        {
            try
            {
                var minecraftPath = _fileService.GetMinecraftDataPath();
                var versionsPath = Path.Combine(minecraftPath, "versions");
                return versionsPath;
            }
            catch (Exception ex)
            {
                return "获取路径失败：" + ex.Message;
            }
        }
    }

    /// <summary>
    /// 微软登录测试，用于彩蛋显示
    /// </summary>
    public string MicrosoftLoginTest => "微软登录功能已实现，可以通过启动页的测试按钮进行测试";

    public LaunchViewModel()
    {
        _minecraftVersionService = App.GetService<IMinecraftVersionService>();
        _fileService = App.GetService<IFileService>();
        _localSettingsService = App.GetService<ILocalSettingsService>();
        _microsoftAuthService = App.GetService<MicrosoftAuthService>();
        _navigationService = App.GetService<INavigationService>();
        _logger = App.GetService<ILogger<LaunchViewModel>>();
        _authlibInjectorService = App.GetService<AuthlibInjectorService>();
        _downloadSourceFactory = App.GetService<XianYuLauncher.Core.Services.DownloadSource.DownloadSourceFactory>();
        _javaRuntimeService = App.GetService<IJavaRuntimeService>();
        
        // 新增：Phase 5 重构服务
        _gameLaunchService = App.GetService<IGameLaunchService>();
        _gameProcessMonitor = App.GetService<IGameProcessMonitor>();
        _crashAnalyzer = App.GetService<ICrashAnalyzer>();
        _regionValidator = App.GetService<IRegionValidator>();
        _tokenRefreshService = App.GetService<ITokenRefreshService>();
        _versionConfigService = App.GetService<IVersionConfigService>();
        
        // 设置 authlib-injector 回调
        _gameLaunchService.SetAuthlibInjectorCallback(new AuthlibInjectorCallbackImpl(_authlibInjectorService));
        
        // 设置令牌刷新回调
        _tokenRefreshService.SetCallback(new TokenRefreshCallbackImpl(this));
        
        // 订阅进程监控事件
        _gameProcessMonitor.ProcessExited += OnGameProcessExited;
        _gameProcessMonitor.OutputReceived += OnGameOutputReceived;
        _gameProcessMonitor.ErrorReceived += OnGameErrorReceived;
        
        // 订阅Minecraft路径变化事件
        _fileService.MinecraftPathChanged += OnMinecraftPathChanged;
        
        InitializeAsync().ConfigureAwait(false);
    }
    
    /// <summary>
    /// Authlib-Injector 回调实现
    /// </summary>
    private class AuthlibInjectorCallbackImpl : IAuthlibInjectorCallback
    {
        private readonly AuthlibInjectorService _authlibInjectorService;
        
        public AuthlibInjectorCallbackImpl(AuthlibInjectorService authlibInjectorService)
        {
            _authlibInjectorService = authlibInjectorService;
        }
        
        public async Task<List<string>> GetJvmArgumentsAsync(string authServer)
        {
            var jvmArgs = await _authlibInjectorService.GetJvmArgumentsAsync(authServer);
            
            // 转换MSIX虚拟路径为真实物理路径
            // 因为Java进程从沙盒外启动，需要使用真实路径
            for (int i = 0; i < jvmArgs.Count; i++)
            {
                if (jvmArgs[i].StartsWith("-javaagent:"))
                {
                    string originalArg = jvmArgs[i];
                    // 提取路径部分：-javaagent:路径=参数
                    int equalIndex = originalArg.IndexOf('=', "-javaagent:".Length);
                    string pathPart = equalIndex > 0 
                        ? originalArg.Substring("-javaagent:".Length, equalIndex - "-javaagent:".Length)
                        : originalArg.Substring("-javaagent:".Length);
                    
                    // 检查是否需要转换路径
                    if (!pathPart.Contains("Packages"))
                    {
                        // 这是虚拟路径，转换为真实路径
                        try
                        {
                            string packagePath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
                            string packagesRoot = packagePath.Substring(0, packagePath.LastIndexOf("LocalState"));
                            string realPath = pathPart.Replace(
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XianYuLauncher"),
                                Path.Combine(packagesRoot, "LocalCache", "Local", "XianYuLauncher")
                            );
                            
                            // 重建参数
                            if (equalIndex > 0)
                            {
                                string paramPart = originalArg.Substring(equalIndex);
                                jvmArgs[i] = $"-javaagent:{realPath}{paramPart}";
                            }
                            else
                            {
                                jvmArgs[i] = $"-javaagent:{realPath}";
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"[AuthlibCallback] 路径转换: {pathPart} -> {realPath}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AuthlibCallback] 路径转换失败: {ex.Message}");
                        }
                    }
                }
            }
            
            return jvmArgs;
        }
    }
    
    /// <summary>
    /// 令牌刷新回调实现
    /// </summary>
    private class TokenRefreshCallbackImpl : ITokenRefreshCallback
    {
        private readonly LaunchViewModel _viewModel;
        
        public TokenRefreshCallbackImpl(LaunchViewModel viewModel)
        {
            _viewModel = viewModel;
        }
        
        public async Task<MinecraftProfile?> RefreshTokenAsync(MinecraftProfile profile)
        {
            var characterManagementViewModel = App.GetService<CharacterManagementViewModel>();
            characterManagementViewModel.CurrentProfile = profile;
            await characterManagementViewModel.ForceRefreshTokenAsync();
            return characterManagementViewModel.CurrentProfile;
        }
    }
    
    /// <summary>
    /// 游戏进程退出事件处理
    /// </summary>
    private async void OnGameProcessExited(object? sender, ProcessExitedEventArgs e)
    {
        LaunchStatus += $"\n游戏进程已退出，退出代码: {e.ExitCode}";
        
        // 更新游戏运行状态（这会自动关闭InfoBar）
        IsGameRunning = false;
        
        // 计算并记录游戏时长
        if (!string.IsNullOrEmpty(_currentLaunchedVersion) && _gameStartTime != default)
        {
            var playTimeSeconds = (long)(DateTime.Now - _gameStartTime).TotalSeconds;
            if (playTimeSeconds > 0)
            {
                _ = _versionConfigService.RecordExitAsync(_currentLaunchedVersion, playTimeSeconds);
            }
            _currentLaunchedVersion = string.Empty;
            _gameStartTime = default;
        }
        
        // 检查是否异常退出（排除用户主动终止的情况）
        if (e.ExitCode != 0 && !e.IsUserTerminated)
        {
            Console.WriteLine($"游戏异常退出，退出代码: {e.ExitCode}");
            
            App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                await ShowErrorAnalysisDialog(e.ExitCode, e.LaunchCommand, e.OutputLogs, e.ErrorLogs);
            });
        }
        else if (e.IsUserTerminated)
        {
            Console.WriteLine("游戏被用户主动终止");
        }
        
        // 清空日志，准备下一次启动
        _gameOutput.Clear();
        _gameError.Clear();
        _launchCommand = string.Empty;
    }
    
    /// <summary>
    /// 游戏输出接收事件处理
    /// </summary>
    private void OnGameOutputReceived(object? sender, OutputReceivedEventArgs e)
    {
        lock (_gameOutput)
        {
            _gameOutput.Add(e.Line);
        }
        Console.WriteLine($"[Minecraft Output]: {e.Line}");
        
        // 只有在启用实时日志时才更新到ErrorAnalysisViewModel
        if (_isRealTimeLogsEnabled)
        {
            try
            {
                var errorAnalysisViewModel = App.GetService<ErrorAnalysisViewModel>();
                errorAnalysisViewModel.AddGameOutputLog(e.Line);
            }
            catch (Exception)
            {
                // 如果ErrorAnalysisViewModel不可用，忽略错误
            }
        }
    }
    
    /// <summary>
    /// 游戏错误接收事件处理
    /// </summary>
    private void OnGameErrorReceived(object? sender, ErrorReceivedEventArgs e)
    {
        lock (_gameError)
        {
            _gameError.Add(e.Line);
        }
        Console.WriteLine($"[Minecraft Error]: {e.Line}");
        
        // 只有在启用实时日志时才更新到ErrorAnalysisViewModel
        if (_isRealTimeLogsEnabled)
        {
            try
            {
                var errorAnalysisViewModel = App.GetService<ErrorAnalysisViewModel>();
                errorAnalysisViewModel.AddGameErrorLog(e.Line);
            }
            catch (Exception)
            {
                // 如果ErrorAnalysisViewModel不可用，忽略错误
            }
        }
    }
    
    /// <summary>
    /// 当Minecraft路径变化时触发
    /// </summary>
    private async void OnMinecraftPathChanged(object? sender, string newPath)
    {
        await LoadInstalledVersionsAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadInstalledVersionsAsync();
        LoadProfiles();
        ShowMinecraftPathInfo();
        await LoadLatestNewsAsync();
        await LoadRecommendedModAsync();
    }
    
    /// <summary>
    /// 加载最新 Minecraft 新闻
    /// </summary>
    private async Task LoadLatestNewsAsync()
    {
        try
        {
            _newsService ??= new MinecraftNewsService(_fileService);
            var newsData = await _newsService.GetLatestNewsAsync();
            
            if (newsData?.Entries != null && newsData.Entries.Count > 0)
            {
                _latestNewsEntry = newsData.Entries[0];
                LatestMinecraftNews = _latestNewsEntry.Title;
            }
            else
            {
                LatestMinecraftNews = "暂无新闻";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[新闻加载] 失败: {ex.Message}");
            LatestMinecraftNews = "加载失败";
        }
    }
    
    /// <summary>
    /// 打开最新新闻详情
    /// </summary>
    [RelayCommand]
    private void OpenLatestNews()
    {
        if (_latestNewsEntry != null)
        {
            _navigationService.NavigateTo(typeof(NewsDetailViewModel).FullName!, _latestNewsEntry);
        }
    }
    
    /// <summary>
    /// 加载推荐 Mod
    /// </summary>
    private async Task LoadRecommendedModAsync()
    {
        try
        {
            _recommendationService ??= new ModrinthRecommendationService(_fileService, _downloadSourceFactory);
            var project = await _recommendationService.GetRandomProjectAsync();
            
            if (project != null)
            {
                _recommendedMod = project;
                RecommendedModTitle = project.Title;
            }
            else
            {
                RecommendedModTitle = "暂无推荐";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Mod推荐加载] 失败: {ex.Message}");
            RecommendedModTitle = "加载失败";
        }
    }
    
    /// <summary>
    /// 打开推荐 Mod 详情
    /// </summary>
    [RelayCommand]
    private void OpenRecommendedMod()
    {
        if (_recommendedMod != null)
        {
            // 导航到 ModDownloadDetailPage，传递项目 ID
            _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, _recommendedMod.Id);
        }
    }

    /// <summary>
    /// 角色数据文件路径
    /// </summary>
    private string ProfilesFilePath => Path.Combine(_fileService.GetMinecraftDataPath(), "profiles.json");

    /// <summary>
    /// 加载角色列表
    /// </summary>
    public void LoadProfiles()
    {
        try
        {
            if (File.Exists(ProfilesFilePath))
            {
                // 🔒 使用安全方法读取（自动解密token）
                var profilesList = XianYuLauncher.Core.Helpers.TokenEncryption.LoadProfilesSecurely(ProfilesFilePath);
                
                // 清空现有列表并添加所有角色
                Profiles.Clear();
                foreach (var profile in profilesList)
                {
                    Profiles.Add(profile);
                }
                
                // 设置活跃角色
                if (Profiles.Count > 0)
                {
                    SelectedProfile = Profiles.FirstOrDefault(p => p.IsActive) ?? Profiles.First();
                    // 更新用户名
                    if (SelectedProfile != null)
                    {
                        Username = SelectedProfile.Name;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LaunchStatus = "加载角色列表失败：" + ex.Message;
        }
    }

    /// <summary>
    /// 切换角色命令
    /// </summary>
    [RelayCommand]
    private void SwitchProfile(MinecraftProfile profile)
    {
        if (profile != null && Profiles.Contains(profile))
        {
            // 更新活跃状态
            foreach (var p in Profiles)
            {
                p.IsActive = false;
            }
            profile.IsActive = true;
            
            // 更新当前选中角色
            SelectedProfile = profile;
            Username = profile.Name;
            
            // 保存角色列表
            SaveProfiles();
        }
    }

    /// <summary>
    /// 保存角色列表
    /// </summary>
    private async void SaveProfiles()
    {
        try
        {
            // 🔒 使用 ProfileManager 安全保存（自动加密token）
            var profileManager = App.GetService<IProfileManager>();
            await profileManager.SaveProfilesAsync(Profiles.ToList());
            System.Diagnostics.Debug.WriteLine($"[Launch] 角色列表已保存（token已加密），共 {Profiles.Count} 个角色");
        }
        catch (Exception ex)
        {
            LaunchStatus = "保存角色列表失败：" + ex.Message;
        }
    }

    /// <summary>
    /// 导航到角色页面命令
    /// </summary>
    [RelayCommand]
    private void NavigateToCharactersPage()
    {
        // 这里将在UI层实现导航逻辑
    }
    
    /// <summary>
    /// 查看实时日志命令
    /// </summary>
    [RelayCommand]
    private void ViewLogs()
    {
        _navigationService.NavigateTo(typeof(ErrorAnalysisViewModel).FullName!);
    }

    [RelayCommand]
    private async Task LoadInstalledVersionsAsync()
    {
        try
        {
            // 获取正确的Minecraft游戏文件夹路径
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var versionsPath = Path.Combine(minecraftPath, "versions");
            if (Directory.Exists(versionsPath))
            {
                InstalledVersions.Clear();
                var directories = Directory.GetDirectories(versionsPath);
                
                foreach (var dir in directories)
                {
                    var versionName = Path.GetFileName(dir);
                    // 检查版本文件夹中是否存在jar文件和json文件
                    if (File.Exists(Path.Combine(dir, $"{versionName}.jar")) &&
                        File.Exists(Path.Combine(dir, $"{versionName}.json")))
                    {
                        InstalledVersions.Add(versionName);
                    }
                }

                if (InstalledVersions.Any())
                {
                    // 尝试从本地设置中读取保存的版本
                    string savedVersion = await _localSettingsService.ReadSettingAsync<string>(SelectedVersionKey);
                    
                    // 如果保存的版本存在于安装列表中，则使用保存的版本，否则选择最新版本
                    if (!string.IsNullOrEmpty(savedVersion) && InstalledVersions.Contains(savedVersion))
                    {
                        SelectedVersion = savedVersion;
                    }
                    else
                    {
                        // 按版本号降序排序并选择最新版本
                        SelectedVersion = InstalledVersions.OrderByDescending(v => v).First();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LaunchStatus = "加载版本列表失败：" + ex.Message;
        }
        finally
        {
            ShowMinecraftPathInfo();
        }
    }

    // 显示Minecraft版本路径信息
    private void ShowMinecraftPathInfo()
    {
        try
        {
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var versionsPath = Path.Combine(minecraftPath, "versions");
            
            // 更新启动状态显示路径信息
            LaunchStatus = $"当前Minecraft版本路径: {versionsPath}";
        }
        catch (Exception ex)
        {
            LaunchStatus = "获取路径信息失败：" + ex.Message;
        }
    }


    
    // 当用户点击版本列表时触发
    partial void OnSelectedVersionChanged(string value)
    {
        // 保存选中的版本到本地设置
        _localSettingsService.SaveSettingAsync(SelectedVersionKey, value).ConfigureAwait(false);
        ShowMinecraftPathInfo();
        // 通知UI更新版本显示文本、页面标题和字体大小
        OnPropertyChanged(nameof(SelectedVersionDisplay));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageTitleFontSize));
    }
    
    /// <summary>
    /// 当游戏运行状态变化时的处理
    /// </summary>
    partial void OnIsGameRunningChanged(bool value)
    {
        System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] IsGameRunning changed to: {value}");
        
        // 更新InfoBar显示状态
        UpdateInfoBarOpenState();
        
        // 当游戏运行状态变为 false（游戏被关闭）
        if (!value)
        {
            System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] Game stopped, _isPreparingGame={_isPreparingGame}, _currentGameProcess={_currentGameProcess != null}");
            
            // 如果正在准备/下载中，取消下载
            if (_isPreparingGame && _downloadCancellationTokenSource != null)
            {
                _downloadCancellationTokenSource.Cancel();
                _isPreparingGame = false;
                LaunchStatus = "已取消下载";
                System.Diagnostics.Debug.WriteLine("[LaunchViewModel] 用户取消了下载");
            }
            // 如果游戏进程正在运行，终止进程
            else if (_currentGameProcess != null && !_currentGameProcess.HasExited)
            {
                try
                {
                    // 通过 GameProcessMonitor 终止进程，标记为用户主动终止
                    _gameProcessMonitor.TerminateProcess(_currentGameProcess, isUserTerminated: true);
                    LaunchStatus = "游戏进程已终止";
                    System.Diagnostics.Debug.WriteLine("[LaunchViewModel] 用户终止了游戏进程");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] 终止进程失败: {ex.Message}");
                }
                finally
                {
                    _currentGameProcess = null;
                }
            }
        }
    }
    
    /// <summary>
    /// 当临时InfoBar状态变化时的处理
    /// </summary>
    partial void OnIsLaunchSuccessInfoBarOpenChanged(bool value)
    {
        System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] IsLaunchSuccessInfoBarOpen changed to: {value}");
        
        // 更新InfoBar显示状态
        UpdateInfoBarOpenState();
    }

    /// <summary>
    /// 检测当前地区是否为中国大陆
    /// </summary>
    /// <returns>如果是中国大陆地区返回true，否则返回false</returns>
    private bool IsChinaMainland()
    {
        // 使用 RegionValidator 服务
        return _regionValidator.IsChinaMainland();
    }

    /// <summary>
    /// 检查并刷新令牌（如果需要）
    /// </summary>
    private async Task CheckAndRefreshTokenIfNeededAsync()
    {
        // 检查是否为在线角色
        if (SelectedProfile != null && !SelectedProfile.IsOffline)
        {
            try
            {
                // 计算令牌剩余有效期，判断是否需要刷新
                var issueTime = SelectedProfile.IssueInstant;
                var expiresIn = SelectedProfile.ExpiresIn;
                var expiryTime = issueTime.AddSeconds(expiresIn);
                var timeUntilExpiry = expiryTime - DateTime.UtcNow;
                
                // 如果剩余有效期小于1小时，显示续签提示
                if (timeUntilExpiry < TimeSpan.FromHours(1))
                {
                    // 根据角色类型显示不同的续签消息
                    string renewingText = SelectedProfile.TokenType == "external" 
                        ? "正在进行外置登录续签" 
                        : "LaunchPage_MicrosoftAccountRenewingText".GetLocalized();
                    
                    // 显示 InfoBar 消息（刷新开始前）
                    IsLaunchSuccessInfoBarOpen = true;
                    LaunchSuccessMessage = $"{SelectedVersion} {renewingText}";
                    
                    // Token 刷新阶段不显示"查看日志"按钮
                    IsViewLogsButtonVisible = false;
                }
                
                var result = await _tokenRefreshService.CheckAndRefreshTokenAsync(SelectedProfile);
                
                if (result.WasRefreshed && result.UpdatedProfile != null)
                {
                    // 根据角色类型显示不同的完成消息
                    string renewedText = SelectedProfile.TokenType == "external" 
                        ? "外置登录续签成功" 
                        : "LaunchPage_MicrosoftAccountRenewedText".GetLocalized();
                    
                    // 更新InfoBar消息（刷新完成后）
                    LaunchSuccessMessage = $"{SelectedVersion} {renewedText}";
                    
                    // 刷新成功，更新当前角色信息
                    SelectedProfile = result.UpdatedProfile;
                }
                else if (!string.IsNullOrEmpty(result.StatusMessage))
                {
                    System.Diagnostics.Debug.WriteLine($"[TokenRefresh] {result.StatusMessage}");
                }
            }
            catch (Exception ex)
            {
                // 刷新失败，继续启动，但记录错误
                Console.WriteLine($"令牌刷新失败: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task LaunchGameAsync()
    {
        _logger.LogInformation("=== 开始启动游戏流程 ===");
        _logger.LogInformation("选中版本: {Version}", SelectedVersion);
        _logger.LogInformation("选中角色: {Profile}", SelectedProfile?.Name ?? "null");
        
        // 清空上次的日志，避免新游戏显示旧日志
        _gameOutput.Clear();
        _gameError.Clear();
        _launchCommand = string.Empty;
        
        if (string.IsNullOrEmpty(SelectedVersion))
        {
            _logger.LogWarning("未选择版本，启动中止");
            LaunchStatus = "LaunchPage_PleaseSelectVersionText".GetLocalized();
            return;
        }

        // 使用 RegionValidator 检查地区限制
        _logger.LogInformation("开始检查地区限制...");
        var regionValidation = _regionValidator.ValidateLoginMethod(SelectedProfile);
        if (!regionValidation.IsValid)
        {
            _logger.LogWarning("地区限制检查失败: {Errors}", string.Join(", ", regionValidation.Errors));
            
            // 显示地区限制弹窗
            var dialog = new ContentDialog
            {
                Title = "地区限制",
                Content = regionValidation.Errors.FirstOrDefault() ?? "当前地区无法使用此登录方式",
                PrimaryButtonText = "前往",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainWindow.Content.XamlRoot
            };

            dialog.PrimaryButtonClick += (sender, args) =>
            {
                _navigationService.NavigateTo("角色");
            };

            await dialog.ShowAsync();
            return;
        }
        _logger.LogInformation("地区限制检查通过");

        IsLaunching = true;
        LaunchStatus = "LaunchPage_StartingGameText".GetLocalized();
        _logger.LogInformation("设置启动状态: IsLaunching=true");

        try
        {
            // 检查并刷新令牌（如果需要）
            _logger.LogInformation("开始检查并刷新令牌...");
            await CheckAndRefreshTokenIfNeededAsync();
            _logger.LogInformation("令牌检查完成");
            
            // 显示准备中的 InfoBar
            _logger.LogInformation("显示准备游戏文件 InfoBar");
            IsLaunchSuccessInfoBarOpen = true;
            CurrentDownloadItem = "LaunchPage_PreparingGameFilesText".GetLocalized();
            LaunchSuccessMessage = $"{SelectedVersion} {"LaunchPage_PreparingGameFilesText".GetLocalized()}";
            
            // 准备阶段不显示"查看日志"按钮
            IsViewLogsButtonVisible = false;
            
            // 标记正在准备游戏
            _isPreparingGame = true;
            _downloadCancellationTokenSource = new CancellationTokenSource();
            
            // 调用 GameLaunchService 启动游戏
            _logger.LogInformation("调用 GameLaunchService.LaunchGameAsync...");
            
            // 用于存储当前下载的 hash 信息
            string currentDownloadHash = string.Empty;
            double currentProgress = 0;
            
            var result = await _gameLaunchService.LaunchGameAsync(
                SelectedVersion,
                SelectedProfile,
                progress =>
                {
                    // 检查是否已取消
                    if (_downloadCancellationTokenSource?.IsCancellationRequested == true)
                    {
                        throw new OperationCanceledException("用户取消了下载");
                    }
                    
                    currentProgress = progress;
                    DownloadProgress = progress;
                    LaunchStatus = string.Format("{0} {1:F1}%", "LaunchPage_PreparingGameFilesProgressText".GetLocalized(), progress);
                    CurrentDownloadItem = string.Format("{0} {1:F1}%", "LaunchPage_PreparingGameFilesProgressText".GetLocalized(), progress);
                    
                    // 更新 InfoBar 消息：显示百分比和当前下载的 hash
                    if (!string.IsNullOrEmpty(currentDownloadHash))
                    {
                        LaunchSuccessMessage = string.Format("{0} {1} {2:F1}% 正在下载:\n{3}", 
                            SelectedVersion, 
                            "LaunchPage_PreparingGameFilesProgressText".GetLocalized(), 
                            progress,
                            currentDownloadHash);
                    }
                    else
                    {
                        LaunchSuccessMessage = string.Format("{0} {1} {2:F1}%", 
                            SelectedVersion, 
                            "LaunchPage_PreparingGameFilesProgressText".GetLocalized(), 
                            progress);
                    }
                },
                status =>
                {
                    // 判断是否是 hash 信息（包含长字符串且不包含百分号）
                    if (status.Contains("正在准备游戏文件...") && !status.Contains("%"))
                    {
                        // 提取 hash 信息（去掉前缀）
                        currentDownloadHash = status.Replace("正在准备游戏文件... ", "").Trim();
                        
                        // 更新 InfoBar 消息：显示百分比和当前下载的 hash
                        LaunchSuccessMessage = string.Format("{0} {1} {2:F1}% 正在下载:\n{3}", 
                            SelectedVersion, 
                            "LaunchPage_PreparingGameFilesProgressText".GetLocalized(), 
                            currentProgress,
                            currentDownloadHash);
                    }
                    else
                    {
                        // 这是普通状态信息，更新上方的状态文本
                        LaunchStatus = status;
                    }
                },
                _downloadCancellationTokenSource.Token);
            
            _downloadCancellationTokenSource?.Dispose();
            _downloadCancellationTokenSource = null;
            _isPreparingGame = false;
            
            _logger.LogInformation("GameLaunchService 返回结果: Success={Success}, ErrorMessage={ErrorMessage}", 
                result.Success, result.ErrorMessage ?? "null");
            
            if (!result.Success)
            {
                _logger.LogError("游戏启动失败: {ErrorMessage}", result.ErrorMessage);
                LaunchStatus = result.ErrorMessage ?? "启动失败";
                
                // 如果是 Java 未找到，显示提示
                if (result.ErrorMessage?.Contains("Java") == true)
                {
                    _logger.LogWarning("Java 未找到，显示提示弹窗");
                    await ShowJavaNotFoundMessageAsync();
                }
                return;
            }
            
            _logger.LogInformation("游戏启动成功！");
            
            // 启动成功
            if (result.GameProcess != null)
            {
                _currentGameProcess = result.GameProcess;
                _launchCommand = result.LaunchCommand ?? string.Empty;
                
                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] Game launched successfully");
                
                // 游戏启动成功，显示"查看日志"按钮
                IsLaunchSuccessInfoBarOpen = true;
                IsViewLogsButtonVisible = _isRealTimeLogsEnabled; // 只有开启实时日志时才显示按钮
                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] 游戏启动成功，IsViewLogsButtonVisible = {IsViewLogsButtonVisible}");
                
                IsGameRunning = true;
                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] Set IsGameRunning = true");
                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] IsInfoBarOpen should now be: {IsInfoBarOpen}");
                
                // 检查是否启用了实时日志
                try
                {
                    _isRealTimeLogsEnabled = await _localSettingsService.ReadSettingAsync<bool?>("EnableRealTimeLogs") ?? false;
                }
                catch
                {
                    var settingsViewModel = App.GetService<SettingsViewModel>();
                    _isRealTimeLogsEnabled = settingsViewModel.EnableRealTimeLogs;
                }
                
                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] Real-time logs enabled: {_isRealTimeLogsEnabled}");
                
                // 更新"查看日志"按钮可见性
                IsViewLogsButtonVisible = _isRealTimeLogsEnabled;
                
                // 更新启动成功消息
                LaunchSuccessMessage = $"{SelectedVersion} {"LaunchPage_GameStartedSuccessfullyText".GetLocalized()}";
                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] LaunchSuccessMessage set to: {LaunchSuccessMessage}");
                
                if (_isRealTimeLogsEnabled)
                {
                    var errorAnalysisViewModel = App.GetService<ErrorAnalysisViewModel>();
                    
                    // 清空上次的日志，避免显示旧版本的日志（只在启动新游戏时清理）
                    errorAnalysisViewModel.ClearLogsOnly();
                    
                    errorAnalysisViewModel.SetLaunchCommand(_launchCommand);
                    
                    // 设置版本信息（用于导出日志时包含 version.json）
                    string minecraftPath = _fileService.GetMinecraftDataPath();
                    errorAnalysisViewModel.SetVersionInfo(SelectedVersion, minecraftPath);
                    
                    _navigationService.NavigateTo(typeof(ErrorAnalysisViewModel).FullName!);
                }
                
                // 使用 GameProcessMonitor 监控进程
                _ = _gameProcessMonitor.MonitorProcessAsync(result.GameProcess, _launchCommand);
                
                // 记录游戏启动时间和版本（用于计算游戏时长）
                _gameStartTime = DateTime.Now;
                _currentLaunchedVersion = SelectedVersion;
                _ = _versionConfigService.RecordLaunchAsync(SelectedVersion);
                
                // 检查是否为离线角色，处理离线启动计数
                if (SelectedProfile.IsOffline)
                {
                    int offlineLaunchCount = await _localSettingsService.ReadSettingAsync<int>(OfflineLaunchCountKey) + 1;
                    await _localSettingsService.SaveSettingAsync(OfflineLaunchCountKey, offlineLaunchCount);
                    
                    if (offlineLaunchCount % 10 == 0)
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                        {
                            // 等待其他 ContentDialog 关闭
                            await _dialogSemaphore.WaitAsync();
                            
                            try
                            {
                                // 如果已经有 ContentDialog 打开，等待它关闭
                                while (_isContentDialogOpen)
                                {
                                    System.Diagnostics.Debug.WriteLine("[LaunchViewModel] 离线登录弹窗等待其他 ContentDialog 关闭...");
                                    await Task.Delay(500);
                                }
                                
                                _isContentDialogOpen = true;
                                
                                var offlineDialog = new ContentDialog
                                {
                                    Title = "离线游玩提示",
                                    Content = $"您已经使用离线模式启动{offlineLaunchCount}次了,支持一下正版吧！",
                                    PrimaryButtonText = "知道了",
                                    SecondaryButtonText = "支持正版",
                                    XamlRoot = App.MainWindow.Content.XamlRoot
                                };
                                
                                var dialogResult = await offlineDialog.ShowAsync();
                                if (dialogResult == ContentDialogResult.Secondary)
                                {
                                    var uri = new Uri("https://www.minecraft.net/zh-hans/store/minecraft-java-bedrock-edition-pc");
                                    await Windows.System.Launcher.LaunchUriAsync(uri);
                                }
                            }
                            catch (COMException ex) when (ex.HResult == unchecked((int)0x80000019))
                            {
                                // 捕获 "Only a single ContentDialog can be open at any time" 异常
                                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] 离线登录弹窗 ContentDialog 冲突: {ex.Message}");
                            }
                            finally
                            {
                                _isContentDialogOpen = false;
                                _dialogSemaphore.Release();
                            }
                        });
                    }
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "用户取消了下载操作");
            LaunchStatus = "已取消下载";
            _isPreparingGame = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "游戏启动异常: {Message}", ex.Message);
            _logger.LogError("异常类型: {ExceptionType}", ex.GetType().FullName);
            _logger.LogError("堆栈跟踪: {StackTrace}", ex.StackTrace);
            
            if (ex.InnerException != null)
            {
                _logger.LogError("内部异常: {InnerMessage}", ex.InnerException.Message);
                _logger.LogError("内部异常堆栈: {InnerStackTrace}", ex.InnerException.StackTrace);
            }
            
            LaunchStatus = $"游戏启动异常: {ex.Message}";
            Console.WriteLine($"启动失败: {ex.Message}");
            Console.WriteLine($"错误堆栈: {ex.StackTrace}");
        }
        finally
        {
            _logger.LogInformation("启动流程结束，清理资源");
            IsLaunching = false;
            _downloadCancellationTokenSource?.Dispose();
            _downloadCancellationTokenSource = null;
        }
        
        _logger.LogInformation("=== 启动游戏流程结束 ===");
    }
    
    /// <summary>
    /// 构建库文件的本地路径
    /// </summary>
    private string GetLibraryFilePath(string libraryName, string librariesDirectory, string classifier = null)
    {
        // 解析库名称：groupId:artifactId:version[:classifier]
        var parts = libraryName.Split(':');
        if (parts.Length < 3)
        {
            throw new Exception($"Invalid library name format: {libraryName}");
        }

        string groupId = parts[0];
        string artifactId = parts[1];
        string version = parts[2];
        string detectedClassifier = null;
        string detectedExtension = null;
        
        // 检查版本号是否包含@符号，可能包含extension信息
        if (version.Contains('@'))
        {
            // 分割版本号和extension
            string[] versionParts = version.Split('@');
            if (versionParts.Length == 2)
            {
                version = versionParts[0];
                detectedExtension = versionParts[1];
            }
        }

        // 处理扩展名中的$extension占位符
        if (!string.IsNullOrEmpty(detectedExtension) && detectedExtension.Equals("$extension", StringComparison.OrdinalIgnoreCase))
        {
            detectedExtension = "zip"; // 默认使用zip
        }

        // 如果库名称中包含分类器（即有4个或更多部分），则提取分类器
        if (parts.Length >= 4)
        {
            detectedClassifier = parts[3];
        }

        // 优先使用方法参数传入的分类器，如果没有则使用从库名称中提取的分类器
        string finalClassifier = !string.IsNullOrEmpty(classifier) ? classifier : detectedClassifier;

        // 将groupId中的点替换为目录分隔符
        string groupPath = groupId.Replace('.', Path.DirectorySeparatorChar);

        // 构建基础文件路径
        string fileName = $"{artifactId}-{version}";
        if (!string.IsNullOrEmpty(finalClassifier))
        {
            fileName += $"-{finalClassifier}";
        }
        
        // 确定文件扩展名
        string extension = ".jar";
        bool hasExtension = false;
        
        // 特殊处理neoform文件，确保使用正确的扩展名
        if (artifactId.Equals("neoform", StringComparison.OrdinalIgnoreCase))
        {
            // 使用从版本号中提取的extension，默认为zip
            extension = detectedExtension != null ? "." + detectedExtension : ".zip";
            hasExtension = false; // 确保添加扩展名
        }
        // 特殊处理mcp_config文件，确保使用正确的zip扩展名
        else if (artifactId.Equals("mcp_config", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".zip";
            hasExtension = false; // 确保添加扩展名
        }
        // 如果从版本号中提取到了extension，使用它
        else if (detectedExtension != null)
        {
            extension = "." + detectedExtension;
            hasExtension = false; // 确保添加扩展名
        }
        // 检查文件名是否已经包含特定扩展名
        else if (fileName.EndsWith(".lzma", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".lzma";
            hasExtension = true;
        }
        else if (fileName.EndsWith(".tsrg", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".tsrg";
            hasExtension = true;
        }
        else if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".zip";
            hasExtension = true;
        }
        
        // 如果文件名已经包含扩展名，就不再添加；否则添加默认扩展名
        if (!hasExtension)
        {
            fileName += extension;
        }

        // 组合完整路径
        string libraryPath = Path.Combine(librariesDirectory, groupPath, artifactId, version, fileName);
        
        return libraryPath;
    }


    /// <summary>
    /// 显示消息对话框
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="title">对话框标题</param>
    private async Task ShowMessageAsync(string message, string title = "提示")
    {
        // 创建并显示消息对话框
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "确定",
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        await dialog.ShowAsync();
    }
    
    // 微软正版登录测试命令
    [RelayCommand]
    private async Task TestMicrosoftAuthAsync()
    {
        try
        {
            LaunchStatus = "正在测试微软登录...";
            
            // 1. 获取设备代码
            var deviceCodeResponse = await _microsoftAuthService.GetMicrosoftDeviceCodeAsync();
            if (deviceCodeResponse == null)
            {
                LaunchStatus = "登录失败: 获取设备代码失败";
                return;
            }
            
            // 2. 自动打开浏览器到验证URL
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = deviceCodeResponse.VerificationUri,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"无法自动打开浏览器，请手动访问验证URL: {deviceCodeResponse.VerificationUri}", "提示");
            }
            
            // 3. 显示8位用户代码给用户
            await ShowMessageAsync(
                $"请在浏览器中输入以下8位代码:\n\n{deviceCodeResponse.UserCode}\n\n代码有效期: {deviceCodeResponse.ExpiresIn}秒\n\n请在浏览器中完成授权，此窗口可以关闭", 
                "微软登录验证");
            
            // 4. 后台轮询完成登录
            LaunchStatus = "正在等待浏览器授权...";
            var result = await _microsoftAuthService.CompleteMicrosoftLoginAsync(
                deviceCodeResponse.DeviceCode,
                deviceCodeResponse.Interval,
                deviceCodeResponse.ExpiresIn);
            
            if (result.Success)
            {
                // 构建完整的登录信息字符串
                string fullInfo = $"登录成功！\n\n" +
                                  $"玩家名: {result.Username}\n" +
                                  $"UUID: {result.Uuid}\n\n" +
                                  $"令牌信息:\n" +
                                  $"  类型: {result.TokenType}\n" +
                                  $"  有效期: {result.ExpiresIn}秒\n" +
                                  $"  颁发时间: {result.IssueInstant}\n" +
                                  $"  过期时间: {result.NotAfter}\n\n" +
                                  $"玩家角色: {string.Join(", ", result.Roles)}\n\n" +
                                  $"皮肤数量: {result.Skins?.Length ?? 0}\n" +
                                  $"披风数量: {result.Capes?.Length ?? 0}";
                
                LaunchStatus = $"登录成功！玩家名: {result.Username}, UUID: {result.Uuid}";
                await ShowMessageAsync(fullInfo, "登录成功");
            }
            else
            {
                LaunchStatus = $"登录失败: {result.ErrorMessage}";
                await ShowMessageAsync($"登录失败: {result.ErrorMessage}", "登录失败");
            }
        }
        catch (Exception ex)
        {
            LaunchStatus = $"登录异常: {ex.Message}";
            await ShowMessageAsync($"登录异常: {ex.Message}", "登录异常");
        }
    }


    /// <summary>
    /// 构建启动参数（内部共享方法）
    /// </summary>
    /// <param name="versionName">版本名称</param>
    /// <param name="profile">角色信息</param>
    /// <returns>包含参数列表、Java路径和版本目录的元组，如果失败返回 null</returns>
    private async Task<(List<string> Args, string JavaPath, string VersionDir)?> BuildLaunchArgumentsInternalAsync(
        string versionName, 
        MinecraftProfile profile)
    {
        // TODO: 这里将包含从 LaunchGameAsync 提取的所有参数构建逻辑
        // 暂时返回 null，后续步骤会逐步填充
        return null;
    }

    /// <summary>
    /// 生成启动命令字符串（供导出使用）
    /// </summary>
    /// <param name="versionName">版本名称</param>
    /// <param name="profile">角色信息</param>
    /// <returns>包含 Java 路径、参数和版本目录的元组，如果失败返回 null</returns>
    public async Task<(string JavaPath, string Arguments, string VersionDir)?> GenerateLaunchCommandStringAsync(string versionName, MinecraftProfile profile)
    {
        if (string.IsNullOrEmpty(versionName) || profile == null)
        {
            return null;
        }
        
        try
        {
            // 使用 GameLaunchService 生成启动命令
            string fullCommand = await _gameLaunchService.GenerateLaunchCommandAsync(versionName, profile);
            
            // 解析命令，提取 Java 路径和参数
            // 命令格式: "C:\path\to\javaw.exe" arg1 arg2 ...
            int firstQuoteEnd = fullCommand.IndexOf('"', 1);
            if (firstQuoteEnd > 0)
            {
                string javaPath = fullCommand.Substring(1, firstQuoteEnd - 1);
                string arguments = fullCommand.Substring(firstQuoteEnd + 2); // +2 跳过 '" '
                
                // 获取版本目录
                var minecraftPath = _fileService.GetMinecraftDataPath();
                string versionDir = Path.Combine(minecraftPath, "versions", versionName);
                
                return (javaPath, arguments, versionDir);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"生成启动命令失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 生成并导出启动参数命令
    /// </summary>
    [RelayCommand]
    private async Task GenerateLaunchCommandAsync()
    {
        if (string.IsNullOrEmpty(SelectedVersion))
        {
            await ShowMessageAsync("请先选择一个游戏版本", "提示");
            return;
        }
        
        if (SelectedProfile == null)
        {
            await ShowMessageAsync("请先选择一个角色", "提示");
            return;
        }
        
        try
        {
            LaunchStatus = "正在生成启动参数...";
            
            // 使用 RegionValidator 检查地区限制
            var regionValidation = _regionValidator.ValidateLoginMethod(SelectedProfile);
            if (!regionValidation.IsValid)
            {
                await ShowMessageAsync(
                    regionValidation.Errors.FirstOrDefault() ?? "当前地区无法使用此登录方式",
                    "地区限制");
                return;
            }
            
            // 生成启动命令
            var result = await GenerateLaunchCommandStringAsync(SelectedVersion, SelectedProfile);
            
            if (result == null)
            {
                await ShowMessageAsync("生成启动参数失败", "错误");
                LaunchStatus = "生成启动参数失败";
                return;
            }
            
            var (javaPath, arguments, versionDir) = result.Value;
            
            // 构建完整的启动命令
            string fullCommand = $"\"{javaPath}\" {arguments}";
            
            // 生成 .bat 文件内容
            StringBuilder batContent = new StringBuilder();
            batContent.AppendLine("chcp 65001>nul");
            batContent.AppendLine("@echo off");
            batContent.AppendLine($"title 启动 - {SelectedVersion}");
            batContent.AppendLine("echo 游戏正在启动，请稍候。");
            batContent.AppendLine($"cd /D \"{versionDir}\"");
            batContent.AppendLine();
            batContent.AppendLine();
            batContent.AppendLine(fullCommand);
            batContent.AppendLine("echo 游戏已退出。");
            batContent.AppendLine("pause");
            
            // 保存到桌面
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"启动_{SelectedVersion}_{timestamp}.bat";
            string filePath = Path.Combine(desktopPath, fileName);
            
            await File.WriteAllTextAsync(filePath, batContent.ToString(), System.Text.Encoding.UTF8);
            
            LaunchStatus = $"启动参数已导出到桌面: {fileName}";
            
            // 显示成功消息
            var dialog = new ContentDialog
            {
                Title = "导出成功",
                Content = $"启动参数已成功导出到桌面:\n{fileName}\n\n您可以双击该文件来启动游戏。",
                PrimaryButtonText = "打开文件位置",
                CloseButtonText = "确定",
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            
            dialog.PrimaryButtonClick += async (sender, args) =>
            {
                // 打开文件所在文件夹并选中文件
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            };
            
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            LaunchStatus = $"生成启动参数失败: {ex.Message}";
            await ShowMessageAsync($"生成启动参数失败:\n{ex.Message}", "错误");
            System.Diagnostics.Debug.WriteLine($"生成启动参数失败: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// 获取库文件路径（供导出使用）
    /// </summary>
    private string GetLibraryPath(string libraryName, string librariesPath)
    {
        // 解析库名称: group:artifact:version[:classifier][@extension]
        string extension = "jar";
        string name = libraryName;
        
        // 处理扩展名
        if (name.Contains("@"))
        {
            var parts = name.Split('@');
            name = parts[0];
            extension = parts[1];
        }
        
        var nameParts = name.Split(':');
        if (nameParts.Length < 3) return string.Empty;
        
        string group = nameParts[0].Replace('.', Path.DirectorySeparatorChar);
        string artifact = nameParts[1];
        string version = nameParts[2];
        string classifier = nameParts.Length > 3 ? nameParts[3] : null;
        
        string fileName = classifier != null
            ? $"{artifact}-{version}-{classifier}.{extension}"
            : $"{artifact}-{version}.{extension}";
        
        return Path.Combine(librariesPath, group, artifact, version, fileName);
    }
}
