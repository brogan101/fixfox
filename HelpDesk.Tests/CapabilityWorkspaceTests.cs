using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class CapabilityWorkspaceTests
{
    [Fact]
    public void MaintenanceProfileService_ExposesReceiptBackedProfiles()
    {
        var service = new MaintenanceProfileService();
        var profiles = service.Profiles;

        Assert.Equal(6, profiles.Count);
        Assert.Equal(profiles.Count, profiles.Select(profile => profile.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(profiles, profile => profile.Id == "quick-clean-profile" && profile.LaunchAction.TargetId == "quick-clean-runbook");
        Assert.Contains(profiles, profile => profile.Id == "safe-maintenance-now-profile" && profile.LaunchAction.TargetId == "safe-maintenance-runbook");
        Assert.All(profiles, profile =>
        {
            Assert.Equal(SupportActionKind.Runbook, profile.LaunchAction.Kind);
            Assert.False(string.IsNullOrWhiteSpace(profile.Title));
            Assert.False(string.IsNullOrWhiteSpace(profile.SafetyNotes));
            Assert.False(string.IsNullOrWhiteSpace(profile.VerificationNotes));
        });
    }

    [Fact]
    public void SupportCenterService_BuildsHighValueCentersWithRealRoutes()
    {
        var service = new SupportCenterService();
        var centers = service.BuildCenters(
            new SystemSnapshot
            {
                DiskFreeGb = 9,
                DiskUsedPct = 92,
                RamUsedPct = 81,
                Uptime = "5 days",
                InternetReachable = false,
                NetworkType = "Wi-Fi",
                PendingUpdateCount = 2
            },
            new[]
            {
                new InstalledProgram("Microsoft Teams", "1.0", "Microsoft", null, 0, "", "", "", ""),
                new InstalledProgram("Microsoft Outlook", "1.0", "Microsoft", null, 0, "", "", "", "")
            },
            new[]
            {
                new RepairHistoryEntry { FixTitle = "Browser cache cleanup", ChangedSummary = "browser cache was cleared", Success = true },
                new RepairHistoryEntry { FixTitle = "VPN reset", ChangedSummary = "share access still failing", Success = false }
            });

        Assert.Equal(8, centers.Count);
        Assert.Contains(centers, center => center.Id == "storage-center" && center.PrimaryAction.TargetId == "disk-full-rescue-runbook");
        Assert.Contains(centers, center => center.Id == "startup-center" && center.SecondaryAction.TargetId == "Startup Apps");
        Assert.Contains(centers, center => center.Id == "software-center" && center.SecondaryAction.TargetId == "repair-outlook-profile");
        Assert.Contains(centers, center => center.Id == "network-center" && center.SecondaryAction.TargetId == "VPN");
        Assert.Contains(centers, center => center.Id == "windows-repair-center" && center.SecondaryAction.TargetId == "Recovery Options");
        Assert.Contains(centers, center => center.Id == "devices-center" && center.SecondaryAction.TargetId == "printing-rescue-runbook");
        Assert.Contains(centers, center => center.Id == "files-center" && center.SecondaryAction.TargetId == "Credential Manager");

        Assert.All(centers, center =>
        {
            Assert.False(string.IsNullOrWhiteSpace(center.Title));
            Assert.False(string.IsNullOrWhiteSpace(center.Summary));
            Assert.NotEmpty(center.Highlights);
            Assert.NotEqual(SupportActionKind.None, center.PrimaryAction.Kind);
            Assert.False(string.IsNullOrWhiteSpace(center.PrimaryAction.TargetId));
        });
    }
}
