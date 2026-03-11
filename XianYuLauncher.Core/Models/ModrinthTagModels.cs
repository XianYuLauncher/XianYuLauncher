using System.Text.Json.Serialization;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// Modrinth tag 接口返回项。
/// 适用于 /v2/tag/category、/v2/tag/loader、/v2/tag/game_version。
/// </summary>
public class ModrinthTagItem
{
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("project_type")]
    public string? ProjectType { get; set; }

    [JsonPropertyName("header")]
    public string? Header { get; set; }
}
