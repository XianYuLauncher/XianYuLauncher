using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 角色管理服务实现
/// </summary>
public class AccountManager : IAccountManager
{
    private readonly IFileService _fileService;
    
    public AccountManager(IFileService fileService)
    {
        _fileService = fileService;
    }
    
    /// <summary>
    /// 加载角色列表
    /// </summary>
    public async Task<List<MinecraftAccount>> LoadAccountsAsync()
    {
        try
        {
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var accountsPath = Path.Combine(minecraftPath, MinecraftFileConsts.AccountsJson);
            var sourcePath = ResolveExistingAccountsPath(minecraftPath);
            
            if (sourcePath == null)
            {
                System.Diagnostics.Debug.WriteLine("[AccountManager] accounts.json / profiles.json 均不存在，返回空列表");
                return new List<MinecraftAccount>();
            }
            
            var json = await File.ReadAllTextAsync(sourcePath);
            var accounts = JsonConvert.DeserializeObject<List<MinecraftAccount>>(json) ?? new List<MinecraftAccount>();

            if (!string.Equals(sourcePath, accountsPath, StringComparison.OrdinalIgnoreCase))
            {
                sourcePath = await MigrateLegacyProfilesFileAsync(sourcePath, accountsPath, json);
            }
            
            // 🔒 安全检查：检测并迁移明文token
            bool needsMigration = await MigrateUnencryptedTokensAsync(accounts, sourcePath);
            
            // 🔓 解密所有token供内存使用
            DecryptAccountTokens(accounts);
            
            System.Diagnostics.Debug.WriteLine($"[AccountManager] 成功加载 {accounts.Count} 个角色{(needsMigration ? "（已自动加密明文token）" : "")}");
            return accounts;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountManager] 加载角色失败: {ex.Message}");
            return new List<MinecraftAccount>();
        }
    }
    
    /// <summary>
    /// 保存角色列表
    /// </summary>
    public async Task SaveAccountsAsync(List<MinecraftAccount> profiles)
    {
        try
        {
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var accountsPath = Path.Combine(minecraftPath, MinecraftFileConsts.AccountsJson);
            
            // 🔒 克隆并加密token后再保存
            var profilesToSave = EncryptAccountsForSave(profiles);
            
            var json = JsonConvert.SerializeObject(profilesToSave, Formatting.Indented);
            await File.WriteAllTextAsync(accountsPath, json);
            
            System.Diagnostics.Debug.WriteLine($"[AccountManager] 成功保存 {profiles.Count} 个角色（token已加密）");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountManager] 保存角色失败: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 切换活跃角色
    /// </summary>
    public async Task<List<MinecraftAccount>> SwitchAccountAsync(
        List<MinecraftAccount> profiles,
        MinecraftAccount targetProfile)
    {
        // 将所有角色设置为非活跃
        foreach (var profile in profiles)
        {
            profile.IsActive = false;
        }
        
        // 设置目标角色为活跃
        var target = profiles.FirstOrDefault(p => p.Id == targetProfile.Id);
        if (target != null)
        {
            target.IsActive = true;
        }
        
        // 保存更新后的角色列表
        await SaveAccountsAsync(profiles);
        
        System.Diagnostics.Debug.WriteLine($"[AccountManager] 切换到角色: {targetProfile.Name}");
        return profiles;
    }
    
    /// <summary>
    /// 获取活跃角色
    /// </summary>
    public MinecraftAccount? GetActiveAccount(List<MinecraftAccount> profiles)
    {
        if (profiles == null || profiles.Count == 0)
        {
            return null;
        }
        
        // 查找活跃角色
        var activeProfile = profiles.FirstOrDefault(p => p.IsActive);
        
        // 如果没有活跃角色，返回第一个
        return activeProfile ?? profiles.First();
    }
    
    // ========== 🔒 安全相关私有方法 ==========
    
    /// <summary>
    /// 检测并迁移明文token
    /// </summary>
    /// <returns>true表示进行了迁移</returns>
    private async Task<bool> MigrateUnencryptedTokensAsync(List<MinecraftAccount> profiles, string profilesPath)
    {
        bool needsMigration = false;
        
        foreach (var profile in profiles)
        {
            // 检查AccessToken
            if (!string.IsNullOrEmpty(profile.AccessToken) && !TokenEncryption.IsEncrypted(profile.AccessToken))
            {
                System.Diagnostics.Debug.WriteLine($"[AccountManager] ⚠️ 检测到明文AccessToken: {profile.Name}");
                needsMigration = true;
            }
            
            // 检查RefreshToken
            if (!string.IsNullOrEmpty(profile.RefreshToken) && !TokenEncryption.IsEncrypted(profile.RefreshToken))
            {
                System.Diagnostics.Debug.WriteLine($"[AccountManager] ⚠️ 检测到明文RefreshToken: {profile.Name}");
                needsMigration = true;
            }

            if (!string.IsNullOrEmpty(profile.ClientToken) && !TokenEncryption.IsEncrypted(profile.ClientToken))
            {
                System.Diagnostics.Debug.WriteLine($"[AccountManager] ⚠️ 检测到明文ClientToken: {profile.Name}");
                needsMigration = true;
            }
        }
        
        if (needsMigration)
        {
            System.Diagnostics.Debug.WriteLine("[AccountManager] 🔒 开始自动迁移明文token...");
            
            // 加密所有明文token
            var encryptedProfiles = EncryptAccountsForSave(profiles);
            
            // 立即保存加密后的数据
            var json = JsonConvert.SerializeObject(encryptedProfiles, Formatting.Indented);
            await File.WriteAllTextAsync(profilesPath, json);
            
            System.Diagnostics.Debug.WriteLine("[AccountManager] ✅ 明文token迁移完成，已加密保存");
        }
        
        return needsMigration;
    }
    
    /// <summary>
    /// 解密profiles中的所有token（供内存使用）
    /// </summary>
    private void DecryptAccountTokens(List<MinecraftAccount> profiles)
    {
        foreach (var profile in profiles)
        {
            if (!string.IsNullOrEmpty(profile.AccessToken))
            {
                profile.AccessToken = TokenEncryption.Decrypt(profile.AccessToken);
            }
            
            if (!string.IsNullOrEmpty(profile.RefreshToken))
            {
                profile.RefreshToken = TokenEncryption.Decrypt(profile.RefreshToken);
            }

            if (!string.IsNullOrEmpty(profile.ClientToken))
            {
                profile.ClientToken = TokenEncryption.Decrypt(profile.ClientToken);
            }
        }
    }
    
    /// <summary>
    /// 克隆并加密profiles用于保存
    /// </summary>
    private List<MinecraftAccount> EncryptAccountsForSave(List<MinecraftAccount> profiles)
    {
        return profiles.Select(p => new MinecraftAccount
        {
            Id = p.Id,
            Name = p.Name,
            AccessToken = TokenEncryption.Encrypt(p.AccessToken),
            RefreshToken = ShouldPersistRefreshToken(p) ? TokenEncryption.Encrypt(p.RefreshToken) : string.Empty,
            ClientToken = TokenEncryption.Encrypt(p.ClientToken),
            MicrosoftHomeAccountId = p.MicrosoftHomeAccountId,
            TokenType = p.TokenType,
            ExpiresIn = p.ExpiresIn,
            IssueInstant = p.IssueInstant,
            NotAfter = p.NotAfter,
            Roles = p.Roles,
            IsActive = p.IsActive,
            IsOffline = p.IsOffline,
            AuthServer = p.AuthServer
        }).ToList();
    }

    private static bool ShouldPersistRefreshToken(MinecraftAccount profile)
    {
        return string.Equals(profile.TokenType, "external", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveExistingAccountsPath(string minecraftPath)
    {
        var accountsPath = Path.Combine(minecraftPath, MinecraftFileConsts.AccountsJson);
        if (File.Exists(accountsPath))
        {
            return accountsPath;
        }

        var legacyProfilesPath = Path.Combine(minecraftPath, MinecraftFileConsts.LegacyProfilesJson);
        return File.Exists(legacyProfilesPath) ? legacyProfilesPath : null;
    }

    private static async Task<string> MigrateLegacyProfilesFileAsync(string legacyProfilesPath, string accountsPath, string legacyJson)
    {
        try
        {
            File.Move(legacyProfilesPath, accountsPath);
            System.Diagnostics.Debug.WriteLine("[AccountManager] 已将旧版 profiles.json 重命名为 accounts.json");
            return accountsPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountManager] 重命名旧版 profiles.json 失败，改为复制迁移: {ex.Message}");
            await File.WriteAllTextAsync(accountsPath, legacyJson);

            try
            {
                File.Delete(legacyProfilesPath);
                System.Diagnostics.Debug.WriteLine("[AccountManager] 已删除旧版 profiles.json");
            }
            catch (Exception deleteEx)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountManager] 删除旧版 profiles.json 失败: {deleteEx.Message}");
            }

            return accountsPath;
        }
    }
}
