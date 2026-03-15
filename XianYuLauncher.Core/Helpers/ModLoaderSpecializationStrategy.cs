using System;
using System.Collections.Generic;
using System.Linq;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

public sealed class ModLoaderSpecializationContext
{
    public ModLoaderSpecializationContext(VersionInfo baseVersion, VersionInfo? loaderVersion = null, bool isAddonMode = false)
    {
        BaseVersion = baseVersion ?? throw new ArgumentNullException(nameof(baseVersion));
        LoaderVersion = loaderVersion;
        IsAddonMode = isAddonMode;
    }

    public VersionInfo BaseVersion { get; }

    public VersionInfo? LoaderVersion { get; }

    public bool IsAddonMode { get; }
}

public abstract class ModLoaderSpecializationStrategy
{
    public virtual List<Library> PrepareBaseLibraries(ModLoaderSpecializationContext context)
    {
        return context.BaseVersion.Libraries != null
            ? new List<Library>(context.BaseVersion.Libraries)
            : new List<Library>();
    }

    public virtual string? ResolveMainClass(string? fallbackMainClass, ModLoaderSpecializationContext context)
    {
        return fallbackMainClass;
    }

    public virtual MinecraftJavaVersion? ResolveJavaVersion(MinecraftJavaVersion? fallbackJavaVersion, ModLoaderSpecializationContext context)
    {
        return fallbackJavaVersion;
    }

    public virtual bool ShouldIncludePrimaryDownloadArtifact(string libraryName)
    {
        return true;
    }

    public virtual IReadOnlyList<Library> FilterLibrariesForClasspath(IReadOnlyList<Library> libraries)
    {
        return libraries.ToList();
    }
}

public static class ModLoaderSpecializationStrategyFactory
{
    private static readonly ModLoaderSpecializationStrategy DefaultStrategy = new DefaultModLoaderSpecializationStrategy();

    private static readonly IReadOnlyDictionary<string, ModLoaderSpecializationStrategy> Strategies =
        new Dictionary<string, ModLoaderSpecializationStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            ["fabric"] = new FabricModLoaderSpecializationStrategy(),
            ["cleanroom"] = new CleanroomModLoaderSpecializationStrategy(),
            ["liteloader"] = new LiteLoaderModLoaderSpecializationStrategy(),
            ["legacyfabric"] = new LegacyFabricModLoaderSpecializationStrategy(),
            ["neoforge"] = new NeoForgeModLoaderSpecializationStrategy()
        };

    public static ModLoaderSpecializationStrategy GetStrategy(string? modLoaderType)
    {
        if (string.IsNullOrWhiteSpace(modLoaderType))
        {
            return DefaultStrategy;
        }

        return Strategies.TryGetValue(modLoaderType, out var strategy)
            ? strategy
            : DefaultStrategy;
    }

    private sealed class DefaultModLoaderSpecializationStrategy : ModLoaderSpecializationStrategy;

    private sealed class FabricModLoaderSpecializationStrategy : ModLoaderSpecializationStrategy
    {
        public override IReadOnlyList<Library> FilterLibrariesForClasspath(IReadOnlyList<Library> libraries)
        {
            string latestAsmVersion = "0.0";

            foreach (var library in libraries)
            {
                if (!library.Name.StartsWith("org.ow2.asm:asm:", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = library.Name.Split(':');
                if (parts.Length < 3)
                {
                    continue;
                }

                if (string.Compare(parts[2], latestAsmVersion, StringComparison.Ordinal) > 0)
                {
                    latestAsmVersion = parts[2];
                }
            }

            if (latestAsmVersion == "0.0")
            {
                return base.FilterLibrariesForClasspath(libraries);
            }

            var filteredLibraries = new List<Library>();
            foreach (var library in libraries)
            {
                if (library.Name.StartsWith("org.ow2.asm:asm:", StringComparison.Ordinal))
                {
                    var parts = library.Name.Split(':');
                    if (parts.Length >= 3 && parts[2] != latestAsmVersion)
                    {
                        continue;
                    }
                }

                filteredLibraries.Add(library);
            }

            return filteredLibraries;
        }
    }

    private sealed class CleanroomModLoaderSpecializationStrategy : ModLoaderSpecializationStrategy
    {
        public override List<Library> PrepareBaseLibraries(ModLoaderSpecializationContext context)
        {
            var baseLibraries = base.PrepareBaseLibraries(context);
            bool shouldUseLwjgl3 = context.LoaderVersion?.Libraries?.Any(library =>
                library.Name.StartsWith("org.lwjgl", StringComparison.OrdinalIgnoreCase) &&
                !library.Name.Contains("2.9", StringComparison.OrdinalIgnoreCase)) ?? false;

            if (!shouldUseLwjgl3)
            {
                return baseLibraries;
            }

            return baseLibraries.Where(library => !IsLwjgl2Library(library.Name)).ToList();
        }

        public override MinecraftJavaVersion? ResolveJavaVersion(MinecraftJavaVersion? fallbackJavaVersion, ModLoaderSpecializationContext context)
        {
            return new MinecraftJavaVersion
            {
                Component = "java-runtime-delta",
                MajorVersion = 21
            };
        }

        private static bool IsLwjgl2Library(string libraryName)
        {
            return libraryName.StartsWith("org.lwjgl.lwjgl:lwjgl", StringComparison.OrdinalIgnoreCase) ||
                   libraryName.StartsWith("org.lwjgl.lwjgl:lwjgl_util", StringComparison.OrdinalIgnoreCase) ||
                   libraryName.StartsWith("org.lwjgl.lwjgl:lwjgl-platform", StringComparison.OrdinalIgnoreCase) ||
                   libraryName.StartsWith("net.java.jinput:jinput", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class LiteLoaderModLoaderSpecializationStrategy : ModLoaderSpecializationStrategy
    {
        private const string LaunchWrapperMainClass = "net.minecraft.launchwrapper.Launch";

        public override string? ResolveMainClass(string? fallbackMainClass, ModLoaderSpecializationContext context)
        {
            if (context.IsAddonMode && !string.IsNullOrEmpty(context.BaseVersion.MainClass))
            {
                return context.BaseVersion.MainClass;
            }

            return LaunchWrapperMainClass;
        }
    }

    private sealed class LegacyFabricModLoaderSpecializationStrategy : ModLoaderSpecializationStrategy
    {
        public override bool ShouldIncludePrimaryDownloadArtifact(string libraryName)
        {
            return !libraryName.Contains(":lwjgl-platform:", StringComparison.OrdinalIgnoreCase) &&
                   !libraryName.Contains(":jinput-platform:", StringComparison.OrdinalIgnoreCase) &&
                   !libraryName.Contains(":natives-", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class NeoForgeModLoaderSpecializationStrategy : ModLoaderSpecializationStrategy
    {
        public override IReadOnlyList<Library> FilterLibrariesForClasspath(IReadOnlyList<Library> libraries)
        {
            return libraries
                .Where(library =>
                    !(library.Name.Contains("neoforge", StringComparison.OrdinalIgnoreCase) &&
                      (library.Name.Contains("universal", StringComparison.OrdinalIgnoreCase) ||
                       library.Name.Contains("installertools", StringComparison.OrdinalIgnoreCase))))
                .ToList();
        }
    }
}