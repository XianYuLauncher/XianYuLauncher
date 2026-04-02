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
        private bool _includeInAIHistory = true;

        [ObservableProperty]
        private bool _showRoleHeader = true;

        [ObservableProperty]
        private string _displayRoleText = string.Empty;

        private string? _aiHistoryContent;

        public string? AIHistoryContent
        {
            get => _aiHistoryContent;
            set
            {
                if (SetProperty(ref _aiHistoryContent, value))
                {
                    OnPropertyChanged(nameof(HasToolOutputContent));
                    OnPropertyChanged(nameof(FormattedToolOutputContent));
                }
            }
        }

        [ObservableProperty]
        private string? _toolCallId;

        [ObservableProperty]
        private List<ToolCallInfo>? _toolCalls;

        private string? _toolInputContent;

        public string? ToolInputContent
        {
            get => _toolInputContent;
            set
            {
                if (SetProperty(ref _toolInputContent, value))
                {
                    OnPropertyChanged(nameof(HasToolInputContent));
                    OnPropertyChanged(nameof(FormattedToolInputContent));
                }
            }
        }

        private string? _toolOutputContent;

        public string? ToolOutputContent
        {
            get => _toolOutputContent;
            set
            {
                if (SetProperty(ref _toolOutputContent, value))
                {
                    OnPropertyChanged(nameof(HasToolOutputContent));
                    OnPropertyChanged(nameof(FormattedToolOutputContent));
                }
            }
        }

        [ObservableProperty]
        private List<ChatImageAttachment> _imageAttachments = [];

        private List<ChatImageAttachment>? _aiHistoryImageAttachments;

        public List<ChatImageAttachment>? AIHistoryImageAttachments
        {
            get => _aiHistoryImageAttachments;
            set => SetProperty(ref _aiHistoryImageAttachments, value);
        }

        [ObservableProperty]
        private bool _suppressContentRendering;

        public bool IsUser => Role == "user";
        public bool IsAssistant => Role == "assistant";
        public bool IsTool => Role == "tool";
        public bool HasImageAttachments => ImageAttachments.Count > 0;
        public bool ShowUserText => IsUser && !string.IsNullOrWhiteSpace(Content);
        public bool ShowAssistantText => IsAssistant && !SuppressContentRendering && !string.IsNullOrWhiteSpace(Content);
        public bool HasToolInputContent => !string.IsNullOrWhiteSpace(ToolInputContent);
        public bool HasToolOutputContent => !string.IsNullOrWhiteSpace(GetToolOutputSource());
        public string FormattedToolInputContent => ToolInputContent ?? string.Empty;
        public string FormattedToolOutputContent => GetToolOutputSource() ?? "等待工具返回...";
        public bool ShouldShowMessageContainer => ShowUserText || HasImageAttachments || ShowAssistantText || IsTool;
        
        public UiChatMessage(string role, string content, bool includeInAIHistory = true, IEnumerable<ChatImageAttachment>? imageAttachments = null)
        {
            Role = role;
            Content = content;
            IncludeInAIHistory = includeInAIHistory;
            DisplayRoleText = role;
            AIHistoryContent = content;
            ImageAttachments = CloneImageAttachments(imageAttachments);
            AIHistoryImageAttachments = CloneImageAttachments(ImageAttachments);
        }

        partial void OnContentChanged(string value)
        {
            OnPropertyChanged(nameof(ShowUserText));
            OnPropertyChanged(nameof(ShowAssistantText));
            OnPropertyChanged(nameof(ShouldShowMessageContainer));
        }

        partial void OnRoleChanged(string value)
        {
            OnPropertyChanged(nameof(IsUser));
            OnPropertyChanged(nameof(IsAssistant));
            OnPropertyChanged(nameof(IsTool));
            OnPropertyChanged(nameof(ShowUserText));
            OnPropertyChanged(nameof(ShowAssistantText));
            OnPropertyChanged(nameof(HasToolOutputContent));
            OnPropertyChanged(nameof(FormattedToolOutputContent));
            OnPropertyChanged(nameof(ShouldShowMessageContainer));
        }

        partial void OnImageAttachmentsChanged(List<ChatImageAttachment> value)
        {
            OnPropertyChanged(nameof(HasImageAttachments));
            OnPropertyChanged(nameof(ShouldShowMessageContainer));
        }

        partial void OnSuppressContentRenderingChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowAssistantText));
            OnPropertyChanged(nameof(ShouldShowMessageContainer));
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

        private string? GetToolOutputSource()
        {
            if (!string.IsNullOrWhiteSpace(ToolOutputContent))
            {
                return ToolOutputContent;
            }

            return string.IsNullOrWhiteSpace(AIHistoryContent) ? null : AIHistoryContent;
        }
    }
}
