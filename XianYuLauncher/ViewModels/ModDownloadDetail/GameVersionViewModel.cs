using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace XianYuLauncher.ViewModels
{
    // 游戏版本视图模型
    public partial class GameVersionViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _gameVersion;

        [ObservableProperty]
        private ObservableCollection<LoaderViewModel> _loaders;

        [ObservableProperty]
        private bool _isExpanded = false;

        /// <summary>
        /// 获取该游戏版本下所有加载器的 Mod 版本总数
        /// </summary>
        public int TotalModVersionsCount => Loaders?.Sum(loader => loader.ModVersions?.Count ?? 0) ?? 0;

        public GameVersionViewModel(string gameVersion)
        {
            GameVersion = gameVersion;
            Loaders = new ObservableCollection<LoaderViewModel>();

            // 监听 Loaders 集合变化，更新总数
            Loaders.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalModVersionsCount));
        }

        /// <summary>
        /// 通知版本数量已更新（供子级LoaderViewModel调用）
        /// </summary>
        public void NotifyTotalModVersionsCountChanged()
        {
            OnPropertyChanged(nameof(TotalModVersionsCount));
        }
    }
}
