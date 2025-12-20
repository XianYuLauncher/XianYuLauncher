using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace XMCL2025.ViewModels
{
    public partial class 错误分析系统ViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _fullLog = string.Empty;

        [ObservableProperty]
        private string _crashReason = string.Empty;

        // 原始日志数据
        private string _originalLog = string.Empty;
        private string _launchCommand = string.Empty;
        private List<string> _gameOutput = new();
        private List<string> _gameError = new();

        // 设置日志数据
        public void SetLogData(string launchCommand, List<string> gameOutput, List<string> gameError)
        {
            _launchCommand = launchCommand;
            _gameOutput = new List<string>(gameOutput);
            _gameError = new List<string>(gameError);
            
            // 生成完整日志
            GenerateFullLog();
        }

        // 生成完整日志
        private void GenerateFullLog()
        {
            var allLogs = new List<string>();

            allLogs.Add("=== 游戏崩溃报告 ===");
            allLogs.Add(string.Format("崩溃时间: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));
            allLogs.Add(string.Format("启动命令: {0}", _launchCommand));
            allLogs.Add(string.Empty);

            // 分析崩溃原因
            string errorAnalysis = AnalyzeCrash(_gameOutput, _gameError);
            allLogs.Add(string.Format("崩溃分析: {0}", errorAnalysis));
            allLogs.Add(string.Empty);

            // 设置崩溃原因属性
            CrashReason = errorAnalysis;

            allLogs.Add("=== 游戏输出日志 ===");
            allLogs.AddRange(_gameOutput);
            allLogs.Add(string.Empty);

            allLogs.Add("=== 游戏错误日志 ===");
            allLogs.AddRange(_gameError);
            allLogs.Add(string.Empty);

            allLogs.Add("=== 系统信息 ===");
            allLogs.Add(string.Format("操作系统: {0}", Environment.OSVersion));
            allLogs.Add(string.Format("处理器: {0} 核心", Environment.ProcessorCount));
            allLogs.Add(string.Format("内存: {0} MB", Environment.WorkingSet / 1024 / 1024));
            allLogs.Add(string.Empty);

            allLogs.Add("请不要将此页面截图,导出崩溃日志发给专业人员以解决问题");

            FullLog = string.Join(Environment.NewLine, allLogs);
            _originalLog = FullLog; // 保存原始日志
        }

        // 分析崩溃原因
        private string AnalyzeCrash(List<string> gameOutput, List<string> gameError)
        {
            // 检查手动触发崩溃
            foreach (var line in gameOutput)
            {
                if (line.Contains("Manually triggered debug crash", StringComparison.OrdinalIgnoreCase))
                {
                    return "玩家手动触发崩溃";
                }
            }
            
            foreach (var line in gameError)
            {
                if (line.Contains("Manually triggered debug crash", StringComparison.OrdinalIgnoreCase))
                {
                    return "玩家手动触发崩溃";
                }
            }
            
            // 检查其他崩溃类型
            if (gameOutput.Any(line => line.Contains("[Fatal Error]")) || gameError.Any(line => line.Contains("[Fatal Error]")))
            {
                return "致命错误导致崩溃";
            }

            if (gameOutput.Any(line => line.Contains("[ERROR]")) || gameError.Any(line => line.Contains("[ERROR]")))
            {
                return "错误日志中存在错误信息";
            }

            if (gameOutput.Any(line => line.Contains("java.lang.Exception")) || gameError.Any(line => line.Contains("java.lang.Exception")))
            {
                return "Java异常导致崩溃";
            }

            if (gameOutput.Any(line => line.Contains("OutOfMemoryError")) || gameError.Any(line => line.Contains("OutOfMemoryError")))
            {
                return "内存不足导致崩溃";
            }

            if (gameOutput.Any(line => line.Contains("玩家崩溃")) || gameError.Any(line => line.Contains("玩家崩溃")))
            {
                return "玩家手动触发崩溃";
            }

            return "未知崩溃原因";
        }

        // 导出错误日志
        [RelayCommand]
        private async Task ExportErrorLogsAsync()
        {
            try
            {
                // 打开文件保存对话框让用户选择导出位置
                var filePicker = new Windows.Storage.Pickers.FileSavePicker();
                
                // 设置文件选择器的起始位置
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, windowHandle);
                
                // 设置文件类型
                filePicker.FileTypeChoices.Add("ZIP 压缩文件", new[] { ".zip" });
                
                // 设置默认文件名
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                filePicker.SuggestedFileName = string.Format("crash_logs_{0}", timestamp);
                
                // 显示文件选择器
                var selectedFile = await filePicker.PickSaveFileAsync();
                
                // 检查用户是否取消了选择
                if (selectedFile == null)
                {
                    return; // 用户取消了操作
                }
                
                // 创建临时目录用于存放日志文件
                string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                // 生成崩溃日志文件名
                string crashLogFile = Path.Combine(tempDir, string.Format("crash_report_{0}.txt", timestamp));

                // 写入完整日志
                File.WriteAllText(crashLogFile, FullLog);

                // 获取用户选择的zip文件路径
                string zipFilePath = selectedFile.Path;

                // 创建zip文件
                ZipFile.CreateFromDirectory(tempDir, zipFilePath);

                // 清理临时目录
                Directory.Delete(tempDir, true);

                // 显示成功提示
                var successDialog = new ContentDialog
                {
                    Title = "成功",
                    Content = string.Format("崩溃日志已成功导出到：{0}", zipFilePath),
                    PrimaryButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };

                await successDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                // 显示错误提示
                var errorDialog = new ContentDialog
                {
                    Title = "错误",
                    Content = string.Format("导出崩溃日志失败：{0}", ex.Message),
                    PrimaryButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };

                await errorDialog.ShowAsync();
            }
        }

        // 复制日志到剪贴板
        [RelayCommand]
        private void CopyLogs()
        {
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(FullLog);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("复制日志失败: " + ex.Message);
            }
        }

        // 清空日志
        [RelayCommand]
        private void ClearLogs()
        {
            FullLog = string.Empty;
        }
    }
}