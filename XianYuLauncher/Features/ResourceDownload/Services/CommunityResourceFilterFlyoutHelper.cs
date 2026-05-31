using CommunityToolkit.Labs.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using XianYuLauncher.Controls;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ResourceDownload.ViewModels;
using XianYuLauncher.Models;

namespace XianYuLauncher.Features.ResourceDownload.Services;

public enum CommunityResourceFilterKind
{
    Mod,
    ShaderPack,
    ResourcePack,
    Datapack,
    Modpack,
    World
}

public sealed class CommunityResourceFilterFlyoutHelper
{
    private const string DefaultCategoryIconGlyph = "\uE8FD";

    public string CaptureOpeningSnapshot(
        CommunityResourceFilterKind kind,
        ResourceDownloadHostViewModel viewModel,
        ResourceFilterFlyout? control) =>
        kind switch
        {
            CommunityResourceFilterKind.Mod => GetModFilterSelectionStateKey(viewModel),
            CommunityResourceFilterKind.ShaderPack => GetShaderPackFilterSelectionStateKey(viewModel, control),
            CommunityResourceFilterKind.ResourcePack => GetResourcePackFilterSelectionStateKey(viewModel, control),
            CommunityResourceFilterKind.Datapack => GetDatapackFilterSelectionStateKey(viewModel, control),
            CommunityResourceFilterKind.Modpack => GetModpackFilterSelectionStateKey(viewModel, control),
            CommunityResourceFilterKind.World => GetWorldFilterSelectionStateKey(viewModel, control),
            _ => string.Empty
        };

    public bool HasSelectionChanged(
        string openingSnapshot,
        CommunityResourceFilterKind kind,
        ResourceDownloadHostViewModel viewModel,
        ResourceFilterFlyout? control) =>
        !string.Equals(openingSnapshot, CaptureOpeningSnapshot(kind, viewModel, control), StringComparison.Ordinal);

    public void RefreshTokenItems(
        CommunityResourceFilterKind kind,
        ResourceFilterFlyout? control,
        ResourceDownloadHostViewModel viewModel)
    {
        switch (kind)
        {
            case CommunityResourceFilterKind.Mod:
                RefreshModFilterTokenItems(control, viewModel);
                break;
            case CommunityResourceFilterKind.ShaderPack:
                RefreshShaderPackFilterTokenItems(control, viewModel);
                break;
            case CommunityResourceFilterKind.ResourcePack:
                RefreshResourcePackFilterTokenItems(control, viewModel);
                break;
            case CommunityResourceFilterKind.Datapack:
                RefreshDatapackFilterTokenItems(control, viewModel);
                break;
            case CommunityResourceFilterKind.Modpack:
                RefreshModpackFilterTokenItems(control, viewModel);
                break;
            case CommunityResourceFilterKind.World:
                RefreshWorldFilterTokenItems(control, viewModel);
                break;
        }
    }

    public void ApplySelectionFromControl(
        CommunityResourceFilterKind kind,
        ResourceFilterFlyout control,
        ResourceDownloadHostViewModel viewModel)
    {
        switch (kind)
        {
            case CommunityResourceFilterKind.Mod:
                ApplyModFilterSelectionFromControl(control, viewModel);
                break;
            case CommunityResourceFilterKind.ShaderPack:
                ApplyShaderPackFilterSelectionFromControl(control, viewModel);
                break;
            case CommunityResourceFilterKind.ResourcePack:
                ApplyResourcePackFilterSelectionFromControl(control, viewModel);
                break;
            case CommunityResourceFilterKind.Datapack:
                ApplyDatapackFilterSelectionFromControl(control, viewModel);
                break;
            case CommunityResourceFilterKind.Modpack:
                ApplyModpackFilterSelectionFromControl(control, viewModel);
                break;
            case CommunityResourceFilterKind.World:
                ApplyWorldFilterSelectionFromControl(control, viewModel);
                break;
        }
    }

