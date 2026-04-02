using FluentAssertions;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class CommunityResourceWorldTargetResolverTests
{
    private readonly string _gameDirectory = Path.Combine(Path.GetTempPath(), $"WorldTargetResolverTests_{Guid.NewGuid():N}");

    [Fact]
    public async Task ResolveAsync_WithKnownWorldId_ReturnsTargetSaveName()
    {
        FakeCommunityResourceInventoryService inventoryService = new(
        [
            CreateWorldItem("AlphaFolder", "Alpha World"),
            CreateWorldItem("Beta Folder", "Beta World")
        ]);
        CommunityResourceWorldTargetResolver resolver = new(inventoryService);

        CommunityResourceWorldTargetResolutionResult result = await resolver.ResolveAsync(new CommunityResourceWorldTargetResolutionRequest
        {
            TargetVersionName = "Fabric 1.21.1",
            ResolvedGameDirectory = _gameDirectory,
            TargetWorldResourceId = "world:saves/Beta Folder"
        });

        result.IsResolved.Should().BeTrue();
        result.Status.Should().Be(CommunityResourceWorldTargetResolutionStatus.Resolved);
        result.ResolvedWorld.Should().NotBeNull();
        result.ResolvedWorld!.TargetSaveName.Should().Be("Beta Folder");
        result.ResolvedWorld.DisplayName.Should().Be("Beta World");
        result.AvailableWorlds.Should().HaveCount(2);
        inventoryService.LastRequest.Should().NotBeNull();
        inventoryService.LastRequest!.ResourceTypes.Should().BeEquivalentTo(["world"]);
        inventoryService.LastRequest.TargetVersionName.Should().Be("Fabric 1.21.1");
        inventoryService.LastRequest.ResolvedGameDirectory.Should().Be(Path.GetFullPath(_gameDirectory));
    }

    [Fact]
    public async Task ResolveAsync_WithoutWorldId_ReturnsAvailableWorldCandidates()
    {
        CommunityResourceWorldTargetResolver resolver = new(new FakeCommunityResourceInventoryService(
        [
            CreateWorldItem("AlphaFolder", "Alpha World"),
            CreateWorldItem("GammaFolder", "Gamma World")
        ]));

        CommunityResourceWorldTargetResolutionResult result = await resolver.ResolveAsync(new CommunityResourceWorldTargetResolutionRequest
        {
            TargetVersionName = "NeoForge 1.21.1",
            ResolvedGameDirectory = _gameDirectory,
        });

        result.IsResolved.Should().BeFalse();
        result.Status.Should().Be(CommunityResourceWorldTargetResolutionStatus.MissingWorldResourceId);
        result.ResolvedWorld.Should().BeNull();
        result.AvailableWorlds.Select(item => item.ResourceInstanceId)
            .Should().Equal("world:saves/AlphaFolder", "world:saves/GammaFolder");
    }

    [Fact]
    public async Task ResolveAsync_WithNonWorldResourceId_ReturnsInvalidStatus()
    {
        CommunityResourceWorldTargetResolver resolver = new(new FakeCommunityResourceInventoryService(
        [CreateWorldItem("AlphaFolder", "Alpha World")]));

        CommunityResourceWorldTargetResolutionResult result = await resolver.ResolveAsync(new CommunityResourceWorldTargetResolutionRequest
        {
            TargetVersionName = "Quilt 1.20.1",
            ResolvedGameDirectory = _gameDirectory,
            TargetWorldResourceId = "datapack:saves/AlphaFolder/datapacks/test.zip"
        });

        result.IsResolved.Should().BeFalse();
        result.Status.Should().Be(CommunityResourceWorldTargetResolutionStatus.InvalidWorldResourceId);
        result.RequestedTargetWorldResourceId.Should().Be("datapack:saves/AlphaFolder/datapacks/test.zip");
        result.AvailableWorlds.Should().ContainSingle();
    }

    [Fact]
    public async Task ResolveAsync_WithUnknownWorldId_ReturnsWorldCandidates()
    {
        CommunityResourceWorldTargetResolver resolver = new(new FakeCommunityResourceInventoryService(
        [CreateWorldItem("AlphaFolder", "Alpha World")]));

        CommunityResourceWorldTargetResolutionResult result = await resolver.ResolveAsync(new CommunityResourceWorldTargetResolutionRequest
        {
            TargetVersionName = "Vanilla 1.21.1",
            ResolvedGameDirectory = _gameDirectory,
            TargetWorldResourceId = "world:saves/MissingFolder"
        });

        result.IsResolved.Should().BeFalse();
        result.Status.Should().Be(CommunityResourceWorldTargetResolutionStatus.WorldNotFound);
        result.RequestedTargetWorldResourceId.Should().Be("world:saves/MissingFolder");
        result.AvailableWorlds.Should().ContainSingle();
        result.AvailableWorlds[0].TargetSaveName.Should().Be("AlphaFolder");
    }

    private CommunityResourceInventoryItem CreateWorldItem(string folderName, string displayName)
    {
        string filePath = Path.Combine(_gameDirectory, "saves", folderName);
        return new CommunityResourceInventoryItem
        {
            TargetVersionName = "TestVersion",
            ResolvedGameDirectory = _gameDirectory,
            ResourceType = "world",
            ResourceInstanceId = $"world:saves/{folderName}",
            DisplayName = displayName,
            FilePath = filePath,
            RelativePath = $"saves/{folderName}",
            IsDirectory = true,
            WorldName = displayName,
            UpdateSupport = "unsupported",
        };
    }

    private sealed class FakeCommunityResourceInventoryService : ICommunityResourceInventoryService
    {
        private readonly IReadOnlyList<CommunityResourceInventoryItem> _items;

        public FakeCommunityResourceInventoryService(IReadOnlyList<CommunityResourceInventoryItem> items)
        {
            _items = items;
        }

        public CommunityResourceInventoryRequest? LastRequest { get; private set; }

        public Task<CommunityResourceInventoryResult> ListAsync(
            CommunityResourceInventoryRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new CommunityResourceInventoryResult
            {
                TargetVersionName = request.TargetVersionName,
                ResolvedGameDirectory = Path.GetFullPath(request.ResolvedGameDirectory),
                Resources = _items,
            });
        }
    }
}