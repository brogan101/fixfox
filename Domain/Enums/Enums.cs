namespace HelpDesk.Domain.Enums;

public enum FixType       { Silent, Guided }
public enum FixStatus     { Idle, Running, Success, Failed }
public enum ScanSeverity  { Good, Warning, Critical }
public enum NotifLevel    { Info, Warning, Critical }
public enum BundleStatus  { Idle, Running, Complete, Failed }
public enum AppTheme      { Dark, Light }
public enum AccentColor   { Orange, Blue, Green, Purple }
public enum AppEdition    { Basic, Pro, ManagedServiceProvider }
public enum CapabilityState { Available, UpgradeRequired, ManagedOff }
public enum VerificationStatus { NotRun, Passed, Failed, Inconclusive }
public enum RunbookStepKind { Diagnostic, Repair, Verification, Message, KnowledgeBase }
public enum RiskLevel { Low, Moderate, High }
public enum RepairTier { SafeUser, AdminDeepFix, GuidedEscalation }
public enum Page
{
    Dashboard, Fixes, Bundles, SystemInfo,
    SymptomChecker, History, Handoff, Settings
}
