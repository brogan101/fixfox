namespace HelpDesk.Domain.Enums;

public enum FixType       { Silent, Guided }
public enum FixStatus     { Idle, Running, Success, Failed }
public enum ToolLaunchState { Idle, Running, Success, Failed }
public enum ScanSeverity  { Good, Warning, Critical }
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
public enum SupportActionKind { None, Fix, Runbook, Toolbox, Uri }
public enum RunbookStepKind { Diagnostic, Repair, Verification, Message, KnowledgeBase }
public enum RiskLevel { Low, Moderate, High }
public enum RepairTier { SafeUser, AdminDeepFix, GuidedEscalation }
public enum DashboardActionKind { None, Fix, Runbook, Page }
public enum CommandPaletteItemKind { Page, Fix, Runbook, MaintenanceProfile, Toolbox, SupportCenter, Action }
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
public enum AutomationScheduleKind { Disabled, Manual, Daily, Weekly, Startup }
public enum AutomationRunOutcome { Completed, Partial, Failed, Skipped, Blocked }
public enum Page
{
    Dashboard, Fixes, Bundles, SystemInfo,
    SymptomChecker, Toolbox, History, Handoff, Settings
}
