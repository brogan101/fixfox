using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using HelpDesk.Domain.Enums;

namespace HelpDesk.Domain.Models;

// ── Fix catalog models ─────────────────────────────────────────────────────

public sealed class FixStep
{
    public string  Id          { get; init; } = Guid.NewGuid().ToString("N");
    public string  Title       { get; init; } = "";
    public string  Instruction { get; init; } = "";
    public string? Script      { get; init; }
}

public sealed class FixItem : INotifyPropertyChanged
{
    public string        Id             { get; init; } = Guid.NewGuid().ToString();
    public string        Title          { get; init; } = "";
    public string        Description    { get; init; } = "";
    public FixType       Type           { get; init; } = FixType.Silent;
    public string?       Script         { get; init; }
    public List<FixStep> Steps          { get; init; } = [];
    public bool          RequiresAdmin  { get; init; }
    public string        EstTime        { get; init; } = "";
    /// <summary>Structured tags for category filtering.</summary>
    public string[]      Tags           { get; init; } = [];
    /// <summary>Plain-English phrases a non-technical user might type to find this fix.</summary>
    public string[]      Keywords       { get; init; } = [];

    // Runtime state (mutable)
    private FixStatus _status = FixStatus.Idle;
    private string? _lastOutput;
    private bool _isFavorite;
    private bool _isPinned;

    public FixStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsSucceeded));
            OnPropertyChanged(nameof(IsFailed));
            OnPropertyChanged(nameof(HasStatusIndicator));
            OnPropertyChanged(nameof(StatusSummary));
        }
    }

    public string? LastOutput
    {
        get => _lastOutput;
        set
        {
            if (_lastOutput == value) return;
            _lastOutput = value;
            OnPropertyChanged();
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value) return;
            _isFavorite = value;
            OnPropertyChanged();
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned == value) return;
            _isPinned = value;
            OnPropertyChanged();
        }
    }

    // Computed helpers for bindings
    public bool HasScript => !string.IsNullOrWhiteSpace(Script);
    public bool HasSteps  => Steps.Count > 0;
    public bool IsIdle => Status == FixStatus.Idle;
    public bool IsRunning => Status == FixStatus.Running;
    public bool IsSucceeded => Status == FixStatus.Success;
    public bool IsFailed => Status == FixStatus.Failed;
    public bool HasStatusIndicator => Status != FixStatus.Idle;
    public string StatusSummary => Status switch
    {
        FixStatus.Running => "Running",
        FixStatus.Success => "Completed",
        FixStatus.Failed => "Failed",
        _ => ""
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class FixCategory
{
    public string        Id    { get; init; } = "";
    public string        Icon  { get; init; } = "\uE90F";
    public string        Title { get; init; } = "";
    public List<FixItem> Fixes { get; init; } = [];
}

public sealed class FixBundle
{
    public string        Id          { get; init; } = "";
    public string        Icon        { get; init; } = "\uE90F";
    public string        Title       { get; init; } = "";
    public string        Description { get; init; } = "";
    public string        EstTime     { get; init; } = "~1 min";
    public List<string>  FixIds      { get; init; } = [];
}

// ── Scan / diagnostic models ───────────────────────────────────────────────

public sealed class ScanResult
{
    public string        Title      { get; init; } = "";
    public string        Detail     { get; init; } = "";
    public ScanSeverity  Severity   { get; init; } = ScanSeverity.Good;
    public string?       FixId      { get; init; }
    public string?       Suggestion { get; init; }
}

// ── Notification model ────────────────────────────────────────────────────

public sealed class AppNotification
{
    public string     Id          { get; init; } = Guid.NewGuid().ToString();
    public DateTime   Timestamp   { get; init; } = DateTime.Now;
    public NotifLevel Level       { get; init; } = NotifLevel.Info;
    public string     Title       { get; init; } = "";
    public string     Message     { get; init; } = "";
    public bool       IsRead      { get; set; }
    public string?    ActionFixId { get; init; }
}

// ── History / log model ───────────────────────────────────────────────────

public sealed class FixLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string   Category  { get; init; } = "";
    public string   FixTitle  { get; init; } = "";
    public string   FixId     { get; init; } = "";
    public bool     Success   { get; init; }
    public string   Output    { get; init; } = "";
}

// ── System snapshot model ─────────────────────────────────────────────────