    public static CommunityResourceFilterKind KindForTabIndex(int tabIndex) => tabIndex switch
    {
        1 => CommunityResourceFilterKind.Mod,
        2 => CommunityResourceFilterKind.ShaderPack,
        3 => CommunityResourceFilterKind.ResourcePack,
        4 => CommunityResourceFilterKind.Datapack,
        5 => CommunityResourceFilterKind.Modpack,
        6 => CommunityResourceFilterKind.World,
        _ => CommunityResourceFilterKind.Mod
    };

    public void RefreshModFilterTokenItems(ResourceFilterFlyout? control, ResourceDownloadHostViewModel viewModel)
    {
        if (control is null)
        {
            return;
        }

        control.LoadersSource = new ObservableCollection<TokenItem>(CreateLoaderTokenItems(viewModel.ModAvailableLoaders));
        control.CategoriesSource = new ObservableCollection<TokenItem>(CreateCategoryTokenItems(viewModel.ModCategories));
        control.VersionsSource = new ObservableCollection<TokenItem>(CreateVersionTokenItems(viewModel));
        control.SetSelectedLoaders(viewModel.SelectedLoaders);
        control.SetSelectedCategories(viewModel.SelectedModCategories);
        control.SetSelectedVersions(viewModel.SelectedVersions);
    }

    public string GetModFilterSelectionStateKey(ResourceDownloadHostViewModel viewModel)
    {
        var selectedLoaders = viewModel.SelectedLoaders.Count == 0
            ? "all"
            : string.Join(",", viewModel.SelectedLoaders.OrderBy(static tag => tag, StringComparer.OrdinalIgnoreCase));
        var selectedVersions = viewModel.SelectedVersions.Count == 0
            ? "all"
            : string.Join(",", viewModel.SelectedVersions.OrderBy(static tag => tag, StringComparer.OrdinalIgnoreCase));
        var selectedCategories = viewModel.SelectedModCategories.Count == 0
            ? "all"
            : string.Join(",", viewModel.SelectedModCategories.OrderBy(static tag => tag, StringComparer.OrdinalIgnoreCase));
        return $"{selectedLoaders}|{selectedVersions}|{selectedCategories}";
    }

    public void ApplyModFilterSelectionFromControl(ResourceFilterFlyout control, ResourceDownloadHostViewModel viewModel)
    {
        viewModel.SelectedLoaders = new ObservableCollection<string>(control.SelectedLoaderTags);
        viewModel.SelectedModCategories = new ObservableCollection<string>(control.SelectedCategoryTags);
        viewModel.SelectedVersions = new ObservableCollection<string>(control.SelectedVersionTags);
    }

    public void RefreshShaderPackFilterTokenItems(ResourceFilterFlyout? control, ResourceDownloadHostViewModel viewModel)
    {
        if (control is null)
        {
            return;
        }

        control.LoadersSource = new ObservableCollection<TokenItem>(CreateLoaderTokenItems(viewModel.ShaderPackAvailableLoaders));
        control.CategoriesSource = new ObservableCollection<TokenItem>(CreateCategoryTokenItems(viewModel.ShaderPackCategories));
        control.VersionsSource = new ObservableCollection<TokenItem>(CreateVersionTokenItems(viewModel));
        control.SetSelectedLoaders(viewModel.SelectedShaderPackLoaders);
        control.SetSelectedCategories(viewModel.SelectedShaderPackCategories);
        control.SetSelectedVersions(viewModel.SelectedShaderPackVersions);
        control.IsShowAllVersions = viewModel.IsShowAllVersions;
    }

    public string GetShaderPackFilterSelectionStateKey(ResourceDownloadHostViewModel viewModel, ResourceFilterFlyout? control) =>
        control is null
            ? string.Empty
            : $"{string.Join(",", viewModel.SelectedShaderPackLoaders)}|{string.Join(",", viewModel.SelectedShaderPackCategories)}|{string.Join(",", viewModel.SelectedShaderPackVersions)}|{viewModel.IsShowAllVersions}";

