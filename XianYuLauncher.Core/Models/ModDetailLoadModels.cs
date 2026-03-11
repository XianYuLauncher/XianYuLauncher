using System.Collections.Generic;

namespace XianYuLauncher.Core.Models;

public sealed record ModDetailVersionItem(
    string VersionNumber,
    string ReleaseDate,
    string Changelog,
    string? DownloadUrl,
    string? FileName,
    IReadOnlyList<string> Loaders,
    string VersionType,
    string GameVersion,
    ModrinthVersion? OriginalModrinthVersion = null,
    CurseForgeFile? OriginalCurseForgeFile = null);

public sealed record ModDetailLoaderGroup(
    string LoaderName,
    IReadOnlyList<ModDetailVersionItem> Versions);

public sealed record ModDetailGameVersionGroup(
    string GameVersion,
    IReadOnlyList<ModDetailLoaderGroup> Loaders);