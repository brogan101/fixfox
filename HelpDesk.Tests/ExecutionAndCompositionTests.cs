using HelpDesk.Application.Interfaces;
using HelpDesk.Application.Services;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HelpDesk.Tests;

public sealed class ExecutionAndCompositionTests
{
    [Fact]
    public async Task RepairExecutionService_ExecutesQuickFix_RecordsHistoryAndClearsState()
    {
        var script = new FakeScriptService();
        var verification = new FakeVerificationService();
        var rollback = new FakeRollbackService
        {
            RollbackInfo = new RollbackInfo { IsAvailable = false, Summary = "No rollback available" }
        };
        var restorePoint = new FakeRestorePointService { Result = false };
        var state = new FakeStatePersistenceService();
        var history = new FakeRepairHistoryService();
        var errors = new FakeErrorReportingService();
        var catalog = new FakeFixCatalogService
        {
            Categories =
            [
                new FixCategory
                {
                    Id = "network",
                    Title = "Internet & Connectivity",
                    Fixes =
                    [
                        new FixItem
                        {
                            Id = "flush-dns",
                            Title = "Flush DNS cache",
                            Description = "Resets DNS.",
                            Type = FixType.Silent,
                            Script = "ipconfig /flushdns"
                        }
                    ]
                }
            ]
        };

        var service = new RepairExecutionService(
            script,
            catalog,
            new FakeRepairCatalogService(),
            verification,
            rollback,
            restorePoint,
            state,
            history,
            errors,
            new FakeEditionCapabilityService());
        var fix = catalog.GetById("flush-dns")!;

        var result = await service.ExecuteAsync(fix, "wifi not working");

        Assert.True(result.Success);
        Assert.Equal("flush-dns", result.FixId);
        Assert.Equal("done", result.Output);
        Assert.Equal(VerificationStatus.Passed, result.Verification.Status);
        Assert.False(result.RestorePointAttempted);
        Assert.False(result.RestorePointCreated);
        Assert.Null(state.Load());
        Assert.Contains(history.Entries, entry => entry.FixId == "flush-dns" && entry.Success);
        Assert.Empty(errors.Records);
        Assert.Equal("flush-dns", rollback.LastTrackedFixId);
    }

    [Fact]
    public async Task RepairExecutionService_ExecutesAdminFix_AttemptsRestorePointAndExposesRollback()
    {
        var script = new FakeScriptService();
        var verification = new FakeVerificationService();
        var rollback = new FakeRollbackService
        {
            RollbackInfo = new RollbackInfo { IsAvailable = true, Summary = "Undo is available" }
        };
        var restorePoint = new FakeRestorePointService { Result = true };
        var state = new FakeStatePersistenceService();
        var history = new FakeRepairHistoryService();
        var errors = new FakeErrorReportingService();
        var catalog = new FakeFixCatalogService
        {
            Categories =
            [
                new FixCategory
                {
                    Id = "updates",
                    Title = "Windows Update",
                    Fixes =
                    [
                        new FixItem
                        {
                            Id = "update-repair-reset",
                            Title = "Repair Windows Update",
                            Description = "Repairs Windows Update components.",
                            Type = FixType.Silent,
                            RequiresAdmin = true,
                            Script = "net stop wuauserv"
                        }
                    ]
                }
            ]
        };

        var service = new RepairExecutionService(
            script,
            catalog,
            new FakeRepairCatalogService(),
            verification,
            rollback,
            restorePoint,
            state,
            history,
            errors,
            new FakeEditionCapabilityService());
        var fix = catalog.GetById("update-repair-reset")!;

        var result = await service.ExecuteAsync(fix, "windows update failed");

        Assert.True(result.Success);
        Assert.True(result.RestorePointAttempted);
        Assert.True(result.RestorePointCreated);
        Assert.True(result.Rollback.IsAvailable);
        Assert.Equal(1, restorePoint.AttemptCount);
        Assert.Contains(history.Entries, entry => entry.FixId == "update-repair-reset" && entry.RequiresAdmin);
        Assert.Equal("update-repair-reset", rollback.LastTrackedFixId);
    }

    [Fact]
    public async Task RepairExecutionService_FailsOverallWhenVerificationFails()
    {
        var script = new FakeScriptService();
        var verification = new FakeVerificationService
        {
            Result = new VerificationResult
            {
                Status = VerificationStatus.Failed,
                Summary = "DNS resolution still failed."
            }
        };
        var rollback = new FakeRollbackService
        {
            RollbackInfo = new RollbackInfo { IsAvailable = true, Summary = "Undo is available" }
        };
        var restorePoint = new FakeRestorePointService { Result = false };
        var state = new FakeStatePersistenceService();
        var history = new FakeRepairHistoryService();
        var errors = new FakeErrorReportingService();
        var catalog = new FakeFixCatalogService
        {
            Categories =
            [
                new FixCategory
                {
                    Id = "network",
                    Title = "Internet & Connectivity",
                    Fixes =
                    [
                        new FixItem
                        {
                            Id = "flush-dns",
                            Title = "Flush DNS cache",
                            Description = "Resets DNS.",
                            Type = FixType.Silent,
                            Script = "ipconfig /flushdns"
                        }
                    ]
                }
            ]
        };

        var service = new RepairExecutionService(
            script,
            catalog,
            new FakeRepairCatalogService(),
            verification,
            rollback,
            restorePoint,
            state,
            history,
            errors,
            new FakeEditionCapabilityService());
        var result = await service.ExecuteAsync(catalog.GetById("flush-dns")!, "dns broken");

        Assert.False(result.Success);
        Assert.Equal("DNS resolution still failed.", result.FailureSummary);
        Assert.Contains("support package", result.NextStep, StringComparison.OrdinalIgnoreCase);
        Assert.Single(errors.Records);
    }

