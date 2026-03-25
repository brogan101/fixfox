using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class VerificationServiceTests
{
    [Fact]
    public async Task VerifyAsync_ParsesSuccessfulSfcOutput()
    {
        var service = new VerificationService(new FakeRepairCatalogService());
        var result = await service.VerifyAsync(new FixItem
        {
            Id = "run-sfc",
            Title = "Run SFC",
            LastOutput = "Windows Resource Protection found corrupt files and successfully repaired them."
        });

        Assert.Equal(VerificationStatus.Passed, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_ParsesFailedDismOutput()
    {
        var service = new VerificationService(new FakeRepairCatalogService());
        var result = await service.VerifyAsync(new FixItem
        {
            Id = "run-dism",
            Title = "Run DISM",
            LastOutput = "Error: 0x800f081f The source files could not be found."
        });

        Assert.Equal(VerificationStatus.Failed, result.Status);
        Assert.Contains("DISM", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CapturePrecheckSummaryAsync_UsesExplicitRepairMetadataStrategy()
    {
        var catalog = new FakeRepairCatalogService
        {
            Repairs =
            [
                new RepairDefinition
                {
                    Id = "cleanup-temp",
                    Title = "Cleanup temp",
                    Verification = new VerificationDefinition
                    {
                        Strategy = VerificationStrategyKind.StoragePressure,
                        PreChecks = ["Capture storage state."]
                    }
                }
            ]
        };

        var service = new VerificationService(catalog);
        var summary = await service.CapturePrecheckSummaryAsync(new FixItem { Id = "cleanup-temp", Title = "Cleanup temp" });

        Assert.Contains("free space", summary, StringComparison.OrdinalIgnoreCase);
    }
}
