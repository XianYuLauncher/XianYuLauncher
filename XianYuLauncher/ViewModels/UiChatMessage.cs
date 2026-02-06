using CommunityToolkit.Mvvm.ComponentModel;

namespace XianYuLauncher.ViewModels
{
    public partial class UiChatMessage : ObservableObject
    {
        [ObservableProperty]
        private string _role = string.Empty;

        [ObservableProperty]
        private string _content = string.Empty;

        public bool IsUser => Role == "user";
        public bool IsAssistant => Role == "assistant";
        
        public UiChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
