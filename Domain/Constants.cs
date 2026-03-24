namespace HelpDesk.Domain;

/// <summary>All magic numbers, limits, durations, and paths in one place.</summary>
public static class Constants
{
    // ── App identity ─────────────────────────────────────────────────────────
    public const string AppName        = "FixFox";
    public const string AppVersion     = "1.0.0";
    public const string DataDirName    = "FixFox";
    public const string TempDirName    = "FixFox";

    // ── File names ───────────────────────────────────────────────────────────
    public const string SettingsFile      = "settings.json";
    public const string SettingsBakFile   = "settings.bak.json";
    public const string HistoryFile       = "history.json";
    public const string NotifFile         = "notifications.json";
    public const string VerifyLogFile     = "startup-verify.log";
    public const string RegBackupDir      = "backups";

    // ── Ring buffer caps ─────────────────────────────────────────────────────
    public const int MaxHistoryEntries    = 500;
    public const int MaxNotifications     = 200;
    public const int MaxRecentSearches    = 5;
    public const int MaxRecentlyRun       = 10;

    // ── Script execution ─────────────────────────────────────────────────────
    public const int  ScriptTimeoutMs     = 90_000;     // 90 s
    public const int  ScriptOutputCap     = 4_000;      // chars

    // ── UI timers ────────────────────────────────────────────────────────────
    public const int  SearchDebounceMs    = 200;
    public const int  ClockTickMs         = 1_000;
    public const int  LiveSysInfoTickMs   = 5_000;
    public const int  ScrollbarFadeInMs   = 200;
    public const int  ScrollbarHoldMs     = 1_500;

    // ── Quick Scan thresholds (defaults — overridden by AppSettings) ─────────
    public const int  DefaultDiskWarnPct  = 85;
    public const int  DefaultRamWarnPct   = 85;
    public const int  DefaultCpuTempWarnC = 85;
    public const int  StartupItemWarnCount = 10;
    public const long DesktopSizeWarnMb   = 1_024;
    public const int  DefenderSigAgeDays  = 7;
    public const int  UptimeWarnDays      = 14;

    // ── Window ───────────────────────────────────────────────────────────────
    public const double MinWindowWidth    = 960;
    public const double MinWindowHeight   = 620;
    public const double DefaultWidth      = 1280;
    public const double DefaultHeight     = 760;

    // ── Sidebar ──────────────────────────────────────────────────────────────
    public const double SidebarExpandedWidth  = 222;
    public const double SidebarCollapsedWidth = 48;

    // ── Logo sizes ───────────────────────────────────────────────────────────
    public const double LogoSizeTitleBar  = 32;
    public const double LogoSizeSidebar   = 48;
    public const double LogoSizeAbout     = 24;

    // ── Duplicate file finder ─────────────────────────────────────────────────
    public const long   DupFileMinSizeBytes = 1_024;    // skip tiny files
    public const int    DupFileScanMaxDepth = 8;

    // ── Scheduler ────────────────────────────────────────────────────────────
    public const string ScheduledTaskName = "FixFox_WeeklyTuneUp";
}