    public void ApplyShaderPackFilterSelectionFromControl(ResourceFilterFlyout control, ResourceDownloadHostViewModel viewModel)
    {
        viewModel.SelectedShaderPackLoaders = new ObservableCollection<string>(control.SelectedLoaderTags);
        viewModel.SelectedShaderPackCategories = new ObservableCollection<string>(control.SelectedCategoryTags);
        viewModel.SelectedShaderPackVersions = new ObservableCollection<string>(control.SelectedVersionTags);
    }

    public void RefreshResourcePackFilterTokenItems(ResourceFilterFlyout? control, ResourceDownloadHostViewModel viewModel)
    {
        if (control is null)
        {
            return;
        }

        control.LoadersSource = new ObservableCollection<TokenItem>(CreateLoaderTokenItems(viewModel.ResourcePackAvailableLoaders));
        control.CategoriesSource = new ObservableCollection<TokenItem>(CreateCategoryTokenItems(viewModel.ResourcePackCategories));
        control.VersionsSource = new ObservableCollection<TokenItem>(CreateVersionTokenItems(viewModel));
        control.SetSelectedLoaders(viewModel.SelectedResourcePackLoaders);
        control.SetSelectedCategories(viewModel.SelectedResourcePackCategories);
        control.SetSelectedVersions(viewModel.SelectedResourcePackVersions);
        control.IsShowAllVersions = viewModel.IsShowAllVersions;
    }

    public string GetResourcePackFilterSelectionStateKey(ResourceDownloadHostViewModel viewModel, ResourceFilterFlyout? control) =>
        control is null
            ? string.Empty
            : $"{string.Join(",", viewModel.SelectedResourcePackLoaders)}|{string.Join(",", viewModel.SelectedResourcePackCategories)}|{string.Join(",", viewModel.SelectedResourcePackVersions)}|{viewModel.IsShowAllVersions}";

    public void ApplyResourcePackFilterSelectionFromControl(ResourceFilterFlyout control, ResourceDownloadHostViewModel viewModel)
    {
        viewModel.SelectedResourcePackLoaders = new ObservableCollection<string>(control.SelectedLoaderTags);
        viewModel.SelectedResourcePackCategories = new ObservableCollection<string>(control.SelectedCategoryTags);
        viewModel.SelectedResourcePackVersions = new ObservableCollection<string>(control.SelectedVersionTags);
    }

    public void RefreshDatapackFilterTokenItems(ResourceFilterFlyout? control, ResourceDownloadHostViewModel viewModel)
    {
        if (control is null)
        {
            return;
        }

        control.LoadersSource = new ObservableCollection<TokenItem>(CreateLoaderTokenItems(viewModel.DatapackAvailableLoaders));
        control.CategoriesSource = new ObservableCollection<TokenItem>(CreateCategoryTokenItems(viewModel.DatapackCategories));
        control.VersionsSource = new ObservableCollection<TokenItem>(CreateVersionTokenItems(viewModel));
        control.SetSelectedLoaders(viewModel.SelectedDatapackLoaders);
        control.SetSelectedCategories(viewModel.SelectedDatapackCategories);
        control.SetSelectedVersions(viewModel.SelectedDatapackVersions);
        control.IsShowAllVersions = viewModel.IsShowAllVersions;
    }

    public string GetDatapackFilterSelectionStateKey(ResourceDownloadHostViewModel viewModel, ResourceFilterFlyout? control) =>
        control is null
            ? string.Empty
            : $"{string.Join(",", viewModel.SelectedDatapackLoaders)}|{string.Join(",", viewModel.SelectedDatapackCategories)}|{string.Join(",", viewModel.SelectedDatapackVersions)}|{viewModel.IsShowAllVersions}";

