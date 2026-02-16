using System.Collections.Generic;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services
{
    public interface IAIAnalysisService
    {
        /// <summary>
        /// 带原生 function calling 的流式聊天
        /// </summary>
        IAsyncEnumerable<AiStreamChunk> StreamChatWithToolsAsync(
            IEnumerable<ChatMessage> messages,
            IEnumerable<AiToolDefinition> tools,
            string apiKey,
            string endpoint,
            string model);
    }
}
