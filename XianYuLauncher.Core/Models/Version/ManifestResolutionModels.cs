using System.Collections.Generic;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Models;

public enum ManifestArgumentResolutionMode
{
    InheritIfMissing,
    UseMergeHelper
}

public sealed class ManifestPatch
{
    public string? Id { get; init; }

    public string? Type { get; init; }

    public string? Time { get; init; }

    public string? ReleaseTime { get; init; }

    public string? Url { get; init; }

    public Downloads? Downloads { get; init; }

    public List<Library>? Libraries { get; init; }

    public string? MainClass { get; init; }

    public Arguments? Arguments { get; init; }

    public AssetIndex? AssetIndex { get; init; }

    public string? Assets { get; init; }

    public MinecraftJavaVersion? JavaVersion { get; init; }

    public string? MinecraftArguments { get; init; }

    public static ManifestPatch FromVersionInfo(VersionInfo versionInfo)
    {
        ArgumentNullException.ThrowIfNull(versionInfo);

        return new ManifestPatch
        {
            Id = versionInfo.Id,
            Type = versionInfo.Type,
            Time = versionInfo.Time,
            ReleaseTime = versionInfo.ReleaseTime,
            Url = versionInfo.Url,
            Downloads = versionInfo.Downloads,
            Libraries = versionInfo.Libraries != null ? new List<Library>(versionInfo.Libraries) : null,
            MainClass = versionInfo.MainClass,
            Arguments = versionInfo.Arguments,
            AssetIndex = versionInfo.AssetIndex,
            Assets = versionInfo.Assets,
            JavaVersion = versionInfo.JavaVersion,
            MinecraftArguments = versionInfo.MinecraftArguments
        };
    }

    public VersionInfo ToVersionInfo(string? fallbackId = null)
    {
        return new VersionInfo
        {
            Id = Id ?? fallbackId ?? string.Empty,
            Type = Type,
            Time = Time,
            ReleaseTime = ReleaseTime,
            Url = Url,
            Downloads = Downloads,
            Libraries = Libraries != null ? new List<Library>(Libraries) : null,
            MainClass = MainClass,
            Arguments = Arguments,
            AssetIndex = AssetIndex,
            Assets = Assets,
            JavaVersion = JavaVersion,
            MinecraftArguments = MinecraftArguments
        };
    }
}

public sealed class ManifestResolutionOptions
{
    public ManifestArgumentResolutionMode ArgumentResolutionMode { get; init; } = ManifestArgumentResolutionMode.InheritIfMissing;

    public LegacyArgumentMergeMode LegacyArgumentMergeMode { get; init; } = LegacyArgumentMergeMode.PreferBaseIfPresent;

    public ModernArgumentMergeMode ModernArgumentMergeMode { get; init; } = ModernArgumentMergeMode.OverrideSections;

    public string? ModLoaderType { get; init; }

    public bool IsAddonMode { get; init; }

    public LibraryRepositoryProfile? LibraryRepositoryProfile { get; init; }

    public static ManifestResolutionOptions CreateInheritanceOptions()
    {
        return new ManifestResolutionOptions();
    }

    public static ManifestResolutionOptions CreateLoaderPatchOptions(
        string? modLoaderType,
        LibraryRepositoryProfile? libraryRepositoryProfile = null,
        bool isAddonMode = false,
        LegacyArgumentMergeMode legacyArgumentMergeMode = LegacyArgumentMergeMode.PreferAnyWithLoaderPriority,
        ModernArgumentMergeMode modernArgumentMergeMode = ModernArgumentMergeMode.OverrideSections)
    {
        return new ManifestResolutionOptions
        {
            ArgumentResolutionMode = ManifestArgumentResolutionMode.UseMergeHelper,
            LegacyArgumentMergeMode = legacyArgumentMergeMode,
            ModernArgumentMergeMode = modernArgumentMergeMode,
            ModLoaderType = modLoaderType,
            IsAddonMode = isAddonMode,
            LibraryRepositoryProfile = libraryRepositoryProfile
        };
    }
}

public sealed class ManifestResolutionResult
{
    public VersionInfo BaseManifest { get; init; } = null!;

    public VersionInfo? OverlayManifest { get; init; }

    public ManifestPatch? AppliedPatch { get; init; }

    public VersionInfo ResolvedManifest { get; init; } = null!;
}