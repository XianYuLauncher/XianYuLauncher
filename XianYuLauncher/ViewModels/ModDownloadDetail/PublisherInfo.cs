using Microsoft.UI.Xaml;

namespace XianYuLauncher.ViewModels
{
    /// <summary>
    /// 发布者信息模型
    /// </summary>
    public class PublisherInfo
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string AvatarUrl { get; set; }
        public string Url { get; set; }

        public Visibility AvatarVisibility =>
            (!string.IsNullOrEmpty(AvatarUrl) && !AvatarUrl.Contains("Placeholder"))
            ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PlaceholderVisibility =>
            AvatarVisibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;
    }
}
