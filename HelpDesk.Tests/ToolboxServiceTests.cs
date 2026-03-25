using HelpDesk.Infrastructure.Services;
using HelpDesk.Domain.Enums;
using Xunit;

namespace HelpDesk.Tests;

public sealed class ToolboxServiceTests
{
    [Fact]
    public void Toolbox_IncludesCoreWindowsSupportEntries()
    {
        var service = new ToolboxService();
        var titles = service.Groups.SelectMany(group => group.Entries).Select(entry => entry.Title).ToList();

        Assert.Contains("Task Manager", titles);
        Assert.Contains("Device Manager", titles);
        Assert.Contains("Windows Update", titles);
        Assert.Contains("Recovery Options", titles);
        Assert.Contains("Storage Settings", titles);
        Assert.Contains("Printers & Scanners", titles);
        Assert.Contains("Quick Assist", titles);
        Assert.Contains("Get Help", titles);
        Assert.Contains("Cleanup Recommendations", titles);
    }

    [Fact]
    public void Toolbox_DoesNotDuplicateTitlesAcrossGroups()
    {
        var service = new ToolboxService();
        var titles = service.Groups.SelectMany(group => group.Entries).Select(entry => entry.Title).ToList();

        Assert.Equal(titles.Count, titles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Toolbox_AdvancedEntriesCarryEditionAndAdvancedModeRequirements()
    {
        var service = new ToolboxService();
        var advancedEntries = service.Groups
            .SelectMany(group => group.Entries)
            .Where(entry => entry.Title is "Services" or "Event Viewer" or "Resource Monitor" or "Reliability Monitor" or "Credential Manager")
            .ToList();

        Assert.NotEmpty(advancedEntries);
        Assert.All(advancedEntries, entry =>
        {
            Assert.Equal(AppEdition.Pro, entry.MinimumEdition);
            Assert.True(entry.RequiresAdvancedMode);
            Assert.Equal(ProductCapability.AdvancedToolbox, entry.RequiredCapability);
        });
    }
}
