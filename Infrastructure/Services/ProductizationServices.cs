using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using Microsoft.Win32;
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
    public static string DocsDir => Path.Combine(AppContext.BaseDirectory, "Docs");
    public static string CatalogDir => Path.Combine(AppContext.BaseDirectory, "Catalog", "RepairPacks");
    public static string RepairHistoryFile => Path.Combine(SharedConstants.AppDataDir, "repair-history.json");
    public static string AutomationHistoryFile => Path.Combine(SharedConstants.AppDataDir, "automation-history.json");
    public static string RollbackFile => Path.Combine(SharedConstants.AppDataDir, "rollback.json");
    public static string InterruptedStateFile => Path.Combine(SharedConstants.AppDataDir, "interrupted-operation.json");
    public static string ErrorReportFile => Path.Combine(SharedConstants.AppDataDir, "error-reports.jsonl");
    public static string EvidenceDir => Path.Combine(SharedConstants.AppDataDir, "evidence-bundles");
    public static string BrandingConfigFile => Path.Combine(ConfigurationDir, "branding.json");
    public static string DeploymentConfigFile => Path.Combine(ConfigurationDir, "deployment.json");
    public static string KnowledgeBaseConfigFile => Path.Combine(ConfigurationDir, "knowledge-base.json");
    public static string UpdateConfigFile => Path.Combine(ConfigurationDir, "update.json");
    public static string UpdateManifestFile => Path.Combine(ConfigurationDir, "release-feed.json");

    public static string ResolveFromAppBase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri)
            && (absoluteUri.Scheme == Uri.UriSchemeHttp
                || absoluteUri.Scheme == Uri.UriSchemeHttps
                || absoluteUri.Scheme == "ms-settings"))
        {
            return path;
        }

        if (Path.IsPathRooted(path))
            return path;

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }
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
    public string AppSubtitle { get; set; } = "Windows support and repair workspace";
    public string VendorName { get; set; } = "FixFox";
    public string SupportDisplayName { get; set; } = "FixFox Support";
    public string SupportEmail { get; set; } = "";
    public string SupportPortalLabel { get; set; } = "Open FixFox guides";
    public string SupportPortalUrl { get; set; } = "Docs\\Quick-Start.md";
    public string AccentHex { get; set; } = "#F97316";
    public string LogoPath { get; set; } = "";
    public string ProductTagline { get; set; } = "Explainable Windows support with guided fixes and clean handoff.";
    public string ManagedModeLabel { get; set; } = "Managed build";
}

internal sealed class DeploymentConfigDocument
{
    public bool ManagedMode { get; set; }
    public string OrganizationName { get; set; } = "";
    public string SupportDisplayName { get; set; } = "";
    public string SupportEmail { get; set; } = "";
    public string SupportPortalLabel { get; set; } = "";
    public string SupportPortalUrl { get; set; } = "";
    public string KnowledgeBaseConfigPath { get; set; } = "";
    public string UpdateFeedUrl { get; set; } = "";
    public string EditionOverride { get; set; } = "";
    public string DefaultBehaviorProfile { get; set; } = "";
    public string ForceBehaviorProfile { get; set; } = "";
    public string ForceNotificationMode { get; set; } = "";
    public string ForceLandingPage { get; set; } = "";
    public string ForceSupportBundleExportLevel { get; set; } = "";
    public bool? ForceMinimizeToTray { get; set; }
    public bool? ForceRunAtStartup { get; set; }
    public bool? ForceShowNotifications { get; set; }
    public bool? ForceSafeMaintenanceDefaults { get; set; }
    public bool AllowAdvancedMode { get; set; } = true;
    public bool DisableDeepRepairs { get; set; }
    public bool RestrictTechnicianExports { get; set; }
    public bool HideAdvancedToolbox { get; set; }
    public List<string> DisabledRepairCategories { get; set; } = [];
    public List<string> HiddenToolTitles { get; set; } = [];
    public string ManagedMessage { get; set; } = "";
}

internal sealed class UpdateConfigDocument
{
    public string Provider { get; set; } = "";
    public string FeedUrl { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Repository { get; set; } = "";
    public string ChannelName { get; set; } = "Stable";
}

internal sealed class ReleaseFeedDocument
{
    public string ChannelName { get; set; } = "Stable";
    public string LatestVersion { get; set; } = SharedConstants.AppVersion;
    public string DownloadUrl { get; set; } = "";
    public string ReleaseNotesPath { get; set; } = "";
    public string Summary { get; set; } = "You are on the current FixFox build.";
}

internal static class CatalogProjection
{
    private static readonly HashSet<string> BasicSelfServiceRepairIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "clear-temp-files",
        "empty-recycle-bin",
        "clear-browser-cache-all",
        "restart-spooler",
        "clear-print-queue",
        "set-default-printer",
        "check-defender-status",
        "check-firewall",
        "manage-startup-programs",
        "run-disk-cleanup",
        "optimize-visual-effects",
        "test-connection",
        "flush-dns",
        "renew-ip",
        "full-network-reset",
        "repair-outlook-profile",
        "clear-teams-cache",
        "restart-audio-service",
        "fix-microphone",
        "fix-webcam",
        "fix-stuck-windows-update",
        "run-sfc",
        "run-dism",
        "open-recovery-options"
    };

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
        var tier = DetermineTier(fix);

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

        var verification = BuildVerificationDefinition(category.Id, fix, verificationChecks);
        var relatedRunbooks = GetRelatedRunbooks(category.Id, fix);
        var relatedTools = GetRelatedWindowsTools(category.Id, fix);
        var relatedSettings = GetRelatedWindowsSettings(category.Id, fix);
        var preconditions = BuildPreconditions(category.Id, fix);
        var environmentRequirements = BuildEnvironmentRequirements(category.Id, fix);
        var whySuggested = BuildWhySuggested(category, fix);
        var successNextStep = BuildNextStepOnSuccess(category.Id, fix);
        var failureNextStep = BuildNextStepOnFailure(category.Id, fix);
        var escalationHint = BuildEscalationHint(category.Id, fix);
        var rollbackHint = GetRollbackScript(fix) is not null
            ? "A rollback command is available if you need to undo this specific change."
            : "This repair does not expose an automatic rollback path. Use the support package if you need to document the change before escalating.";

        return new RepairDefinition
        {
            Id = fix.Id,
            Title = fix.Title,
            ShortDescription = fix.Description,
            LongDescription = fix.Description,
            UserProblemSummary = BuildUserProblemSummary(category, fix),
            WhySuggested = whySuggested,
            MasterCategoryId = category.Id,
            SupportedSubIssues = fix.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SearchPhrases = fix.Keywords.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Synonyms = fix.Tags.Concat(category.Title.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ConfidenceBoostSignals = fix.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Diagnostics = new List<string> { $"Inspect {category.Title.ToLowerInvariant()} state relevant to {fix.Title.ToLowerInvariant()}." },
            Preconditions = preconditions,
            EnvironmentRequirements = environmentRequirements,
            QuickFixActions = actions,
            DeepFixActions = fix.RequiresAdmin ? actions : [],
            VerificationChecks = verificationChecks,
            VerificationStrategyId = verification.Strategy.ToString(),
            RelatedWindowsTools = relatedTools,
            RelatedWindowsSettings = relatedSettings,
            RelatedRunbooks = relatedRunbooks,
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
            SuggestedNextStepOnSuccess = successNextStep,
            SuggestedNextStepOnFailure = failureNextStep,
            EscalationHint = escalationHint,
            RollbackHint = rollbackHint,
            EvidenceExportTags = BuildEvidenceTags(category.Id, fix),
            SuppressionScopeHint = BuildSuppressionScopeHint(category.Id),
            AdvancedNotes = fix.HasScript ? "Scripted repair routed through the centralized execution pipeline." : "Guided repair steps are shown to the user.",
            Verification = verification,
            Fix = fix,
        };
    }

    private static RepairTier DetermineTier(FixItem fix)
    {
        if (fix.Type == FixType.Guided)
            return RepairTier.GuidedEscalation;

        if (!fix.RequiresAdmin || BasicSelfServiceRepairIds.Contains(fix.Id))
            return RepairTier.SafeUser;

        return RepairTier.AdminDeepFix;
    }

