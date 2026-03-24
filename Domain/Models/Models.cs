using HelpDesk.Domain.Enums;

namespace HelpDesk.Domain.Models;

// ── Fix catalog models ─────────────────────────────────────────────────────

public sealed class FixStep
{
    public string  Title       { get; init; } = "";
    public string  Instruction { get; init; } = "";
    public string? Script      { get; init; }
}

public sealed class FixItem
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
    public FixStatus Status     { get; set; } = FixStatus.Idle;
    public string?   LastOutput { get; set; }
    public bool      IsFavorite { get; set; }
    public bool      IsPinned   { get; set; }

    // Computed helpers for bindings
    public bool HasScript => !string.IsNullOrWhiteSpace(Script);
    public bool HasSteps  => Steps.Count > 0;
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
    public string  UpdateFeedUrl             { get; set; } = "";
    public string  BrandingConfigPath        { get; set; } = "";
    public string  KnowledgeBaseConfigPath   { get; set; } = "";
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
}

public sealed class TriageContext
{
    public string Query { get; init; } = "";
    public bool HasBattery { get; init; }
    public bool PendingRebootDetected { get; init; }
    public bool HasRecentFailures { get; init; }
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
}

public sealed class RepairDefinition
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string ShortDescription { get; init; } = "";
    public string LongDescription { get; init; } = "";
    public string MasterCategoryId { get; init; } = "";
    public List<string> SupportedSubIssues { get; init; } = [];
    public List<string> SearchPhrases { get; init; } = [];
    public List<string> Synonyms { get; init; } = [];
    public List<string> ConfidenceBoostSignals { get; init; } = [];
    public List<string> Diagnostics { get; init; } = [];
    public List<string> QuickFixActions { get; init; } = [];
    public List<string> DeepFixActions { get; init; } = [];
    public List<string> VerificationChecks { get; init; } = [];
    public List<string> KnowledgeBaseKeys { get; init; } = [];
    public AppEdition MinimumEdition { get; init; } = AppEdition.Basic;
    public RepairTier Tier { get; init; } = RepairTier.SafeUser;
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Low;
    public bool RequiresAdmin { get; init; }
    public bool MayRequireReboot { get; init; }
    public bool SupportsRestorePoint { get; init; }
    public bool SupportsRollback { get; init; }
    public string WhatWillHappen { get; init; } = "";
    public string UserFacingWarnings { get; init; } = "";
    public string AdvancedNotes { get; init; } = "";
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
    public bool Success { get; init; }
    public string Output { get; init; } = "";
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
    public bool Success { get; init; }
    public bool VerificationPassed { get; init; }
    public bool RollbackAvailable { get; init; }
    public bool RollbackUsed { get; init; }
    public bool RequiresAdmin { get; init; }
    public bool RebootRecommended { get; init; }
    public string RunbookId { get; init; } = "";
    public bool EvidenceBundleGenerated { get; init; }
    public string Notes { get; init; } = "";
}

public sealed class RunbookExecutionSummary
{
    public string RunbookId { get; init; } = "";
    public string Title { get; init; } = "";
    public bool Success { get; init; }
    public int CompletedSteps { get; init; }
    public int TotalSteps { get; init; }
    public string Summary { get; init; } = "";
    public List<RepairExecutionResult> RepairResults { get; init; } = [];
}

public sealed class InterruptedOperationState
{
    public string OperationId { get; init; } = Guid.NewGuid().ToString("N");
    public string OperationType { get; init; } = "";
    public string DisplayTitle { get; init; } = "";
    public string CurrentStepId { get; init; } = "";
    public DateTime StartedAt { get; init; } = DateTime.Now;
    public bool RequiresAdmin { get; init; }
    public bool RollbackAvailable { get; init; }
    public string Summary { get; init; } = "";
}

public sealed class ProactiveRecommendation
{
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
    public string AppSubtitle { get; init; } = "Desktop support toolkit";
    public string SupportEmail { get; init; } = "support@example.com";
    public string SupportPortalLabel { get; init; } = "Help Desk Portal";
    public string SupportPortalUrl { get; init; } = "https://support.example.com";
    public string AccentHex { get; init; } = "#F97316";
}

public sealed class EditionCapabilitySnapshot
{
    public AppEdition Edition { get; init; } = AppEdition.Basic;
    public CapabilityState EvidenceBundles { get; init; } = CapabilityState.Available;
    public CapabilityState Runbooks { get; init; } = CapabilityState.Available;
    public CapabilityState DeepRepairs { get; init; } = CapabilityState.Available;
    public CapabilityState WhiteLabelBranding { get; init; } = CapabilityState.UpgradeRequired;
}

public sealed class AppUpdateInfo
{
    public bool UpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = "";
    public string LatestVersion { get; init; } = "";
    public string SourceName { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
    public string Summary { get; init; } = "";
}

public sealed class EvidenceBundleManifest
{
    public string SummaryPath { get; init; } = "";
    public string TechnicalPath { get; init; } = "";
    public string BundleFolder { get; init; } = "";
    public string Headline { get; init; } = "";
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
