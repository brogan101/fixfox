using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using Newtonsoft.Json;
using FormsSystemInformation = System.Windows.Forms.SystemInformation;

namespace HelpDesk.Infrastructure.Services;

public sealed class AutomationHistoryService : IAutomationHistoryService
{
    private const int MaxEntries = 250;
    private readonly List<AutomationRunReceipt> _entries;

    public AutomationHistoryService()
    {
        _entries = LoadEntries();
    }

    public IReadOnlyList<AutomationRunReceipt> Entries => _entries.AsReadOnly();

    public void Record(AutomationRunReceipt entry)
    {
        _entries.Insert(0, entry);
        if (_entries.Count > MaxEntries)
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);

        SaveEntries(_entries);
    }

    public void Clear()
    {
        _entries.Clear();
        SaveEntries(_entries);
    }

    private static List<AutomationRunReceipt> LoadEntries()
    {
        try
        {
            if (!File.Exists(ProductizationPaths.AutomationHistoryFile))
                return [];

            return JsonConvert.DeserializeObject<List<AutomationRunReceipt>>(
                File.ReadAllText(ProductizationPaths.AutomationHistoryFile)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void SaveEntries(IReadOnlyList<AutomationRunReceipt> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProductizationPaths.AutomationHistoryFile)!);
            var tempPath = ProductizationPaths.AutomationHistoryFile + ".tmp";
            File.WriteAllText(tempPath, JsonConvert.SerializeObject(entries, Formatting.Indented));
            File.Move(tempPath, ProductizationPaths.AutomationHistoryFile, overwrite: true);
        }
        catch
        {
        }
    }
}

public sealed class AutomationCoordinatorService : IAutomationCoordinatorService
{
    private readonly ISettingsService _settingsService;
    private readonly IDeploymentConfigurationService _deploymentConfigurationService;
    private readonly IQuickScanService _quickScanService;
    private readonly IRunbookCatalogService _runbookCatalogService;
    private readonly IRunbookExecutionService _runbookExecutionService;
    private readonly IAutomationHistoryService _automationHistoryService;
    private readonly IRepairHistoryService _repairHistoryService;
    private readonly IStatePersistenceService _statePersistenceService;
    private readonly ISystemInfoService _systemInfoService;
    private readonly INotificationService _notificationService;
    private readonly IAppLogger _logger;

