using System.Collections.ObjectModel;
using System.Text.Json;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.Features.VersionList.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class LoaderUiOrchestrator : ILoaderUiOrchestrator
{
    private readonly IVersionSettingsOrchestrator _versionSettingsOrchestrator;

    public LoaderUiOrchestrator(IVersionSettingsOrchestrator versionSettingsOrchestrator)
    {
        _versionSettingsOrchestrator = versionSettingsOrchestrator;
    }

    public void ApplyMutualExclusion(LoaderItemViewModel selectedLoader, ObservableCollection<LoaderItemViewModel> allLoaders)
    {
        if (string.IsNullOrEmpty(selectedLoader.SelectedVersion))
        {
            return;
        }

        var currentLoaderType = selectedLoader.LoaderType.ToLowerInvariant();
        var forgeGroup = new HashSet<string> { "forge", "optifine", "liteloader" };

        foreach (var otherLoader in allLoaders)
        {
            if (otherLoader == selectedLoader || string.IsNullOrEmpty(otherLoader.SelectedVersion))
            {
                continue;
            }

            var otherLoaderType = otherLoader.LoaderType.ToLowerInvariant();
            var shouldClear = !(forgeGroup.Contains(currentLoaderType) && forgeGroup.Contains(otherLoaderType));

            if (shouldClear)
            {
                otherLoader.SelectedVersion = null;
                otherLoader.IsExpanded = false;
            }
        }
    }

    public LoaderDisplayState BuildDisplayState(VersionSettings? settings)
    {
        if (settings == null || string.IsNullOrEmpty(settings.ModLoaderType) || settings.ModLoaderType == "vanilla")
        {
            return new LoaderDisplayState
            {
                CurrentLoaderDisplayName = "VersionManagement_Vanilla".GetLocalized(),
                CurrentLoaderVersion = settings?.MinecraftVersion ?? string.Empty,
                IsVanillaLoader = true
            };
        }

        var displayName = settings.ModLoaderType switch
        {
            "fabric" => "Fabric",
            "legacyfabric" => "Legacy Fabric",
            "LegacyFabric" => "Legacy Fabric",
            "forge" => "Forge",
            "neoforge" => "NeoForge",
            "quilt" => "Quilt",
            "cleanroom" => "Cleanroom",
            "optifine" => "OptiFine",
            "liteloader" => "LiteLoader",
            "LiteLoader" => "LiteLoader",
            _ => settings.ModLoaderType
        };

        var primaryIconUrl = GetLoaderIconUrl(settings.ModLoaderType);
        var icons = new List<LoaderIconInfo>();

        if (primaryIconUrl != null)
        {
            icons.Add(new LoaderIconInfo
            {
                Name = displayName,
                IconUrl = primaryIconUrl,
                Version = settings.ModLoaderVersion ?? string.Empty
            });
        }

        if (!string.IsNullOrEmpty(settings.OptifineVersion)
            && !settings.ModLoaderType.Equals("optifine", StringComparison.OrdinalIgnoreCase))
        {
            icons.Add(new LoaderIconInfo
            {
                Name = "OptiFine",
                IconUrl = AppAssetResolver.ToUriString(AppAssetResolver.OptifineIconAssetPath),
                Version = settings.OptifineVersion
            });
        }

        if (!string.IsNullOrEmpty(settings.LiteLoaderVersion)
            && !settings.ModLoaderType.Equals("liteloader", StringComparison.OrdinalIgnoreCase)
            && !settings.ModLoaderType.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase))
        {
            icons.Add(new LoaderIconInfo
            {
                Name = "LiteLoader",
                IconUrl = AppAssetResolver.ToUriString(AppAssetResolver.LiteLoaderIconAssetPath),
                Version = settings.LiteLoaderVersion
            });
        }

        var mergedDisplayName = icons.Count > 1 ? string.Join(" + ", icons.Select(icon => icon.Name)) : displayName;

        return new LoaderDisplayState
        {
            CurrentLoaderDisplayName = mergedDisplayName,
            CurrentLoaderVersion = settings.ModLoaderVersion ?? string.Empty,
            CurrentLoaderIconUrl = primaryIconUrl,
            IsVanillaLoader = false,
            CurrentLoaderIcons = icons
        };
    }

    public async Task InitializeAvailableLoadersAsync(
        ObservableCollection<LoaderItemViewModel> availableLoaders,
        VersionListViewModel.VersionInfoItem? selectedVersion,
        string settingsFilePath,
        Func<string, bool> isLoaderInstalled,
        Func<Task<string>> getMinecraftVersionAsync,
        Func<LoaderItemViewModel, Task> loadLoaderVersionsAsync)
    {
        availableLoaders.Clear();

        var minecraftVersion = await getMinecraftVersionAsync();

        availableLoaders.Add(new LoaderItemViewModel
        {
            Name = "Fabric",
            LoaderType = "fabric",
            IconUrl = GetLoaderIconUrl("fabric") ?? string.Empty,
            IsInstalled = isLoaderInstalled("fabric")
        });

        availableLoaders.Add(new LoaderItemViewModel
        {
            Name = "Forge",
            LoaderType = "forge",
            IconUrl = GetLoaderIconUrl("forge") ?? string.Empty,
            IsInstalled = isLoaderInstalled("forge")
        });

        availableLoaders.Add(new LoaderItemViewModel
        {
            Name = "NeoForge",
            LoaderType = "neoforge",
            IconUrl = GetLoaderIconUrl("neoforge") ?? string.Empty,
            IsInstalled = isLoaderInstalled("neoforge")
        });

        availableLoaders.Add(new LoaderItemViewModel
        {
            Name = "Quilt",
            LoaderType = "quilt",
            IconUrl = GetLoaderIconUrl("quilt") ?? string.Empty,
            IsInstalled = isLoaderInstalled("quilt")
        });

        availableLoaders.Add(new LoaderItemViewModel
        {
            Name = "OptiFine",
            LoaderType = "optifine",
            IconUrl = GetLoaderIconUrl("optifine") ?? string.Empty,
            IsInstalled = isLoaderInstalled("optifine")
        });

        if (minecraftVersion == "1.12.2")
        {
            availableLoaders.Add(new LoaderItemViewModel
            {
                Name = "Cleanroom",
                LoaderType = "cleanroom",
                IconUrl = GetLoaderIconUrl("cleanroom") ?? string.Empty,
                IsInstalled = isLoaderInstalled("cleanroom")
            });
        }

        if (_versionSettingsOrchestrator.IsVersionBelow1_14(minecraftVersion))
        {
            availableLoaders.Add(new LoaderItemViewModel
            {
                Name = "Legacy Fabric",
                LoaderType = "LegacyFabric",
                IconUrl = GetLoaderIconUrl("LegacyFabric") ?? string.Empty,
                IsInstalled = isLoaderInstalled("legacyfabric")
            });

            availableLoaders.Add(new LoaderItemViewModel
            {
                Name = "LiteLoader",
                LoaderType = "liteloader",
                IconUrl = GetLoaderIconUrl("liteloader") ?? string.Empty,
                IsInstalled = isLoaderInstalled("liteloader")
            });
        }

        VersionSettings? settings = null;
        try
        {
            if (File.Exists(settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(settingsFilePath);
                settings = JsonSerializer.Deserialize<VersionSettings>(json);
            }
        }
        catch
        {
        }

        foreach (var loader in availableLoaders)
        {
            var shouldSetup = false;
            string? targetVersion = null;

            if (settings != null)
            {
                if (string.Equals(settings.ModLoaderType, loader.LoaderType, StringComparison.OrdinalIgnoreCase))
                {
                    shouldSetup = true;
                    targetVersion = settings.ModLoaderVersion;
                }
                else if (loader.LoaderType.Equals("optifine", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrEmpty(settings.OptifineVersion))
                {
                    shouldSetup = true;
                    targetVersion = settings.OptifineVersion;
                }
                else if (loader.LoaderType.Equals("liteloader", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrEmpty(settings.LiteLoaderVersion))
                {
                    shouldSetup = true;
                    targetVersion = settings.LiteLoaderVersion;
                }
            }
            else if (loader.IsInstalled)
            {
                shouldSetup = true;
            }

            if (!shouldSetup)
            {
                continue;
            }

            loader.IsExpanded = false;
            await loadLoaderVersionsAsync(loader);

            if (!string.IsNullOrEmpty(targetVersion))
            {
                loader.SelectedVersion = targetVersion;
            }
            else if (loader.Versions != null && loader.Versions.Any())
            {
                var currentId = selectedVersion?.Name ?? string.Empty;
                var match = loader.Versions.FirstOrDefault(version => currentId.Contains(version, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    loader.SelectedVersion = match;
                }
            }
        }
    }

    public async Task<List<string>> GetLoaderVersionsAsync(string loaderType, string minecraftVersion)
    {
        return await _versionSettingsOrchestrator.GetLoaderVersionsAsync(loaderType, minecraftVersion);
    }

    private static string? GetLoaderIconUrl(string loaderType)
    {
        if (AppAssetResolver.TryGetLoaderIconAssetPath(loaderType, out var iconAssetPath))
        {
            return AppAssetResolver.ToUriString(iconAssetPath);
        }

        return null;
    }
}