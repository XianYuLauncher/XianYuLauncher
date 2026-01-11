using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

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
            
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] 成功加载 {profiles.Count} 个角色");
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
            
            var json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
            await File.WriteAllTextAsync(profilesPath, json);
            
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] 成功保存 {profiles.Count} 个角色");
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
}
