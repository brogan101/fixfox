using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Fixes;
using Newtonsoft.Json;
using SharedConstants = HelpDesk.Shared.Constants;

namespace HelpDesk.Infrastructure.Services;

internal static class ProductizationPaths
{
    public static string ConfigurationDir => Path.Combine(AppContext.BaseDirectory, "Configuration");
    public static string CatalogDir => Path.Combine(AppContext.BaseDirectory, "Catalog", "RepairPacks");
    public static string RepairHistoryFile => Path.Combine(SharedConstants.AppDataDir, "repair-history.json");
    public static string RollbackFile => Path.Combine(SharedConstants.AppDataDir, "rollback.json");
    public static string InterruptedStateFile => Path.Combine(SharedConstants.AppDataDir, "interrupted-operation.json");
    public static string ErrorReportFile => Path.Combine(SharedConstants.AppDataDir, "error-reports.jsonl");
    public static string EvidenceDir => Path.Combine(SharedConstants.AppDataDir, "evidence-bundles");
    public static string BrandingConfigFile => Path.Combine(ConfigurationDir, "branding.json");
    public static string KnowledgeBaseConfigFile => Path.Combine(ConfigurationDir, "knowledge-base.json");
    public static string UpdateConfigFile => Path.Combine(ConfigurationDir, "update.json");
}

internal sealed class RollbackBookmark
{
    public string FixId { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Script { get; set; } = "";
}

internal sealed class CatalogPackRepairItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public string EstTime { get; set; } = "~1 min";
    public string Script { get; set; } = "";
    public bool RequiresAdmin { get; set; }
    public string[] Tags { get; set; } = [];
    public string[] Keywords { get; set; } = [];
    public string[] Synonyms { get; set; } = [];
    public string[] SupportedSubIssues { get; set; } = [];
    public string[] Diagnostics { get; set; } = [];
    public string[] VerificationChecks { get; set; } = [];
}

internal sealed class CatalogPackDocument
{
    public List<MasterCategoryDefinition> Categories { get; set; } = [];
    public List<CatalogPackRepairItem> Repairs { get; set; } = [];
    public List<FixBundle> Bundles { get; set; } = [];
    public List<RunbookDefinition> Runbooks { get; set; } = [];
}

internal sealed class KnowledgeBaseConfigDocument
{
    public List<KnowledgeBaseEntry> Entries { get; set; } = [];
}

internal sealed class BrandingConfigDocument
{
    public string AppName { get; set; } = "FixFox";
    public string AppSubtitle { get; set; } = "Desktop support toolkit";
    public string SupportEmail { get; set; } = "support@example.com";
    public string SupportPortalLabel { get; set; } = "Help Desk Portal";
    public string SupportPortalUrl { get; set; } = "https://support.example.com";
    public string AccentHex { get; set; } = "#F97316";
}

internal sealed class UpdateConfigDocument
{
    public string Provider { get; set; } = "";
    public string FeedUrl { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Repository { get; set; } = "";
}

internal static class CatalogProjection
{
    public static MasterCategoryDefinition ToMasterCategory(FixCategory category)
    {
        var phrases = category.Fixes
            .SelectMany(f => f.Keywords.Take(3))
            .Concat(category.Title.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        return new MasterCategoryDefinition
        {
            Id = category.Id,
            Title = category.Title,
            Description = $"Fixes and diagnostics for {category.Title.ToLowerInvariant()} issues.",
            IconKey = category.Icon,
            HealthScoreDomain = category.Id,
            DisplayPriority = 100,
            CommonUserPhrases = phrases,
            SymptomPatterns = category.Fixes.SelectMany(f => f.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList(),
            DefaultRecommendedRepairs = category.Fixes.Take(3).Select(f => f.Id).ToList(),
        };
    }

    public static RepairDefinition ToRepairDefinition(FixCategory category, FixItem fix)
    {
        var tier = fix.Type == FixType.Guided
            ? RepairTier.GuidedEscalation
            : fix.RequiresAdmin ? RepairTier.AdminDeepFix : RepairTier.SafeUser;

        var restorePoint = fix.RequiresAdmin && (
            fix.Id.Contains("reset", StringComparison.OrdinalIgnoreCase) ||
            fix.Id.Contains("repair", StringComparison.OrdinalIgnoreCase) ||
            fix.Id.Contains("startup", StringComparison.OrdinalIgnoreCase) ||
            fix.Id.Contains("update", StringComparison.OrdinalIgnoreCase) ||
            fix.Id.Contains("driver", StringComparison.OrdinalIgnoreCase));

        var actions = fix.HasSteps
            ? fix.Steps.Select(s => s.Title).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
            : new List<string> { fix.Title };

        var verificationChecks = category.Id switch
        {
            "network" or "remote" => new List<string> { "Validate adapter state", "Resolve DNS", "Reach a public endpoint" },
            "printers" => new List<string> { "Confirm spooler is running", "Confirm queue is clear" },
            "audio" => new List<string> { "Confirm audio service is running" },
            "updates" => new List<string> { "Confirm Windows Update services respond" },
            "security" => new List<string> { "Confirm Defender or Firewall service state" },
            _ => new List<string> { "Review output and confirm the app reported success" },
        };

        return new RepairDefinition
        {
            Id = fix.Id,
            Title = fix.Title,
            ShortDescription = fix.Description,
            LongDescription = fix.Description,
            MasterCategoryId = category.Id,
            SupportedSubIssues = fix.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SearchPhrases = fix.Keywords.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Synonyms = fix.Tags.Concat(category.Title.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ConfidenceBoostSignals = fix.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Diagnostics = new List<string> { $"Inspect {category.Title.ToLowerInvariant()} state relevant to {fix.Title.ToLowerInvariant()}." },
            QuickFixActions = actions,
            DeepFixActions = fix.RequiresAdmin ? actions : [],
            VerificationChecks = verificationChecks,
            MinimumEdition = tier == RepairTier.SafeUser ? AppEdition.Basic : AppEdition.Pro,
            Tier = tier,
            RiskLevel = tier == RepairTier.SafeUser ? RiskLevel.Low : RiskLevel.Moderate,
            RequiresAdmin = fix.RequiresAdmin,
            MayRequireReboot = fix.Id.Contains("driver", StringComparison.OrdinalIgnoreCase)
                || fix.Id.Contains("update", StringComparison.OrdinalIgnoreCase)
                || fix.Id.Contains("network-reset", StringComparison.OrdinalIgnoreCase),
            SupportsRestorePoint = restorePoint,
            SupportsRollback = GetRollbackScript(fix) is not null,
            WhatWillHappen = BuildWhatWillHappen(fix, verificationChecks),
            UserFacingWarnings = fix.RequiresAdmin
                ? "This repair needs administrator permission and may change system settings."
                : "This repair stays inside a low-risk support workflow.",
            AdvancedNotes = fix.HasScript ? "Scripted repair routed through the centralized execution pipeline." : "Guided repair steps are shown to the user.",
            Fix = fix,
        };
    }

    public static string? GetRollbackScript(FixItem fix) => fix.Id switch
    {
        "set-high-performance" or "set-ultimate-performance" => "powercfg /setactive SCHEME_BALANCED",
        "set-dns-cloudflare" or "set-dns-google" => "netsh interface ip set dns name=\"Ethernet\" dhcp",
        _ => null,
    };

    private static string BuildWhatWillHappen(FixItem fix, IReadOnlyList<string> verificationChecks)
    {
        var builder = new StringBuilder();
        builder.Append(fix.Description.TrimEnd('.'));
        builder.Append(". FixFox will ");
        if (fix.Type == FixType.Guided)
            builder.Append("walk you through each step");
        else if (fix.RequiresAdmin)
            builder.Append("run an elevated repair script");
        else
            builder.Append("run a one-click repair");

        builder.Append(", then verify: ");
        builder.Append(string.Join(", ", verificationChecks.Take(2)));
        builder.Append('.');
        return builder.ToString();
    }
}

public sealed class BuiltInFixCatalogProvider : IFixCatalogProvider
{
    public string Name => "Built-in";
    public IReadOnlyList<FixCategory> Categories { get; }
    public IReadOnlyList<FixBundle> Bundles { get; }
    public IReadOnlyList<RunbookDefinition> Runbooks { get; }
    public IReadOnlyList<RepairDefinition> Repairs { get; }
    public IReadOnlyList<MasterCategoryDefinition> MasterCategories { get; }

    public BuiltInFixCatalogProvider(FixCatalogService catalog)
    {
        Categories = catalog.Categories;
        Bundles = catalog.Bundles;
        MasterCategories = Categories.Select(CatalogProjection.ToMasterCategory).ToList().AsReadOnly();
        Repairs = Categories.SelectMany(c => c.Fixes.Select(f => CatalogProjection.ToRepairDefinition(c, f)))
            .ToList()
            .AsReadOnly();
        Runbooks = Bundles.Select(bundle => new RunbookDefinition
        {
            Id = bundle.Id,
            Title = bundle.Title,
            Description = bundle.Description,
            CategoryId = "bundle",
            EstTime = bundle.EstTime,
            RequiresAdmin = bundle.FixIds
                .Select(id => Repairs.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)))
                .Any(r => r?.RequiresAdmin == true),
            SupportsRollback = bundle.FixIds
                .Select(id => Repairs.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)))
                .Any(r => r?.SupportsRollback == true),
            SupportsRestorePoint = bundle.FixIds
                .Select(id => Repairs.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)))
                .Any(r => r?.SupportsRestorePoint == true),
            MinimumEdition = AppEdition.Pro,
            TriggerHint = $"Recommended when you want to run the {bundle.Title.ToLowerInvariant()} pack end to end.",
            Steps = bundle.FixIds.Select((id, index) => new RunbookStepDefinition
            {
                Id = $"{bundle.Id}-step-{index + 1}",
                Title = Repairs.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase))?.Title ?? id,
                Description = $"Run repair {id}.",
                StepKind = RunbookStepKind.Repair,
                LinkedRepairId = id,
                RequiresAdmin = Repairs.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase))?.RequiresAdmin == true,
                SupportsRollback = Repairs.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase))?.SupportsRollback == true,
                StopOnFailure = true,
                PostStepMessage = "FixFox verifies the result before continuing."
            }).ToList()
        }).ToList().AsReadOnly();
    }
}

