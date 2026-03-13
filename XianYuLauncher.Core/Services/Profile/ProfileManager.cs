using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 角色管理服务实现
/// </summary>
public class ProfileManager : IProfileManager
{
    private readonly IFileService _fileService;
    
    public ProfileManager(IFileService fileService)
    {
        _fileService = fileService;
    }
    
    /// <summary>
    /// 加载角色列表
    /// </summary>
    public async Task<List<MinecraftProfile>> LoadProfilesAsync()
    {
        try
        {
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var profilesPath = Path.Combine(minecraftPath, "profiles.json");
            
            if (!File.Exists(profilesPath))
            {
                System.Diagnostics.Debug.WriteLine("[ProfileManager] profiles.json 不存在，返回空列表");
                return new List<MinecraftProfile>();
            }
            
            var json = await File.ReadAllTextAsync(profilesPath);
            var profiles = JsonConvert.DeserializeObject<List<MinecraftProfile>>(json) ?? new List<MinecraftProfile>();
            
            // 🔒 安全检查：检测并迁移明文token
            bool needsMigration = await MigrateUnencryptedTokensAsync(profiles, profilesPath);
            
            // 🔓 解密所有token供内存使用
            DecryptProfileTokens(profiles);
            
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] 成功加载 {profiles.Count} 个角色{(needsMigration ? "（已自动加密明文token）" : "")}");
            return profiles;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] 加载角色失败: {ex.Message}");
            return new List<MinecraftProfile>();
        }
    }
    
    /// <summary>
    /// 保存角色列表
    /// </summary>
    public async Task SaveProfilesAsync(List<MinecraftProfile> profiles)
    {
        try
        {
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var profilesPath = Path.Combine(minecraftPath, "profiles.json");
            
            // 🔒 克隆并加密token后再保存
            var profilesToSave = EncryptProfilesForSave(profiles);
            
            var json = JsonConvert.SerializeObject(profilesToSave, Formatting.Indented);
            await File.WriteAllTextAsync(profilesPath, json);
            
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] 成功保存 {profiles.Count} 个角色（token已加密）");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] 保存角色失败: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 切换活跃角色
    /// </summary>
    public async Task<List<MinecraftProfile>> SwitchProfileAsync(
        List<MinecraftProfile> profiles,
        MinecraftProfile targetProfile)
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
        await SaveProfilesAsync(profiles);
        
        System.Diagnostics.Debug.WriteLine($"[ProfileManager] 切换到角色: {targetProfile.Name}");
        return profiles;
    }
    
    /// <summary>
    /// 获取活跃角色
    /// </summary>
    public MinecraftProfile? GetActiveProfile(List<MinecraftProfile> profiles)
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
    private async Task<bool> MigrateUnencryptedTokensAsync(List<MinecraftProfile> profiles, string profilesPath)
    {
        bool needsMigration = false;
        
        foreach (var profile in profiles)
        {
            // 检查AccessToken
            if (!string.IsNullOrEmpty(profile.AccessToken) && !TokenEncryption.IsEncrypted(profile.AccessToken))
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileManager] ⚠️ 检测到明文AccessToken: {profile.Name}");
                needsMigration = true;
            }
            
            // 检查RefreshToken
            if (!string.IsNullOrEmpty(profile.RefreshToken) && !TokenEncryption.IsEncrypted(profile.RefreshToken))
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileManager] ⚠️ 检测到明文RefreshToken: {profile.Name}");
                needsMigration = true;
            }
        }
        
        if (needsMigration)
        {
            System.Diagnostics.Debug.WriteLine("[ProfileManager] 🔒 开始自动迁移明文token...");
            
            // 加密所有明文token
            var encryptedProfiles = EncryptProfilesForSave(profiles);
            
            // 立即保存加密后的数据
            var json = JsonConvert.SerializeObject(encryptedProfiles, Formatting.Indented);
            await File.WriteAllTextAsync(profilesPath, json);
            
            System.Diagnostics.Debug.WriteLine("[ProfileManager] ✅ 明文token迁移完成，已加密保存");
        }
        
        return needsMigration;
    }
    
    /// <summary>
    /// 解密profiles中的所有token（供内存使用）
    /// </summary>
    private void DecryptProfileTokens(List<MinecraftProfile> profiles)
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
        }
    }
    
    /// <summary>
    /// 克隆并加密profiles用于保存
    /// </summary>
    private List<MinecraftProfile> EncryptProfilesForSave(List<MinecraftProfile> profiles)
    {
        return profiles.Select(p => new MinecraftProfile
        {
            Id = p.Id,
            Name = p.Name,
            AccessToken = TokenEncryption.Encrypt(p.AccessToken),
            RefreshToken = TokenEncryption.Encrypt(p.RefreshToken),
            ClientToken = p.ClientToken,
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
}
