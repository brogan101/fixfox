using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class CommandPaletteServiceTests
{
    [Fact]
    public void Search_WithoutQuery_PrioritizesHighValueCommands()
    {
        var service = new CommandPaletteService(BuildCatalog());

        var results = service.Search(
            query: "",
            pinnedFixes: [BuildFix("flush-dns", "Flush DNS cache", FixType.Silent)],
            favoriteFixes: [],
            recentFixes: [],
            runbooks: [new RunbookDefinition { Id = "slow-pc-runbook", Title = "Slow PC Rescue", Description = "Recover performance." }],
            maintenanceProfiles: [new MaintenanceProfileDefinition { Id = "safe-maintenance-now-profile", Title = "Safe Maintenance Now", Summary = "Run the conservative maintenance pass." }],
            supportCenters: [new SupportCenterDefinition { Id = "network-center", Title = "Network, VPN & Remote Work", Summary = "Resolve DNS, VPN, and browser path issues.", PrimaryAction = new SupportAction { Label = "Open", Kind = SupportActionKind.Runbook, TargetId = "internet-recovery-runbook" }, Highlights = ["DNS", "VPN"] }],
            toolboxGroups: [new ToolboxGroup { Title = "Windows Utilities", Entries = [new ToolboxEntry { Title = "Task Manager", Description = "Inspect hung apps." }] }]);

        Assert.NotEmpty(results);
        Assert.Equal("Home", results[0].Title);
        Assert.Contains(results, item => item.Kind == CommandPaletteItemKind.MaintenanceProfile && item.Title == "Safe Maintenance Now");
        Assert.Contains(results, item => item.Kind == CommandPaletteItemKind.Page && item.TargetPage == Page.SymptomChecker);
    }

    [Fact]
    public void Search_QuerySurfacesCrossSurfaceWindowsActions()
    {
        var service = new CommandPaletteService(BuildCatalog());

        var results = service.Search(
            query: "vpn",
            pinnedFixes: [],
            favoriteFixes: [],
            recentFixes: [],
            runbooks: [new RunbookDefinition { Id = "work-from-home-runbook", Title = "Work-From-Home Rescue", Description = "Reset remote-work network basics.", TriggerHint = "VPN and internal resources" }],
            maintenanceProfiles: [],
            supportCenters: [new SupportCenterDefinition { Id = "network-center", Title = "Network, VPN & Remote Work", Summary = "Resolve DNS, VPN, and proxy problems.", PrimaryAction = new SupportAction { Label = "Open", Kind = SupportActionKind.Runbook, TargetId = "work-from-home-runbook" }, Highlights = ["VPN", "Proxy"] }],
            toolboxGroups: [new ToolboxGroup { Title = "System Settings", Entries = [new ToolboxEntry { Title = "VPN", Description = "Open VPN connections and remote access settings." }] }]);

        Assert.Contains(results, item => item.Kind == CommandPaletteItemKind.Runbook && item.TargetId == "work-from-home-runbook");
        Assert.Contains(results, item => item.Kind == CommandPaletteItemKind.SupportCenter && item.TargetId == "network-center");
        Assert.Contains(results, item => item.Kind == CommandPaletteItemKind.Toolbox && item.TargetId == "VPN");
    }

    [Fact]
    public void Search_QueryIncludesMatchingRepairActions()
    {
        var service = new CommandPaletteService(BuildCatalog());

        var results = service.Search(
            query: "browser",
            pinnedFixes: [],
            favoriteFixes: [],
            recentFixes: [],
            runbooks: [],
            maintenanceProfiles: [],
            supportCenters: [],
            toolboxGroups: []);

        Assert.Contains(results, item => item.Kind == CommandPaletteItemKind.Fix && item.TargetId == "clear-browser-cache");
    }

    private static FakeFixCatalogService BuildCatalog() => new()
    {
        Categories =
        [
            new FixCategory
            {
                Id = "network",
                Title = "Network",
                Fixes =
                [
                    BuildFix("flush-dns", "Flush DNS cache", FixType.Silent),
                    BuildFix("clear-browser-cache", "Clear browser cache", FixType.Silent)
                ]
            }
        ]
    };

    private static FixItem BuildFix(string id, string title, FixType type) => new()
    {
        Id = id,
        Title = title,
        Description = $"{title} description",
        Type = type,
        Script = "echo ok",
        Tags = ["browser", "network"],
        Keywords = ["vpn", "cache"]
    };
}
