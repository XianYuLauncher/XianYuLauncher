using System;
using System.Threading.Tasks;
using Serilog;
using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Core.Services
{
    /// <summary>
    /// 材质类型枚举
    /// </summary>
    public enum MaterialType
    {
        /// <summary>
        /// Mica材质
        /// </summary>
        Mica,
        /// <summary>
        /// MicaAlt材质
        /// </summary>
        MicaAlt,
        /// <summary>
        /// Acrylic材质
        /// </summary>
        Acrylic,
        /// <summary>
        /// 自定义背景图片
        /// </summary>
        CustomBackground,
        /// <summary>
        /// 动态光效 (Aurora)
        /// </summary>
        Motion
    }
    
    /// <summary>
    /// 背景设置变更事件参数
    /// </summary>
    public class BackgroundChangedEventArgs : EventArgs
    {
        public MaterialType MaterialType { get; set; }
        public string? BackgroundImagePath { get; set; }
    }

    /// <summary>
    /// 材质服务，用于处理窗口材质的加载和应用
    /// 注意：UI相关的应用方法需要在UI层实现
    /// </summary>
    public class MaterialService
    {
        private readonly ILocalSettingsService _localSettingsService;
        private const string MaterialTypeKey = "MaterialType";
        private const string BackgroundImagePathKey = "BackgroundImagePath";
        private const string MotionSpeedKey = "MotionSpeed";
        private const string MotionColorsKey = "MotionColors"; // format: "hex1;hex2;hex3;hex4;hex5"
        private const string BackgroundBlurAmountKey = "BackgroundBlurAmount";
        
        /// <summary>
        /// 背景设置变更事件
        /// </summary>
        public event EventHandler<BackgroundChangedEventArgs>? BackgroundChanged;

        /// <summary>
        /// 流光设置变更事件
        /// </summary>
        public event EventHandler? MotionSettingsChanged;
        
        /// <summary>
        /// 应用材质到窗口的委托（由UI层设置）
        /// </summary>
        public Action<object, MaterialType>? ApplyMaterialAction { get; set; }

        public MaterialService(ILocalSettingsService localSettingsService)
        {
            _localSettingsService = localSettingsService;
        }

        /// <summary>
        /// 加载材质设置
        /// </summary>
        /// <returns>材质类型</returns>
        public async Task<MaterialType> LoadMaterialTypeAsync()
        {
            var materialType = await _localSettingsService.ReadSettingAsync<MaterialType>(MaterialTypeKey);
            Log.Information("[Material] Loaded material type. Key={Key}; MaterialType={MaterialType}", MaterialTypeKey, materialType);
            return materialType;
        }

        /// <summary>
        /// 保存材质设置
        /// </summary>
        /// <param name="materialType">材质类型</param>
        public async Task SaveMaterialTypeAsync(MaterialType materialType)
        {
            Log.Information("[Material] Saving material type. Key={Key}; MaterialType={MaterialType}", MaterialTypeKey, materialType);
            await _localSettingsService.SaveSettingAsync(MaterialTypeKey, materialType);
            Log.Information("[Material] Saved material type. Key={Key}; MaterialType={MaterialType}", MaterialTypeKey, materialType);
        }
        
        /// <summary>
        /// 加载背景图片路径
        /// </summary>
        /// <returns>背景图片路径</returns>
        public async Task<string?> LoadBackgroundImagePathAsync()
        {
            return await _localSettingsService.ReadSettingAsync<string>(BackgroundImagePathKey);
        }
        
        /// <summary>
        /// 保存背景图片路径
        /// </summary>
        /// <param name="path">背景图片路径</param>
        public async Task SaveBackgroundImagePathAsync(string? path)
        {
            await _localSettingsService.SaveSettingAsync(BackgroundImagePathKey, path ?? string.Empty);
        }

        public async Task<double> LoadMotionSpeedAsync()
        {
            var val = await _localSettingsService.ReadSettingAsync<double?>(MotionSpeedKey);
            return val ?? 1.0;
        }

        public async Task SaveMotionSpeedAsync(double speed)
        {
            await _localSettingsService.SaveSettingAsync(MotionSpeedKey, speed);
            MotionSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<string[]> LoadMotionColorsAsync()
        {
            var colorsStr = await _localSettingsService.ReadSettingAsync<string>(MotionColorsKey);
            if (string.IsNullOrEmpty(colorsStr)) return Array.Empty<string>();
            return colorsStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
        }

        public async Task SaveMotionColorsAsync(string[] colors)
        {
            var colorsStr = string.Join(";", colors);
            await _localSettingsService.SaveSettingAsync(MotionColorsKey, colorsStr);
            MotionSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<double> LoadBackgroundBlurAmountAsync()
        {
            var val = await _localSettingsService.ReadSettingAsync<double?>(BackgroundBlurAmountKey);
            return val ?? 30.0; // 默认值 30.0
        }

        public async Task SaveBackgroundBlurAmountAsync(double amount)
        {
            await _localSettingsService.SaveSettingAsync(BackgroundBlurAmountKey, amount);
            BackgroundChanged?.Invoke(this, new BackgroundChangedEventArgs
            {
                MaterialType = await LoadMaterialTypeAsync(),
                BackgroundImagePath = await LoadBackgroundImagePathAsync()
            });
        }

        /// <summary>
        /// 应用材质到窗口（通过委托调用UI层实现）
        /// </summary>
        /// <param name="window">要应用材质的窗口对象</param>
        /// <param name="materialType">材质类型</param>
        public void ApplyMaterialToWindow(object? window, MaterialType materialType)
        {
            try
            {
                if (window == null)
                {
                    Log.Warning("[Material] Apply requested with null window. MaterialType={MaterialType}", materialType);
                    return;
                }

                Log.Information(
                    "[Material] Apply requested. WindowType={WindowType}; MaterialType={MaterialType}; HasApplyAction={HasApplyAction}",
                    window.GetType().FullName,
                    materialType,
                    ApplyMaterialAction is not null);

                if (ApplyMaterialAction is null)
                {
                    Log.Warning(
                        "[Material] ApplyMaterialAction is null. Material application will be skipped. WindowType={WindowType}; MaterialType={MaterialType}",
                        window.GetType().FullName,
                        materialType);
                }

                ApplyMaterialAction?.Invoke(window, materialType);

                Log.Information(
                    "[Material] Apply finished. WindowType={WindowType}; MaterialType={MaterialType}; HasApplyAction={HasApplyAction}",
                    window.GetType().FullName,
                    materialType,
                    ApplyMaterialAction is not null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Material] Apply window material failed. WindowType={WindowType}; MaterialType={MaterialType}", window?.GetType().FullName, materialType);
                Console.WriteLine($"应用窗口材质失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从设置加载并应用材质到窗口
        /// </summary>
        /// <param name="window">要应用材质的窗口对象</param>
        public async Task LoadAndApplyMaterialAsync(object? window)
        {
            Log.Information("[Material] LoadAndApply start. WindowType={WindowType}", window?.GetType().FullName);
            var materialType = await LoadMaterialTypeAsync();
            Log.Information("[Material] LoadAndApply resolved material. WindowType={WindowType}; MaterialType={MaterialType}", window?.GetType().FullName, materialType);
            ApplyMaterialToWindow(window, materialType);
            
            // 如果是自定义背景，触发事件通知 ShellPage 加载背景图片
            if (materialType == MaterialType.CustomBackground)
            {
                var backgroundPath = await LoadBackgroundImagePathAsync();
                Log.Information("[Material] LoadAndApply publishing background change. MaterialType={MaterialType}; BackgroundImagePath={BackgroundImagePath}", materialType, backgroundPath ?? string.Empty);
                OnBackgroundChanged(materialType, backgroundPath);
            }
            else
            {
                Log.Information("[Material] LoadAndApply publishing background change. MaterialType={MaterialType}; BackgroundImagePath=(none)", materialType);
                OnBackgroundChanged(materialType, null);
            }
        }
        
        /// <summary>
        /// 触发背景变更事件
        /// </summary>
        public void OnBackgroundChanged(MaterialType materialType, string? backgroundPath)
        {
            Log.Information("[Material] BackgroundChanged raised. MaterialType={MaterialType}; BackgroundImagePath={BackgroundImagePath}", materialType, backgroundPath ?? string.Empty);
            BackgroundChanged?.Invoke(this, new BackgroundChangedEventArgs
            {
                MaterialType = materialType,
                BackgroundImagePath = backgroundPath
            });
        }
    }
}
