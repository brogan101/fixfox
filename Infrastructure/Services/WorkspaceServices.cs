using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace HelpDesk.Infrastructure.Services;

public sealed class ToolboxService : IToolboxService
{
    public IReadOnlyList<ToolboxGroup> Groups { get; } =
    [
        new ToolboxGroup
        {
            Title = "Windows Utilities",
            Description = "Open the native troubleshooting tools technicians actually use.",
            Entries =
            [
                Entry("Task Manager", "Inspect running apps, startup pressure, and hung processes.", "taskmgr.exe"),
                Entry("Device Manager", "Review hardware status, drivers, and disabled devices.", "devmgmt.msc"),
                Entry("Services", "Check service state before or after a repair.", "services.msc",
                    minimumEdition: AppEdition.Pro, requiresAdvancedMode: true, requiredCapability: ProductCapability.AdvancedToolbox,
                    supportNote: "Useful when a repair step says a Windows service should be running."),
                Entry("Event Viewer", "Review application, system, and update errors.", "eventvwr.msc",
                    minimumEdition: AppEdition.Pro, requiresAdvancedMode: true, requiredCapability: ProductCapability.AdvancedToolbox,
                    supportNote: "Use this when FixFox can only confirm the symptom but Windows still reports a deeper fault."),
                Entry("Resource Monitor", "Inspect CPU, disk, memory, and network pressure.", "resmon.exe",
                    minimumEdition: AppEdition.Pro, requiresAdvancedMode: true, requiredCapability: ProductCapability.AdvancedToolbox,
                    supportNote: "Helpful for slow-PC and background-pressure cases."),
                Entry("System Information", "Open the detailed Windows hardware/software inventory.", "msinfo32.exe"),
                Entry("Reliability Monitor", "Jump straight to app and system reliability history.", "perfmon.exe", "/rel",
                    minimumEdition: AppEdition.Pro, requiresAdvancedMode: true, requiredCapability: ProductCapability.AdvancedToolbox,
                    supportNote: "Good when the issue looks unstable across app launches or recent updates."),
                Entry("Credential Manager", "Review saved credentials when sign-in or share access is failing.", "control.exe", "/name Microsoft.CredentialManager",
                    minimumEdition: AppEdition.Pro, requiresAdvancedMode: true, requiredCapability: ProductCapability.AdvancedToolbox,
                    supportNote: "Use this for file-share and work-resource access failures."),
                Entry("Quick Assist", "Start or receive remote help when local self-service has reached its limit.", "ms-quick-assist:",
                    supportNote: "Use this when the next safe step is guided remote help."),
                Entry("Get Help", "Open Microsoft's guided support app for Windows-native escalation paths.", "ms-contact-support:",
                    supportNote: "Use this when Microsoft support or Windows recovery guidance is the better path.")
            ]
        },
        new ToolboxGroup
        {
            Title = "System Settings",
            Description = "Jump straight into the Windows settings pages most support sessions need.",
            Entries =
            [
                Entry("Installed Apps", "Open Apps & Features for repair, reset, or uninstall.", "ms-settings:appsfeatures"),
                Entry("Startup Apps", "Review which apps load at sign-in.", "ms-settings:startupapps"),
                Entry("Storage Settings", "Open Storage and Storage Sense guidance.", "ms-settings:storagesense"),
                Entry("Cleanup Recommendations", "Review Windows cleanup suggestions before deleting larger files.", "ms-settings:cleanuprecommendations"),
                Entry("Storage Sense", "Open automatic cleanup and retention controls.", "ms-settings:storagesense"),
                Entry("Date & Time", "Correct time drift that can break VPN, sign-in, and browser certificates.", "ms-settings:dateandtime"),
                Entry("Wi-Fi", "Open Wi-Fi controls and network list.", "ms-settings:network-wifi"),
                Entry("VPN", "Open VPN connections and remote access settings.", "ms-settings:network-vpn"),
                Entry("Network Adapters", "Open adapter management for resets and status checks.", "ncpa.cpl"),
                Entry("Sound Settings", "Review playback, recording, and default device state.", "ms-settings:sound"),
                Entry("Microphone Privacy", "Review microphone access and app permissions.", "ms-settings:privacy-microphone"),
                Entry("Camera Privacy", "Review camera access and privacy gating.", "ms-settings:privacy-webcam"),
                Entry("Display", "Open display layout, scale, and monitor configuration.", "ms-settings:display"),
                Entry("Bluetooth & Devices", "Open paired devices, docks, and Bluetooth controls.", "ms-settings:bluetooth"),
                Entry("Printers & Scanners", "Open printers and scanners.", "ms-settings:printers"),
                Entry("Windows Update", "Check update state, retry scans, or review failures.", "ms-settings:windowsupdate"),
                Entry("Recovery Options", "Open Windows recovery options and repair routes.", "ms-settings:recovery"),
                Entry("Default Apps", "Repair broken file or browser associations.", "ms-settings:defaultapps"),
                Entry("Power & Battery", "Review battery saver, power mode, and sleep behavior.", "ms-settings:powersleep")
            ]
        }
    ];

    public void Launch(ToolboxEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.LaunchTarget))
            throw new InvalidOperationException("This toolbox item is missing a launch target.");

        var info = string.IsNullOrWhiteSpace(entry.LaunchArguments)
            ? new ProcessStartInfo(entry.LaunchTarget)
            : new ProcessStartInfo(entry.LaunchTarget, entry.LaunchArguments);

        info.UseShellExecute = true;
        Process.Start(info);
    }

    private static ToolboxEntry Entry(
        string title,
        string description,
        string target,
        string arguments = "",
        AppEdition minimumEdition = AppEdition.Basic,
        bool requiresAdvancedMode = false,
        ProductCapability requiredCapability = ProductCapability.None,
        string supportNote = "") => new()
    {
        Title = title,
        ToolKey = BuildToolKey(title),
        Description = description,
        LaunchTarget = target,
        LaunchArguments = arguments,
        MinimumEdition = minimumEdition,
        RequiresAdvancedMode = requiresAdvancedMode,
        RequiredCapability = requiredCapability,
        SupportNote = supportNote
    };

    private static string BuildToolKey(string title)
    {
        var parts = title.Split([' ', '&', '/', '-', '.', ',', ':', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]);
        return string.Concat(parts);
    }
}

