using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class DashboardWorkspaceServiceTests
{
    [Fact]
    public void BuildAlerts_FlagsLowDiskPendingUpdatesAndInterruptedRepair()
    {
        var service = new DashboardWorkspaceService();
        var alerts = service.BuildAlerts(
            new SystemSnapshot
            {
                DiskFreeGb = 4,
                DiskUsedPct = 96,
                PendingUpdateCount = 3,
                DefenderEnabled = false,
                HasBattery = false
            },
            new HealthCheckReport { OverallScore = 82, Summary = "Stable" },
            null,
            new InterruptedOperationState { DisplayTitle = "Windows Repair Recovery", Summary = "Windows repair stopped mid-run." },
            new[]
            {
                new RepairHistoryEntry { Success = false }
            });

        Assert.Contains(alerts, alert => string.Equals(alert.ActionTargetId, "clear-temp-files", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(alerts, alert => string.Equals(alert.ActionTargetId, "open-windows-update", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(alerts, alert => alert.ActionPage == Page.Handoff);
        Assert.Contains(alerts, alert => alert.ActionKind == DashboardActionKind.Runbook || alert.ActionKind == DashboardActionKind.Fix || alert.ActionKind == DashboardActionKind.Page);
    }

    [Fact]
    public void RecommendRunbooks_PrefersNetworkAndMaintenanceWorkflows()
    {
        var service = new DashboardWorkspaceService();
        IReadOnlyList<RunbookDefinition> runbooks =
        [
            new RunbookDefinition { Id = "internet-recovery-runbook", Title = "Internet Recovery" },
            new RunbookDefinition { Id = "routine-maintenance-runbook", Title = "Routine Maintenance" },
            new RunbookDefinition { Id = "slow-pc-runbook", Title = "Slow PC Recovery" },
            new RunbookDefinition { Id = "meeting-device-runbook", Title = "Meeting Device Recovery" }
        ];

        var results = service.RecommendRunbooks(
            new SystemSnapshot { PendingUpdateCount = 1, DiskFreeGb = 6, NetworkType = "Wi-Fi", InternetReachable = false },
            new[] { new ScanResult { Title = "No internet access", Detail = "Device cannot reach the internet.", Severity = ScanSeverity.Critical } },
            new[] { new RepairHistoryEntry { CategoryName = "network", FixTitle = "Network reset", ChangedSummary = "network still failing", Success = false } },
            runbooks);

        Assert.Equal("internet-recovery-runbook", results[0].Id);
        Assert.Contains(results, runbook => runbook.Id == "routine-maintenance-runbook" || runbook.Id == "slow-pc-runbook");
    }
}
