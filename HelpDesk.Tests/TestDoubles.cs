using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;

namespace HelpDesk.Tests;

internal sealed class FakeSettingsService : ISettingsService
{
    public AppSettings Settings { get; set; } = new();
    public SettingsLoadStatus LastLoadStatus { get; private set; } = new();
    public AppSettings Load() => Settings;
    public void Save(AppSettings settings) => Settings = settings;
    public void ResetToDefaults()
    {
        Settings = new AppSettings();
        LastLoadStatus = new SettingsLoadStatus
        {
            RecoveredDefaults = true,
            SchemaVersion = Settings.SchemaVersion,
            Notes = ["Defaults restored in the fake settings store."]
        };
    }
}

internal sealed class FakeDeploymentConfigurationService : IDeploymentConfigurationService
{
    public DeploymentConfiguration Current { get; set; } = new();

    public void ApplyPolicy(AppSettings settings)
    {
        if (!settings.OnboardingDismissed && !string.IsNullOrWhiteSpace(Current.DefaultBehaviorProfile))
            ProductizationPolicies.ApplyBehaviorProfile(settings, Current.DefaultBehaviorProfile);

        if (!string.IsNullOrWhiteSpace(Current.ForceBehaviorProfile))
            ProductizationPolicies.ApplyBehaviorProfile(settings, Current.ForceBehaviorProfile);

        if (!string.IsNullOrWhiteSpace(Current.ForceNotificationMode))
            settings.NotificationMode = Current.ForceNotificationMode;

        if (!string.IsNullOrWhiteSpace(Current.ForceLandingPage))
            settings.DefaultLandingPage = Current.ForceLandingPage;

        if (!string.IsNullOrWhiteSpace(Current.ForceSupportBundleExportLevel))
            settings.SupportBundleExportLevel = Current.ForceSupportBundleExportLevel;

        if (Current.ForceMinimizeToTray.HasValue)
            settings.MinimizeToTray = Current.ForceMinimizeToTray.Value;

        if (Current.ForceRunAtStartup.HasValue)
            settings.RunAtStartup = Current.ForceRunAtStartup.Value;

        if (Current.ForceShowNotifications.HasValue)
            settings.ShowNotifications = Current.ForceShowNotifications.Value;

        if (Current.ForceSafeMaintenanceDefaults.HasValue)
            settings.PreferSafeMaintenanceDefaults = Current.ForceSafeMaintenanceDefaults.Value;

        if (!Current.AllowAdvancedMode)
            settings.AdvancedMode = false;

        if (Current.RestrictTechnicianExports)
            settings.SupportBundleExportLevel = "Basic";
    }

    public PolicyState GetPolicyState(string settingKey, AppSettings? settings = null)
        => ProductizationPolicies.GetPolicyState(Current, settings ?? SettingsFallback, settingKey);

    private static AppSettings SettingsFallback => new();
}

internal sealed class FakeAppLogger : IAppLogger
{
    public void Error(string message, Exception? ex = null) { }
    public void Info(string message) { }
    public void Warn(string message) { }
}

internal sealed class FakeBrandingConfigurationService : IBrandingConfigurationService
{
    public BrandingConfiguration Current { get; set; } = new();
}

internal sealed class FakeRepairCatalogService : IRepairCatalogService
{
    public IReadOnlyList<MasterCategoryDefinition> MasterCategories { get; init; } = [];
    public IReadOnlyList<RepairDefinition> Repairs { get; init; } = [];
    public RepairDefinition? GetRepair(string id) => Repairs.FirstOrDefault(r => r.Id == id);
    public IReadOnlyList<RepairDefinition> GetRepairsByCategory(string categoryId) => Repairs.Where(r => r.MasterCategoryId == categoryId).ToList();
}

internal sealed class FakeRepairHistoryService : IRepairHistoryService
{
    private readonly List<RepairHistoryEntry> _entries = [];
    public IReadOnlyList<RepairHistoryEntry> Entries => _entries;
    public void Record(RepairHistoryEntry entry) => _entries.Insert(0, entry);
    public void Delete(IEnumerable<string> entryIds)
    {
        var idSet = entryIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _entries.RemoveAll(entry => idSet.Contains(entry.Id));
    }
    public void Clear() => _entries.Clear();
}

internal sealed class FakeAutomationHistoryService : IAutomationHistoryService
{
    private readonly List<AutomationRunReceipt> _entries = [];
    public IReadOnlyList<AutomationRunReceipt> Entries => _entries;
    public void Record(AutomationRunReceipt entry) => _entries.Insert(0, entry);
    public void Clear() => _entries.Clear();
}

internal sealed class FakeHealthAlertHistoryService : IHealthAlertHistoryService
{
    private readonly List<HealthAlertHistoryEntry> _entries = [];
    public IReadOnlyList<HealthAlertHistoryEntry> Entries => _entries;
    public void Record(HealthAlert alert) => _entries.Insert(0, new HealthAlertHistoryEntry
    {
        AlertId = alert.Id,
        Title = alert.Title,
        Severity = alert.Severity,
        DetectedUtc = alert.DetectedUtc
    });
}