public sealed class DashboardWorkspaceService : IDashboardWorkspaceService
{
    public IReadOnlyList<DashboardAlert> BuildAlerts(
        SystemSnapshot? snapshot,
        HealthCheckReport? healthReport,
        AppUpdateInfo? updateInfo,
        InterruptedOperationState? interrupted,
        IReadOnlyList<RepairHistoryEntry> historyEntries)
    {
        var alerts = new List<DashboardAlert>();

        if (interrupted is not null)
        {
            alerts.Add(new DashboardAlert
            {
                Key = "interrupted-repair",
                Title = "Interrupted repair needs review",
                Summary = interrupted.Summary,
                Severity = ScanSeverity.Warning,
                ActionLabel = "Open Support",
                ActionKind = DashboardActionKind.Page,
                ActionPage = Page.Handoff
            });
        }

        if (snapshot is not null)
        {
            if (snapshot.PendingUpdateCount > 0)
            {
                alerts.Add(new DashboardAlert
                {
                    Key = "pending-updates",
                    Title = "Pending Windows updates need attention",
                    Summary = $"{snapshot.PendingUpdateCount} update(s) are still waiting and may be blocking a restart or repair.",
                    Severity = ScanSeverity.Warning,
                    ActionLabel = "Open Update Repair",
                    ActionKind = DashboardActionKind.Fix,
                    ActionTargetId = "open-windows-update"
                });
            }

            if (snapshot.DiskUsedPct >= 90 || snapshot.DiskFreeGb <= 20)
            {
                alerts.Add(new DashboardAlert
                {
                    Key = "storage-pressure",
                    Title = "System drive is under pressure",
                    Summary = $"{snapshot.DiskFreeGb:N0} GB free on C:. Storage cleanup is likely worth doing now.",
                    Severity = snapshot.DiskUsedPct >= 95 ? ScanSeverity.Critical : ScanSeverity.Warning,
                    ActionLabel = "Run Temp Cleanup",
                    ActionKind = DashboardActionKind.Fix,
                    ActionTargetId = "clear-temp-files"
                });
            }

            if (!snapshot.DefenderEnabled)
            {
                alerts.Add(new DashboardAlert
                {
                    Key = "defender-check",
                    Title = "Defender protection is not confirmed",
                    Summary = "Security status should be checked before deeper repair or browser cleanup work.",
                    Severity = ScanSeverity.Critical,
                    ActionLabel = "Check Security",
                    ActionKind = DashboardActionKind.Fix,
                    ActionTargetId = "check-defender-status"
                });
            }

            if (snapshot.HasBattery && string.Equals(snapshot.BatteryHealth, "Fair", StringComparison.OrdinalIgnoreCase))
            {
                alerts.Add(new DashboardAlert
                {
                    Key = "battery-health",
                    Title = "Battery health is trending down",
                    Summary = "Generate a battery report before chasing vague shutdown or runtime issues.",
                    Severity = ScanSeverity.Warning,
                    ActionLabel = "Open Battery Report",
                    ActionKind = DashboardActionKind.Fix,
                    ActionTargetId = "generate-battery-report"
                });
            }
        }

        if (historyEntries.Take(6).Count(entry => !entry.Success) >= 2)
        {
            alerts.Add(new DashboardAlert
            {
                Key = "recent-repair-failures",
                Title = "Recent repairs are failing",
                Summary = "This device has seen multiple recent failures. Create a support package before pushing deeper fixes.",
                Severity = ScanSeverity.Critical,
                ActionLabel = "Create Support Package",
                ActionKind = DashboardActionKind.Page,
                ActionPage = Page.Handoff
            });
        }

        if (updateInfo?.UpdateAvailable == true)
        {
            alerts.Add(new DashboardAlert
            {
                Key = "fixfox-update-available",
                Title = "A newer FixFox build is available",
                Summary = updateInfo.Summary,
                Severity = ScanSeverity.Warning,
                ActionLabel = "Open Settings",
                ActionKind = DashboardActionKind.Page,
                ActionPage = Page.Settings
            });
        }

        if (healthReport is not null && healthReport.OverallScore < 70)
        {
            alerts.Add(new DashboardAlert
            {
                Key = "health-check-low-score",
                Title = "Health check found multiple cleanup targets",
                Summary = $"{healthReport.OverallScore}/100 â€” {healthReport.Summary}",
                Severity = healthReport.OverallScore < 50 ? ScanSeverity.Critical : ScanSeverity.Warning,
                ActionLabel = "Run Maintenance",
                ActionKind = DashboardActionKind.Runbook,
                ActionTargetId = "routine-maintenance-runbook"
            });
        }

        return alerts
            .OrderByDescending(alert => alert.Severity)
            .ThenBy(alert => alert.Title)
            .Take(5)
            .ToList();
    }

    public IReadOnlyList<RunbookDefinition> RecommendRunbooks(
        SystemSnapshot? snapshot,
        IReadOnlyList<ScanResult> scanResults,
        IReadOnlyList<RepairHistoryEntry> historyEntries,
        IReadOnlyList<RunbookDefinition> runbooks)
    {
        var ranked = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void Boost(string id, int amount)
        {
            if (!ranked.TryAdd(id, amount))
                ranked[id] += amount;
        }

        foreach (var finding in scanResults)
        {
            if (ContainsAny(finding.Title, finding.Detail, "internet", "dns", "network", "wifi", "vpn"))
                Boost("internet-recovery-runbook", 5);
            if (ContainsAny(finding.Title, finding.Detail, "browser", "website", "cache"))
                Boost("browser-problem-runbook", 4);
            if (ContainsAny(finding.Title, finding.Detail, "startup", "temp", "memory", "disk", "slow"))
                Boost("slow-pc-runbook", 5);
            if (ContainsAny(finding.Title, finding.Detail, "battery"))
                Boost("routine-maintenance-runbook", 2);
        }

        if (snapshot is not null)
        {
            if (snapshot.DiskUsedPct >= 85 || snapshot.RamUsedPct >= 85)
                Boost("slow-pc-runbook", 4);
            if (snapshot.PendingUpdateCount > 0)
                Boost("windows-repair-runbook", 4);
            if (snapshot.HasBattery && !string.IsNullOrWhiteSpace(snapshot.BatteryStatus))
                Boost("routine-maintenance-runbook", 2);
        }

        foreach (var entry in historyEntries.Take(10))
        {
            if (ContainsAny(entry.FixTitle, entry.ChangedSummary, "vpn", "teams", "share", "mapped drive"))
                Boost("work-from-home-runbook", 4);
            if (ContainsAny(entry.FixTitle, entry.ChangedSummary, "browser", "cache", "website"))
                Boost("browser-problem-runbook", 3);
            if (ContainsAny(entry.FixTitle, entry.ChangedSummary, "camera", "microphone", "audio", "speaker", "webcam"))
                Boost("meeting-device-runbook", 5);
            if (ContainsAny(entry.FixTitle, entry.ChangedSummary, "update", "sfc", "dism", "component"))
                Boost("windows-repair-runbook", 4);
        }

        Boost("routine-maintenance-runbook", 1);

        return runbooks
            .Where(runbook => ranked.ContainsKey(runbook.Id))
            .OrderByDescending(runbook => ranked[runbook.Id])
            .ThenBy(runbook => runbook.Title)
            .Take(3)
            .ToList();
    }

