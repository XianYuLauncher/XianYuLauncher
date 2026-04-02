using FluentAssertions;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public class CommunityResourceInstallPlannerTests
{
    private readonly Mock<IGameDirResolver> _gameDirResolverMock;
    private readonly Mock<ILocalSettingsService> _localSettingsServiceMock;

    public CommunityResourceInstallPlannerTests()
    {
        _gameDirResolverMock = new Mock<IGameDirResolver>();
        _localSettingsServiceMock = new Mock<ILocalSettingsService>();
        _localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<bool?>("DownloadDependencies"))
            .ReturnsAsync((bool?)true);
    }

    private CommunityResourceInstallPlanner CreatePlanner() =>
        new(_gameDirResolverMock.Object, _localSettingsServiceMock.Object);

    [Fact]
    public async Task PlanAsync_ModWithoutTargetVersion_ReturnsMissingRequirement()
    {
        var result = await CreatePlanner().PlanAsync(new CommunityResourceInstallRequest
        {
            ResourceType = "mod",
            FileName = "example.jar"
        });

        result.IsReadyToInstall.Should().BeFalse();
        result.MissingRequirements.Should().ContainSingle();
        result.MissingRequirements[0].Type.Should().Be(CommunityResourceInstallRequirementType.TargetVersion);
    }

    [Fact]
    public async Task PlanAsync_DatapackWithoutSaveName_ReturnsMissingRequirement()
    {
        _gameDirResolverMock
            .Setup(service => service.GetGameDirForVersionAsync("1.20.1-Fabric"))
            .ReturnsAsync(Path.Combine(@"C:\.minecraft", "versions", "1.20.1-Fabric"));

        var result = await CreatePlanner().PlanAsync(new CommunityResourceInstallRequest
        {
            ResourceType = "datapack",
            FileName = "example.zip",
            TargetVersionName = "1.20.1-Fabric"
        });

        result.IsReadyToInstall.Should().BeFalse();
        result.MissingRequirements.Should().ContainSingle();
        result.MissingRequirements[0].Type.Should().Be(CommunityResourceInstallRequirementType.SaveName);
    }

    [Fact]
    public async Task PlanAsync_DatapackWithSaveName_ReturnsWorldDatapacksPath()
    {
        string gameDirectory = Path.Combine(@"C:\.minecraft", "versions", "1.20.1-Fabric");
        _gameDirResolverMock
            .Setup(service => service.GetGameDirForVersionAsync("1.20.1-Fabric"))
            .ReturnsAsync(gameDirectory);

        CommunityResourceInstallPlanningResult result = await CreatePlanner().PlanAsync(new CommunityResourceInstallRequest
        {
            ResourceType = "datapack",
            FileName = "example-datapack.zip",
            TargetVersionName = "1.20.1-Fabric",
            TargetSaveName = "My Adventure World"
        });

        result.IsReadyToInstall.Should().BeTrue();
        result.Plan.Should().NotBeNull();
        result.Plan!.PrimaryTargetDirectory.Should().Be(Path.Combine(gameDirectory, "saves", "My Adventure World", "datapacks"));
        result.Plan.DependencyTargetDirectory.Should().Be(Path.Combine(gameDirectory, "saves", "My Adventure World", "datapacks"));
        result.Plan.TargetSaveName.Should().Be("My Adventure World");
        result.Plan.SavePath.Should().Be(Path.Combine(gameDirectory, "saves", "My Adventure World", "datapacks", "example-datapack.zip"));
    }

    [Fact]
    public async Task PlanAsync_ShaderWithVersion_ReturnsShaderpacksPath()
    {
        string gameDirectory = Path.Combine(@"C:\.minecraft", "versions", "1.20.1-Fabric");
        _gameDirResolverMock
            .Setup(service => service.GetGameDirForVersionAsync("1.20.1-Fabric"))
            .ReturnsAsync(gameDirectory);

        var result = await CreatePlanner().PlanAsync(new CommunityResourceInstallRequest
        {
            ResourceType = "shader",
            FileName = "shaderpack.zip",
            TargetVersionName = "1.20.1-Fabric"
        });

        result.IsReadyToInstall.Should().BeTrue();
        result.Plan.Should().NotBeNull();
        result.Plan!.PrimaryTargetDirectory.Should().Be(Path.Combine(gameDirectory, "shaderpacks"));
        result.Plan.SavePath.Should().Be(Path.Combine(gameDirectory, "shaderpacks", "shaderpack.zip"));
        result.Plan.DownloadDependencies.Should().BeTrue();
    }

    [Fact]
    public async Task PlanAsync_CustomPath_ReturnsCustomDirectoryAndBypassesVersionRequirement()
    {
        var result = await CreatePlanner().PlanAsync(new CommunityResourceInstallRequest
        {
            ResourceType = "resourcepack",
            FileName = "pack.zip",
            UseCustomDownloadPath = true,
            CustomDownloadPath = @"D:\Downloads\packs"
        });

        result.IsReadyToInstall.Should().BeTrue();
        result.Plan.Should().NotBeNull();
        result.Plan!.PrimaryTargetDirectory.Should().Be(@"D:\Downloads\packs");
        result.Plan.DependencyTargetDirectory.Should().Be(@"D:\Downloads\packs");
        result.Plan.UseTargetDirectoryForAllDependencies.Should().BeTrue();
    }

    [Theory]
    [InlineData("..\\evil.jar")]
    [InlineData("nested/folder/evil.jar")]
    public async Task PlanAsync_WhenFileNameContainsPathSegments_ShouldReturnMissingRequirement(string fileName)
    {
        string gameDirectory = Path.Combine(@"C:\.minecraft", "versions", "1.20.1-Fabric");
        _gameDirResolverMock
            .Setup(service => service.GetGameDirForVersionAsync("1.20.1-Fabric"))
            .ReturnsAsync(gameDirectory);

        var result = await CreatePlanner().PlanAsync(new CommunityResourceInstallRequest
        {
            ResourceType = "mod",
            FileName = fileName,
            TargetVersionName = "1.20.1-Fabric"
        });

        result.IsReadyToInstall.Should().BeFalse();
        result.MissingRequirements.Should().ContainSingle();
        result.MissingRequirements[0].Type.Should().Be(CommunityResourceInstallRequirementType.FileName);
        result.MissingRequirements[0].Message.Should().Contain("文件名无效");
    }

    [Fact]
    public async Task PlanAsync_World_ReturnsSavesDirectoryAndModsDependencyDirectory()
    {
        string gameDirectory = Path.Combine(@"C:\.minecraft", "versions", "1.20.1");
        _gameDirResolverMock
            .Setup(service => service.GetGameDirForVersionAsync("1.20.1"))
            .ReturnsAsync(gameDirectory);

        var result = await CreatePlanner().PlanAsync(new CommunityResourceInstallRequest
        {
            ResourceType = "world",
            FileName = "world.zip",
            TargetVersionName = "1.20.1"
        });

        result.IsReadyToInstall.Should().BeTrue();
        result.Plan.Should().NotBeNull();
        result.Plan!.ResourceKind.Should().Be(CommunityResourceKind.World);
        result.Plan.PrimaryTargetDirectory.Should().Be(Path.Combine(gameDirectory, "saves"));
        result.Plan.DependencyTargetDirectory.Should().Be(Path.Combine(gameDirectory, "mods"));
        result.Plan.SavePath.Should().Be(Path.Combine(gameDirectory, "saves", "world.zip"));
        result.Plan.TargetSaveName.Should().BeNull();
    }

    [Fact]
    public async Task PlanAsync_RespectsDownloadDependenciesSetting()
    {
        string gameDirectory = Path.Combine(@"C:\.minecraft", "versions", "1.20.1-Fabric");
        _gameDirResolverMock
            .Setup(service => service.GetGameDirForVersionAsync("1.20.1-Fabric"))
            .ReturnsAsync(gameDirectory);
        _localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<bool?>("DownloadDependencies"))
            .ReturnsAsync((bool?)false);

        var result = await CreatePlanner().PlanAsync(new CommunityResourceInstallRequest
        {
            ResourceType = "mod",
            FileName = "example.jar",
            TargetVersionName = "1.20.1-Fabric"
        });

        result.IsReadyToInstall.Should().BeTrue();
        result.Plan.Should().NotBeNull();
        result.Plan!.DownloadDependencies.Should().BeFalse();
    }
}