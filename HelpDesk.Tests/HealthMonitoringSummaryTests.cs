using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class HealthMonitoringSummaryTests
{
    [Fact]
    public void IsSummaryDueToday_Returns_True_When_Last_Summary_Was_Eight_Days_Ago()
    {
        var settings = new FakeSettingsService
        {
            Settings = new AppSettings
            {
                SendWeeklyHealthSummary = true,
                LastWeeklyHealthSummaryUtc = DateTime.UtcNow.AddDays(-8)
            }
        };

        var service = CreateWeeklySummaryService(settings: settings);

        Assert.True(service.IsSummaryDueToday());
    }

    [Fact]
    public void IsSummaryDueToday_Returns_False_When_Last_Summary_Was_Three_Days_Ago()
    {
        var settings = new FakeSettingsService
        {
            Settings = new AppSettings
            {
                SendWeeklyHealthSummary = true,
                LastWeeklyHealthSummaryUtc = DateTime.UtcNow.AddDays(-3)
            }
        };

        var service = CreateWeeklySummaryService(settings: settings);

        Assert.False(service.IsSummaryDueToday());
    }

    [Fact]
    public void WeeklySummary_Generates_HealthScore_A_When_No_Alerts_No_Crashes_And_All_Automations_Succeed()
    {
        var repairHistory = new FakeRepairHistoryService();
        var automationHistory = new FakeAutomationHistoryService();
        automationHistory.Record(new AutomationRunReceipt
        {
            RuleId = "safe-maintenance",
            RuleTitle = "Safe maintenance",
            StartedAt = DateTime.UtcNow.AddDays(-1),
            Outcome = AutomationRunOutcome.Completed
        });

        var service = CreateWeeklySummaryService(repairHistory, automationHistory, new FakeHealthAlertHistoryService(), new FakeSettingsService());

        var summary = service.Generate();

        Assert.Equal("A", summary.HealthScore);
    }

    [Fact]
    public void WeeklySummary_Generates_HealthScore_D_When_One_Critical_Alert_Exists()
    {
        var alerts = new FakeHealthAlertHistoryService();
        alerts.Record(new HealthAlert
        {
            Id = "recent-crash-detected",
            Title = "Your PC crashed recently",
            Severity = AlertSeverity.Critical,
            DetectedUtc = DateTime.UtcNow.AddHours(-2)
        });

        var service = CreateWeeklySummaryService(alertHistory: alerts);

        var summary = service.Generate();

        Assert.Equal("D", summary.HealthScore);
    }

    [Fact]
    public void WeeklySummary_With_No_Fixes_And_No_Alerts_Generates_Without_Throwing()
    {
        var service = CreateWeeklySummaryService();

        var summary = service.Generate();

        Assert.NotNull(summary);
        Assert.Equal(0, summary.FixesRunCount);
        Assert.Equal(0, summary.AlertsRaisedCount);
    }

    private static WeeklySummaryService CreateWeeklySummaryService(
        FakeRepairHistoryService? repairHistory = null,
        FakeAutomationHistoryService? automationHistory = null,
        FakeHealthAlertHistoryService? alertHistory = null,
        FakeSettingsService? settings = null)
    {
        return new WeeklySummaryService(
            repairHistory ?? new FakeRepairHistoryService(),
            automationHistory ?? new FakeAutomationHistoryService(),
            alertHistory ?? new FakeHealthAlertHistoryService(),
            settings ?? new FakeSettingsService
            {
                Settings = new AppSettings
                {
                    SendWeeklyHealthSummary = true
                }
            });
    }
}
