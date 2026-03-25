using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class RunbookAndEvidenceTests
{
    [Fact]
    public async Task RunbookExecutionService_ExecutesRepairStepsAndClearsState()
    {
        var catalog = new FakeFixCatalogService
        {
            Categories =
            [
                new FixCategory
                {
                    Id = "network",
                    Title = "Network",
                    Fixes =
                    [
                        new FixItem { Id = "flush-dns", Title = "Flush DNS", Description = "Fix DNS" },
                        new FixItem { Id = "renew-ip", Title = "Renew IP", Description = "Renew IP" }
                    ]
                }
            ]
        };

        var execution = new FakeRepairExecutionService();
        var state = new FakeStatePersistenceService();
        var history = new FakeRepairHistoryService();
        var service = new RunbookExecutionService(catalog, execution, state, history, new FakeEditionCapabilityService());

        var summary = await service.ExecuteAsync(new RunbookDefinition
        {
            Id = "network-runbook",
            Title = "Network Runbook",
            CategoryId = "network",
            Steps =
            [
                new RunbookStepDefinition { Id = "step-0", Title = "Confirm scope", StepKind = RunbookStepKind.Message, PostStepMessage = "workflow note" },
                new RunbookStepDefinition { Id = "step-1", Title = "Flush", StepKind = RunbookStepKind.Repair, LinkedRepairId = "flush-dns" },
                new RunbookStepDefinition { Id = "step-2", Title = "Renew", StepKind = RunbookStepKind.Repair, LinkedRepairId = "renew-ip" }
            ]
        });

        Assert.True(summary.Success);
        Assert.Equal(3, summary.CompletedSteps);
        Assert.Equal(new[] { "flush-dns", "renew-ip" }, execution.ExecutedFixIds);
        Assert.Null(state.Load());
        Assert.Contains(history.Entries, entry => entry.RunbookId == "network-runbook");
        Assert.Contains(summary.Timeline, item => item.Contains("Confirm scope", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunbookExecutionService_StopsOnFailedRepairAndKeepsTimeline()
    {
        var catalog = new FakeFixCatalogService
        {
            Categories =
            [
                new FixCategory
                {
                    Id = "network",
                    Title = "Network",
                    Fixes =
                    [
                        new FixItem { Id = "flush-dns", Title = "Flush DNS", Description = "Fix DNS" },
                        new FixItem { Id = "renew-ip", Title = "Renew IP", Description = "Renew IP" }
                    ]
                }
            ]
        };

        var execution = new FakeRepairExecutionService();
        execution.ResultsByFixId["flush-dns"] = new RepairExecutionResult
        {
            FixId = "flush-dns",
            FixTitle = "Flush DNS",
            Success = false,
            Summary = "Flush DNS did not resolve the issue.",
            FailureSummary = "DNS still failed.",
            Verification = new VerificationResult { Status = VerificationStatus.Failed, Summary = "DNS still failed." },
            Rollback = new RollbackInfo { IsAvailable = false, Summary = "none" }
        };

        var service = new RunbookExecutionService(catalog, execution, new FakeStatePersistenceService(), new FakeRepairHistoryService(), new FakeEditionCapabilityService());
        var summary = await service.ExecuteAsync(new RunbookDefinition
        {
            Id = "network-runbook",
            Title = "Network Runbook",
            CategoryId = "network",
            Steps =
            [
                new RunbookStepDefinition { Id = "step-1", Title = "Flush", StepKind = RunbookStepKind.Repair, LinkedRepairId = "flush-dns", StopOnFailure = true },
                new RunbookStepDefinition { Id = "step-2", Title = "Renew", StepKind = RunbookStepKind.Repair, LinkedRepairId = "renew-ip", StopOnFailure = true }
            ]
        });

        Assert.False(summary.Success);
        Assert.Equal(1, summary.CompletedSteps);
        Assert.Single(summary.RepairResults);
        Assert.Contains("Flush", summary.Summary);
        Assert.DoesNotContain("renew-ip", execution.ExecutedFixIds);
    }

    [Fact]
    public async Task RunbookExecutionService_BlocksProOnlyRunbookForBasicEdition()
    {
        var catalog = new FakeFixCatalogService
        {
            Categories =
            [
                new FixCategory
                {
                    Id = "network",
                    Title = "Network",
                    Fixes =
                    [
                        new FixItem { Id = "flush-dns", Title = "Flush DNS", Description = "Fix DNS" }
                    ]
                }
            ]
        };

        var history = new FakeRepairHistoryService();
        var service = new RunbookExecutionService(
            catalog,
            new FakeRepairExecutionService(),
            new FakeStatePersistenceService(),
            history,
            new FakeEditionCapabilityService
            {
                Snapshot = new EditionCapabilitySnapshot
                {
                    Edition = AppEdition.Basic,
                    Runbooks = CapabilityState.Available
                }
            });

        var summary = await service.ExecuteAsync(new RunbookDefinition
        {
            Id = "advanced-network-runbook",
            Title = "Advanced Network Recovery",
            CategoryId = "network",
            MinimumEdition = AppEdition.Pro,
            Steps =
            [
                new RunbookStepDefinition { Id = "step-1", Title = "Flush", StepKind = RunbookStepKind.Repair, LinkedRepairId = "flush-dns" }
            ]
        });

        Assert.False(summary.Success);
        Assert.Equal("advanced-network-runbook", summary.RunbookId);
        Assert.Contains("FixFox Pro", summary.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(history.Entries, entry => entry.RunbookId == "advanced-network-runbook" && !entry.Success);
    }

    [Fact]
    public async Task EvidenceBundleService_RedactsMachineAndUserNames()
    {
        var history = new FakeRepairHistoryService();
        history.Record(new RepairHistoryEntry { FixId = "flush-dns", FixTitle = "Flush DNS", Success = false });

        var notifications = new FakeNotificationService();
        notifications.Add(new AppNotification { Title = "Network warning", Message = "No internet" });

        var log = new FakeLogService();
        var systemInfo = new FakeSystemInfoService();
        var edition = new FakeEditionCapabilityService();
        var automationHistory = new FakeAutomationHistoryService();
        automationHistory.Record(new AutomationRunReceipt
        {
            RuleId = "quick-health-check",
            RuleTitle = "Quick health check",
            Outcome = AutomationRunOutcome.Completed,
            TriggerSource = "Scheduled",
            StartedAt = DateTime.Now.AddMinutes(-30),
            FinishedAt = DateTime.Now.AddMinutes(-29),
            Summary = "Found 1 issue that still needs attention.",
            VerificationSummary = "Review recommended",
            NextStep = "Open Home to review the result."
        });
        var service = new EvidenceBundleService(
            history,
            automationHistory,
            notifications,
            log,
            systemInfo,
            edition,
            new FakeBrandingConfigurationService(),
            new FakeDeploymentConfigurationService());

        var manifest = await service.ExportAsync(
            $"{Environment.UserName} on {Environment.MachineName} cannot reach the VPN",
            new TriageResult
            {
                Query = "vpn issue",
                Candidates =
                [
                    new TriageCandidate
                    {
                        CategoryId = "remote",
                        CategoryName = "VPN & Remote Access",
                        ConfidenceScore = 82,
                        WhatIThinkIsWrong = "Likely VPN or routing problem.",
                        WhyIThinkThat = "Matched VPN language."
                    }
                ]
            },
            new HealthCheckReport { OverallScore = 72, Summary = "Needs attention" },
            new RunbookExecutionSummary { RunbookId = "work-from-home-runbook", Title = "WFH", Summary = "done" });

        var summaryText = File.ReadAllText(manifest.SummaryPath);
        Assert.DoesNotContain(Environment.UserName, summaryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, summaryText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<user>", summaryText);
        Assert.Contains("<device>", summaryText);
    }

    [Fact]
    public async Task EvidenceBundleService_BasicTier_RedactsIpAddress()
    {
        var service = new EvidenceBundleService(
            new FakeRepairHistoryService(),
            new FakeAutomationHistoryService(),
            new FakeNotificationService(),
            new FakeLogService(),
            new FakeSystemInfoService(),
            new FakeEditionCapabilityService(),
            new FakeBrandingConfigurationService(),
            new FakeDeploymentConfigurationService());

        var manifest = await service.ExportAsync(
            "vpn still not working",
            null,
            null,
            null,
            new EvidenceExportOptions
            {
                Level = EvidenceExportLevel.Basic,
                RedactIpAddress = true,
                IncludeTechnicalHistory = true
            });

        var technicalText = File.ReadAllText(manifest.TechnicalPath);
        Assert.Contains("<redacted>", technicalText);
        Assert.DoesNotContain("192.168.1.10", technicalText, StringComparison.OrdinalIgnoreCase);
    }
}