public sealed record SystemSnapshot
{
    public DateTime CapturedAt          { get; init; } = DateTime.Now;
    public string   MachineName         { get; init; } = "";
    public string   UserName            { get; init; } = "";
    public string   OsVersion           { get; init; } = "";
    public string   OsBuild             { get; init; } = "";
    public string   WindowsEdition      { get; init; } = "";
    public DateTime LastBoot            { get; init; }
    public string   Uptime              { get; init; } = "";

    // CPU
    public string   CpuName             { get; init; } = "";
    public int      CpuCores            { get; init; }
    public int      CpuThreads          { get; init; }
    public float    CpuUsagePct         { get; init; }
    public float    CpuTempC            { get; init; }
    public string   CpuSpeedGhz         { get; init; } = "";

    // RAM
    public long     RamTotalMb          { get; init; }
    public long     RamFreeMb           { get; init; }
    public float    RamUsedPct          { get; init; }

    // Disk
    public long     DiskTotalGb         { get; init; }
    public long     DiskFreeGb          { get; init; }
    public float    DiskUsedPct         { get; init; }
    public string   DiskHealth          { get; init; } = "Unknown";
    public string   DiskType            { get; init; } = "";

    // GPU
    public string   GpuName             { get; init; } = "";
    public long     GpuVramMb           { get; init; }
    public float    GpuTempC            { get; init; }

    // Network
    public string   NetworkAdapterName  { get; init; } = "";
    public string   NetworkType         { get; init; } = "";
    public string   IpAddress           { get; init; } = "";
    public long     NetworkSpeedMbps    { get; init; }
    public bool     InternetReachable   { get; init; }
    public string   WifiSignal          { get; init; } = "";

    // Battery
    public bool     HasBattery          { get; init; }
    public int      BatteryPct          { get; init; }
    public string   BatteryStatus       { get; init; } = "";
    public string   BatteryHealth       { get; init; } = "";

    // Security
    public bool     DefenderEnabled     { get; init; }
    public bool     DefenderUpdated     { get; init; }
    public bool     WindowsActivated    { get; init; }
    public int      PendingUpdateCount  { get; init; }

    // Board
    public string   Motherboard         { get; init; } = "";
    public string   BiosVersion         { get; init; } = "";
}

// ── Settings model ────────────────────────────────────────────────────────

public sealed class AppSettings
{
    public int     SettingsSchemaVersion     { get; set; } = 4;

    // Appearance
    public string  Theme                     { get; set; } = "Dark";
    public string  Accent                    { get; set; } = "Orange";
    public bool    SidebarCollapsed          { get; set; } = false;

    // Window state
    public double  WindowWidth               { get; set; } = 1120;
    public double  WindowHeight              { get; set; } = 740;
    public double  WindowLeft                { get; set; } = -1;
    public double  WindowTop                 { get; set; } = -1;

    // Behaviour
    public bool    MinimizeToTray            { get; set; } = false;
    public bool    ShowNotifications         { get; set; } = true;
    public bool    LogFixHistory             { get; set; } = true;
    public bool    RunQuickScanOnLaunch      { get; set; } = true;
    public bool    ShowTrayBalloons          { get; set; } = true;
    public bool    ConfirmBeforeAutoFix      { get; set; } = false;
    public bool    RunAtStartup              { get; set; } = false;
    public string  WeeklyTuneUpDay           { get; set; } = "Sunday";
    public string  WeeklyTuneUpTime          { get; set; } = "10:00";
    public bool    CheckForUpdatesOnLaunch   { get; set; } = true;
    public bool    EnableExtensionCatalogs   { get; set; } = true;
    public bool    TechnicianMode            { get; set; } = false;
    public string  NotificationMode          { get; set; } = "Standard";
    public bool    PreferSafeMaintenanceDefaults { get; set; } = true;
    public bool    RecoverInterruptedOperations  { get; set; } = true;
    public bool    ConfirmBeforeClosingActiveWork { get; set; } = true;
    public string  SupportBundleExportLevel  { get; set; } = "Basic";
    public DateTime? AutomationPausedUntilUtc { get; set; }
    public string  AutomationQuietHoursStart { get; set; } = "22:00";
    public string  AutomationQuietHoursEnd { get; set; } = "07:00";
    public string  UpdateFeedUrl             { get; set; } = "";
    public string  BrandingConfigPath        { get; set; } = "";
    public string  KnowledgeBaseConfigPath   { get; set; } = "";
    public string  DeploymentConfigPath      { get; set; } = "";
    public bool    RunFirstHealthCheckAfterSetup { get; set; } = true;
    public DateTime? LastLaunchUtc           { get; set; }
    public DateTime? LastCleanShutdownUtc    { get; set; }
    public bool    LastSessionEndedCleanly   { get; set; } = true;
    public string  LastLaunchedVersion       { get; set; } = "";
    public AppEdition Edition                { get; set; } = AppEdition.Basic;

