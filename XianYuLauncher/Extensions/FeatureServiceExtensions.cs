using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<IErrorAnalysisAiOrchestrator, ErrorAnalysisAiOrchestrator>();
        services.AddSingleton<IErrorAnalysisExportService, ErrorAnalysisExportService>();
        services.AddSingleton<IAgentToolSupportService, AgentToolSupportService>();
        services.AddSingleton<IAgentGameInstallService, AgentGameInstallService>();
        services.AddSingleton<IAgentToolDispatcher, AgentToolDispatcher>();
        services.AddSingleton<IAgentActionExecutor, AgentActionExecutor>();
        services.AddSingleton<IAgentToolHandler, ListInstalledModsToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetVersionConfigToolHandler>();
        services.AddSingleton<IAgentToolHandler, CheckJavaVersionsToolHandler>();
        services.AddSingleton<IAgentToolHandler, SearchKnowledgeBaseToolHandler>();
        services.AddSingleton<IAgentToolHandler, ReadModInfoToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetCurrentGameDirectoryToolHandler>();
        services.AddSingleton<IAgentToolHandler, InstallGameToolHandler>();
        services.AddSingleton<IAgentToolHandler, LaunchGameToolHandler>();
        services.AddSingleton<IAgentToolHandler, GetOperationStatusToolHandler>();
        services.AddSingleton<IAgentToolHandler, SearchModrinthProjectToolHandler>();
        services.AddSingleton<IAgentToolHandler, DeleteModToolHandler>();
        services.AddSingleton<IAgentToolHandler, ToggleModToolHandler>();
        services.AddSingleton<IAgentToolHandler, SwitchJavaForVersionToolHandler>();
        services.AddSingleton<IAgentActionHandler, InstallGameActionHandler>();
        services.AddSingleton<IAgentActionHandler, LaunchGameActionHandler>();
        services.AddSingleton<IAgentActionHandler, SearchModrinthProjectActionHandler>();
        services.AddSingleton<IAgentActionHandler, DeleteModActionHandler>();
        services.AddSingleton<IAgentActionHandler, ToggleModActionHandler>();
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
        services.AddSingleton<IModResourceDownloadOrchestrator, ModResourceDownloadOrchestrator>();
        services.AddSingleton<IModDetailLoadOrchestrator, ModDetailLoadOrchestrator>();

        return services;
    }
}
