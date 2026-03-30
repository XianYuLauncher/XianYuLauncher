using System.Net;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Features.ErrorAnalysis.Services;
using XianYuLauncher.Features.ModDownloadDetail.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class AgentCommunityResourceServicePhase4Tests
{
    [Fact]
    public async Task GetInstanceCommunityResourcesAsync_ShouldReturnInventoryPayload()
    {
        AgentCommunityResourceService service = CreateService(
            out Mock<ICommunityResourceInventoryService> inventoryServiceMock,
            out _,
            out _,
            out _,
            out _);

        CommunityResourceInventoryRequest? capturedRequest = null;
        inventoryServiceMock
            .Setup(service => service.ListAsync(It.IsAny<CommunityResourceInventoryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CommunityResourceInventoryRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new CommunityResourceInventoryResult
            {
                TargetVersionName = "Fabric-1.20.1",
                ResolvedGameDirectory = @"C:\Minecraft\Instances\Fabric-1.20.1",
                Resources =
                [
                    new CommunityResourceInventoryItem
                    {
                        ResourceType = "mod",
                        ResourceInstanceId = "mod:mods/alpha.jar",
                        DisplayName = "Alpha",
                        FilePath = @"C:\Minecraft\Instances\Fabric-1.20.1\mods\alpha.jar",
                        RelativePath = "mods/alpha.jar",
                        Source = "Modrinth",
                        ProjectId = "alpha-project",
                        CurrentVersionHint = "1.0.0",
                        UpdateSupport = "supported",
                    },
                    new CommunityResourceInventoryItem
                    {
                        ResourceType = "world",
                        ResourceInstanceId = "world:saves/AlphaWorld",
                        DisplayName = "Alpha World",
                        FilePath = @"C:\Minecraft\Instances\Fabric-1.20.1\saves\AlphaWorld",
                        RelativePath = "saves/AlphaWorld",
                        IsDirectory = true,
                        WorldName = "Alpha World",
                        UpdateSupport = "unsupported",
                        UpdateUnsupportedReason = "world_scan_only",
                    }
                ]
            });

        string message = await service.GetInstanceCommunityResourcesAsync(new AgentInstanceCommunityResourceInventoryCommand
        {
            TargetVersionName = "Fabric-1.20.1",
            ResourceTypes = ["mod", "world"],
        }, CancellationToken.None);

        JObject payload = JObject.Parse(message);
        payload["status"]!.Value<string>().Should().Be("ok");
        payload["target_version_name"]!.Value<string>().Should().Be("Fabric-1.20.1");
        payload["version_directory_path"]!.Value<string>().Should().Be(@"C:\Minecraft\versions\Fabric-1.20.1");
        payload["resolved_game_directory"]!.Value<string>().Should().Be(@"C:\Minecraft\Instances\Fabric-1.20.1");
        payload["total_count"]!.Value<int>().Should().Be(2);
        ((JArray)payload["requested_resource_types"]!).Select(token => token.Value<string>()).Should().BeEquivalentTo(["mod", "world"]);

        JArray resources = (JArray)payload["resources"]!;
        resources.Should().HaveCount(2);
        resources[0]!["resource_instance_id"]!.Value<string>().Should().Be("mod:mods/alpha.jar");
        resources[1]!["unsupported_reason"]!.Value<string>().Should().Be("world_scan_only");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.TargetVersionName.Should().Be("Fabric-1.20.1");
        capturedRequest.ResolvedGameDirectory.Should().Be(@"C:\Minecraft\Instances\Fabric-1.20.1");
        capturedRequest.ResourceTypes.Should().BeEquivalentTo(["mod", "world"]);
    }

    [Fact]
    public async Task CheckInstanceCommunityResourceUpdatesAsync_AllInstalledScope_ShouldReturnSummary()
    {
        AgentCommunityResourceService service = CreateService(
            out _,
            out Mock<ICommunityResourceUpdateCheckService> updateCheckServiceMock,
            out _,
            out _,
            out _);

        CommunityResourceUpdateCheckRequest? capturedRequest = null;
        updateCheckServiceMock
            .Setup(service => service.CheckAsync(It.IsAny<CommunityResourceUpdateCheckRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CommunityResourceUpdateCheckRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new CommunityResourceUpdateCheckResult
            {
                TargetVersionName = "Fabric-1.20.1",
                ResolvedGameDirectory = @"C:\Minecraft\Instances\Fabric-1.20.1",
                MinecraftVersion = "1.20.1",
                ModLoaderType = "fabric",
                CheckedAt = new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero),
                Items =
                [
                    new CommunityResourceUpdateCheckItem
                    {
                        ResourceInstanceId = "mod:mods/alpha.jar",
                        ResourceType = "mod",
                        DisplayName = "Alpha",
                        FilePath = @"C:\Minecraft\Instances\Fabric-1.20.1\mods\alpha.jar",
                        RelativePath = "mods/alpha.jar",
                        Status = "update_available",
                        HasUpdate = true,
                        CurrentVersion = "1.0.0",
                        LatestVersion = "1.1.0",
                        Provider = "Modrinth",
                        ProjectId = "alpha-project",
                        LatestResourceFileId = "alpha-v2",
                    },
                    new CommunityResourceUpdateCheckItem
                    {
                        ResourceInstanceId = "world:saves/AlphaWorld",
                        ResourceType = "world",
                        DisplayName = "Alpha World",
                        FilePath = @"C:\Minecraft\Instances\Fabric-1.20.1\saves\AlphaWorld",
                        RelativePath = "saves/AlphaWorld",
                        Status = "unsupported",
                        UnsupportedReason = "world_scan_only",
                    }
                ]
            });

        string message = await service.CheckInstanceCommunityResourceUpdatesAsync(new AgentInstanceCommunityResourceUpdateCheckCommand
        {
            TargetVersionName = "Fabric-1.20.1",
            CheckScope = "all_installed",
        }, CancellationToken.None);

        JObject payload = JObject.Parse(message);
        payload["status"]!.Value<string>().Should().Be("ok");
        payload["check_scope"]!.Value<string>().Should().Be("all_installed");
        payload["minecraft_version"]!.Value<string>().Should().Be("1.20.1");
        payload["loader"]!.Value<string>().Should().Be("fabric");
        payload["summary"]!["update_available"]!.Value<int>().Should().Be(1);
        payload["summary"]!["unsupported"]!.Value<int>().Should().Be(1);
        payload["summary"]!["missing"]!.Value<int>().Should().Be(0);
        ((JArray)payload["items"]!).Should().HaveCount(2);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.TargetVersionName.Should().Be("Fabric-1.20.1");
        capturedRequest.ResolvedGameDirectory.Should().Be(@"C:\Minecraft\Instances\Fabric-1.20.1");
        capturedRequest.ResourceInstanceIds.Should().BeNull();
    }

    [Fact]
    public async Task PrepareUpdateAsync_ExplicitSelection_ShouldReturnConfirmationPayload()
    {
        AgentCommunityResourceService service = CreateService(
            out _,
            out Mock<ICommunityResourceUpdateCheckService> updateCheckServiceMock,
            out _,
            out _,
            out _);

        updateCheckServiceMock
            .Setup(service => service.CheckAsync(It.IsAny<CommunityResourceUpdateCheckRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunityResourceUpdateCheckResult
            {
                TargetVersionName = "Fabric-1.20.1",
                ResolvedGameDirectory = @"C:\Minecraft\Instances\Fabric-1.20.1",
                Items =
                [
                    new CommunityResourceUpdateCheckItem
                    {
                        ResourceInstanceId = "mod:mods/alpha.jar",
                        ResourceType = "mod",
                        DisplayName = "Alpha",
                        FilePath = @"C:\Minecraft\Instances\Fabric-1.20.1\mods\alpha.jar",
                        RelativePath = "mods/alpha.jar",
                        Status = "update_available",
                        HasUpdate = true,
                        CurrentVersion = "1.0.0",
                        LatestVersion = "1.1.0",
                        Provider = "Modrinth",
                        ProjectId = "alpha-project",
                        LatestResourceFileId = "alpha-v2",
                    }
                ]
            });

        AgentCommunityResourceUpdatePreparation preparation = await service.PrepareUpdateAsync(new AgentInstanceCommunityResourceUpdateCommand
        {
            TargetVersionName = "Fabric-1.20.1",
            ResourceInstanceIds = ["mod:mods/alpha.jar"],
        }, CancellationToken.None);

        preparation.IsReadyForConfirmation.Should().BeTrue();
        preparation.ButtonText.Should().Be("更新 Alpha");
        preparation.ProposalParameters["target_version_name"].Should().Be("Fabric-1.20.1");
        preparation.ProposalParameters["selection_mode"].Should().Be("explicit");
        JArray.Parse(preparation.ProposalParameters["resource_instance_ids"]).Values<string>().Should().ContainSingle("mod:mods/alpha.jar");

        JObject payload = JObject.Parse(preparation.Message);
        payload["status"]!.Value<string>().Should().Be("ready_for_confirmation");
        payload["summary"]!["update_available"]!.Value<int>().Should().Be(1);
        ((JArray)payload["update_candidates"]!).Should().HaveCount(1);
        payload["message"]!.Value<string>().Should().Contain("等待用户确认");
    }

    [Fact]
    public async Task StartUpdateAsync_ShouldReturnStartedPayloadAndPassOperationRequest()
    {
        AgentCommunityResourceService service = CreateService(
            out _,
            out _,
            out Mock<ICommunityResourceUpdateService> updateServiceMock,
            out _,
            out _);

        CommunityResourceUpdateRequest? capturedRequest = null;
        updateServiceMock
            .Setup(service => service.StartUpdateAsync(It.IsAny<CommunityResourceUpdateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CommunityResourceUpdateRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync("community-update-op");

        string message = await service.StartUpdateAsync(new AgentInstanceCommunityResourceUpdateCommand
        {
            TargetVersionName = "Fabric-1.20.1",
            ResourceInstanceIds = ["mod:mods/alpha.jar", "shader:shaderpacks/cinematic.zip"],
        }, CancellationToken.None);

        JObject payload = JObject.Parse(message);
        payload["status"]!.Value<string>().Should().Be("started");
        payload["operation_id"]!.Value<string>().Should().Be("community-update-op");
        payload["selection_mode"]!.Value<string>().Should().Be("explicit");
        ((JArray)payload["resource_instance_ids"]!).Values<string>().Should().BeEquivalentTo(["mod:mods/alpha.jar", "shader:shaderpacks/cinematic.zip"]);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.TargetVersionName.Should().Be("Fabric-1.20.1");
        capturedRequest.ResolvedGameDirectory.Should().Be(@"C:\Minecraft\Instances\Fabric-1.20.1");
        capturedRequest.SelectionMode.Should().Be("explicit");
        capturedRequest.ResourceInstanceIds.Should().BeEquivalentTo(["mod:mods/alpha.jar", "shader:shaderpacks/cinematic.zip"]);
    }

    private static AgentCommunityResourceService CreateService(
        out Mock<ICommunityResourceInventoryService> inventoryServiceMock,
        out Mock<ICommunityResourceUpdateCheckService> updateCheckServiceMock,
        out Mock<ICommunityResourceUpdateService> updateServiceMock,
        out Mock<IMinecraftVersionService> minecraftVersionServiceMock,
        out Mock<IGameDirResolver> gameDirResolverMock)
    {
        inventoryServiceMock = new Mock<ICommunityResourceInventoryService>();
        updateCheckServiceMock = new Mock<ICommunityResourceUpdateCheckService>();
        updateServiceMock = new Mock<ICommunityResourceUpdateService>();
        minecraftVersionServiceMock = new Mock<IMinecraftVersionService>();
        Mock<IVersionInfoService> versionInfoServiceMock = new();
        Mock<IFileService> fileServiceMock = new();
        gameDirResolverMock = new Mock<IGameDirResolver>();
        Mock<ICommunityResourceInstallPlanner> installPlannerMock = new();
        Mock<ICommunityResourceInstallService> installServiceMock = new();
        Mock<ITranslationService> translationServiceMock = new();

        minecraftVersionServiceMock
            .Setup(service => service.GetInstalledVersionsAsync())
            .ReturnsAsync(["Fabric-1.20.1"]);

        fileServiceMock
            .Setup(service => service.GetMinecraftDataPath())
            .Returns(@"C:\Minecraft");

        gameDirResolverMock
            .Setup(service => service.GetGameDirForVersionAsync("Fabric-1.20.1"))
            .ReturnsAsync(@"C:\Minecraft\Instances\Fabric-1.20.1");

        translationServiceMock
            .Setup(service => service.GetEnglishKeywordForSearch(It.IsAny<string>()))
            .Returns<string>(value => value);

        return new AgentCommunityResourceService(
            new ModrinthService(new HttpClient(new NeverCalledHttpMessageHandler()), new DownloadSourceFactory()),
            new CurseForgeService(new HttpClient(new NeverCalledHttpMessageHandler()), new DownloadSourceFactory()),
            minecraftVersionServiceMock.Object,
            versionInfoServiceMock.Object,
            fileServiceMock.Object,
            gameDirResolverMock.Object,
            inventoryServiceMock.Object,
            updateCheckServiceMock.Object,
            updateServiceMock.Object,
            installPlannerMock.Object,
            installServiceMock.Object,
            translationServiceMock.Object);
    }

    private sealed class NeverCalledHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("该测试不应发起真实 HTTP 请求。");
        }
    }
}