    // Scan thresholds
    public int     CpuWarningPct             { get; set; } = 85;
    public int     RamWarningPct             { get; set; } = 85;
    public int     DiskWarningPct            { get; set; } = 90;
    public int     CpuTempWarningC           { get; set; } = 85;

    // Personalization
    public string  LastFixCategory           { get; set; } = "";
    public List<string> FavoriteFixIds       { get; set; } = [];
    public List<string> PinnedFixIds         { get; set; } = [];
    public List<string> RecentSearches       { get; set; } = [];

    // First-launch
    public bool    OnboardingDismissed       { get; set; } = false;
    public bool    PrivacyNoticeDismissed    { get; set; } = false;
    public string  BehaviorProfile           { get; set; } = "Standard";
    public bool    AdvancedMode              { get; set; } = false;
    public string  DefaultLandingPage        { get; set; } = "Dashboard";
    public List<string> IgnoredRecommendationKeys { get; set; } = [];
    public List<string> SnoozedAlertKeys          { get; set; } = [];
    public List<AutomationRuleSettings> AutomationRules { get; set; } = [];
}

public sealed class SettingsLoadStatus
{
    public bool LoadedFromPrimary { get; init; }
    public bool LoadedFromBackup { get; init; }
    public bool RecoveredDefaults { get; init; }
    public bool MigrationApplied { get; init; }
    public bool ValidationApplied { get; init; }
    public bool PreviousSessionEndedUncleanly { get; init; }
    public int SchemaVersion { get; init; }
    public List<string> Notes { get; init; } = [];
    public bool HasRecoveryNotice => LoadedFromBackup || RecoveredDefaults || MigrationApplied || ValidationApplied || PreviousSessionEndedUncleanly;
    public string Summary => HasRecoveryNotice
        ? string.Join(" ", Notes)
        : "Settings loaded cleanly.";
}

public sealed class TriageContext
{
    public string Query { get; init; } = "";
    public bool HasBattery { get; init; }
    public bool PendingRebootDetected { get; init; }
    public bool HasRecentFailures { get; init; }
    public bool InternetReachable { get; init; }
    public long DiskFreeGb { get; init; }
    public float RamUsedPct { get; init; }
    public string NetworkType { get; init; } = "";
    public IReadOnlyList<string> RecentSymptoms { get; init; } = [];
}

public sealed class TriageCandidate
{
    public string CategoryId { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public string ProbableSubIssue { get; init; } = "";
    public int ConfidenceScore { get; init; }
    public string ConfidenceLabel { get; init; } = "Possible";
    public string WhatIThinkIsWrong { get; init; } = "";
    public string WhyIThinkThat { get; init; } = "";
    public string WhatWillHappen { get; init; } = "";
    public string AdvancedDetails { get; init; } = "";
    public string SafestFirstAction { get; init; } = "";
    public string StrongerNextAction { get; init; } = "";
    public string EscalationSignal { get; init; } = "";
    public bool RecommendDiagnosticsFirst { get; init; }
    public bool IsHighConfidence => ConfidenceScore >= 75;
    public bool IsLowConfidence => ConfidenceScore < 45;
    public List<string> RecommendedFixIds { get; init; } = [];
}

public sealed class TriageResult
{
    public string Query { get; init; } = "";
    public List<TriageCandidate> Candidates { get; init; } = [];
}

public sealed class MasterCategoryDefinition
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string IconKey { get; init; } = "\uE90F";
    public string HealthScoreDomain { get; init; } = "";
    public int DisplayPriority { get; init; }
    public List<string> CommonUserPhrases { get; init; } = [];
    public List<string> SymptomPatterns { get; init; } = [];
    public List<string> DefaultRecommendedRepairs { get; init; } = [];
}

