using Microsoft.Extensions.DependencyInjection;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ErrorAnalysis.Services;
using XianYuLauncher.Features.ModDownloadDetail.Services;
using XianYuLauncher.Features.VersionManagement.Services;

namespace XianYuLauncher.Extensions;

/// <summary>
/// Features 模块服务（VersionManagement、ModDownloadDetail 等）的 DI 注册扩展。
/// </summary>
internal static class FeatureServiceExtensions
{
    public static IServiceCollection AddFeatureServices(this IServiceCollection services)
    {
        services.AddSingleton<ErrorAnalysisSessionState>();
        services.AddSingleton<IErrorAnalysisSessionCoordinator, ErrorAnalysisSessionCoordinator>();
        services.AddSingleton<IErrorAnalysisLogService, ErrorAnalysisLogService>();
        services.AddSingleton<IErrorAnalysisSessionContextQueryService, ErrorAnalysisSessionContextQueryService>();
        services.AddSingleton<IErrorAnalysisAIOrchestrator, ErrorAnalysisAIOrchestrator>();
        services.AddSingleton<IErrorAnalysisExportService, ErrorAnalysisExportService>();
        services.AddSingleton<IAgentToolSupportService, AgentToolSupportService>();
        services.AddSingleton<IAgentSettingsQueryService, AgentSettingsQueryService>();
        services.AddSingleton<IAgentSettingsActionProposalService, AgentSettingsActionProposalService>();
        services.AddSingleton<IAgentSettingsWriteService, AgentSettingsWriteService>();
        services.AddSingleton<ILaunchOperationTracker, LaunchOperationTracker>();
        services.AddSingleton<IAgentOperationStatusService, AgentOperationStatusService>();
        services.AddSingleton<IAgentGameInstallService, AgentGameInstallService>();
        services.AddSingleton<IAgentCommunityResourceService, AgentCommunityResourceService>();
        services.AddSingleton<ILauncherAIWorkspacePersistenceService, LauncherAIWorkspacePersistenceService>();
        services.AddSingleton<IAgentToolDispatcher, AgentToolDispatcher>();
        services.AddSingleton<IAgentActionExecutor, AgentActionExecutor>();
        services.AddSingleton<IAgentToolHandler, ListInstalledModsToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetVersionConfigToolHandler>();
        services.AddSingleton<IAgentToolHandler, CheckJavaVersionsToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetMinecraftPathsToolHandler>();
        services.AddSingleton<IAgentToolHandler, SwitchMinecraftPathToolHandler>();
        services.AddSingleton<IAgentToolHandler, PatchGlobalLaunchSettingsToolHandler>();
        services.AddSingleton<IAgentToolHandler, PatchInstanceLaunchSettingsToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetGlobalLaunchSettingsToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetEffectiveLaunchSettingsToolHandler>();
        services.AddSingleton<IAgentToolHandler, SearchKnowledgeBaseToolHandler>();
        services.AddSingleton<IAgentToolHandler, ReadModInfoToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetCurrentGameDirectoryToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetLaunchContextToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetLogTailToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetLogChunkToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetProfilesToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetGameManifestToolHandler>();
        services.AddSingleton<IAgentToolHandler, InstallGameToolHandler>();
        services.AddSingleton<IAgentToolHandler, LaunchGameToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetOperationStatusToolHandler>();
        services.AddSingleton<IAgentToolHandler, SearchCommunityResourcesToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetCommunityResourceFilesToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetInstancesToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetInstanceCommunityResourcesToolHandler>();
        services.AddSingleton<IAgentToolHandler, CheckInstanceCommunityResourceUpdatesToolHandler>();
        services.AddSingleton<IAgentToolHandler, UpdateInstanceCommunityResourcesToolHandler>();
        services.AddSingleton<IAgentToolHandler, InstallCommunityResourceToolHandler>();
        services.AddSingleton<IAgentToolHandler, SearchModrinthProjectToolHandler>();
        services.AddSingleton<IAgentToolHandler, DeleteModToolHandler>();
        services.AddSingleton<IAgentToolHandler, ToggleModToolHandler>();
        services.AddSingleton<IAgentToolHandler, SwitchJavaForVersionToolHandler>();
        services.AddSingleton<IAgentActionHandler, InstallGameActionHandler>();
        services.AddSingleton<IAgentActionHandler, LaunchGameActionHandler>();
        services.AddSingleton<IAgentActionHandler, UpdateInstanceCommunityResourcesActionHandler>();
        services.AddSingleton<IAgentActionHandler, InstallCommunityResourceActionHandler>();
        services.AddSingleton<IAgentActionHandler, SearchModrinthProjectActionHandler>();
        services.AddSingleton<IAgentActionHandler, DeleteModActionHandler>();
        services.AddSingleton<IAgentActionHandler, ToggleModActionHandler>();
        services.AddSingleton<IAgentActionHandler, SwitchMinecraftPathActionHandler>();
        services.AddSingleton<IAgentActionHandler, PatchGlobalLaunchSettingsActionHandler>();
        services.AddSingleton<IAgentActionHandler, PatchInstanceLaunchSettingsActionHandler>();
        services.AddSingleton<IAgentActionHandler, SwitchJavaForVersionActionHandler>();
        services.AddSingleton<IIconMetadataPipelineService, IconMetadataPipelineService>();
        services.AddSingleton<IVersionSettingsOrchestrator, VersionSettingsOrchestrator>();
        services.AddSingleton<IOverviewDataService, OverviewDataService>();
        services.AddSingleton<IDragDropImportService, DragDropImportService>();
        services.AddSingleton<IResourceTransferInfrastructureService, ResourceTransferInfrastructureService>();
        services.AddSingleton<IVersionPageLoadOrchestrator, VersionPageLoadOrchestrator>();
        services.AddSingleton<ILoaderUiOrchestrator, LoaderUiOrchestrator>();
        services.AddSingleton<IVersionPathNavigationService, VersionPathNavigationService>();
        services.AddSingleton<IScreenshotInteractionService, ScreenshotInteractionService>();
        services.AddSingleton<IResourceIconLoadCoordinator, ResourceIconLoadCoordinator>();
        services.AddSingleton<ICommunityResourceInstallService, CommunityResourceInstallService>();
        services.AddSingleton<IModResourceDownloadOrchestrator, ModResourceDownloadOrchestrator>();
        services.AddSingleton<IModDetailLoadOrchestrator, ModDetailLoadOrchestrator>();

        return services;
    }
}
