using System;
using System.Collections.Generic;
using System.Linq;

namespace XianYuLauncher.Core.Helpers;

public static class QuickInstallCompatibilityHelper
{
    public static bool EvaluateCompatibility(
        string projectType,
        string gameVersion,
        IReadOnlyCollection<string> gameLoaders,
        ISet<string> supportedGameVersionIds,
        IReadOnlyCollection<string>? supportedLoaders,
        IReadOnlyCollection<string>? modLoaders)
    {
        if (!IsSupportedGameVersion(gameVersion, supportedGameVersionIds))
        {
            return false;
        }

        if (IsVersionOnlyResourceType(projectType, modLoaders))
        {
            return true;
        }

        if (supportedLoaders is { Count: > 0 })
        {
            return gameLoaders.Any(gameLoader => IsLoaderCompatible(gameLoader, supportedLoaders));
        }

        return true;
    }

    public static bool IsVersionOnlyResourceType(string projectType, IReadOnlyCollection<string>? modLoaders)
    {
        var isDatapack = string.Equals(projectType, "datapack", StringComparison.OrdinalIgnoreCase) ||
                         (modLoaders != null && modLoaders.Any(loader =>
                             loader.Equals("Datapack", StringComparison.OrdinalIgnoreCase)));

        return string.Equals(projectType, "resourcepack", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(projectType, "shader", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(projectType, "world", StringComparison.OrdinalIgnoreCase) ||
               isDatapack;
    }

    public static bool IsLoaderCompatible(string gameLoader, IReadOnlyCollection<string> supportedLoaders)
    {
        if (gameLoader.Equals("LegacyFabric", StringComparison.OrdinalIgnoreCase))
        {
            return supportedLoaders.Any(loader =>
                loader.Equals("LegacyFabric", StringComparison.OrdinalIgnoreCase) ||
                loader.Equals("legacy-fabric", StringComparison.OrdinalIgnoreCase));
        }

        if (gameLoader.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase))
        {
            return supportedLoaders.Any(loader =>
                loader.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase) ||
                loader.Equals("liteloader", StringComparison.OrdinalIgnoreCase));
        }

        return supportedLoaders.Any(loader => loader.Equals(gameLoader, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSupportedGameVersion(string gameVersion, ISet<string> supportedGameVersionIds)
    {
        return !string.IsNullOrEmpty(gameVersion) &&
               (supportedGameVersionIds.Contains(gameVersion) || supportedGameVersionIds.Contains("Generic"));
    }
}
