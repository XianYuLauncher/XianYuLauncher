using System;
using System.Collections.Generic;
using System.IO;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

public enum LibraryRepositoryProfile
{
    Default,
    Forge,
    NeoForge,
    Fabric,
    Quilt,
    LegacyFabric,
    Cleanroom,
    LiteLoader
}

public static class LibraryDownloadUrlHelper
{
    private const string DefaultLibraryBaseUrl = "https://libraries.minecraft.net/";
    private const string ForgeMavenBaseUrl = "https://maven.minecraftforge.net/";
    private const string NeoForgeMavenBaseUrl = "https://maven.neoforged.net/releases/";
    private const string FabricMavenBaseUrl = "https://maven.fabricmc.net/";
    private const string QuiltMavenBaseUrl = "https://maven.quiltmc.org/repository/release/";
    private const string LegacyFabricMavenBaseUrl = "https://repo.legacyfabric.net/repository/legacyfabric/";
    private const string CleanroomMavenBaseUrl = "https://repo.cleanroommc.com/releases/";

    public static void EnsureArtifactDownloads(IEnumerable<Library>? libraries, LibraryRepositoryProfile profile)
    {
        if (libraries == null)
        {
            return;
        }

        foreach (var library in libraries)
        {
            EnsureArtifactDownload(library, profile);
        }
    }

    public static void EnsureArtifactDownload(Library library, LibraryRepositoryProfile profile)
    {
        ArgumentNullException.ThrowIfNull(library);

        library.Downloads ??= new LibraryDownloads();

        var existingArtifact = library.Downloads.Artifact;
        var resolvedUrl = ResolveArtifactUrl(library.Name, existingArtifact?.Url ?? library.Url, profile);
        if (string.IsNullOrWhiteSpace(resolvedUrl))
        {
            return;
        }

        library.Downloads.Artifact = new DownloadFile
        {
            Url = resolvedUrl,
            Sha1 = existingArtifact?.Sha1,
            Size = existingArtifact?.Size ?? 0
        };
    }

    public static string? ResolveArtifactUrl(
        string libraryName,
        string? explicitUrlOrBaseUrl,
        LibraryRepositoryProfile profile = LibraryRepositoryProfile.Default)
    {
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return null;
        }

        if (IsArtifactUrl(explicitUrlOrBaseUrl))
        {
            return explicitUrlOrBaseUrl;
        }

        var baseUrl = ResolveRepositoryBaseUrl(libraryName, explicitUrlOrBaseUrl, profile);
        return string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : BuildArtifactUrl(libraryName, baseUrl);
    }

    public static string? ResolveRepositoryBaseUrl(
        string libraryName,
        string? explicitUrlOrBaseUrl,
        LibraryRepositoryProfile profile)
    {
        if (IsArtifactUrl(explicitUrlOrBaseUrl))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(explicitUrlOrBaseUrl))
        {
            return EnsureTrailingSlash(explicitUrlOrBaseUrl);
        }

        var groupId = GetGroupId(libraryName);
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return null;
        }

        if (UsesOfficialLibraryBaseUrl(groupId))
        {
            return DefaultLibraryBaseUrl;
        }

        return profile switch
        {
            LibraryRepositoryProfile.Forge => groupId.StartsWith("net.minecraftforge", StringComparison.OrdinalIgnoreCase)
                ? ForgeMavenBaseUrl
                : DefaultLibraryBaseUrl,
            LibraryRepositoryProfile.NeoForge => ResolveNeoForgeBaseUrl(groupId),
            LibraryRepositoryProfile.Fabric => FabricMavenBaseUrl,
            LibraryRepositoryProfile.Quilt => groupId.StartsWith("net.fabricmc", StringComparison.OrdinalIgnoreCase)
                ? FabricMavenBaseUrl
                : QuiltMavenBaseUrl,
            LibraryRepositoryProfile.LegacyFabric => groupId.StartsWith("net.legacyfabric", StringComparison.OrdinalIgnoreCase)
                ? LegacyFabricMavenBaseUrl
                : FabricMavenBaseUrl,
            LibraryRepositoryProfile.Cleanroom => ResolveCleanroomBaseUrl(groupId),
            LibraryRepositoryProfile.LiteLoader => DefaultLibraryBaseUrl,
            _ => DefaultLibraryBaseUrl
        };
    }

    public static string? BuildArtifactUrl(
        string libraryName,
        string baseUrl,
        string? classifierOverride = null,
        string? extensionOverride = null)
    {
        if (string.IsNullOrWhiteSpace(libraryName) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var parts = libraryName.Split(':');
        if (parts.Length < 3)
        {
            return null;
        }

        var groupId = parts[0];
        var artifactId = parts[1];
        var version = parts[2];
        string? classifier = null;
        var extension = "jar";

        if (parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[3]))
        {
            var classifierParts = parts[3].Split('@', 2);
            classifier = classifierParts[0];
            if (classifierParts.Length == 2 && !string.IsNullOrWhiteSpace(classifierParts[1]))
            {
                extension = classifierParts[1];
            }
        }

        if (!string.IsNullOrWhiteSpace(classifierOverride))
        {
            classifier = classifierOverride;
        }

        if (!string.IsNullOrWhiteSpace(extensionOverride))
        {
            extension = extensionOverride;
        }

        var fileName = $"{artifactId}-{version}";
        if (!string.IsNullOrWhiteSpace(classifier))
        {
            fileName += $"-{classifier}";
        }

        fileName += $".{extension}";
        return $"{EnsureTrailingSlash(baseUrl)}{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";
    }

    public static bool IsArtifactUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return !url.EndsWith("/", StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(Path.GetExtension(uri.AbsolutePath));
    }

    private static string ResolveNeoForgeBaseUrl(string groupId)
    {
        if (groupId.StartsWith("net.neoforged", StringComparison.OrdinalIgnoreCase))
        {
            return NeoForgeMavenBaseUrl;
        }

        return groupId.StartsWith("net.minecraftforge", StringComparison.OrdinalIgnoreCase)
            ? ForgeMavenBaseUrl
            : DefaultLibraryBaseUrl;
    }

    private static string ResolveCleanroomBaseUrl(string groupId)
    {
        if (groupId.StartsWith("com.cleanroommc", StringComparison.OrdinalIgnoreCase))
        {
            return CleanroomMavenBaseUrl;
        }

        return groupId.StartsWith("net.minecraftforge", StringComparison.OrdinalIgnoreCase)
            ? ForgeMavenBaseUrl
            : DefaultLibraryBaseUrl;
    }

    private static bool UsesOfficialLibraryBaseUrl(string groupId)
    {
        return groupId.StartsWith("org.ow2", StringComparison.OrdinalIgnoreCase) ||
               groupId.StartsWith("net.java", StringComparison.OrdinalIgnoreCase) ||
               groupId.StartsWith("org.apache", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetGroupId(string libraryName)
    {
        var parts = libraryName.Split(':');
        return parts.Length >= 1 ? parts[0] : null;
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : baseUrl + "/";
    }
}