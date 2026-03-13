using System;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// Minecraft 角色信息类
/// </summary>
public class MinecraftProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ClientToken { get; set; }
    public string? TokenType { get; set; }
    public int ExpiresIn { get; set; }
    public DateTime IssueInstant { get; set; }
    public DateTime NotAfter { get; set; }
    public string[]? Roles { get; set; }
    public bool IsOffline { get; set; }
    public bool IsActive { get; set; }
    public string? AuthServer { get; set; }
}
