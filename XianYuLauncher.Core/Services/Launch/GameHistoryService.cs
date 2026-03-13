using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 游戏历史记录服务实现
/// </summary>
public class GameHistoryService : IGameHistoryService
{
    private readonly IFileService _fileService;
    private readonly string _historyFilePath;
    private List<GameLaunchHistory> _historyCache;
    private readonly Lock _lock = new();
    
    public GameHistoryService(IFileService fileService)
    {
        _fileService = fileService;
        _historyFilePath = Path.Combine(_fileService.GetMinecraftDataPath(), "launch_history.json");
        _historyCache = new List<GameLaunchHistory>();
        
        // 初始化时加载历史记录
        _ = LoadHistoryAsync();
    }
    
    /// <summary>
    /// 加载历史记录
    /// </summary>
    private async Task LoadHistoryAsync()
    {
        try
        {
            if (File.Exists(_historyFilePath))
            {
                string json = await File.ReadAllTextAsync(_historyFilePath);
                var history = JsonConvert.DeserializeObject<List<GameLaunchHistory>>(json);
                
                lock (_lock)
                {
                    _historyCache = history ?? new List<GameLaunchHistory>();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameHistoryService] 加载历史记录失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 保存历史记录
    /// </summary>
    private async Task SaveHistoryAsync()
    {
        try
        {
            List<GameLaunchHistory> historyToSave;
            lock (_lock)
            {
                historyToSave = new List<GameLaunchHistory>(_historyCache);
            }
            
            string json = JsonConvert.SerializeObject(historyToSave, Formatting.Indented);
            await File.WriteAllTextAsync(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameHistoryService] 保存历史记录失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 记录游戏启动
    /// </summary>
    public async Task RecordLaunchAsync(string versionName, string profileName)
    {
        if (string.IsNullOrEmpty(versionName))
            return;
        
        lock (_lock)
        {
            var existing = _historyCache.FirstOrDefault(h => h.VersionName == versionName);
            
            if (existing != null)
            {
                existing.LastLaunchTime = DateTime.Now;
                existing.LaunchCount++;
                existing.ProfileName = profileName;
            }
            else
            {
                _historyCache.Add(new GameLaunchHistory
                {
                    VersionName = versionName,
                    ProfileName = profileName,
                    LastLaunchTime = DateTime.Now,
                    LaunchCount = 1,
                    TotalPlayTimeSeconds = 0,
                    CrashCount = 0
                });
            }
        }
        
        await SaveHistoryAsync();
    }
    
    /// <summary>
    /// 记录游戏退出
    /// </summary>
    public async Task RecordExitAsync(string versionName, int playTimeSeconds, bool isCrash)
    {
        if (string.IsNullOrEmpty(versionName))
            return;
        
        lock (_lock)
        {
            var existing = _historyCache.FirstOrDefault(h => h.VersionName == versionName);
            
            if (existing != null)
            {
                existing.TotalPlayTimeSeconds += playTimeSeconds;
                
                if (isCrash)
                {
                    existing.CrashCount++;
                }
            }
        }
        
        await SaveHistoryAsync();
    }
    
    /// <summary>
    /// 获取最近游玩的版本（按最后启动时间排序）
    /// </summary>
    public Task<List<GameLaunchHistory>> GetRecentVersionsAsync(int count = 5)
    {
        lock (_lock)
        {
            var recent = _historyCache
                .OrderByDescending(h => h.LastLaunchTime)
                .Take(count)
                .ToList();
            
            return Task.FromResult(recent);
        }
    }
    
    /// <summary>
    /// 获取所有历史记录
    /// </summary>
    public Task<List<GameLaunchHistory>> GetAllHistoryAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(new List<GameLaunchHistory>(_historyCache));
        }
    }
    
    /// <summary>
    /// 获取指定版本的历史记录
    /// </summary>
    public Task<GameLaunchHistory?> GetVersionHistoryAsync(string versionName)
    {
        if (string.IsNullOrEmpty(versionName))
            return Task.FromResult<GameLaunchHistory?>(null);
        
        lock (_lock)
        {
            var history = _historyCache.FirstOrDefault(h => h.VersionName == versionName);
            return Task.FromResult(history);
        }
    }
    
    /// <summary>
    /// 清空历史记录
    /// </summary>
    public async Task ClearHistoryAsync()
    {
        lock (_lock)
        {
            _historyCache.Clear();
        }
        
        await SaveHistoryAsync();
    }
}
