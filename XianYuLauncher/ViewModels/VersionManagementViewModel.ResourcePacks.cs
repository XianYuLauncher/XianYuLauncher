using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Helpers;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Models.VersionManagement;


namespace XianYuLauncher.ViewModels;

public partial class VersionManagementViewModel
{
    #region 资源包管理

    /// <summary>
        /// 仅加载资源包列表，不加载图标
        /// </summary>
        private async Task LoadResourcePacksListOnlyAsync()
        {
            if (SelectedVersion == null)
            {
                return;
            }

            var resourcePacksPath = GetVersionSpecificPath("resourcepacks");
            if (Directory.Exists(resourcePacksPath))
            {
                // 获取所有资源包文件夹和zip文件
                var resourcePackFolders = Directory.GetDirectories(resourcePacksPath);
                var resourcePackZips = Directory.GetFiles(resourcePacksPath, "*.zip");
                
                // 创建新的资源包列表，减少CollectionChanged事件触发次数
                var newResourcePacks = new ObservableCollection<ResourcePackInfo>();
                
                // 添加所有资源包文件夹
                foreach (var resourcePackFolder in resourcePackFolders)
                {
                    var resourcePackInfo = new ResourcePackInfo(resourcePackFolder);
                    // 先设置默认图标为空，后续异步加载
                    resourcePackInfo.Icon = null;
                    newResourcePacks.Add(resourcePackInfo);
                }
                
                // 添加所有资源包zip文件
                foreach (var resourcePackZip in resourcePackZips)
                {
                    var resourcePackInfo = new ResourcePackInfo(resourcePackZip);
                    // 先设置默认图标为空，后续异步加载
                    resourcePackInfo.Icon = null;
                    newResourcePacks.Add(resourcePackInfo);
                }
                
                // 立即显示资源包列表，不等待图标加载完成
                ResourcePacks = newResourcePacks;
            }
            else
            {
                // 清空资源包列表
                ResourcePacks.Clear();
            }
        }
        
        /// <summary>
        /// 加载资源包列表（不加载图标，图标延迟到 Tab 切换时加载）
        /// </summary>
        private async Task LoadResourcePacksAsync()
        {
            await LoadResourcePacksListOnlyAsync();
            // 图标加载已移到 OnSelectedTabIndexChanged 中延迟执行
        }
        
        /// <summary>
        /// 加载资源包预览图
        // 当实现资源包画廊功能时，可以在这里添加预览图加载逻辑
        // 建议实现方式：
        // 1. 创建专门的 ResourcePackGalleryPage
        // 2. 点击资源包时导航到画廊页面
        // 3. 在画廊页面中展示所有纹理贴图
        // 4. 支持搜索、筛选、导出等功能
        
        /// <summary>
        /// 打开资源包文件夹命令
        /// </summary>
    [RelayCommand]
    private async Task OpenResourcePackFolderAsync()
    {
        await OpenFolderByTypeAsync("resourcepacks");
    }
    
    /// <summary>
    /// 删除资源包命令
    /// </summary>
    /// <param name="resourcePack">要删除的资源包</param>
    [RelayCommand]
    private async Task DeleteResourcePackAsync(ResourcePackInfo resourcePack)
    {
        if (resourcePack == null)
        {
            return;
        }
        
        try
        {
            // 删除资源包（文件夹或文件）
            if (Directory.Exists(resourcePack.FilePath))
            {
                Directory.Delete(resourcePack.FilePath, true);
            }
            else if (File.Exists(resourcePack.FilePath))
            {
                File.Delete(resourcePack.FilePath);
            }
            
            // 从列表中移除
            ResourcePacks.Remove(resourcePack);
            
            StatusMessage = $"已删除资源包: {resourcePack.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除资源包失败：{ex.Message}";
        }
    }

    #endregion
}
