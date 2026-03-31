using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Management;
using System.Xml.Linq;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using Microsoft.Win32;
using Newtonsoft.Json;
using SharedConstants = HelpDesk.Shared.Constants;

namespace HelpDesk.Infrastructure.Services;

public sealed class ShellPresenceService : IShellPresenceService
{
    private DateTime _lastAppOpenUtc = DateTime.UtcNow;

    public bool IsTrayActive { get; private set; }
    public DateTime LastAppOpenUtc => _lastAppOpenUtc;

    public void SetTrayActive(bool isTrayActive) => IsTrayActive = isTrayActive;

    public void MarkAppOpened()
    {
        _lastAppOpenUtc = DateTime.UtcNow;
        IsTrayActive = false;
    }
}

public sealed class HealthAlertHistoryService : IHealthAlertHistoryService
{
    private const int MaxEntries = 500;
    private static readonly string FilePath = Path.Combine(SharedConstants.AppDataDir, "health-alert-history.json");
    private readonly List<HealthAlertHistoryEntry> _entries;

    public HealthAlertHistoryService()
    {
        _entries = LoadEntries();
    }

    public IReadOnlyList<HealthAlertHistoryEntry> Entries => _entries.AsReadOnly();

    public void Record(HealthAlert alert)
    {
        _entries.Insert(0, new HealthAlertHistoryEntry
        {
            AlertId = alert.Id,
            Title = alert.Title,
            Severity = alert.Severity,
            DetectedUtc = alert.DetectedUtc
        });

        while (_entries.Count > MaxEntries)
            _entries.RemoveAt(_entries.Count - 1);

        Persist();
    }

    private static List<HealthAlertHistoryEntry> LoadEntries()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonConvert.DeserializeObject<List<HealthAlertHistoryEntry>>(File.ReadAllText(FilePath)) ?? [];
        }
        catch
        {
        }

        return [];
    }

    private void Persist()
    {
        Directory.CreateDirectory(SharedConstants.AppDataDir);
        File.WriteAllText(FilePath, JsonConvert.SerializeObject(_entries, Formatting.Indented));
    }
}

public sealed class WeeklySummaryService : IWeeklySummaryService
{
    private readonly IRepairHistoryService _repairHistory;
    private readonly IAutomationHistoryService _automationHistory;
    private readonly IHealthAlertHistoryService _alertHistory;
    private readonly ISettingsService _settingsService;

    public WeeklySummaryService(
        IRepairHistoryService repairHistory,
        IAutomationHistoryService automationHistory,
        IHealthAlertHistoryService alertHistory,
        ISettingsService settingsService)
    {
        _repairHistory = repairHistory;
        _automationHistory = automationHistory;
        _alertHistory = alertHistory;
        _settingsService = settingsService;
    }

    public bool IsSummaryDueToday()
    {
        var settings = _settingsService.Load();
        var now = DateTime.UtcNow;

        if (!settings.SendWeeklyHealthSummary)
            return false;

        if (!settings.LastWeeklyHealthSummaryUtc.HasValue)
            return true;

        return now - settings.LastWeeklyHealthSummaryUtc.Value >= TimeSpan.FromDays(7);
    }

