using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.IO;

using XMCL2025.ViewModels;

namespace XMCL2025.Views;

// TODO: Set the URL for your privacy policy by updating SettingsPage_PrivacyTermsLink.NavigateUri in Resources.resw.
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel
    {
        get;
    }

    private int _clickCount = 0;

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    private async void VersionTextBlock_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _clickCount++;
        if (_clickCount >= 5)
        {
            try
            {
                // 获取临时目录路径
                string tempDir = Path.GetTempPath();
                string tempLogPath = Path.Combine(tempDir, $"XianYuLauncher-DebugLogs-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                
                // 创建临时日志文件
                using (StreamWriter writer = new StreamWriter(tempLogPath))
                {
                    // 写入基本信息
                    writer.WriteLine("=== XianYuLauncher Debug Logs ===");
                    writer.WriteLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"应用版本: {ViewModel.VersionDescription}");
                    writer.WriteLine($"应用目录: {AppContext.BaseDirectory}");
                    writer.WriteLine($"操作系统: {Environment.OSVersion}");
                    writer.WriteLine($".NET版本: {Environment.Version}");
                    writer.WriteLine();
                    
                    // 写入当前进程信息
                    writer.WriteLine("=== 进程信息 ===");
                    var process = System.Diagnostics.Process.GetCurrentProcess();
                    writer.WriteLine($"进程ID: {process.Id}");
                    writer.WriteLine($"内存使用: {process.WorkingSet64 / 1024 / 1024} MB");
                    writer.WriteLine();
                    
                    // 写入当前文化和区域信息
                    writer.WriteLine("=== 文化和区域信息 ===");
                    writer.WriteLine($"当前Culture: {System.Globalization.CultureInfo.CurrentCulture.Name} ({System.Globalization.CultureInfo.CurrentCulture.DisplayName})");
                    writer.WriteLine($"当前UICulture: {System.Globalization.CultureInfo.CurrentUICulture.Name} ({System.Globalization.CultureInfo.CurrentUICulture.DisplayName})");
                    try
                    {
                        var regionInfo = new System.Globalization.RegionInfo(System.Globalization.CultureInfo.CurrentCulture.Name);
                        writer.WriteLine($"当前Region: {regionInfo.Name} ({regionInfo.DisplayName})");
                        writer.WriteLine($"两字母ISO代码: {regionInfo.TwoLetterISORegionName}");
                        writer.WriteLine($"是否为中国大陆: {regionInfo.TwoLetterISORegionName == "CN"}");
                    }
                    catch (Exception ex)
                    {
                        writer.WriteLine($"区域检测失败: {ex.Message}");
                    }
                    writer.WriteLine();
                    
                    // 写入环境变量
                    writer.WriteLine("=== 关键环境变量 ===");
                    writer.WriteLine($"TEMP: {Environment.GetEnvironmentVariable("TEMP")}");
                    writer.WriteLine($"APPDATA: {Environment.GetEnvironmentVariable("APPDATA")}");
                    writer.WriteLine($"LOCALAPPDATA: {Environment.GetEnvironmentVariable("LOCALAPPDATA")}");
                    writer.WriteLine();
                    
                    // 写入文件系统信息
                    writer.WriteLine("=== 文件系统信息 ===");
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string appFolder = Path.Combine(appDataPath, "XMCL2025");
                    writer.WriteLine($"应用数据目录: {appFolder}");
                    writer.WriteLine($"目录存在: {Directory.Exists(appFolder)}");
                    
                    // 检查可能的日志位置
                    string[] possibleLogLocations = {
                        Path.Combine(AppContext.BaseDirectory, "logs"),
                        Path.Combine(appFolder, "logs"),
                        Path.Combine(AppContext.BaseDirectory)
                    };
                    
                    foreach (var logLocation in possibleLogLocations)
                    {
                        writer.WriteLine($"\n检查日志位置: {logLocation}");
                        writer.WriteLine($"位置存在: {Directory.Exists(logLocation)}");
                        if (Directory.Exists(logLocation))
                        {
                            var logFiles = Directory.GetFiles(logLocation, "*.txt")
                                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                                .Take(5);
                            
                            writer.WriteLine($"找到{logFiles.Count()}个txt文件:");
                            foreach (var logFile in logFiles)
                            {
                                var fileInfo = new FileInfo(logFile);
                                writer.WriteLine($"  {fileInfo.Name} ({fileInfo.Length} bytes, {fileInfo.LastWriteTime})");
                            }
                        }
                    }
                    
                    writer.WriteLine();
                    writer.WriteLine("=== 导出完成 ===");
                }
                
                // 显示成功消息
                var contentDialog = new ContentDialog
                {
                    Title = "调试信息导出成功",
                    Content = $"调试信息已成功导出到临时目录：\n{tempLogPath}",
                    CloseButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await contentDialog.ShowAsync();
                
                // 添加Debug输出
                System.Diagnostics.Debug.WriteLine($"[日志导出] 调试信息已保存到: {tempLogPath}");
            }
            catch (Exception ex)
            {
                // 导出过程中出现错误
                var contentDialog = new ContentDialog
                {
                    Title = "导出失败",
                    Content = $"导出调试信息时发生错误：{ex.Message}",
                    CloseButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await contentDialog.ShowAsync();
                
                // 添加Debug输出
                System.Diagnostics.Debug.WriteLine($"[日志导出] 导出失败: {ex.Message}");
            }
            finally
            {
                // 重置点击计数
                _clickCount = 0;
            }
        }
    }
}
