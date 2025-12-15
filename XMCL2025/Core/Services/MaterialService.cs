using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XMCL2025.Contracts.Services;

namespace XMCL2025.Core.Services
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
        Acrylic
    }

    /// <summary>
    /// 材质服务，用于处理窗口材质的加载和应用
    /// </summary>
    public class MaterialService
    {
        private readonly ILocalSettingsService _localSettingsService;
        private const string MaterialTypeKey = "MaterialType";

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
                    case MaterialType.MicaAlt:
                        // 设置Mica材质
                        window.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
                        break;
                    case MaterialType.Acrylic:
                        // 设置Acrylic材质
                        window.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
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
        }
    }
}