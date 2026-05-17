using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public class LogSanitizerService : ILogSanitizerService
{
    private readonly IAccountManager _accountManager;

    public LogSanitizerService(IAccountManager accountManager)
    {
        _accountManager = accountManager;
    }

    public async Task<string> SanitizeAsync(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        var sanitizedContent = SensitiveDataSanitizer.Sanitize(content);

        try
        {
            // 加载所有角色配置
            var profiles = await _accountManager.LoadAccountsAsync();
            var activeProfile = _accountManager.GetActiveAccount(profiles);

            if (activeProfile == null)
            {
                return sanitizedContent;
            }

            // 收集敏感信息列表
            var sensitiveWords = new HashSet<string>();

            // 1. 用户名 - 根据需求跳过脱敏
            // 如果用户名包含特殊字符导致崩溃，脱敏后开发者就无法定位问题了，所以保留用户名。
            // if (!string.IsNullOrEmpty(activeProfile.Name))
            // {
            //     sensitiveWords.Add(activeProfile.Name);
            // }

            // 2. UUID
            if (!string.IsNullOrEmpty(activeProfile.Id))
            {
                sensitiveWords.Add(activeProfile.Id);
            }

            // 3. AccessToken (已由 AccountManager.LoadAccountsAsync() 解密)
            if (!string.IsNullOrEmpty(activeProfile.AccessToken))
            {
                sensitiveWords.Add(activeProfile.AccessToken);
            }

            // 4. RefreshToken (已由 AccountManager.LoadAccountsAsync() 解密)
            if (!string.IsNullOrEmpty(activeProfile.RefreshToken))
            {
                sensitiveWords.Add(activeProfile.RefreshToken);
            }

            if (!string.IsNullOrEmpty(activeProfile.ClientToken))
            {
                sensitiveWords.Add(activeProfile.ClientToken);
            }

            // 执行替换
            var sb = new StringBuilder(sanitizedContent);
            foreach (var word in sensitiveWords)
            {
                // 确保关键词只含有敏感信息，不包含常见短词以避免误伤（虽然后台token一般很长）
                if (word.Length < 3) continue;

                sb.Replace(word, "[REDACTED]");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LogSanitizer] 脱敏过程出错: {ex.Message}");
            return sanitizedContent;
        }
    }
}