public sealed class ExternalCatalogProvider : IFixCatalogProvider
{
    public string Name => "Extension packs";
    public IReadOnlyList<FixCategory> Categories { get; }
    public IReadOnlyList<FixBundle> Bundles { get; }
    public IReadOnlyList<RunbookDefinition> Runbooks { get; }
    public IReadOnlyList<RepairDefinition> Repairs { get; }
    public IReadOnlyList<MasterCategoryDefinition> MasterCategories { get; }

    public ExternalCatalogProvider(ISettingsService settingsService, IAppLogger logger)
    {
        var settings = settingsService.Load();
        if (!settings.EnableExtensionCatalogs)
        {
            Categories = [];
            Bundles = [];
            Repairs = [];
            MasterCategories = [];
            Runbooks = [];
            return;
        }

        Directory.CreateDirectory(ProductizationPaths.CatalogDir);
        var packs = new List<CatalogPackDocument>();
        foreach (var file in Directory.GetFiles(ProductizationPaths.CatalogDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var document = JsonConvert.DeserializeObject<CatalogPackDocument>(File.ReadAllText(file));
                if (document is not null)
                    packs.Add(document);
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to load repair pack '{Path.GetFileName(file)}': {ex.Message}");
            }
        }

        MasterCategories = packs.SelectMany(p => p.Categories)
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList()
            .AsReadOnly();

        Repairs = packs.SelectMany(p => p.Repairs.Select(ToRepairDefinition))
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList()
            .AsReadOnly();

        Categories = Repairs
            .GroupBy(r => r.MasterCategoryId, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var category = MasterCategories.FirstOrDefault(c => string.Equals(c.Id, group.Key, StringComparison.OrdinalIgnoreCase));
                return new FixCategory
                {
                    Id = group.Key,
                    Title = category?.Title ?? group.Key,
                    Icon = category?.IconKey ?? "\uE90F",
                    Fixes = group.Select(r => r.Fix).ToList()
                };
            })
            .ToList()
            .AsReadOnly();

        Bundles = packs.SelectMany(p => p.Bundles)
            .GroupBy(b => b.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList()
            .AsReadOnly();

        Runbooks = packs.SelectMany(p => p.Runbooks)
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList()
            .AsReadOnly();
    }

    private static RepairDefinition ToRepairDefinition(CatalogPackRepairItem item)
    {
        var fix = new FixItem
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            Type = FixType.Silent,
            Script = item.Script,
            RequiresAdmin = item.RequiresAdmin,
            EstTime = item.EstTime,
            Tags = item.Tags,
            Keywords = item.Keywords,
        };

        return new RepairDefinition
        {
            Id = item.Id,
            Title = item.Title,
            ShortDescription = item.Description,
            LongDescription = item.Description,
            MasterCategoryId = item.CategoryId,
            SupportedSubIssues = item.SupportedSubIssues.ToList(),
            SearchPhrases = item.Keywords.ToList(),
            Synonyms = item.Synonyms.ToList(),
            Diagnostics = item.Diagnostics.ToList(),
            VerificationChecks = item.VerificationChecks.ToList(),
            QuickFixActions = [item.Title],
            DeepFixActions = item.RequiresAdmin ? [item.Title] : [],
            Tier = item.RequiresAdmin ? RepairTier.AdminDeepFix : RepairTier.SafeUser,
            RiskLevel = item.RequiresAdmin ? RiskLevel.Moderate : RiskLevel.Low,
            RequiresAdmin = item.RequiresAdmin,
            SupportsRestorePoint = item.RequiresAdmin,
            SupportsRollback = CatalogProjection.GetRollbackScript(fix) is not null,
            WhatWillHappen = item.Description,
            Fix = fix,
        };
    }
}

public sealed class MergedFixCatalogService : IFixCatalogService
{
    private readonly List<FixCategory> _categories;
    private readonly List<FixBundle> _bundles;
    private readonly Dictionary<string, FixItem> _index;
    private readonly Dictionary<string, string> _catByFixId;

    public IReadOnlyList<FixCategory> Categories => _categories;
    public IReadOnlyList<FixBundle> Bundles => _bundles;

