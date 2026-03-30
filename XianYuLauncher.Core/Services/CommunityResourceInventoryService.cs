using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public sealed class CommunityResourceInventoryService : ICommunityResourceInventoryService
{
    private const string WorldUnsupportedReason = "世界资源已纳入实例清单，但首版仅支持扫描，不支持 AI 检查或执行更新。";
    private const string DataPackUnsupportedReason = "数据包已纳入实例清单，但首版仅支持扫描，不支持 AI 检查或执行更新。";

    private static readonly string[] DefaultResourceTypes =
    [
        "mod",
        "shader",
        "resourcepack",
        "world",
        "datapack"
    ];

    private static readonly Dictionary<string, int> ResourceTypeOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mod"] = 0,
        ["shader"] = 1,
        ["resourcepack"] = 2,
        ["world"] = 3,
        ["datapack"] = 4,
    };

    private readonly ICommunityResourceMetadataService _metadataService;
    private readonly WorldDataService _worldDataService;

    public CommunityResourceInventoryService(
        ICommunityResourceMetadataService metadataService,
        WorldDataService worldDataService)
    {
        _metadataService = metadataService;
        _worldDataService = worldDataService;
    }

    public async Task<CommunityResourceInventoryResult> ListAsync(
        CommunityResourceInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TargetVersionName))
        {
            throw new ArgumentException("TargetVersionName 不能为空。", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ResolvedGameDirectory))
        {
            throw new ArgumentException("ResolvedGameDirectory 不能为空。", nameof(request));
        }

        string targetVersionName = request.TargetVersionName.Trim();
        string resolvedGameDirectory = Path.GetFullPath(request.ResolvedGameDirectory.Trim());
        HashSet<string> requestedResourceTypes = NormalizeRequestedResourceTypes(request.ResourceTypes);

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<WorldScanContext> worldContexts = requestedResourceTypes.Contains("world") || requestedResourceTypes.Contains("datapack")
            ? ScanWorldContexts(resolvedGameDirectory)
            : Array.Empty<WorldScanContext>();

        List<InventoryCandidate> candidates = [];
        if (requestedResourceTypes.Contains("mod"))
        {
            candidates.AddRange(ScanModCandidates(resolvedGameDirectory));
        }

        if (requestedResourceTypes.Contains("shader"))
        {
            candidates.AddRange(ScanShaderCandidates(resolvedGameDirectory));
        }

        if (requestedResourceTypes.Contains("resourcepack"))
        {
            candidates.AddRange(ScanResourcePackCandidates(resolvedGameDirectory));
        }

        if (requestedResourceTypes.Contains("world"))
        {
            candidates.AddRange(worldContexts.Select(world => new InventoryCandidate(
                ResourceType: "world",
                FilePath: world.WorldDirectoryPath,
                DisplayName: world.DisplayName,
                WorldName: world.DisplayName)));
        }

        if (requestedResourceTypes.Contains("datapack"))
        {
            candidates.AddRange(ScanDataPackCandidates(worldContexts));
        }

        CommunityResourceInventoryItem[] items = await Task.WhenAll(
            candidates.Select(candidate => BuildInventoryItemAsync(targetVersionName, resolvedGameDirectory, candidate, cancellationToken)))
            .ConfigureAwait(false);

        return new CommunityResourceInventoryResult
        {
            TargetVersionName = targetVersionName,
            ResolvedGameDirectory = resolvedGameDirectory,
            Resources = items
                .OrderBy(item => GetResourceTypeOrder(item.ResourceType))
                .ThenBy(item => item.WorldName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private async Task<CommunityResourceInventoryItem> BuildInventoryItemAsync(
        string targetVersionName,
        string resolvedGameDirectory,
        InventoryCandidate candidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool isDirectory = Directory.Exists(candidate.FilePath);
        CommunityResourceResolvedMetadata? metadata = await ResolveMetadataAsync(candidate.FilePath, isDirectory, cancellationToken).ConfigureAwait(false);
        PackMetadata packMetadata = candidate.ResourceType is "resourcepack" or "datapack"
            ? ReadPackMetadata(candidate.FilePath)
            : PackMetadata.Empty;

        string relativePath = NormalizeRelativePath(Path.GetRelativePath(resolvedGameDirectory, candidate.FilePath));
        string resourceType = candidate.ResourceType;

        return new CommunityResourceInventoryItem
        {
            TargetVersionName = targetVersionName,
            ResolvedGameDirectory = resolvedGameDirectory,
            ResourceType = resourceType,
            ResourceInstanceId = $"{resourceType}:{relativePath}",
            DisplayName = candidate.DisplayName,
            FilePath = candidate.FilePath,
            RelativePath = relativePath,
            IsDirectory = isDirectory,
            Source = metadata?.Source,
            ProjectId = metadata?.ProjectId,
            Description = !string.IsNullOrWhiteSpace(metadata?.Description) ? metadata.Description : packMetadata.Description,
            CurrentVersionHint = resourceType == "world" ? null : BuildCurrentVersionHint(candidate.FilePath, isDirectory),
            WorldName = candidate.WorldName,
            PackFormat = packMetadata.PackFormat,
            UpdateSupport = IsUnsupportedForUpdate(resourceType) ? "unsupported" : "supported",
            UpdateUnsupportedReason = GetUnsupportedReason(resourceType)
        };
    }

    private async Task<CommunityResourceResolvedMetadata?> ResolveMetadataAsync(
        string filePath,
        bool isDirectory,
        CancellationToken cancellationToken)
    {
        if (isDirectory || !File.Exists(filePath))
        {
            return null;
        }

        return await _metadataService.GetMetadataAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyList<WorldScanContext> ScanWorldContexts(string resolvedGameDirectory)
    {
        string savesDirectory = Path.Combine(resolvedGameDirectory, MinecraftPathConsts.Saves);
        if (!Directory.Exists(savesDirectory))
        {
            return Array.Empty<WorldScanContext>();
        }

        List<WorldScanContext> worlds = [];
        foreach (string worldDirectory in Directory.GetDirectories(savesDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string folderName = Path.GetFileName(worldDirectory);
            WorldData? worldData = _worldDataService.ReadWorldData(worldDirectory);
            string displayName = ResolveWorldDisplayName(folderName, worldData);
            worlds.Add(new WorldScanContext(worldDirectory, displayName));
        }

        return worlds;
    }

    private static IEnumerable<InventoryCandidate> ScanModCandidates(string resolvedGameDirectory)
    {
        string modsDirectory = Path.Combine(resolvedGameDirectory, MinecraftPathConsts.Mods);
        if (!Directory.Exists(modsDirectory))
        {
            return [];
        }

        return Directory.GetFiles(modsDirectory)
            .Where(IsModInventoryFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new InventoryCandidate("mod", path, BuildDisplayName(path, isDirectory: false), null));
    }

    private static IEnumerable<InventoryCandidate> ScanShaderCandidates(string resolvedGameDirectory)
    {
        string shaderDirectory = Path.Combine(resolvedGameDirectory, MinecraftPathConsts.ShaderPacks);
        if (!Directory.Exists(shaderDirectory))
        {
            return [];
        }

        IEnumerable<string> directories = Directory.GetDirectories(shaderDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> zipFiles = Directory.GetFiles(shaderDirectory, $"*{FileExtensionConsts.Zip}").OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        return directories
            .Concat(zipFiles)
            .Select(path => new InventoryCandidate("shader", path, BuildDisplayName(path, Directory.Exists(path)), null));
    }

    private static IEnumerable<InventoryCandidate> ScanResourcePackCandidates(string resolvedGameDirectory)
    {
        string resourcePackDirectory = Path.Combine(resolvedGameDirectory, MinecraftPathConsts.ResourcePacks);
        if (!Directory.Exists(resourcePackDirectory))
        {
            return [];
        }

        IEnumerable<string> directories = Directory.GetDirectories(resourcePackDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> zipFiles = Directory.GetFiles(resourcePackDirectory, $"*{FileExtensionConsts.Zip}").OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        return directories
            .Concat(zipFiles)
            .Select(path => new InventoryCandidate("resourcepack", path, BuildDisplayName(path, Directory.Exists(path)), null));
    }

    private static IEnumerable<InventoryCandidate> ScanDataPackCandidates(IEnumerable<WorldScanContext> worlds)
    {
        List<InventoryCandidate> candidates = [];
        foreach (WorldScanContext world in worlds)
        {
            string dataPackDirectory = Path.Combine(world.WorldDirectoryPath, MinecraftPathConsts.Datapacks);
            if (!Directory.Exists(dataPackDirectory))
            {
                continue;
            }

            foreach (string directory in Directory.GetDirectories(dataPackDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(new InventoryCandidate("datapack", directory, BuildDisplayName(directory, isDirectory: true), world.DisplayName));
            }

            foreach (string zipFile in Directory.GetFiles(dataPackDirectory, $"*{FileExtensionConsts.Zip}").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(new InventoryCandidate("datapack", zipFile, BuildDisplayName(zipFile, isDirectory: false), world.DisplayName));
            }
        }

        return candidates;
    }

    private static HashSet<string> NormalizeRequestedResourceTypes(IReadOnlyCollection<string>? resourceTypes)
    {
        if (resourceTypes == null || resourceTypes.Count == 0)
        {
            return new HashSet<string>(DefaultResourceTypes, StringComparer.OrdinalIgnoreCase);
        }

        HashSet<string> normalized = new(StringComparer.OrdinalIgnoreCase);
        List<string> invalidTypes = [];

        foreach (string? rawType in resourceTypes)
        {
            if (string.IsNullOrWhiteSpace(rawType))
            {
                invalidTypes.Add(rawType ?? string.Empty);
                continue;
            }

            string normalizedType = ModResourcePathHelper.NormalizeProjectType(rawType.Trim());
            if (DefaultResourceTypes.Contains(normalizedType, StringComparer.OrdinalIgnoreCase))
            {
                normalized.Add(normalizedType);
            }
            else
            {
                invalidTypes.Add(rawType);
            }
        }

        if (invalidTypes.Count > 0)
        {
            throw new ArgumentException($"存在不支持的资源类型：{string.Join(", ", invalidTypes)}。", nameof(resourceTypes));
        }

        return normalized;
    }

    private static PackMetadata ReadPackMetadata(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                string packMetadataPath = Path.Combine(path, "pack.mcmeta");
                return File.Exists(packMetadataPath)
                    ? ParsePackMetadata(File.ReadAllText(packMetadataPath))
                    : PackMetadata.Empty;
            }

            if (!File.Exists(path) || !Path.GetExtension(path).Equals(FileExtensionConsts.Zip, StringComparison.OrdinalIgnoreCase))
            {
                return PackMetadata.Empty;
            }

            using ZipArchive archive = ZipFile.OpenRead(path);
            ZipArchiveEntry? packMetadataEntry = archive.GetEntry("pack.mcmeta");
            if (packMetadataEntry == null)
            {
                return PackMetadata.Empty;
            }

            using Stream stream = packMetadataEntry.Open();
            using StreamReader reader = new(stream);
            return ParsePackMetadata(reader.ReadToEnd());
        }
        catch
        {
            return PackMetadata.Empty;
        }
    }

    private static PackMetadata ParsePackMetadata(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("pack", out JsonElement packElement))
            {
                return PackMetadata.Empty;
            }

            string? description = null;
            if (packElement.TryGetProperty("description", out JsonElement descriptionElement))
            {
                description = ExtractTextComponent(descriptionElement);
            }

            int? packFormat = null;
            if (packElement.TryGetProperty("pack_format", out JsonElement formatElement)
                && formatElement.ValueKind == JsonValueKind.Number
                && formatElement.TryGetInt32(out int parsedFormat))
            {
                packFormat = parsedFormat;
            }

            return new PackMetadata(description, packFormat);
        }
        catch
        {
            return PackMetadata.Empty;
        }
    }

    private static string? ExtractTextComponent(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Array:
                return string.Concat(element.EnumerateArray().Select(ExtractTextComponent).Where(text => !string.IsNullOrWhiteSpace(text)));
            case JsonValueKind.Object:
            {
                List<string> parts = [];
                if (element.TryGetProperty("text", out JsonElement textElement) && textElement.ValueKind == JsonValueKind.String)
                {
                    string? text = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        parts.Add(text);
                    }
                }

                if (element.TryGetProperty("translate", out JsonElement translateElement) && translateElement.ValueKind == JsonValueKind.String)
                {
                    string? translate = translateElement.GetString();
                    if (!string.IsNullOrWhiteSpace(translate))
                    {
                        parts.Add(translate);
                    }
                }

                if (element.TryGetProperty("extra", out JsonElement extraElement))
                {
                    string? extraText = ExtractTextComponent(extraElement);
                    if (!string.IsNullOrWhiteSpace(extraText))
                    {
                        parts.Add(extraText);
                    }
                }

                return parts.Count == 0 ? null : string.Concat(parts);
            }
            default:
                return null;
        }
    }

    private static bool IsModInventoryFile(string path)
    {
        return path.EndsWith(FileExtensionConsts.Jar, StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(FileExtensionConsts.JarDisabled, StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".litemod", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".litemod.disabled", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveWorldDisplayName(string folderName, WorldData? worldData)
    {
        string? levelName = worldData?.LevelName;
        if (!string.IsNullOrWhiteSpace(levelName) && !string.Equals(levelName, "未知", StringComparison.Ordinal))
        {
            return levelName;
        }

        return folderName;
    }

    private static string BuildDisplayName(string path, bool isDirectory)
    {
        string name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            return path;
        }

        if (name.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^FileExtensionConsts.Disabled.Length];
        }

        if (!isDirectory)
        {
            name = Path.GetFileNameWithoutExtension(name);
        }

        return name;
    }

    private static string? BuildCurrentVersionHint(string path, bool isDirectory)
    {
        string name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (name.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^FileExtensionConsts.Disabled.Length];
        }

        return isDirectory ? name : name;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath
            .Replace('\\', '/')
            .TrimStart('/');
    }

    private static int GetResourceTypeOrder(string resourceType)
    {
        return ResourceTypeOrder.TryGetValue(resourceType, out int order)
            ? order
            : int.MaxValue;
    }

    private static bool IsUnsupportedForUpdate(string resourceType)
    {
        return resourceType is "world" or "datapack";
    }

    private static string? GetUnsupportedReason(string resourceType)
    {
        return resourceType switch
        {
            "world" => WorldUnsupportedReason,
            "datapack" => DataPackUnsupportedReason,
            _ => null,
        };
    }

    private sealed record InventoryCandidate(
        string ResourceType,
        string FilePath,
        string DisplayName,
        string? WorldName);

    private sealed record WorldScanContext(
        string WorldDirectoryPath,
        string DisplayName);

    private readonly record struct PackMetadata(string? Description, int? PackFormat)
    {
        public static PackMetadata Empty => new(null, null);
    }
}