    private static VerificationDefinition BuildVerificationDefinition(string categoryId, FixItem fix, IReadOnlyList<string> verificationChecks)
    {
        var strategy = categoryId switch
        {
            "network" or "remote" => VerificationStrategyKind.NetworkConnectivity,
            "printers" => VerificationStrategyKind.PrintingQueue,
            "audio" => fix.Id.Contains("camera", StringComparison.OrdinalIgnoreCase)
                || fix.Id.Contains("webcam", StringComparison.OrdinalIgnoreCase)
                ? VerificationStrategyKind.CameraDevices
                : VerificationStrategyKind.AudioDevices,
            "display" => VerificationStrategyKind.DisplayDevices,
            "performance" or "storage" => VerificationStrategyKind.StoragePressure,
            "security" when fix.Id.Contains("firewall", StringComparison.OrdinalIgnoreCase) => VerificationStrategyKind.WindowsFirewall,
            "security" => VerificationStrategyKind.WindowsDefender,
            "updates" => VerificationStrategyKind.WindowsUpdate,
            "browser" => VerificationStrategyKind.BrowserConnectivity,
            "office" or "apps" => VerificationStrategyKind.AppLaunch,
            _ => VerificationStrategyKind.HeuristicFallback
        };

        if (fix.Id.Contains("run-sfc", StringComparison.OrdinalIgnoreCase)
            || fix.Id.Contains("run-dism", StringComparison.OrdinalIgnoreCase))
        {
            strategy = VerificationStrategyKind.ScriptOutput;
        }

        return new VerificationDefinition
        {
            Id = $"{fix.Id}-verify",
            Title = $"{fix.Title} verification",
            Description = $"Post-checks for {fix.Title}.",
            Strategy = strategy,
            PreChecks = [$"Capture pre-state for {categoryId} before running {fix.Title}."],
            PostChecks = verificationChecks.ToList(),
            AllowHeuristicFallback = true
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

    private static string BuildUserProblemSummary(FixCategory category, FixItem fix) => category.Id switch
    {
        "network" => "Use this when the device cannot reach the internet reliably, DNS looks wrong, or adapters need recovery.",
        "remote" => "Use this when remote-work tools, VPN paths, internal resources, or file shares are failing.",
        "browser" => "Use this when websites fail in one browser or web app but Windows itself may still be connected.",
        "performance" => "Use this when the PC feels slow, startup is heavy, or temp/disk pressure is building up.",
        "storage" => "Use this when C: is under pressure, safe cleanup is needed, or downloads clutter needs review.",
        "printers" => "Use this when printing is stuck, the queue will not clear, or the default printer is wrong.",
        "audio" => "Use this when sound, microphone, camera, headset, or meeting-device paths are failing.",
        "display" => "Use this when monitors, docks, resolution, or display routing are unstable.",
        "office" or "apps" => "Use this when Outlook, Teams, Office, or other installed apps fail to launch or keep stale cache.",
        "updates" => "Use this when Windows Update, servicing, SFC, DISM, or recovery prep is the right next path.",
        "files" => "Use this when mapped drives, shares, file paths, or work-resource access are failing.",
        _ => $"Use this when {category.Title.ToLowerInvariant()} issues match the current symptoms."
    };

    private static string BuildWhySuggested(FixCategory category, FixItem fix) =>
        $"FixFox suggests {fix.Title} when {category.Title.ToLowerInvariant()} symptoms line up with {string.Join(", ", fix.Keywords.Take(3).DefaultIfEmpty("this repair family"))}.";

    private static List<string> BuildPreconditions(string categoryId, FixItem fix)
    {
        var checks = new List<string>();
        if (fix.RequiresAdmin)
            checks.Add("Confirm you can approve administrator access before starting.");

        checks.Add(categoryId switch
        {
            "network" or "remote" => "Confirm the device actually has an active network adapter before resetting the stack.",
            "browser" => "Check whether only the browser is affected before resetting broader networking.",
            "printers" => "Confirm the target printer is installed or reachable.",
            "audio" => "Confirm the device is connected and selected in Windows or the meeting app.",
            "display" => "Confirm the cable, dock, or monitor is physically connected.",
            "office" or "apps" => "Save any open app data before clearing cache or resetting state.",
            "updates" => "Save work before running servicing or repair commands that may recommend a reboot.",
            "files" => "Confirm whether the issue is connectivity, credentials, or permissions before changing local settings.",
            _ => "Review the problem summary and save open work before running the repair."
        });
        return checks;
    }

    private static List<string> BuildEnvironmentRequirements(string categoryId, FixItem fix) =>
        categoryId switch
        {
            "network" or "remote" => ["Windows network stack available", "At least one enabled adapter present"],
            "browser" => ["A supported browser is installed", "Internet or intranet path can be tested after cleanup"],
            "storage" or "performance" => ["System drive readable", "Enough free temp space to finish cleanup"],
            "printers" => ["Print spooler service available", "Printer path is still installed or reachable"],
            "audio" => ["Audio or camera device visible to Windows"],
            "display" => ["Display hardware visible to Windows"],
            "office" or "apps" => ["Target app is installed on this device"],
            "updates" => ["Windows servicing stack available"],
            "files" => ["File-share path, VPN path, or saved credentials can be checked"],
            _ => fix.RequiresAdmin ? ["Administrator approval required"] : []
        };

    private static List<string> GetRelatedWindowsTools(string categoryId, FixItem fix) =>
        categoryId switch
        {
            "network" or "remote" => ["Network Adapters", "Wi-Fi", "VPN", "Quick Assist"],
            "browser" => ["Default Apps", "Date & Time", "Get Help"],
            "storage" or "performance" => ["Task Manager", "Storage Settings", "Cleanup Recommendations", "Resource Monitor"],
            "printers" => ["Printers & Scanners", "Services"],
            "audio" => ["Sound Settings", "Microphone Privacy", "Camera Privacy"],
            "display" => ["Display", "Device Manager"],
            "office" or "apps" => ["Installed Apps", "Default Apps", "Quick Assist"],
            "updates" => ["Windows Update", "Recovery Options", "Reliability Monitor"],
            "files" => ["Credential Manager", "VPN", "Quick Assist"],
            _ => []
        };

    private static List<string> GetRelatedWindowsSettings(string categoryId, FixItem fix) =>
        categoryId switch
        {
            "network" or "remote" => ["Wi-Fi", "VPN", "Network Adapters"],
            "browser" => ["Default Apps", "Date & Time"],
            "storage" or "performance" => ["Storage Settings", "Startup Apps", "Power & Battery"],
            "printers" => ["Printers & Scanners"],
            "audio" => ["Sound Settings", "Microphone Privacy", "Camera Privacy"],
            "display" => ["Display", "Bluetooth & Devices"],
            "office" or "apps" => ["Installed Apps", "Default Apps"],
            "updates" => ["Windows Update", "Recovery Options"],
            "files" => ["VPN", "Credential Manager"],
            _ => []
        };

    private static List<string> GetRelatedRunbooks(string categoryId, FixItem fix) =>
        categoryId switch
        {
            "network" => ["internet-recovery-runbook", "work-from-home-runbook"],
            "remote" => ["work-from-home-runbook"],
            "browser" => ["browser-problem-runbook"],
            "performance" => ["slow-pc-runbook", "routine-maintenance-runbook"],
            "storage" => ["disk-full-rescue-runbook", "quick-clean-runbook"],
            "printers" => ["printing-rescue-runbook"],
            "audio" or "display" => ["meeting-device-runbook"],
            "office" or "apps" => ["work-from-home-runbook", "meeting-device-runbook"],
            "updates" => ["windows-repair-runbook"],
            "files" => ["work-from-home-runbook"],
            _ => []
        };

    private static string BuildNextStepOnSuccess(string categoryId, FixItem fix) => categoryId switch
    {
        "network" or "browser" => "Retry the site or service that was failing before you move on to a broader reset.",
        "performance" or "storage" => "Re-check free space and restart the device if performance was affected by long uptime.",
        "printers" => "Print a short test page before declaring the issue closed.",
        "audio" or "display" => "Re-open the meeting or display path that was failing and confirm the device is selected.",
        "office" or "apps" => "Open the target app and confirm sign-in, cache, or launch behavior is stable.",
        "updates" => "Restart if Windows recommends it, then re-check update health before escalating.",
        "files" or "remote" => "Retry the internal path, VPN resource, or mapped drive before escalating to permissions support.",
        _ => "Validate the user’s original problem before closing the issue."
    };

    private static string BuildNextStepOnFailure(string categoryId, FixItem fix) => categoryId switch
    {
        "network" or "remote" => "Move to the network or work-from-home rescue workflow, then collect a support package if connectivity is still broken.",
        "browser" => "Check proxy, date/time, and cert symptoms before escalating browser or account issues.",
        "performance" or "storage" => "Review startup pressure and large files before running deeper cleanup.",
        "printers" => "Confirm the printer path, default printer, and queue health before escalating.",
        "audio" or "display" => "Check Windows device selection, privacy gating, and physical connections before escalating.",
        "office" or "apps" => "Use Installed Apps or the Office/Teams repair path before escalating app failures.",
        "updates" => "Use Windows recovery routes or escalate if servicing still reports corruption.",
        "files" => "Separate access denied from connectivity or credential problems before escalating.",
        _ => "Create a support package if the result is still unclear."
    };

    private static string BuildEscalationHint(string categoryId, FixItem fix) => categoryId switch
    {
        "network" or "remote" => "Escalate if VPN auth, cert, domain, or proxy policy is the real blocker.",
        "browser" => "Escalate if the issue is account, certificate, or site-policy related rather than stale browser state.",
        "printers" => "Escalate if the printer is offline, removed from the server, or blocked by permissions.",
        "audio" or "display" => "Escalate if Windows never detects the device or the dock/driver path keeps dropping.",
        "office" or "apps" => "Escalate if the app profile, license, or sign-in path is still failing after cache repair.",
        "updates" => "Escalate if SFC/DISM still report corruption or recovery is the safer next step.",
        "files" => "Escalate if access denied points to permissions, group policy, or share-side ownership.",
        _ => "Escalate when the repair verifies poorly or the symptom smells like policy, account, or hardware failure."
    };

    private static List<string> BuildEvidenceTags(string categoryId, FixItem fix)
    {
        var tags = new List<string> { categoryId, fix.Id };
        if (fix.RequiresAdmin)
            tags.Add("admin");
        if (fix.Id.Contains("vpn", StringComparison.OrdinalIgnoreCase))
            tags.Add("remote-work");
        if (fix.Id.Contains("browser", StringComparison.OrdinalIgnoreCase))
            tags.Add("browser");
        if (fix.Id.Contains("update", StringComparison.OrdinalIgnoreCase) || fix.Id.Contains("sfc", StringComparison.OrdinalIgnoreCase) || fix.Id.Contains("dism", StringComparison.OrdinalIgnoreCase))
            tags.Add("windows-repair");
        return tags;
    }

    private static string BuildSuppressionScopeHint(string categoryId) => categoryId switch
    {
        "network" or "remote" => "Suppress by network symptom pattern and device context.",
        "storage" or "performance" => "Suppress by device condition and threshold rather than by one-time repair result.",
        "printers" => "Suppress by printer path or repeated spooler symptom.",
        _ => "Suppress by issue pattern only when the user has handled it outside FixFox."
    };
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
    private static readonly Dictionary<string, string[]> CategoryLexicon = new(StringComparer.OrdinalIgnoreCase)
    {
        ["network"] = ["internet", "wifi", "wi-fi", "dns", "browser", "website", "proxy", "slow browsing", "connected", "ethernet", "packet loss"],
        ["remote"] = ["vpn", "remote", "work from home", "mapped drive", "share", "internal", "corporate", "domain", "access denied"],
        ["performance"] = ["slow", "sluggish", "lag", "startup", "disk", "memory", "ram", "temp", "freeze", "cleanup"],
        ["printers"] = ["printer", "printing", "print queue", "spooler", "default printer"],
        ["audio"] = ["audio", "sound", "speaker", "headphones", "camera", "webcam", "microphone", "mic", "teams", "zoom", "display", "monitor", "dock"],
        ["updates"] = ["update", "windows update", "sfc", "dism", "reboot", "pending reboot", "component store"],
        ["security"] = ["defender", "firewall", "virus", "malware", "phishing", "suspicious", "browser extension"],
        ["email"] = ["outlook", "office", "teams", "word", "excel", "app", "launch", "cache", "login"],
        ["files"] = ["drive", "share", "folder", "file", "storage", "disk full", "onedrive", "mapped"]
    };

    private static readonly string[] EscalationTerms =
    [
        "certificate", "cert", "mfa", "token", "permission", "access denied", "account",
        "compromised", "malware", "ransomware", "phishing", "vpn login", "domain"
    ];

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

        var normalizedQuery = Normalize(query);
        var tokens = Tokenize(normalizedQuery);
        var expandedTerms = ExpandTerms(normalizedQuery, tokens);
        var categoryIntent = BuildCategoryIntentScores(normalizedQuery, tokens, expandedTerms);

        var scoredRepairs = _repairCatalog.Repairs
            .Select(repair => EvaluateRepair(repair, normalizedQuery, tokens, expandedTerms, categoryIntent, context))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Repair.Title)
            .ToList();

        var candidates = scoredRepairs
            .GroupBy(x => x.Repair.MasterCategoryId, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ranked = group.ToList();
                var best = ranked[0];
                var category = _repairCatalog.MasterCategories.FirstOrDefault(c => string.Equals(c.Id, group.Key, StringComparison.OrdinalIgnoreCase));
                var secondScore = ranked.Skip(1).FirstOrDefault()?.Score ?? 0;
                var score = Math.Min(100, best.Score);
                var label = score >= 75 ? "High confidence"
                    : score >= 60 ? "Likely"
                    : score >= 45 ? "Possible"
                    : "Low confidence";
                var reasons = best.Reasons.Take(3).ToList();
                var probableSubIssue = best.MatchedSubIssue
                    ?? "General support issue";
                var safestRepair = ranked
                    .Select(x => x.Repair)
                    .OrderBy(r => r.RequiresAdmin)
                    .ThenBy(r => r.RiskLevel)
                    .ThenBy(r => r.Tier)
                    .First();
                var deeperRepair = ranked
                    .Select(x => x.Repair)
                    .FirstOrDefault(r => r.RequiresAdmin || r.RiskLevel != RiskLevel.Low)
                    ?? ranked.Select(x => x.Repair).Skip(1).FirstOrDefault()
                    ?? safestRepair;
                var diagnosticsFirst = score < 65 || (score - secondScore) < 12;

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
                    WhatWillHappen = safestRepair.WhatWillHappen,
                    AdvancedDetails = $"{best.Repair.Tier} repair. {best.Repair.AdvancedNotes}",
                    SafestFirstAction = BuildSafestActionText(safestRepair, diagnosticsFirst),
                    StrongerNextAction = BuildStrongerActionText(safestRepair, deeperRepair),
                    EscalationSignal = BuildEscalationSignal(normalizedQuery, context, best.Repair, score, secondScore),
                    RecommendDiagnosticsFirst = diagnosticsFirst,
                    RecommendedFixIds = ranked.Select(x => x.Repair.Id).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList()
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

    private ScoredRepair EvaluateRepair(
        RepairDefinition repair,
        string query,
        string[] tokens,
        HashSet<string> expandedTerms,
        IReadOnlyDictionary<string, int> categoryIntent,
        TriageContext? context)
    {
        var score = 0;
        var reasons = new List<string>();

        foreach (var phrase in repair.SearchPhrases)
        {
            var phraseScore = ScorePhrase(phrase, query, tokens, expandedTerms);
            if (phraseScore > 0 && reasons.Count < 3)
                reasons.Add($"Matched the phrase '{phrase}'.");
            score += phraseScore;
        }

        foreach (var synonym in repair.Synonyms)
        {
            var synonymScore = ScorePhrase(synonym, query, tokens, expandedTerms);
            if (synonymScore >= 10 && reasons.Count < 3)
                reasons.Add($"Matched related wording around {synonym}.");
            score += synonymScore;
        }

        score += ScorePhrase(repair.Title, query, tokens, expandedTerms) * 2;
        score += ScorePhrase(repair.ShortDescription, query, tokens, expandedTerms);

        var categoryScore = categoryIntent.TryGetValue(repair.MasterCategoryId, out var rawCategoryScore)
            ? rawCategoryScore
            : 0;
        score += categoryScore;

        var strongestOtherCategory = categoryIntent
            .Where(kvp => !string.Equals(kvp.Key, repair.MasterCategoryId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(kvp => kvp.Value)
            .FirstOrDefault();
        if (strongestOtherCategory.Value >= 18 && categoryScore == 0)
        {
            score -= 12;
            if (reasons.Count < 3)
                reasons.Add($"Your wording points more strongly to {strongestOtherCategory.Key} than this category.");
        }

        if (context is not null)
        {
            if (context.PendingRebootDetected && repair.MasterCategoryId is "updates" or "performance")
            {
                score += 8;
                if (reasons.Count < 3)
                    reasons.Add("A pending restart makes update or performance fixes more likely.");
            }
            if (context.HasBattery && repair.MasterCategoryId is "maintenance" or "sleep")
                score += 6;
            if (context.NetworkType.Contains("Wi", StringComparison.OrdinalIgnoreCase) && repair.MasterCategoryId is "network" or "remote")
            {
                score += 8;
                if (reasons.Count < 3)
                    reasons.Add("Current network context matches wireless or remote-work issues.");
            }
            if (!context.InternetReachable && repair.MasterCategoryId is "network" or "remote")
                score += 12;
            if (context.DiskFreeGb > 0 && context.DiskFreeGb <= 10 && repair.MasterCategoryId is "performance" or "updates" or "files")
                score += 12;
            if (context.RamUsedPct >= 80 && repair.MasterCategoryId is "performance" or "email")
                score += 8;
            if (context.HasRecentFailures && _historyService.Entries.Any(e => string.Equals(e.CategoryId, repair.MasterCategoryId, StringComparison.OrdinalIgnoreCase)))
            {
                score += 6;
                if (reasons.Count < 3)
                    reasons.Add("Recent failures in the same area increased the ranking.");
            }

            foreach (var symptom in context.RecentSymptoms)
                score += ScorePhrase(symptom, repair.Title, tokens, expandedTerms) / 2;
        }

        var recentHistory = _historyService.Entries.Take(8).ToList();
        score += recentHistory
            .Where(e => string.Equals(e.CategoryId, repair.MasterCategoryId, StringComparison.OrdinalIgnoreCase))
            .Count() * 3;

        if (recentHistory.Any(e => !e.Success && string.Equals(e.FixId, repair.Id, StringComparison.OrdinalIgnoreCase)))
        {
            score -= 6;
            if (reasons.Count < 3)
                reasons.Add("This exact repair failed recently, so FixFox is being more cautious.");
        }

        var matchedSubIssue = repair.SupportedSubIssues
            .Concat(repair.SearchPhrases)
            .FirstOrDefault(item => ScorePhrase(item, query, tokens, expandedTerms) >= 10);

        return new ScoredRepair(repair, Math.Max(0, score), reasons, matchedSubIssue);
    }

    private IReadOnlyDictionary<string, int> BuildCategoryIntentScores(string query, string[] tokens, HashSet<string> expandedTerms)
    {
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in _repairCatalog.MasterCategories)
        {
            var lexiconTerms = CategoryLexicon.TryGetValue(category.Id, out var mappedTerms)
                ? mappedTerms
                : [];
            var sourceTerms = category.CommonUserPhrases
                .Concat(category.SymptomPatterns)
                .Concat(lexiconTerms)
                .Append(category.Title)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            var score = sourceTerms.Sum(term => ScorePhrase(term, query, tokens, expandedTerms));
            if (score > 0)
                scores[category.Id] = score;
        }

        return scores;
    }

    private static string BuildSafestActionText(RepairDefinition repair, bool diagnosticsFirst)
    {
        if (diagnosticsFirst)
            return $"Start with {repair.Title} so FixFox can confirm the condition before a deeper change.";

        return repair.RequiresAdmin
            ? $"{repair.Title} is the best first action once you approve admin access."
            : $"{repair.Title} is the safest first action because it is low-risk and directly matches the symptom.";
    }

    private static string BuildStrongerActionText(RepairDefinition safestRepair, RepairDefinition deeperRepair)
    {
        if (string.Equals(safestRepair.Id, deeperRepair.Id, StringComparison.OrdinalIgnoreCase))
            return "If the first action does not help, move to the broader repair path or collect evidence for escalation.";

        return $"{deeperRepair.Title} is the stronger next step if the safer action does not improve the issue.";
    }

    private static string BuildEscalationSignal(string query, TriageContext? context, RepairDefinition repair, int score, int secondScore)
    {
        if (EscalationTerms.Any(term => query.Contains(term, StringComparison.OrdinalIgnoreCase)))
            return "Escalate if the issue involves sign-in, certificates, permissions, or suspicious activity.";

        if (repair.MasterCategoryId == "remote" && query.Contains("vpn", StringComparison.OrdinalIgnoreCase))
            return "Escalate after basic network checks if the issue looks like VPN authentication, certificate, or internal DNS policy.";

        if (score < 50 || score - secondScore < 8)
            return "Escalate if the diagnostic-first path does not narrow the issue quickly.";

        if (context?.HasRecentFailures == true)
            return "Escalate if the same issue family has already failed recently on this device.";

        return "Escalate if the recommended first action fails or the problem returns immediately.";
    }

    private static int ScorePhrase(string source, string query, string[] tokens, HashSet<string> expandedTerms)
    {
        if (string.IsNullOrWhiteSpace(source))
            return 0;

        source = Normalize(source);
        var score = 0;
        if (string.Equals(source, query, StringComparison.OrdinalIgnoreCase))
            score += 35;
        if (source.Contains(query, StringComparison.OrdinalIgnoreCase) || query.Contains(source, StringComparison.OrdinalIgnoreCase))
            score += 20;

        var sourceTokens = Tokenize(source);
        foreach (var token in tokens.Concat(expandedTerms).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (sourceTokens.Any(t => string.Equals(t, token, StringComparison.OrdinalIgnoreCase)))
                score += 8;
            else if (sourceTokens.Any(t => t.Contains(token, StringComparison.OrdinalIgnoreCase)))
                score += 4;
            else if (sourceTokens.Any(t => t.Length >= 4 && token.Length >= 4 && Levenshtein(t.ToLowerInvariant(), token.ToLowerInvariant()) <= 2))
                score += 3;
        }

        return score;
    }

    private static HashSet<string> ExpandTerms(string query, IEnumerable<string> tokens)
    {
        var expanded = tokens
            .Where(token => token.Length >= 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in CategoryLexicon)
        foreach (var term in pair.Value)
        {
            if (query.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                expanded.Add(pair.Key);
                foreach (var token in Tokenize(term))
                    expanded.Add(token);
            }
        }

        if (query.Contains("connected", StringComparison.OrdinalIgnoreCase) && query.Contains("no internet", StringComparison.OrdinalIgnoreCase))
            expanded.UnionWith(["network", "dns", "internet"]);
        if (query.Contains("slow", StringComparison.OrdinalIgnoreCase) && query.Contains("startup", StringComparison.OrdinalIgnoreCase))
            expanded.UnionWith(["performance", "startup"]);
        if (query.Contains("teams", StringComparison.OrdinalIgnoreCase) || query.Contains("outlook", StringComparison.OrdinalIgnoreCase))
            expanded.UnionWith(["email", "app"]);
        if (query.Contains("camera", StringComparison.OrdinalIgnoreCase) || query.Contains("webcam", StringComparison.OrdinalIgnoreCase))
            expanded.UnionWith(["audio", "camera"]);

        return expanded;
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("wi-fi", "wifi", StringComparison.OrdinalIgnoreCase)
            .Replace("work from home", "wfh", StringComparison.OrdinalIgnoreCase)
            .Replace("can't", "cannot", StringComparison.OrdinalIgnoreCase)
            .Replace("won't", "will not", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] Tokenize(string value) =>
        value.Split(new[] { ' ', ',', '.', '!', '?', ':', ';', '/', '\\', '-', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

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

    private sealed record ScoredRepair(RepairDefinition Repair, int Score, List<string> Reasons, string? MatchedSubIssue);
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
            "quick-clean-runbook",
            "Quick Clean",
            "Clears low-risk clutter fast so the user can recover space without broader system changes.",
            "maintenance",
            "~4 min",
            repairCatalog,
            knownIds,
            RepairStep("quick-clean-runbook", 1, "Clear temp files", "clear-temp-files"),
            RepairStep("quick-clean-runbook", 2, "Empty the Recycle Bin", "empty-recycle-bin", stopOnFailure: false),
            VerificationStep("quick-clean-runbook", 3, "Confirm free space improved"));

        yield return BuildRunbook(
            "disk-full-rescue-runbook",
            "Disk Full Rescue",
            "Starts with safe clutter removal, then escalates to deeper Windows cleanup when the system drive is under real pressure.",
            "storage",
            "~7 min",
            repairCatalog,
            knownIds,
            DiagnosticStep("disk-full-rescue-runbook", 1, "Confirm the system drive is actually the pressure point"),
            RepairStep("disk-full-rescue-runbook", 2, "Clear temp files", "clear-temp-files"),
            RepairStep("disk-full-rescue-runbook", 3, "Empty the Recycle Bin", "empty-recycle-bin", stopOnFailure: false),
            RepairStep("disk-full-rescue-runbook", 4, "Clear browser caches", "clear-browser-cache-all", stopOnFailure: false),
            VerificationStep("disk-full-rescue-runbook", 5, "Re-check free space after cleanup"),
            RepairStep("disk-full-rescue-runbook", 6, "Run Disk Cleanup", "run-disk-cleanup", stopOnFailure: false));

        yield return BuildRunbook(
            "printing-rescue-runbook",
            "Printing Rescue",
            "Stabilizes the spooler, clears stuck queues, and resets default printer routing before the user is sent to deeper printer escalation.",
            "devices",
            "~5 min",
            repairCatalog,
            knownIds,
            DiagnosticStep("printing-rescue-runbook", 1, "Confirm the printer is online and reachable"),
            RepairStep("printing-rescue-runbook", 2, "Restart the print spooler", "restart-spooler", stopOnFailure: false),
            RepairStep("printing-rescue-runbook", 3, "Clear the print queue", "clear-print-queue"),
            VerificationStep("printing-rescue-runbook", 4, "Confirm the queue is clear and the spooler is responding"),
            RepairStep("printing-rescue-runbook", 5, "Review the default printer", "set-default-printer", stopOnFailure: false));

        yield return BuildRunbook(
            "safe-maintenance-runbook",
            "Safe Maintenance Now",
            "Runs a conservative cleanup and core Windows health check without broad resets or aggressive system changes.",
            "maintenance",
            "~6 min",
            repairCatalog,
            knownIds,
            RepairStep("safe-maintenance-runbook", 1, "Clear temp files", "clear-temp-files"),
            RepairStep("safe-maintenance-runbook", 2, "Empty the Recycle Bin", "empty-recycle-bin", stopOnFailure: false),
            RepairStep("safe-maintenance-runbook", 3, "Check Defender status", "check-defender-status", stopOnFailure: false),
            RepairStep("safe-maintenance-runbook", 4, "Check firewall status", "check-firewall", stopOnFailure: false),
            VerificationStep("safe-maintenance-runbook", 5, "Confirm free space and core security are in a healthier state"));

        yield return BuildRunbook(
            "slow-pc-runbook",
            "Slow PC Recovery",
            "Checks storage pressure first, removes temp bloat, then escalates to heavier cleanup only if needed.",
            "performance",
            "~8 min",
            repairCatalog,
            knownIds,
            new RunbookStepDefinition
            {
                Id = "slow-pc-runbook-intro",
                Title = "Confirm the symptom",
                Description = "Verify the problem is general system slowness rather than one specific app or network issue.",
                StepKind = RunbookStepKind.Message,
                PostStepMessage = "If one app is the only problem, route to the app-specific repair path instead."
            },
            RepairStep("slow-pc-runbook", 1, "Check startup pressure", "manage-startup-programs"),
            RepairStep("slow-pc-runbook", 2, "Clear temp bloat", "clear-temp-files"),
            VerificationStep("slow-pc-runbook", 3, "Re-check free space and responsiveness"),
            RepairStep("slow-pc-runbook", 4, "Run Windows cleanup", "run-disk-cleanup"),
            RepairStep("slow-pc-runbook", 5, "Tune visual overhead", "optimize-visual-effects", stopOnFailure: false));

        yield return BuildRunbook(
            "internet-recovery-runbook",
            "Internet Recovery",
            "Starts with adapter and DNS checks, then moves to broader resets only if basic connectivity still fails.",
            "network",
            "~6 min",
            repairCatalog,
            knownIds,
            DiagnosticStep("internet-recovery-runbook", 1, "Capture current network state"),
            RepairStep("internet-recovery-runbook", 2, "Check the internet path", "test-connection", stopOnFailure: false),
            RepairStep("internet-recovery-runbook", 3, "Flush DNS", "flush-dns"),
            VerificationStep("internet-recovery-runbook", 4, "Validate DNS and reachability"),
            RepairStep("internet-recovery-runbook", 5, "Renew the IP lease", "renew-ip"),
            RepairStep("internet-recovery-runbook", 6, "Reset the network stack", "full-network-reset", stopOnFailure: false));

        yield return BuildRunbook(
            "browser-problem-runbook",
            "Browser Recovery",
            "Separates browser cache issues from wider internet issues so the user does not jump straight to network resets.",
            "apps",
            "~5 min",
            repairCatalog,
            knownIds,
            DiagnosticStep("browser-problem-runbook", 1, "Check whether only the browser is affected"),
            RepairStep("browser-problem-runbook", 2, "Validate internet access", "test-connection", stopOnFailure: false),
            RepairStep("browser-problem-runbook", 3, "Flush DNS", "flush-dns", stopOnFailure: false),
            RepairStep("browser-problem-runbook", 4, "Clear browser caches", "clear-browser-cache-all"),
            VerificationStep("browser-problem-runbook", 5, "Confirm browsing works without stale cache or DNS"));

        yield return BuildRunbook(
            "work-from-home-runbook",
            "Work-From-Home Access",
            "Checks internet basics, then moves through VPN and work-app remediation before escalating auth or policy issues.",
            "remote",
            "~9 min",
            repairCatalog,
            knownIds,
            DiagnosticStep("work-from-home-runbook", 1, "Confirm whether internet works outside the VPN"),
            RepairStep("work-from-home-runbook", 2, "Validate internet access", "test-connection", stopOnFailure: false),
            RepairStep("work-from-home-runbook", 3, "Flush DNS", "flush-dns"),
            RepairStep("work-from-home-runbook", 4, "Stabilize VPN session", "fix-vpn-disconnect", stopOnFailure: false),
            RepairStep("work-from-home-runbook", 5, "Clear Teams cache", "clear-teams-cache", stopOnFailure: false),
            VerificationStep("work-from-home-runbook", 6, "Confirm network and remote-work services respond"),
            new RunbookStepDefinition
            {
                Id = "work-from-home-runbook-escalate",
                Title = "Escalate account or certificate failures",
                Description = "If VPN sign-in, cert, MFA, or mapped-drive access still fails, escalate to IT with the support package.",
                StepKind = RunbookStepKind.KnowledgeBase,
                StopOnFailure = false,
                PostStepMessage = "This step exists so FixFox stops acting like cert or policy failures are local PC fixes."
            });

        yield return BuildRunbook(
            "routine-maintenance-runbook",
            "Routine Maintenance",
            "A practical weekly support workflow: free space, clear stale caches, and check core security health.",
            "maintenance",
            "~7 min",
            repairCatalog,
            knownIds,
            RepairStep("routine-maintenance-runbook", 1, "Clear temp files", "clear-temp-files"),
            RepairStep("routine-maintenance-runbook", 2, "Clear browser caches", "clear-browser-cache-all", stopOnFailure: false),
            RepairStep("routine-maintenance-runbook", 3, "Check Defender status", "check-defender-status", stopOnFailure: false),
            RepairStep("routine-maintenance-runbook", 4, "Check firewall status", "check-firewall", stopOnFailure: false),
            VerificationStep("routine-maintenance-runbook", 5, "Confirm the workstation is in a healthier steady state"));

        yield return BuildRunbook(
            "meeting-device-runbook",
            "Meeting Device Recovery",
            "Checks the audio path, microphone, and camera in a sensible order before pushing the user toward app-specific escalation.",
            "devices",
            "~6 min",
            repairCatalog,
            knownIds,
            DiagnosticStep("meeting-device-runbook", 1, "Confirm whether the problem is audio, microphone, camera, or device selection"),
            RepairStep("meeting-device-runbook", 2, "Restart the audio service", "restart-audio-service", stopOnFailure: false),
            RepairStep("meeting-device-runbook", 3, "Check microphone access and defaults", "fix-microphone", stopOnFailure: false),
            RepairStep("meeting-device-runbook", 4, "Check camera access and presence", "fix-webcam", stopOnFailure: false),
            VerificationStep("meeting-device-runbook", 5, "Confirm meeting devices are visible and available to apps"));

        yield return BuildRunbook(
            "windows-repair-runbook",
            "Windows Repair Recovery",
            "Starts with update health, then uses Windows-native integrity repair tools before routing to recovery options.",
            "windows",
            "~12 min",
            repairCatalog,
            knownIds,
            DiagnosticStep("windows-repair-runbook", 1, "Capture update and servicing state"),
            RepairStep("windows-repair-runbook", 2, "Check or reset Windows Update health", "fix-stuck-windows-update", stopOnFailure: false),
            VerificationStep("windows-repair-runbook", 3, "Confirm update services respond"),
            RepairStep("windows-repair-runbook", 4, "Run System File Checker", "run-sfc"),
            RepairStep("windows-repair-runbook", 5, "Run DISM servicing repair", "run-dism", stopOnFailure: false),
            new RunbookStepDefinition
            {
                Id = "windows-repair-runbook-recovery",
                Title = "Route to Windows recovery options when local repair is not enough",
                Description = "Open Recovery options for startup repair, uninstall update, or reset guidance when servicing repair does not restore the system.",
                StepKind = RunbookStepKind.Repair,
                LinkedRepairId = "open-recovery-options",
                StopOnFailure = false,
                PostStepMessage = "Recovery is the next grounded step when update or integrity repair still leaves Windows unstable."
            });
    }

    private static RunbookDefinition BuildRunbook(
        string id,
        string title,
        string description,
        string categoryId,
        string estTime,
        IRepairCatalogService repairCatalog,
        ISet<string> knownIds,
        params RunbookStepDefinition[] requestedSteps)
    {
        var steps = requestedSteps
            .Where(step => step.StepKind != RunbookStepKind.Repair
                || (!string.IsNullOrWhiteSpace(step.LinkedRepairId) && knownIds.Contains(step.LinkedRepairId)))
            .ToList();
        var linkedRepairs = steps
            .Where(step => step.StepKind == RunbookStepKind.Repair && !string.IsNullOrWhiteSpace(step.LinkedRepairId))
            .Select(step => repairCatalog.GetRepair(step.LinkedRepairId!))
            .Where(repair => repair is not null)
            .Cast<RepairDefinition>()
            .ToList();

        return new RunbookDefinition
        {
            Id = id,
            Title = title,
            Description = description,
            CategoryId = categoryId,
            EstTime = estTime,
            RequiresAdmin = linkedRepairs.Any(repair => repair.RequiresAdmin),
            SupportsRollback = linkedRepairs.Any(repair => repair.SupportsRollback),
            SupportsRestorePoint = linkedRepairs.Any(repair => repair.SupportsRestorePoint),
            MinimumEdition = AppEdition.Basic,
            TriggerHint = description,
            Steps = steps
        };
    }

    private static RunbookStepDefinition RepairStep(string runbookId, int index, string title, string fixId, bool stopOnFailure = true) => new()
    {
        Id = $"{runbookId}-repair-{index}",
        Title = title,
        Description = $"Run {fixId}.",
        StepKind = RunbookStepKind.Repair,
        LinkedRepairId = fixId,
        StopOnFailure = stopOnFailure,
        PostStepMessage = "FixFox verifies the outcome before moving to the next step."
    };

    private static RunbookStepDefinition VerificationStep(string runbookId, int index, string title) => new()
    {
        Id = $"{runbookId}-verify-{index}",
        Title = title,
        Description = title,
        StepKind = RunbookStepKind.Verification,
        StopOnFailure = false,
        PostStepMessage = "Verification should confirm the condition improved before you keep going."
    };

    private static RunbookStepDefinition DiagnosticStep(string runbookId, int index, string title) => new()
    {
        Id = $"{runbookId}-diagnostic-{index}",
        Title = title,
        Description = title,
        StepKind = RunbookStepKind.Diagnostic,
        StopOnFailure = false,
        PostStepMessage = "This step keeps the workflow diagnostic-led instead of jumping to broad repairs too early."
    };
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

        Persist();
    }

    public void Clear()
    {
        _entries.Clear();
        Persist();
    }

    private void Persist()
    {
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
    private readonly IRepairCatalogService _repairCatalog;

    public VerificationService(IRepairCatalogService repairCatalog)
    {
        _repairCatalog = repairCatalog;
    }

    public async Task<VerificationResult> VerifyAsync(FixItem fix, CancellationToken cancellationToken = default)
    {
        if (TryVerifyScriptOutput(fix, out var scriptedResult))
            return scriptedResult;

        var definition = _repairCatalog.GetRepair(fix.Id);
        var strategy = definition?.Verification.Strategy ?? VerificationStrategyKind.HeuristicFallback;

        if (strategy != VerificationStrategyKind.HeuristicFallback)
            return await VerifyByStrategyAsync(strategy, fix, cancellationToken);

        if (IsNetworkFix(fix))
            return await VerifyByStrategyAsync(VerificationStrategyKind.NetworkConnectivity, fix, cancellationToken);
        if (IsPrinterFix(fix))
            return await VerifyByStrategyAsync(VerificationStrategyKind.PrintingQueue, fix, cancellationToken);
        if (IsAudioOrCameraFix(fix))
            return await VerifyByStrategyAsync(VerificationStrategyKind.AudioDevices, fix, cancellationToken);
        if (IsDisplayFix(fix))
            return await VerifyByStrategyAsync(VerificationStrategyKind.DisplayDevices, fix, cancellationToken);
        if (IsPerformanceOrStorageFix(fix))
            return await VerifyByStrategyAsync(VerificationStrategyKind.StoragePressure, fix, cancellationToken);
        if (IsFirewallFix(fix))
            return await VerifyByStrategyAsync(VerificationStrategyKind.WindowsFirewall, fix, cancellationToken);
        if (IsDefenderFix(fix))
            return await VerifyByStrategyAsync(VerificationStrategyKind.WindowsDefender, fix, cancellationToken);
        if (IsBrowserOrAppFix(fix))
            return await VerifyByStrategyAsync(VerificationStrategyKind.BrowserConnectivity, fix, cancellationToken);
        if (IsUpdateFix(fix))
            return await VerifyByStrategyAsync(VerificationStrategyKind.WindowsUpdate, fix, cancellationToken);

        return new VerificationResult
        {
            Status = VerificationStatus.Inconclusive,
            Summary = "FixFox completed the repair, but this item still needs a stronger post-check.",
            Details = ["The repair returned control successfully, but there is no trusted verification probe for this path yet."]
        };
    }

    public async Task<string> CapturePrecheckSummaryAsync(FixItem fix, CancellationToken cancellationToken = default)
    {
        var definition = _repairCatalog.GetRepair(fix.Id);
        var strategy = definition?.Verification.Strategy ?? VerificationStrategyKind.HeuristicFallback;

        return strategy switch
        {
            VerificationStrategyKind.NetworkConnectivity =>
                await BuildNetworkPrecheckAsync(cancellationToken),
            VerificationStrategyKind.PrintingQueue =>
                await BuildPrintingPrecheckAsync(cancellationToken),
            VerificationStrategyKind.AudioDevices or VerificationStrategyKind.CameraDevices =>
                await BuildAudioCameraPrecheckAsync(fix, cancellationToken),
            VerificationStrategyKind.StoragePressure =>
                BuildStoragePrecheck(),
            VerificationStrategyKind.WindowsUpdate =>
                await BuildServicePrecheckAsync(["wuauserv", "bits", "cryptSvc"], cancellationToken),
            _ => definition is null
                ? "No structured precheck metadata is available for this repair."
                : string.Join(" | ", definition.Verification.PreChecks)
        };
    }

    private static async Task<string> BuildNetworkPrecheckAsync(CancellationToken cancellationToken)
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => n.Name)
            .Take(3)
            .ToList();
        var pingOk = await TryPingAsync("1.1.1.1", cancellationToken);
        return interfaces.Count == 0
            ? $"No active adapters detected before execution. Public reachability {(pingOk ? "worked" : "failed")}."
            : $"Active adapters before execution: {string.Join(", ", interfaces)}. Public reachability {(pingOk ? "worked" : "failed")}.";
    }

    private static async Task<string> BuildPrintingPrecheckAsync(CancellationToken cancellationToken)
    {
        var spoolerRunning = await CheckServiceRunningAsync("Spooler", cancellationToken);
        var queuePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "spool", "PRINTERS");
        var queueCount = Directory.Exists(queuePath) ? Directory.GetFiles(queuePath).Length : 0;
        return $"Spooler {(spoolerRunning ? "running" : "not running")} before execution. Queue files: {queueCount}.";
    }

    private static async Task<string> BuildAudioCameraPrecheckAsync(FixItem fix, CancellationToken cancellationToken)
    {
        if (fix.Id.Contains("camera", StringComparison.OrdinalIgnoreCase) || fix.Id.Contains("webcam", StringComparison.OrdinalIgnoreCase))
        {
            var cameras = await RunProcessCaptureAsync("powershell", "-NoProfile -Command \"(Get-PnpDevice -Class Camera,Image -ErrorAction SilentlyContinue | Measure-Object).Count\"", cancellationToken);
            return $"Camera devices before execution: {cameras.Trim()}.";
        }

        var audioRunning = await CheckServiceRunningAsync("Audiosrv", cancellationToken);
        return $"Windows Audio {(audioRunning ? "running" : "not running")} before execution.";
    }

    private static string BuildStoragePrecheck()
    {
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
        var freeGb = Math.Round(drive.AvailableFreeSpace / 1024d / 1024d / 1024d, 1);
        return $"System drive free space before execution: {freeGb:N1} GB.";
    }

    private static async Task<string> BuildServicePrecheckAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken)
    {
        var states = new List<string>();
        foreach (var service in serviceNames)
        {
            var running = await CheckServiceRunningAsync(service, cancellationToken);
            states.Add($"{service}={(running ? "running" : "stopped")}");
        }
        return string.Join(" | ", states);
    }