    public void ApplyDatapackFilterSelectionFromControl(ResourceFilterFlyout control, ResourceDownloadHostViewModel viewModel)
    {
        viewModel.SelectedDatapackLoaders = new ObservableCollection<string>(control.SelectedLoaderTags);
        viewModel.SelectedDatapackCategories = new ObservableCollection<string>(control.SelectedCategoryTags);
        viewModel.SelectedDatapackVersions = new ObservableCollection<string>(control.SelectedVersionTags);
    }

    public void RefreshModpackFilterTokenItems(ResourceFilterFlyout? control, ResourceDownloadHostViewModel viewModel)
    {
        if (control is null)
        {
            return;
        }

        control.LoadersSource = new ObservableCollection<TokenItem>(CreateLoaderTokenItems(viewModel.ModpackAvailableLoaders));
        control.CategoriesSource = new ObservableCollection<TokenItem>(CreateCategoryTokenItems(viewModel.ModpackCategories));
        control.VersionsSource = new ObservableCollection<TokenItem>(CreateVersionTokenItems(viewModel));
        control.SetSelectedLoaders(viewModel.SelectedModpackLoaders);
        control.SetSelectedCategories(viewModel.SelectedModpackCategories);
        control.SetSelectedVersions(viewModel.SelectedModpackVersions);
        control.IsShowAllVersions = viewModel.IsShowAllVersions;
    }

    public string GetModpackFilterSelectionStateKey(ResourceDownloadHostViewModel viewModel, ResourceFilterFlyout? control) =>
        control is null
            ? string.Empty
            : $"{string.Join(",", viewModel.SelectedModpackLoaders)}|{string.Join(",", viewModel.SelectedModpackCategories)}|{string.Join(",", viewModel.SelectedModpackVersions)}|{viewModel.IsShowAllVersions}";

    public void ApplyModpackFilterSelectionFromControl(ResourceFilterFlyout control, ResourceDownloadHostViewModel viewModel)
    {
        viewModel.SelectedModpackLoaders = new ObservableCollection<string>(control.SelectedLoaderTags);
        viewModel.SelectedModpackCategories = new ObservableCollection<string>(control.SelectedCategoryTags);
        viewModel.SelectedModpackVersions = new ObservableCollection<string>(control.SelectedVersionTags);
    }

    public void RefreshWorldFilterTokenItems(ResourceFilterFlyout? control, ResourceDownloadHostViewModel viewModel)
    {
        if (control is null)
        {
            return;
        }

        control.LoadersSource = new ObservableCollection<TokenItem>(CreateLoaderTokenItems(viewModel.WorldAvailableLoaders));
        control.CategoriesSource = new ObservableCollection<TokenItem>(CreateCategoryTokenItems(viewModel.WorldCategories));
        control.VersionsSource = new ObservableCollection<TokenItem>(CreateVersionTokenItems(viewModel));
        control.SetSelectedLoaders(viewModel.SelectedWorldLoaders);
        control.SetSelectedCategories(viewModel.SelectedWorldCategories);
        control.SetSelectedVersions(viewModel.SelectedWorldVersions);
        control.IsShowAllVersions = viewModel.IsShowAllVersions;
    }

    public string GetWorldFilterSelectionStateKey(ResourceDownloadHostViewModel viewModel, ResourceFilterFlyout? control) =>
        control is null
            ? string.Empty
            : $"{string.Join(",", viewModel.SelectedWorldLoaders)}|{string.Join(",", viewModel.SelectedWorldCategories)}|{string.Join(",", viewModel.SelectedWorldVersions)}|{viewModel.IsShowAllVersions}";

