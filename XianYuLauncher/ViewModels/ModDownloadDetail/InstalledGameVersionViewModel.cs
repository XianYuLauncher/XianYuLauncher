using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace XianYuLauncher.ViewModels
{
    // 已安装游戏版本ViewModel
    public partial class InstalledGameVersionViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _gameVersion;

        [ObservableProperty]
        private string _loaderType;

        [ObservableProperty]
        private string _loaderVersion;

        [ObservableProperty]
        private bool _isCompatible;

        [ObservableProperty]
        private string _originalVersionName;

        /// <summary>
        /// 所有加载器列表（包括主加载器和附加加载器如 OptiFine、LiteLoader）
        /// </summary>
        [ObservableProperty]
        private List<string> _allLoaders = new();

        public string DisplayName => $"{OriginalVersionName}";
    }
}
