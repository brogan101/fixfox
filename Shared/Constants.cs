using System.IO;

namespace HelpDesk.Shared;

/// <summary>Computed path helpers and app identity constants.</summary>
public static class Constants
{
    // â”€â”€ App identity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public const string AppName    = "FixFox";
    public const string AppVersion = "1.0.0";

    // â”€â”€ Computed paths â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public static string AppDataDir    => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FixFox");

    public static string TempDir       => Path.Combine(Path.GetTempPath(), "FixFox");
    public static string DocsDir       => Path.Combine(AppContext.BaseDirectory, "Docs");
    public static string ConfigDir     => Path.Combine(AppContext.BaseDirectory, "Configuration");
    public static string QuickStartDoc => Path.Combine(DocsDir, "Quick-Start.md");
    public static string PrivacyDoc    => Path.Combine(DocsDir, "Privacy-and-Data.md");
    public static string RecoveryDoc   => Path.Combine(DocsDir, "Recovery-and-Resume.md");
    public static string SupportBundleDoc => Path.Combine(DocsDir, "Support-Packages.md");
    public static string TroubleshootingDoc => Path.Combine(DocsDir, "Troubleshooting-and-FAQ.md");
    public static string ReleaseNotesDoc => Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");

    public static string VerifyLogFile => Path.Combine(AppDataDir, "startup-verify.log");

    public static string AppLogFile    => Path.Combine(AppDataDir, "app.log");

    public static string CrashDir      => Path.Combine(AppDataDir, "crashes");

    public static string SettingsFile  => Path.Combine(AppDataDir, "settings.json");
}