    public WeeklySummary Generate()
    {
        var now = DateTime.UtcNow;
        var weekStart = now.AddDays(-7);
        var recentFixes = _repairHistory.Entries.Where(entry => entry.Timestamp.ToUniversalTime() >= weekStart && !entry.IsWeeklySummary).ToList();
        var recentAlerts = _alertHistory.Entries.Where(entry => entry.DetectedUtc >= weekStart).ToList();
        var recentAutomation = _automationHistory.Entries.Where(entry => entry.StartedAt.ToUniversalTime() >= weekStart).ToList();
        var previousSummary = _repairHistory.Entries
            .Where(entry => entry.IsWeeklySummary && entry.WeeklySummary is not null)
            .OrderByDescending(entry => entry.Timestamp)
            .Skip(0)
            .FirstOrDefault(entry => entry.Timestamp.ToUniversalTime() < weekStart);

        var warningCount = recentAlerts.Count(entry => entry.Severity == AlertSeverity.Warning);
        var criticalCount = recentAlerts.Count(entry => entry.Severity == AlertSeverity.Critical);
        var crashCount = recentAlerts.Count(entry => string.Equals(entry.AlertId, "recent-crash-detected", StringComparison.OrdinalIgnoreCase));
        var automationFailures = recentAutomation.Count(entry => entry.Outcome is AutomationRunOutcome.Failed or AutomationRunOutcome.Blocked);
        var automationSkips = recentAutomation.Count(entry => entry.Outcome == AutomationRunOutcome.Skipped);
        var automationCompleted = recentAutomation.Count(entry => entry.Outcome == AutomationRunOutcome.Completed);

        var healthScore = crashCount > 0 || criticalCount > 0
            ? "D"
            : warningCount >= 2 || automationFailures >= 2
                ? "C"
                : warningCount == 1 || automationFailures == 1
                    ? "B"
                    : "A";

        var notableEvents = new List<string>();
        if (criticalCount > 0)
            notableEvents.Add($"{criticalCount} critical alert(s) were raised.");
        if (crashCount > 0)
            notableEvents.Add($"{crashCount} unexpected shutdown or bugcheck event(s) were detected.");
        if (automationFailures > 0)
            notableEvents.Add($"{automationFailures} automation run(s) failed or were blocked.");
        if (notableEvents.Count == 0)
            notableEvents.Add("No critical health events stood out this week.");

        var summaryText = recentFixes.Count == 0 && recentAlerts.Count == 0 && recentAutomation.Count == 0
            ? "FixFox did not record any repairs, alerts, or automation activity this week."
            : $"FixFox recorded {recentFixes.Count} repair receipt(s), {recentAlerts.Count} alert(s), and {recentAutomation.Count} automation run(s) this week.";

        var comparedToLastWeekText = BuildDeltaSummary(previousSummary?.WeeklySummary, recentFixes.Count, recentAlerts.Count, automationFailures + automationSkips);

        return new WeeklySummary
        {
            WeekEndingUtc = now,
            FixesRunCount = recentFixes.Count,
            FixesRunNames = recentFixes.Select(entry => entry.FixTitle).Where(title => !string.IsNullOrWhiteSpace(title)).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList(),
            AlertsRaisedCount = recentAlerts.Count,
            AlertTypes = recentAlerts.Select(entry => entry.Title).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList(),
            AutomationsCompletedCount = automationCompleted,
            AutomationsSkippedCount = automationSkips,
            AutomationsFailedCount = automationFailures,
            CrashCount = crashCount,
            NotableEvents = notableEvents,
            HealthScore = healthScore,
            SummaryText = summaryText,
            ComparedToLastWeekText = comparedToLastWeekText
        };
    }

    public void Save(WeeklySummary summary)
    {
        var settings = _settingsService.Load();
        var receipt = new RepairHistoryEntry
        {
            Timestamp = summary.WeekEndingUtc.ToLocalTime(),
            CategoryId = "weekly-health-summary",
            CategoryName = "Health History",
            FixTitle = "Weekly health summary",
            Outcome = ExecutionOutcome.Completed,
            Success = summary.HealthScore is "A" or "B",
            VerificationPassed = true,
            Notes = summary.SummaryText,
            TriggerSource = "Background monitor",
            PreStateSummary = $"Week ending {summary.WeekEndingUtc.ToLocalTime():yyyy-MM-dd}",
            PostStateSummary = $"Health score {summary.HealthScore}",
            VerificationSummary = $"Alerts: {summary.AlertsRaisedCount} | Crashes: {summary.CrashCount}",
            NextStep = summary.NotableEvents.FirstOrDefault() ?? "No follow-up is needed right now.",
            ChangedSummary = summary.SummaryText,
            ReceiptKind = ReceiptKind.WeeklySummary,
            WeeklySummary = summary
        };

        _repairHistory.Record(receipt);
        settings.LastWeeklyHealthSummaryUtc = summary.WeekEndingUtc;
        _settingsService.Save(settings);
    }

