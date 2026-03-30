using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.Features.ErrorAnalysis.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class CommunityResourceToolHandlersTests
{
    private static readonly ErrorAnalysisSessionContext EmptyContext = new();

    [Fact]
    public async Task GetInstanceCommunityResourcesToolHandler_ShouldForwardArguments()
    {
        FakeAgentCommunityResourceService service = new()
        {
            InventoryResponse = "{\"status\":\"ok\"}"
        };
        GetInstanceCommunityResourcesToolHandler handler = new(service);

        AgentToolExecutionResult result = await handler.ExecuteAsync(
            EmptyContext,
            JObject.Parse("""
            {
              "target_version_name": "Fabric-1.20.1",
              "resource_types": ["mod", "world"]
            }
            """),
            CancellationToken.None);

        result.Message.Should().Be("{\"status\":\"ok\"}");
        service.LastInventoryCommand.Should().NotBeNull();
        service.LastInventoryCommand!.TargetVersionName.Should().Be("Fabric-1.20.1");
        service.LastInventoryCommand.ResourceTypes.Should().BeEquivalentTo(["mod", "world"]);
    }

    [Fact]
    public async Task CheckInstanceCommunityResourceUpdatesToolHandler_ShouldForwardScopeAndIds()
    {
        FakeAgentCommunityResourceService service = new()
        {
            UpdateCheckResponse = "{\"status\":\"ok\"}"
        };
        CheckInstanceCommunityResourceUpdatesToolHandler handler = new(service);

        AgentToolExecutionResult result = await handler.ExecuteAsync(
            EmptyContext,
            JObject.Parse("""
            {
              "target_version_name": "Fabric-1.20.1",
              "resource_instance_ids": ["mod:mods/alpha.jar"],
              "check_scope": "all_installed"
            }
            """),
            CancellationToken.None);

        result.Message.Should().Be("{\"status\":\"ok\"}");
        service.LastUpdateCheckCommand.Should().NotBeNull();
        service.LastUpdateCheckCommand!.TargetVersionName.Should().Be("Fabric-1.20.1");
        service.LastUpdateCheckCommand.ResourceInstanceIds.Should().BeEquivalentTo(["mod:mods/alpha.jar"]);
        service.LastUpdateCheckCommand.CheckScope.Should().Be("all_installed");
    }

    [Fact]
    public async Task UpdateInstanceCommunityResourcesToolHandler_WhenReady_ShouldReturnProposal()
    {
        FakeAgentCommunityResourceService service = new()
        {
            UpdatePreparation = new AgentCommunityResourceUpdatePreparation
            {
                Message = "{\"status\":\"ready_for_confirmation\"}",
                IsReadyForConfirmation = true,
                ButtonText = "更新 2 项资源",
                ProposalParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["target_version_name"] = "Fabric-1.20.1",
                    ["selection_mode"] = "all_updatable"
                }
            }
        };
        UpdateInstanceCommunityResourcesToolHandler handler = new(service);

        AgentToolExecutionResult result = await handler.ExecuteAsync(
            EmptyContext,
            JObject.Parse("""
            {
              "target_version_name": "Fabric-1.20.1",
              "selection_mode": "all_updatable"
            }
            """),
            CancellationToken.None);

        result.Message.Should().Be("{\"status\":\"ready_for_confirmation\"}");
        result.ActionProposal.Should().NotBeNull();
        result.ActionProposal!.ActionType.Should().Be("updateInstanceCommunityResources");
        result.ActionProposal.ButtonText.Should().Be("更新 2 项资源");
        result.ActionProposal.Parameters["target_version_name"].Should().Be("Fabric-1.20.1");
        service.LastUpdateCommand.Should().NotBeNull();
        service.LastUpdateCommand!.SelectionMode.Should().Be("all_updatable");
    }

    [Fact]
    public async Task UpdateInstanceCommunityResourcesActionHandler_ShouldParseSerializedIds()
    {
        FakeAgentCommunityResourceService service = new()
        {
            StartUpdateResponse = "{\"status\":\"started\"}"
        };
        UpdateInstanceCommunityResourcesActionHandler handler = new(service);

        string result = await handler.ExecuteAsync(new AgentActionProposal
        {
            ActionType = "updateInstanceCommunityResources",
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["target_version_name"] = "Fabric-1.20.1",
                ["selection_mode"] = "explicit",
                ["resource_instance_ids"] = "[\"mod:mods/alpha.jar\",\"shader:shaderpacks/cinematic.zip\"]"
            }
        }, CancellationToken.None);

        result.Should().Be("{\"status\":\"started\"}");
        service.StartedUpdateCommand.Should().NotBeNull();
        service.StartedUpdateCommand!.TargetVersionName.Should().Be("Fabric-1.20.1");
        service.StartedUpdateCommand.SelectionMode.Should().Be("explicit");
        service.StartedUpdateCommand.ResourceInstanceIds.Should().BeEquivalentTo(["mod:mods/alpha.jar", "shader:shaderpacks/cinematic.zip"]);
    }

    private sealed class FakeAgentCommunityResourceService : IAgentCommunityResourceService
    {
        public string InventoryResponse { get; set; } = string.Empty;

        public string UpdateCheckResponse { get; set; } = string.Empty;

        public AgentCommunityResourceUpdatePreparation UpdatePreparation { get; set; } = new()
        {
            Message = string.Empty
        };

        public string StartUpdateResponse { get; set; } = string.Empty;

        public AgentInstanceCommunityResourceInventoryCommand? LastInventoryCommand { get; private set; }

        public AgentInstanceCommunityResourceUpdateCheckCommand? LastUpdateCheckCommand { get; private set; }

        public AgentInstanceCommunityResourceUpdateCommand? LastUpdateCommand { get; private set; }

        public AgentInstanceCommunityResourceUpdateCommand? StartedUpdateCommand { get; private set; }

        public Task<string> SearchAsync(string query, string resourceType, IReadOnlyList<string>? platforms, string? gameVersion, string? loader, int limit, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> GetProjectFilesAsync(string projectId, string? resourceType, string? gameVersion, string? loader, int limit, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> GetInstallableInstancesAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> GetInstanceCommunityResourcesAsync(AgentInstanceCommunityResourceInventoryCommand command, CancellationToken cancellationToken)
        {
            LastInventoryCommand = command;
            return Task.FromResult(InventoryResponse);
        }

        public Task<string> CheckInstanceCommunityResourceUpdatesAsync(AgentInstanceCommunityResourceUpdateCheckCommand command, CancellationToken cancellationToken)
        {
            LastUpdateCheckCommand = command;
            return Task.FromResult(UpdateCheckResponse);
        }

        public Task<AgentCommunityResourceUpdatePreparation> PrepareUpdateAsync(AgentInstanceCommunityResourceUpdateCommand command, CancellationToken cancellationToken)
        {
            LastUpdateCommand = command;
            return Task.FromResult(UpdatePreparation);
        }

        public Task<string> StartUpdateAsync(AgentInstanceCommunityResourceUpdateCommand command, CancellationToken cancellationToken)
        {
            StartedUpdateCommand = command;
            return Task.FromResult(StartUpdateResponse);
        }

        public Task<AgentCommunityResourceInstallPreparation> PrepareInstallAsync(AgentCommunityResourceInstallCommand command, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> StartInstallAsync(AgentCommunityResourceInstallCommand command, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}