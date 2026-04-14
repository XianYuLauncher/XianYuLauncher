using System;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 表示已经按当前架构解析完成的更新清单结果。
/// </summary>
public sealed class ResolvedUpdateManifest
{
    public UpdateManifest Manifest { get; init; } = new();

    public string Architecture { get; init; } = string.Empty;

    public UpdateManifestTarget Target { get; init; } = new();

    public string Version => Manifest.Release.Version;

    public string Channel => Manifest.Release.Channel;

    public bool Important => Manifest.Release.Important;

    public DateTimeOffset PublishedAt => Manifest.Release.PublishedAt;
}