    public AutomationCoordinatorService(
        ISettingsService settingsService,
        IDeploymentConfigurationService deploymentConfigurationService,
        IQuickScanService quickScanService,
        IRunbookCatalogService runbookCatalogService,
        IRunbookExecutionService runbookExecutionService,
        IAutomationHistoryService automationHistoryService,
        IRepairHistoryService repairHistoryService,
        IStatePersistenceService statePersistenceService,
        ISystemInfoService systemInfoService,
        INotificationService notificationService,
        IAppLogger logger)
    {
        _settingsService = settingsService;
        _deploymentConfigurationService = deploymentConfigurationService;
        _quickScanService = quickScanService;
        _runbookCatalogService = runbookCatalogService;
        _runbookExecutionService = runbookExecutionService;
        _automationHistoryService = automationHistoryService;
        _repairHistoryService = repairHistoryService;
        _statePersistenceService = statePersistenceService;
        _systemInfoService = systemInfoService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public IReadOnlyList<AutomationRuleSettings> EnsureRules(AppSettings settings)
    {
        var defaults = BuildDefaultRules(settings.BehaviorProfile);
        var existing = settings.AutomationRules ?? [];
        var merged = new List<AutomationRuleSettings>();

        foreach (var rule in defaults)
        {
            var saved = existing.FirstOrDefault(item => string.Equals(item.Id, rule.Id, StringComparison.OrdinalIgnoreCase));
            merged.Add(saved is null ? rule : Merge(rule, saved));
        }

        settings.AutomationRules = merged;
        return settings.AutomationRules;
    }

    public AutomationConditionEvaluation EvaluateRule(
        AutomationRuleSettings rule,
        AppSettings settings,
        SystemSnapshot? snapshot,
        bool hasActiveWork,
        IReadOnlyList<AutomationRunReceipt> history,
        DateTime now)
    {
        var reasons = new List<string>();
        var blocked = false;
        var skipped = false;

        if (!rule.Enabled)
        {
            skipped = true;
            reasons.Add("This automation is turned off.");
        }

        if (settings.AutomationPausedUntilUtc.HasValue && settings.AutomationPausedUntilUtc.Value > now)
        {
            skipped = true;
            reasons.Add($"Automation is paused until {settings.AutomationPausedUntilUtc.Value.ToLocalTime():g}.");
        }

        if (rule.PausedUntilUtc.HasValue && rule.PausedUntilUtc.Value > now)
        {
            skipped = true;
            reasons.Add($"{rule.Title} is paused until {rule.PausedUntilUtc.Value.ToLocalTime():g}.");
        }

        if (rule.SkipNextRun && !rule.IsWatcher)
        {
            skipped = true;
            reasons.Add("Skipped because you asked FixFox to skip the next scheduled run once.");
        }

        if (rule.SkipDuringQuietHours
            && TimeSpan.TryParse(settings.AutomationQuietHoursStart, out var quietStart)
            && TimeSpan.TryParse(settings.AutomationQuietHoursEnd, out var quietEnd)
            && IsWithinQuietHours(now.TimeOfDay, quietStart, quietEnd))
        {
            skipped = true;
            reasons.Add("Skipped because automation quiet hours are active.");
        }

        if (rule.SkipIfActiveRepairSession && hasActiveWork)
        {
            blocked = true;
            reasons.Add("FixFox is already running active work, so this automation stayed out of the way.");
        }

        if (rule.RunOnlyWhenIdle && !rule.IsWatcher)
        {
            var idleMinutes = GetIdleMinutes();
            if (idleMinutes < rule.MinimumIdleMinutes)
            {
                skipped = true;
                reasons.Add($"Skipped because the machine has only been idle for {idleMinutes:N0} minute(s).");
            }
        }

        if (rule.SkipOnBattery && !rule.IsWatcher && IsOnBattery(out var batteryPercent))
        {
            skipped = true;
            reasons.Add(batteryPercent >= 0 && batteryPercent <= rule.MinimumBatteryPercent
                ? $"Skipped because battery is at {batteryPercent}%."
                : "Skipped because the device is on battery power.");
        }

        if (rule.SkipOnMeteredConnection && !rule.IsWatcher && IsMeteredConnection())
        {
            skipped = true;
            reasons.Add("Skipped because the active network is marked as metered.");
        }

        if (!rule.IsWatcher && history.Any(entry =>
                string.Equals(entry.RuleId, rule.Id, StringComparison.OrdinalIgnoreCase)
                && entry.Outcome == AutomationRunOutcome.Completed
                && entry.StartedAt > now.AddHours(-4)))
        {
            skipped = true;
            reasons.Add("Skipped because a similar automation just completed recently.");
        }

        return new AutomationConditionEvaluation
        {
            CanRun = !blocked && !skipped,
            WasBlocked = blocked,
            WasSkipped = skipped,
            Reasons = reasons
        };
    }

    public DateTime? GetNextRun(AutomationRuleSettings rule, DateTime now)
    {
        if (!rule.Enabled || rule.IsWatcher)
            return null;

        return rule.ScheduleKind switch
        {
            AutomationScheduleKind.EveryXDays => NextEveryXDays(rule, now),
            AutomationScheduleKind.WeekdaysOnly => NextWeekday(rule, now),
            AutomationScheduleKind.StartupDelay => now.AddMinutes(Math.Max(1, rule.StartupDelayMinutes)),
            AutomationScheduleKind.Daily => NextDaily(rule, now),
            AutomationScheduleKind.Weekly => NextWeekly(rule, now),
            AutomationScheduleKind.Startup => now.AddMinutes(Math.Max(1, rule.StartupDelayMinutes)),
            _ => null
        };
    }

    public void PopulateRuntimeDetails(
        AutomationRuleSettings rule,
        AppSettings settings,
        SystemSnapshot? snapshot,
        HealthCheckReport? healthReport,
        InterruptedOperationState? interrupted,
        IReadOnlyList<RepairHistoryEntry> repairHistory,
        IReadOnlyList<AutomationRunReceipt> automationHistory,
        bool hasActiveWork,
        DateTime now)
    {
        var evaluation = EvaluateRule(rule, settings, snapshot, hasActiveWork, automationHistory, now);
        var lastRun = automationHistory.FirstOrDefault(entry => string.Equals(entry.RuleId, rule.Id, StringComparison.OrdinalIgnoreCase));
        rule.LastRunText = lastRun is null
            ? "Not yet run"
            : $"{lastRun.StartedAt:g} - {lastRun.Outcome}: {lastRun.Summary}";
        rule.NextRunText = GetNextRun(rule, now)?.ToString("ddd, MMM d 'at' h:mm tt")
            ?? (rule.IsWatcher ? "Checked while FixFox is open" : "Manual only");
        rule.ConditionSummary = evaluation.Summary;

        if (rule.IsWatcher)
            PopulateWatcherStatus(rule, snapshot, healthReport, interrupted, repairHistory, automationHistory);
        else
            PopulateTaskStatus(rule, evaluation, lastRun);
    }

    public async Task<AutomationRunReceipt> RunAsync(
        string ruleId,
        string triggerSource,
        bool manualOverride = false,
        bool hasActiveWork = false,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Load();
        _deploymentConfigurationService.ApplyPolicy(settings);
        var rules = EnsureRules(settings);
        var rule = rules.FirstOrDefault(item => string.Equals(item.Id, ruleId, StringComparison.OrdinalIgnoreCase));
        if (rule is null)
            throw new InvalidOperationException($"No automation rule is registered for \"{ruleId}\".");

        _settingsService.Save(settings);

        SystemSnapshot? snapshot = null;
        try
        {
            snapshot = await _systemInfoService.GetSnapshotAsync();
        }
        catch (Exception ex)
        {
            _logger.Warn($"Automation snapshot load failed for {ruleId}: {ex.Message}");
        }

        var evaluation = manualOverride
            ? new AutomationConditionEvaluation()
            : EvaluateRule(rule, settings, snapshot, hasActiveWork, _automationHistoryService.Entries, DateTime.Now);

        if (!manualOverride && !evaluation.CanRun)
        {
            if (rule.SkipNextRun)
            {
                rule.SkipNextRun = false;
                _settingsService.Save(settings);
            }

            var skipped = new AutomationRunReceipt
            {
                RuleId = rule.Id,
                RuleTitle = rule.Title,
                RuleKind = rule.Kind,
                TriggerSource = triggerSource,
                StartedAt = DateTime.Now,
                FinishedAt = DateTime.Now,
                Outcome = evaluation.WasBlocked ? AutomationRunOutcome.Blocked : AutomationRunOutcome.Skipped,
                Summary = evaluation.WasBlocked ? $"{rule.Title} was blocked." : $"{rule.Title} was skipped.",
                ConditionSummary = evaluation.Summary,
                ChangedSummary = "No changes were made.",
                VerificationSummary = evaluation.Summary,
                NextStep = "Run it manually with override if you need it immediately."
            };

            _automationHistoryService.Record(skipped);
            MaybeNotify(rule, skipped, settings);
            return skipped;
        }

        var startedAt = DateTime.Now;
        AutomationRunReceipt receipt;
        switch (rule.Kind)
        {
            case AutomationRuleKind.QuickHealthCheck:
            case AutomationRuleKind.StartupQuickCheck:
                receipt = await RunQuickHealthAsync(rule, triggerSource, snapshot, startedAt, cancellationToken);
                break;

            case AutomationRuleKind.SafeMaintenance:
            case AutomationRuleKind.BrowserCleanup:
            case AutomationRuleKind.WorkFromHomeReadiness:
            case AutomationRuleKind.MeetingReadiness:
                receipt = await RunRunbookAutomationAsync(rule, triggerSource, startedAt, cancellationToken);
                break;

            default:
                receipt = BuildWatcherReceipt(rule, triggerSource, snapshot, startedAt);
                break;
        }

        _automationHistoryService.Record(receipt);
        MaybeNotify(rule, receipt, settings);
        return receipt;
    }

    private static AutomationRuleSettings Merge(AutomationRuleSettings defaults, AutomationRuleSettings saved) => new()
    {
        Id = defaults.Id,
        Title = defaults.Title,
        Summary = defaults.Summary,
        SafetySummary = defaults.SafetySummary,
        Kind = defaults.Kind,
        Enabled = saved.Enabled,
        IsWatcher = defaults.IsWatcher,
        SupportsScheduling = defaults.SupportsScheduling,
        SupportsScanOnly = defaults.SupportsScanOnly,
        ScheduleKind = saved.ScheduleKind switch
        {
            AutomationScheduleKind.Daily => AutomationScheduleKind.EveryXDays,
            AutomationScheduleKind.Startup => AutomationScheduleKind.StartupDelay,
            _ => saved.ScheduleKind
        },
        IntervalDays = saved.IntervalDays <= 0 ? defaults.IntervalDays : saved.IntervalDays,
        ScheduleDay = string.IsNullOrWhiteSpace(saved.ScheduleDay) ? defaults.ScheduleDay : saved.ScheduleDay,
        ScheduleTime = string.IsNullOrWhiteSpace(saved.ScheduleTime) ? defaults.ScheduleTime : saved.ScheduleTime,
        RunOnlyWhenIdle = saved.RunOnlyWhenIdle,
        MinimumIdleMinutes = saved.MinimumIdleMinutes <= 0 ? defaults.MinimumIdleMinutes : saved.MinimumIdleMinutes,
        SkipOnBattery = saved.SkipOnBattery,
        MinimumBatteryPercent = saved.MinimumBatteryPercent <= 0 ? defaults.MinimumBatteryPercent : saved.MinimumBatteryPercent,
        SkipOnMeteredConnection = saved.SkipOnMeteredConnection,
        SkipDuringQuietHours = saved.SkipDuringQuietHours,
        NotifyOnlyIfIssuesFound = saved.NotifyOnlyIfIssuesFound,
        NotifyOnSkippedOrBlocked = saved.NotifyOnSkippedOrBlocked,
        SkipIfActiveRepairSession = saved.SkipIfActiveRepairSession,
        StartupDelayMinutes = saved.StartupDelayMinutes <= 0 ? defaults.StartupDelayMinutes : saved.StartupDelayMinutes,
        ScanOnly = saved.ScanOnly,
        PausedUntilUtc = saved.PausedUntilUtc,
        SkipNextRun = saved.SkipNextRun,
        IsPinnedToTray = saved.IsPinnedToTray,
        LastPinnedAtUtc = saved.LastPinnedAtUtc,
        IncludedTasks = defaults.IncludedTasks,
        PrimaryAction = defaults.PrimaryAction,
        SecondaryAction = defaults.SecondaryAction
    };

    private static List<AutomationRuleSettings> BuildDefaultRules(string profile)
    {
        var isQuiet = string.Equals(profile, "Quiet", StringComparison.OrdinalIgnoreCase);
        var isPowerUser = string.Equals(profile, "Power User", StringComparison.OrdinalIgnoreCase);
        var isWorkLaptop = string.Equals(profile, "Work Laptop", StringComparison.OrdinalIgnoreCase);

        return
        [
            Rule("quick-health-check", "Scheduled Quick Health Check", "Run a low-noise health scan and only surface meaningful issues.", AutomationRuleKind.QuickHealthCheck, isPowerUser || isWorkLaptop ? AutomationScheduleKind.EveryXDays : isQuiet ? AutomationScheduleKind.Disabled : AutomationScheduleKind.Weekly, !isQuiet, "Monday", isWorkLaptop ? "08:15" : "09:00", false, isWorkLaptop, true, true, Action("Run Quick Scan", "Run the health scan now.", SupportActionKind.None, ""), Action("Open Home", "Review findings on Home.", SupportActionKind.Uri, "fixfox://page/home"), ["Run quick scan", "Check if issues need attention", "Keep quiet if nothing changed"], intervalDays: isPowerUser || isWorkLaptop ? 1 : 7),
            Rule("safe-maintenance", "Scheduled Safe Maintenance", "Run the conservative cleanup workflow with receipts and verification.", AutomationRuleKind.SafeMaintenance, isQuiet ? AutomationScheduleKind.Disabled : AutomationScheduleKind.Weekly, !isQuiet, "Sunday", isWorkLaptop ? "18:30" : "10:00", true, true, true, false, Action("Run Safe Maintenance", "Run the maintenance workflow now.", SupportActionKind.Runbook, "safe-maintenance-runbook"), Action("Open Automation", "Review the workflow and history.", SupportActionKind.Uri, "fixfox://page/automation"), ["Clear temp files", "Empty Recycle Bin", "Check Defender", "Check firewall", "Verify cleanup state"]),
            Rule("startup-quick-check", "Startup Quick Check", "Take a lightweight health snapshot shortly after sign-in so FixFox can surface real issues without dragging startup.", AutomationRuleKind.StartupQuickCheck, isQuiet ? AutomationScheduleKind.Disabled : AutomationScheduleKind.StartupDelay, !isQuiet, "Sunday", "09:00", false, false, true, true, Action("Run Startup Check", "Run the startup check now.", SupportActionKind.None, ""), Action("Open Home", "Review startup findings on Home.", SupportActionKind.Uri, "fixfox://page/home"), ["Delay until startup settles", "Run quick scan", "Surface only meaningful issues"], startupDelayMinutes: isPowerUser ? 2 : 3),
            Rule("browser-cleanup-automation", "Browser Cleanup", "Clear stale browser state when web apps are the problem without broad network resets.", AutomationRuleKind.BrowserCleanup, AutomationScheduleKind.Manual, !isQuiet, "Wednesday", "18:00", false, false, true, false, Action("Run Browser Rescue", "Start the browser cleanup workflow.", SupportActionKind.Runbook, "browser-problem-runbook"), Action("Open Browser Center", "Review browser support paths.", SupportActionKind.Uri, "fixfox://page/device-health"), ["Confirm browser-only scope", "Clear browser caches", "Flush DNS", "Verify browsing"]),
            Rule("work-from-home-readiness", "Work-From-Home Readiness", "Check the basics that usually block remote work before the morning gets noisy.", AutomationRuleKind.WorkFromHomeReadiness, isWorkLaptop ? AutomationScheduleKind.Weekly : AutomationScheduleKind.Manual, isWorkLaptop || isPowerUser, "Monday", "08:00", false, isWorkLaptop, false, false, Action("Run Work-From-Home Rescue", "Start the remote-work workflow.", SupportActionKind.Runbook, "work-from-home-runbook"), Action("Open Network Center", "Jump to the remote-work support center.", SupportActionKind.Uri, "fixfox://page/device-health"), ["Validate internet", "Check VPN path", "Review internal resource access", "Verify remote-work basics"]),
            Rule("meeting-readiness", "Meeting Readiness Check", "Confirm microphone, camera, and speaker paths before you need them.", AutomationRuleKind.MeetingReadiness, isWorkLaptop ? AutomationScheduleKind.WeekdaysOnly : AutomationScheduleKind.Manual, !isQuiet, "Friday", "08:30", false, false, false, false, Action("Run Meeting Readiness", "Start the meeting-device workflow.", SupportActionKind.Runbook, "meeting-device-runbook"), Action("Open Devices Center", "Review meeting-device paths.", SupportActionKind.Uri, "fixfox://page/device-health"), ["Check microphone access", "Check camera access", "Confirm device visibility", "Verify meeting devices"]),
            Watcher("low-disk-watcher", "Low Disk Watcher", "Watch for storage pressure and route straight into cleanup when it matters.", AutomationRuleKind.LowDiskWatcher, !isQuiet, Action("Open Storage Center", "Review cleanup paths.", SupportActionKind.Uri, "fixfox://page/device-health"), Action("Run Cleanup Now", "Run the disk-full rescue workflow.", SupportActionKind.Runbook, "disk-full-rescue-runbook")),
            Watcher("pending-reboot-watcher", "Pending Reboot Watcher", "Watch for reboot-needed states that keep repairs from finishing cleanly.", AutomationRuleKind.PendingRebootWatcher, true, Action("Open Windows Repair", "Review reboot and update repair paths.", SupportActionKind.Uri, "fixfox://page/device-health"), Action("Open Recovery Options", "Jump into Windows recovery.", SupportActionKind.Toolbox, "Recovery Options")),
            Watcher("defender-firewall-watcher", "Defender / Firewall Watcher", "Watch for security basics that need attention before deeper cleanup or repair.", AutomationRuleKind.DefenderFirewallWatcher, !isQuiet, Action("Check Security", "Route into the security path.", SupportActionKind.Fix, "check-defender-status"), Action("Open Device Health", "Review the security summary.", SupportActionKind.Uri, "fixfox://page/device-health")),
            Watcher("repeated-failure-watcher", "Repeated Failure Watcher", "Stop blind retry loops and suggest escalation when the same fix or workflow keeps failing.", AutomationRuleKind.RepeatedFailureWatcher, true, Action("Open Activity", "Compare recent failures.", SupportActionKind.Uri, "fixfox://page/history"), Action("Create Support Package", "Capture a support package before escalating.", SupportActionKind.Uri, "fixfox://page/support")),
            Watcher("spooler-watcher", "Spooler Watcher", "Watch for a stopped print spooler and route to the print rescue flow when it recurs.", AutomationRuleKind.SpoolerWatcher, isPowerUser || isWorkLaptop, Action("Run Printing Rescue", "Start the print rescue workflow.", SupportActionKind.Runbook, "printing-rescue-runbook"), Action("Open Printers & Scanners", "Jump into printer settings.", SupportActionKind.Toolbox, "Printers & Scanners")),
            Watcher("update-health-watcher", "Update Health Watcher", "Watch for update backlogs and unhealthy servicing states without nagging when nothing changed.", AutomationRuleKind.UpdateHealthWatcher, true, Action("Open Windows Repair", "Review the Windows repair center.", SupportActionKind.Uri, "fixfox://page/device-health"), Action("Open Windows Update", "Jump straight to update settings.", SupportActionKind.Toolbox, "Windows Update")),
            Watcher("interrupted-repair-watcher", "Interrupted Repair Watcher", "Watch for paused or interrupted repair work so you can resume it safely.", AutomationRuleKind.InterruptedRepairWatcher, true, Action("Resume Interrupted Repair", "Continue the last guided repair if it is still safe.", SupportActionKind.Uri, "fixfox://action/resume-interrupted"), Action("Open Automation", "Review automation and repair attention.", SupportActionKind.Uri, "fixfox://page/automation")),
            Watcher("network-failure-watcher", "Repeated Network Failure Watcher", "Watch for recurring network or VPN failures and stop retrying the same recovery path blindly.", AutomationRuleKind.NetworkFailureWatcher, isWorkLaptop || isPowerUser, Action("Run Work-From-Home Rescue", "Escalate into the remote-work workflow.", SupportActionKind.Runbook, "work-from-home-runbook"), Action("Open Network Center", "Review remote-work support paths.", SupportActionKind.Uri, "fixfox://page/device-health"))
        ];
    }

    private static AutomationRuleSettings Rule(
        string id,
        string title,
        string summary,
        AutomationRuleKind kind,
        AutomationScheduleKind scheduleKind,
        bool enabled,
        string scheduleDay,
        string scheduleTime,
        bool runOnlyWhenIdle,
        bool skipOnBattery,
        bool supportsScanOnly,
        bool scanOnly,
        SupportAction primaryAction,
        SupportAction secondaryAction,
        List<string> includedTasks,
        int startupDelayMinutes = 3,
        int intervalDays = 1) => new()
    {
        Id = id,
        Title = title,
        Summary = summary,
        SafetySummary = "Background automation stays inside scan-only or safe maintenance boundaries unless you run it manually.",
        Kind = kind,
        Enabled = enabled,
        SupportsScheduling = true,
        SupportsScanOnly = supportsScanOnly,
        ScheduleKind = scheduleKind,
        IntervalDays = intervalDays,
        ScheduleDay = scheduleDay,
        ScheduleTime = scheduleTime,
        RunOnlyWhenIdle = runOnlyWhenIdle,
        MinimumIdleMinutes = 10,
        SkipOnBattery = skipOnBattery,
        MinimumBatteryPercent = 35,
        SkipOnMeteredConnection = false,
        SkipDuringQuietHours = true,
        NotifyOnlyIfIssuesFound = true,
        NotifyOnSkippedOrBlocked = false,
        SkipIfActiveRepairSession = true,
        StartupDelayMinutes = startupDelayMinutes,
        ScanOnly = scanOnly,
        IncludedTasks = includedTasks,
        PrimaryAction = primaryAction,
        SecondaryAction = secondaryAction
    };

    private static AutomationRuleSettings Watcher(
        string id,
        string title,
        string summary,
        AutomationRuleKind kind,
        bool enabled,
        SupportAction primaryAction,
        SupportAction secondaryAction) => new()
    {
        Id = id,
        Title = title,
        Summary = summary,
        SafetySummary = "Watchers review state and recommend action. They do not apply deep fixes silently.",
        Kind = kind,
        Enabled = enabled,
        IsWatcher = true,
        SupportsScheduling = false,
        SupportsScanOnly = true,
        ScheduleKind = AutomationScheduleKind.Manual,
        ScheduleDay = DayOfWeek.Sunday.ToString(),
        ScheduleTime = "09:00",
        MinimumBatteryPercent = 25,
        NotifyOnlyIfIssuesFound = true,
        IncludedTasks = ["Review state", "Surface action only when attention is needed"],
        PrimaryAction = primaryAction,
        SecondaryAction = secondaryAction
    };

    private static SupportAction Action(string label, string description, SupportActionKind kind, string targetId) => new()
    {
        Label = label,
        Description = description,
        Kind = kind,
        TargetId = targetId
    };

    private static bool IsWithinQuietHours(TimeSpan now, TimeSpan start, TimeSpan end) =>
        start <= end ? now >= start && now < end : now >= start || now < end;

    private static DateTime? NextDaily(AutomationRuleSettings rule, DateTime now)
    {
        if (!TimeSpan.TryParse(rule.ScheduleTime, out var time))
            time = TimeSpan.FromHours(9);

        var next = now.Date.Add(time);
        return next <= now ? next.AddDays(1) : next;
    }

    private static DateTime? NextEveryXDays(AutomationRuleSettings rule, DateTime now)
    {
        var next = NextDaily(rule, now) ?? now.AddDays(Math.Max(1, rule.IntervalDays));
        var interval = Math.Max(1, rule.IntervalDays);
        while (next <= now)
            next = next.AddDays(interval);
        return next;
    }

    private static DateTime? NextWeekday(AutomationRuleSettings rule, DateTime now)
    {
        var next = NextDaily(rule, now) ?? now.AddDays(1);
        while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            next = next.AddDays(1);
        return next;
    }

    private static DateTime? NextWeekly(AutomationRuleSettings rule, DateTime now)
    {
        if (!Enum.TryParse<DayOfWeek>(rule.ScheduleDay, ignoreCase: true, out var day))
            day = DayOfWeek.Sunday;
        if (!TimeSpan.TryParse(rule.ScheduleTime, out var time))
            time = TimeSpan.FromHours(9);

        var next = now.Date.Add(time);
        while (next.DayOfWeek != day || next <= now)
            next = next.AddDays(1);
        return next;
    }

    private void PopulateTaskStatus(AutomationRuleSettings rule, AutomationConditionEvaluation evaluation, AutomationRunReceipt? lastRun)
    {
        if (lastRun is null)
        {
            rule.StatusText = evaluation.CanRun ? "Ready" : evaluation.Summary;
            rule.NeedsAttention = evaluation.WasBlocked;
            rule.Severity = evaluation.WasBlocked ? ScanSeverity.Warning : ScanSeverity.Good;
            return;
        }

        rule.StatusText = lastRun.Summary;
        rule.NeedsAttention = lastRun.UserActionRequired;
        rule.Severity = lastRun.Outcome switch
        {
            AutomationRunOutcome.Failed => ScanSeverity.Critical,
            AutomationRunOutcome.Blocked => ScanSeverity.Warning,
            AutomationRunOutcome.Skipped => ScanSeverity.Warning,
            _ when lastRun.UserActionRequired => ScanSeverity.Warning,
            _ => ScanSeverity.Good
        };
    }

    private void PopulateWatcherStatus(
        AutomationRuleSettings rule,
        SystemSnapshot? snapshot,
        HealthCheckReport? healthReport,
        InterruptedOperationState? interrupted,
        IReadOnlyList<RepairHistoryEntry> repairHistory,
        IReadOnlyList<AutomationRunReceipt> automationHistory)
    {
        var (attention, severity, status, nextStep) = EvaluateWatcher(rule.Kind, snapshot, healthReport, interrupted, repairHistory, automationHistory);
        rule.NeedsAttention = attention;
        rule.Severity = severity;
        rule.StatusText = status;
        rule.ConditionSummary = nextStep;
    }

    private (bool Attention, ScanSeverity Severity, string Status, string NextStep) EvaluateWatcher(
        AutomationRuleKind kind,
        SystemSnapshot? snapshot,
        HealthCheckReport? healthReport,
        InterruptedOperationState? interrupted,
        IReadOnlyList<RepairHistoryEntry> repairHistory,
        IReadOnlyList<AutomationRunReceipt> automationHistory)
    {
        switch (kind)
        {
            case AutomationRuleKind.LowDiskWatcher:
                if (snapshot is not null && (snapshot.DiskFreeGb <= 20 || snapshot.DiskUsedPct >= 90))
                {
                    var critical = snapshot.DiskFreeGb <= 10 || snapshot.DiskUsedPct >= 95;
                    return (true, critical ? ScanSeverity.Critical : ScanSeverity.Warning,
                        $"{snapshot.DiskFreeGb:N0} GB free on C:. Storage attention is needed.",
                        "Open the Storage Center or run Disk Full Rescue now.");
                }
                return (false, ScanSeverity.Good, "Storage pressure looks steady.", "No action needed.");

            case AutomationRuleKind.PendingRebootWatcher:
                if (snapshot is not null && snapshot.PendingUpdateCount > 0)
                    return (true, ScanSeverity.Warning, $"{snapshot.PendingUpdateCount} pending update item(s) still suggest a reboot.", "Restart the PC when it is safe, then rerun blocked workflows.");
                return (false, ScanSeverity.Good, "No reboot reminder is currently active.", "No action needed.");

            case AutomationRuleKind.DefenderFirewallWatcher:
                if (snapshot is not null && !snapshot.DefenderEnabled)
                    return (true, ScanSeverity.Critical, "Defender protection is not confirmed.", "Open the security path before deeper cleanup.");
                return (false, ScanSeverity.Good, "Security basics look steady.", "No action needed.");

            case AutomationRuleKind.RepeatedFailureWatcher:
                var repeatedFailure = repairHistory
                    .Where(entry => !entry.Success)
                    .GroupBy(entry => string.IsNullOrWhiteSpace(entry.RunbookId) ? entry.FixId : entry.RunbookId)
                    .FirstOrDefault(group => group.Count() >= 2 && !string.IsNullOrWhiteSpace(group.Key));
                if (repeatedFailure is not null)
                    return (true, ScanSeverity.Warning, "The same repair path has failed repeatedly.", "Open Activity, compare failures, and create a support package.");
                return (false, ScanSeverity.Good, "No repeated repair loop is currently obvious.", "No action needed.");

            case AutomationRuleKind.SpoolerWatcher:
                if (IsSpoolerStopped())
                    return (true, ScanSeverity.Warning, "The Print Spooler service is stopped.", "Run Printing Rescue or review printer services.");
                return (false, ScanSeverity.Good, "Print spooler looks healthy.", "No action needed.");

            case AutomationRuleKind.UpdateHealthWatcher:
                if (snapshot is not null && snapshot.PendingUpdateCount >= 3)
                    return (true, ScanSeverity.Warning, "Windows Update needs attention.", "Open the Windows repair path or Windows Update settings.");
                if (repairHistory.Take(8).Any(entry => !entry.Success && entry.FixTitle.Contains("update", StringComparison.OrdinalIgnoreCase)))
                    return (true, ScanSeverity.Warning, "Recent update repair failures were detected.", "Move to the Windows repair center before retrying.");
                return (false, ScanSeverity.Good, "Update health looks steady.", "No action needed.");

            case AutomationRuleKind.InterruptedRepairWatcher:
                if (interrupted is not null)
                    return (true, ScanSeverity.Warning, interrupted.Summary, "Resume or dismiss the interrupted repair.");
                return (false, ScanSeverity.Good, "No interrupted repair is waiting.", "No action needed.");

            case AutomationRuleKind.NetworkFailureWatcher:
                var networkFailures = repairHistory.Take(10).Count(entry => !entry.Success && (
                    entry.CategoryId.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                    entry.CategoryId.Contains("remote", StringComparison.OrdinalIgnoreCase) ||
                    entry.FixTitle.Contains("vpn", StringComparison.OrdinalIgnoreCase)));
                if (networkFailures >= 2)
                    return (true, ScanSeverity.Warning, "Recent network or VPN fixes are still failing.", "Use the Work-From-Home Rescue workflow and stop retrying blind fixes.");
                return (false, ScanSeverity.Good, "No repeated network failure pattern was detected.", "No action needed.");

            default:
                return (false, ScanSeverity.Good, "No action required.", "No action needed.");
        }
    }

    private async Task<AutomationRunReceipt> RunQuickHealthAsync(
        AutomationRuleSettings rule,
        string triggerSource,
        SystemSnapshot? snapshot,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var results = await _quickScanService.ScanAsync();
        var issueCount = results.Count(result => result.Severity != ScanSeverity.Good);
        return new AutomationRunReceipt
        {
            RuleId = rule.Id,
            RuleTitle = rule.Title,
            RuleKind = rule.Kind,
            TriggerSource = triggerSource,
            StartedAt = startedAt,
            FinishedAt = DateTime.Now,
            Outcome = AutomationRunOutcome.Completed,
            Summary = issueCount == 0 ? "Health check ran quietly and found no issues that need attention." : $"Health check found {issueCount} issue(s) that need attention.",
            PrecheckSummary = snapshot is null ? "System snapshot was not available before the scan." : $"{snapshot.DiskFreeGb:N0} GB free, {snapshot.RamUsedPct:N0}% memory in use, {snapshot.PendingUpdateCount} pending update item(s).",
            ChangedSummary = "This automation only scanned device health. No changes were made.",
            VerificationSummary = issueCount == 0 ? "Scan completed with no warning or critical findings." : string.Join(" ", results.Where(result => result.Severity != ScanSeverity.Good).Take(3).Select(result => result.Title)),
            NextStep = issueCount == 0 ? "No further action is needed." : "Open Home or Automation to review the findings and choose the next safe action.",
            ConditionSummary = rule.ScanOnly ? "Scan-only automation." : "Safe automation boundary.",
            UserActionRequired = issueCount > 0,
            TasksAttempted = rule.IncludedTasks
        };
    }

    private async Task<AutomationRunReceipt> RunRunbookAutomationAsync(
        AutomationRuleSettings rule,
        string triggerSource,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var runbook = _runbookCatalogService.Runbooks.FirstOrDefault(item =>
            string.Equals(item.Id, rule.PrimaryAction.TargetId, StringComparison.OrdinalIgnoreCase));
        if (runbook is null)
            throw new InvalidOperationException($"Runbook \"{rule.PrimaryAction.TargetId}\" is not available for {rule.Title}.");

        var summary = await _runbookExecutionService.ExecuteAsync(runbook, cancellationToken: cancellationToken);
        return new AutomationRunReceipt
        {
            RuleId = rule.Id,
            RuleTitle = rule.Title,
            RuleKind = rule.Kind,
            TriggerSource = triggerSource,
            StartedAt = startedAt,
            FinishedAt = DateTime.Now,
            Outcome = summary.Success ? AutomationRunOutcome.Completed : AutomationRunOutcome.Failed,
            Summary = summary.Summary,
            PrecheckSummary = $"{runbook.Title} started with {runbook.Steps.Count} workflow step(s).",
            ChangedSummary = string.Join(" ", summary.Timeline.Take(4)),
            VerificationSummary = summary.Success ? "Workflow completed its planned steps." : "Workflow did not complete cleanly. Review the timeline and next safe path.",
            NextStep = summary.Success ? "Review the receipt or return to Home if the symptom is resolved." : "Open Automation or Activity, review what failed, and create a support package if needed.",
            ConditionSummary = "Safe workflow automation using the trusted runbook pipeline.",
            UserActionRequired = !summary.Success,
            TasksAttempted = rule.IncludedTasks,
            RelatedRunbookId = runbook.Id
        };
    }

    private AutomationRunReceipt BuildWatcherReceipt(
        AutomationRuleSettings rule,
        string triggerSource,
        SystemSnapshot? snapshot,
        DateTime startedAt)
    {
        var (attention, _, status, nextStep) = EvaluateWatcher(rule.Kind, snapshot, null, _statePersistenceService.Load(), _repairHistoryService.Entries, _automationHistoryService.Entries);
        return new AutomationRunReceipt
        {
            RuleId = rule.Id,
            RuleTitle = rule.Title,
            RuleKind = rule.Kind,
            TriggerSource = triggerSource,
            StartedAt = startedAt,
            FinishedAt = DateTime.Now,
            Outcome = AutomationRunOutcome.Completed,
            Summary = status,
            PrecheckSummary = "Watcher reviewed the current device state and recent receipts.",
            ChangedSummary = "Watcher-only automation made no changes.",
            VerificationSummary = attention ? "Attention is still required." : "No action required right now.",
            NextStep = nextStep,
            ConditionSummary = "Watcher evaluation.",
            UserActionRequired = attention,
            TasksAttempted = rule.IncludedTasks,
            RelatedSupportCenterId = rule.SecondaryAction.TargetId
        };
    }

    private void MaybeNotify(AutomationRuleSettings rule, AutomationRunReceipt receipt, AppSettings settings)
    {
        if (!settings.ShowNotifications)
            return;

        if (settings.NotificationMode.Equals("Quiet", StringComparison.OrdinalIgnoreCase))
            return;

        if (rule.NotifyOnlyIfIssuesFound && !receipt.UserActionRequired && receipt.Outcome == AutomationRunOutcome.Completed)
            return;

        if (!rule.NotifyOnSkippedOrBlocked && receipt.Outcome is AutomationRunOutcome.Skipped or AutomationRunOutcome.Blocked)
            return;

        _notificationService.Add(new AppNotification
        {
            Level = receipt.UserActionRequired || receipt.Outcome == AutomationRunOutcome.Failed ? NotifLevel.Warning : NotifLevel.Info,
            Title = rule.Title,
            Message = receipt.Summary
        });
    }

    private static bool IsOnBattery(out int batteryPercent)
    {
        try
        {
            var status = FormsSystemInformation.PowerStatus;
            batteryPercent = (int)Math.Round(status.BatteryLifePercent * 100);
            return status.PowerLineStatus != System.Windows.Forms.PowerLineStatus.Online;
        }
        catch
        {
            batteryPercent = -1;
            return false;
        }
    }

    private static double GetIdleMinutes()
    {
        try
        {
            var info = new LASTINPUTINFO
            {
                cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
            };

            if (!GetLastInputInfo(ref info))
                return 0;

            var idleMilliseconds = unchecked((uint)Environment.TickCount) - info.dwTime;
            return idleMilliseconds / 60000d;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsSpoolerStopped()
    {
        try
        {
            var psi = new ProcessStartInfo("sc", "query Spooler")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null)
                return false;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMeteredConnection()
    {
        try
        {
            var script = "try { (Get-NetConnectionProfile | Select-Object -First 1 -ExpandProperty NetworkCostType) } catch { '' }";
            var psi = new ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return false;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);
            return output.Contains("Fixed", StringComparison.OrdinalIgnoreCase)
                || output.Contains("Variable", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
}
