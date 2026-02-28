using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class LoaderSelection
{
    public string Name { get; init; } = string.Empty;

    public string LoaderType { get; init; } = string.Empty;

    public string SelectedVersion { get; init; } = string.Empty;
}

public sealed class ExtensionInstallOptions
{
    public bool OverrideMemory { get; init; }

    public bool AutoMemoryAllocation { get; init; }

    public double InitialHeapMemory { get; init; }

    public double MaximumHeapMemory { get; init; }

    public string JavaPath { get; init; } = string.Empty;

    public bool UseGlobalJavaSetting { get; init; }

    public bool OverrideResolution { get; init; }

    public int WindowWidth { get; init; }

    public int WindowHeight { get; init; }
}

public sealed class ExtensionInstallResult
{
    public VersionConfig SavedConfig { get; init; } = new();

    public bool NeedsReinstall { get; init; }

    public IReadOnlyList<LoaderSelection> SelectedLoaders { get; init; } = Array.Empty<LoaderSelection>();
}
