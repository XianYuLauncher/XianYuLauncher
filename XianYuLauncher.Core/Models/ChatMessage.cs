namespace XianYuLauncher.Core.Models
{
    public class ChatMessage
    {
        public string Role { get; set; } = "user";
        public string? Content { get; set; } = string.Empty;

        /// <summary>
        /// 当 Role == "assistant" 且模型请求调用工具时，包含工具调用列表
        /// </summary>
        public List<ToolCallInfo>? ToolCalls { get; set; }

        /// <summary>
        /// 当 Role == "tool" 时，对应的 tool_call_id
        /// </summary>
        public string? ToolCallId { get; set; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }

        public ChatMessage(string role, string? content, List<ToolCallInfo>? toolCalls)
        {
            Role = role;
            Content = content;
            ToolCalls = toolCalls;
        }

        /// <summary>
        /// 创建 tool 角色的回复消息
        /// </summary>
        public static ChatMessage ToolResult(string toolCallId, string content)
        {
            return new ChatMessage("tool", content) { ToolCallId = toolCallId };
        }
    }

    /// <summary>
    /// 表示一次工具调用请求
    /// </summary>
    public class ToolCallInfo
    {
        public string Id { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }

    /// <summary>
    /// AI 流式响应的单个片段，可能是文本或工具调用
    /// </summary>
    public class AiStreamChunk
    {
        /// <summary>
        /// 文本内容增量（普通回复）
        /// </summary>
        public string? ContentDelta { get; set; }

        /// <summary>
        /// 当流结束且包含工具调用时，填充此列表
        /// </summary>
        public List<ToolCallInfo>? ToolCalls { get; set; }

        /// <summary>
        /// 是否为流结束标记
        /// </summary>
        public bool IsDone { get; set; }

        public bool IsContent => !string.IsNullOrEmpty(ContentDelta);
        public bool IsToolCall => ToolCalls != null && ToolCalls.Count > 0;
    }
}
