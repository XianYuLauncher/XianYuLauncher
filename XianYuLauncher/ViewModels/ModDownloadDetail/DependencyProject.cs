using System;

namespace XianYuLauncher.ViewModels
{
    // 依赖项目类，用于存储前置Mod的详细信息
    public class DependencyProject
    {
        public string ProjectId { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TranslatedDescription { get; set; } = string.Empty;

        /// <summary>
        /// 显示的描述（优先使用翻译，如果没有则使用原始描述）
        /// 只有当前语言为中文时才返回翻译
        /// </summary>
        public string DisplayDescription
        {
            get
            {
                // 使用 TranslationService 的静态语言检查，避免跨程序集文化信息不同步
                bool isChinese = XianYuLauncher.Core.Services.TranslationService.GetCurrentLanguage().StartsWith("zh", StringComparison.OrdinalIgnoreCase);

                // 只有中文时才返回翻译，否则返回原始描述
                if (isChinese && !string.IsNullOrEmpty(TranslatedDescription))
                {
                    return TranslatedDescription;
                }

                return Description ?? string.Empty;
            }
        }
    }
}
