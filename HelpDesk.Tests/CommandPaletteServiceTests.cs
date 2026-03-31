using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class CommandPaletteServiceTests
{
    [Fact]
    public void Search_Wifi_Returns_A_Network_Result()
    {
        var service = new CommandPaletteService(BuildCatalog());

        var results = service.Search("wifi", BuildContext());

        Assert.Contains(results, item =>
            item.Kind == CommandPaletteItemKind.Fix
            && item.Section.Contains("Network", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Search_Bsod_Returns_A_Crash_Recovery_Result()
    {
        var service = new CommandPaletteService(BuildCatalog());

        var results = service.Search("BSOD", BuildContext());

        Assert.Contains(results, item =>
            item.Kind == CommandPaletteItemKind.Fix
            && item.Section.Contains("Crash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Search_Outlook_Returns_The_Outlook_Rescue_Runbook()
    {
        var service = new CommandPaletteService(BuildCatalog());

        var results = service.Search("outlook", BuildContext());

        Assert.Contains(results, item =>
            item.Kind == CommandPaletteItemKind.Runbook
            && string.Equals(item.TargetId, "outlook-office-rescue-runbook", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Search_Nonexistent_Query_Returns_No_Results()
    {
        var service = new CommandPaletteService(BuildCatalog());

        var results = service.Search("xyzzy_nonexistent", BuildContext());

        Assert.Empty(results);
    }

    [Fact]
    public void Search_Exact_Title_Match_Ranks_Above_Description_Match()
    {
        var service = new CommandPaletteService(BuildCatalog());

        var results = service.Search("Restart and clear memory", BuildContext())
            .Where(item => !item.IsGroupHeader)
            .ToList();

        Assert.NotEmpty(results);
        Assert.Equal("restart-and-clear-memory", results[0].TargetId);
    }

    [Fact]
    public void Search_Results_Do_Not_Contain_Duplicate_Ids()
    {
        var service = new CommandPaletteService(BuildCatalog());

        var results = service.Search("outlook", BuildContext())
            .Where(item => !item.IsGroupHeader)
            .ToList();

        Assert.Equal(results.Count, results.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static CommandPaletteSearchContext BuildContext()
    {
        var outlookRunbook = new RunbookDefinition
        {
            Id = "outlook-office-rescue-runbook",
            Title = "Outlook / Office Rescue",
            Description = "Repair Outlook sign-in, cache, and Microsoft 365 launch issues.",
            CategoryId = "Office Email & Cloud",
            TriggerHint = "outlook office email sign in launch"
        };

        return new CommandPaletteSearchContext
        {
            Runbooks =
            [
                outlookRunbook,
                new RunbookDefinition
                {
                    Id = "teams-camera-mic-rescue-runbook",
                    Title = "Teams Camera / Mic Rescue",
                    Description = "Repair Teams camera and microphone issues.",
                    CategoryId = "Devices & Peripherals",
                    TriggerHint = "teams camera mic meeting audio webcam"
                }
            ],
            ToolboxGroups =
            [
                new ToolboxGroup
                {
                    Title = "Windows Utilities",
                    Entries =
                    [
                        new ToolboxEntry
                        {
                            Title = "Reliability Monitor",
                            ToolKey = "reliability-monitor",
                            Description = "Review recent app and Windows failures."
                        }
                    ]
                }
            ],
            RecentReceipts =
            [
                new RepairHistoryEntry
                {
                    Id = "receipt-1",
                    FixId = "flush-dns",
                    FixTitle = "Flush DNS Cache",
                    CategoryName = "Network & Connectivity",
                    Outcome = ExecutionOutcome.Completed,
                    Timestamp = new DateTime(2026, 3, 30, 9, 0, 0, DateTimeKind.Local),
                    ChangedSummary = "DNS cache cleared."
                }
            ],
            AutomationRules =
            [
                new AutomationRuleSettings
                {
                    Id = "weekly-maintenance",
                    Title = "Weekly Maintenance",
                    Summary = "Run the maintenance bundle each week.",
                    IncludedTasks = ["cleanup", "security"]
                }
            ],
            AdditionalItems =
            [
                new CommandPaletteItem
                {
                    Id = "runbook:outlook-office-rescue-runbook",
                    Title = "Outlook / Office Rescue",
                    Subtitle = "Duplicate test entry that should be deduplicated.",
                    ResultTypeLabel = "Runbook",
                    Section = "Runbook",
                    Kind = CommandPaletteItemKind.Runbook,
                    TargetId = "outlook-office-rescue-runbook",
                    SearchText = "outlook office duplicate"
                },
                new CommandPaletteItem
                {
                    Id = "setting:startup-landing-page",
                    Title = "Startup Landing Page",
                    Subtitle = "Choose the first FixFox page to open.",
                    ResultTypeLabel = "Setting",
                    Section = "Setting",
                    Kind = CommandPaletteItemKind.Setting,
                    TargetPage = Page.Settings,
                    SearchText = "startup landing settings"
                }
            ]
        };
    }

    private static FakeFixCatalogService BuildCatalog() => new()
    {
        Categories =
        [
            new FixCategory
            {
                Id = "network",
                Title = "Network & Connectivity",
                Subtitle = "Repair internet, Wi-Fi, and connectivity problems.",
                Fixes =
                [
                    BuildFix(
                        "flush-dns",
                        "Flush DNS Cache",
                        "Network & Connectivity",
                        "Flushes cached DNS data to resolve stale internet and Wi-Fi name resolution problems.",
                        FixType.Silent,
                        ["dns", "network"],
                        ["internet", "wifi", "wireless"]),
                    BuildFix(
                        "repair-wifi-adapter",
                        "Repair Wi-Fi Adapter",
                        "Network & Connectivity",
                        "Resets core wireless adapter settings when Wi-Fi stops responding.",
                        FixType.Guided,
                        ["adapter", "network"],
                        ["wifi", "wireless"])
                ]
            },
            new FixCategory
            {
                Id = "bsod",
                Title = "BSOD & Crash Recovery",
                Subtitle = "Diagnose stop errors, crashes, and instability.",
                Fixes =
                [
                    BuildFix(
                        "bsod-triage",
                        "Run BSOD Triage",
                        "BSOD & Crash Recovery",
                        "Collects stop-error basics and crash signals for blue-screen recovery.",
                        FixType.Guided,
                        ["crash", "bsod"],
                        ["blue screen", "stop code"])
                ]
            },
            new FixCategory
            {
                Id = "performance",
                Title = "Performance & Cleanup",
                Subtitle = "Speed up Windows and remove common clutter.",
                Fixes =
                [
                    BuildFix(
                        "restart-and-clear-memory",
                        "Restart and clear memory",
                        "Performance & Cleanup",
                        "Restarts Windows cleanly so long-running memory pressure and pending updates clear out.",
                        FixType.Guided,
                        ["restart", "performance"],
                        ["slow", "memory"]),
                    BuildFix(
                        "maintenance-helper",
                        "Maintenance helper",
                        "Performance & Cleanup",
                        "Use this when you need to restart and clear memory after a long uptime.",
                        FixType.Silent,
                        ["cleanup", "performance"],
                        ["restart", "memory"])
                ]
            }
        ]
    };

    private static FixItem BuildFix(
        string id,
        string title,
        string category,
        string description,
        FixType type,
        IEnumerable<string> tags,
        IEnumerable<string> keywords) => new()
    {
        Id = id,
        Title = title,
        Category = category,
        Description = description,
        Type = type,
        Script = "Write-Output 'ok'",
        Tags = tags.ToArray(),
        Keywords = keywords.ToArray()
    };
}
