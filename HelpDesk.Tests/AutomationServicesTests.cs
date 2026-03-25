using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class AutomationServicesTests
{
    [Fact]
    public void EnsureRules_AppliesWorkLaptopDefaultsAndPreservesSavedScheduleChoices()
    {
        var settings = new AppSettings
        {
            BehaviorProfile = "Work Laptop",
            AutomationRules =
            [
                new AutomationRuleSettings
                {
                    Id = "safe-maintenance",
                    Enabled = true,
                    ScheduleKind = AutomationScheduleKind.Daily,
                    ScheduleTime = "07:45",
                    RunOnlyWhenIdle = false,
                    SkipOnBattery = false
                }
            ]
        };

        var service = CreateService();
        var rules = service.EnsureRules(settings);

        var quickHealth = Assert.Single(rules, rule => rule.Id == "quick-health-check");
        var startupCheck = Assert.Single(rules, rule => rule.Id == "startup-quick-check");
        var workFromHome = Assert.Single(rules, rule => rule.Id == "work-from-home-readiness");
        var safeMaintenance = Assert.Single(rules, rule => rule.Id == "safe-maintenance");

        Assert.Equal(AutomationScheduleKind.Daily, quickHealth.ScheduleKind);
        Assert.Equal(AutomationScheduleKind.Startup, startupCheck.ScheduleKind);
        Assert.True(workFromHome.Enabled);
        Assert.Equal(AutomationScheduleKind.Weekly, workFromHome.ScheduleKind);

        Assert.Equal(AutomationScheduleKind.Daily, safeMaintenance.ScheduleKind);
        Assert.Equal("07:45", safeMaintenance.ScheduleTime);
        Assert.False(safeMaintenance.RunOnlyWhenIdle);
        Assert.False(safeMaintenance.SkipOnBattery);
    }

    [Fact]
    public void EvaluateRule_RespectsPauseQuietHoursAndRecentSuccessfulRuns()
    {
        var settings = new AppSettings
        {
            AutomationPausedUntilUtc = DateTime.UtcNow.AddHours(2),
            AutomationQuietHoursStart = "22:00",
            AutomationQuietHoursEnd = "07:00"
        };

        var rule = new AutomationRuleSettings
        {
            Id = "quick-health-check",
            Title = "Scheduled Quick Health Check",
            Enabled = true,
            ScheduleKind = AutomationScheduleKind.Daily,
            SkipDuringQuietHours = true
        };

        var history = new List<AutomationRunReceipt>
        {
            new()
            {
                RuleId = "quick-health-check",
                RuleTitle = "Scheduled Quick Health Check",
                Outcome = AutomationRunOutcome.Completed,
                StartedAt = new DateTime(2026, 3, 24, 5, 30, 0),
                FinishedAt = new DateTime(2026, 3, 24, 5, 31, 0),
                Summary = "Completed quietly."
            }
        };

        var service = CreateService();
        var evaluation = service.EvaluateRule(
            rule,
            settings,
            snapshot: null,
            hasActiveWork: false,
            history,
            new DateTime(2026, 3, 24, 6, 0, 0));

        Assert.False(evaluation.CanRun);
        Assert.True(evaluation.WasSkipped);
        Assert.Contains(evaluation.Reasons, reason => reason.Contains("paused", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(evaluation.Reasons, reason => reason.Contains("quiet hours", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(evaluation.Reasons, reason => reason.Contains("recently", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_QuickHealthCreatesReceiptAndStaysQuietWhenNoIssuesAreFound()
    {
        var settings = new AppSettings
        {
            BehaviorProfile = "Standard",
            ShowNotifications = true,
            NotificationMode = "Standard"
        };

        var history = new FakeAutomationHistoryService();
        var notifications = new FakeNotificationService();
        var service = CreateService(
            settingsService: new FakeSettingsService { Settings = settings },
            quickScanService: new FakeQuickScanService
            {
                Results =
                [
                    new ScanResult { Title = "Everything looks steady", Detail = "No issues", Severity = ScanSeverity.Good }
                ]
            },
            automationHistoryService: history,
            notificationService: notifications);

        var receipt = await service.RunAsync("quick-health-check", "Scheduled", manualOverride: false);

        Assert.Equal(AutomationRunOutcome.Completed, receipt.Outcome);
        Assert.Contains("found no issues", receipt.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(history.Entries, entry => entry.RuleId == "quick-health-check");
        Assert.Empty(notifications.All);
    }

    [Fact]
    public async Task RunAsync_BlockedAutomationCreatesReceiptAndNotificationWhenConfigured()
    {
        var settings = new AppSettings
        {
            BehaviorProfile = "Standard",
            ShowNotifications = true,
            NotificationMode = "Standard"
        };

        var service = CreateService(
            settingsService: new FakeSettingsService { Settings = settings },
            notificationService: new FakeNotificationService());

        var rules = service.EnsureRules(settings).ToList();
        var safeMaintenance = Assert.Single(rules, rule => rule.Id == "safe-maintenance");
        safeMaintenance.NotifyOnSkippedOrBlocked = true;
        safeMaintenance.SkipIfActiveRepairSession = true;
        settings.AutomationRules = rules;

        var notifications = new FakeNotificationService();
        service = CreateService(
            settingsService: new FakeSettingsService { Settings = settings },
            notificationService: notifications);

        var receipt = await service.RunAsync("safe-maintenance", "Scheduled", manualOverride: false, hasActiveWork: true);

        Assert.Equal(AutomationRunOutcome.Blocked, receipt.Outcome);
        Assert.Contains("blocked", receipt.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Single(notifications.All);
        Assert.Contains("active work", receipt.ConditionSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_InterruptedRepairWatcherLeavesAttentionReceipt()
    {
        var history = new FakeAutomationHistoryService();
        var state = new FakeStatePersistenceService();
        state.Save(new InterruptedOperationState
        {
            OperationType = "guided",
            OperationTargetId = "repair-outlook-profile",
            DisplayTitle = "Repair Outlook Profile",
            CurrentStepId = "step-2",
            StartedAt = DateTime.Now.AddMinutes(-20),
            Summary = "Interrupted Outlook profile repair is waiting to resume.",
            Outcome = ExecutionOutcome.Interrupted,
            CanResume = true
        });

        var service = CreateService(
            statePersistenceService: state,
            automationHistoryService: history);

        var receipt = await service.RunAsync("interrupted-repair-watcher", "Heartbeat");

        Assert.Equal(AutomationRunOutcome.Completed, receipt.Outcome);
        Assert.True(receipt.UserActionRequired);
        Assert.Contains("interrupted", receipt.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("fixfox://page/automation", receipt.RelatedSupportCenterId);
        Assert.Contains(history.Entries, entry => entry.RuleId == "interrupted-repair-watcher");
    }

    [Fact]
    public void PopulateRuntimeDetails_RepeatedFailureWatcherFlagsAttention()
    {
        var service = CreateService();
        var settings = new AppSettings { BehaviorProfile = "Standard" };
        var watcher = service.EnsureRules(settings).First(rule => rule.Id == "repeated-failure-watcher");

        service.PopulateRuntimeDetails(
            watcher,
            settings,
            snapshot: null,
            healthReport: null,
            interrupted: null,
            repairHistory:
            [
                new RepairHistoryEntry { FixId = "flush-dns", FixTitle = "Flush DNS", Success = false },
                new RepairHistoryEntry { FixId = "flush-dns", FixTitle = "Flush DNS", Success = false }
            ],
            automationHistory: [],
            hasActiveWork: false,
            now: DateTime.Now);

        Assert.True(watcher.NeedsAttention);
        Assert.Equal(ScanSeverity.Warning, watcher.Severity);
        Assert.Contains("failed repeatedly", watcher.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("support package", watcher.ConditionSummary, StringComparison.OrdinalIgnoreCase);
    }

    private static AutomationCoordinatorService CreateService(
        ISettingsService? settingsService = null,
        IDeploymentConfigurationService? deploymentConfigurationService = null,
        IQuickScanService? quickScanService = null,
        IRunbookCatalogService? runbookCatalogService = null,
        IRunbookExecutionService? runbookExecutionService = null,
        IAutomationHistoryService? automationHistoryService = null,
        IRepairHistoryService? repairHistoryService = null,
        IStatePersistenceService? statePersistenceService = null,
        ISystemInfoService? systemInfoService = null,
        INotificationService? notificationService = null,
        IAppLogger? logger = null)
    {
        return new AutomationCoordinatorService(
            settingsService ?? new FakeSettingsService(),
            deploymentConfigurationService ?? new FakeDeploymentConfigurationService(),
            quickScanService ?? new FakeQuickScanService(),
            runbookCatalogService ?? new FakeRunbookCatalogService
            {
                Runbooks =
                [
                    new RunbookDefinition { Id = "safe-maintenance-runbook", Title = "Safe Maintenance", Steps = [new RunbookStepDefinition { Id = "step-1", Title = "Clean temp files", StepKind = RunbookStepKind.Diagnostic }] },
                    new RunbookDefinition { Id = "browser-problem-runbook", Title = "Browser Rescue", Steps = [new RunbookStepDefinition { Id = "step-1", Title = "Check browser path", StepKind = RunbookStepKind.Diagnostic }] },
                    new RunbookDefinition { Id = "work-from-home-runbook", Title = "Work-From-Home Rescue", Steps = [new RunbookStepDefinition { Id = "step-1", Title = "Check remote path", StepKind = RunbookStepKind.Diagnostic }] },
                    new RunbookDefinition { Id = "meeting-device-runbook", Title = "Meeting Readiness", Steps = [new RunbookStepDefinition { Id = "step-1", Title = "Check meeting devices", StepKind = RunbookStepKind.Diagnostic }] },
                    new RunbookDefinition { Id = "disk-full-rescue-runbook", Title = "Disk Full Rescue", Steps = [new RunbookStepDefinition { Id = "step-1", Title = "Review large files", StepKind = RunbookStepKind.Diagnostic }] },
                    new RunbookDefinition { Id = "printing-rescue-runbook", Title = "Printing Rescue", Steps = [new RunbookStepDefinition { Id = "step-1", Title = "Check spooler", StepKind = RunbookStepKind.Diagnostic }] }
                ]
            },
            runbookExecutionService ?? new FakeRunbookExecutionService(),
            automationHistoryService ?? new FakeAutomationHistoryService(),
            repairHistoryService ?? new FakeRepairHistoryService(),
            statePersistenceService ?? new FakeStatePersistenceService(),
            systemInfoService ?? new FakeSystemInfoService(),
            notificationService ?? new FakeNotificationService(),
            logger ?? new FakeAppLogger());
    }
}