public sealed class VerificationDefinition
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public VerificationStrategyKind Strategy { get; init; } = VerificationStrategyKind.HeuristicFallback;
    public List<string> PreChecks { get; init; } = [];
    public List<string> PostChecks { get; init; } = [];
    public bool AllowHeuristicFallback { get; init; } = true;
}

public sealed class RepairDefinition
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string ShortDescription { get; init; } = "";
    public string LongDescription { get; init; } = "";
    public string UserProblemSummary { get; init; } = "";
    public string WhySuggested { get; init; } = "";
    public string MasterCategoryId { get; init; } = "";
    public List<string> SupportedSubIssues { get; init; } = [];
    public List<string> SearchPhrases { get; init; } = [];
    public List<string> Synonyms { get; init; } = [];
    public List<string> ConfidenceBoostSignals { get; init; } = [];
    public List<string> Diagnostics { get; init; } = [];
    public List<string> Preconditions { get; init; } = [];
    public List<string> EnvironmentRequirements { get; init; } = [];
    public List<string> QuickFixActions { get; init; } = [];
    public List<string> DeepFixActions { get; init; } = [];
    public List<string> VerificationChecks { get; init; } = [];
    public List<string> KnowledgeBaseKeys { get; init; } = [];
    public string VerificationStrategyId { get; init; } = "";
    public List<string> RelatedWindowsTools { get; init; } = [];
    public List<string> RelatedWindowsSettings { get; init; } = [];
    public List<string> RelatedRunbooks { get; init; } = [];
    public AppEdition MinimumEdition { get; init; } = AppEdition.Basic;
    public RepairTier Tier { get; init; } = RepairTier.SafeUser;
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Low;
    public bool RequiresAdmin { get; init; }
    public bool MayRequireReboot { get; init; }
    public bool SupportsRestorePoint { get; init; }
    public bool SupportsRollback { get; init; }
    public string WhatWillHappen { get; init; } = "";
    public string UserFacingWarnings { get; init; } = "";
    public string SuggestedNextStepOnSuccess { get; init; } = "";
    public string SuggestedNextStepOnFailure { get; init; } = "";
    public string EscalationHint { get; init; } = "";
    public string RollbackHint { get; init; } = "";
    public List<string> EvidenceExportTags { get; init; } = [];
    public string SuppressionScopeHint { get; init; } = "";
    public string AdvancedNotes { get; init; } = "";
    public VerificationDefinition Verification { get; init; } = new();
    public FixItem Fix { get; init; } = new();
}

public sealed class RunbookStepDefinition
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public RunbookStepKind StepKind { get; init; } = RunbookStepKind.Message;
    public string? LinkedRepairId { get; init; }
    public string? LinkedKnowledgeBaseId { get; init; }
    public bool RequiresAdmin { get; init; }
    public bool SupportsRollback { get; init; }
    public bool StopOnFailure { get; init; } = true;
    public string PostStepMessage { get; init; } = "";
}

public sealed class RunbookDefinition
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string CategoryId { get; init; } = "";
    public string EstTime { get; init; } = "~2 min";
    public bool RequiresAdmin { get; init; }
    public bool SupportsRollback { get; init; }
    public bool SupportsRestorePoint { get; init; }
    public AppEdition MinimumEdition { get; init; } = AppEdition.Basic;
    public string TriggerHint { get; init; } = "";
    public List<RunbookStepDefinition> Steps { get; init; } = [];
}

public sealed class VerificationResult
{
    public VerificationStatus Status { get; init; } = VerificationStatus.NotRun;
    public string Summary { get; init; } = "";
    public List<string> Details { get; init; } = [];
}

public sealed class RollbackInfo
{
    public bool IsAvailable { get; init; }
    public string Summary { get; init; } = "";
}

public sealed class RollbackResult
{
    public bool Success { get; init; }
    public string Summary { get; init; } = "";
}

public sealed class RepairExecutionResult
{
    public string FixId { get; init; } = "";
    public string FixTitle { get; init; } = "";
    public ExecutionOutcome Outcome { get; init; } = ExecutionOutcome.Completed;
    public bool Success { get; init; }
    public string Output { get; init; } = "";
    public string Summary { get; init; } = "";
    public string FailureSummary { get; init; } = "";
    public string NextStep { get; init; } = "";
    public VerificationResult Verification { get; init; } = new();
    public RollbackInfo Rollback { get; init; } = new();
    public bool RestorePointAttempted { get; init; }
    public bool RestorePointCreated { get; init; }
    public bool RebootRecommended { get; init; }
}

