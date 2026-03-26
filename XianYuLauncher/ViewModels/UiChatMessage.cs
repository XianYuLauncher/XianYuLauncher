using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
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

        public bool IsUser => Role == "user";
        public bool IsAssistant => Role == "assistant";
        public bool IsTool => Role == "tool";
        
        public UiChatMessage(string role, string content, bool includeInAiHistory = true)
        {
            Role = role;
            Content = content;
            IncludeInAiHistory = includeInAiHistory;
            DisplayRoleText = role;
            AiHistoryContent = content;
        }
    }
}
