namespace HelpDesk.Domain.Enums;

public enum FixType       { Silent, Guided }
public enum FixRiskLevel  { Safe, NeedsAdmin, MayRestart, Advanced }
public enum FixStatus     { Idle, Running, Success, Failed }
public enum ToolLaunchState { Idle, Running, Success, Failed }
public enum ScanSeverity  { Good, Warning, Critical }
public enum AlertSeverity { Info, Warning, Critical }
public enum NotifLevel    { Info, Warning, Critical }
public enum BundleStatus  { Idle, Running, Complete, Failed }
public enum AppTheme      { Dark, Light }
public enum AccentColor   { Orange, Blue, Green, Purple }
public enum AppEdition    { Basic, Pro, ManagedServiceProvider }
public enum CapabilityState { Available, UpgradeRequired, ManagedOff }
public enum ProductCapability
{
    None,
    EvidenceBundles,
    Runbooks,
    DeepRepairs,
    AdvancedMode,
    AdvancedDiagnostics,
    TechnicianExports,
    AdvancedAutomation,
    AdvancedToolbox,
    AdvancedRecovery,
    CustomSupportRouting,
    WhiteLabelBranding,
    ManagedPolicies
}
public enum VerificationStatus { NotRun, Passed, Failed, Inconclusive }
public enum VerificationStrategyKind
{
    HeuristicFallback,
    NetworkConnectivity,
    PrintingQueue,
    AudioDevices,
    CameraDevices,
    DisplayDevices,
    StoragePressure,
    WindowsFirewall,
    WindowsDefender,
    BrowserConnectivity,
    AppLaunch,
    WindowsUpdate,
    ScriptOutput
}
public enum ExecutionOutcome
{
    InProgress,
    Completed,
    Failed,
    Blocked,
    Cancelled,
    Interrupted,
    Resumable
}
public enum EvidenceExportLevel { Basic, Technician }
public enum SupportBundlePreset { Quick, Standard, Technician }
public enum SupportActionKind { None, Fix, Runbook, Toolbox, Uri, Page, GlobalSearch }
public enum RunbookStepKind { Diagnostic, Repair, Verification, Message, KnowledgeBase }
public enum RiskLevel { Low, Moderate, High }
public enum RepairTier { SafeUser, AdminDeepFix, GuidedEscalation }
public enum DashboardActionKind { None, Fix, Runbook, Page }
public enum CommandPaletteItemKind { Page, Fix, Runbook, MaintenanceProfile, Toolbox, SupportCenter, Action, Receipt, Setting, AutomationRule }
public enum AutomationRuleKind
{
    QuickHealthCheck,
    SafeMaintenance,
    StartupQuickCheck,
    BrowserCleanup,
    WorkFromHomeReadiness,
    MeetingReadiness,
    LowDiskWatcher,
    PendingRebootWatcher,
    DefenderFirewallWatcher,
    RepeatedFailureWatcher,
    SpoolerWatcher,
    UpdateHealthWatcher,
    InterruptedRepairWatcher,
    NetworkFailureWatcher
}
public enum AutomationScheduleKind
{
    Disabled,
    Manual,
    Daily,
    Weekly,
    Startup,
    EveryXDays,
    WeekdaysOnly,
    StartupDelay
}
public enum AutomationRunOutcome { Completed, Partial, Failed, Skipped, Blocked }
public enum PolicyState { None, Managed, Locked, Inherited }
public enum StartupImpactLevel { Unknown, Low, Medium, High }
public enum StartupItemClassification { MicrosoftComponent, KnownThirdParty, Unrecognized }
public enum BrowserPermissionRisk { Low, Medium, High }
public enum WorkResourceDependencyHintType { RequiresVpn, RequiresSpecificDns, RequiresCredentialRefresh, RequiresCertificate }
public enum HealthAlertNotificationFrequency { All, WarningsAndCritical, CriticalOnly }
public enum ReceiptKind { Standard, WeeklySummary }
public enum SimplifiedConfirmationDecision { Cancel, Run, GetHelpInstead }
public enum Page
{
    Dashboard, Fixes, FixMyPc, Bundles, SystemInfo,
    SymptomChecker, Toolbox, History, Handoff, Settings
}