public sealed class RepairHistoryEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Query { get; init; } = "";
    public string CategoryId { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public string SubIssue { get; init; } = "";
    public int ConfidenceScore { get; init; }
    public string FixId { get; init; } = "";
    public string FixTitle { get; init; } = "";
    public ExecutionOutcome Outcome { get; init; } = ExecutionOutcome.Completed;
    public bool Success { get; init; }
    public bool VerificationPassed { get; init; }
    public bool RollbackAvailable { get; init; }
    public bool RollbackUsed { get; init; }
    public bool RequiresAdmin { get; init; }
    public bool RebootRecommended { get; init; }
    public string RunbookId { get; init; } = "";
    public bool EvidenceBundleGenerated { get; init; }
    public string Notes { get; init; } = "";
    public string TriggerSource { get; init; } = "User";
    public string PreStateSummary { get; init; } = "";
    public string PostStateSummary { get; init; } = "";
    public string VerificationSummary { get; init; } = "";
    public string NextStep { get; init; } = "";
    public string RollbackSummary { get; init; } = "";
    public string FailedStepId { get; init; } = "";
    public string FailedStepTitle { get; init; } = "";
    public string ChangedSummary { get; init; } = "";
}

public sealed class RunbookExecutionSummary
{
    public string RunbookId { get; init; } = "";
    public string Title { get; init; } = "";
    public bool Success { get; init; }
    public int CompletedSteps { get; init; }
    public int TotalSteps { get; init; }
    public string Summary { get; init; } = "";
    public List<string> Timeline { get; init; } = [];
    public List<RepairExecutionResult> RepairResults { get; init; } = [];
}

public sealed class InterruptedOperationState
{
    public string OperationId { get; init; } = Guid.NewGuid().ToString("N");
    public string OperationType { get; init; } = "";
    public string OperationTargetId { get; init; } = "";
    public string DisplayTitle { get; init; } = "";
    public string CurrentStepId { get; init; } = "";
    public DateTime StartedAt { get; init; } = DateTime.Now;
    public bool RequiresAdmin { get; init; }
    public bool RollbackAvailable { get; init; }
    public string Summary { get; init; } = "";
    public ExecutionOutcome Outcome { get; init; } = ExecutionOutcome.Interrupted;
    public string FailedStepId { get; init; } = "";
    public string FailedStepTitle { get; init; } = "";
    public string LastOutput { get; init; } = "";
    public bool CanResume { get; init; }
}

public sealed class InterruptedRecoveryDecision
{
    public InterruptedOperationState? State { get; init; }
    public bool ShouldResume { get; init; }
    public bool KeepForInspection { get; init; }
    public bool ClearState { get; init; }
    public string Notice { get; init; } = "";
}

public sealed class GuidedRepairExecutionResult
{
    public string FixId { get; init; } = "";
    public string FixTitle { get; init; } = "";
    public ExecutionOutcome Outcome { get; init; } = ExecutionOutcome.InProgress;
    public int CurrentStepIndex { get; init; }
    public int TotalSteps { get; init; }
    public string CurrentStepId { get; init; } = "";
    public string CurrentStepTitle { get; init; } = "";
    public string FailedStepId { get; init; } = "";
    public string FailedStepTitle { get; init; } = "";
    public string Output { get; init; } = "";
    public string Summary { get; init; } = "";
    public string NextStep { get; init; } = "";
    public bool CanResume { get; init; }
    public RepairHistoryEntry? Receipt { get; init; }
}

public sealed class EvidenceExportOptions
{
    public EvidenceExportLevel Level { get; init; } = EvidenceExportLevel.Basic;
    public bool RedactIpAddress { get; init; } = true;
    public bool IncludeNotifications { get; init; } = true;
    public bool IncludeTechnicalHistory { get; init; } = true;
}

public sealed class ProactiveRecommendation
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string? ActionFixId { get; init; }
    public string? ActionRunbookId { get; init; }
    public ScanSeverity Severity { get; init; } = ScanSeverity.Warning;
}

public sealed class KnowledgeBaseEntry
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Url { get; init; } = "";
}

