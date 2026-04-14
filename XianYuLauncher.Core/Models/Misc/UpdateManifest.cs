using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 表示 Gitee bootstrap 更新清单。
/// </summary>
public sealed class UpdateManifest
{
    [JsonProperty("schema_version")]
    public int SchemaVersion { get; set; }

    [JsonProperty("delivery")]
    public string Delivery { get; set; } = string.Empty;

    [JsonProperty("release")]
    public UpdateManifestRelease Release { get; set; } = new();

    [JsonProperty("migration")]
    public UpdateManifestMigration? Migration { get; set; }

    [JsonProperty("notes")]
    public List<string> Notes { get; set; } = new();

    [JsonProperty("targets")]
    public Dictionary<string, UpdateManifestTarget> Targets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 表示清单中的版本发布信息。
/// </summary>
public sealed class UpdateManifestRelease
{
    [JsonProperty("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonProperty("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonProperty("version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty("published_at")]
    public DateTimeOffset PublishedAt { get; set; }

    [JsonProperty("important")]
    public bool Important { get; set; }
}

/// <summary>
/// 表示清单中的迁移提示信息。
/// </summary>
public sealed class UpdateManifestMigration
{
    [JsonProperty("required")]
    public bool Required { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 表示某个架构的更新目标。
/// </summary>
public sealed class UpdateManifestTarget
{
    [JsonProperty("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonProperty("setup_url")]
    public string SetupUrl { get; set; } = string.Empty;

    [JsonProperty("setup_sha256")]
    public string SetupSha256 { get; set; } = string.Empty;

    [JsonProperty("feed_url")]
    public string FeedUrl { get; set; } = string.Empty;

    [JsonProperty("package_url")]
    public string PackageUrl { get; set; } = string.Empty;

    [JsonProperty("package_sha256")]
    public string PackageSha256 { get; set; } = string.Empty;

    [JsonProperty("package_size")]
    public long PackageSize { get; set; }
}