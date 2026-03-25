using HelpDesk.Application.Interfaces;
using HelpDesk.Infrastructure.Fixes;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class SupportDepthTests
{
    [Fact]
    public void MaintenanceProfileService_ExposesTaskListsAndSchedulingGuidance()
    {
        var service = new MaintenanceProfileService();

        var routine = Assert.Single(service.Profiles, profile => profile.Id == "routine-tune-up-profile");
        Assert.NotEmpty(routine.IncludedTasks);
        Assert.True(routine.SupportsScheduling);
        Assert.True(routine.PreferIdleWhenScheduled);
        Assert.True(routine.AvoidWhenOnBattery);

        var quick = Assert.Single(service.Profiles, profile => profile.Id == "quick-clean-profile");
        Assert.NotEmpty(quick.IncludedTasks);
        Assert.True(quick.SupportsScheduling);
        Assert.False(quick.PreferIdleWhenScheduled);
    }

    [Fact]
    public async Task StorageInsightsService_DetectsLargeFilesFromConfiguredRoots()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fixfox-storage-{Guid.NewGuid():N}");
        var downloads = Path.Combine(root, "Downloads");
        Directory.CreateDirectory(downloads);
        await File.WriteAllBytesAsync(Path.Combine(downloads, "small.txt"), new byte[256]);
        await File.WriteAllBytesAsync(Path.Combine(downloads, "large.iso"), new byte[4096]);

        try
        {
            var service = new StorageInsightsService([downloads]);

            var insights = await service.GetInsightsAsync();

            Assert.NotEmpty(insights);
            Assert.Equal("large.iso", insights[0].DisplayName);
            Assert.Equal("Downloads", insights[0].LocationLabel);
            Assert.Contains("safe", insights[0].SafeToRemoveSummary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RepairCatalogService_ProjectsStructuredRepairContractsForCoreFamilies()
    {
        IFixCatalogProvider provider = new BuiltInFixCatalogProvider(new FixCatalogService());
        var service = new RepairCatalogService([provider]);

        var networkRepair = service.GetRepair("flush-dns");
        Assert.NotNull(networkRepair);
        Assert.False(string.IsNullOrWhiteSpace(networkRepair!.UserProblemSummary));
        Assert.False(string.IsNullOrWhiteSpace(networkRepair.WhySuggested));
        Assert.False(string.IsNullOrWhiteSpace(networkRepair.VerificationStrategyId));
        Assert.NotEmpty(networkRepair.Preconditions);
        Assert.NotEmpty(networkRepair.EvidenceExportTags);
        Assert.False(string.IsNullOrWhiteSpace(networkRepair.SuggestedNextStepOnFailure));
        Assert.False(string.IsNullOrWhiteSpace(networkRepair.SuppressionScopeHint));

        var windowsRepair = service.GetRepair("run-sfc");
        Assert.NotNull(windowsRepair);
        Assert.NotEmpty(windowsRepair!.RelatedWindowsTools);
        Assert.NotEmpty(windowsRepair.RelatedRunbooks);
        Assert.False(string.IsNullOrWhiteSpace(windowsRepair.RollbackHint));
        Assert.False(string.IsNullOrWhiteSpace(windowsRepair.EscalationHint));
    }
}
