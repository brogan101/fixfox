using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using SharedConstants = HelpDesk.Shared.Constants;

namespace HelpDesk.Presentation.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    // ── Services ──────────────────────────────────────────────────────────
    private readonly IScriptService       _scripts;
    private readonly IFixCatalogService   _catalog;
    private readonly IQuickScanService    _scanner;
    private readonly ISystemInfoService   _sysInfo;
    private readonly ILogService          _log;
    private readonly INotificationService _notifs;
    private readonly ISettingsService     _settingsSvc;
    private readonly IAppLogger           _logger;
    private readonly IElevationService    _elevation;
    private readonly ITriageEngine        _triage;
    private readonly IRunbookCatalogService _runbookCatalog;
    private readonly IRunbookExecutionService _runbookExecution;
    private readonly IRepairExecutionService _repairExecution;
    private readonly IHealthCheckService  _healthCheck;
    private readonly IEvidenceBundleService _evidenceBundles;
    private readonly IGuidedRepairExecutionService _guidedRepairExecution;
    private readonly IKnowledgeBaseService _knowledgeBase;
    private readonly IBrandingConfigurationService _branding;
    private readonly IDeploymentConfigurationService _deployment;
    private readonly IEditionCapabilityService _edition;
    private readonly IAppUpdateService _updates;
    private readonly IStatePersistenceService _statePersistence;
    private readonly IRepairHistoryService _repairHistory;
    private readonly IRepairCatalogService _repairCatalog;
    private readonly InstalledProgramsService _installedPrograms;
    private readonly StartupAppsService _startupApps;
    private readonly StorageInsightsService _storageInsights;
    private readonly SchedulerService        _scheduler;
    private readonly IToolboxService _toolbox;
    private readonly IMaintenanceProfileService _maintenanceProfileService;
    private readonly ISupportCenterService _supportCenterService;
    private readonly ICommandPaletteService _commandPaletteService;
    private readonly IDashboardWorkspaceService _dashboardWorkspace;
    private readonly IAutomationHistoryService _automationHistory;
    private readonly IAutomationCoordinatorService _automationCoordinator;
    private readonly List<RunbookDefinition> _allRunbooks = [];
    private readonly List<FixBundle> _allBundles = [];
    private readonly List<MaintenanceProfileDefinition> _allMaintenanceProfiles = [];

    // ── Navigation ────────────────────────────────────────────────────────
    private Page _currentPage = Page.Dashboard;
    public Page CurrentPage
    {
        get => _currentPage;
        set
        {
            if (_currentPage == value) return;
            _currentPage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentPageSummaryText));
            RefreshBreadcrumb();
            foreach (var n in new[]
            {
                nameof(ShowDashboard), nameof(ShowFixes), nameof(ShowBundles),
                nameof(ShowSystemInfo), nameof(ShowSymptomChecker),
                nameof(ShowToolbox), nameof(ShowHistory), nameof(ShowHandoff), nameof(ShowSettings)
            }) OnPropertyChanged(n);
        }
    }

    public bool ShowDashboard       => CurrentPage == Page.Dashboard;
    public bool ShowFixes           => CurrentPage == Page.Fixes;
    public bool ShowBundles         => CurrentPage == Page.Bundles;
    public bool ShowSystemInfo      => CurrentPage == Page.SystemInfo;
    public bool ShowSymptomChecker  => CurrentPage == Page.SymptomChecker;
    public bool ShowToolbox         => CurrentPage == Page.Toolbox;
    public bool ShowHistory         => CurrentPage == Page.History;
    public bool ShowHandoff         => CurrentPage == Page.Handoff;
    public bool ShowSettings        => CurrentPage == Page.Settings;

    public string BreadcrumbText => CurrentPage switch
    {
        Page.Dashboard       => "Home",
        Page.Fixes           => _selectedCategory is null
                                    ? "Repair Library"
                                    : $"Repair Library  \u203A  {_selectedCategory.Title}",
        Page.Bundles         => "Automation",
        Page.SystemInfo      => "Device Health",
        Page.SymptomChecker  => "Guided Diagnosis",
        Page.Toolbox         => "Windows Tools",
        Page.History         => "Activity",
        Page.Handoff         => "Support Package",
        Page.Settings        => "Settings",
        _                    => ""
    };

    /// <summary>Status bar label — same text as breadcrumb.</summary>
    public string CurrentPageLabel => BreadcrumbText;
    public string CurrentPageSummaryText => CurrentPage switch
    {
        Page.Dashboard => "See what needs attention and take the next safe action.",
        Page.Fixes => "Browse verified repairs and run them directly.",
        Page.Bundles => "Review scheduled automations, watchers, and safe maintenance.",
        Page.SystemInfo => "Use the device baseline and support centers to choose the right path.",
        Page.SymptomChecker => "Rank likely causes before you run a repair.",
        Page.Toolbox => "Open the exact Windows tool you need.",
        Page.History => "Review what changed, rerun it, or escalate with evidence.",
        Page.Handoff => "Package the issue cleanly when self-service stops helping.",
        Page.Settings => "Tune startup behavior, profiles, and local data handling.",
        _ => ""
    };
    public string ProductDisplayName => Branding.AppName;
    public string ProductSubtitle => Branding.AppSubtitle;
    public string ProductTagline => Branding.ProductTagline;
    public string OnboardingTitleText => $"Set up {ProductDisplayName}";
    public string OnboardingSummaryText =>
        $"{ProductDisplayName} checks workstation health, runs guided repairs, and builds support packages when self-service is not enough. Choose the defaults that fit this device, then start with a health check or jump straight into the workspace.";
    public string OnboardingCapabilitySummaryText =>
        $"Use Home for the next safe action, Guided Diagnosis for plain-language symptoms, Repair Library for direct fixes, and Support Package when the issue needs escalation.";
    public string OnboardingPrivacySummaryText =>
        $"{ProductDisplayName} keeps settings, receipts, logs, and support packages on this PC. You can review what a package contains before opening or sharing it.";
    public string LocalDataSummaryText =>
        $"{ProductDisplayName} stores its logs, repair history, and support packages locally so you can audit what was captured before sharing it with anyone else.";
    public string ProductDisplayModeText => $"{FormatEditionLabel(EditionSnapshot.Edition)}{(EditionSnapshot.ManagedMode ? " • managed" : "")}";
    public string CurrentProfileStatusText
    {
        get
        {
            var parts = new List<string> { $"{SelectedBehaviorProfile} profile" };
            if (AdvancedModeEnabled)
                parts.Add("Advanced");
            if (EditionSnapshot.ManagedMode)
                parts.Add("Managed");
            return string.Join(" • ", parts);
        }
    }
    public string ShellStatusText =>
        HasInterruptedOperation ? "Interrupted repair needs review" :
        AutomationPaused ? AutomationPauseStatusText :
        AutomationAttentionCount > 0 ? $"{AutomationAttentionCount} automation item{(AutomationAttentionCount == 1 ? "" : "s")} need attention" :
        UnreadNotifCount > 0 ? $"{UnreadNotifCount} alert{(UnreadNotifCount == 1 ? "" : "s")} need review" :
        HasEvidenceBundle ? "Support package ready" :
        LastHealthCheckReport is not null ? $"{LastHealthCheckReport.OverallScore}/100 health score" :
        "Ready";

    public bool HasCommandPaletteResults => CommandPaletteResults.Count > 0;

    /// <summary>Breadcrumb bar items: always starts with the current product name, adds page name if not Dashboard.</summary>
    public ObservableCollection<string> BreadcrumbItems { get; } = [];

    private void RefreshBreadcrumb()
    {
        BreadcrumbItems.Clear();
        BreadcrumbItems.Add(ProductDisplayName);
        if (CurrentPage != Page.Dashboard)
            BreadcrumbItems.Add(BreadcrumbText);
        OnPropertyChanged(nameof(BreadcrumbText));
        OnPropertyChanged(nameof(CurrentPageLabel));
    }

    // ── Startup verifier panel ────────────────────────────────────────────
    private bool _forceShowVerifyPanel;
    public bool ForceShowVerifyPanel
    {
        get => _forceShowVerifyPanel;
        set { _forceShowVerifyPanel = value; OnPropertyChanged(); }
    }

    // ── Sidebar ───────────────────────────────────────────────────────────
    private bool _isSidebarCollapsed;
    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set
        {
            _isSidebarCollapsed = value;
            _settings.SidebarCollapsed = value;
            _settingsSvc.Save(_settings);
            OnPropertyChanged();
        }
    }

    // ── Fix Center ────────────────────────────────────────────────────────
    public ObservableCollection<FixCategory> Categories  { get; } = [];
    public ObservableCollection<FixItem>     ActiveFixes { get; } = [];
    public ObservableCollection<FixItem>     PinnedFixes { get; } = [];
    public ObservableCollection<FixItem>     FavoriteFixes { get; } = [];
    public bool HasPinnedFixes => PinnedFixes.Count > 0;

    private FixCategory? _selectedCategory;
    public FixCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            _settings.LastFixCategory = value?.Id ?? "";
            _settingsSvc.Save(_settings);
            OnPropertyChanged();
            RefreshBreadcrumb();
            RefreshActiveFixes();
        }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            if (!string.IsNullOrWhiteSpace(value))
                AddRecentSearch(value);
            RefreshActiveFixes();
        }
    }

    private bool _isSearching;
    public bool IsSearching { get => _isSearching; set { _isSearching = value; OnPropertyChanged(); } }

    public int ActiveFixCount => ActiveFixes.Count;
    public bool HasActiveFixes => ActiveFixCount > 0;
    public string ActiveFixCountText => $"{ActiveFixCount} repair option{(ActiveFixCount == 1 ? "" : "s")} ready";
    public string FixLibrarySummaryText => !string.IsNullOrWhiteSpace(SearchText)
        ? $"{ActiveFixCount} repair option{(ActiveFixCount == 1 ? "" : "s")} match \"{SearchText}\"."
        : SelectedCategory is null
            ? "Browse the full repair library or search for a specific problem."
            : $"{ActiveFixCount} repair option{(ActiveFixCount == 1 ? "" : "s")} in {SelectedCategory.Title}.";
    public string FixLibraryEmptyStateTitle => !string.IsNullOrWhiteSpace(SearchText)
        ? "No repairs match that search"
        : "No repairs are available here";
    public string FixLibraryEmptyStateText => !string.IsNullOrWhiteSpace(SearchText)
        ? "Try a broader problem word, clear the search, or switch to Guided Diagnosis if you are not sure which repair fits yet."
        : "Pick a repair category or use Guided Diagnosis to narrow the issue before you run anything.";

    private void RefreshActiveFixes()
    {
        ActiveFixes.Clear();
        IEnumerable<FixItem> source;

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            IsSearching = true;
            source      = _catalog.Search(_searchText);
        }
        else
        {
            IsSearching = false;
            source      = _selectedCategory?.Fixes ?? [];
        }

        foreach (var f in source.Where(CanAccessFix)) ActiveFixes.Add(f);
        OnPropertyChanged(nameof(ActiveFixCount));
        OnPropertyChanged(nameof(ActiveFixCountText));
        OnPropertyChanged(nameof(HasActiveFixes));
        OnPropertyChanged(nameof(FixLibrarySummaryText));
        OnPropertyChanged(nameof(FixLibraryEmptyStateTitle));
        OnPropertyChanged(nameof(FixLibraryEmptyStateText));
    }

    // ── Dashboard / Quick Scan ────────────────────────────────────────────
    public ObservableCollection<ScanResult> ScanResults { get; } = [];

    private bool   _scanRunning;
    private string _scanStatusText = "Click 'Run Quick Scan' to check your PC's health.";

    public bool   ScanRunning
    {
        get => _scanRunning;
        set
        {
            _scanRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasActiveWork));
            OnPropertyChanged(nameof(ActiveWorkSummary));
        }
    }
    public string ScanStatusText { get => _scanStatusText; set { _scanStatusText = value; OnPropertyChanged(); } }

    public int    ScanCriticalCount => ScanResults.Count(r => r.Severity == ScanSeverity.Critical);
    public int    ScanWarningCount  => ScanResults.Count(r => r.Severity == ScanSeverity.Warning);
    public int    ScanGoodCount     => ScanResults.Count(r => r.Severity == ScanSeverity.Good);
    public string OverallHealth =>
        ScanCriticalCount > 0 ? "Needs Attention" :
        ScanWarningCount  > 0 ? "Fair"            :
        ScanResults.Count > 0 ? "Good"            : "\u2013";
    public string OverallHealthColor =>
        ScanCriticalCount > 0 ? "#EF4444" :
        ScanWarningCount  > 0 ? "#F59E0B" :
        ScanResults.Count > 0 ? "#22C55E" : "#7E8FAD";

    // ── Recently Run Fixes ────────────────────────────────────────────────
    public ObservableCollection<FixItem> RecentlyRunFixes { get; } = [];
    public bool HasRecentlyRunFixes => RecentlyRunFixes.Count > 0;
    public ObservableCollection<RepairHistoryEntry> RecentFailedEntries { get; } = [];
    public bool HasRecentFailures => RecentFailedEntries.Count > 0;
    public ObservableCollection<DashboardAlert> DashboardAlerts { get; } = [];
    public bool HasDashboardAlerts => DashboardAlerts.Count > 0;
    public ObservableCollection<RunbookDefinition> SuggestedRunbooks { get; } = [];
    public bool HasSuggestedRunbooks => SuggestedRunbooks.Count > 0;

    private void RefreshRecentlyRun()
    {
        RecentlyRunFixes.Clear();
        RecentFailedEntries.Clear();
        var seen = new HashSet<string>();
        foreach (var e in _repairHistory.Entries.Where(entry => !string.IsNullOrWhiteSpace(entry.FixId)))
        {
            if (seen.Count >= 3) break;
            if (!seen.Add(e.FixId)) continue;
            var fix = _catalog.GetById(e.FixId);
            if (fix is not null) RecentlyRunFixes.Add(fix);
        }
        foreach (var failure in _repairHistory.Entries.Where(entry => !entry.Success).Take(4))
            RecentFailedEntries.Add(failure);
        OnPropertyChanged(nameof(HasRecentlyRunFixes));
        OnPropertyChanged(nameof(HasRecentFailures));
        RefreshCommandPalette();
    }

    // ── Symptom Checker ───────────────────────────────────────────────────
    public ObservableCollection<FixItem> SymptomResults { get; } = [];
    public ObservableCollection<TriageCandidate> TriageCandidates { get; } = [];

    private string _symptomInput = "";
    private bool   _symptomSearched;

    public string SymptomInput
    {
        get => _symptomInput;
        set { _symptomInput = value; OnPropertyChanged(); }
    }
    public bool SymptomSearched { get => _symptomSearched; set { _symptomSearched = value; OnPropertyChanged(); } }
    public int  SymptomCount    => SymptomResults.Count;
    public bool SymptomHasNoResults => SymptomSearched && SymptomResults.Count == 0;
    public bool HasTriageCandidates => TriageCandidates.Count > 0;
    public bool HasRecentSearches => RecentSearches.Count > 0;

    public void RunSymptomSearch()
    {
        SymptomResults.Clear();
        TriageCandidates.Clear();
        if (string.IsNullOrWhiteSpace(_symptomInput))
        {
            SymptomSearched = false;
            OnPropertyChanged(nameof(SymptomCount));
            OnPropertyChanged(nameof(SymptomHasNoResults));
            OnPropertyChanged(nameof(HasTriageCandidates));
            return;
        }

        var triageContext = new TriageContext
        {
            Query = _symptomInput,
            HasBattery = Snapshot?.HasBattery ?? false,
            PendingRebootDetected = (Snapshot?.PendingUpdateCount ?? 0) > 0,
            HasRecentFailures = HistoryEntries.Take(5).Any(e => !e.Success),
            InternetReachable = Snapshot?.InternetReachable ?? false,
            DiskFreeGb = Snapshot?.DiskFreeGb ?? 0,
            RamUsedPct = Snapshot?.RamUsedPct ?? 0,
            NetworkType = Snapshot?.NetworkType ?? "",
            RecentSymptoms = RecentSearches.ToList()
        };
        var triageResult = _triage.Analyze(_symptomInput, triageContext);
        foreach (var candidate in triageResult.Candidates)
            TriageCandidates.Add(candidate);

        var results = triageResult.Candidates.Count > 0
            ? triageResult.Candidates
                .SelectMany(c => c.RecommendedFixIds)
                .Select(id => _catalog.GetById(id))
                .Where(f => f is not null)
                .Cast<FixItem>()
                .DistinctBy(f => f.Id)
                .ToList()
            : _catalog.Search(_symptomInput);
        foreach (var r in results) SymptomResults.Add(r);
        SymptomSearched = true;
        OnPropertyChanged(nameof(SymptomCount));
        OnPropertyChanged(nameof(SymptomHasNoResults));
        OnPropertyChanged(nameof(HasTriageCandidates));
    }

    // ── Fix Bundles ───────────────────────────────────────────────────────
    public ObservableCollection<FixBundle> Bundles { get; } = [];
    public ObservableCollection<RunbookDefinition> Runbooks { get; } = [];

    private FixBundle? _runningBundle;
    private string     _bundleStatus   = "";
    private int        _bundleProgress;
    private int        _bundleTotal    = 1;
    private bool       _isRunbookRunning;
    private string     _runningRunbookTitle = "";

    public FixBundle? RunningBundle
    {
        get => _runningBundle;
        set
        {
            _runningBundle = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBundleRunning));
            OnPropertyChanged(nameof(HasActiveWork));
            OnPropertyChanged(nameof(ActiveWorkSummary));
        }
    }
    public bool       IsBundleRunning => _runningBundle is not null;
    public string     BundleStatus    { get => _bundleStatus;    set { _bundleStatus   = value; OnPropertyChanged(); } }
    public int        BundleProgress  { get => _bundleProgress;  set { _bundleProgress = value; OnPropertyChanged(); } }
    public int        BundleTotal     { get => _bundleTotal;     set { _bundleTotal    = value; OnPropertyChanged(); } }
    public bool IsRunbookRunning
    {
        get => _isRunbookRunning;
        set
        {
            _isRunbookRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasActiveWork));
            OnPropertyChanged(nameof(ActiveWorkSummary));
        }
    }
    public string RunningRunbookTitle
    {
        get => _runningRunbookTitle;
        set
        {
            _runningRunbookTitle = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveWorkSummary));
        }
    }
    public IReadOnlyList<string> WeeklyTuneUpDayOptions { get; } = Enum.GetNames<DayOfWeek>();
    public IReadOnlyList<string> WeeklyTuneUpTimeOptions { get; } =
        Enumerable.Range(0, 24).Select(h => $"{h:D2}:00").ToList();

    private string _selectedWeeklyTuneUpDay = DayOfWeek.Sunday.ToString();
    private string _selectedWeeklyTuneUpTime = "10:00";
    private string _nextWeeklyTuneUpText = "Not scheduled";

    public string SelectedWeeklyTuneUpDay
    {
        get => _selectedWeeklyTuneUpDay;
        set { _selectedWeeklyTuneUpDay = value; OnPropertyChanged(); }
    }

    public string SelectedWeeklyTuneUpTime
    {
        get => _selectedWeeklyTuneUpTime;
        set { _selectedWeeklyTuneUpTime = value; OnPropertyChanged(); }
    }

    public bool IsWeeklyTuneUpScheduled => _scheduler.IsScheduled();
    public string NextWeeklyTuneUpText
    {
        get => _nextWeeklyTuneUpText;
        set { _nextWeeklyTuneUpText = value; OnPropertyChanged(); }
    }

    // ── Wizard ────────────────────────────────────────────────────────────
    public ObservableCollection<ProactiveRecommendation> ProactiveRecommendations { get; } = [];
    public bool HasProactiveRecommendations => ProactiveRecommendations.Count > 0;

    private HealthCheckReport? _lastHealthCheckReport;
    public HealthCheckReport? LastHealthCheckReport
    {
        get => _lastHealthCheckReport;
        set
        {
            _lastHealthCheckReport = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HealthCheckSummaryText));
            OnPropertyChanged(nameof(MaintenanceSummaryText));
            OnPropertyChanged(nameof(ShellStatusText));
        }
    }

    public string HealthCheckSummaryText =>
        LastHealthCheckReport is null
            ? "Run a full health check to score the device and generate proactive recommendations."
            : $"{LastHealthCheckReport.OverallScore}/100 - {LastHealthCheckReport.Summary}";

    private FixItem? _wizardFix;
    private int      _wizardStep;
    private bool     _wizardVisible;
    private GuidedRepairExecutionResult? _guidedRepairState;

    public bool      WizardVisible
    {
        get => _wizardVisible;
        set
        {
            _wizardVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasActiveWork));
            OnPropertyChanged(nameof(ActiveWorkSummary));
        }
    }
    public FixItem?  WizardFix
    {
        get => _wizardFix;
        set
        {
            _wizardFix = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasActiveWork));
            OnPropertyChanged(nameof(ActiveWorkSummary));
            RefreshWizard();
        }
    }
    public FixStep?  CurrentStep    => WizardFix?.Steps.Count > _wizardStep ? WizardFix.Steps[_wizardStep] : null;
    public string    WizardStepLabel => WizardFix is null ? "" : $"Step {_wizardStep + 1} of {WizardFix.Steps.Count}";
    public bool      WizardHasScript => !string.IsNullOrWhiteSpace(CurrentStep?.Script);
    public bool      WizardIsLast   => WizardFix is not null && _wizardStep == WizardFix.Steps.Count - 1;
    public string    WizardNextLabel => WizardIsLast ? "Done" : "Next step";
    public GuidedRepairExecutionResult? GuidedRepairState
    {
        get => _guidedRepairState;
        set { _guidedRepairState = value; OnPropertyChanged(); OnPropertyChanged(nameof(WizardSummaryText)); OnPropertyChanged(nameof(WizardCanResume)); OnPropertyChanged(nameof(WizardFailedStepText)); }
    }
    public string WizardSummaryText => GuidedRepairState?.Summary ?? "";
    public bool WizardCanResume => GuidedRepairState?.CanResume == true;
    public string WizardFailedStepText => string.IsNullOrWhiteSpace(GuidedRepairState?.FailedStepTitle) ? "" : $"Last blocked step: {GuidedRepairState.FailedStepTitle}";

    private void RefreshWizard()
    {
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(WizardStepLabel));
        OnPropertyChanged(nameof(WizardHasScript));
        OnPropertyChanged(nameof(WizardIsLast));
        OnPropertyChanged(nameof(WizardNextLabel));
        OnPropertyChanged(nameof(WizardSummaryText));
        OnPropertyChanged(nameof(WizardCanResume));
        OnPropertyChanged(nameof(WizardFailedStepText));
    }

    // ── System Info ───────────────────────────────────────────────────────
    private SystemSnapshot? _snapshot;
    private bool            _snapshotLoading;
    public SystemSnapshot?  Snapshot        { get => _snapshot;        set { _snapshot        = value; OnPropertyChanged(); OnPropertyChanged(nameof(FirstRunSummaryText)); } }
    public bool             SnapshotLoading { get => _snapshotLoading; set { _snapshotLoading  = value; OnPropertyChanged(); } }

    private readonly List<InstalledProgram> _allInstalledPrograms = [];
    private string _installedProgramSearchText = "";
    private bool   _installedProgramsLoading;
    public ObservableCollection<InstalledProgram> InstalledPrograms { get; } = [];

    public string InstalledProgramSearchText
    {
        get => _installedProgramSearchText;
        set
        {
            _installedProgramSearchText = value;
            OnPropertyChanged();
            RefreshInstalledPrograms();
        }
    }

    public bool InstalledProgramsLoading
    {
        get => _installedProgramsLoading;
        set
        {
            _installedProgramsLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowInstalledProgramsEmptyState));
        }
    }

    public bool HasInstalledPrograms => InstalledPrograms.Count > 0;
    public bool ShowInstalledProgramsEmptyState => !InstalledProgramsLoading && !HasInstalledPrograms;
    public string InstalledProgramsSummaryText =>
        _allInstalledPrograms.Count == 0
            ? "Load your installed software inventory to browse, search, and uninstall apps."
            : $"{InstalledPrograms.Count} of {_allInstalledPrograms.Count} installed apps";
    public ObservableCollection<StartupAppEntry> StartupApps { get; } = [];
    private bool _startupAppsLoading;
    public bool StartupAppsLoading
    {
        get => _startupAppsLoading;
        set
        {
            _startupAppsLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowStartupAppsEmptyState));
        }
    }
    public bool HasStartupApps => StartupApps.Count > 0;
    public bool ShowStartupAppsEmptyState => !StartupAppsLoading && !HasStartupApps;
    public string StartupAppsSummaryText =>
        StartupApps.Count == 0
            ? "Load startup inventory to review sign-in pressure and background launchers."
            : $"{StartupApps.Count} startup item(s) found. {StartupApps.Count(item => item.RecommendedDisableCandidate)} worth reviewing if startup feels heavy.";
    public ObservableCollection<StorageInsight> StorageInsights { get; } = [];
    private bool _storageInsightsLoading;
    public bool StorageInsightsLoading
    {
        get => _storageInsightsLoading;
        set
        {
            _storageInsightsLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowStorageInsightsEmptyState));
        }
    }
    public bool HasStorageInsights => StorageInsights.Count > 0;
    public bool ShowStorageInsightsEmptyState => !StorageInsightsLoading && !HasStorageInsights;
    public string StorageInsightsSummaryText =>
        StorageInsights.Count == 0
            ? "Load large-file review to inspect common clutter locations safely."
            : $"{StorageInsights.Count} large file candidate(s) surfaced from common user folders.";
    public IReadOnlyList<ToolboxGroup> ToolboxGroups => BuildVisibleToolboxGroups();
    public ObservableCollection<SupportCenterDefinition> SupportCenters { get; } = [];
    public bool HasSupportCenters => SupportCenters.Count > 0;
    public ObservableCollection<MaintenanceProfileDefinition> MaintenanceProfiles { get; } = [];
    public bool HasMaintenanceProfiles => MaintenanceProfiles.Count > 0;
    public ObservableCollection<AutomationRuleSettings> AutomationRules { get; } = [];
    public ObservableCollection<AutomationRuleSettings> ScheduledAutomationRules { get; } = [];
    public ObservableCollection<AutomationRuleSettings> WatcherAutomationRules { get; } = [];
    public ObservableCollection<AutomationRunReceipt> AutomationHistoryEntries { get; } = [];
    public ObservableCollection<AutomationRunReceipt> FilteredAutomationHistoryEntries { get; } = [];
    public ObservableCollection<AutomationRunReceipt> RecentAutomationHistoryEntries { get; } = [];
    public bool HasVisibleBundles => Bundles.Count > 0;
    public string AdvancedAutomationAvailabilityText => _edition.Describe(ProductCapability.AdvancedAutomation).Summary;
    public bool HasAutomationRules => AutomationRules.Count > 0;
    public bool HasScheduledAutomationRules => ScheduledAutomationRules.Count > 0;
    public bool HasWatcherAutomationRules => WatcherAutomationRules.Count > 0;
    public bool HasAutomationHistoryEntries => AutomationHistoryEntries.Count > 0;
    public bool HasFilteredAutomationHistoryEntries => FilteredAutomationHistoryEntries.Count > 0;
    public bool HasRecentAutomationHistory => RecentAutomationHistoryEntries.Count > 0;
    public IReadOnlyList<AutomationScheduleKind> AutomationScheduleModeOptions { get; } = Enum.GetValues<AutomationScheduleKind>();
    public IReadOnlyList<string> AutomationHistoryFilterOptions { get; } = ["All", "Attention Needed", "Failures", "Skipped", "Completed"];
    public string[] QuietHoursTimeOptions { get; } = Enumerable.Range(0, 24).Select(hour => $"{hour:D2}:00").ToArray();

    private string _automationHistoryFilter = "All";
    public string AutomationHistoryFilter
    {
        get => _automationHistoryFilter;
        set
        {
            _automationHistoryFilter = string.IsNullOrWhiteSpace(value) ? "All" : value;
            OnPropertyChanged();
            FilterAutomationHistory();
        }
    }

    public bool AutomationPaused =>
        _settings.AutomationPausedUntilUtc.HasValue
        && _settings.AutomationPausedUntilUtc.Value > DateTime.UtcNow;
    public string AutomationPauseStatusText => AutomationPaused
        ? $"Paused until {_settings.AutomationPausedUntilUtc!.Value.ToLocalTime():g}"
        : "Automation is active.";
    public int ActiveAutomationCount => AutomationRules.Count(rule => rule.Enabled);
    public int PausedAutomationCount =>
        (AutomationPaused ? ActiveAutomationCount : 0)
        + AutomationRules.Count(rule => rule.PausedUntilUtc.HasValue && rule.PausedUntilUtc.Value > DateTime.UtcNow);
    public int AutomationAttentionCount => AutomationRules.Count(rule => rule.NeedsAttention);
    public string NextAutomationRunText
    {
        get
        {
            var next = ScheduledAutomationRules
                .Where(rule => rule.Enabled)
                .Select(rule => _scheduler.GetNextRun(rule.Id) ?? _automationCoordinator.GetNextRun(rule, DateTime.Now))
                .Where(value => value.HasValue)
                .OrderBy(value => value)
                .FirstOrDefault();
            return next?.ToLocalTime().ToString("ddd, MMM d 'at' h:mm tt") ?? "No scheduled run";
        }
    }
    public string LastAutomationResultText
    {
        get
        {
            var last = AutomationHistoryEntries.FirstOrDefault();
            return last is null ? "No automation has run yet." : $"{last.RuleTitle}: {last.Summary}";
        }
    }
    public string AutomationOverviewText =>
        $"{ActiveAutomationCount} active, {PausedAutomationCount} paused, next run {NextAutomationRunText}.";

    // ── History ───────────────────────────────────────────────────────────
    public ObservableCollection<RepairHistoryEntry> HistoryEntries { get; } = [];
    public ObservableCollection<RepairHistoryEntry> FilteredHistoryEntries { get; } = [];
    private string _historySearchText = "";

    public string HistorySearchText
    {
        get => _historySearchText;
        set
        {
            _historySearchText = value;
            OnPropertyChanged();
            FilterHistory();
        }
    }

    public bool HasHistoryEntries => HistoryEntries.Count > 0;
    public bool HasFilteredHistoryEntries => FilteredHistoryEntries.Count > 0;
    public bool HasHistorySearchText => !string.IsNullOrWhiteSpace(HistorySearchText);
    public string HistorySummaryText =>
        HistoryEntries.Count == 0
            ? "No repairs, workflows, or automation runs have been recorded yet."
            : HasHistorySearchText
                ? $"{FilteredHistoryEntries.Count} of {HistoryEntries.Count} activity entries shown"
                : $"{HistoryEntries.Count} activity entr{(HistoryEntries.Count == 1 ? "y" : "ies")}";
    public string HistoryEmptyStateTitle => HasHistorySearchText
        ? "No matching activity"
        : "No activity yet";
    public string HistoryEmptyStateText => HasHistorySearchText
        ? "Try a broader search or clear it to see the full receipt history."
        : "Run a repair, workflow, or automation and FixFox will keep the receipt here so you can review what changed later.";

    // ── Settings ──────────────────────────────────────────────────────────
    private AppSettings _settings = new();
    public AppSettings  Settings  { get => _settings; set { _settings = value; OnPropertyChanged(); } }
    public string AppVersionSummaryText => $"{ProductDisplayName} {SharedConstants.AppVersion}";
    public IReadOnlyList<string> BehaviorProfileOptions =>
        CanToggleAdvancedMode
            ? ["Standard", "Quiet", "Power User", "Work Laptop", "Home PC"]
            : ["Standard", "Quiet", "Work Laptop", "Home PC"];
    public IReadOnlyList<string> NotificationModeOptions { get; } = ["Quiet", "Important Only", "Standard"];
    public IReadOnlyList<string> SupportBundleExportLevelOptions =>
        CanUseTechnicianExports ? ["Basic", "Technician"] : ["Basic"];
    public IReadOnlyList<string> LandingPageOptions { get; } =
    [
        "Home",
        "Guided Diagnosis",
        "Repair Library",
        "Automation",
        "Device Health",
        "Windows Tools",
        "Support Package",
        "Activity",
        "Settings"
    ];
    public DeploymentConfiguration Deployment => _deployment.Current;
    public bool IsManagedDeployment => Deployment.ManagedMode;
    public string EditionSummaryText => $"{FormatEditionLabel(EditionSnapshot.Edition)} edition";
    public string DeploymentSummaryText =>
        IsManagedDeployment
            ? $"{(string.IsNullOrWhiteSpace(Deployment.OrganizationName) ? "Managed deployment" : Deployment.OrganizationName)} • {Branding.ManagedModeLabel}"
            : EditionSnapshot.Edition == AppEdition.ManagedServiceProvider ? "MSP deployment" : "Consumer deployment";
    public string SupportRoutingSummaryText
    {
        get
        {
            var contact = !string.IsNullOrWhiteSpace(Branding.SupportDisplayName)
                ? Branding.SupportDisplayName
                : ProductDisplayName;
            if (!string.IsNullOrWhiteSpace(Branding.SupportEmail) && !string.IsNullOrWhiteSpace(Branding.SupportPortalUrl))
                return $"{contact} routes through {Branding.SupportPortalLabel} and {Branding.SupportEmail}.";
            if (!string.IsNullOrWhiteSpace(Branding.SupportPortalUrl))
                return $"{contact} routes through {Branding.SupportPortalLabel}.";
            if (!string.IsNullOrWhiteSpace(Branding.SupportEmail))
                return $"{contact} can be reached at {Branding.SupportEmail}.";
            return $"{contact} uses the built-in guides on this device.";
        }
    }
    public bool HasSupportPortal => !string.IsNullOrWhiteSpace(Branding.SupportPortalUrl);
    public bool HasSupportEmail => !string.IsNullOrWhiteSpace(Branding.SupportEmail);
    public string SupportPortalLabel => string.IsNullOrWhiteSpace(Branding.SupportPortalLabel) ? "Open support guide" : Branding.SupportPortalLabel;
    public string SupportEmailAddress => Branding.SupportEmail;
    public CapabilityAvailability AdvancedModeCapability => _edition.Describe(ProductCapability.AdvancedMode);
    public CapabilityAvailability TechnicianExportCapability => _edition.Describe(ProductCapability.TechnicianExports);
    public CapabilityAvailability AdvancedToolboxCapability => _edition.Describe(ProductCapability.AdvancedToolbox);
    public bool CanToggleAdvancedMode => AdvancedModeCapability.State == CapabilityState.Available;
    public bool CanUseTechnicianExports => TechnicianExportCapability.State == CapabilityState.Available;
    public string AdvancedModeAvailabilityText => AdvancedModeCapability.Summary;
    public string SupportBundleDetailAvailabilityText => TechnicianExportCapability.Summary;
    public bool CanEditBehaviorProfile => string.IsNullOrWhiteSpace(Deployment.ForceBehaviorProfile);
    public bool CanEditNotificationMode => string.IsNullOrWhiteSpace(Deployment.ForceNotificationMode);
    public bool CanEditLandingPage => string.IsNullOrWhiteSpace(Deployment.ForceLandingPage);
    public bool CanEditSupportBundleDetail => string.IsNullOrWhiteSpace(Deployment.ForceSupportBundleExportLevel) && !Deployment.RestrictTechnicianExports;
    public bool HasManagedRestrictions =>
        IsManagedDeployment
        || Deployment.DisabledRepairCategories.Count > 0
        || Deployment.HiddenToolTitles.Count > 0
        || !CanEditBehaviorProfile
        || !CanEditNotificationMode
        || !CanEditLandingPage
        || !CanEditSupportBundleDetail
        || !CanToggleAdvancedMode;
    public string ManagedPolicySummaryText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Deployment.ManagedMessage))
                parts.Add(Deployment.ManagedMessage);
            if (!CanEditBehaviorProfile)
                parts.Add($"Behavior profile is fixed to {Deployment.ForceBehaviorProfile}.");
            if (!CanEditNotificationMode)
                parts.Add($"Notifications are fixed to {Deployment.ForceNotificationMode}.");
            if (!CanEditLandingPage)
                parts.Add($"Startup landing is fixed to {PageToLabel(LabelToPage(Deployment.ForceLandingPage))}.");
            if (!CanEditSupportBundleDetail)
                parts.Add("Support-package detail is restricted by deployment policy.");
            if (Deployment.DisabledRepairCategories.Count > 0)
                parts.Add($"{Deployment.DisabledRepairCategories.Count} repair category restriction(s) are active.");
            if (Deployment.HiddenToolTitles.Count > 0)
                parts.Add($"{Deployment.HiddenToolTitles.Count} Windows tool restriction(s) are active.");
            return parts.Count == 0 ? "No managed restrictions are active for this build." : string.Join(" ", parts);
        }
    }
    public string SelectedBehaviorProfile
    {
        get => string.IsNullOrWhiteSpace(_settings.BehaviorProfile) ? "Standard" : _settings.BehaviorProfile;
        set
        {
            if (!CanEditBehaviorProfile)
                return;
            if (string.Equals(_settings.BehaviorProfile, value, StringComparison.OrdinalIgnoreCase))
                return;
            if (!BehaviorProfileOptions.Contains(value, StringComparer.OrdinalIgnoreCase))
                value = "Standard";
            ApplyBehaviorProfile(value);
            OnPropertyChanged();
        }
    }
    public bool AdvancedModeEnabled
    {
        get => _settings.AdvancedMode;
        set
        {
            if (value && !CanToggleAdvancedMode)
                return;
            if (_settings.AdvancedMode == value) return;
            _settings.AdvancedMode = value;
            _deployment.ApplyPolicy(_settings);
            _settingsSvc.Save(_settings);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentProfileStatusText));
            OnPropertyChanged(nameof(ToolboxGroups));
            RefreshCapabilityScopedContent();
            RefreshCommandPalette();
        }
    }
    public string SelectedLandingPage
    {
        get => PageToLabel(ParseLandingPage(_settings.DefaultLandingPage));
        set
        {
            if (!CanEditLandingPage)
                return;
            var page = LabelToPage(value);
            if (string.Equals(_settings.DefaultLandingPage, page.ToString(), StringComparison.OrdinalIgnoreCase))
                return;

            _settings.DefaultLandingPage = page.ToString();
            SaveSettings();
            OnPropertyChanged();
        }
    }
    public string SelectedNotificationMode
    {
        get => string.IsNullOrWhiteSpace(_settings.NotificationMode) ? "Standard" : _settings.NotificationMode;
        set
        {
            if (!CanEditNotificationMode)
                return;
            if (string.Equals(_settings.NotificationMode, value, StringComparison.OrdinalIgnoreCase))
                return;

            _settings.NotificationMode = value;
            SaveSettings();
            OnPropertyChanged();
        }
    }
    public string SelectedSupportBundleExportLevel
    {
        get => string.IsNullOrWhiteSpace(_settings.SupportBundleExportLevel) ? "Basic" : _settings.SupportBundleExportLevel;
        set
        {
            if (!CanEditSupportBundleDetail)
                return;
            if (!SupportBundleExportLevelOptions.Contains(value, StringComparer.OrdinalIgnoreCase))
                value = "Basic";
            if (string.Equals(_settings.SupportBundleExportLevel, value, StringComparison.OrdinalIgnoreCase))
                return;

            _settings.SupportBundleExportLevel = value;
            _deployment.ApplyPolicy(_settings);
            SaveSettings();
            OnPropertyChanged();
        }
    }
    public string SelectedAutomationQuietHoursStart
    {
        get => string.IsNullOrWhiteSpace(_settings.AutomationQuietHoursStart) ? "22:00" : _settings.AutomationQuietHoursStart;
        set
        {
            _settings.AutomationQuietHoursStart = value;
            SaveSettings();
            OnPropertyChanged();
        }
    }
    public string SelectedAutomationQuietHoursEnd
    {
        get => string.IsNullOrWhiteSpace(_settings.AutomationQuietHoursEnd) ? "07:00" : _settings.AutomationQuietHoursEnd;
        set
        {
            _settings.AutomationQuietHoursEnd = value;
            SaveSettings();
            OnPropertyChanged();
        }
    }
    public string FirstRunSummaryText =>
        Snapshot is null
            ? $"{ProductDisplayName} will establish a lightweight device baseline after setup."
            : $"{(Snapshot.PendingUpdateCount > 0 ? $"{Snapshot.PendingUpdateCount} update(s) waiting" : "Updates look steady")} | " +
              $"{(Snapshot.DiskFreeGb <= 20 ? $"{Snapshot.DiskFreeGb} GB free on C:" : "Storage is in a healthy range")} | " +
              $"{(Snapshot.DefenderEnabled ? "Defender looks enabled" : "Security status needs review")}";
    public bool HasSuppressedItems => _settings.IgnoredRecommendationKeys.Count > 0 || _settings.SnoozedAlertKeys.Count > 0;
    public SettingsLoadStatus SettingsLoadStatus => _settingsSvc.LastLoadStatus;
    public bool HasSettingsLoadNotice => SettingsLoadStatus.HasRecoveryNotice;
    public string SettingsLoadStatusText => SettingsLoadStatus.Summary;
    public string PrivilegeModeText => _elevation.IsElevated
        ? "Running with administrator rights. Deep repairs and scheduled maintenance can complete without extra prompts."
        : $"Running as a standard user. {ProductDisplayName} requests elevation only when a repair truly needs it.";
    public string DataFolderPath => SharedConstants.AppDataDir;
    public string AppLogPath => SharedConstants.AppLogFile;
    public string VerifyLogPath => SharedConstants.VerifyLogFile;
    public string QuickStartPath => Path.Combine(AppContext.BaseDirectory, "Docs", "Quick-Start.md");
    public string PrivacyGuidePath => Path.Combine(AppContext.BaseDirectory, "Docs", "Privacy-and-Data.md");
    public string RecoveryGuidePath => Path.Combine(AppContext.BaseDirectory, "Docs", "Recovery-and-Resume.md");
    public string SupportBundleGuidePath => Path.Combine(AppContext.BaseDirectory, "Docs", "Support-Packages.md");
    public string TroubleshootingGuidePath => Path.Combine(AppContext.BaseDirectory, "Docs", "Troubleshooting-and-FAQ.md");
    public string ReleaseNotesPath => LastUpdateInfo?.ReleaseNotesPath ?? Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
    private string _startupRecoverySummaryText = "";
    public string StartupRecoverySummaryText
    {
        get => _startupRecoverySummaryText;
        set
        {
            _startupRecoverySummaryText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStartupRecoverySummary));
        }
    }
    public bool HasStartupRecoverySummary => !string.IsNullOrWhiteSpace(StartupRecoverySummaryText);
    private bool _runHealthCheckAfterSetup = true;
    public bool RunHealthCheckAfterSetup
    {
        get => _runHealthCheckAfterSetup;
        set { _runHealthCheckAfterSetup = value; OnPropertyChanged(); }
    }
    public bool HasActiveWork => ScanRunning || IsBundleRunning || IsRunbookRunning || (WizardVisible && WizardFix is not null);
    public string ActiveWorkSummary =>
        ScanRunning ? "A health scan is still running." :
        IsBundleRunning ? $"\"{RunningBundle?.Title}\" is still running." :
        IsRunbookRunning ? $"\"{RunningRunbookTitle}\" is still running." :
        WizardVisible && WizardFix is not null ? $"\"{WizardFix.Title}\" is still waiting on the next guided step." :
        "";

    public int  UnreadNotifCount => _notifs.UnreadCount;
    public IReadOnlyList<AppNotification> NotificationEntries => _notifs.All.ToList();
    public bool HasNotifications => _notifs.All.Count > 0;
    public string NotificationSummaryText =>
        _notifs.All.Count == 0
            ? "No active alerts in this session."
            : $"{UnreadNotifCount} alert{(UnreadNotifCount == 1 ? "" : "s")} still need review";

    // ── Command Palette ───────────────────────────────────────────────────
    public BrandingConfiguration Branding => _branding.Current;
    public EditionCapabilitySnapshot EditionSnapshot => _edition.GetSnapshot();
    public IReadOnlyList<KnowledgeBaseEntry> KnowledgeBaseEntries => _knowledgeBase.Entries;
    public bool HasKnowledgeBaseEntries => KnowledgeBaseEntries.Count > 0;
    public string KnowledgeBaseSummaryText => HasKnowledgeBaseEntries
        ? $"{KnowledgeBaseEntries.Count} support resource{(KnowledgeBaseEntries.Count == 1 ? "" : "s")} ready for {Branding.SupportDisplayName}."
        : "No support links are configured for this build yet.";
    public bool CanCreateEvidenceBundle => EditionSnapshot.EvidenceBundles == CapabilityState.Available;
    public string SupportPackageAvailabilityText => CanCreateEvidenceBundle
        ? $"Create a local support package with triage notes, device health, and recent repair history. {SupportBundleDetailAvailabilityText}"
        : _edition.Describe(ProductCapability.EvidenceBundles).Summary;
    public string MaintenanceSummaryText =>
        LastHealthCheckReport is null
            ? "Run a quick scan or a full maintenance workflow to surface the next safe actions."
            : $"{LastHealthCheckReport.OverallScore}/100 overall health with {LastHealthCheckReport.Recommendations.Count} recommended action(s).";

    private InterruptedOperationState? _interruptedOperation;
    public InterruptedOperationState? InterruptedOperation
    {
        get => _interruptedOperation;
        set
        {
            _interruptedOperation = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasInterruptedOperation));
            OnPropertyChanged(nameof(CanResumeInterruptedRepair));
            OnPropertyChanged(nameof(ShellStatusText));
        }
    }

    public bool HasInterruptedOperation => InterruptedOperation is not null;
    public bool CanResumeInterruptedRepair =>
        InterruptedOperation is not null
        && InterruptedOperation.CanResume
        && string.Equals(InterruptedOperation.OperationType, "guided", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(InterruptedOperation.OperationTargetId);
    public bool HasLastUsefulAction => HistoryEntries.Count > 0;
    public string LastUsefulActionLabel
    {
        get
        {
            var latest = HistoryEntries.FirstOrDefault();
            if (latest is null)
                return "Run Last Useful Action";

            return !string.IsNullOrWhiteSpace(latest.RunbookId)
                ? $"Run {latest.FixTitle}"
                : $"Run {latest.FixTitle}";
        }
    }

    private EvidenceBundleManifest? _lastEvidenceBundle;
    public EvidenceBundleManifest? LastEvidenceBundle
    {
        get => _lastEvidenceBundle;
        set
        {
            _lastEvidenceBundle = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasEvidenceBundle));
            OnPropertyChanged(nameof(ShellStatusText));
        }
    }

    public bool HasEvidenceBundle => LastEvidenceBundle is not null;

    private string _lastEvidenceBundlePreviewText = "";
    public string LastEvidenceBundlePreviewText
    {
        get => _lastEvidenceBundlePreviewText;
        set
        {
            _lastEvidenceBundlePreviewText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasEvidenceBundlePreview));
        }
    }

    public bool HasEvidenceBundlePreview => !string.IsNullOrWhiteSpace(LastEvidenceBundlePreviewText);

    private RunbookExecutionSummary? _lastRunbookSummary;
    public RunbookExecutionSummary? LastRunbookSummary
    {
        get => _lastRunbookSummary;
        set { _lastRunbookSummary = value; OnPropertyChanged(); }
    }

    private AppUpdateInfo? _lastUpdateInfo;
    public AppUpdateInfo? LastUpdateInfo
    {
        get => _lastUpdateInfo;
        set
        {
            _lastUpdateInfo = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UpdateStatusSummaryText));
        }
    }
    public string UpdateStatusSummaryText =>
        LastUpdateInfo is null
            ? "Update status is unavailable right now."
            : $"{LastUpdateInfo.SourceName}: {LastUpdateInfo.Summary}";

    private bool   _commandPaletteOpen;
    private string _commandPaletteQuery = "";

    public bool IsCommandPaletteOpen
    {
        get => _commandPaletteOpen;
        set { _commandPaletteOpen = value; OnPropertyChanged(); }
    }

    public string CommandPaletteQuery
    {
        get => _commandPaletteQuery;
        set
        {
            _commandPaletteQuery = value;
            OnPropertyChanged();
            RefreshCommandPalette();
        }
    }

    public ObservableCollection<CommandPaletteItem> CommandPaletteResults { get; } = [];

    public void RefreshCommandPalette()
    {
        CommandPaletteResults.Clear();
        var results = _commandPaletteService.Search(
            _commandPaletteQuery,
            PinnedFixes.ToList(),
            FavoriteFixes.ToList(),
            RecentlyRunFixes.ToList(),
            Runbooks.ToList(),
            MaintenanceProfiles.ToList(),
            SupportCenters.ToList(),
            ToolboxGroups.ToList()).ToList();

        results.AddRange(BuildAutomationCommandPaletteItems());

        foreach (var r in results
                     .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First())
                     .Take(16))
            CommandPaletteResults.Add(r);

        OnPropertyChanged(nameof(HasCommandPaletteResults));
    }

    private List<CommandPaletteItem> BuildAutomationCommandPaletteItems()
    {
        var items = new List<CommandPaletteItem>
        {
            new()
            {
                Id = "automation-open-center",
                Title = "Automation Center",
                Subtitle = "Open schedules, watchers, and automation history.",
                Section = "Automation",
                Hint = "Open page",
                Glyph = "\uE8B1",
                SearchText = "automation center schedules watchers history",
                Kind = CommandPaletteItemKind.Page,
                TargetPage = Page.Bundles
            },
            new()
            {
                Id = "automation-run-quick-health",
                Title = "Run Quick Health Scan",
                Subtitle = "Run the low-noise health check now.",
                Section = "Automation",
                Hint = "Run now",
                Glyph = "\uE9D2",
                SearchText = "automation quick health scan run",
                Kind = CommandPaletteItemKind.Action,
                TargetId = "run-automation:quick-health-check"
            },
            new()
            {
                Id = "automation-run-safe-maintenance",
                Title = "Run Safe Maintenance Now",
                Subtitle = "Run the conservative maintenance workflow now.",
                Section = "Automation",
                Hint = "Run now",
                Glyph = "\uE768",
                SearchText = "automation maintenance safe run now",
                Kind = CommandPaletteItemKind.Action,
                TargetId = "run-automation:safe-maintenance"
            },
            new()
            {
                Id = "automation-toggle-pause",
                Title = AutomationPaused ? "Resume Automation" : "Pause Automation For 1 Hour",
                Subtitle = AutomationPaused ? "Resume scheduled tasks and watchers." : "Pause automated runs for the next hour.",
                Section = "Automation",
                Hint = AutomationPaused ? "Resume" : "Pause",
                Glyph = AutomationPaused ? "\uE768" : "\uE769",
                SearchText = "automation pause resume quiet",
                Kind = CommandPaletteItemKind.Action,
                TargetId = AutomationPaused ? "automation:resume" : "automation:pause-hour"
            }
        };

        if (CanResumeInterruptedRepair)
        {
            items.Add(new CommandPaletteItem
            {
                Id = "automation-resume-interrupted",
                Title = "Resume Interrupted Repair",
                Subtitle = "Continue the last guided repair that can still be resumed.",
                Section = "Automation",
                Hint = "Resume",
                Glyph = "\uE72A",
                SearchText = "automation interrupted repair resume",
                Kind = CommandPaletteItemKind.Action,
                TargetId = "automation:resume-interrupted"
            });
        }

        return string.IsNullOrWhiteSpace(_commandPaletteQuery)
            ? items
            : items.Where(item =>
                    item.Title.Contains(_commandPaletteQuery, StringComparison.OrdinalIgnoreCase)
                    || item.Subtitle.Contains(_commandPaletteQuery, StringComparison.OrdinalIgnoreCase)
                    || item.SearchText.Contains(_commandPaletteQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();
    }

    private void SyncAutomationSchedules()
    {
        foreach (var rule in _settings.AutomationRules)
        {
            try
            {
                _scheduler.SyncAutomationRule(rule);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Automation schedule sync failed for {rule.Id}: {ex.Message}");
            }
        }
    }

    private void RefreshCapabilityScopedContent()
    {
        Runbooks.Clear();
        foreach (var runbook in _allRunbooks.Where(CanAccessRunbook))
            Runbooks.Add(runbook);

        Bundles.Clear();
        foreach (var bundle in _allBundles.Where(CanAccessBundle))
            Bundles.Add(bundle);

        MaintenanceProfiles.Clear();
        foreach (var profile in _allMaintenanceProfiles.Where(CanAccessMaintenanceProfile))
            MaintenanceProfiles.Add(profile);

        OnPropertyChanged(nameof(BehaviorProfileOptions));
        OnPropertyChanged(nameof(SupportBundleExportLevelOptions));
        OnPropertyChanged(nameof(ToolboxGroups));
        OnPropertyChanged(nameof(HasMaintenanceProfiles));
        OnPropertyChanged(nameof(HasVisibleBundles));
        RefreshAutomationWorkspace();
    }

    private IReadOnlyList<ToolboxGroup> BuildVisibleToolboxGroups()
    {
        return _toolbox.Groups
            .Select(group => new ToolboxGroup
            {
                Title = group.Title,
                Description = group.Description,
                Entries = group.Entries.Where(CanAccessToolboxEntry).ToList()
            })
            .Where(group => group.Entries.Count > 0)
            .ToList();
    }

    private bool CanAccessToolboxEntry(ToolboxEntry entry)
    {
        if (Deployment.HiddenToolTitles.Contains(entry.Title, StringComparer.OrdinalIgnoreCase))
            return false;

        if (EditionSnapshot.Edition < entry.MinimumEdition)
            return false;

        if (entry.RequiresAdvancedMode && !AdvancedModeEnabled)
            return false;

        if (entry.RequiredCapability != ProductCapability.None
            && _edition.GetState(entry.RequiredCapability) != CapabilityState.Available)
            return false;

        return true;
    }

    private bool CanAccessRunbook(RunbookDefinition runbook) =>
        EditionSnapshot.Edition >= runbook.MinimumEdition
        && _edition.GetState(ProductCapability.Runbooks) == CapabilityState.Available;

    private bool CanAccessBundle(FixBundle bundle) =>
        AdvancedModeEnabled && _edition.GetState(ProductCapability.AdvancedAutomation) == CapabilityState.Available;

    private bool CanAccessMaintenanceProfile(MaintenanceProfileDefinition profile)
    {
        if (profile.LaunchAction.Kind != SupportActionKind.Runbook)
            return true;

        var runbook = _allRunbooks.FirstOrDefault(item =>
            string.Equals(item.Id, profile.LaunchAction.TargetId, StringComparison.OrdinalIgnoreCase));
        return runbook is null || CanAccessRunbook(runbook);
    }

    private bool CanAccessFix(FixItem fix)
    {
        var repair = _repairCatalog.GetRepair(fix.Id);
        if (repair is null)
            return true;

        if (EditionSnapshot.Edition < repair.MinimumEdition)
            return false;

        if (repair.Tier != RepairTier.SafeUser
            && _edition.GetState(ProductCapability.DeepRepairs) != CapabilityState.Available)
            return false;

        if (Deployment.DisabledRepairCategories.Contains(repair.MasterCategoryId, StringComparer.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private string GetFixAvailabilityMessage(FixItem fix)
    {
        var repair = _repairCatalog.GetRepair(fix.Id);
        if (repair is null)
            return "This repair is unavailable in the current configuration.";

        if (EditionSnapshot.Edition < repair.MinimumEdition)
            return $"{fix.Title} is available in {FormatEditionLabel(repair.MinimumEdition)}.";

        if (repair.Tier != RepairTier.SafeUser
            && _edition.GetState(ProductCapability.DeepRepairs) != CapabilityState.Available)
            return _edition.Describe(ProductCapability.DeepRepairs).Summary;

        if (Deployment.DisabledRepairCategories.Contains(repair.MasterCategoryId, StringComparer.OrdinalIgnoreCase))
            return "This repair category is unavailable in the current managed deployment.";

        return "This repair is unavailable in the current configuration.";
    }

    public void OpenCommandPalette()
    {
        CommandPaletteQuery  = "";
        IsCommandPaletteOpen = true;
    }

    public void CloseCommandPalette() => IsCommandPaletteOpen = false;

    // ── Status bar ────────────────────────────────────────────────────────
    private string _currentTimeText = "";
    public string CurrentTimeText
    {
        get => _currentTimeText;
        set { _currentTimeText = value; OnPropertyChanged(); }
    }

    private string _lastScanTimeText = "Not yet scanned";
    public string LastScanTimeText
    {
        get => _lastScanTimeText;
        set { _lastScanTimeText = value; OnPropertyChanged(); }
    }

    public int    FixesTodayCount => _repairHistory.Entries.Count(e => e.Timestamp.Date == DateTime.Today && !string.IsNullOrWhiteSpace(e.FixId));
    public string FixesTodayText  => FixesTodayCount > 0 ? $"{FixesTodayCount} fixes today" : "";

    // ── Privacy notice ────────────────────────────────────────────────────
    private bool _showPrivacyNotice;
    public bool ShowPrivacyNotice
    {
        get => _showPrivacyNotice;
        set { _showPrivacyNotice = value; OnPropertyChanged(); }
    }

    public void DismissPrivacyNotice()
    {
        ShowPrivacyNotice = false;
        _settings.PrivacyNoticeDismissed = true;
        _settings.OnboardingDismissed = true;
        _settingsSvc.Save(_settings);
    }

    public async Task CompleteOnboardingAsync()
    {
        _settings.RunFirstHealthCheckAfterSetup = RunHealthCheckAfterSetup;
        DismissPrivacyNotice();
        if (RunHealthCheckAfterSetup)
            await RunQuickScanAsync();
    }

    public void ApplyBehaviorProfile(string profile)
    {
        profile = string.IsNullOrWhiteSpace(profile) ? "Standard" : profile;
        if (!BehaviorProfileOptions.Contains(profile, StringComparer.OrdinalIgnoreCase))
            profile = "Standard";
        ProductizationPolicies.ApplyBehaviorProfile(_settings, profile);
        _deployment.ApplyPolicy(_settings);

        _settingsSvc.Save(_settings);
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(SelectedBehaviorProfile));
        OnPropertyChanged(nameof(SelectedNotificationMode));
        OnPropertyChanged(nameof(SelectedSupportBundleExportLevel));
        OnPropertyChanged(nameof(SelectedLandingPage));
        OnPropertyChanged(nameof(AdvancedModeEnabled));
        OnPropertyChanged(nameof(CurrentProfileStatusText));
        OnPropertyChanged(nameof(BehaviorProfileOptions));
        OnPropertyChanged(nameof(SupportBundleExportLevelOptions));
        RefreshCapabilityScopedContent();
        SyncAutomationSchedules();
        RefreshAutomationWorkspace();
        RefreshActiveFixes();
        RefreshCommandPalette();
    }

    public void SnoozeDashboardAlert(DashboardAlert alert)
    {
        if (string.IsNullOrWhiteSpace(alert.Key)) return;
        if (!_settings.SnoozedAlertKeys.Contains(alert.Key, StringComparer.OrdinalIgnoreCase))
            _settings.SnoozedAlertKeys.Add(alert.Key);
        _settingsSvc.Save(_settings);
        RefreshDashboardWorkspace();
    }

    public void IgnoreRecommendation(ProactiveRecommendation recommendation)
    {
        if (string.IsNullOrWhiteSpace(recommendation.Key)) return;
        if (!_settings.IgnoredRecommendationKeys.Contains(recommendation.Key, StringComparer.OrdinalIgnoreCase))
            _settings.IgnoredRecommendationKeys.Add(recommendation.Key);
        _settingsSvc.Save(_settings);
        RefreshProactiveRecommendations();
    }

    public void ResetSuppressedItems()
    {
        _settings.IgnoredRecommendationKeys.Clear();
        _settings.SnoozedAlertKeys.Clear();
        _settingsSvc.Save(_settings);
        RefreshProactiveRecommendations();
        RefreshDashboardWorkspace();
        OnPropertyChanged(nameof(HasSuppressedItems));
    }

    public void RestoreDefaultSettings()
    {
        _settingsSvc.ResetToDefaults();
        Settings = _settingsSvc.Load();
        _deployment.ApplyPolicy(_settings);
        _automationCoordinator.EnsureRules(_settings);
        _settingsSvc.Save(_settings);
        RunHealthCheckAfterSetup = _settings.RunFirstHealthCheckAfterSetup;
        OnPropertyChanged(nameof(SelectedBehaviorProfile));
        OnPropertyChanged(nameof(SelectedLandingPage));
        OnPropertyChanged(nameof(SelectedNotificationMode));
        OnPropertyChanged(nameof(SelectedSupportBundleExportLevel));
        OnPropertyChanged(nameof(AdvancedModeEnabled));
        OnPropertyChanged(nameof(BehaviorProfileOptions));
        OnPropertyChanged(nameof(SupportBundleExportLevelOptions));
        OnPropertyChanged(nameof(HasSuppressedItems));
        OnPropertyChanged(nameof(SettingsLoadStatus));
        OnPropertyChanged(nameof(SettingsLoadStatusText));
        OnPropertyChanged(nameof(HasSettingsLoadNotice));
        OnPropertyChanged(nameof(CurrentProfileStatusText));
        RefreshCapabilityScopedContent();
        SyncAutomationSchedules();
        RefreshAutomationWorkspace();
        RefreshActiveFixes();
        RefreshCommandPalette();
    }

    // ── Recent searches ───────────────────────────────────────────────────
    public ObservableCollection<string> RecentSearches { get; } = [];

    private void AddRecentSearch(string query)
    {
        query = query.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3) return;
        if (RecentSearches.Contains(query)) return;
        RecentSearches.Insert(0, query);
        while (RecentSearches.Count > 5) RecentSearches.RemoveAt(RecentSearches.Count - 1);
        _settings.RecentSearches = [.. RecentSearches];
        OnPropertyChanged(nameof(HasRecentSearches));
    }

    // ── Favorites ─────────────────────────────────────────────────────────
    public void ToggleFavorite(FixItem fix)
    {
        fix.IsFavorite = !fix.IsFavorite;
        if (fix.IsFavorite)
        {
            if (!_settings.FavoriteFixIds.Contains(fix.Id))
                _settings.FavoriteFixIds.Add(fix.Id);
            if (!FavoriteFixes.Contains(fix)) FavoriteFixes.Add(fix);
        }
        else
        {
            _settings.FavoriteFixIds.Remove(fix.Id);
            FavoriteFixes.Remove(fix);
        }
        _settingsSvc.Save(_settings);
        OnPropertyChanged(nameof(HasPinnedFixes));
    }

    // ── Pinned fixes ──────────────────────────────────────────────────────
    public void TogglePin(FixItem fix)
    {
        fix.IsPinned = !fix.IsPinned;
        if (fix.IsPinned)
        {
            if (!_settings.PinnedFixIds.Contains(fix.Id))
                _settings.PinnedFixIds.Add(fix.Id);
            if (!PinnedFixes.Contains(fix)) PinnedFixes.Add(fix);
        }
        else
        {
            _settings.PinnedFixIds.Remove(fix.Id);
            PinnedFixes.Remove(fix);
        }
        _settingsSvc.Save(_settings);
    }

    // ── Clock timer ───────────────────────────────────────────────────────
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _automationHeartbeat;

    // ── Constructor ───────────────────────────────────────────────────────
    public MainViewModel(
        IScriptService       scripts,
        IFixCatalogService   catalog,
        IQuickScanService    scanner,
        ISystemInfoService   sysInfo,
        ILogService          log,
        INotificationService notifs,
        ISettingsService     settingsSvc,
        IAppLogger           logger,
        IElevationService    elevation,
        ITriageEngine        triage,
        IRunbookCatalogService runbookCatalog,
        IRunbookExecutionService runbookExecution,
        IRepairExecutionService repairExecution,
        IHealthCheckService  healthCheck,
        IEvidenceBundleService evidenceBundles,
        IGuidedRepairExecutionService guidedRepairExecution,
        IKnowledgeBaseService knowledgeBase,
        IBrandingConfigurationService branding,
        IDeploymentConfigurationService deployment,
        IEditionCapabilityService edition,
        IAppUpdateService    updates,
        IStatePersistenceService statePersistence,
        IRepairHistoryService repairHistory,
        IRepairCatalogService repairCatalog,
        InstalledProgramsService installedPrograms,
        StartupAppsService startupApps,
        StorageInsightsService storageInsights,
        SchedulerService        scheduler,
        IToolboxService toolbox,
        IMaintenanceProfileService maintenanceProfileService,
        ISupportCenterService supportCenterService,
        ICommandPaletteService commandPaletteService,
        IDashboardWorkspaceService dashboardWorkspace,
        IAutomationHistoryService automationHistory,
        IAutomationCoordinatorService automationCoordinator)
    {
        _scripts     = scripts;
        _catalog     = catalog;
        _scanner     = scanner;
        _sysInfo     = sysInfo;
        _log         = log;
        _notifs      = notifs;
        _settingsSvc = settingsSvc;
        _logger      = logger;
        _elevation   = elevation;
        _triage      = triage;
        _runbookCatalog = runbookCatalog;
        _runbookExecution = runbookExecution;
        _repairExecution = repairExecution;
        _healthCheck = healthCheck;
        _evidenceBundles = evidenceBundles;
        _guidedRepairExecution = guidedRepairExecution;
        _knowledgeBase = knowledgeBase;
        _branding = branding;
        _deployment = deployment;
        _edition = edition;
        _updates = updates;
        _statePersistence = statePersistence;
        _repairHistory = repairHistory;
        _repairCatalog = repairCatalog;
        _installedPrograms = installedPrograms;
        _startupApps = startupApps;
        _storageInsights = storageInsights;
        _scheduler         = scheduler;
        _toolbox = toolbox;
        _maintenanceProfileService = maintenanceProfileService;
        _supportCenterService = supportCenterService;
        _commandPaletteService = commandPaletteService;
        _dashboardWorkspace = dashboardWorkspace;
        _automationHistory = automationHistory;
        _automationCoordinator = automationCoordinator;

        _settings = settingsSvc.Load();
        _deployment.ApplyPolicy(_settings);
        _isSidebarCollapsed = _settings.SidebarCollapsed;
        RunHealthCheckAfterSetup = _settings.RunFirstHealthCheckAfterSetup;

        foreach (var c in _catalog.Categories) Categories.Add(c);
        _allBundles.AddRange(_catalog.Bundles);
        _allRunbooks.AddRange(_runbookCatalog.Runbooks);
        _allMaintenanceProfiles.AddRange(_maintenanceProfileService.Profiles);
        _automationCoordinator.EnsureRules(_settings);
        SyncAutomationSchedules();
        RefreshCapabilityScopedContent();

        // Restore last selected category
        if (!string.IsNullOrEmpty(_settings.LastFixCategory))
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == _settings.LastFixCategory)
                               ?? Categories.FirstOrDefault();
        else
            SelectedCategory = Categories.FirstOrDefault();

        // Restore favorites and pins
        foreach (var id in _settings.FavoriteFixIds)
        {
            var fix = _catalog.GetById(id);
            if (fix is not null) { fix.IsFavorite = true; FavoriteFixes.Add(fix); }
        }
        foreach (var id in _settings.PinnedFixIds)
        {
            var fix = _catalog.GetById(id);
            if (fix is not null) { fix.IsPinned = true; PinnedFixes.Add(fix); }
        }

        // Restore recent searches
        foreach (var s in _settings.RecentSearches) RecentSearches.Add(s);

        SelectedWeeklyTuneUpDay  = _settings.WeeklyTuneUpDay;
        SelectedWeeklyTuneUpTime = _settings.WeeklyTuneUpTime;
        RefreshWeeklyTuneUpSchedule();

        RefreshBreadcrumb();
        RefreshHistory();
        RefreshRecentlyRun();
        RefreshSupportCenters();
        RefreshAutomationWorkspace();
        var startupNotices = new List<string>();
        if (_settingsSvc.LastLoadStatus.HasRecoveryNotice)
            startupNotices.Add(_settingsSvc.LastLoadStatus.Summary);

        var interruptedDecision = ProductizationPolicies.EvaluateInterruptedState(_statePersistence.Load(), _settings, DateTime.Now);
        if (interruptedDecision.ClearState)
            _statePersistence.Clear();

        InterruptedOperation = interruptedDecision.State;
        if (!string.IsNullOrWhiteSpace(interruptedDecision.Notice))
            startupNotices.Add(interruptedDecision.Notice);

        if (interruptedDecision.ShouldResume
            && InterruptedOperation is not null
            && string.Equals(InterruptedOperation.OperationType, "guided", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(InterruptedOperation.OperationTargetId))
        {
            var interruptedFix = _catalog.GetById(InterruptedOperation.OperationTargetId);
            if (interruptedFix is not null && CanAccessFix(interruptedFix))
            {
                StartWizard(interruptedFix);
                GuidedRepairState = _guidedRepairExecution.BuildResumeState(interruptedFix, InterruptedOperation);
                if (GuidedRepairState is not null)
                    _wizardStep = GuidedRepairState.CurrentStepIndex;
                RefreshWizard();
            }
            else if (interruptedFix is not null)
            {
                startupNotices.Add(GetFixAvailabilityMessage(interruptedFix));
                _statePersistence.Clear();
                InterruptedOperation = null;
            }
        }

        StartupRecoverySummaryText = string.Join(" ", startupNotices.Where(note => !string.IsNullOrWhiteSpace(note)).Distinct(StringComparer.OrdinalIgnoreCase));
        RefreshDashboardWorkspace();
        RefreshCommandPalette();

        ShowPrivacyNotice = !_settings.OnboardingDismissed;

        // Clock
        _clock = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clock.Tick += (_, _) => CurrentTimeText = DateTime.Now.ToString("h:mm:ss tt");
        CurrentTimeText = DateTime.Now.ToString("h:mm:ss tt");
        _clock.Start();

        _automationHeartbeat = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(10)
        };
        _automationHeartbeat.Tick += async (_, _) =>
        {
            await RunAutomationHeartbeatAsync();
        };
        _automationHeartbeat.Start();

        _ = LoadUpdateInfoAsync();
    }

    // ── Quick Scan ────────────────────────────────────────────────────────
    public async Task RunQuickScanAsync()
    {
        if (ScanRunning) return;
        ScanRunning    = true;
        ScanStatusText = "Scanning your PC\u2026";
        ScanResults.Clear();

        IReadOnlyList<ScanResult> results;
        try
        {
            results = await _scanner.ScanAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Quick scan failed", ex);
            results = [];
            ScanStatusText = $"Quick scan failed. {ProductDisplayName} recorded the error and kept the app running.";
        }

        foreach (var r in results) ScanResults.Add(r);

        if (_settings.ShowNotifications)
            foreach (var r in results.Where(ShouldSurfaceNotification))
                _notifs.AddFromScanResult(r);

        OnPropertyChanged(nameof(ScanCriticalCount));
        OnPropertyChanged(nameof(ScanWarningCount));
        OnPropertyChanged(nameof(ScanGoodCount));
        OnPropertyChanged(nameof(OverallHealth));
        OnPropertyChanged(nameof(OverallHealthColor));
        RefreshNotifications();

        ScanRunning     = false;
        var time        = DateTime.Now.ToString("h:mm tt");
        ScanStatusText  = $"Last scan: {time} \u2014 {ScanCriticalCount} critical, {ScanWarningCount} warnings, {ScanGoodCount} OK";
        LastScanTimeText = time;
        await RunFullHealthCheckAsync();
        RefreshAutomationWorkspace();
    }

    public async Task RunFullHealthCheckAsync()
    {
        try
        {
            LastHealthCheckReport = await _healthCheck.RunFullAsync();
            RefreshProactiveRecommendations();
            OnPropertyChanged(nameof(MaintenanceSummaryText));
            RefreshDashboardWorkspace();
            RefreshAutomationWorkspace();
        }
        catch (Exception ex)
        {
            _logger.Error("Full health check failed", ex);
        }
    }

    // ── Fix execution ─────────────────────────────────────────────────────
    public async Task RunFixAsync(FixItem fix)
    {
        try
        {
            var result = await _repairExecution.ExecuteAsync(fix, SymptomInput);
            fix.LastOutput = result.Output;
        }
        catch (Exception ex)
        {
            _logger.Error($"Repair failed unexpectedly for {fix.Id}", ex);
            fix.Status     = FixStatus.Failed;
            fix.LastOutput = $"Unexpected error: {ex.Message}";
        }

        InterruptedOperation = _statePersistence.Load();
        RefreshHistory();
        RefreshRecentlyRun();
        RefreshDashboardWorkspace();
        RefreshAutomationWorkspace();
        OnPropertyChanged(nameof(FixesTodayCount));
        OnPropertyChanged(nameof(FixesTodayText));
        OnPropertyChanged(nameof(ActiveFixes));
        OnPropertyChanged(nameof(SymptomResults));
    }

    public async Task RunFixByIdAsync(string fixId)
    {
        var fix = _catalog.GetById(fixId);
        if (fix is null) return;
        if (!CanAccessFix(fix))
        {
            fix.Status = FixStatus.Failed;
            fix.LastOutput = GetFixAvailabilityMessage(fix);
            RefreshActiveFixes();
            return;
        }
        if (fix.Type == FixType.Guided) { StartWizard(fix); return; }
        await RunFixAsync(fix);
    }

    // ── Bundle ────────────────────────────────────────────────────────────
    public async Task RunBundleAsync(FixBundle bundle)
    {
        if (IsBundleRunning) return;
        RunningBundle  = bundle;
        BundleTotal    = bundle.FixIds.Count;
        BundleProgress = 0;
        BundleStatus   = $"Starting {bundle.Title}\u2026";

        var mappedRunbook = Runbooks.FirstOrDefault(r => string.Equals(r.Id, bundle.Id, StringComparison.OrdinalIgnoreCase));
        if (mappedRunbook is not null)
        {
            LastRunbookSummary = await _runbookExecution.ExecuteAsync(mappedRunbook, SymptomInput);
            BundleProgress = BundleTotal;
            BundleStatus = LastRunbookSummary.Summary;
            InterruptedOperation = _statePersistence.Load();
            RefreshHistory();
            RefreshRecentlyRun();
            RefreshDashboardWorkspace();
            RefreshAutomationWorkspace();
            OnPropertyChanged(nameof(FixesTodayCount));
            OnPropertyChanged(nameof(FixesTodayText));
            await Task.Delay(4000);
            RunningBundle = null;
            BundleStatus = "";
            return;
        }

        var failCount = 0;
        foreach (var id in bundle.FixIds)
        {
            var fix = _catalog.GetById(id);
            if (fix is null || fix.Type == FixType.Guided) { BundleProgress++; continue; }

            BundleStatus = $"Running: {fix.Title}\u2026";
            try
            {
                var result = await _repairExecution.ExecuteAsync(fix, SymptomInput);
                if (!result.Success) failCount++;
            }
            catch { failCount++; }

            BundleProgress++;
        }

        RefreshHistory();
        RefreshRecentlyRun();
        RefreshAutomationWorkspace();
        OnPropertyChanged(nameof(FixesTodayCount));
        OnPropertyChanged(nameof(FixesTodayText));

        BundleStatus = failCount == 0
            ? $"{bundle.Title} complete!"
            : $"{bundle.Title} complete \u2014 {failCount} fix{(failCount > 1 ? "es" : "")} had issues.";

        InterruptedOperation = _statePersistence.Load();
        await Task.Delay(4000);
        RunningBundle = null;
        BundleStatus  = "";
    }

    public void RefreshWeeklyTuneUpSchedule()
    {
        var nextRun = _scheduler.GetNextRun();
        NextWeeklyTuneUpText = nextRun?.ToString("ddd, MMM d 'at' h:mm tt") ?? "Not scheduled";
        OnPropertyChanged(nameof(IsWeeklyTuneUpScheduled));
    }

    public void SaveWeeklyTuneUpSchedule()
    {
        if (!Enum.TryParse<DayOfWeek>(SelectedWeeklyTuneUpDay, out var day))
            day = DayOfWeek.Sunday;
        if (!TimeSpan.TryParse(SelectedWeeklyTuneUpTime, out var time))
            time = TimeSpan.FromHours(10);

        _scheduler.Schedule(day, time);
        _settings.WeeklyTuneUpDay  = SelectedWeeklyTuneUpDay;
        _settings.WeeklyTuneUpTime = SelectedWeeklyTuneUpTime;
        _settingsSvc.Save(_settings);
        RefreshWeeklyTuneUpSchedule();
    }

    public void DisableWeeklyTuneUpSchedule()
    {
        _scheduler.Unschedule();
        RefreshWeeklyTuneUpSchedule();
    }

    public Task RunWeeklyTuneUpNowAsync()
    {
        var bundle = Bundles.FirstOrDefault(b => b.Id == "weekly-tune-up");
        return bundle is null ? Task.CompletedTask : RunBundleAsync(bundle);
    }

    public async Task RunRunbookAsync(RunbookDefinition runbook)
    {
        IsRunbookRunning = true;
        RunningRunbookTitle = runbook.Title;
        try
        {
            LastRunbookSummary = await _runbookExecution.ExecuteAsync(runbook, SymptomInput);
            InterruptedOperation = _statePersistence.Load();
            RefreshHistory();
            RefreshRecentlyRun();
            RefreshDashboardWorkspace();
            RefreshAutomationWorkspace();
            OnPropertyChanged(nameof(FixesTodayCount));
            OnPropertyChanged(nameof(FixesTodayText));
        }
        catch (Exception ex)
        {
            _logger.Error($"Runbook failed unexpectedly for {runbook.Id}", ex);
            throw;
        }
        finally
        {
            IsRunbookRunning = false;
            RunningRunbookTitle = "";
        }
    }

    public async Task CreateEvidenceBundleAsync()
    {
        try
        {
            var exportLevel = Enum.TryParse<EvidenceExportLevel>(_settings.SupportBundleExportLevel, ignoreCase: true, out var parsedLevel)
                ? parsedLevel
                : EvidenceExportLevel.Basic;
            if (exportLevel == EvidenceExportLevel.Technician && !CanUseTechnicianExports)
                exportLevel = EvidenceExportLevel.Basic;

            var options = new EvidenceExportOptions
            {
                Level = exportLevel,
                RedactIpAddress = exportLevel == EvidenceExportLevel.Basic,
                IncludeNotifications = true,
                IncludeTechnicalHistory = exportLevel == EvidenceExportLevel.Technician || (AdvancedModeEnabled && CanUseTechnicianExports)
            };

            LastEvidenceBundlePreviewText = await _evidenceBundles.BuildPreviewAsync(
                SymptomInput,
                new TriageResult
                {
                    Query = SymptomInput,
                    Candidates = TriageCandidates.ToList()
                },
                LastHealthCheckReport,
                LastRunbookSummary,
                options);

            LastEvidenceBundle = await _evidenceBundles.ExportAsync(
                SymptomInput,
                new TriageResult
                {
                    Query = SymptomInput,
                    Candidates = TriageCandidates.ToList()
                },
                LastHealthCheckReport,
                LastRunbookSummary,
                options);
        }
        catch (Exception ex)
        {
            _logger.Error("Evidence bundle export failed", ex);
            throw;
        }
    }

    // ── Wizard ────────────────────────────────────────────────────────────
    public void StartWizard(FixItem fix)
    {
        if (!CanAccessFix(fix))
        {
            fix.Status = FixStatus.Failed;
            fix.LastOutput = GetFixAvailabilityMessage(fix);
            return;
        }

        WizardFix     = fix;
        _wizardStep   = 0;
        GuidedRepairState = null;
        fix.Status = FixStatus.Running;
        WizardVisible = true;
        RefreshWizard();
    }

    public async Task WizardNextAsync()
    {
        if (WizardFix is null)
            return;

        var result = await _guidedRepairExecution.AdvanceAsync(WizardFix, _wizardStep, SymptomInput);
        GuidedRepairState = result;
        WizardFix.LastOutput = result.Output;
        InterruptedOperation = _statePersistence.Load();

        switch (result.Outcome)
        {
            case ExecutionOutcome.InProgress:
                _wizardStep = result.CurrentStepIndex;
                WizardFix.Status = FixStatus.Running;
                RefreshWizard();
                break;
            case ExecutionOutcome.Completed:
                WizardFix.Status = FixStatus.Success;
                WizardVisible = false;
                WizardFix = null;
                break;
            default:
                WizardFix.Status = FixStatus.Failed;
                RefreshWizard();
                break;
        }

        RefreshHistory();
        RefreshRecentlyRun();
        RefreshDashboardWorkspace();
        OnPropertyChanged(nameof(FixesTodayCount));
    }

    public void WizardCancel()
    {
        if (WizardFix is not null)
        {
            GuidedRepairState = _guidedRepairExecution.CancelAsync(WizardFix, _wizardStep, "Guided repair was cancelled by the user.", SymptomInput).GetAwaiter().GetResult();
            InterruptedOperation = _statePersistence.Load();
            RefreshHistory();
            RefreshRecentlyRun();
            RefreshDashboardWorkspace();
            RefreshAutomationWorkspace();
        }
        WizardVisible = false;
        WizardFix     = null;
    }

    // ── System Info ───────────────────────────────────────────────────────
    public async Task LoadSystemInfoAsync()
    {
        if (SnapshotLoading) return;
        SnapshotLoading = true;
        try
        {
            Snapshot = await _sysInfo.GetSnapshotAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("System snapshot failed", ex);
        }
        finally
        {
            SnapshotLoading = false;
            RefreshSupportCenters();
            RefreshDashboardWorkspace();
            RefreshAutomationWorkspace();
        }
    }

    public async Task LoadInstalledProgramsAsync()
    {
        if (InstalledProgramsLoading) return;

        InstalledProgramsLoading = true;
        try
        {
            _allInstalledPrograms.Clear();
            _allInstalledPrograms.AddRange(await _installedPrograms.GetInstalledAsync());
            RefreshInstalledPrograms();
        }
        catch (Exception ex)
        {
            _logger.Error("Installed program inventory failed", ex);
            _allInstalledPrograms.Clear();
            RefreshInstalledPrograms();
        }
        finally
        {
            InstalledProgramsLoading = false;
            RefreshSupportCenters();
        }
    }

    public async Task LoadStartupAppsAsync()
    {
        if (StartupAppsLoading) return;

        StartupAppsLoading = true;
        try
        {
            StartupApps.Clear();
            foreach (var item in await _startupApps.GetEntriesAsync())
                StartupApps.Add(item);
        }
        catch (Exception ex)
        {
            _logger.Error("Startup app inventory failed", ex);
            StartupApps.Clear();
        }
        finally
        {
            StartupAppsLoading = false;
            OnPropertyChanged(nameof(HasStartupApps));
            OnPropertyChanged(nameof(ShowStartupAppsEmptyState));
            OnPropertyChanged(nameof(StartupAppsSummaryText));
        }
    }

    public async Task LoadStorageInsightsAsync()
    {
        if (StorageInsightsLoading) return;

        StorageInsightsLoading = true;
        try
        {
            StorageInsights.Clear();
            foreach (var item in await _storageInsights.GetInsightsAsync())
                StorageInsights.Add(item);
        }
        catch (Exception ex)
        {
            _logger.Error("Storage insight scan failed", ex);
            StorageInsights.Clear();
        }
        finally
        {
            StorageInsightsLoading = false;
            OnPropertyChanged(nameof(HasStorageInsights));
            OnPropertyChanged(nameof(ShowStorageInsightsEmptyState));
            OnPropertyChanged(nameof(StorageInsightsSummaryText));
        }
    }

    private async Task LoadUpdateInfoAsync()
    {
        try
        {
            LastUpdateInfo = await _updates.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Update check failed", ex);
            LastUpdateInfo = null;
        }
        finally
        {
            RefreshDashboardWorkspace();
        }
    }

    private void RefreshInstalledPrograms()
    {
        InstalledPrograms.Clear();

        IEnumerable<InstalledProgram> items = _allInstalledPrograms;
        if (!string.IsNullOrWhiteSpace(_installedProgramSearchText))
        {
            var q = _installedProgramSearchText.Trim();
            items = items.Where(p =>
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Publisher.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Version.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in items)
            InstalledPrograms.Add(item);

        OnPropertyChanged(nameof(HasInstalledPrograms));
        OnPropertyChanged(nameof(ShowInstalledProgramsEmptyState));
        OnPropertyChanged(nameof(InstalledProgramsSummaryText));
    }

    public void RefreshAutomationWorkspace()
    {
        _automationCoordinator.EnsureRules(_settings);

        AutomationRules.Clear();
        ScheduledAutomationRules.Clear();
        WatcherAutomationRules.Clear();

        foreach (var rule in _settings.AutomationRules)
        {
            _automationCoordinator.PopulateRuntimeDetails(
                rule,
                _settings,
                Snapshot,
                LastHealthCheckReport,
                InterruptedOperation,
                HistoryEntries.ToList(),
                _automationHistory.Entries,
                HasActiveWork,
                DateTime.Now);

            AutomationRules.Add(rule);
            if (rule.IsWatcher)
                WatcherAutomationRules.Add(rule);
            else
                ScheduledAutomationRules.Add(rule);
        }

        AutomationHistoryEntries.Clear();
        foreach (var entry in _automationHistory.Entries)
            AutomationHistoryEntries.Add(entry);

        FilterAutomationHistory();
        RefreshRecentAutomationHistory();
        OnPropertyChanged(nameof(HasAutomationRules));
        OnPropertyChanged(nameof(HasScheduledAutomationRules));
        OnPropertyChanged(nameof(HasWatcherAutomationRules));
        OnPropertyChanged(nameof(HasAutomationHistoryEntries));
        OnPropertyChanged(nameof(AutomationPaused));
        OnPropertyChanged(nameof(AutomationPauseStatusText));
        OnPropertyChanged(nameof(ActiveAutomationCount));
        OnPropertyChanged(nameof(PausedAutomationCount));
        OnPropertyChanged(nameof(AutomationAttentionCount));
        OnPropertyChanged(nameof(NextAutomationRunText));
        OnPropertyChanged(nameof(LastAutomationResultText));
        OnPropertyChanged(nameof(AutomationOverviewText));
        OnPropertyChanged(nameof(ShellStatusText));
    }

    private void RefreshRecentAutomationHistory()
    {
        RecentAutomationHistoryEntries.Clear();
        foreach (var entry in AutomationHistoryEntries.Take(4))
            RecentAutomationHistoryEntries.Add(entry);

        OnPropertyChanged(nameof(HasRecentAutomationHistory));
    }

    private void FilterAutomationHistory()
    {
        FilteredAutomationHistoryEntries.Clear();

        IEnumerable<AutomationRunReceipt> entries = AutomationHistoryEntries;
        entries = AutomationHistoryFilter switch
        {
            "Attention Needed" => entries.Where(entry => entry.UserActionRequired),
            "Failures" => entries.Where(entry => entry.Outcome == AutomationRunOutcome.Failed),
            "Skipped" => entries.Where(entry => entry.Outcome is AutomationRunOutcome.Skipped or AutomationRunOutcome.Blocked),
            "Completed" => entries.Where(entry => entry.Outcome == AutomationRunOutcome.Completed),
            _ => entries
        };

        foreach (var entry in entries)
            FilteredAutomationHistoryEntries.Add(entry);

        OnPropertyChanged(nameof(HasFilteredAutomationHistoryEntries));
    }

    public async Task RunAutomationRuleAsync(AutomationRuleSettings rule, string triggerSource = "Manual", bool manualOverride = true)
    {
        if (rule is null)
            return;

        var receipt = await _automationCoordinator.RunAsync(
            rule.Id,
            triggerSource,
            manualOverride,
            HasActiveWork);

        RefreshAutomationWorkspace();

        if (rule.Kind is AutomationRuleKind.QuickHealthCheck or AutomationRuleKind.StartupQuickCheck)
            await RunFullHealthCheckAsync();
    }

    public void SaveAutomationRule(AutomationRuleSettings rule)
    {
        if (rule is null)
            return;

        SaveSettings();
        RefreshAutomationWorkspace();
    }

    public void PauseAutomationForHour()
    {
        _settings.AutomationPausedUntilUtc = DateTime.UtcNow.AddHours(1);
        SaveSettings();
        RefreshAutomationWorkspace();
    }

    public void PauseAutomationUntilTomorrow()
    {
        var tomorrow = DateTime.Today.AddDays(1).AddHours(8);
        _settings.AutomationPausedUntilUtc = tomorrow.ToUniversalTime();
        SaveSettings();
        RefreshAutomationWorkspace();
    }

    public void ResumeAutomation()
    {
        _settings.AutomationPausedUntilUtc = null;
        SaveSettings();
        RefreshAutomationWorkspace();
    }

    public void PauseAutomationRuleUntilTomorrow(AutomationRuleSettings rule)
    {
        rule.PausedUntilUtc = DateTime.Today.AddDays(1).AddHours(8).ToUniversalTime();
        SaveAutomationRule(rule);
    }

    public async Task RunStartupAutomationAsync()
    {
        var startupRule = ScheduledAutomationRules.FirstOrDefault(rule =>
            rule.Kind == AutomationRuleKind.StartupQuickCheck
            && rule.Enabled
            && rule.ScheduleKind == AutomationScheduleKind.Startup);
        if (startupRule is null || ShowPrivacyNotice)
            return;

        await Task.Delay(TimeSpan.FromMinutes(Math.Clamp(startupRule.StartupDelayMinutes, 1, 10)));
        await RunAutomationRuleAsync(startupRule, "Startup", manualOverride: false);
    }

    public async Task RunAutomationHeartbeatAsync()
    {
        if (HasActiveWork || AutomationPaused)
        {
            RefreshAutomationWorkspace();
            return;
        }

        foreach (var rule in WatcherAutomationRules.Where(rule => rule.Enabled))
        {
            var evaluation = _automationCoordinator.EvaluateRule(rule, _settings, Snapshot, HasActiveWork, _automationHistory.Entries, DateTime.Now);
            if (evaluation.CanRun)
                await _automationCoordinator.RunAsync(rule.Id, "Watcher", manualOverride: false, hasActiveWork: false);
        }

        RefreshAutomationWorkspace();
    }

    public string BuildAutomationRuleDetailText(AutomationRuleSettings rule)
    {
        var lines = new List<string>
        {
            rule.Title,
            "",
            rule.Summary,
            "",
            $"Enabled: {(rule.Enabled ? "Yes" : "No")}",
            $"Schedule: {rule.ScheduleKind}",
            $"Day: {rule.ScheduleDay}",
            $"Time: {rule.ScheduleTime}",
            $"Run only when idle: {(rule.RunOnlyWhenIdle ? $"Yes ({rule.MinimumIdleMinutes} min)" : "No")}",
            $"Skip on battery: {(rule.SkipOnBattery ? $"Yes (below {rule.MinimumBatteryPercent}%)" : "No")}",
            $"Skip on metered connection: {(rule.SkipOnMeteredConnection ? "Yes" : "No")}",
            $"Skip during quiet hours: {(rule.SkipDuringQuietHours ? $"{SelectedAutomationQuietHoursStart} to {SelectedAutomationQuietHoursEnd}" : "No")}",
            $"Notify only if issues are found: {(rule.NotifyOnlyIfIssuesFound ? "Yes" : "No")}",
            $"Current status: {rule.StatusText}",
            $"Last run: {rule.LastRunText}",
            $"Next run: {rule.NextRunText}",
            "",
            "Included tasks:"
        };

        foreach (var task in rule.IncludedTasks)
            lines.Add($"- {task}");

        return string.Join(Environment.NewLine, lines);
    }

    public string BuildAutomationReceiptDetailText(AutomationRunReceipt entry)
    {
        var lines = new List<string>
        {
            entry.RuleTitle,
            "",
            $"Trigger: {entry.TriggerSource}",
            $"Started: {entry.StartedAt:yyyy-MM-dd HH:mm:ss}",
            $"Finished: {entry.FinishedAt:yyyy-MM-dd HH:mm:ss}",
            $"Outcome: {entry.Outcome}",
            $"Summary: {entry.Summary}",
            $"Prechecks: {entry.PrecheckSummary}",
            $"What changed: {entry.ChangedSummary}",
            $"Verification: {entry.VerificationSummary}",
            $"Conditions: {entry.ConditionSummary}",
            $"Next step: {entry.NextStep}",
            $"Attention required: {(entry.UserActionRequired ? "Yes" : "No")}",
            "",
            "Tasks attempted:"
        };

        foreach (var task in entry.TasksAttempted)
            lines.Add($"- {task}");

        return string.Join(Environment.NewLine, lines);
    }

    // ── History ───────────────────────────────────────────────────────────
    private void RefreshHistory()
    {
        HistoryEntries.Clear();
        foreach (var e in _repairHistory.Entries) HistoryEntries.Add(e);
        OnPropertyChanged(nameof(FixesTodayCount));
        OnPropertyChanged(nameof(HasLastUsefulAction));
        OnPropertyChanged(nameof(LastUsefulActionLabel));
        FilterHistory();
        RefreshSupportCenters();
        RefreshDashboardWorkspace();
        RefreshAutomationWorkspace();
        RefreshCommandPalette();
    }

    public void ClearHistory() { _repairHistory.Clear(); _log.Clear(); RefreshHistory(); RefreshRecentlyRun(); }

    public void ClearAutomationHistory()
    {
        _automationHistory.Clear();
        RefreshAutomationWorkspace();
        RefreshCommandPalette();
    }

    private void FilterHistory()
    {
        FilteredHistoryEntries.Clear();

        IEnumerable<RepairHistoryEntry> items = HistoryEntries;
        if (!string.IsNullOrWhiteSpace(_historySearchText))
        {
            var q = _historySearchText.Trim();
            items = items.Where(e =>
                e.FixTitle.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.CategoryName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.Notes.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.VerificationSummary.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in items)
            FilteredHistoryEntries.Add(item);

        OnPropertyChanged(nameof(HasHistoryEntries));
        OnPropertyChanged(nameof(HasFilteredHistoryEntries));
        OnPropertyChanged(nameof(HasHistorySearchText));
        OnPropertyChanged(nameof(HistorySummaryText));
        OnPropertyChanged(nameof(HistoryEmptyStateTitle));
        OnPropertyChanged(nameof(HistoryEmptyStateText));
    }

    public Task RerunHistoryEntryAsync(RepairHistoryEntry entry)
        => !string.IsNullOrWhiteSpace(entry.RunbookId)
            ? RunRunbookByIdAsync(entry.RunbookId)
            : string.IsNullOrWhiteSpace(entry.FixId)
                ? Task.CompletedTask
                : RunFixByIdAsync(entry.FixId);

    public Task RunLastUsefulActionAsync()
    {
        var latest = HistoryEntries.FirstOrDefault();
        return latest is null ? Task.CompletedTask : RerunHistoryEntryAsync(latest);
    }

    public Task ResumeInterruptedRepairAsync()
    {
        if (!CanResumeInterruptedRepair || InterruptedOperation is null)
            return Task.CompletedTask;

        var fix = _catalog.GetById(InterruptedOperation.OperationTargetId);
        if (fix is null || !CanAccessFix(fix))
            return Task.CompletedTask;

        StartWizard(fix);
        GuidedRepairState = _guidedRepairExecution.BuildResumeState(fix, InterruptedOperation);
        if (GuidedRepairState is not null)
            _wizardStep = GuidedRepairState.CurrentStepIndex;
        RefreshWizard();
        RefreshAutomationWorkspace();
        return Task.CompletedTask;
    }

    public string BuildRepairDetailText(FixItem fix)
    {
        var repair = _repairCatalog.GetRepair(fix.Id);
        if (repair is null)
            return $"{fix.Title}\n\nNo structured repair contract was found for this item.";

        var lines = new List<string>
        {
            repair.Title,
            "",
            $"Problem summary: {repair.UserProblemSummary}",
            $"Why FixFox suggests it: {repair.WhySuggested}",
            $"Category: {repair.MasterCategoryId}",
            $"Risk level: {repair.RiskLevel}",
            $"Tier: {repair.Tier}",
            $"Admin required: {(repair.RequiresAdmin ? "Yes" : "No")}",
            $"What will happen: {repair.WhatWillHappen}",
            $"Verification strategy: {repair.VerificationStrategyId}",
            ""
        };

        AppendSection(lines, "Preconditions", repair.Preconditions);
        AppendSection(lines, "Environment requirements", repair.EnvironmentRequirements);
        AppendSection(lines, "Quick actions", repair.QuickFixActions);
        AppendSection(lines, "Verification checks", repair.VerificationChecks);
        AppendSection(lines, "Related Windows tools", repair.RelatedWindowsTools);
        AppendSection(lines, "Related Windows settings", repair.RelatedWindowsSettings);
        AppendSection(lines, "Related workflows", repair.RelatedRunbooks);
        AppendSection(lines, "Evidence tags", repair.EvidenceExportTags);

        lines.Add($"Next step on success: {repair.SuggestedNextStepOnSuccess}");
        lines.Add($"Next step on failure: {repair.SuggestedNextStepOnFailure}");
        lines.Add($"Escalation hint: {repair.EscalationHint}");
        lines.Add($"Rollback hint: {repair.RollbackHint}");
        lines.Add($"Suppression scope: {repair.SuppressionScopeHint}");

        return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    public IReadOnlyList<RunbookDefinition> GetRelatedRunbooks(FixItem fix)
    {
        var repair = _repairCatalog.GetRepair(fix.Id);
        if (repair is null || repair.RelatedRunbooks.Count == 0)
            return [];

        return Runbooks
            .Where(runbook => repair.RelatedRunbooks.Contains(runbook.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public string BuildRunbookDetailText(RunbookDefinition runbook)
    {
        var lines = new List<string>
        {
            runbook.Title,
            "",
            runbook.Description,
            "",
            $"Estimated time: {runbook.EstTime}",
            $"Admin required: {(runbook.RequiresAdmin ? "Yes" : "No")}",
            $"Restore point aware: {(runbook.SupportsRestorePoint ? "Yes" : "No")}",
            $"Rollback aware: {(runbook.SupportsRollback ? "Yes" : "No")}",
            $"Trigger hint: {runbook.TriggerHint}",
            "",
            "Workflow steps:"
        };

        foreach (var step in runbook.Steps)
            lines.Add($"- {step.Title}: {step.Description}");

        return string.Join(Environment.NewLine, lines);
    }

    public string BuildMaintenanceProfileDetailText(MaintenanceProfileDefinition profile)
    {
        var lines = new List<string>
        {
            profile.Title,
            "",
            profile.Summary,
            "",
            $"Safety: {profile.SafetyNotes}",
            $"Verification: {profile.VerificationNotes}",
            $"Supports scheduling: {(profile.SupportsScheduling ? "Yes" : "No")}",
            $"Prefer idle when scheduled: {(profile.PreferIdleWhenScheduled ? "Yes" : "No")}",
            $"Avoid when on battery: {(profile.AvoidWhenOnBattery ? "Yes" : "No")}",
            "",
            "Included tasks:"
        };

        foreach (var task in profile.IncludedTasks)
            lines.Add($"- {task}");

        return string.Join(Environment.NewLine, lines);
    }

    public string BuildReceiptDetailText(RepairHistoryEntry entry)
    {
        var lines = new List<string>
        {
            entry.FixTitle,
            "",
            $"When: {entry.Timestamp:yyyy-MM-dd HH:mm:ss}",
            $"Outcome: {entry.Outcome}",
            $"Success: {(entry.Success ? "Yes" : "No")}",
            $"Category: {entry.CategoryName}",
            $"Verification: {entry.VerificationSummary}",
            $"What changed: {entry.ChangedSummary}",
            $"Post-state: {entry.PostStateSummary}",
            $"Next step: {entry.NextStep}",
            $"Rollback: {entry.RollbackSummary}",
            $"Trigger: {entry.TriggerSource}",
            $"Requires admin: {(entry.RequiresAdmin ? "Yes" : "No")}",
            $"Reboot recommended: {(entry.RebootRecommended ? "Yes" : "No")}",
        };

        if (!string.IsNullOrWhiteSpace(entry.FailedStepTitle))
            lines.Add($"Failed step: {entry.FailedStepTitle}");

        return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    public string BuildSupportCenterDetailText(SupportCenterDefinition center)
    {
        var lines = new List<string>
        {
            center.Title,
            "",
            center.Summary,
            "",
            $"Current status: {center.StatusText}",
            "",
            "Highlights:"
        };

        foreach (var item in center.Highlights)
            lines.Add($"- {item}");

        lines.Add("");
        lines.Add($"Primary action: {center.PrimaryAction.Label} - {center.PrimaryAction.Description}");
        lines.Add($"Secondary action: {center.SecondaryAction.Label} - {center.SecondaryAction.Description}");
        return string.Join(Environment.NewLine, lines);
    }

    public string BuildInstalledProgramDetailText(InstalledProgram program)
    {
        var lines = new List<string>
        {
            program.Name,
            "",
            $"Publisher: {program.Publisher}",
            $"Version: {program.Version}",
            $"Installed: {program.InstallDateLabel}",
            $"Size: {program.SizeLabel}",
            $"Install location: {(program.HasInstallLocation ? program.InstallLocation : "Not published by the app installer")}",
            $"Uninstall available: {(!string.IsNullOrWhiteSpace(program.UninstallCommand) || !string.IsNullOrWhiteSpace(program.QuietUninstallCommand) ? "Yes" : "No")}"
        };

        return string.Join(Environment.NewLine, lines.Where(line => !line.EndsWith(": ")));
    }

    public string BuildStartupAppDetailText(StartupAppEntry entry)
    {
        var lines = new List<string>
        {
            entry.Name,
            "",
            $"Source: {entry.Source}",
            $"Command: {entry.Command}",
            $"Launch target: {(entry.HasLaunchTarget ? entry.LaunchTarget : "Not parsed automatically")}",
            $"Review candidate: {(entry.RecommendedDisableCandidate ? "Yes" : "No")}",
            $"Why it matters: {entry.RecommendationReason}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    public string BuildStorageInsightDetailText(StorageInsight insight)
    {
        var lines = new List<string>
        {
            insight.DisplayName,
            "",
            $"Location: {insight.LocationLabel}",
            $"Size: {insight.SizeLabel}",
            $"Path: {insight.FullPath}",
            $"Safe to remove guidance: {insight.SafeToRemoveSummary}",
            $"Caution: {insight.Caution}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    public Task RunMaintenanceProfileAsync(MaintenanceProfileDefinition profile) =>
        profile is null ? Task.CompletedTask : RunSupportActionAsync(profile.LaunchAction);

    public async Task RunSupportActionAsync(SupportAction action)
    {
        if (action is null || string.IsNullOrWhiteSpace(action.TargetId))
            return;

        switch (action.Kind)
        {
            case SupportActionKind.Fix:
                await RunFixByIdAsync(action.TargetId);
                break;
            case SupportActionKind.Runbook:
                await RunRunbookByIdAsync(action.TargetId);
                break;
            case SupportActionKind.Toolbox:
                LaunchToolboxAction(action.TargetId);
                break;
            case SupportActionKind.Uri:
                if (TryHandleInternalRoute(action.TargetId))
                    break;
                Process.Start(new ProcessStartInfo(action.TargetId) { UseShellExecute = true });
                break;
        }
    }

    private bool TryHandleInternalRoute(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return false;

        return target switch
        {
            "fixfox://page/home" => SetPage(Page.Dashboard),
            "fixfox://page/automation" => SetPage(Page.Bundles),
            "fixfox://page/device-health" => SetPage(Page.SystemInfo),
            "fixfox://page/history" => SetPage(Page.History),
            "fixfox://page/support" => SetPage(Page.Handoff),
            _ => false
        };
    }

    private bool SetPage(Page page)
    {
        CurrentPage = page;
        return true;
    }

    public async Task ExecuteCommandPaletteItemAsync(CommandPaletteItem? item)
    {
        if (item is null)
            return;

        switch (item.Kind)
        {
            case CommandPaletteItemKind.Page when item.TargetPage.HasValue:
                CurrentPage = item.TargetPage.Value;
                break;
            case CommandPaletteItemKind.Fix:
                await RunFixByIdAsync(item.TargetId);
                break;
            case CommandPaletteItemKind.Runbook:
                await RunRunbookByIdAsync(item.TargetId);
                break;
            case CommandPaletteItemKind.MaintenanceProfile:
            {
                var profile = MaintenanceProfiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, item.TargetId, StringComparison.OrdinalIgnoreCase));
                if (profile is not null)
                    await RunMaintenanceProfileAsync(profile);
                break;
            }
            case CommandPaletteItemKind.SupportCenter:
            {
                var center = SupportCenters.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, item.TargetId, StringComparison.OrdinalIgnoreCase));
                if (center is not null)
                    await RunSupportActionAsync(center.PrimaryAction);
                break;
            }
            case CommandPaletteItemKind.Toolbox:
                LaunchToolboxAction(item.TargetId);
                break;
            case CommandPaletteItemKind.Action:
                await ExecuteAutomationActionAsync(item.TargetId);
                break;
        }
    }

    private async Task ExecuteAutomationActionAsync(string actionId)
    {
        switch (actionId)
        {
            case "run-automation:quick-health-check":
                if (AutomationRules.FirstOrDefault(rule => rule.Id == "quick-health-check") is { } quickHealth)
                    await RunAutomationRuleAsync(quickHealth);
                break;
            case "run-automation:safe-maintenance":
                if (AutomationRules.FirstOrDefault(rule => rule.Id == "safe-maintenance") is { } safeMaintenance)
                    await RunAutomationRuleAsync(safeMaintenance);
                break;
            case "automation:pause-hour":
                PauseAutomationForHour();
                break;
            case "automation:resume":
                ResumeAutomation();
                break;
            case "automation:resume-interrupted":
                await ResumeInterruptedRepairAsync();
                break;
        }
    }

    public async Task RunRunbookByIdAsync(string runbookId)
    {
        var runbook = Runbooks.FirstOrDefault(item => string.Equals(item.Id, runbookId, StringComparison.OrdinalIgnoreCase));
        if (runbook is not null)
            await RunRunbookAsync(runbook);
    }

    public async Task RunRecommendedMaintenanceAsync()
    {
        var runbook = Runbooks.FirstOrDefault(item => string.Equals(item.Id, "safe-maintenance-runbook", StringComparison.OrdinalIgnoreCase))
            ?? Runbooks.FirstOrDefault(item => string.Equals(item.Id, "routine-maintenance-runbook", StringComparison.OrdinalIgnoreCase))
            ?? Runbooks.FirstOrDefault(item => string.Equals(item.Id, "slow-pc-runbook", StringComparison.OrdinalIgnoreCase));

        if (runbook is not null)
        {
            await RunRunbookAsync(runbook);
            return;
        }

        await RunFullHealthCheckAsync();
    }

    // ── Notifications ─────────────────────────────────────────────────────
    public void MarkNotificationsRead()
    {
        _notifs.MarkAllRead();
        RefreshNotifications();
    }

    public void ClearNotifications()
    {
        _notifs.Clear();
        RefreshNotifications();
    }

    public void MarkNotificationRead(AppNotification notification)
    {
        _notifs.MarkRead(notification.Id);
        RefreshNotifications();
    }

    public void DismissNotification(AppNotification notification)
    {
        _notifs.Remove(notification.Id);
        RefreshNotifications();
    }

    public void ClearInterruptedOperation()
    {
        _statePersistence.Clear();
        InterruptedOperation = null;
        RefreshDashboardWorkspace();
        RefreshAutomationWorkspace();
    }

    private void RefreshNotifications()
    {
        OnPropertyChanged(nameof(UnreadNotifCount));
        OnPropertyChanged(nameof(NotificationEntries));
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(NotificationSummaryText));
        OnPropertyChanged(nameof(ShellStatusText));
    }

    // ── Settings ──────────────────────────────────────────────────────────
    public void SaveSettings()
    {
        _deployment.ApplyPolicy(_settings);
        _automationCoordinator.EnsureRules(_settings);
        _settingsSvc.Save(_settings);
        OnPropertyChanged(nameof(HasSuppressedItems));
        OnPropertyChanged(nameof(CurrentProfileStatusText));
        OnPropertyChanged(nameof(SelectedLandingPage));
        OnPropertyChanged(nameof(SelectedNotificationMode));
        OnPropertyChanged(nameof(SelectedSupportBundleExportLevel));
        OnPropertyChanged(nameof(SelectedAutomationQuietHoursStart));
        OnPropertyChanged(nameof(SelectedAutomationQuietHoursEnd));
        OnPropertyChanged(nameof(BehaviorProfileOptions));
        OnPropertyChanged(nameof(SupportBundleExportLevelOptions));
        OnPropertyChanged(nameof(SettingsLoadStatus));
        OnPropertyChanged(nameof(SettingsLoadStatusText));
        OnPropertyChanged(nameof(HasSettingsLoadNotice));
        OnPropertyChanged(nameof(ManagedPolicySummaryText));
        OnPropertyChanged(nameof(DeploymentSummaryText));
        OnPropertyChanged(nameof(SupportRoutingSummaryText));
        RefreshCapabilityScopedContent();
        SyncAutomationSchedules();
        RefreshAutomationWorkspace();
        RefreshActiveFixes();
        RefreshCommandPalette();
    }

    public void ApplyTheme(string theme)
    {
        _settings.Theme = theme;
        App.SwitchTheme(theme);
        _settingsSvc.Save(_settings);
    }

    private void RefreshDashboardWorkspace()
    {
        DashboardAlerts.Clear();
        foreach (var alert in _dashboardWorkspace.BuildAlerts(Snapshot, LastHealthCheckReport, LastUpdateInfo, InterruptedOperation, HistoryEntries.ToList())
                     .Where(alert => !_settings.SnoozedAlertKeys.Contains(alert.Key, StringComparer.OrdinalIgnoreCase)))
            DashboardAlerts.Add(alert);

        SuggestedRunbooks.Clear();
        foreach (var runbook in _dashboardWorkspace.RecommendRunbooks(Snapshot, ScanResults.ToList(), HistoryEntries.ToList(), Runbooks.ToList()))
            SuggestedRunbooks.Add(runbook);

        OnPropertyChanged(nameof(HasDashboardAlerts));
        OnPropertyChanged(nameof(HasSuggestedRunbooks));
        OnPropertyChanged(nameof(HasSuppressedItems));
        OnPropertyChanged(nameof(AutomationAttentionCount));
    }

    private void RefreshSupportCenters()
    {
        SupportCenters.Clear();
        foreach (var center in _supportCenterService.BuildCenters(Snapshot, _allInstalledPrograms, HistoryEntries.ToList()))
            SupportCenters.Add(center);

        OnPropertyChanged(nameof(HasSupportCenters));
        RefreshCommandPalette();
    }

    private void RefreshProactiveRecommendations()
    {
        ProactiveRecommendations.Clear();
        if (LastHealthCheckReport is null)
        {
            OnPropertyChanged(nameof(HasProactiveRecommendations));
            OnPropertyChanged(nameof(HasSuppressedItems));
            return;
        }

        foreach (var recommendation in LastHealthCheckReport.Recommendations
                     .Where(item => !_settings.IgnoredRecommendationKeys.Contains(item.Key, StringComparer.OrdinalIgnoreCase)))
            ProactiveRecommendations.Add(recommendation);

        OnPropertyChanged(nameof(ProactiveRecommendations));
        OnPropertyChanged(nameof(HasProactiveRecommendations));
        OnPropertyChanged(nameof(HasSuppressedItems));
    }

    private bool ShouldSurfaceNotification(ScanResult result) =>
        _settings.NotificationMode switch
        {
            "Quiet" => false,
            "Important Only" => result.Severity == ScanSeverity.Critical,
            _ => result.Severity != ScanSeverity.Good
        };

    // ── INotifyPropertyChanged ────────────────────────────────────────────
    private void LaunchToolboxAction(string title)
    {
        var entry = ToolboxGroups
            .SelectMany(group => group.Entries)
            .FirstOrDefault(item => string.Equals(item.Title, title, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            throw new InvalidOperationException($"No Windows tool route is registered for \"{title}\".");

        entry.LaunchState = ToolLaunchState.Running;
        entry.LaunchSummary = "Opening Windows tool...";

        try
        {
            _toolbox.Launch(entry);
            entry.LaunchState = ToolLaunchState.Success;
            entry.LastLaunchedAt = DateTime.Now;
            entry.LaunchSummary = "Opened successfully.";
        }
        catch (Exception ex)
        {
            entry.LaunchState = ToolLaunchState.Failed;
            entry.LaunchSummary = ex.Message;
            throw;
        }
    }

    public Page GetStartupLandingPage() => ParseLandingPage(_settings.DefaultLandingPage);

    private static Page ParseLandingPage(string? value)
        => Enum.TryParse<Page>(value, ignoreCase: true, out var page) ? page : Page.Dashboard;

    private static void AppendSection(List<string> lines, string title, IReadOnlyCollection<string> items)
    {
        if (items.Count == 0)
            return;

        lines.Add(title + ":");
        foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item)))
            lines.Add($"- {item}");
        lines.Add("");
    }

    private static string PageToLabel(Page page) => page switch
    {
        Page.Dashboard => "Home",
        Page.SymptomChecker => "Guided Diagnosis",
        Page.Fixes => "Repair Library",
        Page.Bundles => "Automation",
        Page.SystemInfo => "Device Health",
        Page.Toolbox => "Windows Tools",
        Page.Handoff => "Support Package",
        Page.History => "Activity",
        Page.Settings => "Settings",
        _ => "Home"
    };

    private static Page LabelToPage(string? value) => value switch
    {
        "Guided Diagnosis" => Page.SymptomChecker,
        "Repair Library" => Page.Fixes,
        "Automation" => Page.Bundles,
        "Device Health" => Page.SystemInfo,
        "Windows Tools" => Page.Toolbox,
        "Support Package" => Page.Handoff,
        "Activity" => Page.History,
        "Settings" => Page.Settings,
        _ => Page.Dashboard
    };

    private static string FormatEditionLabel(AppEdition edition) => edition switch
    {
        AppEdition.ManagedServiceProvider => "MSP",
        AppEdition.Pro => "Pro",
        _ => "Basic"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
