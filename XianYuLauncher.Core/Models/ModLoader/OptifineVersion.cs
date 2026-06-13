using Newtonsoft.Json;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Optifine 版本模型，用于解析 BMCLAPI 返回的 Optifine 版本数据
/// </summary>
public class OptifineVersion
{
    /// <summary>
    /// Optifine 版本类型（如 HD_U_J7）
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Optifine 补丁版本（如 pre10）
    /// </summary>
    [JsonProperty("patch")]
    public string Patch { get; set; } = string.Empty;
    
    /// <summary>
    /// 兼容的 Forge 版本
    /// </summary>
    [JsonProperty("forge")]
    public string Forge { get; set; } = string.Empty;
    
    /// <summary>
    /// 完整的 Optifine 版本名称（Type_Patch）
    /// </summary>
    public string FullVersion => $"{Type}_{Patch}";
}