    private static Task<VerificationResult> VerifyByStrategyAsync(VerificationStrategyKind strategy, FixItem fix, CancellationToken cancellationToken) =>
        strategy switch
        {
            VerificationStrategyKind.NetworkConnectivity => VerifyNetworkAsync(fix, cancellationToken),
            VerificationStrategyKind.PrintingQueue => VerifyPrintingAsync(fix, cancellationToken),
            VerificationStrategyKind.AudioDevices or VerificationStrategyKind.CameraDevices => VerifyAudioCameraAsync(fix, cancellationToken),
            VerificationStrategyKind.DisplayDevices => VerifyDisplayAsync(fix, cancellationToken),
            VerificationStrategyKind.StoragePressure => VerifyPerformanceAsync(fix, cancellationToken),
            VerificationStrategyKind.WindowsFirewall => VerifyFirewallAsync(cancellationToken),
            VerificationStrategyKind.WindowsDefender => VerifyDefenderAsync(cancellationToken),
            VerificationStrategyKind.BrowserConnectivity or VerificationStrategyKind.AppLaunch => VerifyBrowserOrAppAsync(fix, cancellationToken),
            VerificationStrategyKind.WindowsUpdate => VerifyWindowsUpdateAsync(cancellationToken),
            _ => Task.FromResult(new VerificationResult
            {
                Status = VerificationStatus.Inconclusive,
                Summary = "No explicit verification strategy was available for this repair.",
                Details = ["FixFox needs either a typed verification strategy or a stronger fallback probe for this path."]
            })
        };

