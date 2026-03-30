using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using fNbt;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Tests.Services;

public sealed class CommunityResourceUpdateCheckServiceTests : IDisposable
{
    private readonly string _minecraftRoot;
    private readonly string _gameDirectory;
    private readonly FakeCommunityResourceMetadataService _metadataService;
    private readonly Mock<IGameDirResolver> _gameDirResolverMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<IVersionInfoService> _versionInfoServiceMock;
    private readonly RecordingHashLookupCenter _hashLookupCenter;
    private readonly CommunityResourceUpdateCheckService _service;

    public CommunityResourceUpdateCheckServiceTests()
    {
        _minecraftRoot = Path.Combine(Path.GetTempPath(), $"CommunityResourceUpdateCheckTests_{Guid.NewGuid():N}");
        _gameDirectory = Path.Combine(_minecraftRoot, "game");

        Directory.CreateDirectory(_minecraftRoot);
        Directory.CreateDirectory(_gameDirectory);

        _metadataService = new FakeCommunityResourceMetadataService();
        _gameDirResolverMock = new Mock<IGameDirResolver>();
        _fileServiceMock = new Mock<IFileService>();
        _versionInfoServiceMock = new Mock<IVersionInfoService>();
        _hashLookupCenter = new RecordingHashLookupCenter();

        _gameDirResolverMock
            .Setup(service => service.GetGameDirForVersionAsync(It.IsAny<string>()))
            .ReturnsAsync(_gameDirectory);

        _fileServiceMock
            .Setup(service => service.GetMinecraftDataPath())
            .Returns(_minecraftRoot);

        ConfigureVersionInfo("1.20.1", "fabric");

        CommunityResourceInventoryService inventoryService = new(_metadataService, new WorldDataService());
        ModrinthService modrinthService = new(
            new HttpClient(new ThrowingHttpMessageHandler()),
            new DownloadSourceFactory(),
            null,
            _hashLookupCenter);
        CurseForgeService curseForgeService = new(
            new HttpClient(new ThrowingHttpMessageHandler()),
            new DownloadSourceFactory(),
            null,
            _hashLookupCenter);

        _service = new CommunityResourceUpdateCheckService(
            inventoryService,
            _gameDirResolverMock.Object,
            _fileServiceMock.Object,
            _versionInfoServiceMock.Object,
            modrinthService,
            curseForgeService);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_minecraftRoot))
            {
                Directory.Delete(_minecraftRoot, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task CheckAsync_MixedResources_ReturnsExpectedStatuses()
    {
        CreateVersionDirectory("Fabric-1.20.1");

        string modFile = CreateFile(Path.Combine(_gameDirectory, "mods", "alpha.jar"), "alpha-mod-content");
        string unknownModFile = CreateFile(Path.Combine(_gameDirectory, "mods", "mystery.jar"), "mystery-mod-content");
        string shaderFile = CreateZip(
            Path.Combine(_gameDirectory, "shaderpacks", "cinematic.zip"),
            ("shader.properties", "profile=cinematic"));
        CreateDirectory(
            Path.Combine(_gameDirectory, "resourcepacks", "DirPack"),
            ("pack.mcmeta", "{\"pack\":{\"description\":\"Directory Pack\",\"pack_format\":34}}"));
        string worldDirectory = CreateWorld("AlphaWorld", "Alpha World");
        CreateZip(
            Path.Combine(worldDirectory, "datapacks", "alpha-pack.zip"),
            ("pack.mcmeta", "{\"pack\":{\"description\":\"Alpha datapack\",\"pack_format\":48}}"));

        string modHash = ComputeSha1(modFile);
        uint shaderFingerprint = CurseForgeFingerprintHelper.ComputeFingerprint(shaderFile);

        _metadataService.Set(modFile, "Alpha remote description", "Modrinth", "alpha-project");

        _hashLookupCenter.SetModrinthVersions(
            "modrinth:version_files:sha1",
            (modHash, BuildModrinthVersion("alpha-v1", "alpha-project", "1.0.0", "alpha.jar", modHash)));
        _hashLookupCenter.SetModrinthVersions(
            "modrinth:version_files:update:sha1:loaders=fabric:gameVersions=1.20.1",
            (modHash, BuildModrinthVersion("alpha-v2", "alpha-project", "1.1.0", "alpha.jar", new string('b', 40))));
        _hashLookupCenter.SetCurseForgeMatches(
            "curseforge:fingerprints",
            new CurseForgeFingerprintMatchesResult
            {
                ExactMatches =
                {
                    new CurseForgeFingerprintMatch
                    {
                        Id = 12345,
                        File = BuildCurseForgeFile(200, "Cinematic Shader 1.0", "cinematic.zip", shaderFingerprint, DateTime.UtcNow.AddDays(-5), "1.20.1"),
                        LatestFiles =
                        {
                            BuildCurseForgeFile(201, "Cinematic Shader 2.0", "cinematic.zip", shaderFingerprint + 1, DateTime.UtcNow, "1.20.1")
                        }
                    }
                },
                ExactFingerprints = { shaderFingerprint },
                InstalledFingerprints = { shaderFingerprint },
            });

        CommunityResourceUpdateCheckResult result = await _service.CheckAsync(new CommunityResourceUpdateCheckRequest
        {
            TargetVersionName = "Fabric-1.20.1",
        });

        result.Items.Should().HaveCount(6);
        result.MinecraftVersion.Should().Be("1.20.1");
        result.ModLoaderType.Should().Be("fabric");

        CommunityResourceUpdateCheckItem modItem = result.Items.Single(item => item.ResourceInstanceId == "mod:mods/alpha.jar");
        modItem.Status.Should().Be("update_available");
        modItem.Provider.Should().Be("Modrinth");
        modItem.ProjectId.Should().Be("alpha-project");
        modItem.CurrentVersion.Should().Be("1.0.0");
        modItem.LatestVersion.Should().Be("1.1.0");
        modItem.LatestResourceFileId.Should().Be("alpha-v2");
        modItem.HasUpdate.Should().BeTrue();

        CommunityResourceUpdateCheckItem shaderItem = result.Items.Single(item => item.ResourceInstanceId == "shader:shaderpacks/cinematic.zip");
        shaderItem.Status.Should().Be("update_available");
        shaderItem.Provider.Should().Be("CurseForge");
        shaderItem.ProjectId.Should().Be("12345");
        shaderItem.CurrentVersion.Should().Be("Cinematic Shader 1.0");
        shaderItem.LatestVersion.Should().Be("Cinematic Shader 2.0");
        shaderItem.LatestResourceFileId.Should().Be("201");
        shaderItem.HasUpdate.Should().BeTrue();

        CommunityResourceUpdateCheckItem directoryPackItem = result.Items.Single(item => item.ResourceInstanceId == "resourcepack:resourcepacks/DirPack");
        directoryPackItem.Status.Should().Be("unsupported");
        directoryPackItem.UnsupportedReason.Should().NotBeNullOrWhiteSpace();

        CommunityResourceUpdateCheckItem worldItem = result.Items.Single(item => item.ResourceInstanceId == "world:saves/AlphaWorld");
        worldItem.Status.Should().Be("unsupported");
        worldItem.UnsupportedReason.Should().NotBeNullOrWhiteSpace();

        CommunityResourceUpdateCheckItem datapackItem = result.Items.Single(item => item.ResourceInstanceId == "datapack:saves/AlphaWorld/datapacks/alpha-pack.zip");
        datapackItem.Status.Should().Be("unsupported");
        datapackItem.UnsupportedReason.Should().NotBeNullOrWhiteSpace();

        CommunityResourceUpdateCheckItem unknownModItem = result.Items.Single(item => item.ResourceInstanceId == "mod:mods/mystery.jar");
        unknownModItem.Status.Should().Be("not_identified");
        unknownModItem.Provider.Should().BeNull();
        unknownModItem.HasUpdate.Should().BeFalse();

        _hashLookupCenter.RequestedModrinthScopes.Should().Contain("modrinth:version_files:update:sha1:loaders=fabric:gameVersions=1.20.1");
        _hashLookupCenter.RequestedCurseForgeScopes.Should().Contain("curseforge:fingerprints");
        File.Exists(unknownModFile).Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_SubsetRequest_OnlyReturnsRequestedItemsAndKeepsCurrentOnlyMatchUpToDate()
    {
        CreateVersionDirectory("Fabric-1.20.1");

        CreateFile(Path.Combine(_gameDirectory, "mods", "ignored.jar"), "ignored-mod-content");
        string resourcePackFile = CreateZip(
            Path.Combine(_gameDirectory, "resourcepacks", "Faithful.zip"),
            ("pack.mcmeta", "{\"pack\":{\"description\":\"Faithful\",\"pack_format\":22}}"));

        string resourcePackHash = ComputeSha1(resourcePackFile);
        _hashLookupCenter.SetModrinthVersions(
            "modrinth:version_files:sha1",
            (resourcePackHash, BuildModrinthVersion("faithful-v4", "faithful-project", "Faithful 4.0", "Faithful.zip", resourcePackHash)));

        CommunityResourceUpdateCheckResult result = await _service.CheckAsync(new CommunityResourceUpdateCheckRequest
        {
            TargetVersionName = "Fabric-1.20.1",
            ResourceInstanceIds = new[] { "resourcepack:resourcepacks/Faithful.zip" },
        });

        result.Items.Should().ContainSingle();

        CommunityResourceUpdateCheckItem packItem = result.Items[0];
        packItem.ResourceInstanceId.Should().Be("resourcepack:resourcepacks/Faithful.zip");
        packItem.Status.Should().Be("up_to_date");
        packItem.Provider.Should().Be("Modrinth");
        packItem.ProjectId.Should().Be("faithful-project");
        packItem.CurrentVersion.Should().Be("Faithful 4.0");
        packItem.LatestVersion.Should().Be("Faithful 4.0");
        packItem.LatestResourceFileId.Should().Be("faithful-v4");
        packItem.HasUpdate.Should().BeFalse();

        _hashLookupCenter.RequestedModrinthScopes.Should().Contain("modrinth:version_files:update:sha1:loaders=minecraft:gameVersions=1.20.1");
        _hashLookupCenter.RequestedCurseForgeScopes.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_ResourceTypeFilter_OnlyReturnsRequestedType()
    {
        CreateVersionDirectory("Fabric-1.20.1");

        string modFile = CreateFile(Path.Combine(_gameDirectory, "mods", "alpha.jar"), "alpha-mod-content");
        CreateZip(
            Path.Combine(_gameDirectory, "resourcepacks", "Faithful.zip"),
            ("pack.mcmeta", "{\"pack\":{\"description\":\"Faithful\",\"pack_format\":22}}"));

        string modHash = ComputeSha1(modFile);
        _hashLookupCenter.SetModrinthVersions(
            "modrinth:version_files:sha1",
            (modHash, BuildModrinthVersion("alpha-v1", "alpha-project", "1.0.0", "alpha.jar", modHash)));
        _hashLookupCenter.SetModrinthVersions(
            "modrinth:version_files:update:sha1:loaders=fabric:gameVersions=1.20.1",
            (modHash, BuildModrinthVersion("alpha-v2", "alpha-project", "1.1.0", "alpha.jar", new string('b', 40))));

        CommunityResourceUpdateCheckResult result = await _service.CheckAsync(new CommunityResourceUpdateCheckRequest
        {
            TargetVersionName = "Fabric-1.20.1",
            ResourceTypes = ["mod"],
        });

        result.Items.Should().ContainSingle();

        CommunityResourceUpdateCheckItem item = result.Items[0];
        item.ResourceType.Should().Be("mod");
        item.ResourceInstanceId.Should().Be("mod:mods/alpha.jar");
        item.Status.Should().Be("update_available");
    }

    [Fact]
    public async Task CheckAsync_FallsBackToExtractedVersionConfigWhenCachedInfoIsEmpty()
    {
        const string targetVersionName = "NeoForge-1.21.1";
        CreateVersionDirectory(targetVersionName);

        string modFile = CreateFile(Path.Combine(_gameDirectory, "mods", "neoforge.jar"), "neoforge-mod-content");
        string modHash = ComputeSha1(modFile);

        ConfigureVersionInfo(
            string.Empty,
            string.Empty,
            extractedVersionConfigFactory: versionName => new VersionConfig
            {
                MinecraftVersion = "1.21.1",
                ModLoaderType = "NeoForge",
            });

        _hashLookupCenter.SetModrinthVersions(
            "modrinth:version_files:sha1",
            (modHash, BuildModrinthVersion("neo-v1", "neo-project", "2.0.0", "neoforge.jar", modHash)));

        CommunityResourceUpdateCheckResult result = await _service.CheckAsync(new CommunityResourceUpdateCheckRequest
        {
            TargetVersionName = targetVersionName,
            ResolvedGameDirectory = _gameDirectory,
        });

        result.MinecraftVersion.Should().Be("1.21.1");
        result.ModLoaderType.Should().Be("NeoForge");
        _hashLookupCenter.RequestedModrinthScopes.Should().Contain("modrinth:version_files:update:sha1:loaders=neoforge:gameVersions=1.21.1");
    }

    [Fact]
    public async Task CheckAsync_FallsBackToVersionNameStringInferenceForNeoForge()
    {
        const string targetVersionName = "NeoForge-1.21.1";
        CreateVersionDirectory(targetVersionName);

        string modFile = CreateFile(Path.Combine(_gameDirectory, "mods", "neoforge.jar"), "neoforge-mod-content");
        string modHash = ComputeSha1(modFile);

        ConfigureVersionInfo(
            string.Empty,
            string.Empty,
            extractedVersionConfigFactory: _ => new VersionConfig
            {
                MinecraftVersion = "1.21.1",
                ModLoaderType = string.Empty,
            });

        _hashLookupCenter.SetModrinthVersions(
            "modrinth:version_files:sha1",
            (modHash, BuildModrinthVersion("neo-v1", "neo-project", "2.0.0", "neoforge.jar", modHash)));
        _hashLookupCenter.SetModrinthVersions(
            "modrinth:version_files:update:sha1:loaders=neoforge:gameVersions=1.21.1",
            (modHash, BuildModrinthVersion("neo-v2", "neo-project", "2.1.0", "neoforge.jar", new string('c', 40))));

        CommunityResourceUpdateCheckResult result = await _service.CheckAsync(new CommunityResourceUpdateCheckRequest
        {
            TargetVersionName = targetVersionName,
            ResolvedGameDirectory = _gameDirectory,
        });

        result.MinecraftVersion.Should().Be("1.21.1");
        result.ModLoaderType.Should().Be("neoforge");
        _hashLookupCenter.RequestedModrinthScopes.Should().Contain("modrinth:version_files:update:sha1:loaders=neoforge:gameVersions=1.21.1");
        _hashLookupCenter.RequestedModrinthScopes.Should().NotContain("modrinth:version_files:update:sha1:loaders=forge:gameVersions=1.21.1");
    }

    private void ConfigureVersionInfo(
        string minecraftVersion,
        string modLoaderType,
        Func<string, VersionConfig>? extractedVersionConfigFactory = null)
    {
        _versionInfoServiceMock.Reset();
        _versionInfoServiceMock
            .Setup(service => service.GetFullVersionInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(new VersionConfig
            {
                MinecraftVersion = minecraftVersion,
                ModLoaderType = modLoaderType,
            });
        _versionInfoServiceMock
            .Setup(service => service.ExtractVersionConfigFromName(It.IsAny<string>()))
            .Returns((string versionName) => extractedVersionConfigFactory?.Invoke(versionName) ?? new VersionConfig
            {
                MinecraftVersion = minecraftVersion,
                ModLoaderType = modLoaderType,
            });
    }

    private string CreateVersionDirectory(string versionName)
    {
        string versionDirectory = Path.Combine(_minecraftRoot, "versions", versionName);
        Directory.CreateDirectory(versionDirectory);
        return versionDirectory;
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

    private static string CreateFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
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

    private static string ComputeSha1(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        using SHA1 sha1 = SHA1.Create();
        return Convert.ToHexString(sha1.ComputeHash(stream)).ToLowerInvariant();
    }

    private static ModrinthVersion BuildModrinthVersion(
        string id,
        string projectId,
        string versionNumber,
        string fileName,
        string sha1)
    {
        return new ModrinthVersion
        {
            Id = id,
            ProjectId = projectId,
            VersionNumber = versionNumber,
            Name = versionNumber,
            Changelog = string.Empty,
            VersionType = "release",
            DatePublished = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Downloads = 1,
            IsPrerelease = false,
            GameVersions = new List<string>(),
            Loaders = new List<string>(),
            Dependencies = new List<Dependency>(),
            Files =
            {
                new ModrinthVersionFile
                {
                    Filename = fileName,
                    Url = new Uri($"https://example.com/{fileName}"),
                    Primary = true,
                    Size = 1024,
                    FileType = "required-resource-pack",
                    Hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["sha1"] = sha1,
                    },
                }
            }
        };
    }

    private static CurseForgeFile BuildCurseForgeFile(
        int id,
        string displayName,
        string fileName,
        uint fingerprint,
        DateTime fileDate,
        params string[] gameVersions)
    {
        return new CurseForgeFile
        {
            Id = id,
            GameId = 432,
            ModId = 1,
            IsAvailable = true,
            DisplayName = displayName,
            FileName = fileName,
            ReleaseType = 1,
            FileStatus = 4,
            FileDate = fileDate,
            FileLength = 1024,
            DownloadCount = 10,
            DownloadUrl = $"https://example.com/{fileName}",
            GameVersions = gameVersions.ToList(),
            FileFingerprint = fingerprint,
        };
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

    private sealed class RecordingHashLookupCenter : IHashLookupCenter
    {
        private readonly Dictionary<string, Dictionary<string, ModrinthVersion>> _modrinthVersionsByScope = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CurseForgeFingerprintMatchesResult> _curseForgeResultsByScope = new(StringComparer.OrdinalIgnoreCase);

        public List<string> RequestedModrinthScopes { get; } = new();

        public List<string> RequestedCurseForgeScopes { get; } = new();

        public void SetModrinthVersions(string scope, params (string Hash, ModrinthVersion Version)[] entries)
        {
            _modrinthVersionsByScope[scope] = entries.ToDictionary(
                entry => entry.Hash,
                entry => entry.Version,
                StringComparer.OrdinalIgnoreCase);
        }

        public void SetCurseForgeMatches(string scope, CurseForgeFingerprintMatchesResult result)
        {
            _curseForgeResultsByScope[scope] = result;
        }

        public Task<Dictionary<string, ModrinthVersion>> GetOrFetchModrinthVersionsByHashesAsync(
            string scope,
            IReadOnlyCollection<string> hashes,
            Func<IReadOnlyCollection<string>, Task<Dictionary<string, ModrinthVersion>>> fetchBatchAsync,
            TimeSpan? successTtl = null,
            TimeSpan? emptyTtl = null,
            CancellationToken cancellationToken = default)
        {
            RequestedModrinthScopes.Add(scope);

            if (!_modrinthVersionsByScope.TryGetValue(scope, out Dictionary<string, ModrinthVersion>? versions))
            {
                return Task.FromResult(new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase));
            }

            Dictionary<string, ModrinthVersion> filtered = versions
                .Where(entry => hashes.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(filtered);
        }

        public Task<CurseForgeFingerprintMatchesResult> GetOrFetchCurseForgeMatchesByFingerprintsAsync(
            string scope,
            IReadOnlyCollection<uint> fingerprints,
            Func<IReadOnlyCollection<uint>, Task<CurseForgeFingerprintMatchesResult>> fetchBatchAsync,
            TimeSpan? successTtl = null,
            TimeSpan? emptyTtl = null,
            CancellationToken cancellationToken = default)
        {
            RequestedCurseForgeScopes.Add(scope);

            if (!_curseForgeResultsByScope.TryGetValue(scope, out CurseForgeFingerprintMatchesResult? result))
            {
                return Task.FromResult(new CurseForgeFingerprintMatchesResult
                {
                    UnmatchedFingerprints = fingerprints.ToList(),
                });
            }

            HashSet<uint> requested = fingerprints.ToHashSet();
            return Task.FromResult(new CurseForgeFingerprintMatchesResult
            {
                IsCacheBuilt = result.IsCacheBuilt,
                ExactMatches = result.ExactMatches
                    .Where(match => match.File != null && requested.Contains((uint)match.File.FileFingerprint))
                    .ToList(),
                ExactFingerprints = result.ExactFingerprints.Where(requested.Contains).ToList(),
                PartialMatches = new List<CurseForgeFingerprintMatch>(),
                PartialMatchFingerprints = new Dictionary<string, List<uint>>(),
                InstalledFingerprints = result.InstalledFingerprints.Where(requested.Contains).ToList(),
                UnmatchedFingerprints = result.UnmatchedFingerprints.Where(requested.Contains).ToList(),
            });
        }

        public Task<ModrinthProjectDetail?> GetOrFetchModrinthProjectDetailAsync(
            string scope,
            string projectIdOrSlug,
            Func<string, Task<ModrinthProjectDetail?>> fetchAsync,
            TimeSpan? successTtl = null,
            TimeSpan? emptyTtl = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ModrinthProjectDetail?>(null);
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(HttpStatusCode.InternalServerError)
            {
                RequestMessage = request,
                Content = new StringContent("Unexpected network call in test."),
            };
            throw new HttpRequestException("Unexpected network call in test.", null, response.StatusCode);
        }
    }
}