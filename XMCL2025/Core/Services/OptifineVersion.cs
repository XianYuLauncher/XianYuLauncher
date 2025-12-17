using Newtonsoft.Json;

namespace XMCL2025.Core.Services;

/// <summary>
/// Optifine版本模型，用于解析BMCLAPI返回的Optifine版本数据
/// </summary>
public class OptifineVersion
{
    /// <summary>
    /// Optifine版本类型（如HD_U_J7）
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; }
    
    /// <summary>
    /// Optifine补丁版本（如pre10）
    /// </summary>
    [JsonProperty("patch")]
    public string Patch { get; set; }
    
    /// <summary>
    /// 兼容的Forge版本
    /// </summary>
    [JsonProperty("forge")]
    public string Forge { get; set; }
    
    /// <summary>
    /// 完整的Optifine版本名称（Type_Patch）
    /// </summary>
    public string FullVersion => $"{Type}_{Patch}";
}