    private static bool TryVerifyScriptOutput(FixItem fix, out VerificationResult result)
    {
        var output = fix.LastOutput ?? string.Empty;
        if (fix.Id.Contains("run-sfc", StringComparison.OrdinalIgnoreCase))
        {
            if (output.Contains("did not find any integrity violations", StringComparison.OrdinalIgnoreCase)
                || output.Contains("successfully repaired", StringComparison.OrdinalIgnoreCase))
            {
                result = new VerificationResult
                {
                    Status = VerificationStatus.Passed,
                    Summary = "System File Checker finished and reported that system file integrity was restored.",
                    Details = ["SFC reported a healthy or repaired Windows component state."]
                };
                return true;
            }

            if (output.Contains("could not perform", StringComparison.OrdinalIgnoreCase)
                || output.Contains("unable to fix", StringComparison.OrdinalIgnoreCase))
            {
                result = new VerificationResult
                {
                    Status = VerificationStatus.Failed,
                    Summary = "SFC did not finish cleanly, so FixFox cannot treat this repair as successful.",
                    Details = ["Review the SFC output and route to DISM or escalation if corruption remains."]
                };
                return true;
            }
        }

        if (fix.Id.Contains("run-dism", StringComparison.OrdinalIgnoreCase))
        {
            if (output.Contains("restore operation completed successfully", StringComparison.OrdinalIgnoreCase)
                || output.Contains("the operation completed successfully", StringComparison.OrdinalIgnoreCase))
            {
                result = new VerificationResult
                {
                    Status = VerificationStatus.Passed,
                    Summary = "DISM completed successfully and reported a repaired Windows image.",
                    Details = ["The component store repair completed without a blocking error."]
                };
                return true;
            }

            if (output.Contains("source files could not be found", StringComparison.OrdinalIgnoreCase)
                || output.Contains("error:", StringComparison.OrdinalIgnoreCase))
            {
                result = new VerificationResult
                {
                    Status = VerificationStatus.Failed,
                    Summary = "DISM did not complete successfully.",
                    Details = ["The Windows image may still be unhealthy or require installation media / WSUS access."]
                };
                return true;
            }
        }

        result = new VerificationResult();
        return false;
    }