internal sealed class FakeFixCatalogService : IFixCatalogService
{
    public IReadOnlyList<FixCategory> Categories { get; init; } = [];
    public IReadOnlyList<FixBundle> Bundles { get; init; } = [];

    public FixItem? GetById(string id) =>
        Categories.SelectMany(c => c.Fixes).FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));

    public string GetCategoryTitle(FixItem fix) =>
        Categories.FirstOrDefault(c => c.Fixes.Any(f => f.Id == fix.Id))?.Title ?? "";

    public IReadOnlyList<FixItem> Search(string query) =>
        Categories.SelectMany(c => c.Fixes).Where(f => f.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
}

internal sealed class FakeRepairExecutionService : IRepairExecutionService
{
    public List<string> ExecutedFixIds { get; } = [];
    public Dictionary<string, RepairExecutionResult> ResultsByFixId { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<RepairExecutionResult> ExecuteAsync(FixItem fix, string userQuery = "", CancellationToken cancellationToken = default)
    {
        ExecutedFixIds.Add(fix.Id);
        if (ResultsByFixId.TryGetValue(fix.Id, out var configured))
            return Task.FromResult(configured);

        return Task.FromResult(new RepairExecutionResult
        {
            FixId = fix.Id,
            FixTitle = fix.Title,
            Success = true,
            Output = "ok",
            Summary = "ok",
            Verification = new VerificationResult { Status = VerificationStatus.Passed, Summary = "ok" },
            Rollback = new RollbackInfo { IsAvailable = false, Summary = "none" }
        });
    }
}

internal sealed class FakeQuickScanService : IQuickScanService
{
    public IReadOnlyList<ScanResult> Results { get; set; } = [];

    public Task<IReadOnlyList<ScanResult>> ScanAsync() => Task.FromResult(Results);
}

internal sealed class FakeRunbookCatalogService : IRunbookCatalogService
{
    public IReadOnlyList<RunbookDefinition> Runbooks { get; set; } = [];
    public RunbookDefinition? GetRunbook(string id) => Runbooks.FirstOrDefault(runbook => string.Equals(runbook.Id, id, StringComparison.OrdinalIgnoreCase));
}

internal sealed class FakeRunbookExecutionService : IRunbookExecutionService
{
    public List<string> ExecutedRunbookIds { get; } = [];
    public Dictionary<string, RunbookExecutionSummary> ResultsByRunbookId { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<RunbookExecutionSummary> ExecuteAsync(
        RunbookDefinition runbook,
        string userQuery = "",
        CancellationToken cancellationToken = default)
    {
        ExecutedRunbookIds.Add(runbook.Id);
        if (ResultsByRunbookId.TryGetValue(runbook.Id, out var configured))
            return Task.FromResult(configured);

        return Task.FromResult(new RunbookExecutionSummary
        {
            RunbookId = runbook.Id,
            Title = runbook.Title,
            Success = true,
            CompletedSteps = runbook.Steps.Count,
            Summary = $"{runbook.Title} completed cleanly.",
            Timeline = [$"{runbook.Title} completed."]
        });
    }
}

internal sealed class FakeStatePersistenceService : IStatePersistenceService
{
    public InterruptedOperationState? State { get; private set; }
    public InterruptedOperationState? Load() => State;
    public void Save(InterruptedOperationState state) => State = state;
    public void Clear() => State = null;
}

internal sealed class FakeNotificationService : INotificationService
{
    private readonly List<AppNotification> _items = [];
    public IReadOnlyList<AppNotification> All => _items;
    public int UnreadCount => _items.Count(x => !x.IsRead);
    public void Add(AppNotification notification) => _items.Add(notification);
    public void AddFromScanResult(ScanResult result) => _items.Add(new AppNotification { Title = result.Title, Message = result.Detail });
    public void MarkRead(string notificationId)
    {
        var item = _items.FirstOrDefault(x => x.Id == notificationId);
        if (item is not null) item.IsRead = true;
    }
    public void MarkAllRead() { foreach (var item in _items) item.IsRead = true; }
    public void Remove(string notificationId) => _items.RemoveAll(x => x.Id == notificationId);
    public void Clear() => _items.Clear();
}

internal sealed class FakeLogService : ILogService
{
    private readonly List<FixLogEntry> _entries = [];
    public IReadOnlyList<FixLogEntry> Entries => _entries;
    public void Record(string category, FixItem fix) => _entries.Add(new FixLogEntry { Category = category, FixId = fix.Id, FixTitle = fix.Title, Output = fix.LastOutput ?? "", Success = true });
    public void Clear() => _entries.Clear();
}

internal sealed class FakeSystemInfoService : ISystemInfoService
{
    public Task<SystemSnapshot> GetSnapshotAsync() =>
        Task.FromResult(new SystemSnapshot
        {
            MachineName = Environment.MachineName,
            OsVersion = "Windows 11",
            OsBuild = "26100",
            WindowsEdition = "Pro",
            NetworkType = "Wi-Fi",
            IpAddress = "192.168.1.10",
            InternetReachable = true,
            DiskFreeGb = 120,
            DiskUsedPct = 40,
            RamUsedPct = 52,
            PendingUpdateCount = 1,
            DefenderEnabled = true,
            HasBattery = true,
            BatteryHealth = "Fair"
        });
}

internal sealed class FakeEditionCapabilityService : IEditionCapabilityService
{
    public EditionCapabilitySnapshot Snapshot { get; set; } = new()
    {
        Edition = AppEdition.Pro,
        EvidenceBundles = CapabilityState.Available,
        Runbooks = CapabilityState.Available,
        DeepRepairs = CapabilityState.Available,
        AdvancedMode = CapabilityState.Available,
        AdvancedDiagnostics = CapabilityState.Available,
        TechnicianExports = CapabilityState.Available,
        AdvancedAutomation = CapabilityState.Available,
        AdvancedToolbox = CapabilityState.Available,
        AdvancedRecovery = CapabilityState.Available,
        CustomSupportRouting = CapabilityState.UpgradeRequired,
        ManagedPolicies = CapabilityState.UpgradeRequired,
        WhiteLabelBranding = CapabilityState.UpgradeRequired
    };

