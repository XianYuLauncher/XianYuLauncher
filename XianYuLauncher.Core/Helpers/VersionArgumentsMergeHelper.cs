using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

public enum LegacyArgumentMergeMode
{
    PreferBaseIfPresent,
    PreferAnyWithLoaderPriority
}

public enum ModernArgumentMergeMode
{
    MergeLists,
    OverrideSections
}

public sealed class VersionArgumentsMergeResult
{
    public Arguments? Arguments { get; init; }

    public string? MinecraftArguments { get; init; }
}

public static class VersionArgumentsMergeHelper
{
    public static VersionArgumentsMergeResult Merge(
        Arguments? baseArguments,
        string? baseMinecraftArguments,
        Arguments? modLoaderArguments,
        string? modLoaderMinecraftArguments,
        LegacyArgumentMergeMode legacyMode,
        ModernArgumentMergeMode modernMode)
    {
        var mergedMinecraftArguments = ResolveLegacyArguments(
            baseMinecraftArguments,
            modLoaderMinecraftArguments,
            legacyMode);

        if (!string.IsNullOrEmpty(mergedMinecraftArguments))
        {
            return new VersionArgumentsMergeResult
            {
                Arguments = null,
                MinecraftArguments = mergedMinecraftArguments
            };
        }

        return new VersionArgumentsMergeResult
        {
            Arguments = MergeModernArguments(baseArguments, modLoaderArguments, modernMode),
            MinecraftArguments = null
        };
    }

    public static VersionArgumentsMergeResult AppendGameArgumentsPreservingFormat(
        Arguments? baseArguments,
        string? baseMinecraftArguments,
        params object[] additionalGameArguments)
    {
        if (!string.IsNullOrEmpty(baseMinecraftArguments))
        {
            var serializedArguments = string.Join(" ", Array.FindAll(additionalGameArguments, argument => argument != null));
            return new VersionArgumentsMergeResult
            {
                Arguments = null,
                MinecraftArguments = string.IsNullOrEmpty(serializedArguments)
                    ? baseMinecraftArguments
                    : $"{baseMinecraftArguments} {serializedArguments}"
            };
        }

        var additionalArguments = new Arguments
        {
            Game = new List<object>()
        };

        foreach (var argument in additionalGameArguments)
        {
            if (argument != null)
            {
                additionalArguments.Game.Add(argument);
            }
        }

        return new VersionArgumentsMergeResult
        {
            Arguments = MergeModernArguments(baseArguments, additionalArguments, ModernArgumentMergeMode.MergeLists),
            MinecraftArguments = null
        };
    }

    private static string? ResolveLegacyArguments(
        string? baseMinecraftArguments,
        string? modLoaderMinecraftArguments,
        LegacyArgumentMergeMode legacyMode)
    {
        return legacyMode switch
        {
            LegacyArgumentMergeMode.PreferBaseIfPresent => !string.IsNullOrEmpty(baseMinecraftArguments)
                ? baseMinecraftArguments
                : modLoaderMinecraftArguments,
            LegacyArgumentMergeMode.PreferAnyWithLoaderPriority => !string.IsNullOrEmpty(modLoaderMinecraftArguments)
                ? modLoaderMinecraftArguments
                : baseMinecraftArguments,
            _ => throw new ArgumentOutOfRangeException(nameof(legacyMode), legacyMode, null)
        };
    }

    private static Arguments? MergeModernArguments(
        Arguments? baseArguments,
        Arguments? modLoaderArguments,
        ModernArgumentMergeMode modernMode)
    {
        return modernMode switch
        {
            ModernArgumentMergeMode.MergeLists => CreateArgumentsOrNull(
                MergeArgumentList(baseArguments?.Game, modLoaderArguments?.Game),
                MergeArgumentList(baseArguments?.Jvm, modLoaderArguments?.Jvm),
                MergeArgumentList(baseArguments?.DefaultUserJvm, modLoaderArguments?.DefaultUserJvm)),
            ModernArgumentMergeMode.OverrideSections => CreateArgumentsOrNull(
                HasEntries(modLoaderArguments?.Game) ? CloneArgumentList(modLoaderArguments?.Game) : CloneArgumentList(baseArguments?.Game),
                HasEntries(modLoaderArguments?.Jvm) ? CloneArgumentList(modLoaderArguments?.Jvm) : CloneArgumentList(baseArguments?.Jvm),
                HasEntries(modLoaderArguments?.DefaultUserJvm) ? CloneArgumentList(modLoaderArguments?.DefaultUserJvm) : CloneArgumentList(baseArguments?.DefaultUserJvm)),
            _ => throw new ArgumentOutOfRangeException(nameof(modernMode), modernMode, null)
        };
    }

    private static Arguments? CreateArgumentsOrNull(
        List<object>? game,
        List<object>? jvm,
        List<object>? defaultUserJvm)
    {
        if (!HasEntries(game) && !HasEntries(jvm) && !HasEntries(defaultUserJvm))
        {
            return null;
        }

        return new Arguments
        {
            Game = game,
            Jvm = jvm,
            DefaultUserJvm = defaultUserJvm
        };
    }

    private static List<object>? MergeArgumentList(List<object>? baseArguments, List<object>? modLoaderArguments)
    {
        if (!HasEntries(baseArguments) && !HasEntries(modLoaderArguments))
        {
            return null;
        }

        var merged = new List<object>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AddArguments(baseArguments, merged, seen);
        AddArguments(modLoaderArguments, merged, seen);

        return merged.Count > 0 ? merged : null;
    }

    private static void AddArguments(
        List<object>? source,
        List<object> destination,
        HashSet<string> seen)
    {
        if (!HasEntries(source))
        {
            return;
        }

        foreach (var argument in source)
        {
            var key = GetArgumentKey(argument);
            if (key == null || !seen.Add(key))
            {
                continue;
            }

            destination.Add(argument);
        }
    }

    private static List<object>? CloneArgumentList(List<object>? source)
    {
        return HasEntries(source) ? new List<object>(source!) : null;
    }

    private static bool HasEntries(List<object>? arguments)
    {
        return arguments != null && arguments.Count > 0;
    }

    private static string? GetArgumentKey(object? argument)
    {
        return argument switch
        {
            null => null,
            JToken token => token.ToString(Formatting.None),
            string text => $"string:{text}",
            IFormattable formattable => $"value:{formattable.ToString(null, CultureInfo.InvariantCulture)}",
            _ => JsonConvert.SerializeObject(argument)
        };
    }
}