    private static bool IsNetworkFix(FixItem fix) =>
        fix.Id.Contains("dns", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("network", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("vpn", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("proxy", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("internet", StringComparison.OrdinalIgnoreCase);

    private static bool IsPrinterFix(FixItem fix) =>
        fix.Id.Contains("print", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("printer", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("spooler", StringComparison.OrdinalIgnoreCase);

    private static bool IsAudioOrCameraFix(FixItem fix) =>
        fix.Id.Contains("audio", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("sound", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("webcam", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("camera", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("microphone", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("mic", StringComparison.OrdinalIgnoreCase);

    private static bool IsDisplayFix(FixItem fix) =>
        fix.Id.Contains("display", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("monitor", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("screen", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("dock", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("dpi", StringComparison.OrdinalIgnoreCase);

    private static bool IsPerformanceOrStorageFix(FixItem fix) =>
        fix.Id.Contains("temp", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("cleanup", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("disk", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("startup", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("ssd", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("memory", StringComparison.OrdinalIgnoreCase);

    private static bool IsFirewallFix(FixItem fix) =>
        fix.Id.Contains("firewall", StringComparison.OrdinalIgnoreCase);

    private static bool IsDefenderFix(FixItem fix) =>
        fix.Id.Contains("defender", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("virus", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("malware", StringComparison.OrdinalIgnoreCase);

    private static bool IsBrowserOrAppFix(FixItem fix) =>
        fix.Id.Contains("browser", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("edge", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("teams", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("outlook", StringComparison.OrdinalIgnoreCase);

    private static bool IsUpdateFix(FixItem fix) =>
        fix.Id.Contains("update", StringComparison.OrdinalIgnoreCase)
        || fix.Id.Contains("wu", StringComparison.OrdinalIgnoreCase);

    private static async Task<VerificationResult> VerifyNetworkAsync(FixItem fix, CancellationToken cancellationToken)
    {
        var details = new List<string>();
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToList();

        details.Add(interfaces.Count > 0
            ? $"Active adapters: {string.Join(", ", interfaces.Select(i => i.Name).Take(3))}."
            : "No active network adapter was found.");

        var hasGateway = interfaces.Any(i => i.GetIPProperties().GatewayAddresses.Any(g => g.Address is not null && !g.Address.Equals(IPAddress.Any)));
        details.Add(hasGateway ? "A default gateway is present." : "No default gateway was found.");

        var dnsOk = false;
        try
        {
            var dns = await Dns.GetHostAddressesAsync("example.com", cancellationToken);
            dnsOk = dns.Length > 0;
        }
        catch
        {
        }
        details.Add(dnsOk ? "DNS resolution succeeded." : "DNS resolution failed.");

        var pingOk = await TryPingAsync("1.1.1.1", cancellationToken);
        details.Add(pingOk ? "Public network reachability succeeded." : "Public reachability could not be confirmed.");

        var vpnAdapterPresent = interfaces.Any(IsVpnLikeAdapter);
        if (fix.Id.Contains("vpn", StringComparison.OrdinalIgnoreCase))
        {
            details.Add(vpnAdapterPresent
                ? "A VPN-style adapter is present."
                : "No VPN-style adapter is currently active; auth or certificate issues may still require escalation.");
        }

        var status = interfaces.Count == 0
            ? VerificationStatus.Failed
            : dnsOk || pingOk
                ? VerificationStatus.Passed
                : VerificationStatus.Inconclusive;

        return new VerificationResult
        {
            Status = status,
            Summary = status switch
            {
                VerificationStatus.Passed => "Network verification found an active adapter and at least one working reachability signal.",
                VerificationStatus.Failed => "Network verification could not confirm a usable adapter path.",
                _ => "FixFox confirmed some local network state, but external reachability is still uncertain."
            },
            Details = details
        };
    }

    private static async Task<VerificationResult> VerifyPrintingAsync(FixItem fix, CancellationToken cancellationToken)
    {
        var spoolerRunning = await CheckServiceRunningAsync("Spooler", cancellationToken);
        var queuePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "spool", "PRINTERS");
        var queueCount = Directory.Exists(queuePath)
            ? Directory.GetFiles(queuePath).Length
            : 0;
        var details = new List<string>
        {
            spoolerRunning ? "Spooler service reports RUNNING." : "Spooler service is not running.",
            queueCount == 0 ? "No stuck jobs remain in the print queue." : $"{queueCount} file(s) still remain in the print queue."
        };

        if (fix.Id.Contains("default", StringComparison.OrdinalIgnoreCase))
        {
            var defaultPrinter = await RunProcessCaptureAsync("powershell", "-NoProfile -Command \"Get-Printer | Where-Object {$_.Default} | Select-Object -First 1 -ExpandProperty Name\"", cancellationToken);
            details.Add(string.IsNullOrWhiteSpace(defaultPrinter)
                ? "No default printer was detected."
                : $"Default printer: {defaultPrinter.Trim()}");
        }

        return new VerificationResult
        {
            Status = spoolerRunning && queueCount == 0 ? VerificationStatus.Passed : VerificationStatus.Failed,
            Summary = spoolerRunning && queueCount == 0
                ? "Printing verification passed: the spooler is running and the queue is clear."
                : "Printing verification failed because the spooler or queue is still unhealthy.",
            Details = details
        };
    }

    private static async Task<VerificationResult> VerifyAudioCameraAsync(FixItem fix, CancellationToken cancellationToken)
    {
        var details = new List<string>();
        var audioRunning = await CheckServiceRunningAsync("Audiosrv", cancellationToken);
        var endpointRunning = await CheckServiceRunningAsync("AudioEndpointBuilder", cancellationToken);
        if (fix.Id.Contains("audio", StringComparison.OrdinalIgnoreCase) || fix.Id.Contains("sound", StringComparison.OrdinalIgnoreCase))
        {
            details.Add(audioRunning ? "Windows Audio is running." : "Windows Audio is not running.");
            details.Add(endpointRunning ? "Audio Endpoint Builder is running." : "Audio Endpoint Builder is not running.");
        }

        if (fix.Id.Contains("camera", StringComparison.OrdinalIgnoreCase) || fix.Id.Contains("webcam", StringComparison.OrdinalIgnoreCase))
        {
            var cameras = await RunProcessCaptureAsync("powershell", "-NoProfile -Command \"(Get-PnpDevice -Class Camera,Image -ErrorAction SilentlyContinue | Measure-Object).Count\"", cancellationToken);
            var privacy = ReadRegistryValue(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam", "Value");
            details.Add(int.TryParse(cameras.Trim(), out var cameraCount) && cameraCount > 0
                ? $"Camera devices detected: {cameraCount}."
                : "No camera devices were detected.");
            details.Add(string.Equals(privacy, "Deny", StringComparison.OrdinalIgnoreCase)
                ? "Camera privacy is still set to Deny."
                : $"Camera privacy state: {privacy ?? "Unknown"}.");
        }

        if (fix.Id.Contains("microphone", StringComparison.OrdinalIgnoreCase) || fix.Id.Contains("mic", StringComparison.OrdinalIgnoreCase))
        {
            var privacy = ReadRegistryValue(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone", "Value");
            details.Add(string.Equals(privacy, "Deny", StringComparison.OrdinalIgnoreCase)
                ? "Microphone privacy is still set to Deny."
                : $"Microphone privacy state: {privacy ?? "Unknown"}.");
        }

        var failed = details.Any(detail => detail.Contains("not running", StringComparison.OrdinalIgnoreCase) || detail.Contains("No camera devices", StringComparison.OrdinalIgnoreCase) || detail.Contains("Deny", StringComparison.OrdinalIgnoreCase));
        return new VerificationResult
        {
            Status = failed ? VerificationStatus.Inconclusive : VerificationStatus.Passed,
            Summary = failed
                ? "Audio or meeting-device verification still needs manual confirmation."
                : "Audio and meeting-device checks look healthy.",
            Details = details
        };
    }

    private static async Task<VerificationResult> VerifyDisplayAsync(FixItem fix, CancellationToken cancellationToken)
    {
        var monitorCountText = await RunProcessCaptureAsync("powershell", "-NoProfile -Command \"(Get-PnpDevice -Class Monitor -ErrorAction SilentlyContinue | Where-Object {$_.Status -eq 'OK'} | Measure-Object).Count\"", cancellationToken);
        var monitorCount = int.TryParse(monitorCountText.Trim(), out var parsedCount) ? parsedCount : 0;
        var details = new List<string>
        {
            monitorCount > 0 ? $"Detected monitors: {monitorCount}." : "No healthy monitor entries were detected.",
            fix.Id.Contains("dock", StringComparison.OrdinalIgnoreCase)
                ? "Dock-related display failures still need cable, firmware, and GPU-driver checks if monitors do not return."
                : "Display verification confirms Windows still sees monitor hardware."
        };

        return new VerificationResult
        {
            Status = monitorCount > 0 ? VerificationStatus.Passed : VerificationStatus.Inconclusive,
            Summary = monitorCount > 0
                ? "Display verification found monitor hardware after the repair."
                : "Display verification could not confirm monitor hardware.",
            Details = details
        };
    }

    private static Task<VerificationResult> VerifyPerformanceAsync(FixItem fix, CancellationToken cancellationToken)
    {
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
        var freeGb = Math.Round(drive.AvailableFreeSpace / 1024d / 1024d / 1024d, 1);
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var details = new List<string>
        {
            $"System drive free space: {freeGb:N1} GB.",
            $"Current uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m."
        };

        if (fix.Id.Contains("startup", StringComparison.OrdinalIgnoreCase))
            details.Add("Startup optimizations still need a reboot before the user will feel the change.");

        return Task.FromResult(new VerificationResult
        {
            Status = freeGb > 5 ? VerificationStatus.Passed : VerificationStatus.Inconclusive,
            Summary = freeGb > 5
                ? "Performance/storage verification found usable free space after the repair."
                : "The system is still under storage pressure, so performance improvements may be limited.",
            Details = details
        });
    }

    private static async Task<VerificationResult> VerifyFirewallAsync(CancellationToken cancellationToken)
    {
        var running = await CheckServiceRunningAsync("MpsSvc", cancellationToken);
        return new VerificationResult
        {
            Status = running ? VerificationStatus.Passed : VerificationStatus.Failed,
            Summary = running ? "Windows Firewall service is running." : "Windows Firewall service is not running.",
            Details = [running ? "MpsSvc reported RUNNING." : "MpsSvc did not report RUNNING."]
        };
    }

    private static async Task<VerificationResult> VerifyDefenderAsync(CancellationToken cancellationToken)
    {
        var running = await CheckServiceRunningAsync("WinDefend", cancellationToken);
        var details = new List<string>
        {
            running ? "WinDefend reported RUNNING." : "Microsoft Defender service was not confirmed as RUNNING."
        };

        var statusOutput = await RunProcessCaptureAsync("powershell", "-NoProfile -Command \"$s=Get-MpComputerStatus -ErrorAction SilentlyContinue; if($s){$s.RealTimeProtectionEnabled.ToString() + '|' + $s.AntivirusSignatureAge}else{'Unavailable'}\"", cancellationToken);
        if (!string.IsNullOrWhiteSpace(statusOutput))
            details.Add($"Defender state: {statusOutput.Trim()}");

        return new VerificationResult
        {
            Status = running ? VerificationStatus.Passed : VerificationStatus.Inconclusive,
            Summary = running ? "Microsoft Defender is active." : "FixFox could not fully confirm Microsoft Defender state.",
            Details = details
        };
    }

    private static async Task<VerificationResult> VerifyBrowserOrAppAsync(FixItem fix, CancellationToken cancellationToken)
    {
        var details = new List<string>();
        if (fix.Id.Contains("teams", StringComparison.OrdinalIgnoreCase))
        {
            var teamsRunning = Process.GetProcessesByName("ms-teams").Any() || Process.GetProcessesByName("Teams").Any();
            details.Add(teamsRunning
                ? "Teams is running again after the cache reset."
                : "Teams is not running; launch it once to confirm sign-in and meeting devices.");
        }

        if (fix.Id.Contains("browser", StringComparison.OrdinalIgnoreCase) || fix.Id.Contains("edge", StringComparison.OrdinalIgnoreCase))
        {
            var pingOk = await TryPingAsync("1.1.1.1", cancellationToken);
            details.Add(pingOk
                ? "The network path is reachable, so lingering browser issues are more likely cache, proxy, or extension related."
                : "Network reachability still cannot be confirmed, so the browser may not be the only issue.");
        }

        if (fix.Id.Contains("outlook", StringComparison.OrdinalIgnoreCase))
            details.Add("Outlook process cleanup completed; reopen Outlook to confirm the profile and add-ins load correctly.");

        return new VerificationResult
        {
            Status = details.Any(d => d.Contains("cannot be confirmed", StringComparison.OrdinalIgnoreCase))
                ? VerificationStatus.Inconclusive
                : VerificationStatus.Passed,
            Summary = "App-specific verification completed with the checks FixFox can safely run automatically.",
            Details = details
        };
    }

    private static async Task<VerificationResult> VerifyWindowsUpdateAsync(CancellationToken cancellationToken)
    {
        var details = new List<string>();
        var services = new[] { "wuauserv", "bits", "cryptSvc" };
        foreach (var service in services)
        {
            var running = await CheckServiceRunningAsync(service, cancellationToken);
            details.Add(running ? $"{service} is running." : $"{service} is not running.");
        }

        return new VerificationResult
        {
            Status = details.All(d => d.Contains("is running", StringComparison.OrdinalIgnoreCase))
                ? VerificationStatus.Passed
                : VerificationStatus.Inconclusive,
            Summary = "Windows Update verification checked the core servicing services.",
            Details = details
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

    private static bool IsVpnLikeAdapter(NetworkInterface networkInterface) =>
        networkInterface.Name.Contains("vpn", StringComparison.OrdinalIgnoreCase)
        || networkInterface.Description.Contains("vpn", StringComparison.OrdinalIgnoreCase)
        || networkInterface.Description.Contains("wireguard", StringComparison.OrdinalIgnoreCase)
        || networkInterface.Description.Contains("anyconnect", StringComparison.OrdinalIgnoreCase)
        || networkInterface.Description.Contains("globalprotect", StringComparison.OrdinalIgnoreCase)
        || networkInterface.Description.Contains("forti", StringComparison.OrdinalIgnoreCase)
        || networkInterface.Description.Contains("tap", StringComparison.OrdinalIgnoreCase);

    private static async Task<bool> TryPingAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 3000);
            cancellationToken.ThrowIfCancellationRequested();
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> RunProcessCaptureAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(output) ? error : output;
    }

    private static string? ReadRegistryValue(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(subKey);
            return key?.GetValue(valueName)?.ToString();
        }
        catch
        {
            return null;
        }
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
    private readonly IRepairCatalogService _repairCatalogService;
    private readonly IVerificationService _verificationService;
    private readonly IRollbackService _rollbackService;
    private readonly IRestorePointService _restorePointService;
    private readonly IStatePersistenceService _statePersistenceService;
    private readonly IRepairHistoryService _repairHistoryService;
    private readonly IErrorReportingService _errorReportingService;
    private readonly IEditionCapabilityService _editionCapabilityService;

    public RepairExecutionService(
        IScriptService scriptService,
        IFixCatalogService catalog,
        IRepairCatalogService repairCatalogService,
        IVerificationService verificationService,
        IRollbackService rollbackService,
        IRestorePointService restorePointService,
        IStatePersistenceService statePersistenceService,
        IRepairHistoryService repairHistoryService,
        IErrorReportingService errorReportingService,
        IEditionCapabilityService editionCapabilityService)
    {
        _scriptService = scriptService;
        _catalog = catalog;
        _repairCatalogService = repairCatalogService;
        _verificationService = verificationService;
        _rollbackService = rollbackService;
        _restorePointService = restorePointService;
        _statePersistenceService = statePersistenceService;
        _repairHistoryService = repairHistoryService;
        _errorReportingService = errorReportingService;
        _editionCapabilityService = editionCapabilityService;
    }

    public async Task<RepairExecutionResult> ExecuteAsync(FixItem fix, string userQuery = "", CancellationToken cancellationToken = default)
    {
        var precheckFailure = ValidateFixBeforeExecution(fix);
        precheckFailure ??= ValidateFixAvailability(fix);
        if (precheckFailure is not null)
        {
            fix.Status = FixStatus.Failed;
            fix.LastOutput = precheckFailure;
            var blockedReceipt = new RepairHistoryEntry
            {
                Query = userQuery,
                CategoryId = _catalog.GetCategoryTitle(fix),
                CategoryName = _catalog.GetCategoryTitle(fix),
                FixId = fix.Id,
                FixTitle = fix.Title,
                Outcome = ExecutionOutcome.Blocked,
                Success = false,
                VerificationPassed = false,
                RollbackAvailable = false,
                RequiresAdmin = fix.RequiresAdmin,
                Notes = precheckFailure,
                TriggerSource = "User-triggered fix",
                PreStateSummary = "Execution was blocked before any script ran.",
                PostStateSummary = $"{fix.Title} was blocked.",
                VerificationSummary = "No verification ran because the repair was blocked.",
                NextStep = "Use a different repair path or update the repair definition before retrying.",
                ChangedSummary = "No system changes were made."
            };
            _repairHistoryService.Record(blockedReceipt);
            return new RepairExecutionResult
            {
                FixId = fix.Id,
                FixTitle = fix.Title,
                Outcome = ExecutionOutcome.Blocked,
                Success = false,
                Output = precheckFailure,
                Summary = $"{fix.Title} did not run because FixFox blocked it during prechecks.",
                FailureSummary = precheckFailure,
                NextStep = "Use a different repair path or update the repair definition before retrying.",
                Verification = new VerificationResult
                {
                    Status = VerificationStatus.NotRun,
                    Summary = "Verification did not run because the repair was blocked before execution.",
                    Details = [precheckFailure]
                },
                Rollback = new RollbackInfo { IsAvailable = false, Summary = "No rollback was created because execution never started." }
            };
        }

        var preStateSummary = await _verificationService.CapturePrecheckSummaryAsync(fix, cancellationToken);
        var interruptedState = new InterruptedOperationState
        {
            OperationType = "repair",
            OperationTargetId = fix.Id,
            DisplayTitle = fix.Title,
            CurrentStepId = fix.Id,
            RequiresAdmin = fix.RequiresAdmin,
            Summary = $"FixFox was running {fix.Title}."
        };
        _statePersistenceService.Save(interruptedState);

        var restorePointCreated = false;
        var restorePointAttempted = fix.RequiresAdmin
            && (fix.Id.Contains("reset", StringComparison.OrdinalIgnoreCase)
                || fix.Id.Contains("repair", StringComparison.OrdinalIgnoreCase)
                || fix.Id.Contains("update", StringComparison.OrdinalIgnoreCase)
                || fix.Id.Contains("driver", StringComparison.OrdinalIgnoreCase)
                || fix.Id.Contains("startup", StringComparison.OrdinalIgnoreCase));

        try
        {
            restorePointCreated = await _restorePointService.TryCreateRestorePointAsync(fix, cancellationToken);

            try
            {
                await _scriptService.RunFixAsync(fix);
            }
            catch (Exception ex)
            {
                fix.Status = FixStatus.Failed;
                fix.LastOutput = ex.Message;
            }

            var verification = await _verificationService.VerifyAsync(fix, cancellationToken);
            var rollback = await _rollbackService.GetRollbackInfoAsync(fix, cancellationToken);
            var overallSuccess = fix.Status == FixStatus.Success && verification.Status != VerificationStatus.Failed;
            if (overallSuccess)
                _rollbackService.TrackSuccessfulRepair(fix);

            var summary = BuildRepairSummary(fix, verification, overallSuccess);
            var failureSummary = overallSuccess ? "" : BuildFailureSummary(fix, verification);
            var nextStep = BuildNextStep(fix, verification, rollback, overallSuccess);

            _repairHistoryService.Record(new RepairHistoryEntry
            {
                Query = userQuery,
                CategoryId = _catalog.GetCategoryTitle(fix),
                CategoryName = _catalog.GetCategoryTitle(fix),
                FixId = fix.Id,
                FixTitle = fix.Title,
                Outcome = overallSuccess ? ExecutionOutcome.Completed : ExecutionOutcome.Failed,
                Success = overallSuccess,
                VerificationPassed = verification.Status == VerificationStatus.Passed,
                RollbackAvailable = rollback.IsAvailable,
                RequiresAdmin = fix.RequiresAdmin,
                RebootRecommended = fix.Id.Contains("driver", StringComparison.OrdinalIgnoreCase)
                    || fix.Id.Contains("update", StringComparison.OrdinalIgnoreCase),
                Notes = string.Join(Environment.NewLine, new[] { summary, failureSummary, nextStep, fix.LastOutput ?? string.Empty }
                    .Where(text => !string.IsNullOrWhiteSpace(text))),
                TriggerSource = "User-triggered fix",
                PreStateSummary = $"{preStateSummary} | {(fix.RequiresAdmin ? "Administrator rights required" : "Standard user flow")} | Restore point {(restorePointAttempted ? "considered" : "not needed")}",
                PostStateSummary = summary,
                VerificationSummary = verification.Summary,
                NextStep = nextStep,
                RollbackSummary = rollback.Summary,
                ChangedSummary = string.IsNullOrWhiteSpace(fix.LastOutput) ? "No concrete output was captured." : fix.LastOutput
            });

            if (!overallSuccess)
            {
                _errorReportingService.Report(new ErrorReportRecord
                {
                    Category = verification.Status == VerificationStatus.Failed ? "repair-verification-failed" : "repair-failed",
                    FixId = fix.Id,
                    Message = fix.Title,
                    Detail = string.IsNullOrWhiteSpace(fix.LastOutput) ? failureSummary : fix.LastOutput
                });
            }

            _statePersistenceService.Clear();
            return new RepairExecutionResult
            {
                FixId = fix.Id,
                FixTitle = fix.Title,
                Outcome = overallSuccess ? ExecutionOutcome.Completed : ExecutionOutcome.Failed,
                Success = overallSuccess,
                Output = (fix.LastOutput ?? "").Trim(),
                Summary = summary,
                FailureSummary = failureSummary,
                NextStep = nextStep,
                Verification = verification,
                Rollback = rollback,
                RestorePointAttempted = restorePointAttempted,
                RestorePointCreated = restorePointCreated,
                RebootRecommended = fix.Id.Contains("driver", StringComparison.OrdinalIgnoreCase)
                    || fix.Id.Contains("update", StringComparison.OrdinalIgnoreCase)
            };
        }
        catch (Exception ex)
        {
            _statePersistenceService.Clear();
            _errorReportingService.Report(new ErrorReportRecord
            {
                Category = "repair-exception",
                FixId = fix.Id,
                Message = fix.Title,
                Detail = ex.ToString()
            });
            fix.Status = FixStatus.Failed;
            fix.LastOutput = ex.Message;
            _repairHistoryService.Record(new RepairHistoryEntry
            {
                Query = userQuery,
                CategoryId = _catalog.GetCategoryTitle(fix),
                CategoryName = _catalog.GetCategoryTitle(fix),
                FixId = fix.Id,
                FixTitle = fix.Title,
                Outcome = ExecutionOutcome.Failed,
                Success = false,
                VerificationPassed = false,
                RollbackAvailable = false,
                RequiresAdmin = fix.RequiresAdmin,
                Notes = ex.Message,
                TriggerSource = "User-triggered fix",
                PreStateSummary = "Repair started but ended in an unexpected exception.",
                PostStateSummary = $"{fix.Title} stopped with an exception.",
                VerificationSummary = "Verification did not run because execution threw an exception.",
                NextStep = "Review the error, export a support package, and escalate if the same failure repeats.",
                ChangedSummary = "Execution stopped before FixFox could confirm the final state."
            });
            return new RepairExecutionResult
            {
                FixId = fix.Id,
                FixTitle = fix.Title,
                Outcome = ExecutionOutcome.Failed,
                Success = false,
                Output = ex.Message,
                Summary = $"{fix.Title} stopped with an unexpected error.",
                FailureSummary = ex.Message,
                NextStep = "Review the error, export a support package, and escalate if the same failure repeats.",
                Verification = new VerificationResult
                {
                    Status = VerificationStatus.NotRun,
                    Summary = "Verification did not run because execution failed unexpectedly.",
                    Details = [ex.Message]
                },
                Rollback = new RollbackInfo { IsAvailable = false, Summary = "Rollback availability could not be confirmed after the exception." },
                RestorePointAttempted = restorePointAttempted,
                RestorePointCreated = restorePointCreated
            };
        }
    }

    private string? ValidateFixAvailability(FixItem fix)
    {
        var repair = _repairCatalogService.GetRepair(fix.Id);
        if (repair is null)
            return null;

        var edition = _editionCapabilityService.GetSnapshot().Edition;
        if (edition < repair.MinimumEdition)
            return $"{fix.Title} is available in {FormatEdition(repair.MinimumEdition)}.";

        if (repair.Tier != RepairTier.SafeUser
            && _editionCapabilityService.GetState(ProductCapability.DeepRepairs) != CapabilityState.Available)
        {
            return _editionCapabilityService.Describe(ProductCapability.DeepRepairs).Summary;
        }

        return null;
    }

    private static string? ValidateFixBeforeExecution(FixItem fix)
    {
        if (fix.Type == FixType.Silent && string.IsNullOrWhiteSpace(fix.Script))
            return "Silent repair is missing its script body.";

        if (fix.Type == FixType.Guided && fix.Steps.Count == 0)
            return "Guided repair is missing its step list.";

        return null;
    }

    private static string BuildRepairSummary(FixItem fix, VerificationResult verification, bool success)
    {
        if (success && verification.Status == VerificationStatus.Passed)
            return $"{fix.Title} completed and the post-check passed.";

        if (success)
            return $"{fix.Title} completed, but FixFox could not fully verify the final condition.";

        return $"{fix.Title} did not finish in a state FixFox can treat as successful.";
    }

    private static string BuildFailureSummary(FixItem fix, VerificationResult verification)
    {
        if (verification.Status == VerificationStatus.Failed)
            return verification.Summary;

        return string.IsNullOrWhiteSpace(fix.LastOutput)
            ? "The repair did not report a successful result."
            : fix.LastOutput;
    }

    private static string BuildNextStep(FixItem fix, VerificationResult verification, RollbackInfo rollback, bool success)
    {
        if (success && verification.Status == VerificationStatus.Passed)
            return fix.RequiresAdmin
                ? "No further action is needed unless the issue returns."
                : "If the symptom comes back, move to the deeper repair path or export a support package.";

        if (verification.Status == VerificationStatus.Inconclusive)
            return "Confirm the user-facing symptom improved. If not, move to a broader repair or capture evidence for escalation.";

        if (rollback.IsAvailable)
            return "Use rollback if the repair made the system worse, then escalate with the support package.";

        return "Capture a support package and escalate if the same failure still blocks the user.";
    }

    private static string FormatEdition(AppEdition edition) => edition switch
    {
        AppEdition.ManagedServiceProvider => "the MSP edition",
        AppEdition.Pro => "FixFox Pro",
        _ => "the Basic edition"
    };
}

public sealed class GuidedRepairExecutionService : IGuidedRepairExecutionService
{
    private readonly IScriptService _scriptService;
    private readonly IStatePersistenceService _statePersistenceService;
    private readonly IRepairHistoryService _repairHistoryService;

    public GuidedRepairExecutionService(
        IScriptService scriptService,
        IStatePersistenceService statePersistenceService,
        IRepairHistoryService repairHistoryService)
    {
        _scriptService = scriptService;
        _statePersistenceService = statePersistenceService;
        _repairHistoryService = repairHistoryService;
    }

    public async Task<GuidedRepairExecutionResult> AdvanceAsync(FixItem fix, int stepIndex, string userQuery = "", CancellationToken cancellationToken = default)
    {
        if (stepIndex < 0 || stepIndex >= fix.Steps.Count)
            return BuildTerminalResult(fix, ExecutionOutcome.Blocked, stepIndex, "Guided repair step is out of range.", userQuery);

        var step = fix.Steps[stepIndex];
        _statePersistenceService.Save(new InterruptedOperationState
        {
            OperationType = "guided",
            OperationTargetId = fix.Id,
            DisplayTitle = fix.Title,
            CurrentStepId = step.Id,
            RequiresAdmin = fix.RequiresAdmin,
            Summary = $"FixFox was running the guided repair {fix.Title} on step {step.Title}.",
            Outcome = ExecutionOutcome.Resumable,
            CanResume = true
        });

        if (!string.IsNullOrWhiteSpace(step.Script))
        {
            try
            {
                var result = await _scriptService.RunAsync(step.Script, fix.RequiresAdmin);
                fix.LastOutput = result.Output;
                if (!result.Success)
                {
                    fix.Status = FixStatus.Failed;
                    _statePersistenceService.Save(new InterruptedOperationState
                    {
                        OperationType = "guided",
                        OperationTargetId = fix.Id,
                        DisplayTitle = fix.Title,
                        CurrentStepId = step.Id,
                        RequiresAdmin = fix.RequiresAdmin,
                        Summary = $"FixFox stopped on {step.Title}.",
                        Outcome = ExecutionOutcome.Failed,
                        FailedStepId = step.Id,
                        FailedStepTitle = step.Title,
                        LastOutput = result.Output,
                        CanResume = true
                    });

                    return BuildTerminalResult(fix, ExecutionOutcome.Failed, stepIndex, result.Output, userQuery);
                }
            }
            catch (Exception ex)
            {
                fix.Status = FixStatus.Failed;
                fix.LastOutput = ex.Message;
                _statePersistenceService.Save(new InterruptedOperationState
                {
                    OperationType = "guided",
                    OperationTargetId = fix.Id,
                    DisplayTitle = fix.Title,
                    CurrentStepId = step.Id,
                    RequiresAdmin = fix.RequiresAdmin,
                    Summary = $"FixFox stopped on {step.Title}.",
                    Outcome = ExecutionOutcome.Failed,
                    FailedStepId = step.Id,
                    FailedStepTitle = step.Title,
                    LastOutput = ex.Message,
                    CanResume = true
                });

                return BuildTerminalResult(fix, ExecutionOutcome.Failed, stepIndex, ex.Message, userQuery);
            }
        }

        var nextIndex = stepIndex + 1;
        if (nextIndex >= fix.Steps.Count)
        {
            fix.Status = FixStatus.Success;
            fix.LastOutput = "Guided repair completed.";
            _statePersistenceService.Clear();
            var completedReceipt = BuildReceipt(fix, ExecutionOutcome.Completed, stepIndex, "", userQuery);
            _repairHistoryService.Record(completedReceipt);
            return new GuidedRepairExecutionResult
            {
                FixId = fix.Id,
                FixTitle = fix.Title,
                Outcome = ExecutionOutcome.Completed,
                CurrentStepIndex = stepIndex,
                TotalSteps = fix.Steps.Count,
                CurrentStepId = step.Id,
                CurrentStepTitle = step.Title,
                Output = fix.LastOutput ?? "",
                Summary = $"{fix.Title} completed successfully.",
                NextStep = "Review the result and rerun only if the user-facing symptom remains.",
                Receipt = completedReceipt
            };
        }

        var nextStep = fix.Steps[nextIndex];
        _statePersistenceService.Save(new InterruptedOperationState
        {
            OperationType = "guided",
            OperationTargetId = fix.Id,
            DisplayTitle = fix.Title,
            CurrentStepId = nextStep.Id,
            RequiresAdmin = fix.RequiresAdmin,
            Summary = $"FixFox paused {fix.Title} after {step.Title}.",
            Outcome = ExecutionOutcome.Resumable,
            CanResume = true
        });

        fix.Status = FixStatus.Running;
        return new GuidedRepairExecutionResult
        {
            FixId = fix.Id,
            FixTitle = fix.Title,
            Outcome = ExecutionOutcome.InProgress,
            CurrentStepIndex = nextIndex,
            TotalSteps = fix.Steps.Count,
            CurrentStepId = nextStep.Id,
            CurrentStepTitle = nextStep.Title,
            Output = fix.LastOutput ?? "",
            Summary = $"{step.Title} completed. {nextStep.Title} is next.",
            NextStep = "Continue when you are ready for the next guided step.",
            CanResume = true
        };
    }

    public Task<GuidedRepairExecutionResult> CancelAsync(FixItem fix, int stepIndex, string reason, string userQuery = "", CancellationToken cancellationToken = default)
    {
        fix.Status = FixStatus.Failed;
        fix.LastOutput = reason;
        _statePersistenceService.Save(new InterruptedOperationState
        {
            OperationType = "guided",
            OperationTargetId = fix.Id,
            DisplayTitle = fix.Title,
            CurrentStepId = stepIndex >= 0 && stepIndex < fix.Steps.Count ? fix.Steps[stepIndex].Id : "",
            RequiresAdmin = fix.RequiresAdmin,
            Summary = reason,
            Outcome = ExecutionOutcome.Cancelled,
            LastOutput = reason,
            CanResume = false
        });

        var receipt = BuildReceipt(fix, ExecutionOutcome.Cancelled, stepIndex, reason, userQuery);
        _repairHistoryService.Record(receipt);
        _statePersistenceService.Clear();
        return Task.FromResult(new GuidedRepairExecutionResult
        {
            FixId = fix.Id,
            FixTitle = fix.Title,
            Outcome = ExecutionOutcome.Cancelled,
            CurrentStepIndex = stepIndex,
            TotalSteps = fix.Steps.Count,
            CurrentStepId = stepIndex >= 0 && stepIndex < fix.Steps.Count ? fix.Steps[stepIndex].Id : "",
            CurrentStepTitle = stepIndex >= 0 && stepIndex < fix.Steps.Count ? fix.Steps[stepIndex].Title : "",
            Output = reason,
            Summary = reason,
            NextStep = "Restart the guided repair only if you still want to continue manually.",
            Receipt = receipt
        });
    }

    public GuidedRepairExecutionResult? BuildResumeState(FixItem fix, InterruptedOperationState? state)
    {
        if (state is null
            || !string.Equals(state.OperationType, "guided", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(state.OperationTargetId, fix.Id, StringComparison.OrdinalIgnoreCase))
            return null;

        var index = fix.Steps.FindIndex(step => string.Equals(step.Id, state.CurrentStepId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            index = 0;

        var step = fix.Steps[index];
        return new GuidedRepairExecutionResult
        {
            FixId = fix.Id,
            FixTitle = fix.Title,
            Outcome = state.Outcome,
            CurrentStepIndex = index,
            TotalSteps = fix.Steps.Count,
            CurrentStepId = step.Id,
            CurrentStepTitle = step.Title,
            FailedStepId = state.FailedStepId,
            FailedStepTitle = state.FailedStepTitle,
            Output = state.LastOutput,
            Summary = state.Summary,
            NextStep = state.CanResume
                ? "Resume from the recorded step when you are ready."
                : "Start the guided repair again if you still want to continue.",
            CanResume = state.CanResume
        };
    }

    private GuidedRepairExecutionResult BuildTerminalResult(FixItem fix, ExecutionOutcome outcome, int stepIndex, string detail, string userQuery)
    {
        var receipt = BuildReceipt(fix, outcome, stepIndex, detail, userQuery);
        _repairHistoryService.Record(receipt);
        return new GuidedRepairExecutionResult
        {
            FixId = fix.Id,
            FixTitle = fix.Title,
            Outcome = outcome,
            CurrentStepIndex = stepIndex,
            TotalSteps = fix.Steps.Count,
            CurrentStepId = stepIndex >= 0 && stepIndex < fix.Steps.Count ? fix.Steps[stepIndex].Id : "",
            CurrentStepTitle = stepIndex >= 0 && stepIndex < fix.Steps.Count ? fix.Steps[stepIndex].Title : "",
            FailedStepId = stepIndex >= 0 && stepIndex < fix.Steps.Count ? fix.Steps[stepIndex].Id : "",
            FailedStepTitle = stepIndex >= 0 && stepIndex < fix.Steps.Count ? fix.Steps[stepIndex].Title : "",
            Output = detail,
            Summary = string.IsNullOrWhiteSpace(detail) ? $"{fix.Title} stopped." : $"{fix.Title} stopped on a guided step.",
            NextStep = "Inspect the failed step, retry only if the prerequisite is fixed, or export a support package.",
            CanResume = true,
            Receipt = receipt
        };
    }

    private static RepairHistoryEntry BuildReceipt(FixItem fix, ExecutionOutcome outcome, int stepIndex, string detail, string userQuery) => new()
    {
        Query = userQuery,
        CategoryId = "guided",
        CategoryName = "Guided Repair",
        FixId = fix.Id,
        FixTitle = fix.Title,
        Outcome = outcome,
        Success = outcome == ExecutionOutcome.Completed,
        VerificationPassed = outcome == ExecutionOutcome.Completed,
        RollbackAvailable = false,
        RequiresAdmin = fix.RequiresAdmin,
        Notes = detail,
        TriggerSource = "Guided repair",
        PreStateSummary = $"Guided repair with {fix.Steps.Count} step(s).",
        PostStateSummary = outcome == ExecutionOutcome.Completed ? $"{fix.Title} completed." : $"{fix.Title} stopped before completion.",
        VerificationSummary = outcome == ExecutionOutcome.Completed ? "Required guided steps completed." : "Guided workflow did not complete successfully.",
        NextStep = outcome == ExecutionOutcome.Completed
            ? "Confirm the user-facing symptom improved."
            : "Inspect the failed step and resume only if the prerequisite is fixed.",
        FailedStepId = stepIndex >= 0 && stepIndex < fix.Steps.Count ? fix.Steps[stepIndex].Id : "",
        FailedStepTitle = stepIndex >= 0 && stepIndex < fix.Steps.Count ? fix.Steps[stepIndex].Title : "",
        ChangedSummary = string.IsNullOrWhiteSpace(detail) ? "Guided workflow state changed." : detail
    };
}

public sealed class RunbookExecutionService : IRunbookExecutionService
{
    private readonly IFixCatalogService _catalog;
    private readonly IRepairExecutionService _repairExecutionService;
    private readonly IStatePersistenceService _statePersistenceService;
    private readonly IRepairHistoryService _repairHistoryService;
    private readonly IEditionCapabilityService _editionCapabilityService;

    public RunbookExecutionService(
        IFixCatalogService catalog,
        IRepairExecutionService repairExecutionService,
        IStatePersistenceService statePersistenceService,
        IRepairHistoryService repairHistoryService,
        IEditionCapabilityService editionCapabilityService)
    {
        _catalog = catalog;
        _repairExecutionService = repairExecutionService;
        _statePersistenceService = statePersistenceService;
        _repairHistoryService = repairHistoryService;
        _editionCapabilityService = editionCapabilityService;
    }

    public async Task<RunbookExecutionSummary> ExecuteAsync(
        RunbookDefinition runbook,
        string userQuery = "",
        CancellationToken cancellationToken = default)
    {
        var effectiveEdition = _editionCapabilityService.GetSnapshot().Edition;
        if (effectiveEdition < runbook.MinimumEdition)
        {
            var summary = $"{runbook.Title} is available in {FormatEdition(runbook.MinimumEdition)}.";
            _repairHistoryService.Record(new RepairHistoryEntry
            {
                Query = userQuery,
                CategoryId = runbook.CategoryId,
                CategoryName = runbook.Title,
                RunbookId = runbook.Id,
                Outcome = ExecutionOutcome.Blocked,
                Success = false,
                VerificationPassed = false,
                RollbackAvailable = false,
                Notes = summary,
                TriggerSource = "Guided workflow",
                PreStateSummary = "Workflow blocked before execution.",
                PostStateSummary = summary,
                VerificationSummary = "Workflow did not run.",
                NextStep = "Choose a workflow available in the current edition or use the standard repair path.",
                ChangedSummary = "No workflow steps ran."
            });

            return new RunbookExecutionSummary
            {
                RunbookId = runbook.Id,
                Title = runbook.Title,
                Success = false,
                CompletedSteps = 0,
                TotalSteps = runbook.Steps.Count,
                Summary = summary,
                Timeline = [summary]
            };
        }

        var results = new List<RepairExecutionResult>();
        var timeline = new List<string>();
        var completedSteps = 0;
        var blockedStep = "";

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
                timeline.Add($"{step.Title}: {BuildNonRepairStepMessage(step)}");
                completedSteps++;
                continue;
            }

            var fix = _catalog.GetById(step.LinkedRepairId);
            if (fix is null)
            {
                timeline.Add($"{step.Title}: linked repair '{step.LinkedRepairId}' was not found.");
                if (step.StopOnFailure)
                {
                    blockedStep = step.Title;
                    break;
                }
                continue;
            }

            var result = await _repairExecutionService.ExecuteAsync(fix, userQuery, cancellationToken);
            results.Add(result);
            completedSteps++;
            timeline.Add($"{step.Title}: {result.Summary}");

            if (!result.Success && step.StopOnFailure)
            {
                blockedStep = step.Title;
                break;
            }
        }

        _statePersistenceService.Clear();

        var success = completedSteps == runbook.Steps.Count && results.All(r => r.Success);
        _repairHistoryService.Record(new RepairHistoryEntry
        {
            Query = userQuery,
            CategoryId = runbook.CategoryId,
            CategoryName = runbook.Title,
            RunbookId = runbook.Id,
            Outcome = success ? ExecutionOutcome.Completed : ExecutionOutcome.Failed,
            Success = success,
            VerificationPassed = results.All(r => r.Verification.Status != VerificationStatus.Failed),
            RollbackAvailable = results.Any(r => r.Rollback.IsAvailable),
            Notes = string.Join(Environment.NewLine, timeline),
            TriggerSource = "Guided workflow",
            PreStateSummary = $"{runbook.Steps.Count} workflow step(s) queued.",
            PostStateSummary = success ? $"{runbook.Title} completed." : $"{runbook.Title} stopped before all steps completed.",
            VerificationSummary = results.All(r => r.Verification.Status != VerificationStatus.Failed)
                ? "Workflow verification did not detect a hard failure."
                : "One or more repair steps failed verification.",
            NextStep = success
                ? "Review the result and export a support package only if the issue persists."
                : "Review the blocking step, rerun only what makes sense, or export a support package.",
            RollbackSummary = results.FirstOrDefault(r => r.Rollback.IsAvailable)?.Rollback.Summary ?? "No workflow-level rollback was captured.",
            ChangedSummary = string.Join(" | ", results.Select(r => r.Summary).Where(text => !string.IsNullOrWhiteSpace(text)))
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
                : string.IsNullOrWhiteSpace(blockedStep)
                    ? $"{runbook.Title} stopped after {completedSteps} of {runbook.Steps.Count} steps."
                    : $"{runbook.Title} stopped at '{blockedStep}' after {completedSteps} of {runbook.Steps.Count} steps.",
            Timeline = timeline,
            RepairResults = results
        };
    }

    private static string BuildNonRepairStepMessage(RunbookStepDefinition step) =>
        step.StepKind switch
        {
            RunbookStepKind.Diagnostic => "diagnostic checkpoint completed",
            RunbookStepKind.Verification => "verification checkpoint recorded",
            RunbookStepKind.KnowledgeBase => string.IsNullOrWhiteSpace(step.PostStepMessage) ? "escalation guidance reviewed" : step.PostStepMessage,
            _ => string.IsNullOrWhiteSpace(step.PostStepMessage) ? "workflow note recorded" : step.PostStepMessage
        };

    private static string FormatEdition(AppEdition edition) => edition switch
    {
        AppEdition.ManagedServiceProvider => "the MSP edition",
        AppEdition.Pro => "FixFox Pro",
        _ => "the Basic edition"
    };
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
                Key = "pending-updates",
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
                Key = "battery-health",
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
                Key = "recent-failed-repairs",
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
                Key = "interrupted-repair",
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
                Key = $"scan:{f.FixId ?? f.Title}",
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

    public KnowledgeBaseService(ISettingsService settingsService, IDeploymentConfigurationService deploymentConfigurationService)
    {
        var settings = settingsService.Load();
        var deployment = deploymentConfigurationService.Current;
        var configuredPath = !string.IsNullOrWhiteSpace(deployment.KnowledgeBaseConfigPath)
            ? deployment.KnowledgeBaseConfigPath
            : settings.KnowledgeBaseConfigPath;
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? ProductizationPaths.KnowledgeBaseConfigFile
            : ProductizationPaths.ResolveFromAppBase(configuredPath);

        var defaults = BuildDefaultEntries();

        if (File.Exists(path))
        {
            try
            {
                var document = JsonConvert.DeserializeObject<KnowledgeBaseConfigDocument>(File.ReadAllText(path));
                if (document?.Entries?.Count > 0)
                {
                    Entries = defaults
                        .Concat(document.Entries.Select(entry => new KnowledgeBaseEntry
                        {
                            Key = entry.Key,
                            Title = entry.Title,
                            Description = entry.Description,
                            Url = ProductizationPaths.ResolveFromAppBase(entry.Url)
                        }))
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

    private static List<KnowledgeBaseEntry> BuildDefaultEntries() =>
    [
        new KnowledgeBaseEntry
        {
            Key = "quick-start",
            Title = "Quick Start",
            Description = "Understand the home screen, health checks, repairs, and support packages in a couple of minutes.",
            Url = ProductizationPaths.ResolveFromAppBase("Docs\\Quick-Start.md")
        },
        new KnowledgeBaseEntry
        {
            Key = "support-bundles",
            Title = "Support Packages",
            Description = "See what FixFox includes in a support package before you share it.",
            Url = ProductizationPaths.ResolveFromAppBase("Docs\\Support-Packages.md")
        },
        new KnowledgeBaseEntry
        {
            Key = "privacy-and-data",
            Title = "Privacy and Local Data",
            Description = "Review what FixFox stores locally, what gets redacted, and when to escalate instead.",
            Url = ProductizationPaths.ResolveFromAppBase("Docs\\Privacy-and-Data.md")
        },
        new KnowledgeBaseEntry
        {
            Key = "recovery-and-resume",
            Title = "Recovery and Resume",
            Description = "Learn how interrupted repairs, restarts, and recovery notices behave.",
            Url = ProductizationPaths.ResolveFromAppBase("Docs\\Recovery-and-Resume.md")
        },
        new KnowledgeBaseEntry
        {
            Key = "troubleshooting-and-faq",
            Title = "Troubleshooting and FAQ",
            Description = "Review common launch, update, repair, and support-package questions.",
            Url = ProductizationPaths.ResolveFromAppBase("Docs\\Troubleshooting-and-FAQ.md")
        }
    ];
}

public sealed class DeploymentConfigurationService : IDeploymentConfigurationService
{
    public DeploymentConfiguration Current { get; }

    public DeploymentConfigurationService(ISettingsService settingsService)
    {
        var settings = settingsService.Load();
        var path = string.IsNullOrWhiteSpace(settings.DeploymentConfigPath)
            ? ProductizationPaths.DeploymentConfigFile
            : ProductizationPaths.ResolveFromAppBase(settings.DeploymentConfigPath);

        if (File.Exists(path))
        {
            try
            {
                var document = JsonConvert.DeserializeObject<DeploymentConfigDocument>(File.ReadAllText(path));
                if (document is not null)
                {
                    Current = new DeploymentConfiguration
                    {
                        ManagedMode = document.ManagedMode,
                        OrganizationName = document.OrganizationName,
                        SupportDisplayName = document.SupportDisplayName,
                        SupportEmail = document.SupportEmail,
                        SupportPortalLabel = document.SupportPortalLabel,
                        SupportPortalUrl = ProductizationPaths.ResolveFromAppBase(document.SupportPortalUrl),
                        KnowledgeBaseConfigPath = ProductizationPaths.ResolveFromAppBase(document.KnowledgeBaseConfigPath),
                        UpdateFeedUrl = ProductizationPaths.ResolveFromAppBase(document.UpdateFeedUrl),
                        EditionOverride = Enum.TryParse<AppEdition>(document.EditionOverride, ignoreCase: true, out var edition)
                            ? edition
                            : null,
                        DefaultBehaviorProfile = document.DefaultBehaviorProfile,
                        ForceBehaviorProfile = document.ForceBehaviorProfile,
                        ForceNotificationMode = document.ForceNotificationMode,
                        ForceLandingPage = document.ForceLandingPage,
                        ForceSupportBundleExportLevel = document.ForceSupportBundleExportLevel,
                        ForceMinimizeToTray = document.ForceMinimizeToTray,
                        ForceRunAtStartup = document.ForceRunAtStartup,
                        ForceShowNotifications = document.ForceShowNotifications,
                        ForceSafeMaintenanceDefaults = document.ForceSafeMaintenanceDefaults,
                        AllowAdvancedMode = document.AllowAdvancedMode,
                        DisableDeepRepairs = document.DisableDeepRepairs,
                        RestrictTechnicianExports = document.RestrictTechnicianExports,
                        HideAdvancedToolbox = document.HideAdvancedToolbox,
                        DisabledRepairCategories = document.DisabledRepairCategories,
                        HiddenToolTitles = document.HiddenToolTitles,
                        ManagedMessage = document.ManagedMessage
                    };
                    return;
                }
            }
            catch
            {
            }
        }

        Current = new DeploymentConfiguration();
    }

    public void ApplyPolicy(AppSettings settings)
    {
        if (settings is null)
            return;

        if (!settings.OnboardingDismissed && !string.IsNullOrWhiteSpace(Current.DefaultBehaviorProfile))
            ProductizationPolicies.ApplyBehaviorProfile(settings, Current.DefaultBehaviorProfile);

        if (!string.IsNullOrWhiteSpace(Current.ForceBehaviorProfile))
            ProductizationPolicies.ApplyBehaviorProfile(settings, Current.ForceBehaviorProfile);

        if (!string.IsNullOrWhiteSpace(Current.ForceNotificationMode))
            settings.NotificationMode = Current.ForceNotificationMode;

        if (!string.IsNullOrWhiteSpace(Current.ForceLandingPage))
            settings.DefaultLandingPage = Current.ForceLandingPage;

        if (!string.IsNullOrWhiteSpace(Current.ForceSupportBundleExportLevel))
            settings.SupportBundleExportLevel = Current.ForceSupportBundleExportLevel;

        if (Current.ForceMinimizeToTray.HasValue)
            settings.MinimizeToTray = Current.ForceMinimizeToTray.Value;

        if (Current.ForceRunAtStartup.HasValue)
            settings.RunAtStartup = Current.ForceRunAtStartup.Value;

        if (Current.ForceShowNotifications.HasValue)
            settings.ShowNotifications = Current.ForceShowNotifications.Value;

        if (Current.ForceSafeMaintenanceDefaults.HasValue)
            settings.PreferSafeMaintenanceDefaults = Current.ForceSafeMaintenanceDefaults.Value;

        if (!Current.AllowAdvancedMode)
            settings.AdvancedMode = false;

        if (Current.RestrictTechnicianExports)
            settings.SupportBundleExportLevel = EvidenceExportLevel.Basic.ToString();
    }
}

public sealed class BrandingConfigurationService : IBrandingConfigurationService
{
    public BrandingConfiguration Current { get; }

    public BrandingConfigurationService(
        ISettingsService settingsService,
        IDeploymentConfigurationService deploymentConfigurationService)
    {
        var settings = settingsService.Load();
        var deployment = deploymentConfigurationService.Current;
        var path = string.IsNullOrWhiteSpace(settings.BrandingConfigPath)
            ? ProductizationPaths.BrandingConfigFile
            : ProductizationPaths.ResolveFromAppBase(settings.BrandingConfigPath);

        if (File.Exists(path))
        {
            try
            {
                var document = JsonConvert.DeserializeObject<BrandingConfigDocument>(File.ReadAllText(path));
                if (document is not null)
                {
                    var effectiveEdition = deployment.EditionOverride ?? settings.Edition;
                    var whiteLabelAllowed = effectiveEdition == AppEdition.ManagedServiceProvider || deployment.ManagedMode;
                    Current = new BrandingConfiguration
                    {
                        AppName = whiteLabelAllowed && !string.IsNullOrWhiteSpace(document.AppName) ? document.AppName : SharedConstants.AppName,
                        AppSubtitle = whiteLabelAllowed && !string.IsNullOrWhiteSpace(document.AppSubtitle)
                            ? document.AppSubtitle
                            : "Windows support and repair workspace",
                        VendorName = whiteLabelAllowed && !string.IsNullOrWhiteSpace(document.VendorName)
                            ? document.VendorName
                            : SharedConstants.AppName,
                        SupportDisplayName = !string.IsNullOrWhiteSpace(deployment.SupportDisplayName)
                            ? deployment.SupportDisplayName
                            : !string.IsNullOrWhiteSpace(document.SupportDisplayName) ? document.SupportDisplayName : "FixFox Support",
                        SupportEmail = !string.IsNullOrWhiteSpace(deployment.SupportEmail)
                            ? deployment.SupportEmail
                            : document.SupportEmail,
                        SupportPortalLabel = !string.IsNullOrWhiteSpace(deployment.SupportPortalLabel)
                            ? deployment.SupportPortalLabel
                            : document.SupportPortalLabel,
                        SupportPortalUrl = !string.IsNullOrWhiteSpace(deployment.SupportPortalUrl)
                            ? deployment.SupportPortalUrl
                            : ProductizationPaths.ResolveFromAppBase(document.SupportPortalUrl),
                        AccentHex = document.AccentHex,
                        LogoPath = whiteLabelAllowed && !string.IsNullOrWhiteSpace(document.LogoPath)
                            ? ProductizationPaths.ResolveFromAppBase(document.LogoPath)
                            : "",
                        ProductTagline = string.IsNullOrWhiteSpace(document.ProductTagline)
                            ? "Explainable Windows support with guided fixes and clean handoff."
                            : document.ProductTagline,
                        ManagedModeLabel = string.IsNullOrWhiteSpace(document.ManagedModeLabel)
                            ? "Managed build"
                            : document.ManagedModeLabel
                    };
                    return;
                }
            }
            catch
            {
            }
        }

        Current = new BrandingConfiguration
        {
            SupportDisplayName = string.IsNullOrWhiteSpace(deployment.SupportDisplayName) ? "FixFox Support" : deployment.SupportDisplayName,
            SupportEmail = deployment.SupportEmail,
            SupportPortalLabel = string.IsNullOrWhiteSpace(deployment.SupportPortalLabel) ? "Open FixFox guides" : deployment.SupportPortalLabel,
            SupportPortalUrl = string.IsNullOrWhiteSpace(deployment.SupportPortalUrl)
                ? ProductizationPaths.ResolveFromAppBase("Docs\\Quick-Start.md")
                : deployment.SupportPortalUrl
        };
    }
}

public sealed class EditionCapabilityService : IEditionCapabilityService
{
    private readonly ISettingsService _settingsService;
    private readonly IDeploymentConfigurationService _deploymentConfigurationService;

    public EditionCapabilityService(
        ISettingsService settingsService,
        IDeploymentConfigurationService deploymentConfigurationService)
    {
        _settingsService = settingsService;
        _deploymentConfigurationService = deploymentConfigurationService;
    }

    public EditionCapabilitySnapshot GetSnapshot()
    {
        var settings = _settingsService.Load();
        var deployment = _deploymentConfigurationService.Current;
        var edition = deployment.EditionOverride ?? settings.Edition;

        var snapshot = edition switch
        {
            AppEdition.ManagedServiceProvider => new EditionCapabilitySnapshot
            {
                Edition = edition,
                ManagedMode = deployment.ManagedMode,
                EvidenceBundles = CapabilityState.Available,
                Runbooks = CapabilityState.Available,
                DeepRepairs = CapabilityState.Available,
                AdvancedMode = CapabilityState.Available,
                AdvancedDiagnostics = CapabilityState.Available,
                TechnicianExports = CapabilityState.Available,
                AdvancedAutomation = CapabilityState.Available,
                AdvancedToolbox = CapabilityState.Available,
                AdvancedRecovery = CapabilityState.Available,
                CustomSupportRouting = CapabilityState.Available,
                ManagedPolicies = CapabilityState.Available,
                WhiteLabelBranding = CapabilityState.Available
            },
            AppEdition.Pro => new EditionCapabilitySnapshot
            {
                Edition = edition,
                ManagedMode = deployment.ManagedMode,
                EvidenceBundles = CapabilityState.Available,
                Runbooks = CapabilityState.Available,
                DeepRepairs = CapabilityState.Available,
                AdvancedMode = CapabilityState.Available,
                AdvancedDiagnostics = CapabilityState.Available,
                TechnicianExports = CapabilityState.Available,
                AdvancedAutomation = CapabilityState.Available,
                AdvancedToolbox = CapabilityState.Available,
                AdvancedRecovery = CapabilityState.Available,
                CustomSupportRouting = CapabilityState.UpgradeRequired,
                ManagedPolicies = deployment.ManagedMode ? CapabilityState.Available : CapabilityState.UpgradeRequired,
                WhiteLabelBranding = CapabilityState.UpgradeRequired
            },
            _ => new EditionCapabilitySnapshot
            {
                Edition = AppEdition.Basic,
                ManagedMode = deployment.ManagedMode,
                EvidenceBundles = CapabilityState.Available,
                Runbooks = CapabilityState.Available,
                DeepRepairs = CapabilityState.Available,
                AdvancedMode = CapabilityState.UpgradeRequired,
                AdvancedDiagnostics = CapabilityState.UpgradeRequired,
                TechnicianExports = CapabilityState.UpgradeRequired,
                AdvancedAutomation = CapabilityState.UpgradeRequired,
                AdvancedToolbox = CapabilityState.UpgradeRequired,
                AdvancedRecovery = CapabilityState.UpgradeRequired,
                CustomSupportRouting = deployment.ManagedMode ? CapabilityState.Available : CapabilityState.UpgradeRequired,
                ManagedPolicies = deployment.ManagedMode ? CapabilityState.Available : CapabilityState.UpgradeRequired,
                WhiteLabelBranding = CapabilityState.UpgradeRequired
            }
        };

        return new EditionCapabilitySnapshot
        {
            Edition = snapshot.Edition,
            ManagedMode = snapshot.ManagedMode,
            EvidenceBundles = snapshot.EvidenceBundles,
            Runbooks = snapshot.Runbooks,
            DeepRepairs = ApplyManagedOverride(snapshot.DeepRepairs, deployment.DisableDeepRepairs),
            AdvancedMode = ApplyManagedOverride(snapshot.AdvancedMode, !deployment.AllowAdvancedMode),
            AdvancedDiagnostics = ApplyManagedOverride(snapshot.AdvancedDiagnostics, !deployment.AllowAdvancedMode),
            TechnicianExports = ApplyManagedOverride(snapshot.TechnicianExports, deployment.RestrictTechnicianExports),
            AdvancedAutomation = ApplyManagedOverride(snapshot.AdvancedAutomation, !deployment.AllowAdvancedMode),
            AdvancedToolbox = ApplyManagedOverride(snapshot.AdvancedToolbox, deployment.HideAdvancedToolbox || !deployment.AllowAdvancedMode),
            AdvancedRecovery = ApplyManagedOverride(snapshot.AdvancedRecovery, !deployment.AllowAdvancedMode),
            CustomSupportRouting = snapshot.CustomSupportRouting,
            ManagedPolicies = deployment.ManagedMode ? CapabilityState.Available : snapshot.ManagedPolicies,
            WhiteLabelBranding = snapshot.WhiteLabelBranding
        };
    }

    public CapabilityState GetState(ProductCapability capability)
    {
        var snapshot = GetSnapshot();
        return capability switch
        {
            ProductCapability.EvidenceBundles => snapshot.EvidenceBundles,
            ProductCapability.Runbooks => snapshot.Runbooks,
            ProductCapability.DeepRepairs => snapshot.DeepRepairs,
            ProductCapability.AdvancedMode => snapshot.AdvancedMode,
            ProductCapability.AdvancedDiagnostics => snapshot.AdvancedDiagnostics,
            ProductCapability.TechnicianExports => snapshot.TechnicianExports,
            ProductCapability.AdvancedAutomation => snapshot.AdvancedAutomation,
            ProductCapability.AdvancedToolbox => snapshot.AdvancedToolbox,
            ProductCapability.AdvancedRecovery => snapshot.AdvancedRecovery,
            ProductCapability.CustomSupportRouting => snapshot.CustomSupportRouting,
            ProductCapability.WhiteLabelBranding => snapshot.WhiteLabelBranding,
            ProductCapability.ManagedPolicies => snapshot.ManagedPolicies,
            _ => CapabilityState.Available
        };
    }

    public CapabilityAvailability Describe(ProductCapability capability) => new()
    {
        Capability = capability,
        State = GetState(capability),
        Title = capability switch
        {
            ProductCapability.AdvancedMode => "Advanced Mode",
            ProductCapability.TechnicianExports => "Technician support packages",
            ProductCapability.AdvancedToolbox => "Advanced Windows tools",
            ProductCapability.WhiteLabelBranding => "White-label branding",
            ProductCapability.ManagedPolicies => "Managed deployment policies",
            ProductCapability.CustomSupportRouting => "Custom support routing",
            ProductCapability.AdvancedAutomation => "Advanced automation",
            ProductCapability.AdvancedRecovery => "Advanced recovery helpers",
            ProductCapability.AdvancedDiagnostics => "Advanced diagnostics",
            ProductCapability.DeepRepairs => "Deep repairs",
            ProductCapability.Runbooks => "Guided workflows",
            _ => "Capability"
        },
        Summary = BuildCapabilitySummary(capability, GetState(capability))
    };

    private static CapabilityState ApplyManagedOverride(CapabilityState current, bool disabledByPolicy) =>
        disabledByPolicy ? CapabilityState.ManagedOff : current;

    private static string BuildCapabilitySummary(ProductCapability capability, CapabilityState state) =>
        (capability, state) switch
        {
            (_, CapabilityState.Available) => capability switch
            {
                ProductCapability.AdvancedMode => "Advanced Mode can be enabled for deeper diagnostics, richer receipts, and more control.",
                ProductCapability.TechnicianExports => "Technician support packages can include richer receipt history and deeper technical detail.",
                ProductCapability.AdvancedToolbox => "Advanced Windows tools are available for deeper support work.",
                ProductCapability.WhiteLabelBranding => "This deployment can apply organization-specific branding and support identity.",
                ProductCapability.ManagedPolicies => "This build can enforce organization defaults and feature restrictions cleanly.",
                ProductCapability.CustomSupportRouting => "Support can route to organization-specific contacts, KB content, and escalation paths.",
                ProductCapability.DeepRepairs => "Deeper repairs are available when a safe first-line fix is not enough.",
                _ => "This capability is available in the current product mode."
            },
            (_, CapabilityState.UpgradeRequired) => capability switch
            {
                ProductCapability.AdvancedMode => "Advanced Mode is available in Pro and MSP deployments.",
                ProductCapability.TechnicianExports => "Technician export detail is available in Pro and MSP deployments.",
                ProductCapability.AdvancedToolbox => "Advanced Windows tool launchers are available in Pro and MSP deployments.",
                ProductCapability.WhiteLabelBranding => "White-label branding is reserved for MSP and managed deployments.",
                ProductCapability.ManagedPolicies => "Managed deployment controls are reserved for MSP and managed deployments.",
                ProductCapability.CustomSupportRouting => "Custom support routing is reserved for MSP and managed deployments.",
                _ => "This capability is reserved for a higher product tier."
            },
            (_, CapabilityState.ManagedOff) => capability switch
            {
                ProductCapability.AdvancedMode => "Advanced Mode is turned off by the current organization policy.",
                ProductCapability.TechnicianExports => "Technician export detail is restricted by the current organization policy.",
                ProductCapability.AdvancedToolbox => "Advanced Windows tools are hidden by the current organization policy.",
                ProductCapability.DeepRepairs => "Deep repairs are restricted by the current organization policy.",
                _ => "This capability is unavailable because the current deployment policy turns it off."
            },
            _ => "Capability state is unavailable."
        };
}

public sealed class AppUpdateService : IAppUpdateService
{
    private readonly ISettingsService _settingsService;
    private readonly IDeploymentConfigurationService _deploymentConfigurationService;

    public AppUpdateService(
        ISettingsService settingsService,
        IDeploymentConfigurationService deploymentConfigurationService)
    {
        _settingsService = settingsService;
        _deploymentConfigurationService = deploymentConfigurationService;
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

        var config = LoadConfig(settings, _deploymentConfigurationService.Current);
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

        if (string.Equals(config.Provider, "manifest", StringComparison.OrdinalIgnoreCase))
            return await QueryManifestAsync(config, cancellationToken);

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

    private static UpdateConfigDocument? LoadConfig(AppSettings settings, DeploymentConfiguration deployment)
    {
        var configuredFeed = !string.IsNullOrWhiteSpace(deployment.UpdateFeedUrl)
            ? deployment.UpdateFeedUrl
            : settings.UpdateFeedUrl;

        if (!string.IsNullOrWhiteSpace(configuredFeed))
        {
            return new UpdateConfigDocument
            {
                Provider = "manifest",
                FeedUrl = ProductizationPaths.ResolveFromAppBase(configuredFeed)
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
            ChannelName = string.IsNullOrWhiteSpace(config.ChannelName) ? "Stable" : config.ChannelName,
            DownloadUrl = (string?)release?.html_url ?? "",
            ReleaseNotesPath = ProductizationPaths.ResolveFromAppBase("CHANGELOG.md"),
            Summary = updateAvailable ? "A newer FixFox build is available." : "You are on the latest configured version."
        };
    }

    private static async Task<AppUpdateInfo> QueryManifestAsync(UpdateConfigDocument config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.FeedUrl))
        {
            return new AppUpdateInfo
            {
                CurrentVersion = SharedConstants.AppVersion,
                LatestVersion = SharedConstants.AppVersion,
                SourceName = "Release feed",
                ChannelName = string.IsNullOrWhiteSpace(config.ChannelName) ? "Stable" : config.ChannelName,
                Summary = "The configured release feed is missing a manifest path."
            };
        }

        try
        {
            var resolvedPath = ProductizationPaths.ResolveFromAppBase(config.FeedUrl);
            string raw;

            if (Uri.TryCreate(resolvedPath, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                using var client = new HttpClient();
                raw = await client.GetStringAsync(uri, cancellationToken);
            }
            else
            {
                raw = await File.ReadAllTextAsync(resolvedPath, cancellationToken);
            }

            var manifest = JsonConvert.DeserializeObject<ReleaseFeedDocument>(raw);
            if (manifest is null)
                throw new InvalidDataException("The release feed could not be parsed.");

            var normalizedLatest = (manifest.LatestVersion ?? SharedConstants.AppVersion).TrimStart('v', 'V');
            var updateAvailable = Version.TryParse(normalizedLatest, out var latestVersion)
                && Version.TryParse(SharedConstants.AppVersion, out var currentVersion)
                && latestVersion > currentVersion;

            return new AppUpdateInfo
            {
                UpdateAvailable = updateAvailable,
                CurrentVersion = SharedConstants.AppVersion,
                LatestVersion = normalizedLatest,
                SourceName = "Release feed",
                ChannelName = string.IsNullOrWhiteSpace(manifest.ChannelName) ? (string.IsNullOrWhiteSpace(config.ChannelName) ? "Stable" : config.ChannelName) : manifest.ChannelName,
                DownloadUrl = ProductizationPaths.ResolveFromAppBase(manifest.DownloadUrl),
                ReleaseNotesPath = ProductizationPaths.ResolveFromAppBase(manifest.ReleaseNotesPath),
                Summary = string.IsNullOrWhiteSpace(manifest.Summary)
                    ? (updateAvailable ? "A newer FixFox build is available." : "You are on the current configured FixFox build.")
                    : manifest.Summary
            };
        }
        catch (Exception ex)
        {
            return new AppUpdateInfo
            {
                CurrentVersion = SharedConstants.AppVersion,
                LatestVersion = SharedConstants.AppVersion,
                SourceName = "Release feed",
                ChannelName = string.IsNullOrWhiteSpace(config.ChannelName) ? "Stable" : config.ChannelName,
                Summary = $"FixFox could not read the configured release feed: {ex.Message}"
            };
        }
    }
}

public sealed class EvidenceBundleService : IEvidenceBundleService
{
    private readonly IRepairHistoryService _repairHistoryService;
    private readonly IAutomationHistoryService _automationHistoryService;
    private readonly INotificationService _notificationService;
    private readonly ILogService _logService;
    private readonly ISystemInfoService _systemInfoService;
    private readonly IEditionCapabilityService _editionCapabilityService;
    private readonly IBrandingConfigurationService _brandingConfigurationService;
    private readonly IDeploymentConfigurationService _deploymentConfigurationService;

    public EvidenceBundleService(
        IRepairHistoryService repairHistoryService,
        IAutomationHistoryService automationHistoryService,
        INotificationService notificationService,
        ILogService logService,
        ISystemInfoService systemInfoService,
        IEditionCapabilityService editionCapabilityService,
        IBrandingConfigurationService brandingConfigurationService,
        IDeploymentConfigurationService deploymentConfigurationService)
    {
        _repairHistoryService = repairHistoryService;
        _automationHistoryService = automationHistoryService;
        _notificationService = notificationService;
        _logService = logService;
        _systemInfoService = systemInfoService;
        _editionCapabilityService = editionCapabilityService;
        _brandingConfigurationService = brandingConfigurationService;
        _deploymentConfigurationService = deploymentConfigurationService;
    }

    public async Task<EvidenceBundleManifest> ExportAsync(
        string userIssue,
        TriageResult? triageResult,
        HealthCheckReport? healthReport,
        RunbookExecutionSummary? runbookSummary,
        EvidenceExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _systemInfoService.GetSnapshotAsync();
        options ??= new EvidenceExportOptions();
        var folder = Path.Combine(ProductizationPaths.EvidenceDir, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);

        var summaryPath = Path.Combine(folder, "summary.txt");
        var technicalPath = Path.Combine(folder, "technical-details.txt");
        var preview = await BuildPreviewAsync(userIssue, triageResult, healthReport, runbookSummary, options, cancellationToken);
        var branding = _brandingConfigurationService.Current;
        var deployment = _deploymentConfigurationService.Current;

        var summaryBuilder = new StringBuilder(preview);
        var technicalBuilder = new StringBuilder();
        technicalBuilder.AppendLine($"{branding.AppName} Technical Details");
        technicalBuilder.AppendLine($"Export level: {options.Level}");
        technicalBuilder.AppendLine($"Redaction policy: machine names always redacted; IP address {(options.RedactIpAddress ? "redacted" : "included")}.");
        technicalBuilder.AppendLine($"Edition: {_editionCapabilityService.GetSnapshot().Edition}");
        if (!string.IsNullOrWhiteSpace(deployment.OrganizationName))
            technicalBuilder.AppendLine($"Deployment: {deployment.OrganizationName}");
        technicalBuilder.AppendLine();

        technicalBuilder.AppendLine("Recent repair history:");
        foreach (var entry in _repairHistoryService.Entries.Take(10))
            technicalBuilder.AppendLine($" - [{entry.Timestamp:yyyy-MM-dd HH:mm}] {entry.FixTitle} | Outcome={entry.Outcome} | Verification={entry.VerificationSummary} | Next={Redact(entry.NextStep)}");
        technicalBuilder.AppendLine();
        technicalBuilder.AppendLine("Recent automation history:");
        foreach (var entry in _automationHistoryService.Entries.Take(10))
            technicalBuilder.AppendLine($" - [{entry.StartedAt:yyyy-MM-dd HH:mm}] {entry.RuleTitle} | Outcome={entry.Outcome} | Summary={Redact(entry.Summary)} | Next={Redact(entry.NextStep)}");
        technicalBuilder.AppendLine();
        technicalBuilder.AppendLine(JsonConvert.SerializeObject(new
        {
            MachineName = Redact(snapshot.MachineName),
            IpAddress = options.RedactIpAddress ? "<redacted>" : snapshot.IpAddress,
            snapshot.OsVersion,
            snapshot.OsBuild,
            snapshot.WindowsEdition,
            snapshot.NetworkType,
            snapshot.InternetReachable,
            snapshot.DiskFreeGb,
            snapshot.DiskUsedPct,
            snapshot.RamUsedPct,
            snapshot.PendingUpdateCount,
            snapshot.DefenderEnabled,
            Notifications = options.IncludeNotifications ? _notificationService.All.Take(10).Select(n => new { n.Title, n.Level, n.IsRead }) : [],
            History = options.IncludeTechnicalHistory ? _repairHistoryService.Entries.Take(20) : [],
            AutomationHistory = options.IncludeTechnicalHistory ? _automationHistoryService.Entries.Take(20) : [],
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
            Headline = triageResult?.Candidates.FirstOrDefault()?.CategoryName ?? $"{branding.AppName} support bundle"
        };
    }

    public async Task<string> BuildPreviewAsync(
        string userIssue,
        TriageResult? triageResult,
        HealthCheckReport? healthReport,
        RunbookExecutionSummary? runbookSummary,
        EvidenceExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new EvidenceExportOptions();
        var snapshot = await _systemInfoService.GetSnapshotAsync();
        var branding = _brandingConfigurationService.Current;
        var deployment = _deploymentConfigurationService.Current;

        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine($"{branding.AppName} Support Summary");
        summaryBuilder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        summaryBuilder.AppendLine($"Export level: {options.Level}");
        summaryBuilder.AppendLine($"Included fields: machine summary, repair receipts, {(options.IncludeNotifications ? "alerts" : "no alerts")}, {(options.IncludeTechnicalHistory ? "technical receipt history" : "summary-only history")}.");
        summaryBuilder.AppendLine($"Issue summary: {Redact(userIssue)}");
        if (triageResult?.Candidates.Count > 0)
        {
            var top = triageResult.Candidates[0];
            summaryBuilder.AppendLine($"Top match: {top.CategoryName} ({top.ConfidenceScore}%)");
            summaryBuilder.AppendLine($"What FixFox thinks is wrong: {top.WhatIThinkIsWrong}");
            summaryBuilder.AppendLine($"Why: {top.WhyIThinkThat}");
        }
        if (healthReport is not null)
        {
            summaryBuilder.AppendLine($"Health check: {healthReport.OverallScore}/100 - {healthReport.Summary}");
            foreach (var category in healthReport.Categories.OrderBy(category => category.Score).Take(4))
                summaryBuilder.AppendLine($" - {category.Title}: {category.Score}/100 - {category.Summary}");
        }
        if (runbookSummary is not null)
        {
            summaryBuilder.AppendLine($"Runbook: {runbookSummary.Title} - {runbookSummary.Summary}");
            foreach (var line in runbookSummary.Timeline.Take(5))
                summaryBuilder.AppendLine($" - {line}");
        }

        summaryBuilder.AppendLine($"Device: {Redact(snapshot.MachineName)} ({snapshot.OsVersion} build {snapshot.OsBuild})");
        summaryBuilder.AppendLine($"Connectivity: {(snapshot.InternetReachable ? "Internet reachable" : "Internet not confirmed")} via {snapshot.NetworkType}");
        summaryBuilder.AppendLine($"Storage free: {snapshot.DiskFreeGb} GB");
        summaryBuilder.AppendLine($"Memory in use: {snapshot.RamUsedPct}%");
        summaryBuilder.AppendLine($"Pending updates: {snapshot.PendingUpdateCount}");
        summaryBuilder.AppendLine($"Defender enabled: {(snapshot.DefenderEnabled ? "Yes" : "No")}");
        summaryBuilder.AppendLine($"Recent failed receipts: {_repairHistoryService.Entries.Count(e => !e.Success)}");
        summaryBuilder.AppendLine($"Recent automation runs: {_automationHistoryService.Entries.Count}");

        foreach (var failure in _repairHistoryService.Entries.Where(entry => !entry.Success).Take(3))
            summaryBuilder.AppendLine($" - Failed: {failure.FixTitle} | {Redact(failure.NextStep)}");
        foreach (var success in _repairHistoryService.Entries.Where(entry => entry.Success).Take(3))
            summaryBuilder.AppendLine($" - Successful: {success.FixTitle}");
        foreach (var automation in _automationHistoryService.Entries.Take(3))
            summaryBuilder.AppendLine($" - Automation: {automation.RuleTitle} | {Redact(automation.Summary)}");

        if (options.IncludeNotifications && _notificationService.All.Count > 0)
        {
            summaryBuilder.AppendLine("Open alerts:");
            foreach (var notification in _notificationService.All.Take(5))
                summaryBuilder.AppendLine($" - {notification.Title}: {notification.Message}");
        }

        if (!string.IsNullOrWhiteSpace(branding.SupportDisplayName))
        {
            summaryBuilder.AppendLine($"Support route: {branding.SupportDisplayName}");
            if (!string.IsNullOrWhiteSpace(branding.SupportEmail))
                summaryBuilder.AppendLine($"Support email: {branding.SupportEmail}");
        }

        if (!string.IsNullOrWhiteSpace(deployment.OrganizationName))
            summaryBuilder.AppendLine($"Deployment: {deployment.OrganizationName}");

        return summaryBuilder.ToString();
    }

    private static string Redact(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var redacted = value.Replace(Environment.UserName, "<user>", StringComparison.OrdinalIgnoreCase);
        return redacted.Replace(Environment.MachineName, "<device>", StringComparison.OrdinalIgnoreCase);
    }
}
