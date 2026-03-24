using System.IO;

namespace HelpDesk.Shared;

/// <summary>Computed path helpers and app identity constants.</summary>
public static class Constants
{
    // ── App identity ─────────────────────────────────────────────────────────
    public const string AppName    = "FixFox";
    public const string AppVersion = "1.0.0";

    // ── Computed paths ────────────────────────────────────────────────────────
    public static string AppDataDir    => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FixFox");

    public static string TempDir       => Path.Combine(Path.GetTempPath(), "FixFox");

    public static string VerifyLogFile => Path.Combine(AppDataDir, "startup-verify.log");

    public static string AppLogFile    => Path.Combine(AppDataDir, "app.log");

    public static string CrashDir      => Path.Combine(AppDataDir, "crashes");

    public static string SettingsFile  => Path.Combine(AppDataDir, "settings.json");
}