public sealed class BrandingConfiguration
{
    public string AppName { get; init; } = "FixFox";
    public string AppSubtitle { get; init; } = "Windows support and repair workspace";
    public string VendorName { get; init; } = "FixFox";
    public string SupportDisplayName { get; init; } = "FixFox Support";
    public string SupportEmail { get; init; } = "";
    public string SupportPortalLabel { get; init; } = "Open FixFox guides";
    public string SupportPortalUrl { get; init; } = "Docs\\Quick-Start.md";
    public string AccentHex { get; init; } = "#F97316";
    public string LogoPath { get; init; } = "";
    public string ProductTagline { get; init; } = "Explainable Windows support with guided fixes and clean handoff.";
    public string ManagedModeLabel { get; init; } = "Managed build";
}

public sealed class DeploymentConfiguration
{
    public bool ManagedMode { get; init; }
    public string OrganizationName { get; init; } = "";
    public string SupportDisplayName { get; init; } = "";
    public string SupportEmail { get; init; } = "";
    public string SupportPortalLabel { get; init; } = "";
    public string SupportPortalUrl { get; init; } = "";
    public string KnowledgeBaseConfigPath { get; init; } = "";
    public string UpdateFeedUrl { get; init; } = "";
    public AppEdition? EditionOverride { get; init; }
    public string DefaultBehaviorProfile { get; init; } = "";
    public string ForceBehaviorProfile { get; init; } = "";
    public string ForceNotificationMode { get; init; } = "";
    public string ForceLandingPage { get; init; } = "";
    public string ForceSupportBundleExportLevel { get; init; } = "";
    public bool? ForceMinimizeToTray { get; init; }
    public bool? ForceRunAtStartup { get; init; }
    public bool? ForceShowNotifications { get; init; }
    public bool? ForceSafeMaintenanceDefaults { get; init; }
    public bool AllowAdvancedMode { get; init; } = true;
    public bool DisableDeepRepairs { get; init; }
    public bool RestrictTechnicianExports { get; init; }
    public bool HideAdvancedToolbox { get; init; }
    public List<string> DisabledRepairCategories { get; init; } = [];
    public List<string> HiddenToolTitles { get; init; } = [];
    public string ManagedMessage { get; init; } = "";
}

public sealed class CapabilityAvailability
{
    public ProductCapability Capability { get; init; }
    public CapabilityState State { get; init; } = CapabilityState.Available;
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
}

public sealed class EditionCapabilitySnapshot
{
    public AppEdition Edition { get; init; } = AppEdition.Basic;
    public bool ManagedMode { get; init; }
    public CapabilityState EvidenceBundles { get; init; } = CapabilityState.Available;
    public CapabilityState Runbooks { get; init; } = CapabilityState.Available;
    public CapabilityState DeepRepairs { get; init; } = CapabilityState.Available;
    public CapabilityState AdvancedMode { get; init; } = CapabilityState.UpgradeRequired;
    public CapabilityState AdvancedDiagnostics { get; init; } = CapabilityState.UpgradeRequired;
    public CapabilityState TechnicianExports { get; init; } = CapabilityState.UpgradeRequired;
    public CapabilityState AdvancedAutomation { get; init; } = CapabilityState.UpgradeRequired;
    public CapabilityState AdvancedToolbox { get; init; } = CapabilityState.UpgradeRequired;
    public CapabilityState AdvancedRecovery { get; init; } = CapabilityState.UpgradeRequired;
    public CapabilityState CustomSupportRouting { get; init; } = CapabilityState.UpgradeRequired;
    public CapabilityState ManagedPolicies { get; init; } = CapabilityState.UpgradeRequired;
    public CapabilityState WhiteLabelBranding { get; init; } = CapabilityState.UpgradeRequired;
}

public sealed class AppUpdateInfo
{
    public bool UpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = "";
    public string LatestVersion { get; init; } = "";
    public string SourceName { get; init; } = "";
    public string ChannelName { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
    public string ReleaseNotesPath { get; init; } = "";
    public string Summary { get; init; } = "";
}

public sealed class EvidenceBundleManifest
{
    public string SummaryPath { get; init; } = "";
    public string TechnicalPath { get; init; } = "";
    public string BundleFolder { get; init; } = "";
    public string Headline { get; init; } = "";
}

public sealed class DashboardAlert
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public ScanSeverity Severity { get; init; } = ScanSeverity.Warning;
    public string ActionLabel { get; init; } = "";
    public DashboardActionKind ActionKind { get; init; }
    public string ActionTargetId { get; init; } = "";
    public Page? ActionPage { get; init; }
}

