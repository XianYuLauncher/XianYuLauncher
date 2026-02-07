using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 启动设置解析器，负责合并全局设置和版本设置，输出最终生效值
/// </summary>
public class LaunchSettingsResolver : ILaunchSettingsResolver
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IJavaRuntimeService _javaRuntimeService;

    // 全局设置 Key 常量
    private const string GlobalAutoMemoryKey = "GlobalAutoMemoryAllocation";
    private const string GlobalInitialHeapKey = "GlobalInitialHeapMemory";
    private const string GlobalMaxHeapKey = "GlobalMaximumHeapMemory";
    private const string GlobalWindowWidthKey = "GlobalWindowWidth";
    private const string GlobalWindowHeightKey = "GlobalWindowHeight";

    public LaunchSettingsResolver(
        ILocalSettingsService localSettingsService,
        IJavaRuntimeService javaRuntimeService)
    {
        _localSettingsService = localSettingsService;
        _javaRuntimeService = javaRuntimeService;
    }

    /// <inheritdoc/>
    public async Task<EffectiveLaunchSettings> ResolveAsync(VersionConfig versionConfig, int requiredJavaVersion = 8)
    {
        var result = new EffectiveLaunchSettings();

        // === 内存设置 ===
        if (versionConfig.OverrideMemory)
        {
            // 版本自定义
            result.AutoMemoryAllocation = versionConfig.AutoMemoryAllocation;
            result.InitialHeapMemory = versionConfig.InitialHeapMemory;
            result.MaximumHeapMemory = versionConfig.MaximumHeapMemory;
        }
        else
        {
            // 跟随全局
            result.AutoMemoryAllocation = await _localSettingsService.ReadSettingAsync<bool?>(GlobalAutoMemoryKey) ?? true;
            result.InitialHeapMemory = await _localSettingsService.ReadSettingAsync<double?>(GlobalInitialHeapKey) ?? 6.0;
            result.MaximumHeapMemory = await _localSettingsService.ReadSettingAsync<double?>(GlobalMaxHeapKey) ?? 12.0;
        }

        // === Java 设置 ===
        if (!versionConfig.UseGlobalJavaSetting && !string.IsNullOrEmpty(versionConfig.JavaPath))
        {
            // 版本自定义 Java
            result.JavaPath = versionConfig.JavaPath;
        }
        else
        {
            // 跟随全局：自动选择最佳 Java
            result.JavaPath = await _javaRuntimeService.SelectBestJavaAsync(requiredJavaVersion, versionConfig.JavaPath) ?? string.Empty;
        }

        // === 分辨率设置 ===
        if (versionConfig.OverrideResolution)
        {
            // 版本自定义
            result.WindowWidth = versionConfig.WindowWidth;
            result.WindowHeight = versionConfig.WindowHeight;
        }
        else
        {
            // 跟随全局
            result.WindowWidth = await _localSettingsService.ReadSettingAsync<int?>(GlobalWindowWidthKey) ?? 1280;
            result.WindowHeight = await _localSettingsService.ReadSettingAsync<int?>(GlobalWindowHeightKey) ?? 720;
        }

        return result;
    }
}
