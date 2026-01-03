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
    public partial class ErrorAnalysisViewModel : ObservableObject
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
        
        // 节流机制相关字段
        private DateTime _lastLogUpdateTime = DateTime.MinValue;
        private const int LogUpdateIntervalMs = 100; // 日志更新间隔，单位毫秒
        private bool _isUpdateScheduled = false;
        
        // 日志限制相关字段
        private const int MaxLogLines = 10000; // 最大日志行数限制，避免内存占用过大
        private const int LogTrimAmount = 2000; // 超过限制时，每次删除的行数

        // 设置日志数据
    public void SetLogData(string launchCommand, List<string> gameOutput, List<string> gameError)
    {
        System.Diagnostics.Debug.WriteLine($"ErrorAnalysisViewModel: 设置日志数据，输出日志行数: {gameOutput.Count}，错误日志行数: {gameError.Count}");
        _launchCommand = launchCommand;
        _gameOutput = new List<string>(gameOutput);
        _gameError = new List<string>(gameError);
        
        // 生成完整日志
        GenerateFullLog();
    }
    
    /// <summary>
    /// 实时添加游戏输出日志
    /// </summary>
    /// <param name="logLine">日志行</param>
    public void AddGameOutputLog(string logLine)
    {
        System.Diagnostics.Debug.WriteLine($"ErrorAnalysisViewModel: 添加游戏输出日志: {logLine}");
        
        // 使用锁确保线程安全
        lock (_gameOutput)
        {
            _gameOutput.Add(logLine);
            // 限制日志行数，避免内存占用过大
            if (_gameOutput.Count > MaxLogLines)
            {
                _gameOutput.RemoveRange(0, LogTrimAmount);
            }
        }
        
        ScheduleLogUpdate();
    }
    
    /// <summary>
    /// 实时添加游戏错误日志
    /// </summary>
    /// <param name="logLine">日志行</param>
    public void AddGameErrorLog(string logLine)
    {
        System.Diagnostics.Debug.WriteLine($"ErrorAnalysisViewModel: 添加游戏错误日志: {logLine}");
        
        // 使用锁确保线程安全
        lock (_gameError)
        {
            _gameError.Add(logLine);
            // 限制日志行数，避免内存占用过大
            if (_gameError.Count > MaxLogLines)
            {
                _gameError.RemoveRange(0, LogTrimAmount);
            }
        }
        
        ScheduleLogUpdate();
    }
    
    /// <summary>
    /// 调度日志更新，添加节流机制
    /// </summary>
    private void ScheduleLogUpdate()
    {
        var now = DateTime.Now;
        var timeSinceLastUpdate = now - _lastLogUpdateTime;
        
        // 如果距离上次更新已经超过间隔时间，立即更新
        if (timeSinceLastUpdate.TotalMilliseconds >= LogUpdateIntervalMs)
        {
            _lastLogUpdateTime = now;
            GenerateFullLog();
        }
        // 否则，只调度一次更新
        else if (!_isUpdateScheduled)
        {
            _isUpdateScheduled = true;
            var delay = LogUpdateIntervalMs - (int)timeSinceLastUpdate.TotalMilliseconds;
            
            // 使用Task.Delay实现延迟更新，避免阻塞主线程
            Task.Delay(delay).ContinueWith(_ =>
            {
                if (_isUpdateScheduled)
                {
                    _lastLogUpdateTime = DateTime.Now;
                    _isUpdateScheduled = false;
                    GenerateFullLog();
                }
            });
        }
    }

        // 生成完整日志
        private void GenerateFullLog()
        {
            // 简化日志生成，只保留必要的日志内容
            var sb = new StringBuilder();

            sb.AppendLine("=== 实时游戏日志 ===");
            sb.AppendLine(string.Format("日志开始时间: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));
            sb.AppendLine();
            
            // 准备分析和生成日志所需的列表
            List<string> outputList = new();
            List<string> errorList = new();
            
            // 使用锁保护日志列表，避免并发修改异常
            lock (_gameOutput)
            {
                outputList.AddRange(_gameOutput);
            }
            
            lock (_gameError)
            {
                errorList.AddRange(_gameError);
            }
            
            if (outputList.Count == 0 && errorList.Count == 0)
            {
                sb.AppendLine("等待游戏输出...");
            }
            else
            {
                // 总是分析崩溃原因，确保完整性
                string errorAnalysis = AnalyzeCrash(outputList, errorList);
                sb.AppendLine(string.Format("崩溃分析: {0}", errorAnalysis));
                sb.AppendLine();

                // 设置崩溃原因属性
                // 确保在UI线程上更新属性
                if (App.MainWindow.DispatcherQueue.HasThreadAccess)
                {
                    CrashReason = errorAnalysis;
                }
                else
                {
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        CrashReason = errorAnalysis;
                    });
                }
            }

            sb.AppendLine("=== 游戏输出日志 ===");
            foreach (var line in outputList)
            {
                sb.AppendLine(line);
            }
            sb.AppendLine();

            sb.AppendLine("=== 游戏错误日志 ===");
            foreach (var line in errorList)
            {
                sb.AppendLine(line);
            }
            sb.AppendLine();

            // 移除系统信息，减少生成时间
            sb.AppendLine("实时日志持续更新中...");

            // 限制最终日志的大小，避免内存占用过大
            string finalLog = sb.ToString();
            _originalLog = finalLog; // 保存原始日志

            // 确保在UI线程上更新FullLog属性
            if (App.MainWindow.DispatcherQueue.HasThreadAccess)
            {
                FullLog = finalLog;
            }
            else
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    FullLog = finalLog;
                });
            }
        }

        // 分析崩溃原因
        private string AnalyzeCrash(List<string> gameOutput, List<string> gameError)
        {
            // 优化分析逻辑，先检查错误日志，再检查输出日志
            // 并且使用更高效的方式检查关键词
            
            // 检查手动触发崩溃
            if (ContainsKeyword(gameError, "Manually triggered debug crash") || ContainsKeyword(gameOutput, "Manually triggered debug crash"))
            {
                return "玩家手动触发崩溃";
            }
            
            // 检查致命错误
            if (ContainsKeyword(gameError, "[Fatal Error]") || ContainsKeyword(gameOutput, "[Fatal Error]"))
            {
                return "致命错误导致崩溃";
            }
            
            // 检查Java异常
            if (ContainsKeyword(gameError, "java.lang.Exception") || ContainsKeyword(gameOutput, "java.lang.Exception"))
            {
                return "Java异常导致崩溃";
            }
            
            // 检查内存不足
            if (ContainsKeyword(gameError, "OutOfMemoryError") || ContainsKeyword(gameOutput, "OutOfMemoryError"))
            {
                return "内存不足导致崩溃";
            }
            
            // 检查玩家崩溃
            if (ContainsKeyword(gameError, "玩家崩溃") || ContainsKeyword(gameOutput, "玩家崩溃"))
            {
                return "玩家手动触发崩溃";
            }
            
            // 检查普通错误
            if (ContainsKeyword(gameError, "[ERROR]") || ContainsKeyword(gameOutput, "[ERROR]"))
            {
                return "错误日志中存在错误信息";
            }

            return "未知崩溃原因";
        }
        
        /// <summary>
        /// 高效检查列表中是否包含关键词
        /// </summary>
        /// <param name="lines">日志行列表</param>
        /// <param name="keyword">关键词</param>
        /// <returns>是否包含关键词</returns>
        private bool ContainsKeyword(List<string> lines, string keyword)
        {
            // 使用高效的循环方式，避免LINQ的额外开销
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
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

                // 生成启动参数.bat文件
                string batFilePath = Path.Combine(tempDir, "启动参数.bat");
                File.WriteAllText(batFilePath, _launchCommand);

                // 生成崩溃日志文件名
                string crashLogFile = Path.Combine(tempDir, string.Format("crash_report_{0}.txt", timestamp));

                // 写入完整日志（不包含启动参数，已单独保存为.bat文件）
                File.WriteAllText(crashLogFile, FullLog);

                // 获取用户选择的zip文件路径
                string zipFilePath = selectedFile.Path;

                // 如果文件已存在，先删除
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }

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