    public IReadOnlyList<DashboardSuggestion> BuildSuggestions(
        DashboardSuggestionSignals signals,
        IReadOnlyList<AutomationRuleSettings> automationRules,
        IReadOnlyList<AutomationRunReceipt> automationHistory,
        DateTime now)
    {
        var suggestions = new List<(DashboardSuggestion Suggestion, int Priority)>();

        if (signals.Uptime is { TotalDays: > 7 } uptime)
        {
            suggestions.Add((new DashboardSuggestion
            {
                Key = "uptime-restart",
                Title = "Restart and clear memory pressure",
                Summary = $"This PC has been running for {(int)Math.Floor(uptime.TotalDays)} day(s). A restart can clear memory pressure and apply waiting Windows changes.",
                Glyph = "\uE777",
                ActionKind = DashboardActionKind.Runbook,
                ActionTargetId = "routine-maintenance-runbook",
                RunButtonLabel = "Run now"
            }, 95));
        }

        if (signals.TempFolderBytes is > 1_073_741_824)
        {
            suggestions.Add((new DashboardSuggestion
            {
                Key = "temp-cleanup",
                Title = "Clear temp file buildup",
                Summary = $"The Windows temp folder is using about {signals.TempFolderBytes.Value / 1_073_741_824d:N1} GB. Cleaning it up can free space and reduce background clutter.",
                Glyph = "\uE74D",
                ActionKind = DashboardActionKind.Fix,
                ActionTargetId = "clear-temp-files",
                RunButtonLabel = "Run now"
            }, 90));
        }

        if (signals.HasPendingWindowsUpdate)
        {
            suggestions.Add((new DashboardSuggestion
            {
                Key = "pending-windows-update",
                Title = "Check Windows Update",
                Summary = "Windows reports pending update work. Clearing that backlog often resolves restart, servicing, and slowdown complaints.",
                Glyph = "\uE895",
                ActionKind = DashboardActionKind.Fix,
                ActionTargetId = "open-windows-update",
                RunButtonLabel = "Open update"
            }, 88));
        }

        if (signals.HasRecentCriticalCrash)
        {
            suggestions.Add((new DashboardSuggestion
            {
                Key = "recent-crash",
                Title = "Review recent crash recovery steps",
                Summary = "A critical crash was recorded recently. FixFox is prioritizing the Windows repair workflow before you try more aggressive changes.",
                Glyph = "\uE814",
                ActionKind = DashboardActionKind.Runbook,
                ActionTargetId = "windows-repair-runbook",
                RunButtonLabel = "Run now"
            }, 92));
        }

        if (signals.EnabledStartupItemCount > 8)
        {
            suggestions.Add((new DashboardSuggestion
            {
                Key = "startup-review",
                Title = "Review startup items",
                Summary = $"{signals.EnabledStartupItemCount} startup items are enabled. Trimming the noisier ones can noticeably improve sign-in time.",
                Glyph = "\uE823",
                ActionKind = DashboardActionKind.Fix,
                ActionTargetId = "manage-startup-programs",
                RunButtonLabel = "Run now"
            }, 84));
        }

        if (signals.HasAutomationRules
            && (!signals.LastAutomationRunUtc.HasValue || now - signals.LastAutomationRunUtc.Value > TimeSpan.FromDays(14)))
        {
            suggestions.Add((new DashboardSuggestion
            {
                Key = "automation-refresh",
                Title = "Run your maintenance bundle",
                Summary = "Automation has not completed a run in over two weeks. Running maintenance now helps FixFox refresh its baseline before issues build up.",
                Glyph = "\uE768",
                ActionKind = DashboardActionKind.Runbook,
                ActionTargetId = "safe-maintenance-runbook",
                RunButtonLabel = "Run now"
            }, 82));
        }

        if (signals.SystemDriveFreePercent is < 15)
        {
            suggestions.Add((new DashboardSuggestion
            {
                Key = "low-disk-space",
                Title = "Free up disk space",
                Summary = $"The system drive is below {signals.SystemDriveFreePercent.Value:N0}% free. Start with safe cleanup before installs, updates, or repairs fail.",
                Glyph = "\uE7F8",
                ActionKind = DashboardActionKind.Runbook,
                ActionTargetId = "disk-full-rescue-runbook",
                RunButtonLabel = "Run now"
            }, 94));
        }

        return suggestions
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Suggestion.Title)
            .Select(item => item.Suggestion)
            .Take(5)
            .ToList();
    }

    private static bool ContainsAny(string left, string right, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (left.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                right.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

public sealed class DashboardSuggestionSignalService : IDashboardSuggestionSignalService
{
    private static readonly TimeSpan SuggestionProbeTimeout = TimeSpan.FromMilliseconds(1800);
    private static readonly TimeSpan SuggestionEvaluationTimeout = TimeSpan.FromMilliseconds(2200);
    private readonly Func<CancellationToken, Task<TimeSpan?>> _uptimeProbe;
    private readonly Func<CancellationToken, Task<long?>> _tempFolderProbe;
    private readonly Func<CancellationToken, Task<bool>> _pendingUpdateProbe;
    private readonly Func<CancellationToken, Task<bool>> _recentCrashProbe;
    private readonly Func<CancellationToken, Task<int>> _startupCountProbe;
    private readonly Func<CancellationToken, Task<double?>> _systemDriveFreePercentProbe;

    public DashboardSuggestionSignalService()
        : this(
            uptimeProbe: _ => Task.FromResult<TimeSpan?>(TimeSpan.FromMilliseconds(Environment.TickCount64)),
            tempFolderProbe: ct => Task.Run<long?>(() => CalculateDirectorySize(Path.GetTempPath(), ct), ct),
            pendingUpdateProbe: _ => Task.FromResult(HasPendingWindowsUpdate()),
            recentCrashProbe: ct => Task.Run(() => HasRecentCriticalCrash(ct), ct),
            startupCountProbe: ct => Task.Run(() => CountEnabledStartupItems(ct), ct),
            systemDriveFreePercentProbe: _ => Task.FromResult(GetSystemDriveFreePercent()))
    {
    }

    internal DashboardSuggestionSignalService(
        Func<CancellationToken, Task<TimeSpan?>> uptimeProbe,
        Func<CancellationToken, Task<long?>> tempFolderProbe,
        Func<CancellationToken, Task<bool>> pendingUpdateProbe,
        Func<CancellationToken, Task<bool>> recentCrashProbe,
        Func<CancellationToken, Task<int>> startupCountProbe,
        Func<CancellationToken, Task<double?>> systemDriveFreePercentProbe)
    {
        _uptimeProbe = uptimeProbe;
        _tempFolderProbe = tempFolderProbe;
        _pendingUpdateProbe = pendingUpdateProbe;
        _recentCrashProbe = recentCrashProbe;
        _startupCountProbe = startupCountProbe;
        _systemDriveFreePercentProbe = systemDriveFreePercentProbe;
    }

    public async Task<DashboardSuggestionSignals> EvaluateAsync(
        IReadOnlyList<AutomationRuleSettings> automationRules,
        IReadOnlyList<AutomationRunReceipt> automationHistory,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(SuggestionEvaluationTimeout);
        var token = timeoutSource.Token;

        var uptimeTask = TryProbeAsync(_uptimeProbe, null, token);
        var tempTask = TryProbeAsync(_tempFolderProbe, null, token);
        var updateTask = TryProbeAsync(_pendingUpdateProbe, false, token);
        var crashTask = TryProbeAsync(_recentCrashProbe, false, token);
        var startupTask = TryProbeAsync(_startupCountProbe, 0, token);
        var freePercentTask = TryProbeAsync(_systemDriveFreePercentProbe, null, token);

        await Task.WhenAll(uptimeTask, tempTask, updateTask, crashTask, startupTask, freePercentTask);

        return new DashboardSuggestionSignals
        {
            Uptime = uptimeTask.Result,
            TempFolderBytes = tempTask.Result,
            HasPendingWindowsUpdate = updateTask.Result,
            HasRecentCriticalCrash = crashTask.Result,
            EnabledStartupItemCount = startupTask.Result,
            HasAutomationRules = automationRules.Any(),
            LastAutomationRunUtc = automationHistory
                .OrderByDescending(entry => entry.FinishedAt)
                .Select(entry => entry.FinishedAt.ToUniversalTime())
                .FirstOrDefault(),
            SystemDriveFreePercent = freePercentTask.Result
        };
    }

    private static async Task<T> TryProbeAsync<T>(
        Func<CancellationToken, Task<T>> probe,
        T fallback,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(SuggestionProbeTimeout);
        var probeTask = probe(timeoutSource.Token);
        var timeoutTask = Task.Delay(SuggestionProbeTimeout, cancellationToken);

        try
        {
            var completedTask = await Task.WhenAny(probeTask, timeoutTask);
            if (!ReferenceEquals(completedTask, probeTask))
            {
                timeoutSource.Cancel();
                return fallback;
            }

            return await probeTask;
        }
        catch (OperationCanceledException)
        {
            return fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static long CalculateDirectorySize(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return 0;

        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return total;
    }

    private static bool HasPendingWindowsUpdate()
    {
        try
        {
            using var updateKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            if (updateKey is not null)
                return true;

            using var cbsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            return cbsKey is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasRecentCriticalCrash(CancellationToken cancellationToken)
    {
        try
        {
            var query = new EventLogQuery("System", PathType.LogName, "*[System[(Level=1 or Level=2)]]")
            {
                ReverseDirection = true
            };

            using var reader = new EventLogReader(query);
            for (var index = 0; index < 40; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var record = reader.ReadEvent();
                if (record is null)
                    break;

                if (record.TimeCreated is DateTime created
                    && created >= DateTime.Now.AddHours(-48))
                    return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static int CountEnabledStartupItems(CancellationToken cancellationToken)
    {
        var count = 0;
        CountRegistryApproved(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", ref count, cancellationToken);
        CountRegistryApproved(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", ref count, cancellationToken);
        CountRegistryApproved(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", ref count, cancellationToken);
        CountRegistryApproved(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32", ref count, cancellationToken);
        CountRegistryApproved(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", ref count, cancellationToken);
        return count;
    }

    private static void CountRegistryApproved(
        RegistryKey root,
        string subKeyPath,
        ref int count,
        CancellationToken cancellationToken)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyPath);
            if (key is null)
                return;

            foreach (var name in key.GetValueNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (key.GetValue(name) is byte[] state && state.Length > 0 && state[0] == 0x02)
                    count++;
            }
        }
        catch
        {
        }
    }

    private static double? GetSystemDriveFreePercent()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrWhiteSpace(root))
                return null;

            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0)
                return null;

            return drive.AvailableFreeSpace / (double)drive.TotalSize * 100d;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class MaintenanceProfileService : IMaintenanceProfileService
{
    public IReadOnlyList<MaintenanceProfileDefinition> Profiles { get; } =
    [
        Profile(
            "quick-clean-profile", "Quick Clean", "Clear temp clutter and free easy space without broader resets.",
            "Low-risk cleanup only.", "Checks free space after cleanup.", SupportActionKind.Runbook, "quick-clean-runbook",
            ["Clear temp files", "Empty Recycle Bin", "Verify free space improved"], supportsScheduling: true, preferIdle: false, avoidOnBattery: false),
        Profile(
            "routine-tune-up-profile", "Routine Tune-Up", "Weekly support workflow for temp files, browser caches, and core security health.",
            "Safe defaults for a normal workstation.", "Verifies the device returns to a healthier steady state.", SupportActionKind.Runbook, "routine-maintenance-runbook",
            ["Clear temp files", "Clear browser caches", "Check Defender", "Check firewall", "Verify steady state"], supportsScheduling: true, preferIdle: true, avoidOnBattery: true),
        Profile(
            "browser-cleanup-profile", "Browser Cleanup", "Rescue stale browser cache, DNS residue, and browser-only web problems.",
            "Avoids broad network resets unless the browser is not the only problem.", "Confirms browsing works without stale cache or DNS.", SupportActionKind.Runbook, "browser-problem-runbook",
            ["Check browser-only scope", "Validate internet", "Flush DNS", "Clear browser caches", "Verify browsing"], supportsScheduling: false, preferIdle: false, avoidOnBattery: false),
        Profile(
            "work-from-home-reset-profile", "Work-From-Home Reset", "Stabilize internet, VPN, Teams cache, and remote-work access basics.",
            "Stops and escalates when auth or policy is the real blocker.", "Verifies the network path before deeper escalation.", SupportActionKind.Runbook, "work-from-home-runbook",
            ["Confirm internet path", "Validate connection", "Flush DNS", "Stabilize VPN", "Clear Teams cache", "Verify remote-work path"], supportsScheduling: false, preferIdle: false, avoidOnBattery: false),
        Profile(
            "meeting-readiness-profile", "Meeting Readiness Check", "Check audio, microphone, and camera in a practical order before a meeting.",
            "Keeps the workflow device-led instead of app-led.", "Confirms meeting devices are visible and available.", SupportActionKind.Runbook, "meeting-device-runbook",
            ["Confirm failing device path", "Restart audio service", "Check microphone access", "Check camera access", "Verify device visibility"], supportsScheduling: false, preferIdle: false, avoidOnBattery: false),
        Profile(
            "safe-maintenance-now-profile", "Safe Maintenance Now", "Run a conservative cleanup and security health pass right now.",
            "No broad stack resets or high-risk system changes.", "Verifies cleanup and core Windows health checks.", SupportActionKind.Runbook, "safe-maintenance-runbook",
            ["Clear temp files", "Empty Recycle Bin", "Check Defender", "Check firewall", "Verify cleanup and security"], supportsScheduling: true, preferIdle: true, avoidOnBattery: true)
    ];

    private static MaintenanceProfileDefinition Profile(
        string id,
        string title,
        string summary,
        string safetyNotes,
        string verificationNotes,
        SupportActionKind kind,
        string targetId,
        List<string> tasks,
        bool supportsScheduling,
        bool preferIdle,
        bool avoidOnBattery) => new()
    {
        Id = id,
        Title = title,
        Summary = summary,
        SafetyNotes = safetyNotes,
        VerificationNotes = verificationNotes,
        IncludedTasks = tasks,
        SupportsScheduling = supportsScheduling,
        PreferIdleWhenScheduled = preferIdle,
        AvoidWhenOnBattery = avoidOnBattery,
        LaunchAction = new SupportAction
        {
            Label = "Run",
            Description = summary,
            Kind = kind,
            TargetId = targetId
        }
    };
}

public sealed class CommandPaletteService : ICommandPaletteService
{
    private readonly IFixCatalogService _catalog;
    private readonly Lazy<IReadOnlyList<SearchSynonymEntry>> _synonyms;
    private static readonly string SynonymConfigPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "search-synonyms.json");

    public CommandPaletteService(IFixCatalogService catalog)
    {
        _catalog = catalog;
        _synonyms = new Lazy<IReadOnlyList<SearchSynonymEntry>>(LoadSynonyms);
    }

    public IReadOnlyList<CommandPaletteItem> Search(
        string query,
        CommandPaletteSearchContext context)
    {
        var allFixes = _catalog.Categories
            .SelectMany(category => category.Fixes)
            .Where(fix => !context.ExcludeAdvancedFixes || fix.RiskLevel != FixRiskLevel.Advanced)
            .ToList();
        var recentUsage = BuildRecentUsageMap(context);

        var items = new List<CommandPaletteItem>();
        items.AddRange(BuildPageItems());
        items.AddRange(allFixes.Select(fix => BuildFixItem(fix, recentUsage)));
        items.AddRange(context.MaintenanceProfiles.Select(BuildMaintenanceProfileItem));
        items.AddRange(context.Runbooks.Select(runbook => BuildRunbookItem(runbook, recentUsage)));
        items.AddRange(context.SupportCenters.Select(BuildSupportCenterItem));
        items.AddRange(context.ToolboxGroups.SelectMany(group => group.Entries.Select(entry => BuildToolboxItem(entry, recentUsage))));
        items.AddRange(context.RecentReceipts.Select(BuildReceiptItem));
        items.AddRange(context.AutomationRules.Select(rule => BuildAutomationRuleItem(rule, recentUsage)));
        items.AddRange(context.AdditionalItems);

        var distinctItems = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.LastUsedUtc).First())
            .ToList();

        if (string.IsNullOrWhiteSpace(query))
        {
            return distinctItems
                .OrderByDescending(DefaultRank)
                .ThenByDescending(item => item.LastUsedUtc)
                .ThenBy(item => item.Title)
                .Take(12)
                .ToList();
        }

        var trimmed = query.Trim();
        var expandedQueryTerms = ExpandQueryTerms(trimmed);

        var ranked = distinctItems
            .Select(item => new
            {
                Item = item,
                Tier = ScoreTier(item, trimmed, expandedQueryTerms)
            })
            .Where(result => result.Tier > 0)
            .Select(result =>
            {
                result.Item.MatchTier = result.Tier;
                return result.Item;
            })
            .OrderBy(item => item.MatchTier)
            .ThenByDescending(item => item.LastUsedUtc)
            .ThenByDescending(DefaultRank)
            .ThenBy(item => item.Title)
            .Take(12)
            .ToList();

        return ShouldGroupResults(trimmed, ranked)
            ? InsertGroupHeaders(ranked)
            : ranked;
    }

    private CommandPaletteItem BuildFixItem(FixItem fix, IReadOnlyDictionary<string, DateTime?> recentUsage) => new()
    {
        Id = $"fix:{fix.Id}",
        Title = fix.Title,
        Subtitle = fix.Description,
        ResultTypeLabel = "Fix",
        Section = $"Fix · {fix.Category}",
        Hint = fix.Type == FixType.Guided ? "Start guided repair" : "Run repair",
        Glyph = fix.Type == FixType.Guided ? "\uE946" : "\uE90F",
        SearchText = $"{fix.Title} {fix.Description} {string.Join(" ", fix.Tags)} {string.Join(" ", fix.Keywords)}",
        SearchTags = BuildTags(fix.Category, fix.Tags, fix.Keywords),
        Kind = CommandPaletteItemKind.Fix,
        TargetId = fix.Id,
        LastUsedUtc = GetLastUsedUtc(recentUsage, fix.Id),
        TooltipText = fix.Description
    };

    private static IEnumerable<CommandPaletteItem> BuildPageItems()
    {
        yield return PageItem("page:home", "Home", "Go to the main command center.", "\uE80F", Page.Dashboard, ["dashboard", "home", "overview"]);
        yield return PageItem("page:diagnosis", "Guided Diagnosis", "Describe an issue and rank the most likely repair paths.", "\uE897", Page.SymptomChecker, ["triage", "diagnosis", "symptoms"]);
        yield return PageItem("page:library", "Repair Library", "Browse and run verified repairs directly.", "\uE90F", Page.Fixes, ["fix center", "fixes", "repairs"]);
        yield return PageItem("page:automation", "Automation", "Open schedules, workflows, and maintenance automation.", "\uE8B1", Page.Bundles, ["bundles", "maintenance", "automation"]);
        yield return PageItem("page:device-health", "Device Health", "Review the baseline and open the right support center.", "\uEC4F", Page.SystemInfo, ["support centers", "health", "system"]);
        yield return PageItem("page:tools", "Windows Tools", "Jump straight into the native Windows utilities FixFox surfaces.", "\uE77B", Page.Toolbox, ["toolbox", "windows tools", "utilities"]);
        yield return PageItem("page:support", "Support Package", "Open escalation tools, support resources, and evidence export.", "\uE9A5", Page.Handoff, ["handoff", "support", "bundle"]);
        yield return PageItem("page:activity", "Activity", "Review repair history, rerun actions, or create support packages.", "\uE81C", Page.History, ["history", "receipts", "activity"]);
        yield return PageItem("page:settings", "Settings", "Adjust startup behavior, profiles, and local data handling.", "\uE713", Page.Settings, ["preferences", "options", "settings"]);
    }

    private static CommandPaletteItem PageItem(string id, string title, string subtitle, string glyph, Page page, string[] tags) => new()
    {
        Id = id,
        Title = title,
        Subtitle = subtitle,
        ResultTypeLabel = "Page",
        Section = "Page",
        Hint = "Open page",
        Glyph = glyph,
        SearchText = $"{title} {subtitle} {string.Join(" ", tags)}",
        SearchTags = tags,
        Kind = CommandPaletteItemKind.Page,
        TargetPage = page,
        TooltipText = subtitle
    };

    private static int DefaultRank(CommandPaletteItem item) => item.Kind switch
    {
        CommandPaletteItemKind.Page when item.TargetPage == Page.Dashboard => 100,
        CommandPaletteItemKind.MaintenanceProfile => 95,
        CommandPaletteItemKind.Page when item.TargetPage == Page.SymptomChecker => 90,
        CommandPaletteItemKind.Page when item.TargetPage == Page.Handoff => 88,
        CommandPaletteItemKind.SupportCenter => 86,
        CommandPaletteItemKind.Runbook => 84,
        CommandPaletteItemKind.Toolbox => 80,
        CommandPaletteItemKind.Fix => 70,
        CommandPaletteItemKind.Setting => 72,
        CommandPaletteItemKind.AutomationRule => 74,
        CommandPaletteItemKind.Receipt => 60,
        _ => 50
    };

    private int ScoreTier(CommandPaletteItem item, string query, IReadOnlySet<string> expandedTerms)
    {
        if (string.Equals(item.Title, query, StringComparison.OrdinalIgnoreCase))
            return 1;

        if (item.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 2;

        if (ContainsWord(item.Title, query))
            return 3;

        if (ContainsWord(item.Subtitle, query) || ContainsWord(item.SearchText, query))
            return 4;

        if (expandedTerms.Count > 0 && item.SearchTags.Any(tag => expandedTerms.Contains(tag)))
            return item.Kind == CommandPaletteItemKind.Receipt ? 6 : 5;

        if (item.Kind == CommandPaletteItemKind.Receipt
            && (ContainsWord(item.Title, query) || ContainsWord(item.Subtitle, query) || ContainsWord(item.SearchText, query)))
            return 6;

        return 0;
    }

    private IReadOnlySet<string> ExpandQueryTerms(string query)
    {
        var terms = query
            .Split([' ', '\t', '-', '/', ',', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => term.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in _synonyms.Value)
        {
            if (!entry.Terms.Any(term => terms.Contains(term)))
                continue;

            foreach (var tag in entry.Tags)
                terms.Add(tag);
        }

        return terms;
    }

    private static bool ContainsWord(string source, string query)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(query))
            return false;

        return source.Contains(query, StringComparison.OrdinalIgnoreCase)
            || source.Split([' ', '\t', '-', '/', ',', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(word => word.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldGroupResults(string query, IReadOnlyList<CommandPaletteItem> results)
        => query.Length <= 12
           && results.Count > 3
           && results.Select(item => item.ResultTypeLabel).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;

    private static IReadOnlyList<CommandPaletteItem> InsertGroupHeaders(IReadOnlyList<CommandPaletteItem> ranked)
    {
        var grouped = new List<CommandPaletteItem>();
        foreach (var group in ranked.GroupBy(item => item.ResultTypeLabel))
        {
            grouped.Add(new CommandPaletteItem
            {
                Id = $"group:{group.Key}",
                Title = $"{group.Key}s ({group.Count()})",
                ResultTypeLabel = group.Key,
                GroupKey = group.Key,
                GroupTitle = group.Key,
                IsGroupHeader = true
            });
            grouped.AddRange(group);
        }

        return grouped;
    }

    private static IReadOnlyDictionary<string, DateTime?> BuildRecentUsageMap(CommandPaletteSearchContext context)
    {
        var recentUsage = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);

        foreach (var fix in context.RecentFixes)
            recentUsage[fix.Id] = DateTime.UtcNow;

        foreach (var receipt in context.RecentReceipts)
        {
            var timestamp = receipt.Timestamp.ToUniversalTime();
            if (!string.IsNullOrWhiteSpace(receipt.FixId))
                recentUsage[receipt.FixId] = timestamp;
            if (!string.IsNullOrWhiteSpace(receipt.RunbookId))
                recentUsage[receipt.RunbookId] = timestamp;
        }

        foreach (var rule in context.AutomationRules.Where(rule => rule.LastPinnedAtUtc.HasValue))
            recentUsage[rule.Id] = rule.LastPinnedAtUtc;

        return recentUsage;
    }

    private static DateTime? GetLastUsedUtc(IReadOnlyDictionary<string, DateTime?> map, string key)
        => map.TryGetValue(key, out var value) ? value : null;

    private static string[] BuildTags(string category, params IEnumerable<string>[] sources)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizeCategoryTag(category)
        };

        foreach (var source in sources)
        {
            foreach (var value in source.Where(value => !string.IsNullOrWhiteSpace(value)))
                tags.Add(value.Trim().ToLowerInvariant());
        }

        return tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray();
    }

    private static string NormalizeCategoryTag(string category)
    {
        var normalized = category.ToLowerInvariant();
        if (normalized.Contains("network"))
            return "network";
        if (normalized.Contains("dns") || normalized.Contains("browser"))
            return "browser";
        if (normalized.Contains("performance") || normalized.Contains("startup"))
            return "performance";
        if (normalized.Contains("audio"))
            return "audio";
        if (normalized.Contains("display"))
            return "display";
        if (normalized.Contains("update"))
            return "updates";
        if (normalized.Contains("storage"))
            return "storage";
        if (normalized.Contains("security"))
            return "security";
        if (normalized.Contains("crash") || normalized.Contains("bsod"))
            return "bsod";
        if (normalized.Contains("device"))
            return "devices";
        if (normalized.Contains("printer"))
            return "printer";
        if (normalized.Contains("app"))
            return "apps";
        if (normalized.Contains("office"))
            return "outlook";
        if (normalized.Contains("vpn") || normalized.Contains("remote"))
            return "vpn";
        if (normalized.Contains("power"))
            return "power";
        if (normalized.Contains("account") || normalized.Contains("sign"))
            return "accounts";
        return normalized.Replace('&', ' ').Trim();
    }

    private static CommandPaletteItem BuildMaintenanceProfileItem(MaintenanceProfileDefinition profile) => new()
    {
        Id = $"profile:{profile.Id}",
        Title = profile.Title,
        Subtitle = profile.Summary,
        ResultTypeLabel = "Maintenance",
        Section = "Maintenance profile",
        Hint = "Run profile",
        Glyph = "\uE768",
        SearchText = $"{profile.Title} {profile.Summary} {profile.SafetyNotes} {profile.VerificationNotes}",
        SearchTags = ["maintenance", "profile"],
        Kind = CommandPaletteItemKind.MaintenanceProfile,
        TargetId = profile.Id,
        TooltipText = profile.Summary
    };

    private static CommandPaletteItem BuildRunbookItem(RunbookDefinition runbook, IReadOnlyDictionary<string, DateTime?> recentUsage) => new()
    {
        Id = $"runbook:{runbook.Id}",
        Title = runbook.Title,
        Subtitle = runbook.Description,
        ResultTypeLabel = "Runbook",
        Section = $"Runbook · {runbook.CategoryId}",
        Hint = "Run flow",
        Glyph = "\uE7C4",
        SearchText = $"{runbook.Title} {runbook.Description} {runbook.CategoryId} {runbook.TriggerHint}",
        SearchTags = BuildTags(runbook.CategoryId, [runbook.TriggerHint]),
        Kind = CommandPaletteItemKind.Runbook,
        TargetId = runbook.Id,
        LastUsedUtc = GetLastUsedUtc(recentUsage, runbook.Id),
        TooltipText = runbook.Description
    };

    private static CommandPaletteItem BuildSupportCenterItem(SupportCenterDefinition center) => new()
    {
        Id = $"support:{center.Id}",
        Title = center.Title,
        Subtitle = center.Summary,
        ResultTypeLabel = "Page",
        Section = "Support center",
        Hint = center.PrimaryAction.Label,
        Glyph = "\uE9CE",
        SearchText = $"{center.Title} {center.Summary} {center.StatusText} {string.Join(" ", center.Highlights)}",
        SearchTags = BuildTags(center.Title, center.Highlights),
        Kind = CommandPaletteItemKind.SupportCenter,
        TargetId = center.Id,
        TooltipText = center.Summary
    };

    private static CommandPaletteItem BuildToolboxItem(ToolboxEntry entry, IReadOnlyDictionary<string, DateTime?> recentUsage) => new()
    {
        Id = $"tool:{entry.ToolKey}",
        Title = entry.Title,
        Subtitle = entry.Description,
        ResultTypeLabel = "Tool",
        Section = "Windows tool",
        Hint = "Open tool",
        Glyph = "\uE77B",
        SearchText = $"{entry.Title} {entry.Description} {entry.SupportNote}",
        SearchTags = BuildTags("toolbox", [entry.Title, entry.Description]),
        Kind = CommandPaletteItemKind.Toolbox,
        TargetId = entry.Title,
        LastUsedUtc = GetLastUsedUtc(recentUsage, entry.ToolKey),
        TooltipText = entry.Description
    };

    private static CommandPaletteItem BuildReceiptItem(RepairHistoryEntry receipt) => new()
    {
        Id = $"receipt:{receipt.Id}",
        Title = string.IsNullOrWhiteSpace(receipt.FixTitle) ? "Recent receipt" : receipt.FixTitle,
        Subtitle = $"Receipt · {receipt.Outcome} · {receipt.Timestamp:g}",
        ResultTypeLabel = "Recent",
        Section = "Recent receipt",
        Hint = "Open activity",
        Glyph = "\uE81C",
        SearchText = $"{receipt.FixTitle} {receipt.Outcome} {receipt.Timestamp:g} {receipt.VerificationSummary} {receipt.ChangedSummary}",
        SearchTags = BuildTags(receipt.CategoryName, [receipt.Outcome.ToString()]),
        Kind = CommandPaletteItemKind.Receipt,
        TargetId = receipt.Id,
        LastUsedUtc = receipt.Timestamp.ToUniversalTime(),
        TooltipText = receipt.ChangedSummary
    };

    private static CommandPaletteItem BuildAutomationRuleItem(AutomationRuleSettings rule, IReadOnlyDictionary<string, DateTime?> recentUsage) => new()
    {
        Id = $"automation:{rule.Id}",
        Title = rule.Title,
        Subtitle = rule.Summary,
        ResultTypeLabel = "Automation",
        Section = "Automation rule",
        Hint = "Open automation",
        Glyph = "\uE8B1",
        SearchText = $"{rule.Title} {rule.Summary} {rule.RecurrenceSummary} {rule.ScheduleExpression}",
        SearchTags = BuildTags("automation", rule.IncludedTasks),
        Kind = CommandPaletteItemKind.AutomationRule,
        TargetId = rule.Id,
        LastUsedUtc = GetLastUsedUtc(recentUsage, rule.Id),
        TooltipText = rule.Summary
    };

    private static IReadOnlyList<SearchSynonymEntry> LoadSynonyms()
    {
        try
        {
            if (!File.Exists(SynonymConfigPath))
                return [];

            var document = JsonConvert.DeserializeObject<SearchSynonymDocument>(File.ReadAllText(SynonymConfigPath));
            return document?.Entries
                       .Where(entry => entry.Terms.Count > 0 && entry.Tags.Count > 0)
                       .Select(entry => new SearchSynonymEntry
                       {
                           Terms = entry.Terms.Select(term => term.Trim().ToLowerInvariant()).Distinct().ToList(),
                           Tags = entry.Tags.Select(tag => tag.Trim().ToLowerInvariant()).Distinct().ToList()
                       })
                       .ToList()
                   ?? [];
        }
        catch
        {
            return [];
        }
    }
}

public sealed class SupportCenterService : ISupportCenterService
{
    public IReadOnlyList<SupportCenterDefinition> BuildCenters(
        SystemSnapshot? snapshot,
        IReadOnlyList<InstalledProgram> installedPrograms,
        IReadOnlyList<RepairHistoryEntry> receiptHistory)
    {
        var centers = new List<SupportCenterDefinition>
        {
            new()
            {
                Id = "storage-center",
                Title = "Storage Center",
                Summary = "Clear safe clutter, review disk pressure, and jump straight into Windows storage controls.",
                StatusText = snapshot is null
                    ? "Load a device snapshot to review storage pressure."
                    : $"{snapshot.DiskFreeGb:N0} GB free on C: with {snapshot.DiskUsedPct:N0}% used.",
                Highlights =
                [
                    "Run temp cleanup, recycle bin cleanup, and browser cache cleanup.",
                    "Use Storage settings and Disk Cleanup for deeper Windows-native cleanup.",
                    GetDownloadsSignal()
                ],
                PrimaryAction = Action("Run Disk Full Rescue", "Clears safe clutter before broader cleanup.", SupportActionKind.Runbook, "disk-full-rescue-runbook"),
                SecondaryAction = Action("Open Storage", "Jump directly into Storage and Storage Sense.", SupportActionKind.Toolbox, "Storage Settings")
            },
            new()
            {
                Id = "startup-center",
                Title = "Startup & Background Apps",
                Summary = "Find boot pressure, noisy startup items, and routes into Startup Apps and Task Manager.",
                StatusText = snapshot is null
                    ? "Load a device snapshot to review uptime and memory pressure."
                    : $"Uptime {snapshot.Uptime}. Memory currently at {snapshot.RamUsedPct:N0}%.",
                Highlights =
                [
                    "Audit startup programs before disabling anything.",
                    "Route into Startup Apps and Task Manager when boot feels heavy.",
                    "Use the slow PC workflow if the whole device feels sluggish."
                ],
                PrimaryAction = Action("Run Slow PC Recovery", "Checks startup pressure first, then cleanup.", SupportActionKind.Runbook, "slow-pc-runbook"),
                SecondaryAction = Action("Open Startup Apps", "Review what launches at sign-in.", SupportActionKind.Toolbox, "Startup Apps")
            },
            new()
            {
                Id = "software-center",
                Title = "Software & App Center",
                Summary = "Review installed apps, uninstall flows, broken associations, and common Office or Teams issues.",
                StatusText = installedPrograms.Count == 0
                    ? "Load installed apps to review software state."
                    : $"{installedPrograms.Count} installed app(s) available for review.",
                Highlights =
                [
                    "Use Installed Apps for uninstall, repair, and reset entry points.",
                    "Route Outlook, Teams, and Office issues into targeted guided repair flows.",
                    $"Microsoft apps detected: {CountMatches(installedPrograms, "Microsoft", "Teams", "Outlook", "Office")}."
                ],
                PrimaryAction = Action("Open Installed Apps", "Jump into Apps & Features.", SupportActionKind.Toolbox, "Installed Apps"),
                SecondaryAction = Action("Repair Outlook Profile", "Use the guided Outlook repair flow.", SupportActionKind.Fix, "repair-outlook-profile")
            },
            new()
            {
                Id = "browser-center",
                Title = "Browser & Web Apps Center",
                Summary = "Separate browser-only issues from wider internet failures, then clear stale browser state safely.",
                StatusText = receiptHistory.Any(entry => entry.FixTitle.Contains("browser", StringComparison.OrdinalIgnoreCase))
                    ? "Browser-related receipts were already recorded on this device."
                    : "Use this when websites fail but Windows itself looks normal.",
                Highlights =
                [
                    "Clear browser caches without immediately resetting the whole network stack.",
                    "Check default browser, time drift, proxy issues, and browser-only failures.",
                    "Escalate cert or account failures instead of over-fixing locally."
                ],
                PrimaryAction = Action("Run Browser Recovery", "Start with browser-safe cleanup and validation.", SupportActionKind.Runbook, "browser-problem-runbook"),
                SecondaryAction = Action("Open Default Apps", "Check browser and file associations.", SupportActionKind.Toolbox, "Default Apps")
            },
            new()
            {
                Id = "network-center",
                Title = "Network, VPN & Remote Work",
                Summary = "Check DNS, proxy, gateway, and VPN-aware rescue flows before escalating remote access issues.",
                StatusText = snapshot is null
                    ? "Load a device snapshot to review network context."
                    : $"{(snapshot.InternetReachable ? "Internet reachable" : "Internet not confirmed")} over {snapshot.NetworkType}.",
                Highlights =
                [
                    "Differentiate browser-only issues from wider network failure.",
                    "Use VPN-aware flows before treating work-resource access like a local PC issue.",
                    "Jump directly into Wi-Fi, VPN, and Network Adapters."
                ],
                PrimaryAction = Action("Run Internet Recovery", "Start with DNS and reachability checks.", SupportActionKind.Runbook, "internet-recovery-runbook"),
                SecondaryAction = Action("Open VPN", "Jump directly into VPN settings.", SupportActionKind.Toolbox, "VPN")
            },
            new()
            {
                Id = "windows-repair-center",
                Title = "Windows Repair & Recovery",
                Summary = "Use Windows-native servicing, update repair, and recovery routes when the OS itself is unstable.",
                StatusText = snapshot is null
                    ? "Load a device snapshot to review update and reboot state."
                    : $"{snapshot.PendingUpdateCount} pending update(s) detected.",
                Highlights =
                [
                    "Start with update health before DISM or recovery options.",
                    "Use restore point and rollback awareness before broad repair.",
                    "Route to Recovery when servicing repair still leaves Windows unstable."
                ],
                PrimaryAction = Action("Run Windows Repair Recovery", "Use the Windows-native repair sequence.", SupportActionKind.Runbook, "windows-repair-runbook"),
                SecondaryAction = Action("Open Recovery", "Jump straight into Windows recovery options.", SupportActionKind.Toolbox, "Recovery Options")
            },
            new()
            {
                Id = "devices-center",
                Title = "Devices & Meetings",
                Summary = "Handle audio, microphone, camera, display, dock, Bluetooth, USB, and printing issues from one place.",
                StatusText = "Use meeting rescue for audio/camera paths and printing rescue for spooler or queue failures.",
                Highlights =
                [
                    "Route into sound, microphone privacy, camera privacy, display, Bluetooth, and printers.",
                    "Use guided repairs for Bluetooth, USB, microphone, webcam, and display issues.",
                    "Keep meeting-device and printing rescue flows separate."
                ],
                PrimaryAction = Action("Run Meeting Readiness", "Check meeting devices in a sensible order.", SupportActionKind.Runbook, "meeting-device-runbook"),
                SecondaryAction = Action("Run Printing Rescue", "Reset spooler and queue health before escalating.", SupportActionKind.Runbook, "printing-rescue-runbook")
            },
            new()
            {
                Id = "files-center",
                Title = "Files, Shares & Work Resources",
                Summary = "Distinguish access-denied from path, VPN, DNS, or credential problems before sending the user to IT.",
                StatusText = receiptHistory.Any(entry => entry.FixTitle.Contains("vpn", StringComparison.OrdinalIgnoreCase) || entry.ChangedSummary.Contains("share", StringComparison.OrdinalIgnoreCase))
                    ? "Recent remote-work receipts suggest this device has already seen share or VPN issues."
                    : "Best used when drives, shares, or internal resources are missing or inaccessible.",
                Highlights =
                [
                    "Use Credential Manager when a saved sign-in or share credential may be stale.",
                    "Treat access denied differently from connectivity failure.",
                    "Use work-from-home rescue before escalating mapped-drive or internal-resource problems."
                ],
                PrimaryAction = Action("Run Work-From-Home Access", "Check the internet path, VPN, and remote-work basics first.", SupportActionKind.Runbook, "work-from-home-runbook"),
                SecondaryAction = Action("Open Credential Manager", "Review saved credentials for work resources.", SupportActionKind.Toolbox, "Credential Manager")
            }
        };

        return centers;
    }

    private static string GetDownloadsSignal()
    {
        try
        {
            var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(downloads))
                return "Downloads folder is not available on this profile.";

            var files = new DirectoryInfo(downloads).EnumerateFiles("*", SearchOption.TopDirectoryOnly).OrderByDescending(file => file.Length).Take(3).ToList();
            if (files.Count == 0)
                return "Downloads folder is currently light.";

            var largest = files[0].Length / 1_048_576d;
            return $"Largest current Downloads item: {files[0].Name} ({largest:N0} MB).";
        }
        catch
        {
            return "Downloads folder size could not be inspected automatically.";
        }
    }

    private static int CountMatches(IReadOnlyList<InstalledProgram> installedPrograms, params string[] needles) =>
        installedPrograms.Count(program => needles.Any(needle =>
            program.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || program.Publisher.Contains(needle, StringComparison.OrdinalIgnoreCase)));

    private static SupportAction Action(string label, string description, SupportActionKind kind, string targetId) => new()
    {
        Label = label,
        Description = description,
        Kind = kind,
        TargetId = targetId
    };
}
