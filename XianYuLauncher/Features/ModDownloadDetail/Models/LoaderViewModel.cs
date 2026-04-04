using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace XianYuLauncher.Features.ModDownloadDetail.Models
{
    // 加载器视图模型
    public partial class LoaderViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _loaderName;

        [ObservableProperty]
        private ObservableCollection<ModVersionViewModel> _modVersions;

        [ObservableProperty]
        private bool _isExpanded = false;

        // 父级GameVersionViewModel的引用，用于通知版本数量变化
        public GameVersionViewModel? ParentGameVersion { get; set; }

        public LoaderViewModel(string loaderName)
        {
            LoaderName = loaderName;
            ModVersions = new ObservableCollection<ModVersionViewModel>();

            // 监听ModVersions集合变化，通知父级更新总数
            ModVersions.CollectionChanged += (s, e) =>
            {
                ParentGameVersion?.NotifyTotalModVersionsCountChanged();
            };
        }

        [RelayCommand]
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }
    }
}
