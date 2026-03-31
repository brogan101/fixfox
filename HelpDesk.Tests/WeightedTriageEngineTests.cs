using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class WeightedTriageEngineTests
{
    private static WeightedTriageEngine CreateEngine()
    {
        var catalog = new FakeRepairCatalogService
        {
            MasterCategories =
            [
                new MasterCategoryDefinition { Id = "network", Title = "Internet & Connectivity" },
                new MasterCategoryDefinition { Id = "performance", Title = "Performance & Speed" }
            ],
            Repairs =
            [
                new RepairDefinition
                {
                    Id = "flush-dns",
                    Title = "Flush DNS cache",
                    ShortDescription = "Fixes website and network name resolution problems.",
                    MasterCategoryId = "network",
                    SearchPhrases = ["wifi not working", "no internet", "connected but no internet"],
                    Synonyms = ["wifi", "wi-fi", "internet", "network", "dns"],
                    SupportedSubIssues = ["dns failure"],
                    WhatWillHappen = "FixFox will reset DNS and verify connectivity.",
                    Fix = new FixItem { Id = "flush-dns", Title = "Flush DNS cache", Description = "Flush DNS.", Tags = ["network"], Keywords = ["wifi not working"] }
                },
                new RepairDefinition
                {
                    Id = "clear-temp-files",
                    Title = "Clear all temp files",
                    ShortDescription = "Removes temporary files to improve performance.",
                    MasterCategoryId = "performance",
                    SearchPhrases = ["computer slow", "pc sluggish", "slow startup"],
                    Synonyms = ["slow", "sluggish", "lagging"],
                    SupportedSubIssues = ["temp bloat"],
                    WhatWillHappen = "FixFox will clear temporary files and summarize the cleanup.",
                    Fix = new FixItem { Id = "clear-temp-files", Title = "Clear temp files", Description = "Cleanup", Tags = ["performance"], Keywords = ["computer slow"] }
                }
            ]
        };

        var history = new FakeRepairHistoryService();
        history.Record(new RepairHistoryEntry { CategoryId = "network", CategoryName = "Internet & Connectivity", Success = false });
        return new WeightedTriageEngine(catalog, history);
    }

    [Fact]
    public void Analyze_RanksExactNetworkPhrase_Highest()
    {
        var engine = CreateEngine();

        var result = engine.Analyze("wifi not working", new TriageContext { NetworkType = "Wi-Fi", HasRecentFailures = true });

        Assert.NotEmpty(result.Candidates);
        Assert.Equal("network", result.Candidates[0].CategoryId);
        Assert.True(result.Candidates[0].ConfidenceScore >= 75);
    }

    [Fact]
    public void Analyze_HandlesMinorTypos()
    {
        var engine = CreateEngine();

        var result = engine.Analyze("wfi not workng", new TriageContext { NetworkType = "Wi-Fi" });

        Assert.NotEmpty(result.Candidates);
        Assert.Equal("network", result.Candidates[0].CategoryId);
    }

    [Fact]
    public void Analyze_PrefersDiagnosticsFirstWhenConfidenceIsAmbiguous()
    {
        var catalog = new FakeRepairCatalogService
        {
            MasterCategories =
            [
                new MasterCategoryDefinition { Id = "network", Title = "Internet & Connectivity" }
            ],
            Repairs =
            [
                new RepairDefinition
                {
                    Id = "flush-dns",
                    Title = "Flush DNS cache",
                    ShortDescription = "Fixes basic browsing problems.",
                    MasterCategoryId = "network",
                    SearchPhrases = ["connection issue"],
                    Synonyms = ["internet", "dns"],
                    SupportedSubIssues = ["browser connectivity"],
                    WhatWillHappen = "FixFox will start with a low-risk connectivity check.",
                    Fix = new FixItem { Id = "flush-dns", Title = "Flush DNS cache", Description = "Flush DNS.", Tags = ["network"], Keywords = ["connection issue"] }
                }
            ]
        };
        var engine = new WeightedTriageEngine(catalog, new FakeRepairHistoryService());

        var result = engine.Analyze("internet acting odd");

        Assert.NotEmpty(result.Candidates);
        Assert.True(result.Candidates[0].RecommendDiagnosticsFirst);
        Assert.False(string.IsNullOrWhiteSpace(result.Candidates[0].SafestFirstAction));
        Assert.False(string.IsNullOrWhiteSpace(result.Candidates[0].StrongerNextAction));
    }

    [Fact]
    public void Analyze_AddsEscalationGuidanceForVpnAuthStyleIssues()
    {
        var engine = CreateEngine();

        var result = engine.Analyze("vpn certificate access denied");

        Assert.NotEmpty(result.Candidates);
        Assert.Contains("Escalate", result.Candidates[0].EscalationSignal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_WithThreeCandidates_PopulatesRunnerUpRankingReasons()
    {
        var catalog = new FakeRepairCatalogService
        {
            MasterCategories =
            [
                new MasterCategoryDefinition { Id = "network", Title = "Internet & Connectivity" },
                new MasterCategoryDefinition { Id = "performance", Title = "Performance & Speed" },
                new MasterCategoryDefinition { Id = "devices", Title = "Devices & Peripherals" }
            ],
            Repairs =
            [
                new RepairDefinition
                {
                    Id = "flush-dns",
                    Title = "Flush DNS cache",
                    ShortDescription = "Fixes website and network name resolution problems.",
                    MasterCategoryId = "network",
                    SearchPhrases = ["wifi not working"],
                    Synonyms = ["wifi", "internet"],
                    SupportedSubIssues = ["dns failure"],
                    WhatWillHappen = "FixFox will reset DNS and verify connectivity.",
                    Fix = new FixItem { Id = "flush-dns", Title = "Flush DNS cache", Description = "Flush DNS." }
                },
                new RepairDefinition
                {
                    Id = "clear-temp-files",
                    Title = "Clear temp files",
                    ShortDescription = "Reduces temporary file pressure when the PC feels slow.",
                    MasterCategoryId = "performance",
                    SearchPhrases = ["computer slow"],
                    Synonyms = ["slow", "lagging"],
                    SupportedSubIssues = ["temp bloat"],
                    WhatWillHappen = "FixFox will clear temporary files and summarize the cleanup.",
                    Fix = new FixItem { Id = "clear-temp-files", Title = "Clear temp files", Description = "Cleanup." }
                },
                new RepairDefinition
                {
                    Id = "fix-webcam",
                    Title = "Check webcam",
                    ShortDescription = "Checks whether the camera is present and usable.",
                    MasterCategoryId = "devices",
                    SearchPhrases = ["camera issue"],
                    Synonyms = ["camera", "webcam"],
                    SupportedSubIssues = ["camera blocked"],
                    WhatWillHappen = "FixFox will inspect camera state and privacy access.",
                    Fix = new FixItem { Id = "fix-webcam", Title = "Check webcam", Description = "Camera check." }
                }
            ]
        };

        var engine = new WeightedTriageEngine(catalog, new FakeRepairHistoryService());

        var result = engine.Analyze("wifi not working and computer slow after a camera issue");

        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(result.Candidates[0], result.Candidates.OrderByDescending(candidate => candidate.ConfidenceScore).First());
        Assert.False(string.IsNullOrWhiteSpace(result.Candidates[1].RankingReason));
        Assert.False(string.IsNullOrWhiteSpace(result.Candidates[2].RankingReason));
    }
}
