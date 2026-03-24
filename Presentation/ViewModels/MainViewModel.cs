using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    private readonly IElevationService    _elevation;
    private readonly ITriageEngine        _triage;
    private readonly IRunbookCatalogService _runbookCatalog;
    private readonly IRunbookExecutionService _runbookExecution;
    private readonly IRepairExecutionService _repairExecution;
    private readonly IHealthCheckService  _healthCheck;
    private readonly IEvidenceBundleService _evidenceBundles;
    private readonly IKnowledgeBaseService _knowledgeBase;
    private readonly IBrandingConfigurationService _branding;
    private readonly IEditionCapabilityService _edition;
    private readonly IAppUpdateService _updates;
    private readonly IStatePersistenceService _statePersistence;
    private readonly InstalledProgramsService _installedPrograms;
    private readonly SchedulerService        _scheduler;

    public INotificationService Notifications => _notifs;

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
            RefreshBreadcrumb();
            foreach (var n in new[]
            {
                nameof(ShowDashboard), nameof(ShowFixes), nameof(ShowBundles),
                nameof(ShowSystemInfo), nameof(ShowSymptomChecker),
                nameof(ShowHistory), nameof(ShowHandoff), nameof(ShowSettings)
            }) OnPropertyChanged(n);
        }
    }

    public bool ShowDashboard       => CurrentPage == Page.Dashboard;
    public bool ShowFixes           => CurrentPage == Page.Fixes;
    public bool ShowBundles         => CurrentPage == Page.Bundles;
    public bool ShowSystemInfo      => CurrentPage == Page.SystemInfo;
    public bool ShowSymptomChecker  => CurrentPage == Page.SymptomChecker;
    public bool ShowHistory         => CurrentPage == Page.History;
    public bool ShowHandoff         => CurrentPage == Page.Handoff;
    public bool ShowSettings        => CurrentPage == Page.Settings;

    public string BreadcrumbText => CurrentPage switch
    {
        Page.Dashboard       => "Dashboard",
        Page.Fixes           => _selectedCategory is null
                                    ? "Fix Center"
                                    : $"Fix Center  \u203A  {_selectedCategory.Title}",
        Page.Bundles         => "Runbooks",
        Page.SystemInfo      => "Device Health",
        Page.SymptomChecker  => "Fix an Issue",
        Page.History         => "Fix History",
        Page.Handoff         => "Help Desk Handoff",
        Page.Settings        => "Settings",
        _                    => ""
    };

    /// <summary>Status bar label — same text as breadcrumb.</summary>
    public string CurrentPageLabel => BreadcrumbText;

    /// <summary>Breadcrumb bar items: always starts with "FixFox", adds page name if not Dashboard.</summary>
    public ObservableCollection<string> BreadcrumbItems { get; } = ["FixFox"];

    private void RefreshBreadcrumb()
    {
        BreadcrumbItems.Clear();
        BreadcrumbItems.Add("FixFox");
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

        foreach (var f in source) ActiveFixes.Add(f);
        OnPropertyChanged(nameof(ActiveFixCount));
    }

    // ── Dashboard / Quick Scan ────────────────────────────────────────────
    public ObservableCollection<ScanResult> ScanResults { get; } = [];

    private bool   _scanRunning;
    private string _scanStatusText = "Click 'Run Quick Scan' to check your PC's health.";

    public bool   ScanRunning    { get => _scanRunning;    set { _scanRunning    = value; OnPropertyChanged(); } }
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

    private void RefreshRecentlyRun()
    {
        RecentlyRunFixes.Clear();
        var seen = new HashSet<string>();
        foreach (var e in _log.Entries)
        {
            if (seen.Count >= 3) break;
            if (!seen.Add(e.FixId)) continue;
            var fix = _catalog.GetById(e.FixId);
            if (fix is not null) RecentlyRunFixes.Add(fix);
        }
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

    public FixBundle? RunningBundle   { get => _runningBundle;   set { _runningBundle  = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsBundleRunning)); } }
    public bool       IsBundleRunning => _runningBundle is not null;
    public string     BundleStatus    { get => _bundleStatus;    set { _bundleStatus   = value; OnPropertyChanged(); } }
    public int        BundleProgress  { get => _bundleProgress;  set { _bundleProgress = value; OnPropertyChanged(); } }
    public int        BundleTotal     { get => _bundleTotal;     set { _bundleTotal    = value; OnPropertyChanged(); } }
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

    private HealthCheckReport? _lastHealthCheckReport;
    public HealthCheckReport? LastHealthCheckReport
    {
        get => _lastHealthCheckReport;
        set { _lastHealthCheckReport = value; OnPropertyChanged(); OnPropertyChanged(nameof(HealthCheckSummaryText)); }
    }

    public string HealthCheckSummaryText =>
        LastHealthCheckReport is null
            ? "Run a full health check to score the device and generate proactive recommendations."
            : $"{LastHealthCheckReport.OverallScore}/100 - {LastHealthCheckReport.Summary}";

    private FixItem? _wizardFix;
    private int      _wizardStep;
    private bool     _wizardVisible;

    public bool      WizardVisible  { get => _wizardVisible; set { _wizardVisible = value; OnPropertyChanged(); } }
    public FixItem?  WizardFix      { get => _wizardFix;     set { _wizardFix = value; OnPropertyChanged(); RefreshWizard(); } }
    public FixStep?  CurrentStep    => WizardFix?.Steps.Count > _wizardStep ? WizardFix.Steps[_wizardStep] : null;
    public string    WizardStepLabel => WizardFix is null ? "" : $"Step {_wizardStep + 1} of {WizardFix.Steps.Count}";
    public bool      WizardHasScript => !string.IsNullOrWhiteSpace(CurrentStep?.Script);
    public bool      WizardIsLast   => WizardFix is not null && _wizardStep == WizardFix.Steps.Count - 1;
    public string    WizardNextLabel => WizardIsLast ? "Done" : "Next step";

    private void RefreshWizard()
    {
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(WizardStepLabel));
        OnPropertyChanged(nameof(WizardHasScript));
        OnPropertyChanged(nameof(WizardIsLast));
        OnPropertyChanged(nameof(WizardNextLabel));
    }

    // ── System Info ───────────────────────────────────────────────────────
    private SystemSnapshot? _snapshot;
    private bool            _snapshotLoading;
    public SystemSnapshot?  Snapshot        { get => _snapshot;        set { _snapshot        = value; OnPropertyChanged(); } }
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

    // ── History ───────────────────────────────────────────────────────────
    public ObservableCollection<FixLogEntry> HistoryEntries { get; } = [];
    public ObservableCollection<FixLogEntry> FilteredHistoryEntries { get; } = [];
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
    public string HistorySummaryText =>
        HistoryEntries.Count == 0
            ? "No fixes have been run yet."
            : $"{FilteredHistoryEntries.Count} of {HistoryEntries.Count} history entries";

    // ── Settings ──────────────────────────────────────────────────────────
    private AppSettings _settings = new();
    public AppSettings  Settings  { get => _settings; set { _settings = value; OnPropertyChanged(); } }
    public string AppVersionSummaryText { get; } = "Free and open-source desktop support toolkit";
    public string PrivilegeModeText => _elevation.IsElevated
        ? "Running as administrator. Scheduled bundles can execute elevated fixes automatically."
        : "Running as a standard user. Admin fixes will ask for permission when needed.";
    public string DataFolderPath => SharedConstants.AppDataDir;
    public string AppLogPath => SharedConstants.AppLogFile;
    public string VerifyLogPath => SharedConstants.VerifyLogFile;

    public int  UnreadNotifCount => _notifs.UnreadCount;
    public IReadOnlyList<AppNotification> NotificationEntries => _notifs.All.ToList();
    public bool HasNotifications => _notifs.All.Count > 0;
    public string NotificationSummaryText =>
        _notifs.All.Count == 0
            ? "No notifications yet."
            : $"{UnreadNotifCount} unread of {_notifs.All.Count} total";

    // ── Command Palette ───────────────────────────────────────────────────
    public BrandingConfiguration Branding => _branding.Current;
    public EditionCapabilitySnapshot EditionSnapshot => _edition.GetSnapshot();
    public IReadOnlyList<KnowledgeBaseEntry> KnowledgeBaseEntries => _knowledgeBase.Entries;

    private InterruptedOperationState? _interruptedOperation;
    public InterruptedOperationState? InterruptedOperation
    {
        get => _interruptedOperation;
        set { _interruptedOperation = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasInterruptedOperation)); }
    }

    public bool HasInterruptedOperation => InterruptedOperation is not null;

    private EvidenceBundleManifest? _lastEvidenceBundle;
    public EvidenceBundleManifest? LastEvidenceBundle
    {
        get => _lastEvidenceBundle;
        set { _lastEvidenceBundle = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEvidenceBundle)); }
    }

    public bool HasEvidenceBundle => LastEvidenceBundle is not null;

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
        set { _lastUpdateInfo = value; OnPropertyChanged(); }
    }

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

    public ObservableCollection<FixItem> CommandPaletteResults { get; } = [];

    public void RefreshCommandPalette()
    {
        CommandPaletteResults.Clear();
        IEnumerable<FixItem> results;
        if (string.IsNullOrWhiteSpace(_commandPaletteQuery))
        {
            results = PinnedFixes
                .Concat(FavoriteFixes)
                .Concat(RecentlyRunFixes)
                .GroupBy(f => f.Id)
                .Select(g => g.First());
        }
        else
        {
            results = _catalog.Search(_commandPaletteQuery);
        }

        foreach (var r in results.Take(8)) CommandPaletteResults.Add(r);
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

    public int    FixesTodayCount => _log.Entries.Count(e => e.Timestamp.Date == DateTime.Today);
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
        _settingsSvc.Save(_settings);
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

    // ── Constructor ───────────────────────────────────────────────────────
    public MainViewModel(
        IScriptService       scripts,
        IFixCatalogService   catalog,
        IQuickScanService    scanner,
        ISystemInfoService   sysInfo,
        ILogService          log,
        INotificationService notifs,
        ISettingsService     settingsSvc,
        IElevationService    elevation,
        ITriageEngine        triage,
        IRunbookCatalogService runbookCatalog,
        IRunbookExecutionService runbookExecution,
        IRepairExecutionService repairExecution,
        IHealthCheckService  healthCheck,
        IEvidenceBundleService evidenceBundles,
        IKnowledgeBaseService knowledgeBase,
        IBrandingConfigurationService branding,
        IEditionCapabilityService edition,
        IAppUpdateService    updates,
        IStatePersistenceService statePersistence,
        InstalledProgramsService installedPrograms,
        SchedulerService        scheduler)
    {
        _scripts     = scripts;
        _catalog     = catalog;
        _scanner     = scanner;
        _sysInfo     = sysInfo;
        _log         = log;
        _notifs      = notifs;
        _settingsSvc = settingsSvc;
        _elevation   = elevation;
        _triage      = triage;
        _runbookCatalog = runbookCatalog;
        _runbookExecution = runbookExecution;
        _repairExecution = repairExecution;
        _healthCheck = healthCheck;
        _evidenceBundles = evidenceBundles;
        _knowledgeBase = knowledgeBase;
        _branding = branding;
        _edition = edition;
        _updates = updates;
        _statePersistence = statePersistence;
        _installedPrograms = installedPrograms;
        _scheduler         = scheduler;

        _settings = settingsSvc.Load();
        _isSidebarCollapsed = _settings.SidebarCollapsed;

        foreach (var c in _catalog.Categories) Categories.Add(c);
        foreach (var b in _catalog.Bundles)    Bundles.Add(b);
        foreach (var runbook in _runbookCatalog.Runbooks) Runbooks.Add(runbook);

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

        RefreshHistory();
        RefreshRecentlyRun();
        InterruptedOperation = _statePersistence.Load();

        // Show privacy notice on first launch
        ShowPrivacyNotice = !_settings.PrivacyNoticeDismissed;

        // Clock
        _clock = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clock.Tick += (_, _) => CurrentTimeText = DateTime.Now.ToString("h:mm:ss tt");
        CurrentTimeText = DateTime.Now.ToString("h:mm:ss tt");
        _clock.Start();

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
        try { results = await _scanner.ScanAsync(); }
        catch { results = []; }

        foreach (var r in results) ScanResults.Add(r);

        if (_settings.ShowNotifications)
            foreach (var r in results.Where(r => r.Severity != ScanSeverity.Good))
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
    }

    public async Task RunFullHealthCheckAsync()
    {
        LastHealthCheckReport = await _healthCheck.RunFullAsync();
        ProactiveRecommendations.Clear();
        foreach (var recommendation in LastHealthCheckReport.Recommendations)
            ProactiveRecommendations.Add(recommendation);
        OnPropertyChanged(nameof(ProactiveRecommendations));
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
            fix.Status     = FixStatus.Failed;
            fix.LastOutput = $"Unexpected error: {ex.Message}";
        }

        if (_settings.LogFixHistory)
            _log.Record(_catalog.GetCategoryTitle(fix), fix);

        InterruptedOperation = _statePersistence.Load();
        RefreshHistory();
        RefreshRecentlyRun();
        OnPropertyChanged(nameof(FixesTodayCount));
        OnPropertyChanged(nameof(FixesTodayText));
        OnPropertyChanged(nameof(ActiveFixes));
        OnPropertyChanged(nameof(SymptomResults));
    }

    public async Task RunFixByIdAsync(string fixId)
    {
        var fix = _catalog.GetById(fixId);
        if (fix is null) return;
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
                await _scripts.RunFixAsync(fix);
                if (fix.Status == FixStatus.Failed) failCount++;
            }
            catch { failCount++; }

            if (_settings.LogFixHistory)
                _log.Record(_catalog.GetCategoryTitle(fix), fix);

            BundleProgress++;
        }

        RefreshHistory();
        RefreshRecentlyRun();
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
        LastRunbookSummary = await _runbookExecution.ExecuteAsync(runbook, SymptomInput);
        InterruptedOperation = _statePersistence.Load();
        RefreshHistory();
        RefreshRecentlyRun();
        OnPropertyChanged(nameof(FixesTodayCount));
        OnPropertyChanged(nameof(FixesTodayText));
    }

    public async Task CreateEvidenceBundleAsync()
    {
        LastEvidenceBundle = await _evidenceBundles.ExportAsync(
            SymptomInput,
            new TriageResult
            {
                Query = SymptomInput,
                Candidates = TriageCandidates.ToList()
            },
            LastHealthCheckReport,
            LastRunbookSummary);
    }

    // ── Wizard ────────────────────────────────────────────────────────────
    public void StartWizard(FixItem fix)
    {
        WizardFix     = fix;
        _wizardStep   = 0;
        WizardVisible = true;
        RefreshWizard();
    }

    public async Task WizardNextAsync()
    {
        if (WizardHasScript && CurrentStep?.Script is { } script)
        {
            try { await _scripts.RunAsync(script, WizardFix?.RequiresAdmin ?? false); }
            catch { }
        }

        _wizardStep++;
        if (_wizardStep >= (WizardFix?.Steps.Count ?? 0))
        {
            if (WizardFix is not null)
            {
                WizardFix.Status     = FixStatus.Success;
                WizardFix.LastOutput = "Wizard completed.";
                if (_settings.LogFixHistory)
                    _log.Record(_catalog.GetCategoryTitle(WizardFix), WizardFix);
                RefreshHistory();
                RefreshRecentlyRun();
                OnPropertyChanged(nameof(FixesTodayCount));
            }
            WizardVisible = false;
            WizardFix     = null;
        }
        else
        {
            RefreshWizard();
        }
    }

    public void WizardCancel()
    {
        WizardVisible = false;
        WizardFix     = null;
    }

    // ── System Info ───────────────────────────────────────────────────────
    public async Task LoadSystemInfoAsync()
    {
        if (SnapshotLoading) return;
        SnapshotLoading = true;
        try   { Snapshot = await _sysInfo.GetSnapshotAsync(); }
        catch { }
        SnapshotLoading = false;
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
        catch
        {
            _allInstalledPrograms.Clear();
            RefreshInstalledPrograms();
        }
        finally
        {
            InstalledProgramsLoading = false;
        }
    }

    private async Task LoadUpdateInfoAsync()
    {
        try { LastUpdateInfo = await _updates.CheckForUpdatesAsync(); }
        catch { LastUpdateInfo = null; }
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

    // ── History ───────────────────────────────────────────────────────────
    private void RefreshHistory()
    {
        HistoryEntries.Clear();
        foreach (var e in _log.Entries) HistoryEntries.Add(e);
        OnPropertyChanged(nameof(FixesTodayCount));
        FilterHistory();
    }

    public void ClearHistory() { _log.Clear(); RefreshHistory(); RefreshRecentlyRun(); }

    private void FilterHistory()
    {
        FilteredHistoryEntries.Clear();

        IEnumerable<FixLogEntry> items = HistoryEntries;
        if (!string.IsNullOrWhiteSpace(_historySearchText))
        {
            var q = _historySearchText.Trim();
            items = items.Where(e =>
                e.FixTitle.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.Output.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in items)
            FilteredHistoryEntries.Add(item);

        OnPropertyChanged(nameof(HasHistoryEntries));
        OnPropertyChanged(nameof(HasFilteredHistoryEntries));
        OnPropertyChanged(nameof(HistorySummaryText));
    }

    public Task RerunHistoryEntryAsync(FixLogEntry entry)
        => string.IsNullOrWhiteSpace(entry.FixId)
            ? Task.CompletedTask
            : RunFixByIdAsync(entry.FixId);

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
    }

    private void RefreshNotifications()
    {
        OnPropertyChanged(nameof(UnreadNotifCount));
        OnPropertyChanged(nameof(NotificationEntries));
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(NotificationSummaryText));
    }

    // ── Settings ──────────────────────────────────────────────────────────
    public void SaveSettings()
    {
        _settingsSvc.Save(_settings);
    }

    public void ApplyTheme(string theme)
    {
        _settings.Theme = theme;
        App.SwitchTheme(theme);
        _settingsSvc.Save(_settings);
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