public sealed class ToolboxEntry : INotifyPropertyChanged
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string SupportNote { get; init; } = "";
    public string LaunchTarget { get; init; } = "";
    public string LaunchArguments { get; init; } = "";
    public AppEdition MinimumEdition { get; init; } = AppEdition.Basic;
    public bool RequiresAdvancedMode { get; init; }
    public ProductCapability RequiredCapability { get; init; } = ProductCapability.None;
    private ToolLaunchState _launchState;
    private string _launchSummary = "";
    private DateTime? _lastLaunchedAt;

    public ToolLaunchState LaunchState
    {
        get => _launchState;
        set
        {
            if (_launchState == value) return;
            _launchState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsSucceeded));
            OnPropertyChanged(nameof(IsFailed));
            OnPropertyChanged(nameof(HasStatusIndicator));
            OnPropertyChanged(nameof(StatusSummary));
        }
    }

    public string LaunchSummary
    {
        get => _launchSummary;
        set
        {
            if (_launchSummary == value) return;
            _launchSummary = value;
            OnPropertyChanged();
        }
    }

    public DateTime? LastLaunchedAt
    {
        get => _lastLaunchedAt;
        set
        {
            if (_lastLaunchedAt == value) return;
            _lastLaunchedAt = value;
            OnPropertyChanged();
        }
    }

    public bool IsIdle => LaunchState == ToolLaunchState.Idle;
    public bool IsRunning => LaunchState == ToolLaunchState.Running;
    public bool IsSucceeded => LaunchState == ToolLaunchState.Success;
    public bool IsFailed => LaunchState == ToolLaunchState.Failed;
    public bool HasStatusIndicator => LaunchState != ToolLaunchState.Idle;
    public string StatusSummary => LaunchState switch
    {
        ToolLaunchState.Running => "Opening",
        ToolLaunchState.Success => "Opened",
        ToolLaunchState.Failed => "Failed",
        _ => ""
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class ToolboxGroup
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public List<ToolboxEntry> Entries { get; init; } = [];
}

public sealed class SupportAction
{
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public SupportActionKind Kind { get; init; } = SupportActionKind.None;
    public string TargetId { get; init; } = "";
}

public sealed class SupportCenterDefinition
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string StatusText { get; init; } = "";
    public List<string> Highlights { get; init; } = [];
    public SupportAction PrimaryAction { get; init; } = new();
    public SupportAction SecondaryAction { get; init; } = new();
}

public sealed class MaintenanceProfileDefinition
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string SafetyNotes { get; init; } = "";
    public string VerificationNotes { get; init; } = "";
    public List<string> IncludedTasks { get; init; } = [];
    public bool SupportsScheduling { get; init; }
    public bool PreferIdleWhenScheduled { get; init; }
    public bool AvoidWhenOnBattery { get; init; }
    public SupportAction LaunchAction { get; init; } = new();
}

public sealed class AutomationRuleSettings
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string SafetySummary { get; set; } = "";
    public AutomationRuleKind Kind { get; set; } = AutomationRuleKind.QuickHealthCheck;
    public bool Enabled { get; set; } = true;
    public bool IsWatcher { get; set; }
    public bool SupportsScheduling { get; set; }
    public bool SupportsScanOnly { get; set; }
    public AutomationScheduleKind ScheduleKind { get; set; } = AutomationScheduleKind.Manual;
    public string ScheduleDay { get; set; } = DayOfWeek.Sunday.ToString();
    public string ScheduleTime { get; set; } = "09:00";
    public bool RunOnlyWhenIdle { get; set; }
    public int MinimumIdleMinutes { get; set; } = 10;
    public bool SkipOnBattery { get; set; }
    public int MinimumBatteryPercent { get; set; } = 35;
    public bool SkipOnMeteredConnection { get; set; }
    public bool SkipDuringQuietHours { get; set; } = true;
    public bool NotifyOnlyIfIssuesFound { get; set; } = true;
    public bool NotifyOnSkippedOrBlocked { get; set; }
    public bool SkipIfActiveRepairSession { get; set; } = true;
    public int StartupDelayMinutes { get; set; } = 3;
    public bool ScanOnly { get; set; }
    public DateTime? PausedUntilUtc { get; set; }
    public List<string> IncludedTasks { get; set; } = [];
    public SupportAction PrimaryAction { get; set; } = new();
    public SupportAction SecondaryAction { get; set; } = new();

    // Runtime summary fields populated by the automation workspace.
    public string StatusText { get; set; } = "";
    public string LastRunText { get; set; } = "Not yet run";
    public string NextRunText { get; set; } = "Manual only";
    public string ConditionSummary { get; set; } = "";
    public bool NeedsAttention { get; set; }
    public ScanSeverity Severity { get; set; } = ScanSeverity.Good;
}

