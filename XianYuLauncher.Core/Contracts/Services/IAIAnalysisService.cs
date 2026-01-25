using System.Collections.Generic;
using System.Threading.Tasks;

namespace XianYuLauncher.Core.Contracts.Services
{
    public interface IAIAnalysisService
    {
        Task<string> AnalyzeLogAsync(string logContent, string apiKey, string endpoint, string model, string language = "Chinese");
        IAsyncEnumerable<string> StreamAnalyzeLogAsync(string logContent, string apiKey, string endpoint, string model, string language = "Chinese");
    }
}