    public void ApplyWorldFilterSelectionFromControl(ResourceFilterFlyout control, ResourceDownloadHostViewModel viewModel)
    {
        viewModel.SelectedWorldLoaders = new ObservableCollection<string>(control.SelectedLoaderTags);
        viewModel.SelectedWorldCategories = new ObservableCollection<string>(control.SelectedCategoryTags);
        viewModel.SelectedWorldVersions = new ObservableCollection<string>(control.SelectedVersionTags);
    }

    public List<TokenItem> CreateLoaderTokenItems(IEnumerable<string>? availableLoaders)
    {
        var items = new List<TokenItem>
        {
            new()
            {
                Content = "所有加载器",
                Tag = "all",
                Icon = new FontIcon { Glyph = "\uE71D" },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4)
            }
        };

        if (availableLoaders == null)
        {
            return items;
        }

        foreach (var loader in availableLoaders.Where(static loader => !string.IsNullOrWhiteSpace(loader)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(loader, "all", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new TokenItem
            {
                Content = GetLoaderDisplayName(loader),
                Tag = loader,
                Icon = new FontIcon { Glyph = GetLoaderGlyph(loader) },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4)
            });
        }

        return items;
    }

    public List<TokenItem> CreateCategoryTokenItems(IEnumerable<CategoryItem> categories)
    {
        var items = new List<TokenItem>();

        foreach (var category in categories)
        {
            items.Add(new TokenItem
            {
                Content = category.DisplayName,
                Tag = category.Tag,
                Icon = new FontIcon { Glyph = GetCategoryGlyph(category.Tag) },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4)
            });
        }

        return items;
    }

    public List<TokenItem> CreateVersionTokenItems(ResourceDownloadHostViewModel viewModel)
    {
        var items = new List<TokenItem>
        {
            new()
            {
                Content = "所有版本",
                Tag = "all",
                Icon = new FontIcon { Glyph = "\uE71D" },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4)
            }
        };

        foreach (var version in viewModel.AvailableVersions)
        {
            items.Add(new TokenItem
            {
                Content = version,
                Tag = version,
                Icon = new FontIcon { Glyph = "\uE8FD" },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4)
            });
        }

        return items;
    }

    private static string GetLoaderDisplayName(string loader) =>
        loader.ToLowerInvariant() switch
        {
            "legacy-fabric" => "Legacy Fabric",
            "liteloader" => "LiteLoader",
            "neoforge" => "NeoForge",
            _ => string.IsNullOrWhiteSpace(loader)
                ? "未知加载器"
                : char.ToUpperInvariant(loader[0]) + loader[1..]
        };

    private static string GetLoaderGlyph(string loader) =>
        loader.ToLowerInvariant() switch
        {
            "all" => "\uE71D",
            "fabric" => "\uE8D2",
            "forge" => "\uE7FC",
            "quilt" => "\uE8FD",
            "legacy-fabric" => "\uE8FD",
            "liteloader" => "\uE9CE",
            "neoforge" => "\uE7FC",
            _ => "\uE8FD"
        };

    private static string GetCategoryGlyph(string? categoryTag)
    {
        if (string.IsNullOrWhiteSpace(categoryTag))
        {
            return DefaultCategoryIconGlyph;
        }

        return categoryTag.ToLowerInvariant() switch
        {
            "all" => "\uE71D",
            "adventure" => "\uE7FC",
            "cursed" => "\uE814",
            "decoration" => "\uECA5",
            "economy" => "\uE8EF",
            "equipment" => "\uE8D7",
            "food" => "\uE719",
            "game-mechanics" => "\uE7FC",
            "library" => "\uE8F1",
            "magic" => "\uEA8C",
            "management" => "\uE78B",
            "minigame" => "\uE7FC",
            "mobs" => "\uE825",
            "optimization" => "\uE9D9",
            "social" => "\uE716",
            "storage" => "\uE8B7",
            "technology" => "\uE772",
            "transportation" => "\uEC4A",
            "utility" => "\uE90F",
            "worldgen" => "\uE909",
            _ => DefaultCategoryIconGlyph
        };
    }
}