    [Fact]
    public async Task RepairExecutionService_BlocksRepairThatRequiresHigherEdition()
    {
        var fix = new FixItem
        {
            Id = "pro-network-repair",
            Title = "Deep network repair",
            Description = "Advanced repair.",
            Type = FixType.Silent,
            Script = "Write-Output 'ok'"
        };

        var catalog = new FakeFixCatalogService
        {
            Categories =
            [
                new FixCategory
                {
                    Id = "network",
                    Title = "Network",
                    Fixes = [fix]
                }
            ]
        };

        var repairCatalog = new FakeRepairCatalogService
        {
            Repairs =
            [
                new RepairDefinition
                {
                    Id = "pro-network-repair",
                    Title = "Deep network repair",
                    MasterCategoryId = "network",
                    MinimumEdition = AppEdition.Pro,
                    Tier = RepairTier.AdminDeepFix,
                    Fix = fix
                }
            ]
        };

        var service = new RepairExecutionService(
            new FakeScriptService(),
            catalog,
            repairCatalog,
            new FakeVerificationService(),
            new FakeRollbackService(),
            new FakeRestorePointService(),
            new FakeStatePersistenceService(),
            new FakeRepairHistoryService(),
            new FakeErrorReportingService(),
            new FakeEditionCapabilityService
            {
                Snapshot = new EditionCapabilitySnapshot
                {
                    Edition = AppEdition.Basic,
                    EvidenceBundles = CapabilityState.Available,
                    Runbooks = CapabilityState.Available,
                    DeepRepairs = CapabilityState.UpgradeRequired
                }
            });

        var result = await service.ExecuteAsync(fix, "need deeper repair");

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcome.Blocked, result.Outcome);
        Assert.Contains("FixFox Pro", result.FailureSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppServiceRegistrar_WiresTheV3ServiceLayerIntoTheRealCompositionRoot()
    {
        var services = new ServiceCollection();
        AppServiceRegistrar.Configure(services, headless: true);
        using var provider = services.BuildServiceProvider();

        var providers = provider.GetServices<IFixCatalogProvider>().ToList();
        Assert.True(providers.Count >= 2);
        Assert.IsType<MergedFixCatalogService>(provider.GetRequiredService<IFixCatalogService>());
        Assert.IsType<RepairCatalogService>(provider.GetRequiredService<IRepairCatalogService>());
        Assert.IsType<WeightedTriageEngine>(provider.GetRequiredService<ITriageEngine>());
        Assert.IsType<RunbookCatalogService>(provider.GetRequiredService<IRunbookCatalogService>());
        Assert.IsType<RepairExecutionService>(provider.GetRequiredService<IRepairExecutionService>());
        Assert.IsType<RunbookExecutionService>(provider.GetRequiredService<IRunbookExecutionService>());
        Assert.IsType<EvidenceBundleService>(provider.GetRequiredService<IEvidenceBundleService>());
        Assert.IsType<DeploymentConfigurationService>(provider.GetRequiredService<IDeploymentConfigurationService>());
        Assert.IsType<KnowledgeBaseService>(provider.GetRequiredService<IKnowledgeBaseService>());
        Assert.IsType<EditionCapabilityService>(provider.GetRequiredService<IEditionCapabilityService>());
        Assert.IsType<AppUpdateService>(provider.GetRequiredService<IAppUpdateService>());
        Assert.IsType<MaintenanceProfileService>(provider.GetRequiredService<IMaintenanceProfileService>());
        Assert.IsType<SupportCenterService>(provider.GetRequiredService<ISupportCenterService>());
        Assert.IsType<CommandPaletteService>(provider.GetRequiredService<ICommandPaletteService>());

        var triage = provider.GetRequiredService<ITriageEngine>().Analyze("wifi not working", new TriageContext { NetworkType = "Wi-Fi" });
        Assert.NotEmpty(triage.Candidates);

        var runbooks = provider.GetRequiredService<IRunbookCatalogService>().Runbooks;
        Assert.NotEmpty(runbooks);
        Assert.Contains(runbooks, runbook => runbook.Id == "quick-clean-runbook");
        Assert.Contains(runbooks, runbook => runbook.Id == "disk-full-rescue-runbook");
        Assert.Contains(runbooks, runbook => runbook.Id == "printing-rescue-runbook");
        Assert.Contains(runbooks, runbook => runbook.Id == "safe-maintenance-runbook");
    }
}