    public MergedFixCatalogService(IEnumerable<IFixCatalogProvider> providers)
    {
        _categories = MergeCategories(providers.SelectMany(p => p.Categories));
        _bundles = providers.SelectMany(p => p.Bundles)
            .GroupBy(b => b.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(b => b.Title)
            .ToList();
        _index = _categories.SelectMany(c => c.Fixes).ToDictionary(f => f.Id, StringComparer.OrdinalIgnoreCase);
        _catByFixId = _categories
            .SelectMany(c => c.Fixes.Select(f => new KeyValuePair<string, string>(f.Id, c.Title)))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    public FixItem? GetById(string id) => _index.TryGetValue(id, out var fix) ? fix : null;

    public string GetCategoryTitle(FixItem fix) =>
        _catByFixId.TryGetValue(fix.Id, out var title) ? title : "";

    public IReadOnlyList<FixItem> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var normalizedQuery = query.Trim().ToLowerInvariant();
        return _categories
            .SelectMany(c => c.Fixes)
            .Select(fix => new { Fix = fix, Score = ScoreFix(fix, normalizedQuery) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Fix.Title)
            .Take(30)
            .Select(x => x.Fix)
            .ToList()
            .AsReadOnly();
    }

    private static List<FixCategory> MergeCategories(IEnumerable<FixCategory> categories)
    {
        var merged = new List<FixCategory>();
        foreach (var group in categories.GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase))
        {
            var template = group.First();
            var fixes = group.SelectMany(c => c.Fixes)
                .GroupBy(f => f.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(f => f.Title)
                .ToList();

            merged.Add(new FixCategory
            {
                Id = template.Id,
                Title = template.Title,
                Icon = template.Icon,
                Fixes = fixes
            });
        }

        return merged.OrderBy(c => c.Title).ToList();
    }

    private static int ScoreFix(FixItem fix, string query)
    {
        var score = 0;
        var queryTokens = query.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in queryTokens)
        {
            score += ScoreText(fix.Title, token, 18, 8, 4);
            score += fix.Keywords.Sum(keyword => ScoreText(keyword, token, 14, 6, 3));
            score += fix.Tags.Sum(tag => ScoreText(tag, token, 8, 4, 2));
            score += ScoreText(fix.Description, token, 6, 3, 1);
        }

        if (fix.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 30;
        if (fix.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 12;

        return score;
    }

    private static int ScoreText(string source, string token, int exact, int contains, int fuzzy)
    {
        if (string.Equals(source, token, StringComparison.OrdinalIgnoreCase))
            return exact;
        if (source.Contains(token, StringComparison.OrdinalIgnoreCase))
            return contains;
        return IsFuzzyMatch(source, token) ? fuzzy : 0;
    }

    private static bool IsFuzzyMatch(string source, string token)
    {
        var words = source.Split(new[] { ' ', '-', '/', '&', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Any(word => word.Length >= 4 && token.Length >= 4 && Levenshtein(word.ToLowerInvariant(), token.ToLowerInvariant()) <= 1);
    }

    private static int Levenshtein(string a, string b)
    {
        var buffer = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            buffer[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            var previousDiagonal = buffer[0];
            buffer[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var temp = buffer[j];
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                buffer[j] = Math.Min(Math.Min(buffer[j] + 1, buffer[j - 1] + 1), previousDiagonal + cost);
                previousDiagonal = temp;
            }
        }

        return buffer[b.Length];
    }
}

public sealed class RepairCatalogService : IRepairCatalogService
{
    public IReadOnlyList<MasterCategoryDefinition> MasterCategories { get; }
    public IReadOnlyList<RepairDefinition> Repairs { get; }

    public RepairCatalogService(IEnumerable<IFixCatalogProvider> providers)
    {
        var providerMasterCategories = providers.SelectMany(p => p.MasterCategories).ToList();
        Repairs = providers.SelectMany(p => p.Repairs)
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.Title)
            .ToList()
            .AsReadOnly();

        MasterCategories = providerMasterCategories
            .Concat(Repairs
                .GroupBy(r => r.MasterCategoryId, StringComparer.OrdinalIgnoreCase)
                .Where(g => !providerMasterCategories.Any(c => string.Equals(c.Id, g.Key, StringComparison.OrdinalIgnoreCase)))
                .Select(g => new MasterCategoryDefinition
                {
                    Id = g.Key,
                    Title = g.Key,
                    Description = $"Repairs for {g.Key}.",
                    DefaultRecommendedRepairs = g.Take(3).Select(r => r.Id).ToList()
                }))
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(c => c.DisplayPriority)
            .ThenBy(c => c.Title)
            .ToList()
            .AsReadOnly();
    }

    public RepairDefinition? GetRepair(string id) =>
        Repairs.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<RepairDefinition> GetRepairsByCategory(string categoryId) =>
        Repairs.Where(r => string.Equals(r.MasterCategoryId, categoryId, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
}

public sealed class WeightedTriageEngine : ITriageEngine
{
    private readonly IRepairCatalogService _repairCatalog;
    private readonly IRepairHistoryService _historyService;

    public WeightedTriageEngine(IRepairCatalogService repairCatalog, IRepairHistoryService historyService)
    {
        _repairCatalog = repairCatalog;
        _historyService = historyService;
    }

    public TriageResult Analyze(string query, TriageContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new TriageResult { Query = query };

        var normalizedQuery = query.Trim().ToLowerInvariant();
        var tokens = normalizedQuery.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

        var scoredRepairs = _repairCatalog.Repairs
            .Select(repair => new
            {
                Repair = repair,
                Score = ScoreRepair(repair, normalizedQuery, tokens, context),
                Reasons = GetReasons(repair, normalizedQuery, tokens, context)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Repair.Title)
            .ToList();

        var candidates = scoredRepairs
            .GroupBy(x => x.Repair.MasterCategoryId, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var best = group.First();
                var category = _repairCatalog.MasterCategories.FirstOrDefault(c => string.Equals(c.Id, group.Key, StringComparison.OrdinalIgnoreCase));
                var score = Math.Min(100, best.Score);
                var label = score >= 75 ? "High confidence"
                    : score >= 60 ? "Likely"
                    : score >= 45 ? "Possible"
                    : "Low confidence";
                var reasons = best.Reasons.Take(3).ToList();
                var probableSubIssue = best.Repair.SupportedSubIssues.FirstOrDefault()
                    ?? best.Repair.SearchPhrases.FirstOrDefault()
                    ?? "General support issue";

                return new TriageCandidate
                {
                    CategoryId = group.Key,
                    CategoryName = category?.Title ?? best.Repair.MasterCategoryId,
                    ProbableSubIssue = probableSubIssue,
                    ConfidenceScore = score,
                    ConfidenceLabel = label,
                    WhatIThinkIsWrong = best.Repair.ShortDescription,
                    WhyIThinkThat = reasons.Count == 0
                        ? "The language in your symptom lines up with this repair family."
                        : string.Join(" ", reasons),
                    WhatWillHappen = best.Repair.WhatWillHappen,
                    AdvancedDetails = $"{best.Repair.Tier} repair. {best.Repair.AdvancedNotes}",
                    RecommendDiagnosticsFirst = score < 45,
                    RecommendedFixIds = group.Take(3).Select(x => x.Repair.Id).ToList()
                };
            })
            .OrderByDescending(c => c.ConfidenceScore)
            .Take(3)
            .ToList();

        return new TriageResult
        {
            Query = query,
            Candidates = candidates
        };
    }

    private int ScoreRepair(RepairDefinition repair, string query, string[] tokens, TriageContext? context)
    {
        var score = 0;

        foreach (var phrase in repair.SearchPhrases.Concat(repair.Synonyms))
            score += ScorePhrase(phrase, query, tokens);

        score += ScorePhrase(repair.Title, query, tokens) * 2;
        score += ScorePhrase(repair.ShortDescription, query, tokens);

        if (context is not null)
        {
            if (context.PendingRebootDetected && repair.MasterCategoryId is "updates" or "performance")
                score += 8;
            if (context.HasBattery && repair.MasterCategoryId is "maintenance" or "sleep")
                score += 6;
            if (context.NetworkType.Contains("Wi", StringComparison.OrdinalIgnoreCase) && repair.MasterCategoryId is "network" or "remote")
                score += 8;
            if (context.HasRecentFailures && _historyService.Entries.Any(e => string.Equals(e.CategoryId, repair.MasterCategoryId, StringComparison.OrdinalIgnoreCase)))
                score += 6;

            foreach (var symptom in context.RecentSymptoms)
                score += ScorePhrase(symptom, query, tokens) / 2;
        }

        score += _historyService.Entries
            .Where(e => string.Equals(e.CategoryId, repair.MasterCategoryId, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Count() * 3;

        return score;
    }

    private static List<string> GetReasons(RepairDefinition repair, string query, string[] tokens, TriageContext? context)
    {
        var reasons = new List<string>();

        if (repair.SearchPhrases.Any(phrase => query.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
            reasons.Add("I found a direct phrase match in your symptom.");

        if (repair.Synonyms.Any(phrase => tokens.Any(token => phrase.Contains(token, StringComparison.OrdinalIgnoreCase))))
            reasons.Add("I matched related wording and support synonyms.");

        if (context?.PendingRebootDetected == true && repair.MasterCategoryId is "updates" or "performance")
            reasons.Add("Your device context suggests a pending restart may be involved.");

        if (context?.HasRecentFailures == true)
            reasons.Add("You have recent issue history in this area, so I boosted it.");

        return reasons;
    }

    private static int ScorePhrase(string source, string query, string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(source))
            return 0;

        var score = 0;
        if (string.Equals(source, query, StringComparison.OrdinalIgnoreCase))
            score += 35;
        if (source.Contains(query, StringComparison.OrdinalIgnoreCase) || query.Contains(source, StringComparison.OrdinalIgnoreCase))
            score += 20;

        var sourceTokens = source.Split(new[] { ' ', '-', '/', '&', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (sourceTokens.Any(t => string.Equals(t, token, StringComparison.OrdinalIgnoreCase)))
                score += 8;
            else if (sourceTokens.Any(t => t.Contains(token, StringComparison.OrdinalIgnoreCase)))
                score += 4;
            else if (sourceTokens.Any(t => t.Length >= 4 && token.Length >= 4 && Levenshtein(t.ToLowerInvariant(), token.ToLowerInvariant()) <= 1))
                score += 3;
        }

        return score;
    }

    private static int Levenshtein(string a, string b)
    {
        var buffer = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            buffer[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            var previousDiagonal = buffer[0];
            buffer[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var temp = buffer[j];
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                buffer[j] = Math.Min(Math.Min(buffer[j] + 1, buffer[j - 1] + 1), previousDiagonal + cost);
                previousDiagonal = temp;
            }
        }

        return buffer[b.Length];
    }
}

public sealed class RunbookCatalogService : IRunbookCatalogService
{
    public IReadOnlyList<RunbookDefinition> Runbooks { get; }

    public RunbookCatalogService(IEnumerable<IFixCatalogProvider> providers, IRepairCatalogService repairCatalog)
    {
        var runbooks = providers.SelectMany(p => p.Runbooks).ToList();
        runbooks.AddRange(BuildCoreRunbooks(repairCatalog));

        Runbooks = runbooks
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.Title)
            .ToList()
            .AsReadOnly();
    }

    public RunbookDefinition? GetRunbook(string id) =>
        Runbooks.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<RunbookDefinition> BuildCoreRunbooks(IRepairCatalogService repairCatalog)
    {
        var knownIds = repairCatalog.Repairs.Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        yield return BuildRunbook(
            "slow-pc-runbook",
            "Fix Slow PC",
            "Low disk, startup pressure, and cache cleanup in one guided support flow.",
            "performance",
            ["clear-temp-files", "run-disk-cleanup", "disable-startup-apps"],
            knownIds);

        yield return BuildRunbook(
            "browser-problem-runbook",
            "Fix Browser Problem",
            "Connectivity, cache, and browser health checks for web issues.",
            "apps",
            ["flush-dns", "clear-browser-cache", "run-network-diag"],
            knownIds);

        yield return BuildRunbook(
            "work-from-home-runbook",
            "Fix Work From Home Access",
            "Internet, VPN, and internal connectivity checks for remote work.",
            "remote",
            ["flush-dns", "renew-ip", "fix-vpn-disconnect", "full-network-reset"],
            knownIds);
    }

    private static RunbookDefinition BuildRunbook(
        string id,
        string title,
        string description,
        string categoryId,
        IEnumerable<string> fixIds,
        ISet<string> knownIds)
    {
        var steps = fixIds
            .Where(knownIds.Contains)
            .Select((fixId, index) => new RunbookStepDefinition
            {
                Id = $"{id}-step-{index + 1}",
                Title = fixId,
                Description = $"Run {fixId}.",
                StepKind = RunbookStepKind.Repair,
                LinkedRepairId = fixId,
                StopOnFailure = true,
                PostStepMessage = "FixFox captures the result before moving to the next step."
            })
            .ToList();

        return new RunbookDefinition
        {
            Id = id,
            Title = title,
            Description = description,
            CategoryId = categoryId,
            RequiresAdmin = steps.Any(),
            SupportsRollback = true,
            SupportsRestorePoint = true,
            MinimumEdition = AppEdition.Pro,
            TriggerHint = description,
            Steps = steps
        };
    }
}

public sealed class StatePersistenceService : IStatePersistenceService
{
    public InterruptedOperationState? Load()
    {
        try
        {
            if (!File.Exists(ProductizationPaths.InterruptedStateFile))
                return null;
            return JsonConvert.DeserializeObject<InterruptedOperationState>(File.ReadAllText(ProductizationPaths.InterruptedStateFile));
        }
        catch
        {
            return null;
        }
    }

    public void Save(InterruptedOperationState state)
    {
        Directory.CreateDirectory(SharedConstants.AppDataDir);
        File.WriteAllText(ProductizationPaths.InterruptedStateFile, JsonConvert.SerializeObject(state, Formatting.Indented));
    }

    public void Clear()
    {
        if (File.Exists(ProductizationPaths.InterruptedStateFile))
            File.Delete(ProductizationPaths.InterruptedStateFile);
    }
}

public sealed class RepairHistoryService : IRepairHistoryService
{
    private readonly List<RepairHistoryEntry> _entries;

    public IReadOnlyList<RepairHistoryEntry> Entries => _entries.AsReadOnly();

    public RepairHistoryService()
    {
        _entries = LoadEntries();
    }

    public void Record(RepairHistoryEntry entry)
    {
        _entries.Insert(0, entry);
        while (_entries.Count > 250)
            _entries.RemoveAt(_entries.Count - 1);

        Directory.CreateDirectory(SharedConstants.AppDataDir);
        File.WriteAllText(ProductizationPaths.RepairHistoryFile, JsonConvert.SerializeObject(_entries, Formatting.Indented));
    }

    private static List<RepairHistoryEntry> LoadEntries()
    {
        try
        {
            if (File.Exists(ProductizationPaths.RepairHistoryFile))
                return JsonConvert.DeserializeObject<List<RepairHistoryEntry>>(File.ReadAllText(ProductizationPaths.RepairHistoryFile)) ?? [];
        }
        catch
        {
        }

        return [];
    }
}

public sealed class VerificationService : IVerificationService
{
    public async Task<VerificationResult> VerifyAsync(FixItem fix, CancellationToken cancellationToken = default)
    {
        if (fix.Id.Contains("dns", StringComparison.OrdinalIgnoreCase)
            || fix.Id.Contains("network", StringComparison.OrdinalIgnoreCase)
            || fix.Id.Contains("vpn", StringComparison.OrdinalIgnoreCase))
        {
            var details = new List<string>();
            var hasAdapter = NetworkInterface.GetAllNetworkInterfaces()
                .Any(n => n.OperationalStatus == OperationalStatus.Up &&
                          n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            details.Add(hasAdapter ? "An active network adapter is present." : "No active network adapter was found.");

            try
            {
                var dns = await Dns.GetHostAddressesAsync("example.com", cancellationToken);
                details.Add(dns.Length > 0 ? "DNS resolution succeeded." : "DNS resolution returned no results.");
            }
            catch
            {
                details.Add("DNS resolution failed.");
            }

            return new VerificationResult
            {
                Status = details.All(d => !d.Contains("failed", StringComparison.OrdinalIgnoreCase) && !d.Contains("No active", StringComparison.OrdinalIgnoreCase))
                    ? VerificationStatus.Passed
                    : VerificationStatus.Failed,
                Summary = "Validated adapter state and DNS resolution.",
                Details = details
            };
        }

        if (fix.Id.Contains("print", StringComparison.OrdinalIgnoreCase) || fix.Id.Contains("spooler", StringComparison.OrdinalIgnoreCase))
        {
            var running = await CheckServiceRunningAsync("Spooler", cancellationToken);
            return new VerificationResult
            {
                Status = running ? VerificationStatus.Passed : VerificationStatus.Failed,
                Summary = running ? "Print spooler is running." : "Print spooler is not running.",
                Details = [running ? "Spooler service reports RUNNING." : "Spooler service did not report RUNNING."]
            };
        }

        if (fix.Id.Contains("audio", StringComparison.OrdinalIgnoreCase))
        {
            var running = await CheckServiceRunningAsync("Audiosrv", cancellationToken);
            return new VerificationResult
            {
                Status = running ? VerificationStatus.Passed : VerificationStatus.Inconclusive,
                Summary = running ? "Windows Audio service is running." : "Windows Audio service could not be confirmed.",
                Details = [running ? "Audiosrv reported RUNNING." : "FixFox could not confirm Audiosrv is running."]
            };
        }

        if (fix.Id.Contains("firewall", StringComparison.OrdinalIgnoreCase))
        {
            var running = await CheckServiceRunningAsync("MpsSvc", cancellationToken);
            return new VerificationResult
            {
                Status = running ? VerificationStatus.Passed : VerificationStatus.Failed,
                Summary = running ? "Windows Firewall service is running." : "Windows Firewall service is not running.",
                Details = [running ? "MpsSvc reported RUNNING." : "MpsSvc did not report RUNNING."]
            };
        }

        if (fix.Id.Contains("defender", StringComparison.OrdinalIgnoreCase) || fix.Id.Contains("virus", StringComparison.OrdinalIgnoreCase))
        {
            var running = await CheckServiceRunningAsync("WinDefend", cancellationToken);
            return new VerificationResult
            {
                Status = running ? VerificationStatus.Passed : VerificationStatus.Inconclusive,
                Summary = running ? "Microsoft Defender service is active." : "FixFox could not confirm Microsoft Defender service state.",
                Details = [running ? "WinDefend reported RUNNING." : "WinDefend was not confirmed as RUNNING."]
            };
        }

        return new VerificationResult
        {
            Status = VerificationStatus.Inconclusive,
            Summary = "No category-specific probe is available for this repair yet.",
            Details = ["The repair completed, but FixFox does not have a trusted post-check for this item yet."]
        };
    }

    private static async Task<bool> CheckServiceRunningAsync(string serviceName, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("sc.exe", $"query {serviceName}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class RollbackService : IRollbackService
{
    private readonly IScriptService _scriptService;

    public RollbackService(IScriptService scriptService)
    {
        _scriptService = scriptService;
    }

    public Task<RollbackInfo> GetRollbackInfoAsync(FixItem fix, CancellationToken cancellationToken = default)
    {
        var script = CatalogProjection.GetRollbackScript(fix);
        return Task.FromResult(new RollbackInfo
        {
            IsAvailable = !string.IsNullOrWhiteSpace(script),
            Summary = script is null
                ? "Rollback is not automated for this repair yet."
                : "FixFox can undo the last supported settings change from this repair family."
        });
    }

    public void TrackSuccessfulRepair(FixItem fix)
    {
        var script = CatalogProjection.GetRollbackScript(fix);
        if (string.IsNullOrWhiteSpace(script))
            return;

        Directory.CreateDirectory(SharedConstants.AppDataDir);
        var bookmark = new RollbackBookmark
        {
            FixId = fix.Id,
            Summary = $"Undo the last change made by {fix.Title}.",
            Script = script
        };
        File.WriteAllText(ProductizationPaths.RollbackFile, JsonConvert.SerializeObject(bookmark, Formatting.Indented));
    }

    public async Task<RollbackResult> RollbackLastAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(ProductizationPaths.RollbackFile))
                return new RollbackResult { Success = false, Summary = "No rollback bookmark is available." };

            var bookmark = JsonConvert.DeserializeObject<RollbackBookmark>(File.ReadAllText(ProductizationPaths.RollbackFile));
            if (bookmark is null || string.IsNullOrWhiteSpace(bookmark.Script))
                return new RollbackResult { Success = false, Summary = "Rollback data was unreadable." };

            var result = await _scriptService.RunAsync(bookmark.Script, requiresAdmin: true);
            return new RollbackResult
            {
                Success = result.Success,
                Summary = result.Success ? bookmark.Summary : $"Rollback failed: {result.Output}"
            };
        }
        catch (Exception ex)
        {
            return new RollbackResult
            {
                Success = false,
                Summary = $"Rollback failed: {ex.Message}"
            };
        }
    }
}

public sealed class RestorePointService : IRestorePointService
{
    private readonly IScriptService _scriptService;

    public RestorePointService(IScriptService scriptService)
    {
        _scriptService = scriptService;
    }

    public async Task<bool> TryCreateRestorePointAsync(FixItem fix, CancellationToken cancellationToken = default)
    {
        if (!fix.RequiresAdmin)
            return false;

        if (!(fix.Id.Contains("reset", StringComparison.OrdinalIgnoreCase)
              || fix.Id.Contains("repair", StringComparison.OrdinalIgnoreCase)
              || fix.Id.Contains("update", StringComparison.OrdinalIgnoreCase)
              || fix.Id.Contains("driver", StringComparison.OrdinalIgnoreCase)
              || fix.Id.Contains("startup", StringComparison.OrdinalIgnoreCase)))
            return false;

        var description = $"FixFox-{fix.Id}";
        var script = $"Checkpoint-Computer -Description '{description}' -RestorePointType 'MODIFY_SETTINGS'";
        var result = await _scriptService.RunAsync(script, requiresAdmin: true);
        return result.Success;
    }
}

public sealed class ErrorReportingService : IErrorReportingService
{
    public void Report(ErrorReportRecord record)
    {
        Directory.CreateDirectory(SharedConstants.AppDataDir);
        File.AppendAllText(ProductizationPaths.ErrorReportFile, JsonConvert.SerializeObject(record) + Environment.NewLine);
    }
}

public sealed class RepairExecutionService : IRepairExecutionService
{
    private readonly IScriptService _scriptService;
    private readonly IFixCatalogService _catalog;
    private readonly IVerificationService _verificationService;
    private readonly IRollbackService _rollbackService;
    private readonly IRestorePointService _restorePointService;
    private readonly IStatePersistenceService _statePersistenceService;
    private readonly IRepairHistoryService _repairHistoryService;
    private readonly IErrorReportingService _errorReportingService;

    public RepairExecutionService(
        IScriptService scriptService,
        IFixCatalogService catalog,
        IVerificationService verificationService,
        IRollbackService rollbackService,
        IRestorePointService restorePointService,
        IStatePersistenceService statePersistenceService,
        IRepairHistoryService repairHistoryService,
        IErrorReportingService errorReportingService)
    {
        _scriptService = scriptService;
        _catalog = catalog;
        _verificationService = verificationService;
        _rollbackService = rollbackService;
        _restorePointService = restorePointService;
        _statePersistenceService = statePersistenceService;
        _repairHistoryService = repairHistoryService;
        _errorReportingService = errorReportingService;
    }

    public async Task<RepairExecutionResult> ExecuteAsync(FixItem fix, string userQuery = "", CancellationToken cancellationToken = default)
    {
        var interruptedState = new InterruptedOperationState
        {
            OperationType = "repair",
            DisplayTitle = fix.Title,
            CurrentStepId = fix.Id,
            RequiresAdmin = fix.RequiresAdmin,
            Summary = $"FixFox was running {fix.Title}."
        };
        _statePersistenceService.Save(interruptedState);

        var restorePointCreated = await _restorePointService.TryCreateRestorePointAsync(fix, cancellationToken);

        try
        {
            await _scriptService.RunFixAsync(fix);
            var verification = await _verificationService.VerifyAsync(fix, cancellationToken);
            var rollback = await _rollbackService.GetRollbackInfoAsync(fix, cancellationToken);
            if (fix.Status == FixStatus.Success)
                _rollbackService.TrackSuccessfulRepair(fix);

            _repairHistoryService.Record(new RepairHistoryEntry
            {
                Query = userQuery,
                CategoryId = _catalog.GetCategoryTitle(fix),
                CategoryName = _catalog.GetCategoryTitle(fix),
                FixId = fix.Id,
                FixTitle = fix.Title,
                Success = fix.Status == FixStatus.Success,
                VerificationPassed = verification.Status == VerificationStatus.Passed,
                RollbackAvailable = rollback.IsAvailable,
                RequiresAdmin = fix.RequiresAdmin,
                RebootRecommended = fix.Id.Contains("driver", StringComparison.OrdinalIgnoreCase)
                    || fix.Id.Contains("update", StringComparison.OrdinalIgnoreCase),
                Notes = fix.LastOutput ?? ""
            });

            if (fix.Status != FixStatus.Success)
            {
                _errorReportingService.Report(new ErrorReportRecord
                {
                    Category = "repair-failed",
                    FixId = fix.Id,
                    Message = fix.Title,
                    Detail = fix.LastOutput ?? "Repair did not report success."
                });
            }

            _statePersistenceService.Clear();
            return new RepairExecutionResult
            {
                FixId = fix.Id,
                FixTitle = fix.Title,
                Success = fix.Status == FixStatus.Success,
                Output = fix.LastOutput ?? "",
                Verification = verification,
                Rollback = rollback,
                RestorePointAttempted = fix.RequiresAdmin,
                RestorePointCreated = restorePointCreated,
                RebootRecommended = fix.Id.Contains("driver", StringComparison.OrdinalIgnoreCase)
                    || fix.Id.Contains("update", StringComparison.OrdinalIgnoreCase)
            };
        }
        catch (Exception ex)
        {
            _errorReportingService.Report(new ErrorReportRecord
            {
                Category = "repair-exception",
                FixId = fix.Id,
                Message = fix.Title,
                Detail = ex.ToString()
            });
            throw;
        }
    }
}

public sealed class RunbookExecutionService : IRunbookExecutionService
{
    private readonly IFixCatalogService _catalog;
    private readonly IRepairExecutionService _repairExecutionService;
    private readonly IStatePersistenceService _statePersistenceService;
    private readonly IRepairHistoryService _repairHistoryService;

    public RunbookExecutionService(
        IFixCatalogService catalog,
        IRepairExecutionService repairExecutionService,
        IStatePersistenceService statePersistenceService,
        IRepairHistoryService repairHistoryService)
    {
        _catalog = catalog;
        _repairExecutionService = repairExecutionService;
        _statePersistenceService = statePersistenceService;
        _repairHistoryService = repairHistoryService;
    }

    public async Task<RunbookExecutionSummary> ExecuteAsync(
        RunbookDefinition runbook,
        string userQuery = "",
        CancellationToken cancellationToken = default)
    {
        var results = new List<RepairExecutionResult>();
        var completedSteps = 0;

        foreach (var step in runbook.Steps)
        {
            _statePersistenceService.Save(new InterruptedOperationState
            {
                OperationType = "runbook",
                DisplayTitle = runbook.Title,
                CurrentStepId = step.Id,
                RequiresAdmin = runbook.RequiresAdmin,
                RollbackAvailable = runbook.SupportsRollback,
                Summary = $"FixFox was running {runbook.Title} and stopped on {step.Title}."
            });

            if (step.StepKind != RunbookStepKind.Repair || string.IsNullOrWhiteSpace(step.LinkedRepairId))
            {
                completedSteps++;
                continue;
            }

            var fix = _catalog.GetById(step.LinkedRepairId);
            if (fix is null)
            {
                if (step.StopOnFailure)
                    break;
                continue;
            }

            var result = await _repairExecutionService.ExecuteAsync(fix, userQuery, cancellationToken);
            results.Add(result);
            completedSteps++;

            if (!result.Success && step.StopOnFailure)
                break;
        }

        _statePersistenceService.Clear();

        var success = completedSteps == runbook.Steps.Count && results.All(r => r.Success);
        _repairHistoryService.Record(new RepairHistoryEntry
        {
            Query = userQuery,
            CategoryId = runbook.CategoryId,
            CategoryName = runbook.Title,
            RunbookId = runbook.Id,
            Success = success,
            VerificationPassed = results.All(r => r.Verification.Status != VerificationStatus.Failed),
            Notes = $"{completedSteps} of {runbook.Steps.Count} steps completed."
        });

        return new RunbookExecutionSummary
        {
            RunbookId = runbook.Id,
            Title = runbook.Title,
            Success = success,
            CompletedSteps = completedSteps,
            TotalSteps = runbook.Steps.Count,
            Summary = success
                ? $"{runbook.Title} completed successfully."
                : $"{runbook.Title} stopped after {completedSteps} of {runbook.Steps.Count} steps.",
            RepairResults = results
        };
    }
}

public sealed class HealthCheckService : IHealthCheckService
{
    private readonly IQuickScanService _quickScanService;
    private readonly ISystemInfoService _systemInfoService;
    private readonly IRepairHistoryService _repairHistoryService;
    private readonly IStatePersistenceService _statePersistenceService;

    public HealthCheckService(
        IQuickScanService quickScanService,
        ISystemInfoService systemInfoService,
        IRepairHistoryService repairHistoryService,
        IStatePersistenceService statePersistenceService)
    {
        _quickScanService = quickScanService;
        _systemInfoService = systemInfoService;
        _repairHistoryService = repairHistoryService;
        _statePersistenceService = statePersistenceService;
    }

    public async Task<HealthCheckReport> RunFullAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _systemInfoService.GetSnapshotAsync();
        var scanFindings = await _quickScanService.ScanAsync();
        var recommendations = new List<ProactiveRecommendation>();

        if (snapshot.PendingUpdateCount > 0)
        {
            recommendations.Add(new ProactiveRecommendation
            {
                Title = "Pending Windows updates detected",
                Summary = $"{snapshot.PendingUpdateCount} update(s) are waiting and may affect performance or restarts.",
                ActionFixId = "open-windows-update",
                Severity = ScanSeverity.Warning
            });
        }

        if (snapshot.HasBattery && !string.Equals(snapshot.BatteryHealth, "Good", StringComparison.OrdinalIgnoreCase))
        {
            recommendations.Add(new ProactiveRecommendation
            {
                Title = "Battery health needs attention",
                Summary = "Your battery health report suggests reduced capacity.",
                ActionFixId = "generate-battery-report",
                Severity = ScanSeverity.Warning
            });
        }

        if (_repairHistoryService.Entries.Take(5).Count(e => !e.Success) >= 2)
        {
            recommendations.Add(new ProactiveRecommendation
            {
                Title = "Repeated failed repairs detected",
                Summary = "FixFox has seen multiple recent failures and can prepare a handoff bundle.",
                Severity = ScanSeverity.Critical
            });
        }

        var interrupted = _statePersistenceService.Load();
        if (interrupted is not null)
        {
            recommendations.Add(new ProactiveRecommendation
            {
                Title = "Interrupted repair needs review",
                Summary = interrupted.Summary,
                Severity = ScanSeverity.Warning
            });
        }

        recommendations.AddRange(scanFindings
            .Where(f => f.Severity != ScanSeverity.Good)
            .Take(4)
            .Select(f => new ProactiveRecommendation
            {
                Title = f.Title,
                Summary = f.Detail,
                ActionFixId = f.FixId,
                Severity = f.Severity
            }));

        var critical = scanFindings.Count(f => f.Severity == ScanSeverity.Critical);
        var warning = scanFindings.Count(f => f.Severity == ScanSeverity.Warning);
        var overallScore = Math.Max(0, 100 - (critical * 20) - (warning * 8));

        return new HealthCheckReport
        {
            OverallScore = overallScore,
            Summary = critical > 0
                ? "FixFox found issues that need attention."
                : warning > 0 ? "FixFox found a few items worth cleaning up." : "Your device looks healthy right now.",
            Categories =
            [
                new HealthCategoryScore
                {
                    CategoryId = "network",
                    Title = "Network Health",
                    Score = snapshot.InternetReachable ? 92 : 45,
                    Summary = snapshot.InternetReachable ? "Internet reachability looks healthy." : "Internet reachability could not be confirmed."
                },
                new HealthCategoryScore
                {
                    CategoryId = "performance",
                    Title = "Performance Health",
                    Score = (int)Math.Clamp(100 - snapshot.RamUsedPct, 30, 100),
                    Summary = snapshot.RamUsedPct > 85 ? "Memory pressure is high." : "Memory usage is within a healthy range."
                },
                new HealthCategoryScore
                {
                    CategoryId = "security",
                    Title = "Security Health",
                    Score = snapshot.DefenderEnabled ? 90 : 40,
                    Summary = snapshot.DefenderEnabled ? "Defender appears enabled." : "Defender does not appear enabled."
                },
                new HealthCategoryScore
                {
                    CategoryId = "storage",
                    Title = "Storage Health",
                    Score = snapshot.DiskUsedPct > 90 ? 35 : 88,
                    Summary = snapshot.DiskUsedPct > 90 ? "System drive is nearly full." : "System drive has reasonable free space."
                }
            ],
            Recommendations = recommendations,
            ScanFindings = scanFindings.ToList()
        };
    }
}

public sealed class KnowledgeBaseService : IKnowledgeBaseService
{
    public IReadOnlyList<KnowledgeBaseEntry> Entries { get; }

    public KnowledgeBaseService(ISettingsService settingsService)
    {
        var settings = settingsService.Load();
        var path = string.IsNullOrWhiteSpace(settings.KnowledgeBaseConfigPath)
            ? ProductizationPaths.KnowledgeBaseConfigFile
            : settings.KnowledgeBaseConfigPath;

        var defaults = new List<KnowledgeBaseEntry>
        {
            new() { Key = "password-reset", Title = "Password reset", Description = "Open your password reset portal.", Url = "https://support.example.com/password-reset" },
            new() { Key = "mfa-reset", Title = "MFA reset", Description = "Open multifactor reset guidance.", Url = "https://support.example.com/mfa-reset" },
            new() { Key = "vpn-setup", Title = "VPN setup", Description = "Review the approved VPN setup guide.", Url = "https://support.example.com/vpn" },
            new() { Key = "printer-install", Title = "Printer install", Description = "Open printer install steps.", Url = "https://support.example.com/printers" },
            new() { Key = "mapped-drive", Title = "Mapped drive help", Description = "Open mapped drive troubleshooting.", Url = "https://support.example.com/mapped-drives" },
        };

        if (File.Exists(path))
        {
            try
            {
                var document = JsonConvert.DeserializeObject<KnowledgeBaseConfigDocument>(File.ReadAllText(path));
                if (document?.Entries?.Count > 0)
                {
                    Entries = defaults
                        .Concat(document.Entries)
                        .GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.Last())
                        .OrderBy(e => e.Title)
                        .ToList()
                        .AsReadOnly();
                    return;
                }
            }
            catch
            {
            }
        }

        Entries = defaults.AsReadOnly();
    }

    public KnowledgeBaseEntry? Get(string key) =>
        Entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
}

public sealed class BrandingConfigurationService : IBrandingConfigurationService
{
    public BrandingConfiguration Current { get; }

    public BrandingConfigurationService(ISettingsService settingsService)
    {
        var settings = settingsService.Load();
        var path = string.IsNullOrWhiteSpace(settings.BrandingConfigPath)
            ? ProductizationPaths.BrandingConfigFile
            : settings.BrandingConfigPath;

        if (File.Exists(path))
        {
            try
            {
                var document = JsonConvert.DeserializeObject<BrandingConfigDocument>(File.ReadAllText(path));
                if (document is not null)
                {
                    Current = new BrandingConfiguration
                    {
                        AppName = document.AppName,
                        AppSubtitle = document.AppSubtitle,
                        SupportEmail = document.SupportEmail,
                        SupportPortalLabel = document.SupportPortalLabel,
                        SupportPortalUrl = document.SupportPortalUrl,
                        AccentHex = document.AccentHex
                    };
                    return;
                }
            }
            catch
            {
            }
        }

        Current = new BrandingConfiguration();
    }
}

public sealed class EditionCapabilityService : IEditionCapabilityService
{
    private readonly ISettingsService _settingsService;

    public EditionCapabilityService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public EditionCapabilitySnapshot GetSnapshot()
    {
        var edition = _settingsService.Load().Edition;
        return edition switch
        {
            AppEdition.ManagedServiceProvider => new EditionCapabilitySnapshot
            {
                Edition = edition,
                EvidenceBundles = CapabilityState.Available,
                Runbooks = CapabilityState.Available,
                DeepRepairs = CapabilityState.Available,
                WhiteLabelBranding = CapabilityState.Available
            },
            AppEdition.Pro => new EditionCapabilitySnapshot
            {
                Edition = edition,
                EvidenceBundles = CapabilityState.Available,
                Runbooks = CapabilityState.Available,
                DeepRepairs = CapabilityState.Available,
                WhiteLabelBranding = CapabilityState.UpgradeRequired
            },
            _ => new EditionCapabilitySnapshot
            {
                Edition = AppEdition.Basic,
                EvidenceBundles = CapabilityState.UpgradeRequired,
                Runbooks = CapabilityState.UpgradeRequired,
                DeepRepairs = CapabilityState.UpgradeRequired,
                WhiteLabelBranding = CapabilityState.UpgradeRequired
            }
        };
    }
}

public sealed class AppUpdateService : IAppUpdateService
{
    private readonly ISettingsService _settingsService;

    public AppUpdateService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Load();
        if (!settings.CheckForUpdatesOnLaunch)
        {
            return new AppUpdateInfo
            {
                CurrentVersion = SharedConstants.AppVersion,
                LatestVersion = SharedConstants.AppVersion,
                SourceName = "Disabled",
                Summary = "Automatic update checks are turned off."
            };
        }

        var config = LoadConfig(settings);
        if (config is null)
        {
            return new AppUpdateInfo
            {
                CurrentVersion = SharedConstants.AppVersion,
                LatestVersion = SharedConstants.AppVersion,
                SourceName = "Not configured",
                Summary = "No update feed is configured yet."
            };
        }

        if (string.Equals(config.Provider, "github", StringComparison.OrdinalIgnoreCase))
            return await QueryGitHubAsync(config, cancellationToken);

        return new AppUpdateInfo
        {
            CurrentVersion = SharedConstants.AppVersion,
            LatestVersion = SharedConstants.AppVersion,
            SourceName = config.Provider,
            Summary = "The configured update provider is not supported yet."
        };
    }

    private static UpdateConfigDocument? LoadConfig(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.UpdateFeedUrl))
        {
            return new UpdateConfigDocument
            {
                Provider = "manifest",
                FeedUrl = settings.UpdateFeedUrl
            };
        }

        if (!File.Exists(ProductizationPaths.UpdateConfigFile))
            return null;

        try
        {
            return JsonConvert.DeserializeObject<UpdateConfigDocument>(File.ReadAllText(ProductizationPaths.UpdateConfigFile));
        }
        catch
        {
            return null;
        }
    }

    private static async Task<AppUpdateInfo> QueryGitHubAsync(UpdateConfigDocument config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.Owner) || string.IsNullOrWhiteSpace(config.Repository))
        {
            return new AppUpdateInfo
            {
                CurrentVersion = SharedConstants.AppVersion,
                LatestVersion = SharedConstants.AppVersion,
                SourceName = "GitHub Releases",
                Summary = "GitHub update settings are incomplete."
            };
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FixFox");
        var json = await client.GetStringAsync($"https://api.github.com/repos/{config.Owner}/{config.Repository}/releases/latest", cancellationToken);
        dynamic? release = JsonConvert.DeserializeObject(json);
        var latest = (string?)release?.tag_name ?? SharedConstants.AppVersion;
        var normalizedLatest = latest.TrimStart('v', 'V');
        var updateAvailable = Version.TryParse(normalizedLatest, out var latestVersion)
            && Version.TryParse(SharedConstants.AppVersion, out var currentVersion)
            && latestVersion > currentVersion;

        return new AppUpdateInfo
        {
            UpdateAvailable = updateAvailable,
            CurrentVersion = SharedConstants.AppVersion,
            LatestVersion = normalizedLatest,
            SourceName = "GitHub Releases",
            DownloadUrl = (string?)release?.html_url ?? "",
            Summary = updateAvailable ? "A newer FixFox build is available." : "You are on the latest configured version."
        };
    }
}

public sealed class EvidenceBundleService : IEvidenceBundleService
{
    private readonly IRepairHistoryService _repairHistoryService;
    private readonly INotificationService _notificationService;
    private readonly ILogService _logService;
    private readonly ISystemInfoService _systemInfoService;
    private readonly IEditionCapabilityService _editionCapabilityService;

    public EvidenceBundleService(
        IRepairHistoryService repairHistoryService,
        INotificationService notificationService,
        ILogService logService,
        ISystemInfoService systemInfoService,
        IEditionCapabilityService editionCapabilityService)
    {
        _repairHistoryService = repairHistoryService;
        _notificationService = notificationService;
        _logService = logService;
        _systemInfoService = systemInfoService;
        _editionCapabilityService = editionCapabilityService;
    }

    public async Task<EvidenceBundleManifest> ExportAsync(
        string userIssue,
        TriageResult? triageResult,
        HealthCheckReport? healthReport,
        RunbookExecutionSummary? runbookSummary,
        CancellationToken cancellationToken = default)
    {
        var edition = _editionCapabilityService.GetSnapshot();
        if (edition.EvidenceBundles == CapabilityState.UpgradeRequired)
            throw new InvalidOperationException("Evidence bundles are available in Pro or MSP mode.");

        var snapshot = await _systemInfoService.GetSnapshotAsync();
        var folder = Path.Combine(ProductizationPaths.EvidenceDir, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);

        var summaryPath = Path.Combine(folder, "summary.txt");
        var technicalPath = Path.Combine(folder, "technical-details.txt");

        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine("FixFox Help Desk Summary");
        summaryBuilder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        summaryBuilder.AppendLine($"Issue summary: {Redact(userIssue)}");
        if (triageResult?.Candidates.Count > 0)
        {
            var top = triageResult.Candidates[0];
            summaryBuilder.AppendLine($"Top match: {top.CategoryName} ({top.ConfidenceScore}%)");
            summaryBuilder.AppendLine($"What FixFox thinks is wrong: {top.WhatIThinkIsWrong}");
            summaryBuilder.AppendLine($"Why: {top.WhyIThinkThat}");
        }
        if (healthReport is not null)
            summaryBuilder.AppendLine($"Health check: {healthReport.OverallScore}/100 - {healthReport.Summary}");
        if (runbookSummary is not null)
            summaryBuilder.AppendLine($"Runbook: {runbookSummary.Title} - {runbookSummary.Summary}");
        summaryBuilder.AppendLine($"Device: {Redact(snapshot.MachineName)} ({snapshot.OsVersion} build {snapshot.OsBuild})");
        summaryBuilder.AppendLine($"Pending updates: {snapshot.PendingUpdateCount}");
        summaryBuilder.AppendLine($"Recent failed repairs: {_repairHistoryService.Entries.Count(e => !e.Success)}");

        var technicalBuilder = new StringBuilder();
        technicalBuilder.AppendLine("FixFox Technical Details");
        technicalBuilder.AppendLine(JsonConvert.SerializeObject(new
        {
            MachineName = Redact(snapshot.MachineName),
            snapshot.OsVersion,
            snapshot.OsBuild,
            snapshot.WindowsEdition,
            snapshot.NetworkType,
            snapshot.IpAddress,
            snapshot.InternetReachable,
            snapshot.DiskFreeGb,
            snapshot.DiskUsedPct,
            snapshot.RamUsedPct,
            snapshot.PendingUpdateCount,
            snapshot.DefenderEnabled,
            Notifications = _notificationService.All.Take(10).Select(n => new { n.Title, n.Level, n.IsRead }),
            History = _repairHistoryService.Entries.Take(20),
            LegacyLog = _logService.Entries.Take(20),
            Triage = triageResult,
            Health = healthReport,
            Runbook = runbookSummary
        }, Formatting.Indented));

        File.WriteAllText(summaryPath, summaryBuilder.ToString());
        File.WriteAllText(technicalPath, technicalBuilder.ToString());

        return new EvidenceBundleManifest
        {
            SummaryPath = summaryPath,
            TechnicalPath = technicalPath,
            BundleFolder = folder,
            Headline = triageResult?.Candidates.FirstOrDefault()?.CategoryName ?? "FixFox support bundle"
        };
    }

    private static string Redact(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var redacted = value.Replace(Environment.UserName, "<user>", StringComparison.OrdinalIgnoreCase);
        return redacted.Replace(Environment.MachineName, "<device>", StringComparison.OrdinalIgnoreCase);
    }
}
