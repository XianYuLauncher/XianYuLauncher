using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.ViewModels
{
    // Mod版本视图模型
    public partial class ModVersionViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _versionNumber = string.Empty;

        [ObservableProperty]
        private string _releaseDate = string.Empty;

        [ObservableProperty]
        private string _changelog = string.Empty;

        [ObservableProperty]
        private string _downloadUrl = string.Empty;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private List<string> _loaders = new();

        [ObservableProperty]
        private string _versionType = string.Empty;

        // 添加游戏版本属性，用于记录该Mod版本支持的游戏版本
        [ObservableProperty]
        private string _gameVersion = string.Empty;

        // 图标URL属性
        [ObservableProperty]
        private string _iconUrl = string.Empty;

        // 资源类型标签（用于非Mod资源，如 IRIS、OPTIFINE、MINECRAFT、DATAPACK）
        [ObservableProperty]
        private string? _resourceTypeTag;

        // Modrinth原始版本信息，用于获取依赖项
        public ModrinthVersion? OriginalVersion { get; set; }

        // CurseForge原始文件信息，用于获取依赖项
        public CurseForgeFile? OriginalCurseForgeFile { get; set; }

        // 是否来自CurseForge
        public bool IsCurseForge => OriginalCurseForgeFile is not null;
    }
}
