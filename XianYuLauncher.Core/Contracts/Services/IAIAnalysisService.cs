using System.Collections.Generic;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services
{
    public interface IAIAnalysisService
    {
        Task<string> AnalyzeLogAsync(string logContent, string apiKey, string endpoint, string model, string language = "Chinese");
        IAsyncEnumerable<string> StreamAnalyzeLogAsync(string logContent, string apiKey, string endpoint, string model, string language = "Chinese");
        IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, string apiKey, string endpoint, string model);

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