    private static string BuildDeltaSummary(WeeklySummary? previousSummary, int fixCount, int alertCount, int automationIssues)
    {
        if (previousSummary is null)
            return "This is your first weekly health summary.";

        var fixDelta = fixCount - previousSummary.FixesRunCount;
        var alertDelta = alertCount - previousSummary.AlertsRaisedCount;
        var automationDelta = automationIssues - (previousSummary.AutomationsFailedCount + previousSummary.AutomationsSkippedCount);

        return $"Compared to last week: {FormatDelta(fixDelta, "repair")}, {FormatDelta(alertDelta, "alert")}, {FormatDelta(automationDelta, "automation issue")}.";
    }

    private static string FormatDelta(int delta, string noun)
    {
        if (delta == 0)
            return $"no change in {noun}s";

        var magnitude = Math.Abs(delta);
        return delta > 0
            ? $"{magnitude} more {noun}{(magnitude == 1 ? "" : "s")}"
            : $"{magnitude} fewer {noun}{(magnitude == 1 ? "" : "s")}";
    }
}

public sealed class HealthMonitorService : IHealthMonitorService
{
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);
    private readonly ISettingsService _settingsService;
    private readonly IHealthAlertHistoryService _alertHistory;
    private readonly IShellPresenceService _shellPresence;
    private readonly IAppLogger _logger;
    private readonly object _syncRoot = new();
    private readonly HashSet<string> _raisedThisSession = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HealthAlert> _activeAlerts = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private Task? _runnerTask;

    public HealthMonitorService(
        ISettingsService settingsService,
        IHealthAlertHistoryService alertHistory,
        IShellPresenceService shellPresence,
        IAppLogger logger)
    {
        _settingsService = settingsService;
        _alertHistory = alertHistory;
        _shellPresence = shellPresence;
        _logger = logger;
    }

    public event EventHandler<HealthAlert>? AlertRaised;
    public event EventHandler? AlertsChanged;

    public Task StartAsync(CancellationToken ct)
    {
        lock (_syncRoot)
        {
            if (_runnerTask is { IsCompleted: false })
                return Task.CompletedTask;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _runnerTask = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public void Stop()
    {
        var hadAlerts = false;
        lock (_syncRoot)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _runnerTask = null;
            hadAlerts = _activeAlerts.Count > 0;
            _activeAlerts.Clear();
        }

        if (hadAlerts)
            AlertsChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<HealthAlert> GetActiveAlerts()
    {
        lock (_syncRoot)
            return _activeAlerts.Values.Select(CloneAlert).OrderByDescending(alert => alert.Severity).ThenBy(alert => alert.Title).ToList();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await EvaluateAsync(runWithoutTrayRequirement: true, cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(MonitorInterval, cancellationToken).ConfigureAwait(false);
                if (_shellPresence.IsTrayActive)
                    await EvaluateAsync(runWithoutTrayRequirement: false, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("Background health monitor loop failed", ex);
            }
        }
    }

    private async Task EvaluateAsync(bool runWithoutTrayRequirement, CancellationToken cancellationToken)
    {
        var settings = _settingsService.Load();
        var now = DateTime.UtcNow;

        if (TrimExpiredDismissals(settings, now))
            _settingsService.Save(settings);

        if (!settings.EnableBackgroundHealthMonitoring)
        {
            ClearActiveAlerts();
            return;
        }

        if (!runWithoutTrayRequirement && !_shellPresence.IsTrayActive)
            return;

        var checks = BuildCheckTasks(settings, cancellationToken);
        var results = await Task.WhenAll(checks).ConfigureAwait(false);
        var nextAlerts = results.Where(alert => alert is not null)
            .Select(alert => ApplyDismissedState(alert!, settings, now))
            .ToDictionary(alert => alert.Id, alert => alert, StringComparer.OrdinalIgnoreCase);

        List<HealthAlert> newlyRaised = [];
        var changed = false;

        lock (_syncRoot)
        {
            var clearedIds = _activeAlerts.Keys.Except(nextAlerts.Keys, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var clearedId in clearedIds)
            {
                _activeAlerts.Remove(clearedId);
                changed = true;
            }

            foreach (var next in nextAlerts.Values)
            {
                if (_activeAlerts.TryGetValue(next.Id, out var existing))
                {
                    var updated = new HealthAlert
                    {
                        Id = next.Id,
                        Title = next.Title,
                        Body = next.Body,
                        Severity = next.Severity,
                        ActionLabel = next.ActionLabel,
                        ActionTarget = next.ActionTarget,
                        DetectedUtc = existing.DetectedUtc,
                        IsDismissed = next.IsDismissed
                    };
                    if (!AreAlertsEquivalent(existing, updated))
                        changed = true;

                    _activeAlerts[next.Id] = updated;
                }
                else
                {
                    _activeAlerts[next.Id] = next;
                    changed = true;
                    if (_raisedThisSession.Add(next.Id))
                    {
                        newlyRaised.Add(CloneAlert(next));
                        _alertHistory.Record(next);
                    }
                }
            }
        }

        foreach (var alert in newlyRaised)
            AlertRaised?.Invoke(this, alert);

        if (changed || newlyRaised.Count > 0)
            AlertsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static HealthAlert CloneAlert(HealthAlert alert) => new()
    {
        Id = alert.Id,
        Title = alert.Title,
        Body = alert.Body,
        Severity = alert.Severity,
        ActionLabel = alert.ActionLabel,
        ActionTarget = alert.ActionTarget,
        DetectedUtc = alert.DetectedUtc,
        IsDismissed = alert.IsDismissed
    };

    private static bool AreAlertsEquivalent(HealthAlert left, HealthAlert right)
        => string.Equals(left.Title, right.Title, StringComparison.Ordinal)
           && string.Equals(left.Body, right.Body, StringComparison.Ordinal)
           && left.Severity == right.Severity
           && string.Equals(left.ActionLabel, right.ActionLabel, StringComparison.Ordinal)
           && string.Equals(left.ActionTarget, right.ActionTarget, StringComparison.Ordinal)
           && left.IsDismissed == right.IsDismissed
           && left.DetectedUtc == right.DetectedUtc;

    private static HealthAlert ApplyDismissedState(HealthAlert alert, AppSettings settings, DateTime now)
    {
        var isDismissed = settings.DismissedHealthAlerts.Any(entry =>
            string.Equals(entry.AlertId, alert.Id, StringComparison.OrdinalIgnoreCase)
            && entry.DismissedUntilUtc > now);

        return new HealthAlert
        {
            Id = alert.Id,
            Title = alert.Title,
            Body = alert.Body,
            Severity = alert.Severity,
            ActionLabel = alert.ActionLabel,
            ActionTarget = alert.ActionTarget,
            DetectedUtc = alert.DetectedUtc,
            IsDismissed = isDismissed
        };
    }

    private static bool TrimExpiredDismissals(AppSettings settings, DateTime now)
    {
        var before = settings.DismissedHealthAlerts.Count;
        settings.DismissedHealthAlerts.RemoveAll(entry => entry.DismissedUntilUtc <= now || string.IsNullOrWhiteSpace(entry.AlertId));
        return before != settings.DismissedHealthAlerts.Count;
    }

    private void ClearActiveAlerts()
    {
        lock (_syncRoot)
        {
            if (_activeAlerts.Count == 0)
                return;

            _activeAlerts.Clear();
        }

        AlertsChanged?.Invoke(this, EventArgs.Empty);
    }

    private IEnumerable<Task<HealthAlert?>> BuildCheckTasks(AppSettings settings, CancellationToken cancellationToken)
    {
        if (settings.HealthAlertLowDiskEnabled)
            yield return RunCheckAsync("low-disk-space", CheckLowDiskSpaceAsync, cancellationToken);
        if (settings.HealthAlertWindowsUpdateEnabled)
            yield return RunCheckAsync("windows-update-overdue", CheckWindowsUpdateOverdueAsync, cancellationToken);
        if (settings.HealthAlertDefenderDefinitionsEnabled)
            yield return RunCheckAsync("defender-definitions", CheckDefenderDefinitionsAsync, cancellationToken);
        if (settings.HealthAlertPendingRebootEnabled)
            yield return RunCheckAsync("pending-reboot", CheckPendingRebootAsync, cancellationToken);
        if (settings.HealthAlertCrashDetectionEnabled)
            yield return RunCheckAsync("recent-crash", CheckRecentCrashAsync, cancellationToken);
        if (settings.HealthAlertStartupSlowdownEnabled)
            yield return RunCheckAsync("startup-slowdown", ct => CheckStartupSlowdownAsync(settings, ct), cancellationToken);
    }

    private async Task<HealthAlert?> RunCheckAsync(string checkName, Func<CancellationToken, Task<HealthAlert?>> check, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(CheckTimeout);
            var worker = Task.Run(async () => await check(timeoutSource.Token).ConfigureAwait(false), CancellationToken.None);
            return await worker.WaitAsync(CheckTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Background health check '{checkName}' failed: {ex.Message}");
            return null;
        }
    }

    private static Task<HealthAlert?> CheckLowDiskSpaceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var drive = new DriveInfo(systemRoot);
        if (!drive.IsReady)
            return Task.FromResult<HealthAlert?>(null);

        var freeGb = drive.AvailableFreeSpace / 1_073_741_824d;
        var freePercent = drive.TotalSize == 0 ? 100 : drive.AvailableFreeSpace / (double)drive.TotalSize * 100d;
        if (freeGb >= 5 && freePercent >= 10)
            return Task.FromResult<HealthAlert?>(null);

        var severity = freeGb < 5 ? AlertSeverity.Critical : AlertSeverity.Warning;
        return Task.FromResult<HealthAlert?>(new HealthAlert
        {
            Id = "low-disk-space",
            Title = $"Low disk space on {drive.Name.TrimEnd('\\')}",
            Body = $"Your system drive has {freeGb:N1} GB free. This can cause slowness and prevent Windows Update from installing.",
            Severity = severity,
            ActionLabel = "Free up space",
            ActionTarget = "clear-temp-files",
            DetectedUtc = DateTime.UtcNow
        });
    }

    private static Task<HealthAlert?> CheckWindowsUpdateOverdueAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Install");
        var raw = key?.GetValue("LastSuccessTime")?.ToString();
        if (string.IsNullOrWhiteSpace(raw) || !DateTime.TryParse(raw, out var lastSuccessLocal))
            return Task.FromResult<HealthAlert?>(null);

        var days = (int)Math.Floor((DateTime.Now - lastSuccessLocal).TotalDays);
        if (days <= 30)
            return Task.FromResult<HealthAlert?>(null);

        return Task.FromResult<HealthAlert?>(new HealthAlert
        {
            Id = "windows-update-overdue",
            Title = "Windows updates are overdue",
            Body = $"No updates have been installed in {days} days.",
            Severity = AlertSeverity.Warning,
            ActionLabel = "Check for updates",
            ActionTarget = "ms-settings:windowsupdate",
            DetectedUtc = DateTime.UtcNow
        });
    }

    private static Task<HealthAlert?> CheckDefenderDefinitionsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var query = new ManagementObjectSearcher(@"root\Microsoft\Windows\Defender", "SELECT AntivirusSignatureAge FROM MSFT_MpComputerStatus");
            foreach (ManagementObject result in query.Get())
            {
                var age = Convert.ToInt32(result["AntivirusSignatureAge"] ?? 0);
                if (age <= 3)
                    return Task.FromResult<HealthAlert?>(null);

                return Task.FromResult<HealthAlert?>(new HealthAlert
                {
                    Id = "defender-definitions-stale",
                    Title = "Security definitions are out of date",
                    Body = $"Defender definitions haven't updated in {age} days.",
                    Severity = AlertSeverity.Warning,
                    ActionLabel = "Update now",
                    ActionTarget = "update-virus-definitions",
                    DetectedUtc = DateTime.UtcNow
                });
            }
        }
        catch
        {
        }

        return Task.FromResult<HealthAlert?>(null);
    }

    private static Task<HealthAlert?> CheckPendingRebootAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hasPendingReboot =
            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") is not null
            || Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") is not null;

        if (!hasPendingReboot)
            return Task.FromResult<HealthAlert?>(null);

        return Task.FromResult<HealthAlert?>(new HealthAlert
        {
            Id = "pending-reboot",
            Title = "A restart is needed",
            Body = "Windows is waiting for a restart to finish installing updates.",
            Severity = AlertSeverity.Info,
            ActionLabel = null,
            ActionTarget = null,
            DetectedUtc = DateTime.UtcNow
        });
    }

    private static Task<HealthAlert?> CheckRecentCrashAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = new EventLogQuery(
            "System",
            PathType.LogName,
            "*[System[(EventID=41 or EventID=1001) and TimeCreated[timediff(@SystemTime) <= 86400000]]]");

        var crashCount = 0;
        using var reader = new EventLogReader(query);
        while (reader.ReadEvent() is { } record)
        {
            cancellationToken.ThrowIfCancellationRequested();
            crashCount++;
            record.Dispose();
        }

        if (crashCount == 0)
            return Task.FromResult<HealthAlert?>(null);

        return Task.FromResult<HealthAlert?>(new HealthAlert
        {
            Id = "recent-crash-detected",
            Title = "Your PC crashed recently",
            Body = $"FixFox detected {crashCount} unexpected shutdown(s) in the last 24 hours.",
            Severity = crashCount >= 2 ? AlertSeverity.Critical : AlertSeverity.Warning,
            ActionLabel = "Investigate",
            ActionTarget = "post-crash-triage-bundle",
            DetectedUtc = DateTime.UtcNow
        });
    }

    private Task<HealthAlert?> CheckStartupSlowdownAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bootTimeMs = ReadLatestBootTimeMilliseconds(cancellationToken);
        if (!bootTimeMs.HasValue || bootTimeMs.Value <= 0)
            return Task.FromResult<HealthAlert?>(null);

        if (settings.StartupBaselineMilliseconds <= 0)
        {
            settings.StartupBaselineMilliseconds = bootTimeMs.Value;
            _settingsService.Save(settings);
            return Task.FromResult<HealthAlert?>(null);
        }

        if (bootTimeMs.Value <= settings.StartupBaselineMilliseconds * 1.5)
            return Task.FromResult<HealthAlert?>(null);

        return Task.FromResult<HealthAlert?>(new HealthAlert
        {
            Id = "startup-slower-than-baseline",
            Title = "Startup is taking longer than usual",
            Body = $"Your PC is taking {bootTimeMs.Value / 1000d:N1}s to start, up from {settings.StartupBaselineMilliseconds / 1000d:N1}s.",
            Severity = AlertSeverity.Info,
            ActionLabel = "Review startup items",
            ActionTarget = Page.SystemInfo.ToString(),
            DetectedUtc = DateTime.UtcNow
        });
    }

    private static long? ReadLatestBootTimeMilliseconds(CancellationToken cancellationToken)
    {
        var query = new EventLogQuery(
            "Microsoft-Windows-Diagnostics-Performance/Operational",
            PathType.LogName,
            "*[System[(EventID=100)]]");

        using var reader = new EventLogReader(query);
        using var record = reader.ReadEvent();
        if (record is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        var xml = XDocument.Parse(record.ToXml());
        var bootTimeValue = xml.Descendants("Data")
            .FirstOrDefault(node => string.Equals(node.Attribute("Name")?.Value, "BootTime", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return long.TryParse(bootTimeValue, out var bootTime) ? bootTime : null;
    }
}