    public EditionCapabilitySnapshot GetSnapshot() => Snapshot;

    public CapabilityState GetState(ProductCapability capability) => capability switch
    {
        ProductCapability.EvidenceBundles => Snapshot.EvidenceBundles,
        ProductCapability.Runbooks => Snapshot.Runbooks,
        ProductCapability.DeepRepairs => Snapshot.DeepRepairs,
        ProductCapability.AdvancedMode => Snapshot.AdvancedMode,
        ProductCapability.AdvancedDiagnostics => Snapshot.AdvancedDiagnostics,
        ProductCapability.TechnicianExports => Snapshot.TechnicianExports,
        ProductCapability.AdvancedAutomation => Snapshot.AdvancedAutomation,
        ProductCapability.AdvancedToolbox => Snapshot.AdvancedToolbox,
        ProductCapability.AdvancedRecovery => Snapshot.AdvancedRecovery,
        ProductCapability.CustomSupportRouting => Snapshot.CustomSupportRouting,
        ProductCapability.WhiteLabelBranding => Snapshot.WhiteLabelBranding,
        ProductCapability.ManagedPolicies => Snapshot.ManagedPolicies,
        _ => CapabilityState.Available
    };

    public CapabilityAvailability Describe(ProductCapability capability) => new()
    {
        Capability = capability,
        State = GetState(capability),
        Title = capability.ToString(),
        Summary = GetState(capability).ToString()
    };
}

internal sealed class FakeScriptService : IScriptService
{
    public List<string> InlineScripts { get; } = [];
    public List<string> ExecutedFixIds { get; } = [];
    public bool InlineSuccess { get; set; } = true;

    public Task<(bool Success, string Output)> RunAsync(string script, bool requiresAdmin = false)
    {
        InlineScripts.Add(script);
        return Task.FromResult((InlineSuccess, InlineSuccess ? "ok" : "failed"));
    }

    public Task RunFixAsync(FixItem fix)
    {
        ExecutedFixIds.Add(fix.Id);
        fix.Status = FixStatus.Success;
        fix.LastOutput = "done";
        return Task.CompletedTask;
    }
}

internal sealed class FakeVerificationService : IVerificationService
{
    public VerificationResult Result { get; set; } = new()
    {
        Status = VerificationStatus.Passed,
        Summary = "verified"
    };
    public string PrecheckSummary { get; set; } = "precheck";

    public Task<VerificationResult> VerifyAsync(FixItem fix, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result);

    public Task<string> CapturePrecheckSummaryAsync(FixItem fix, CancellationToken cancellationToken = default) =>
        Task.FromResult(PrecheckSummary);
}

internal sealed class FakeRollbackService : IRollbackService
{
    public RollbackInfo RollbackInfo { get; set; } = new() { IsAvailable = false, Summary = "none" };
    public string? LastTrackedFixId { get; private set; }

    public Task<RollbackInfo> GetRollbackInfoAsync(FixItem fix, CancellationToken cancellationToken = default) =>
        Task.FromResult(RollbackInfo);

    public void TrackSuccessfulRepair(FixItem fix) => LastTrackedFixId = fix.Id;

    public Task<RollbackResult> RollbackLastAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new RollbackResult { Success = true, Summary = "rolled back" });
}

internal sealed class FakeRestorePointService : IRestorePointService
{
    public bool Result { get; set; }
    public int AttemptCount { get; private set; }

    public Task<bool> TryCreateRestorePointAsync(FixItem fix, CancellationToken cancellationToken = default)
    {
        AttemptCount++;
        return Task.FromResult(Result);
    }
}

internal sealed class FakeErrorReportingService : IErrorReportingService
{
    public List<ErrorReportRecord> Records { get; } = [];
    public void Report(ErrorReportRecord record) => Records.Add(record);
}
