using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        CustomBackground
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
    /// </summary>
    public class MaterialService
    {
        private readonly ILocalSettingsService _localSettingsService;
        private const string MaterialTypeKey = "MaterialType";
        private const string BackgroundImagePathKey = "BackgroundImagePath";
        
        /// <summary>
        /// 背景设置变更事件
        /// </summary>
        public event EventHandler<BackgroundChangedEventArgs>? BackgroundChanged;

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
            return await _localSettingsService.ReadSettingAsync<MaterialType>(MaterialTypeKey);
        }

        /// <summary>
        /// 保存材质设置
        /// </summary>
        /// <param name="materialType">材质类型</param>
        public async Task SaveMaterialTypeAsync(MaterialType materialType)
        {
            await _localSettingsService.SaveSettingAsync(MaterialTypeKey, materialType);
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

        /// <summary>
        /// 应用材质到窗口
        /// </summary>
        /// <param name="window">要应用材质的窗口</param>
        /// <param name="materialType">材质类型</param>
        public void ApplyMaterialToWindow(Window window, MaterialType materialType)
        {
            try
            {
                if (window == null) return;

                switch (materialType)
                {
                    case MaterialType.Mica:
                        // 设置Mica Base材质
                        window.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop()
                        {
                            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
                        };
                        break;
                    case MaterialType.MicaAlt:
                        // 设置Mica Alt材质
                        window.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop()
                        {
                            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
                        };
                        break;
                    case MaterialType.Acrylic:
                        // 设置Acrylic材质
                        window.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
                        break;
                    case MaterialType.CustomBackground:
                        // 自定义背景时，移除系统材质，使用纯色背景
                        window.SystemBackdrop = null;
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用窗口材质失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从设置加载并应用材质到窗口
        /// </summary>
        /// <param name="window">要应用材质的窗口</param>
        public async Task LoadAndApplyMaterialAsync(Window window)
        {
            var materialType = await LoadMaterialTypeAsync();
            ApplyMaterialToWindow(window, materialType);
            
            // 如果是自定义背景，触发事件通知 ShellPage 加载背景图片
            if (materialType == MaterialType.CustomBackground)
            {
                var backgroundPath = await LoadBackgroundImagePathAsync();
                OnBackgroundChanged(materialType, backgroundPath);
            }
            else
            {
                OnBackgroundChanged(materialType, null);
            }
        }
        
        /// <summary>
        /// 触发背景变更事件
        /// </summary>
        public void OnBackgroundChanged(MaterialType materialType, string? backgroundPath)
        {
            BackgroundChanged?.Invoke(this, new BackgroundChangedEventArgs
            {
                MaterialType = materialType,
                BackgroundImagePath = backgroundPath
            });
        }
    }
}