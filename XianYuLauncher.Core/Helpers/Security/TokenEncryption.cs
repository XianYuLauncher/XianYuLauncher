using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// Token加密工具类
/// 使用Windows DPAPI加密，绑定到当前用户
/// </summary>
public static class TokenEncryption
{
    private const string EncryptionPrefix = "ENC:";
    
    /// <summary>
    /// 加密token
    /// </summary>
    /// <param name="plainText">明文token</param>
    /// <returns>加密后的token，格式：ENC:base64</returns>
    public static string Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText ?? string.Empty;
        }
        
        // 如果已经加密，直接返回
        if (IsEncrypted(plainText))
        {
            return plainText;
        }
        
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            string base64 = Convert.ToBase64String(encrypted);
            
            System.Diagnostics.Debug.WriteLine($"[TokenEncryption] Token已加密，长度: {plainText.Length} -> {base64.Length}");
            return EncryptionPrefix + base64;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TokenEncryption] 加密失败: {ex.Message}");
            throw new InvalidOperationException("敏感令牌加密失败，已阻止明文保存。", ex);
        }
    }
    
    /// <summary>
    /// 解密token
    /// </summary>
    /// <param name="encryptedText">加密的token</param>
    /// <returns>明文token</returns>
    public static string Decrypt(string? encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
        {
            return encryptedText ?? string.Empty;
        }
        
        // 如果不是加密格式，直接返回（可能是明文，用于兼容旧数据）
        if (!IsEncrypted(encryptedText))
        {
            System.Diagnostics.Debug.WriteLine($"[TokenEncryption] 检测到明文token，需要迁移");
            return encryptedText;
        }
        
        try
        {
            // 移除前缀
            string base64 = encryptedText.Substring(EncryptionPrefix.Length);
            byte[] data = Convert.FromBase64String(base64);
            byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            string plainText = Encoding.UTF8.GetString(decrypted);
            
            System.Diagnostics.Debug.WriteLine($"[TokenEncryption] Token已解密，长度: {base64.Length} -> {plainText.Length}");
            return plainText;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TokenEncryption] 解密失败: {ex.Message}");
            // 解密失败，可能是损坏的数据，返回空字符串
            return string.Empty;
        }
    }
    
    /// <summary>
    /// 检查token是否已加密
    /// </summary>
    /// <param name="text">要检查的文本</param>
    /// <returns>true表示已加密，false表示明文</returns>
    public static bool IsEncrypted(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        
        return text.StartsWith(EncryptionPrefix);
    }
    
    /// <summary>
    /// 批量加密token（用于迁移）
    /// </summary>
    /// <param name="tokens">token列表</param>
    /// <returns>加密后的token列表</returns>
    public static List<string> EncryptBatch(List<string> tokens)
    {
        return tokens.Select(Encrypt).ToList();
    }
    
    /// <summary>
    /// 批量解密token
    /// </summary>
    /// <param name="encryptedTokens">加密的token列表</param>
    /// <returns>明文token列表</returns>
    public static List<string> DecryptBatch(List<string> encryptedTokens)
    {
        return encryptedTokens.Select(Decrypt).ToList();
    }
    
    /// <summary>
    /// 🔒 安全读取 accounts.json（自动解密token）
    /// 这是一个临时兼容方法，建议使用AccountManager
    /// </summary>
    public static List<MinecraftAccount> LoadAccountsSecurely(string accountsFilePath)
    {
        try
        {
            var sourcePath = ResolveExistingAccountsPath(accountsFilePath);
            if (sourcePath == null)
            {
                return new List<MinecraftAccount>();
            }
            
            string json = File.ReadAllText(sourcePath);
            var profiles = JsonConvert.DeserializeObject<List<MinecraftAccount>>(json) ?? new List<MinecraftAccount>();
            
            // 自动解密所有token
            foreach (var profile in profiles)
            {
                if (!string.IsNullOrEmpty(profile.AccessToken))
                {
                    profile.AccessToken = Decrypt(profile.AccessToken);
                }
                
                if (!string.IsNullOrEmpty(profile.RefreshToken))
                {
                    profile.RefreshToken = Decrypt(profile.RefreshToken);
                }

                if (!string.IsNullOrEmpty(profile.ClientToken))
                {
                    profile.ClientToken = Decrypt(profile.ClientToken);
                }
            }
            
            return profiles;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TokenEncryption] 安全读取账户失败: {ex.Message}");
            return new List<MinecraftAccount>();
        }
    }

    private static string? ResolveExistingAccountsPath(string accountsFilePath)
    {
        if (File.Exists(accountsFilePath))
        {
            return accountsFilePath;
        }

        var directory = Path.GetDirectoryName(accountsFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var legacyProfilesPath = Path.Combine(directory, MinecraftFileConsts.LegacyProfilesJson);
        return File.Exists(legacyProfilesPath) ? legacyProfilesPath : null;
    }
}
