using System.Diagnostics;
using System.IO;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;

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
        Description = description,
        LaunchTarget = target,
        LaunchArguments = arguments,
        MinimumEdition = minimumEdition,
        RequiresAdvancedMode = requiresAdvancedMode,
        RequiredCapability = requiredCapability,
        SupportNote = supportNote
    };
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
                Summary = $"{healthReport.OverallScore}/100 — {healthReport.Summary}",
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

    public CommandPaletteService(IFixCatalogService catalog)
    {
        _catalog = catalog;
    }

    public IReadOnlyList<CommandPaletteItem> Search(
        string query,
        IReadOnlyList<FixItem> pinnedFixes,
        IReadOnlyList<FixItem> favoriteFixes,
        IReadOnlyList<FixItem> recentFixes,
        IReadOnlyList<RunbookDefinition> runbooks,
        IReadOnlyList<MaintenanceProfileDefinition> maintenanceProfiles,
        IReadOnlyList<SupportCenterDefinition> supportCenters,
        IReadOnlyList<ToolboxGroup> toolboxGroups)
    {
        var items = new List<CommandPaletteItem>();

        items.AddRange(BuildPageItems());
        items.AddRange(maintenanceProfiles.Select(profile => new CommandPaletteItem
        {
            Id = profile.Id,
            Title = profile.Title,
            Subtitle = profile.Summary,
            Section = "Maintenance Profiles",
            Hint = "Run profile",
            Glyph = "\uE768",
            SearchText = $"{profile.Title} {profile.Summary} {profile.SafetyNotes} {profile.VerificationNotes}",
            Kind = CommandPaletteItemKind.MaintenanceProfile,
            TargetId = profile.Id
        }));
        items.AddRange(runbooks.Select(runbook => new CommandPaletteItem
        {
            Id = runbook.Id,
            Title = runbook.Title,
            Subtitle = runbook.Description,
            Section = "Workflows",
            Hint = "Start workflow",
            Glyph = "\uE7C4",
            SearchText = $"{runbook.Title} {runbook.Description} {runbook.CategoryId} {runbook.TriggerHint}",
            Kind = CommandPaletteItemKind.Runbook,
            TargetId = runbook.Id
        }));
        items.AddRange(supportCenters.Select(center => new CommandPaletteItem
        {
            Id = center.Id,
            Title = center.Title,
            Subtitle = center.Summary,
            Section = "Support Centers",
            Hint = center.PrimaryAction.Label,
            Glyph = "\uE9CE",
            SearchText = $"{center.Title} {center.Summary} {center.StatusText} {string.Join(" ", center.Highlights)}",
            Kind = CommandPaletteItemKind.SupportCenter,
            TargetId = center.Id
        }));
        items.AddRange(toolboxGroups.SelectMany(group => group.Entries.Select(entry => new CommandPaletteItem
        {
            Id = $"toolbox-{entry.Title}",
            Title = entry.Title,
            Subtitle = entry.Description,
            Section = "Windows Tools",
            Hint = "Open tool",
            Glyph = "\uE77B",
            SearchText = $"{entry.Title} {entry.Description}",
            Kind = CommandPaletteItemKind.Toolbox,
            TargetId = entry.Title
        })));

        var spotlightFixes = pinnedFixes
            .Concat(favoriteFixes)
            .Concat(recentFixes)
            .DistinctBy(fix => fix.Id)
            .Select(BuildFixItem);

        items.AddRange(spotlightFixes);

        if (!string.IsNullOrWhiteSpace(query))
        {
            items.AddRange(_catalog.Search(query)
                .Select(BuildFixItem));
        }

        var allItems = items
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (string.IsNullOrWhiteSpace(query))
        {
            return allItems
                .OrderByDescending(item => DefaultRank(item))
                .ThenBy(item => item.Title)
                .Take(10)
                .ToList();
        }

        var trimmed = query.Trim();
        return allItems
            .Where(item => Matches(item, trimmed))
            .OrderByDescending(item => Score(item, trimmed))
            .ThenBy(item => item.Title)
            .Take(12)
            .ToList();
    }

    private CommandPaletteItem BuildFixItem(FixItem fix) => new()
    {
        Id = fix.Id,
        Title = fix.Title,
        Subtitle = fix.Description,
        Section = "Repairs",
        Hint = fix.Type == FixType.Guided ? "Start guided repair" : "Run repair",
        Glyph = fix.Type == FixType.Guided ? "\uE946" : "\uE90F",
        SearchText = $"{fix.Title} {fix.Description} {string.Join(" ", fix.Tags)} {string.Join(" ", fix.Keywords)}",
        Kind = CommandPaletteItemKind.Fix,
        TargetId = fix.Id
    };

    private static IEnumerable<CommandPaletteItem> BuildPageItems()
    {
        yield return PageItem("page-home", "Home", "Go to the main command center.", "Navigation", "Open page", "\uE80F", Page.Dashboard);
        yield return PageItem("page-diagnosis", "Guided Diagnosis", "Describe an issue and rank the most likely repair paths.", "Navigation", "Open page", "\uE897", Page.SymptomChecker);
        yield return PageItem("page-library", "Repair Library", "Browse and run verified repairs directly.", "Navigation", "Open page", "\uE90F", Page.Fixes);
        yield return PageItem("page-automation", "Automation", "Open maintenance profiles and guided workflows.", "Navigation", "Open page", "\uE8B1", Page.Bundles);
        yield return PageItem("page-device-health", "Device Health", "Review the baseline and open the right support center.", "Navigation", "Open page", "\uEC4F", Page.SystemInfo);
        yield return PageItem("page-tools", "Windows Tools", "Jump straight into the native Windows utilities FixFox surfaces.", "Navigation", "Open page", "\uE77B", Page.Toolbox);
        yield return PageItem("page-support", "Support Package", "Open escalation tools, support resources, and evidence export.", "Navigation", "Open page", "\uE9A5", Page.Handoff);
        yield return PageItem("page-activity", "Activity", "Review repair history, rerun actions, or create support packages.", "Navigation", "Open page", "\uE81C", Page.History);
        yield return PageItem("page-settings", "Settings", "Adjust startup behavior, profiles, and local data handling.", "Navigation", "Open page", "\uE713", Page.Settings);
    }

    private static CommandPaletteItem PageItem(string id, string title, string subtitle, string section, string hint, string glyph, Page page) => new()
    {
        Id = id,
        Title = title,
        Subtitle = subtitle,
        Section = section,
        Hint = hint,
        Glyph = glyph,
        SearchText = $"{title} {subtitle}",
        Kind = CommandPaletteItemKind.Page,
        TargetPage = page
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
        _ => 50
    };

    private static bool Matches(CommandPaletteItem item, string query) =>
        item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || item.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase)
        || item.Section.Contains(query, StringComparison.OrdinalIgnoreCase)
        || item.Hint.Contains(query, StringComparison.OrdinalIgnoreCase)
        || item.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static int Score(CommandPaletteItem item, string query)
    {
        var score = DefaultRank(item);
        if (item.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 40;
        if (item.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            score += 25;
        if (item.Section.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 10;
        if (item.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 15;

        return score;
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
