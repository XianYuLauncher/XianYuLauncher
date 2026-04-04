using Microsoft.UI.Xaml;

namespace XianYuLauncher.Features.ModDownloadDetail.Models
{
    /// <summary>
    /// 发布者信息模型
    /// </summary>
    public class PublisherInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;

        public Visibility AvatarVisibility =>
            (!string.IsNullOrEmpty(AvatarUrl) && !AvatarUrl.Contains("Placeholder", StringComparison.OrdinalIgnoreCase))
            ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PlaceholderVisibility =>
            AvatarVisibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;
    }
}
