using System.IO.Compression;
using FluentAssertions;
using fNbt;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class CommunityResourceInventoryServiceTests : IDisposable
{
    private readonly string _gameDirectory;
    private readonly FakeCommunityResourceMetadataService _metadataService;
    private readonly CommunityResourceInventoryService _service;

    public CommunityResourceInventoryServiceTests()
    {
        _gameDirectory = Path.Combine(Path.GetTempPath(), $"CommunityResourceInventoryTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_gameDirectory);
        _metadataService = new FakeCommunityResourceMetadataService();
        _service = new CommunityResourceInventoryService(_metadataService, new WorldDataService());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_gameDirectory))
            {
                Directory.Delete(_gameDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task ListAsync_EmptyInstance_ReturnsEmptyInventory()
    {
        CommunityResourceInventoryResult result = await _service.ListAsync(new CommunityResourceInventoryRequest
        {
            TargetVersionName = "TestInstance",
            ResolvedGameDirectory = _gameDirectory,
        });

        result.TargetVersionName.Should().Be("TestInstance");
        result.ResolvedGameDirectory.Should().Be(Path.GetFullPath(_gameDirectory));
        result.Resources.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_FileResources_ReturnsStableIdsAndMetadata()
    {
        string modFile = CreateFile(Path.Combine(_gameDirectory, "mods", "alpha.jar"));
        string disabledModFile = CreateFile(Path.Combine(_gameDirectory, "mods", "beta.jar.disabled"));
        string shaderZip = CreateZip(Path.Combine(_gameDirectory, "shaderpacks", "SuperShader.zip"));
        string shaderDirectory = CreateDirectory(Path.Combine(_gameDirectory, "shaderpacks", "FolderShader"));
        string resourcePackZip = CreateZip(
            Path.Combine(_gameDirectory, "resourcepacks", "Faithful.zip"),
            ("pack.mcmeta", "{\"pack\":{\"description\":\"Faithful Local\",\"pack_format\":22}}"));
        string resourcePackDirectory = CreateDirectory(
            Path.Combine(_gameDirectory, "resourcepacks", "DirPack"),
            ("pack.mcmeta", "{\"pack\":{\"description\":[{\"text\":\"Dir\"},{\"text\":\" Pack\"}],\"pack_format\":34}}"));

        _metadataService.Set(modFile, "Alpha remote description", "Modrinth", "alpha-project");
        _metadataService.Set(shaderZip, "Shader remote description", "CurseForge", "12345");
        _metadataService.Set(resourcePackZip, "Faithful remote description", "Modrinth", "faithful-project");

        CommunityResourceInventoryResult result = await _service.ListAsync(new CommunityResourceInventoryRequest
        {
            TargetVersionName = "Fabric-1.20.1",
            ResolvedGameDirectory = _gameDirectory,
            ResourceTypes = new[] { "mod", "shader", "resourcepack" }
        });

        result.Resources.Should().HaveCount(6);

        CommunityResourceInventoryItem alphaMod = result.Resources.Single(item => item.ResourceInstanceId == "mod:mods/alpha.jar");
        alphaMod.DisplayName.Should().Be("alpha");
        alphaMod.CurrentVersionHint.Should().Be("alpha.jar");
        alphaMod.Source.Should().Be("Modrinth");
        alphaMod.ProjectId.Should().Be("alpha-project");
        alphaMod.Description.Should().Be("Alpha remote description");
        alphaMod.UpdateSupport.Should().Be("supported");
        alphaMod.IsDirectory.Should().BeFalse();

        CommunityResourceInventoryItem disabledMod = result.Resources.Single(item => item.ResourceInstanceId == "mod:mods/beta.jar.disabled");
        disabledMod.DisplayName.Should().Be("beta");
        disabledMod.CurrentVersionHint.Should().Be("beta.jar");

        CommunityResourceInventoryItem shaderItem = result.Resources.Single(item => item.ResourceInstanceId == "shader:shaderpacks/SuperShader.zip");
        shaderItem.DisplayName.Should().Be("SuperShader");
        shaderItem.ProjectId.Should().Be("12345");
        shaderItem.Source.Should().Be("CurseForge");
        shaderItem.Description.Should().Be("Shader remote description");

        CommunityResourceInventoryItem shaderDirectoryItem = result.Resources.Single(item => item.ResourceInstanceId == "shader:shaderpacks/FolderShader");
        shaderDirectoryItem.IsDirectory.Should().BeTrue();
        shaderDirectoryItem.Description.Should().BeNull();

        CommunityResourceInventoryItem resourcePackItem = result.Resources.Single(item => item.ResourceInstanceId == "resourcepack:resourcepacks/Faithful.zip");
        resourcePackItem.Description.Should().Be("Faithful remote description");
        resourcePackItem.PackFormat.Should().Be(22);
        resourcePackItem.ProjectId.Should().Be("faithful-project");

        CommunityResourceInventoryItem directoryPackItem = result.Resources.Single(item => item.ResourceInstanceId == "resourcepack:resourcepacks/DirPack");
        directoryPackItem.IsDirectory.Should().BeTrue();
        directoryPackItem.Description.Should().Be("Dir Pack");
        directoryPackItem.PackFormat.Should().Be(34);
        directoryPackItem.Source.Should().BeNull();

        Directory.Exists(shaderDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task ListAsync_WorldsAndDatapacks_IncludeWorldOwnershipAndPackMetadata()
    {
        string alphaWorld = CreateWorld("AlphaFolder", "Alpha World");
        string betaWorld = CreateWorld("BetaFolder", "Beta World");
        string alphaDatapack = CreateZip(
            Path.Combine(alphaWorld, "datapacks", "alpha-pack.zip"),
            ("pack.mcmeta", "{\"pack\":{\"description\":{\"text\":\"Alpha local datapack\"},\"pack_format\":48}}"));
        string betaDatapack = CreateDirectory(
            Path.Combine(betaWorld, "datapacks", "beta-pack"),
            ("pack.mcmeta", "{\"pack\":{\"description\":[{\"text\":\"Beta\"},{\"text\":\" Pack\"}],\"pack_format\":50}}"));

        _metadataService.Set(alphaDatapack, "Alpha remote datapack", "Modrinth", "alpha-datapack-project");

        CommunityResourceInventoryResult result = await _service.ListAsync(new CommunityResourceInventoryRequest
        {
            TargetVersionName = "NeoForge-1.21.1",
            ResolvedGameDirectory = _gameDirectory,
            ResourceTypes = new[] { "world", "datapack" }
        });

        result.Resources.Should().HaveCount(4);

        CommunityResourceInventoryItem alphaWorldItem = result.Resources.Single(item => item.ResourceInstanceId == "world:saves/AlphaFolder");
        alphaWorldItem.DisplayName.Should().Be("Alpha World");
        alphaWorldItem.WorldName.Should().Be("Alpha World");
        alphaWorldItem.UpdateSupport.Should().Be("unsupported");
        alphaWorldItem.UpdateUnsupportedReason.Should().NotBeNullOrWhiteSpace();

        CommunityResourceInventoryItem alphaDatapackItem = result.Resources.Single(item => item.ResourceInstanceId == "datapack:saves/AlphaFolder/datapacks/alpha-pack.zip");
        alphaDatapackItem.WorldName.Should().Be("Alpha World");
        alphaDatapackItem.Description.Should().Be("Alpha remote datapack");
        alphaDatapackItem.Source.Should().Be("Modrinth");
        alphaDatapackItem.ProjectId.Should().Be("alpha-datapack-project");
        alphaDatapackItem.PackFormat.Should().Be(48);
        alphaDatapackItem.UpdateSupport.Should().Be("unsupported");
        alphaDatapackItem.UpdateUnsupportedReason.Should().NotBeNullOrWhiteSpace();

        CommunityResourceInventoryItem betaDatapackItem = result.Resources.Single(item => item.ResourceInstanceId == "datapack:saves/BetaFolder/datapacks/beta-pack");
        betaDatapackItem.IsDirectory.Should().BeTrue();
        betaDatapackItem.WorldName.Should().Be("Beta World");
        betaDatapackItem.Description.Should().Be("Beta Pack");
        betaDatapackItem.PackFormat.Should().Be(50);
    }

    [Fact]
    public async Task ListAsync_FilterOnlyWorlds_ReturnsRequestedTypes()
    {
        CreateFile(Path.Combine(_gameDirectory, "mods", "only-mod.jar"));
        CreateWorld("WorldOnly", "World Only");

        CommunityResourceInventoryResult result = await _service.ListAsync(new CommunityResourceInventoryRequest
        {
            TargetVersionName = "OnlyWorlds",
            ResolvedGameDirectory = _gameDirectory,
            ResourceTypes = new[] { "world" }
        });

        result.Resources.Should().ContainSingle();
        result.Resources[0].ResourceType.Should().Be("world");
        result.Resources[0].ResourceInstanceId.Should().Be("world:saves/WorldOnly");
    }

    private string CreateWorld(string folderName, string levelName)
    {
        string worldDirectory = Path.Combine(_gameDirectory, "saves", folderName);
        Directory.CreateDirectory(worldDirectory);

        NbtCompound root = new(string.Empty)
        {
            new NbtCompound("Data")
            {
                new NbtString("LevelName", levelName),
                new NbtInt("GameType", 1),
                new NbtLong("LastPlayed", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            }
        };

        NbtFile file = new(root);
        file.SaveToFile(Path.Combine(worldDirectory, "level.dat"), NbtCompression.GZip);

        return worldDirectory;
    }

    private static string CreateFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test");
        return path;
    }

    private static string CreateDirectory(string path, params (string RelativePath, string Content)[] files)
    {
        Directory.CreateDirectory(path);
        foreach ((string relativePath, string content) in files)
        {
            string filePath = Path.Combine(path, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, content);
        }

        return path;
    }

    private static string CreateZip(string path, params (string RelativePath, string Content)[] files)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((string relativePath, string content) in files)
        {
            ZipArchiveEntry entry = archive.CreateEntry(relativePath, CompressionLevel.Fastest);
            using StreamWriter writer = new(entry.Open());
            writer.Write(content);
        }

        return path;
    }

    private sealed class FakeCommunityResourceMetadataService : ICommunityResourceMetadataService
    {
        private readonly Dictionary<string, CommunityResourceResolvedMetadata> _metadataByPath = new(StringComparer.OrdinalIgnoreCase);

        public Task<CommunityResourceResolvedMetadata?> GetMetadataAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            _metadataByPath.TryGetValue(Path.GetFullPath(filePath), out CommunityResourceResolvedMetadata? metadata);
            return Task.FromResult(metadata);
        }

        public void Set(string filePath, string description, string source, string projectId)
        {
            _metadataByPath[Path.GetFullPath(filePath)] = new CommunityResourceResolvedMetadata
            {
                Description = description,
                Source = source,
                ProjectId = projectId,
            };
        }
    }
}