using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;

namespace HelpDesk.Infrastructure.Services;

internal static class ProductizationPolicies
{
    internal const int CurrentSettingsSchemaVersion = 1;

    private static readonly HashSet<string> ValidThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dark",
        "Light"
    };

    private static readonly HashSet<string> ValidProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Quiet",
        "Standard",
        "Power User",
        "Work Laptop",
        "Home PC"
    };

    private static readonly HashSet<string> ValidNotificationModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Quiet",
        "Important Only",
        "Standard"
    };

    private static readonly HashSet<string> ValidExportLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Basic",
        "Technician"
    };

    private static readonly HashSet<string> ValidLandingPages = Enum
        .GetNames<Page>()
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static AppSettings CreateDefaultSettings()
    {
        var defaults = new AppSettings
        {
            RunQuickScanOnLaunch = false,
            CheckForUpdatesOnLaunch = false,
            ShowTrayBalloons = false,
            RunFirstHealthCheckAfterSetup = true
        };

        return Normalize(defaults, out _, out _, null);
    }

    public static AppSettings Normalize(
        AppSettings settings,
        out bool migrationApplied,
        out bool validationApplied,
        List<string>? notes)
    {
        settings ??= new AppSettings();
        migrationApplied = false;
        validationApplied = false;

        if (settings.SchemaVersion < CurrentSettingsSchemaVersion)
        {
            migrationApplied = true;
            notes?.Add($"Settings were migrated to schema v{CurrentSettingsSchemaVersion}.");
            settings.SchemaVersion = CurrentSettingsSchemaVersion;
        }

        validationApplied |= NormalizeTheme(settings);
        validationApplied |= NormalizeProfile(settings);
        validationApplied |= NormalizeNotificationMode(settings);
        validationApplied |= NormalizeExportLevel(settings);
        validationApplied |= NormalizeLandingPage(settings);
        validationApplied |= NormalizeSchedule(settings);
        validationApplied |= NormalizeAutomation(settings);
        validationApplied |= NormalizeHealthMonitoring(settings);
        validationApplied |= ClampThresholds(settings);
        validationApplied |= ClampWindowBounds(settings);

        if (validationApplied)
            notes?.Add("FixFox repaired unsupported or out-of-range settings values.");

        return settings;
    }

    public static void ApplyBehaviorProfile(AppSettings settings, string profile)
    {
        profile = string.IsNullOrWhiteSpace(profile) ? "Standard" : profile;
        settings.BehaviorProfile = profile;

        switch (profile)
        {
            case "Quiet":
                settings.ShowNotifications = false;
                settings.NotificationMode = "Quiet";
                settings.ShowTrayBalloons = false;
                settings.RunQuickScanOnLaunch = false;
                settings.MinimizeToTray = false;
                settings.PreferSafeMaintenanceDefaults = true;
                settings.DefaultLandingPage = Page.Dashboard.ToString();
                settings.SupportBundleExportLevel = "Basic";
                settings.RecoverInterruptedOperations = false;
                settings.ConfirmBeforeClosingActiveWork = true;
                settings.AdvancedMode = false;
                settings.AutomationPausedUntilUtc = null;
                break;

            case "Power User":
                settings.ShowNotifications = true;
                settings.NotificationMode = "Standard";
                settings.ShowTrayBalloons = true;
                settings.RunQuickScanOnLaunch = true;
                settings.MinimizeToTray = true;
                settings.PreferSafeMaintenanceDefaults = false;
                settings.DefaultLandingPage = Page.Fixes.ToString();
                settings.SupportBundleExportLevel = "Technician";
                settings.RecoverInterruptedOperations = true;
                settings.ConfirmBeforeClosingActiveWork = true;
                settings.AdvancedMode = true;
                settings.CheckForUpdatesOnLaunch = true;
                settings.AutomationPausedUntilUtc = null;
                break;

            case "Work Laptop":
                settings.ShowNotifications = true;
                settings.NotificationMode = "Important Only";
                settings.ShowTrayBalloons = true;
                settings.RunQuickScanOnLaunch = true;
                settings.MinimizeToTray = true;
                settings.PreferSafeMaintenanceDefaults = true;
                settings.DefaultLandingPage = Page.SystemInfo.ToString();
                settings.SupportBundleExportLevel = "Basic";
                settings.RecoverInterruptedOperations = true;
                settings.ConfirmBeforeClosingActiveWork = true;
                settings.AdvancedMode = false;
                settings.CheckForUpdatesOnLaunch = true;
                settings.AutomationPausedUntilUtc = null;
                break;

            case "Home PC":
                settings.ShowNotifications = true;
                settings.NotificationMode = "Standard";
                settings.ShowTrayBalloons = false;
                settings.RunQuickScanOnLaunch = true;
                settings.MinimizeToTray = false;
                settings.PreferSafeMaintenanceDefaults = true;
                settings.DefaultLandingPage = Page.Dashboard.ToString();
                settings.SupportBundleExportLevel = "Basic";
                settings.RecoverInterruptedOperations = true;
                settings.ConfirmBeforeClosingActiveWork = true;
                settings.AdvancedMode = false;
                settings.AutomationPausedUntilUtc = null;
                break;

            default:
                settings.ShowNotifications = true;
                settings.NotificationMode = "Standard";
                settings.ShowTrayBalloons = true;
                settings.RunQuickScanOnLaunch = true;
                settings.MinimizeToTray = false;
                settings.PreferSafeMaintenanceDefaults = true;
                settings.DefaultLandingPage = Page.Dashboard.ToString();
                settings.SupportBundleExportLevel = "Basic";
                settings.RecoverInterruptedOperations = true;
                settings.ConfirmBeforeClosingActiveWork = true;
                settings.AdvancedMode = false;
                settings.CheckForUpdatesOnLaunch = true;
                settings.AutomationPausedUntilUtc = null;
                break;
        }

        settings.AutomationRules = [];
    }

    public static InterruptedRecoveryDecision EvaluateInterruptedState(
        InterruptedOperationState? state,
        AppSettings settings,
        DateTime now)
    {
        if (state is null)
            return new InterruptedRecoveryDecision { ClearState = true };

        if (state.Outcome is ExecutionOutcome.Completed or ExecutionOutcome.Cancelled)
        {
            return new InterruptedRecoveryDecision
            {
                ClearState = true,
                Notice = "FixFox cleared an old completed recovery marker."
            };
        }

        var age = now - state.StartedAt;
        if (age > TimeSpan.FromDays(3))
        {
            return new InterruptedRecoveryDecision
            {
                State = state,
                KeepForInspection = true,
                Notice = $"A repair from {state.StartedAt:g} was kept for review instead of auto-resuming because it is stale."
            };
        }

        if (!settings.RecoverInterruptedOperations || !state.CanResume)
        {
            return new InterruptedRecoveryDecision
            {
                State = state,
                KeepForInspection = true,
                Notice = "An interrupted repair was preserved for review. Automatic resume is turned off for this profile."
            };
        }

        return new InterruptedRecoveryDecision
        {
            State = state,
            ShouldResume = true,
            Notice = $"FixFox restored the last interrupted {state.DisplayTitle.ToLowerInvariant()} flow so you can continue safely."
        };
    }

    public static PolicyState GetPolicyState(
        DeploymentConfiguration deployment,
        AppSettings settings,
        string settingKey)
    {
        settingKey = settingKey?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(settingKey))
            return PolicyState.None;

        var state = settingKey switch
        {
            "BehaviorProfile" => !string.IsNullOrWhiteSpace(deployment.ForceBehaviorProfile)
                ? PolicyState.Locked
                : PolicyState.None,
            "NotificationMode" => !string.IsNullOrWhiteSpace(deployment.ForceNotificationMode)
                ? PolicyState.Locked
                : IsInheritedFromProfile(settings, deployment, settingKey),
            "SupportBundleExportLevel" => deployment.RestrictTechnicianExports || !string.IsNullOrWhiteSpace(deployment.ForceSupportBundleExportLevel)
                ? PolicyState.Locked
                : IsInheritedFromProfile(settings, deployment, settingKey),
            "LandingPage" => !string.IsNullOrWhiteSpace(deployment.ForceLandingPage)
                ? PolicyState.Locked
                : IsInheritedFromProfile(settings, deployment, settingKey),
            "AdvancedMode" => !deployment.AllowAdvancedMode
                ? PolicyState.Locked
                : IsInheritedFromProfile(settings, deployment, settingKey),
            "MinimizeToTray" => deployment.ForceMinimizeToTray.HasValue
                ? PolicyState.Locked
                : IsInheritedFromProfile(settings, deployment, settingKey),
            "RunAtStartup" => deployment.ForceRunAtStartup.HasValue
                ? PolicyState.Locked
                : PolicyState.None,
            "ShowNotifications" => deployment.ForceShowNotifications.HasValue
                ? PolicyState.Locked
                : IsInheritedFromProfile(settings, deployment, settingKey),
            "PreferSafeMaintenanceDefaults" => deployment.ForceSafeMaintenanceDefaults.HasValue
                ? PolicyState.Locked
                : IsInheritedFromProfile(settings, deployment, settingKey),
            "RunQuickScanOnLaunch" => IsInheritedFromProfile(settings, deployment, settingKey),
            "ShowTrayBalloons" => IsInheritedFromProfile(settings, deployment, settingKey),
            "CheckForUpdatesOnLaunch" => IsInheritedFromProfile(settings, deployment, settingKey),
            _ => PolicyState.None
        };

        if (state == PolicyState.None && IsManagedEditableSetting(deployment, settingKey))
            return PolicyState.Managed;

        return state;
    }

    private static bool IsManagedEditableSetting(DeploymentConfiguration deployment, string settingKey)
    {
        if (!deployment.ManagedMode)
            return false;

        return settingKey switch
        {
            "BehaviorProfile" or
            "NotificationMode" or
            "SupportBundleExportLevel" or
            "LandingPage" or
            "AdvancedMode" or
            "RunAtStartup" or
            "MinimizeToTray" or
            "ShowNotifications" or
            "PreferSafeMaintenanceDefaults" or
            "RunQuickScanOnLaunch" or
            "ShowTrayBalloons" or
            "CheckForUpdatesOnLaunch" => true,
            _ => false
        };
    }

    private static PolicyState IsInheritedFromProfile(AppSettings settings, DeploymentConfiguration deployment, string settingKey)
    {
        var profile = string.IsNullOrWhiteSpace(settings.BehaviorProfile)
            ? string.IsNullOrWhiteSpace(deployment.DefaultBehaviorProfile) ? "Standard" : deployment.DefaultBehaviorProfile
            : settings.BehaviorProfile;

        var baseline = CreateDefaultSettings();
        ApplyBehaviorProfile(baseline, profile);

        return settingKey switch
        {
            "NotificationMode" when string.Equals(settings.NotificationMode, baseline.NotificationMode, StringComparison.OrdinalIgnoreCase) => PolicyState.Inherited,
            "SupportBundleExportLevel" when string.Equals(settings.SupportBundleExportLevel, baseline.SupportBundleExportLevel, StringComparison.OrdinalIgnoreCase) => PolicyState.Inherited,
            "LandingPage" when string.Equals(settings.DefaultLandingPage, baseline.DefaultLandingPage, StringComparison.OrdinalIgnoreCase) => PolicyState.Inherited,
            "AdvancedMode" when settings.AdvancedMode == baseline.AdvancedMode => PolicyState.Inherited,
            "MinimizeToTray" when settings.MinimizeToTray == baseline.MinimizeToTray => PolicyState.Inherited,
            "ShowNotifications" when settings.ShowNotifications == baseline.ShowNotifications => PolicyState.Inherited,
            "PreferSafeMaintenanceDefaults" when settings.PreferSafeMaintenanceDefaults == baseline.PreferSafeMaintenanceDefaults => PolicyState.Inherited,
            "RunQuickScanOnLaunch" when settings.RunQuickScanOnLaunch == baseline.RunQuickScanOnLaunch => PolicyState.Inherited,
            "ShowTrayBalloons" when settings.ShowTrayBalloons == baseline.ShowTrayBalloons => PolicyState.Inherited,
            "CheckForUpdatesOnLaunch" when settings.CheckForUpdatesOnLaunch == baseline.CheckForUpdatesOnLaunch => PolicyState.Inherited,
            _ => PolicyState.None
        };
    }

    private static bool NormalizeTheme(AppSettings settings)
    {
        if (ValidThemes.Contains(settings.Theme))
            return false;

        settings.Theme = "Dark";
        return true;
    }

    private static bool NormalizeProfile(AppSettings settings)
    {
        if (ValidProfiles.Contains(settings.BehaviorProfile))
            return false;

        settings.BehaviorProfile = "Standard";
        return true;
    }

    private static bool NormalizeNotificationMode(AppSettings settings)
    {
        if (ValidNotificationModes.Contains(settings.NotificationMode))
            return false;

        settings.NotificationMode = "Standard";
        return true;
    }

    private static bool NormalizeExportLevel(AppSettings settings)
    {
        if (ValidExportLevels.Contains(settings.SupportBundleExportLevel))
            return false;

        settings.SupportBundleExportLevel = "Basic";
        return true;
    }

    private static bool NormalizeLandingPage(AppSettings settings)
    {
        if (ValidLandingPages.Contains(settings.DefaultLandingPage))
            return false;

        settings.DefaultLandingPage = Page.Dashboard.ToString();
        return true;
    }

    private static bool NormalizeSchedule(AppSettings settings)
    {
        var changed = false;
        if (!Enum.TryParse<DayOfWeek>(settings.WeeklyTuneUpDay, ignoreCase: true, out _))
        {
            settings.WeeklyTuneUpDay = DayOfWeek.Sunday.ToString();
            changed = true;
        }

        if (!TimeSpan.TryParse(settings.WeeklyTuneUpTime, out _))
        {
            settings.WeeklyTuneUpTime = "10:00";
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeAutomation(AppSettings settings)
    {
        var changed = false;

        if (!TimeSpan.TryParse(settings.AutomationQuietHoursStart, out _))
        {
            settings.AutomationQuietHoursStart = "22:00";
            changed = true;
        }

        if (!TimeSpan.TryParse(settings.AutomationQuietHoursEnd, out _))
        {
            settings.AutomationQuietHoursEnd = "07:00";
            changed = true;
        }

        if (settings.AutomationRules is null)
        {
            settings.AutomationRules = [];
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeHealthMonitoring(AppSettings settings)
    {
        var changed = false;

        if (!Enum.IsDefined(settings.HealthAlertNotificationFrequency))
        {
            settings.HealthAlertNotificationFrequency = HealthAlertNotificationFrequency.All;
            changed = true;
        }

        settings.DismissedHealthAlerts ??= [];
        return changed;
    }

    private static bool ClampThresholds(AppSettings settings)
    {
        var changed = false;
        var cpuWarning = settings.CpuWarningPct;
        var ramWarning = settings.RamWarningPct;
        var diskWarning = settings.DiskWarningPct;
        var cpuTemp = settings.CpuTempWarningC;

        changed |= Clamp(ref cpuWarning, 50, 99);
        changed |= Clamp(ref ramWarning, 50, 99);
        changed |= Clamp(ref diskWarning, 50, 99);
        changed |= Clamp(ref cpuTemp, 50, 110);

        settings.CpuWarningPct = cpuWarning;
        settings.RamWarningPct = ramWarning;
        settings.DiskWarningPct = diskWarning;
        settings.CpuTempWarningC = cpuTemp;

        return changed;
    }

    private static bool ClampWindowBounds(AppSettings settings)
    {
        var changed = false;
        var width = settings.WindowWidth;
        var height = settings.WindowHeight;

        changed |= Clamp(ref width, 800, 3840);
        changed |= Clamp(ref height, 500, 2160);

        settings.WindowWidth = width;
        settings.WindowHeight = height;
        return changed;
    }

    private static bool Clamp(ref int value, int min, int max)
    {
        var clamped = Math.Clamp(value, min, max);
        if (clamped == value)
            return false;

        value = clamped;
        return true;
    }

    private static bool Clamp(ref double value, double min, double max)
    {
        var clamped = Math.Clamp(value, min, max);
        if (Math.Abs(clamped - value) < double.Epsilon)
            return false;

        value = clamped;
        return true;
    }
}
