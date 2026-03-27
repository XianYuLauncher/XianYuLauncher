using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.ViewModels
{
    public partial class UiChatMessage : ObservableObject
    {
        [ObservableProperty]
        private string _role = string.Empty;

        [ObservableProperty]
        private string _content = string.Empty;

        [ObservableProperty]
        private bool _includeInAiHistory = true;

        [ObservableProperty]
        private bool _showRoleHeader = true;

        [ObservableProperty]
        private string _displayRoleText = string.Empty;

        [ObservableProperty]
        private string? _aiHistoryContent;

        [ObservableProperty]
        private string? _toolCallId;

        [ObservableProperty]
        private List<ToolCallInfo>? _toolCalls;

        [ObservableProperty]
        private List<ChatImageAttachment> _imageAttachments = [];

        [ObservableProperty]
        private List<ChatImageAttachment>? _aiHistoryImageAttachments;

        public bool IsUser => Role == "user";
        public bool IsAssistant => Role == "assistant";
        public bool IsTool => Role == "tool";
        public bool HasImageAttachments => ImageAttachments.Count > 0;
        public bool ShowUserText => IsUser && !string.IsNullOrWhiteSpace(Content);
        
        public UiChatMessage(string role, string content, bool includeInAiHistory = true, IEnumerable<ChatImageAttachment>? imageAttachments = null)
        {
            Role = role;
            Content = content;
            IncludeInAiHistory = includeInAiHistory;
            DisplayRoleText = role;
            AiHistoryContent = content;
            ImageAttachments = CloneImageAttachments(imageAttachments);
            AiHistoryImageAttachments = CloneImageAttachments(ImageAttachments);
        }

        partial void OnContentChanged(string value)
        {
            OnPropertyChanged(nameof(ShowUserText));
        }

        partial void OnRoleChanged(string value)
        {
            OnPropertyChanged(nameof(IsUser));
            OnPropertyChanged(nameof(IsAssistant));
            OnPropertyChanged(nameof(IsTool));
            OnPropertyChanged(nameof(ShowUserText));
        }

        partial void OnImageAttachmentsChanged(List<ChatImageAttachment> value)
        {
            OnPropertyChanged(nameof(HasImageAttachments));
        }

        private static List<ChatImageAttachment> CloneImageAttachments(IEnumerable<ChatImageAttachment>? attachments)
        {
            return attachments?.Select(attachment => new ChatImageAttachment
            {
                FileName = attachment.FileName,
                FilePath = attachment.FilePath,
                ContentType = attachment.ContentType,
                DataUrl = attachment.DataUrl
            }).ToList() ?? [];
        }
    }
}