public sealed class AutomationConditionEvaluation
{
    public bool CanRun { get; init; } = true;
    public bool WasSkipped { get; init; }
    public bool WasBlocked { get; init; }
    public List<string> Reasons { get; init; } = [];
    public string Summary => Reasons.Count == 0 ? "Ready to run." : string.Join(" ", Reasons);
}

public sealed class AutomationRunReceipt
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string RuleId { get; init; } = "";
    public string RuleTitle { get; init; } = "";
    public AutomationRuleKind RuleKind { get; init; } = AutomationRuleKind.QuickHealthCheck;
    public string TriggerSource { get; init; } = "Manual";
    public DateTime StartedAt { get; init; } = DateTime.Now;
    public DateTime FinishedAt { get; init; } = DateTime.Now;
    public AutomationRunOutcome Outcome { get; init; } = AutomationRunOutcome.Completed;
    public string Summary { get; init; } = "";
    public string PrecheckSummary { get; init; } = "";
    public string ChangedSummary { get; init; } = "";
    public string VerificationSummary { get; init; } = "";
    public string NextStep { get; init; } = "";
    public string ConditionSummary { get; init; } = "";
    public bool UserActionRequired { get; init; }
    public List<string> TasksAttempted { get; init; } = [];
    public string RelatedRunbookId { get; init; } = "";
    public string RelatedFixId { get; init; } = "";
    public string RelatedSupportCenterId { get; init; } = "";
}

public sealed record StartupAppEntry(
    string Name,
    string Source,
    string Command,
    string LaunchTarget,
    bool RecommendedDisableCandidate,
    string RecommendationReason)
{
    public string LaunchTargetLabel => string.IsNullOrWhiteSpace(LaunchTarget) ? "" : Path.GetFileName(LaunchTarget);
    public bool HasLaunchTarget => !string.IsNullOrWhiteSpace(LaunchTarget);
}

public sealed record StorageInsight(
    string DisplayName,
    string FullPath,
    string LocationLabel,
    long SizeBytes,
    string SafeToRemoveSummary,
    string Caution)
{
    public string SizeLabel => SizeBytes >= 1_073_741_824
        ? $"{SizeBytes / 1_073_741_824d:N1} GB"
        : $"{SizeBytes / 1_048_576d:N0} MB";

    public string FolderPath => string.IsNullOrWhiteSpace(FullPath) ? "" : Path.GetDirectoryName(FullPath) ?? "";
    public bool IsSensitiveLocation =>
        LocationLabel.Contains("Desktop", StringComparison.OrdinalIgnoreCase)
        || LocationLabel.Contains("Documents", StringComparison.OrdinalIgnoreCase);
}

public sealed class CommandPaletteItem
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Section { get; init; } = "";
    public string Hint { get; init; } = "";
    public string Glyph { get; init; } = "\uE721";
    public string SearchText { get; init; } = "";
    public CommandPaletteItemKind Kind { get; init; } = CommandPaletteItemKind.Page;
    public string TargetId { get; init; } = "";
    public Page? TargetPage { get; init; }
}

public sealed class HealthCategoryScore
{
    public string CategoryId { get; init; } = "";
    public string Title { get; init; } = "";
    public int Score { get; init; }
    public string Summary { get; init; } = "";
}

public sealed class HealthCheckReport
{
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    public int OverallScore { get; init; }
    public string Summary { get; init; } = "";
    public List<HealthCategoryScore> Categories { get; init; } = [];
    public List<ProactiveRecommendation> Recommendations { get; init; } = [];
    public List<ScanResult> ScanFindings { get; init; } = [];
}

public sealed class ErrorReportRecord
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Category { get; init; } = "";
    public string FixId { get; init; } = "";
    public string RunbookId { get; init; } = "";
    public string Message { get; init; } = "";
    public string Detail { get; init; } = "";
}
