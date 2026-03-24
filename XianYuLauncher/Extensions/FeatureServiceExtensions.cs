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
        services.AddSingleton<IErrorAnalysisToolSupportService, ErrorAnalysisToolSupportService>();
        services.AddSingleton<IErrorAnalysisToolDispatcher, ErrorAnalysisToolDispatcher>();
        services.AddSingleton<IErrorAnalysisActionExecutor, ErrorAnalysisActionExecutor>();
        services.AddSingleton<IErrorAnalysisToolHandler, ListInstalledModsToolHandler>();
        services.AddSingleton<IErrorAnalysisToolHandler, GetVersionConfigToolHandler>();
        services.AddSingleton<IErrorAnalysisToolHandler, CheckJavaVersionsToolHandler>();
        services.AddSingleton<IErrorAnalysisToolHandler, SearchKnowledgeBaseToolHandler>();
        services.AddSingleton<IErrorAnalysisToolHandler, ReadModInfoToolHandler>();
        services.AddSingleton<IErrorAnalysisToolHandler, SearchModrinthProjectToolHandler>();
        services.AddSingleton<IErrorAnalysisToolHandler, DeleteModToolHandler>();
        services.AddSingleton<IErrorAnalysisToolHandler, ToggleModToolHandler>();
        services.AddSingleton<IErrorAnalysisToolHandler, SwitchJavaForVersionToolHandler>();
        services.AddSingleton<IErrorAnalysisActionHandler, SearchModrinthProjectActionHandler>();
        services.AddSingleton<IErrorAnalysisActionHandler, DeleteModActionHandler>();
        services.AddSingleton<IErrorAnalysisActionHandler, ToggleModActionHandler>();
        services.AddSingleton<IErrorAnalysisActionHandler, SwitchJavaForVersionActionHandler>();
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
