using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

public interface IUnifiedVersionManifestResolver
{
    ManifestResolutionResult ResolveInheritance(
        VersionInfo childManifest,
        VersionInfo? parentManifest,
        ManifestResolutionOptions? options = null);

    ManifestResolutionResult ResolvePatch(
        VersionInfo baseManifest,
        ManifestPatch patch,
        ManifestResolutionOptions? options = null);
}