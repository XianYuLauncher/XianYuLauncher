using System.Threading.Tasks;

namespace XianYuLauncher.Core.Contracts.Services;

public interface ILogSanitizerService
{
    /// <summary>
    /// 对日志内容进行脱敏处理，移除敏感信息
    /// </summary>
    /// <param name="content">原始日志内容</param>
    /// <returns>脱敏后的日志内容</returns>
    Task<string> SanitizeAsync(string content);
}
