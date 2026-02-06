namespace XianYuLauncher.Core.Models
{
    public class ChatMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
