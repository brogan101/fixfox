using HelpDesk.Application.Interfaces;
using HelpDesk.Infrastructure.Fixes;
using HelpDesk.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HelpDesk.Application.Services;

public static class AppServiceRegistrar
{
    public static void Configure(IServiceCollection services, bool headless)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IScriptService, ScriptService>();
        services.AddSingleton<IQuickScanService, QuickScanService>();
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        services.AddSingleton<ICrashLogger, CrashLogger>();
        services.AddSingleton<IElevationService, ElevationService>();

        services.AddSingleton<FixCatalogService>();
        services.AddSingleton<IFixCatalogProvider, BuiltInFixCatalogProvider>();
        services.AddSingleton<IFixCatalogProvider, ExternalCatalogProvider>();
        services.AddSingleton<IFixCatalogService, MergedFixCatalogService>();
        services.AddSingleton<IRepairCatalogService, RepairCatalogService>();
        services.AddSingleton<ITriageEngine, WeightedTriageEngine>();
        services.AddSingleton<IRunbookCatalogService, RunbookCatalogService>();
        services.AddSingleton<IVerificationService, VerificationService>();
        services.AddSingleton<IRollbackService, RollbackService>();
        services.AddSingleton<IRestorePointService, RestorePointService>();
        services.AddSingleton<IStatePersistenceService, StatePersistenceService>();
        services.AddSingleton<IRepairHistoryService, RepairHistoryService>();
        services.AddSingleton<IAutomationHistoryService, AutomationHistoryService>();
        services.AddSingleton<IRepairExecutionService, RepairExecutionService>();
        services.AddSingleton<IGuidedRepairExecutionService, GuidedRepairExecutionService>();
        services.AddSingleton<IRunbookExecutionService, RunbookExecutionService>();
        services.AddSingleton<IHealthCheckService, HealthCheckService>();
        services.AddSingleton<IEvidenceBundleService, EvidenceBundleService>();
        services.AddSingleton<IDeploymentConfigurationService, DeploymentConfigurationService>();
        services.AddSingleton<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddSingleton<IBrandingConfigurationService, BrandingConfigurationService>();
        services.AddSingleton<IEditionCapabilityService, EditionCapabilityService>();
        services.AddSingleton<IAppUpdateService, AppUpdateService>();
        services.AddSingleton<IErrorReportingService, ErrorReportingService>();
        services.AddSingleton<IToolboxService, ToolboxService>();
        services.AddSingleton<IMaintenanceProfileService, MaintenanceProfileService>();
        services.AddSingleton<ISupportCenterService, SupportCenterService>();
        services.AddSingleton<ICommandPaletteService, CommandPaletteService>();
        services.AddSingleton<IDashboardWorkspaceService, DashboardWorkspaceService>();
        services.AddSingleton<IAutomationCoordinatorService, AutomationCoordinatorService>();

        if (headless)
            services.AddSingleton<IAppLogger, ConsoleAppLogger>();
        else
            services.AddSingleton<IAppLogger, AppLogger>();

        services.AddSingleton<StartupVerifier>();
        services.AddSingleton<DuplicateFileService>();
        services.AddSingleton<InstalledProgramsService>();
        services.AddSingleton<StartupAppsService>();
        services.AddSingleton<StorageInsightsService>();
        services.AddSingleton<SchedulerService>();
    }
}
