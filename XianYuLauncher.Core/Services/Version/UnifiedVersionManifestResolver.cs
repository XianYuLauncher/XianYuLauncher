using System;
using System.Collections.Generic;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public class UnifiedVersionManifestResolver : IUnifiedVersionManifestResolver
{
    public ManifestResolutionResult ResolveInheritance(
        VersionInfo childManifest,
        VersionInfo? parentManifest,
        ManifestResolutionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(childManifest);

        if (parentManifest == null)
        {
            return new ManifestResolutionResult
            {
                BaseManifest = childManifest,
                OverlayManifest = childManifest,
                ResolvedManifest = childManifest
            };
        }

        return ResolveCore(
            parentManifest,
            childManifest,
            null,
            options ?? ManifestResolutionOptions.CreateInheritanceOptions());
    }

    public ManifestResolutionResult ResolvePatch(
        VersionInfo baseManifest,
        ManifestPatch patch,
        ManifestResolutionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(baseManifest);
        ArgumentNullException.ThrowIfNull(patch);

        var overlayManifest = patch.ToVersionInfo(baseManifest.Id);

        return ResolveCore(
            baseManifest,
            overlayManifest,
            patch,
            options ?? ManifestResolutionOptions.CreateLoaderPatchOptions(null));
    }

    private static ManifestResolutionResult ResolveCore(
        VersionInfo baseManifest,
        VersionInfo? overlayManifest,
        ManifestPatch? appliedPatch,
        ManifestResolutionOptions options)
    {
        var specializationStrategy = ModLoaderSpecializationStrategyFactory.GetStrategy(options.ModLoaderType);
        var specializationContext = new ModLoaderSpecializationContext(baseManifest, overlayManifest, options.IsAddonMode);

        var resolvedArguments = ResolveArguments(baseManifest, overlayManifest, options);
        var resolvedLibraries = ResolveLibraries(baseManifest, overlayManifest, specializationStrategy, specializationContext, options);
        var resolvedMainClass = specializationStrategy.ResolveMainClass(
            FirstNonEmpty(overlayManifest?.MainClass, baseManifest.MainClass),
            specializationContext);
        var resolvedJavaVersion = specializationStrategy.ResolveJavaVersion(
            overlayManifest?.JavaVersion ?? baseManifest.JavaVersion,
            specializationContext);

        var resolvedManifest = new VersionInfo
        {
            Id = FirstNonEmpty(overlayManifest?.Id, baseManifest.Id) ?? string.Empty,
            Type = FirstNonEmpty(overlayManifest?.Type, baseManifest.Type),
            Time = FirstNonEmpty(overlayManifest?.Time, baseManifest.Time),
            ReleaseTime = FirstNonEmpty(overlayManifest?.ReleaseTime, baseManifest.ReleaseTime),
            Url = FirstNonEmpty(overlayManifest?.Url, baseManifest.Url),
            Downloads = overlayManifest?.Downloads ?? baseManifest.Downloads,
            Libraries = resolvedLibraries,
            MainClass = resolvedMainClass,
            Arguments = resolvedArguments.Arguments,
            AssetIndex = overlayManifest?.AssetIndex ?? baseManifest.AssetIndex,
            Assets = FirstNonEmpty(overlayManifest?.Assets, baseManifest.Assets),
            JavaVersion = resolvedJavaVersion,
            InheritsFrom = overlayManifest?.InheritsFrom,
            MinecraftArguments = resolvedArguments.MinecraftArguments
        };

        return new ManifestResolutionResult
        {
            BaseManifest = baseManifest,
            OverlayManifest = overlayManifest,
            AppliedPatch = appliedPatch,
            ResolvedManifest = resolvedManifest
        };
    }

    private static List<Library> ResolveLibraries(
        VersionInfo baseManifest,
        VersionInfo? overlayManifest,
        ModLoaderSpecializationStrategy specializationStrategy,
        ModLoaderSpecializationContext specializationContext,
        ManifestResolutionOptions options)
    {
        var baseLibraries = specializationStrategy.PrepareBaseLibraries(specializationContext);
        var resolvedLibraries = VersionLibraryMergeHelper.MergeLibraries(overlayManifest?.Libraries, baseLibraries);

        if (options.LibraryRepositoryProfile.HasValue)
        {
            LibraryDownloadUrlHelper.EnsureArtifactDownloads(resolvedLibraries, options.LibraryRepositoryProfile.Value);
        }

        return resolvedLibraries;
    }

    private static VersionArgumentsMergeResult ResolveArguments(
        VersionInfo baseManifest,
        VersionInfo? overlayManifest,
        ManifestResolutionOptions options)
    {
        if (overlayManifest == null)
        {
            return new VersionArgumentsMergeResult
            {
                Arguments = baseManifest.Arguments,
                MinecraftArguments = baseManifest.MinecraftArguments
            };
        }

        if (options.ArgumentResolutionMode == ManifestArgumentResolutionMode.UseMergeHelper)
        {
            return VersionArgumentsMergeHelper.Merge(
                baseManifest.Arguments,
                baseManifest.MinecraftArguments,
                overlayManifest.Arguments,
                overlayManifest.MinecraftArguments,
                options.LegacyArgumentMergeMode,
                options.ModernArgumentMergeMode);
        }

        return new VersionArgumentsMergeResult
        {
            Arguments = overlayManifest.Arguments ?? baseManifest.Arguments,
            MinecraftArguments = !string.IsNullOrEmpty(overlayManifest.MinecraftArguments)
                ? overlayManifest.MinecraftArguments
                : baseManifest.MinecraftArguments
        };
    }

    private static string? FirstNonEmpty(string? primary, string? fallback)
    {
        return !string.IsNullOrEmpty(primary) ? primary : fallback;
    }
}