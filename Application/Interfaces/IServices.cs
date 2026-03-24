using HelpDesk.Domain.Models;

namespace HelpDesk.Application.Interfaces;

public interface IScriptService
{
    Task<(bool Success, string Output)> RunAsync(string script, bool requiresAdmin = false);
    Task RunFixAsync(FixItem fix);
}

public interface IFixCatalogService
{
    IReadOnlyList<FixCategory> Categories { get; }
    IReadOnlyList<FixBundle>   Bundles    { get; }
    FixItem?                   GetById(string id);
    string                     GetCategoryTitle(FixItem fix);
    IReadOnlyList<FixItem>     Search(string query);
}

public interface IQuickScanService
{
    Task<IReadOnlyList<ScanResult>> ScanAsync();
}

public interface ISystemInfoService
{
    Task<SystemSnapshot> GetSnapshotAsync();
}

public interface ILogService
{
    IReadOnlyList<FixLogEntry> Entries { get; }
    void Record(string category, FixItem fix);
    void Clear();
}

public interface INotificationService
{
    IReadOnlyList<AppNotification> All         { get; }
    int                            UnreadCount { get; }
    void Add(AppNotification notification);
    void AddFromScanResult(ScanResult result);
    void MarkRead(string notificationId);
    void MarkAllRead();
    void Remove(string notificationId);
    void Clear();
}

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public interface IFixCatalogProvider
{
    string Name { get; }
    IReadOnlyList<FixCategory> Categories { get; }
    IReadOnlyList<FixBundle> Bundles { get; }
    IReadOnlyList<RunbookDefinition> Runbooks { get; }
    IReadOnlyList<RepairDefinition> Repairs { get; }
    IReadOnlyList<MasterCategoryDefinition> MasterCategories { get; }
}

public interface IRepairCatalogService
{
    IReadOnlyList<MasterCategoryDefinition> MasterCategories { get; }
    IReadOnlyList<RepairDefinition> Repairs { get; }
    RepairDefinition? GetRepair(string id);
    IReadOnlyList<RepairDefinition> GetRepairsByCategory(string categoryId);
}

public interface ITriageEngine
{
    TriageResult Analyze(string query, TriageContext? context = null);
}

public interface IRunbookCatalogService
{
    IReadOnlyList<RunbookDefinition> Runbooks { get; }
    RunbookDefinition? GetRunbook(string id);
}

public interface IRunbookExecutionService
{
    Task<RunbookExecutionSummary> ExecuteAsync(
        RunbookDefinition runbook,
        string userQuery = "",
        CancellationToken cancellationToken = default);
}

public interface IRepairExecutionService
{
    Task<RepairExecutionResult> ExecuteAsync(FixItem fix, string userQuery = "", CancellationToken cancellationToken = default);
}

public interface IVerificationService
{
    Task<VerificationResult> VerifyAsync(FixItem fix, CancellationToken cancellationToken = default);
}

public interface IRollbackService
{
    Task<RollbackInfo> GetRollbackInfoAsync(FixItem fix, CancellationToken cancellationToken = default);
    void TrackSuccessfulRepair(FixItem fix);
    Task<RollbackResult> RollbackLastAsync(CancellationToken cancellationToken = default);
}

public interface IRestorePointService
{
    Task<bool> TryCreateRestorePointAsync(FixItem fix, CancellationToken cancellationToken = default);
}

public interface IStatePersistenceService
{
    InterruptedOperationState? Load();
    void Save(InterruptedOperationState state);
    void Clear();
}

public interface IRepairHistoryService
{
    IReadOnlyList<RepairHistoryEntry> Entries { get; }
    void Record(RepairHistoryEntry entry);
}

public interface IEvidenceBundleService
{
    Task<EvidenceBundleManifest> ExportAsync(
        string userIssue,
        TriageResult? triageResult,
        HealthCheckReport? healthReport,
        RunbookExecutionSummary? runbookSummary,
        CancellationToken cancellationToken = default);
}

public interface IKnowledgeBaseService
{
    IReadOnlyList<KnowledgeBaseEntry> Entries { get; }
    KnowledgeBaseEntry? Get(string key);
}

public interface IBrandingConfigurationService
{
    BrandingConfiguration Current { get; }
}

public interface IEditionCapabilityService
{
    EditionCapabilitySnapshot GetSnapshot();
}

public interface IAppUpdateService
{
    Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}

public interface IErrorReportingService
{
    void Report(ErrorReportRecord record);
}

public interface IHealthCheckService
{
    Task<HealthCheckReport> RunFullAsync(CancellationToken cancellationToken = default);
}

public interface IAppLogger
{
    void Info (string message);
    void Warn (string message);
    void Error(string message, Exception? ex = null);
}

public interface ICrashLogger
{
    void Log(Exception ex, string? context = null);
}

public interface IElevationService
{
    bool IsElevated { get; }
    bool RelaunchElevated(string? extraArgs = null);
}
