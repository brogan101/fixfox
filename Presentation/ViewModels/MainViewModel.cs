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
    // â”€â”€ Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
    private readonly BrowserExtensionReviewService _browserExtensions;
    private readonly WorkFromHomeDependencyService _workFromHomeDependencies;
    private readonly SchedulerService        _scheduler;
    private readonly IToolboxService _toolbox;
    private readonly IMaintenanceProfileService _maintenanceProfileService;
    private readonly ISupportCenterService _supportCenterService;
    private readonly ICommandPaletteService _commandPaletteService;
    private readonly ITextSubstitutionService _textSubstitutions;
    private readonly IDashboardWorkspaceService _dashboardWorkspace;
    private readonly IDashboardSuggestionSignalService _dashboardSuggestionSignals;
    private readonly IAutomationHistoryService _automationHistory;
    private readonly IAutomationCoordinatorService _automationCoordinator;
    private readonly HistoryPagingService _historyPaging = new();
    private readonly ToolboxWorkspaceState _toolboxWorkspace = new();
    private readonly Dictionary<string, bool> _deviceHealthSectionStates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SystemOverview"] = true,
        ["Storage"] = false,
        ["StartupPerformance"] = false,
        ["WindowsHealth"] = false,
        ["Network"] = false,
        ["DevicesPeripherals"] = false,
        ["Security"] = false
    };
    private readonly List<RunbookDefinition> _allRunbooks = [];
    private readonly List<FixBundle> _allBundles = [];
    private readonly List<MaintenanceProfileDefinition> _allMaintenanceProfiles = [];
    private readonly List<HealthAlert> _healthAlertsRaw = [];
    private string _highlightedHealthAlertId = "";
    private int _simplifiedOnboardingStep = 1;

    public Func<RunbookDefinition, Task<bool>>? RunbookPreflightRequestAsync { get; set; }
    public Func<RunbookDefinition, RunbookExecutionSummary, Task>? RunbookPostResultRequestAsync { get; set; }
    public Func<FixItem, Task<SimplifiedConfirmationDecision>>? FixConfirmationRequestAsync { get; set; }
    public Action? OpenGlobalSearchRequest { get; set; }

    // â”€â”€ Navigation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
                nameof(ShowDashboard), nameof(ShowFixes), nameof(ShowFixMyPc), nameof(ShowBundles),
                nameof(ShowSystemInfo), nameof(ShowSymptomChecker),
                nameof(ShowToolbox), nameof(ShowHistory), nameof(ShowHandoff), nameof(ShowSettings),
                nameof(ShowNavDashboard), nameof(ShowNavFixes), nameof(ShowNavBundles), nameof(ShowNavSystemInfo),
                nameof(ShowNavSymptomChecker), nameof(ShowNavToolbox), nameof(ShowNavHistory), nameof(ShowNavHandoff),
                nameof(ShowNavSettings), nameof(FixNavLabel), nameof(FixNavAutomationName)
            }) OnPropertyChanged(n);
        }
    }

    public bool ShowDashboard       => CurrentPage == Page.Dashboard;
    public bool ShowFixes           => CurrentPage == Page.Fixes;
    public bool ShowFixMyPc         => CurrentPage == Page.FixMyPc;
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
        Page.FixMyPc         => "Fix My PC",
        Page.Bundles         => "Automation",
        Page.SystemInfo      => "Device Health",
        Page.SymptomChecker  => "Guided Diagnosis",
        Page.Toolbox         => "Windows Tools",
        Page.History         => "Activity",
        Page.Handoff         => "Support Package",
        Page.Settings        => "Settings",
        _                    => ""
    };

    /// <summary>Status bar label â€” same text as breadcrumb.</summary>
    public string CurrentPageLabel => BreadcrumbText;
    public string CurrentPageSummaryText => CurrentPage switch
    {
        Page.Dashboard => "See what needs attention and take the next safe action.",
        Page.Fixes => "Browse verified repairs and run them directly.",
        Page.FixMyPc => "Pick the problem in plain English and let FixFox guide the next safe step.",
        Page.Bundles => "Review scheduled automations, watchers, and safe maintenance.",
        Page.SystemInfo => "Use the device baseline and support centers to choose the right path.",
        Page.SymptomChecker => "Rank likely causes before you run a repair.",
        Page.Toolbox => "Open the exact Windows tool you need.",
        Page.History => "Review what changed, rerun it, or escalate with evidence.",
        Page.Handoff => "Package the issue cleanly when self-service stops helping.",
        Page.Settings => "Tune startup behavior, profiles, and local data handling.",
        _ => ""
    };
    public bool ShowNavDashboard => true;
    public bool ShowNavFixes => true;
    public bool ShowNavBundles => !SimplifiedModeEnabled;
    public bool ShowNavSystemInfo => !SimplifiedModeEnabled;
    public bool ShowNavSymptomChecker => !SimplifiedModeEnabled;
    public bool ShowNavToolbox => !SimplifiedModeEnabled;
    public bool ShowNavHistory => !SimplifiedModeEnabled;
    public bool ShowNavHandoff => !SimplifiedModeEnabled;
    public bool ShowNavSettings => true;
    public string FixNavLabel => SimplifiedModeEnabled ? "Fix My PC" : "Repair Library";
    public string FixNavAutomationName => SimplifiedModeEnabled ? "Fix My PC" : "Repair Library";
    public string ProductDisplayName => Branding.AppName;
    public string ProductSubtitle => Branding.AppSubtitle;
    public string ProductTagline => Branding.ProductTagline;
    public ITextSubstitutionService TextSubstitutions => _textSubstitutions;
    public string OnboardingTitleText => $"Set up {ProductDisplayName}";
    public string OnboardingSummaryText =>
        $"{ProductDisplayName} checks workstation health, runs guided repairs, and builds support packages when self-service is not enough. Choose the defaults that fit this device, then start with a health check or jump straight into the workspace.";
    public string OnboardingCapabilitySummaryText =>
        $"Use Home for the next safe action, Guided Diagnosis for plain-language symptoms, Repair Library for direct fixes, and Support Package when the issue needs escalation.";
    public string OnboardingPrivacySummaryText =>
        $"{ProductDisplayName} keeps settings, receipts, logs, and support packages on this PC. You can review what a package contains before opening or sharing it.";
    public int SimplifiedOnboardingStep
    {
        get => _simplifiedOnboardingStep;
        set
        {
            if (_simplifiedOnboardingStep == value)
                return;

            _simplifiedOnboardingStep = Math.Clamp(value, 1, 3);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSimplifiedOnboardingStep1));
            OnPropertyChanged(nameof(IsSimplifiedOnboardingStep2));
            OnPropertyChanged(nameof(IsSimplifiedOnboardingStep3));
        }
    }
    public bool IsSimplifiedOnboardingStep1 => SimplifiedOnboardingStep == 1;
    public bool IsSimplifiedOnboardingStep2 => SimplifiedOnboardingStep == 2;
    public bool IsSimplifiedOnboardingStep3 => SimplifiedOnboardingStep == 3;
    public string LocalDataSummaryText =>
        $"{ProductDisplayName} stores its logs, repair history, and support packages locally so you can audit what was captured before sharing it with anyone else.";
    public string ProductDisplayModeText => $"{FormatEditionLabel(EditionSnapshot.Edition)}{(EditionSnapshot.ManagedMode ? " â€¢ managed" : "")}";
    public string CurrentProfileStatusText
    {
        get
        {
            var parts = new List<string> { $"{SelectedBehaviorProfile} profile" };
            if (AdvancedModeEnabled)
                parts.Add("Advanced");
            if (EditionSnapshot.ManagedMode)
                parts.Add("Managed");
            return string.Join(" â€¢ ", parts);
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
    public bool SimplifiedModeEnabled
    {
        get => _settings.SimplifiedMode;
        set
        {
            if (_settings.SimplifiedMode == value)
                return;

            _settings.SimplifiedMode = value;
            _textSubstitutions.SetSimplifiedMode(value);
            SaveSettingsLight();
            if (value && CurrentPage == Page.Fixes)
                CurrentPage = Page.FixMyPc;
            if (!value && CurrentPage == Page.FixMyPc)
                CurrentPage = Page.Fixes;

            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowAdvancedSettings));
            OnPropertyChanged(nameof(ShowSimpleHelp));
            OnPropertyChanged(nameof(FixNavLabel));
            OnPropertyChanged(nameof(FixNavAutomationName));
            OnPropertyChanged(nameof(ShowNavBundles));
            OnPropertyChanged(nameof(ShowNavSystemInfo));
            OnPropertyChanged(nameof(ShowNavSymptomChecker));
            OnPropertyChanged(nameof(ShowNavToolbox));
            OnPropertyChanged(nameof(ShowNavHistory));
            OnPropertyChanged(nameof(ShowNavHandoff));
            OnPropertyChanged(nameof(VisibleNavItemCount));
            OnPropertyChanged(nameof(CurrentPageSummaryText));
            OnPropertyChanged(nameof(BreadcrumbText));
            RefreshActiveFixes();
            RefreshCommandPalette();
        }
    }
    public bool ShowAdvancedSettings => !SimplifiedModeEnabled;
    public bool ShowSimpleHelp => SimplifiedModeEnabled;
    public bool IsOnboardingModeSelectionVisible => ShowPrivacyNotice && string.IsNullOrWhiteSpace(_settings.FirstRunExperienceMode);
    public bool IsSimpleOnboardingVisible => ShowPrivacyNotice && string.Equals(_settings.FirstRunExperienceMode, "Simple", StringComparison.OrdinalIgnoreCase);
    public bool IsFullOnboardingVisible => ShowPrivacyNotice && !IsOnboardingModeSelectionVisible && !IsSimpleOnboardingVisible;
    public int VisibleNavItemCount => GetNavigationPages(SimplifiedModeEnabled).Count;

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

    // â”€â”€ Startup verifier panel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private bool _forceShowVerifyPanel;
    public bool ForceShowVerifyPanel
    {
        get => _forceShowVerifyPanel;
        set { _forceShowVerifyPanel = value; OnPropertyChanged(); }
    }

    // â”€â”€ Sidebar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ Fix Center â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
            OnPropertyChanged(nameof(SelectedFixCategoryLabel));
            OnPropertyChanged(nameof(SelectedCategoryAccessibleFixCount));
            OnPropertyChanged(nameof(FixListHeaderText));
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
    public int TotalAccessibleFixCount => Categories.Sum(category => GetAccessibleFixCount(category));
    public int SelectedCategoryAccessibleFixCount => SelectedCategory is null ? TotalAccessibleFixCount : GetAccessibleFixCount(SelectedCategory);
    public string SelectedFixCategoryLabel => SelectedCategory?.Title ?? "All Fixes";
    public string FixListHeaderText => !string.IsNullOrWhiteSpace(SearchText)
        ? $"{SelectedFixCategoryLabel}  ·  {ActiveFixCount} of {SelectedCategoryAccessibleFixCount} fixes"
        : $"{SelectedFixCategoryLabel}  ·  {SelectedCategoryAccessibleFixCount} fixes";
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
            var matches = _catalog.Search(_searchText);
            source = _selectedCategory is null
                ? matches
                : matches.Where(fix => string.Equals(fix.Category, _selectedCategory.Title, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            IsSearching = false;
            source = _selectedCategory is null
                ? Categories.SelectMany(category => category.Fixes)
                : _selectedCategory.Fixes;
        }

        foreach (var f in source.Where(CanAccessFix)) ActiveFixes.Add(f);
        OnPropertyChanged(nameof(ActiveFixCount));
        OnPropertyChanged(nameof(ActiveFixCountText));
        OnPropertyChanged(nameof(HasActiveFixes));
        OnPropertyChanged(nameof(TotalAccessibleFixCount));
        OnPropertyChanged(nameof(SelectedCategoryAccessibleFixCount));
        OnPropertyChanged(nameof(SelectedFixCategoryLabel));
        OnPropertyChanged(nameof(FixListHeaderText));
        OnPropertyChanged(nameof(FixLibrarySummaryText));
        OnPropertyChanged(nameof(FixLibraryEmptyStateTitle));
        OnPropertyChanged(nameof(FixLibraryEmptyStateText));
    }

    public int GetAccessibleFixCount(FixCategory? category)
        => (category?.Fixes ?? Categories.SelectMany(item => item.Fixes))
            .Count(CanAccessFix);

    public void SelectFixCategory(FixCategory? category)
    {
        SelectedCategory = category;
    }

    public void ToggleFixExpansion(FixItem fix)
    {
        if (fix is null)
            return;

        fix.IsExpanded = !fix.IsExpanded;
    }

    public string GetFixExpandedDurationText(FixItem fix)
    {
        if (!string.IsNullOrWhiteSpace(fix.EstTime))
            return fix.EstTime;

        if (fix.EstimatedDurationSeconds <= 60)
            return "~30 seconds";

        var minutes = (int)Math.Ceiling(fix.EstimatedDurationSeconds / 60d);
        return $"~{minutes} minute{(minutes == 1 ? "" : "s")}";
    }

    public string GetFixWhatChangesText(FixItem fix)
    {
        var repair = _repairCatalog.GetRepair(fix.Id);
        if (!string.IsNullOrWhiteSpace(repair?.WhatWillHappen))
            return repair.WhatWillHappen;

        if (fix.HasSteps)
            return "FixFox will guide you through checked steps before any change is applied.";

        if (fix.RequiresAdmin)
            return "This fix changes Windows settings, services, or system state to repair the issue.";

        return "This fix changes the affected app or Windows setting in a safe, targeted way.";
    }

    public string GetFixFailureGuidanceText(FixItem fix)
    {
        var repair = _repairCatalog.GetRepair(fix.Id);
        if (!string.IsNullOrWhiteSpace(repair?.SuggestedNextStepOnFailure))
            return repair.SuggestedNextStepOnFailure;

        if (fix.HasSteps)
            return "If the problem continues, stop at the failed step and switch to Guided Diagnosis or a related workflow.";

        return "If the problem continues, review the output in Activity and try the related guided workflow or support bundle next.";
    }

    private void HydrateFixLibraryPresentationFields()
    {
        foreach (var fix in Categories.SelectMany(category => category.Fixes))
        {
            fix.ExpandedDurationText = GetFixExpandedDurationText(fix);
            fix.ExpandedWhatChanges = GetFixWhatChangesText(fix);
            fix.ExpandedFailureGuidance = GetFixFailureGuidanceText(fix);
        }
    }

    // â”€â”€ Dashboard / Quick Scan â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public ObservableCollection<ScanResult> ScanResults { get; } = [];

    private bool   _scanRunning;
    private string _scanStatusText = "Click 'Run Quick Scan' to check your PC's health.";
    private string _activeAutomationRuleTitle = "";

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
    public string ActiveAutomationRuleTitle
    {
        get => _activeAutomationRuleTitle;
        set
        {
            if (string.Equals(_activeAutomationRuleTitle, value, StringComparison.Ordinal))
                return;

            _activeAutomationRuleTitle = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasActiveWork));
            OnPropertyChanged(nameof(HasDashboardActiveOperation));
            OnPropertyChanged(nameof(DashboardActiveRunLabel));
            OnPropertyChanged(nameof(ActiveWorkSummary));
            OnPropertyChanged(nameof(DashboardStatusBarCollapsed));
        }
    }

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

    // â”€â”€ Recently Run Fixes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public ObservableCollection<FixItem> RecentlyRunFixes { get; } = [];
    public bool HasRecentlyRunFixes => RecentlyRunFixes.Count > 0;
    public ObservableCollection<RecentQuickActionEntry> RecentQuickActions { get; } = [];
    public bool HasRecentQuickActions => RecentQuickActions.Count > 0;
    public ObservableCollection<DashboardSuggestion> DashboardSuggestions { get; } = [];
    public bool HasDashboardSuggestions => DashboardSuggestions.Count > 0;
    public string DashboardSuggestionsEmptyText => HasDashboardSuggestions
        ? ""
        : "Your PC looks healthy. Nothing needs attention right now.";
    public ObservableCollection<HealthAlert> HealthAlerts { get; } = [];
    public bool HasHealthAlerts => HealthAlerts.Count > 0;
    public string HealthAlertsHeaderText => $"Health alerts  ·  {HealthAlerts.Count} item{(HealthAlerts.Count == 1 ? "" : "s")}";
    public string HighlightedHealthAlertId
    {
        get => _highlightedHealthAlertId;
        set
        {
            if (string.Equals(_highlightedHealthAlertId, value, StringComparison.Ordinal))
                return;

            _highlightedHealthAlertId = value;
            OnPropertyChanged();
        }
    }
    public ObservableCollection<RepairHistoryEntry> RecentFailedEntries { get; } = [];
    public bool HasRecentFailures => RecentFailedEntries.Count > 0;
    public ObservableCollection<DashboardAlert> DashboardAlerts { get; } = [];
    public bool HasDashboardAlerts => DashboardAlerts.Count > 0;
    public ObservableCollection<RunbookDefinition> SuggestedRunbooks { get; } = [];
    public bool HasSuggestedRunbooks => SuggestedRunbooks.Count > 0;
    public ObservableCollection<OnboardingChecklistItem> OnboardingChecklistItems { get; } = [];
    public bool HasOnboardingChecklist => !Settings.OnboardingChecklistDismissed && OnboardingChecklistItems.Any(item => !item.IsCompleted);
    public ObservableCollection<SimplifiedProblemOption> SimplifiedProblemOptions { get; } = [];
    public IReadOnlyList<SimplifiedProblemOption> PrimarySimplifiedProblemOptions => SimplifiedProblemOptions.Where(option => option.Key != "other").ToList();
    public SimplifiedProblemOption? SimplifiedOtherProblemOption => SimplifiedProblemOptions.FirstOrDefault(option => option.Key == "other");
    public IReadOnlyList<RecentQuickActionEntry> SimplifiedRecentQuickActions => RecentQuickActions.Take(3).ToList();
    public bool HasSimplifiedRecentQuickActions => SimplifiedRecentQuickActions.Count > 0;
    public RepairHistoryEntry? DashboardLastReceipt => HistoryEntries.FirstOrDefault();
    public bool HasDashboardLastReceipt => DashboardLastReceipt is not null;
    public string DashboardLastReceiptSummary =>
        DashboardLastReceipt is null
            ? ""
            : $"{DashboardLastReceipt.FixTitle} - {GetOutcomeLabel(DashboardLastReceipt.Outcome)} - {GetRelativeTimeText(DashboardLastReceipt.Timestamp)}";
    public string DashboardLastReceiptTitle => DashboardLastReceipt?.FixTitle ?? "";
    public string DashboardLastReceiptOutcomeLabel => DashboardLastReceipt is null ? "" : GetOutcomeLabel(DashboardLastReceipt.Outcome);
    public string DashboardLastReceiptRelativeText => DashboardLastReceipt is null ? "" : GetRelativeTimeText(DashboardLastReceipt.Timestamp);
    public bool DashboardStatusBarCollapsed => AutomationAttentionCount == 0 && !HasDashboardActiveOperation && !HasDashboardLastReceipt;
    public string DashboardAutomationAttentionText => AutomationAttentionCount > 0
        ? $"Needs attention: {AutomationAttentionCount}"
        : "All automations healthy";
    public string DashboardAutomationAttentionSubtext => AutomationAttentionCount > 0
        ? "Open the attention queue to review skipped, blocked, or failed automation runs."
        : "No failed, skipped, or blocked automation runs need review right now.";

    public bool HealthMonitoringEnabled
    {
        get => _settings.EnableBackgroundHealthMonitoring;
        set
        {
            if (_settings.EnableBackgroundHealthMonitoring == value)
                return;

            _settings.EnableBackgroundHealthMonitoring = value;
            SaveSettingsLight();
            OnPropertyChanged();
        }
    }

    public bool ShowHealthAlertTrayNotifications
    {
        get => _settings.ShowHealthAlertTrayNotifications;
        set
        {
            if (_settings.ShowHealthAlertTrayNotifications == value)
                return;

            _settings.ShowHealthAlertTrayNotifications = value;
            SaveSettingsLight();
            OnPropertyChanged();
        }
    }

    public bool SendWeeklyHealthSummary
    {
        get => _settings.SendWeeklyHealthSummary;
        set
        {
            if (_settings.SendWeeklyHealthSummary == value)
                return;

            _settings.SendWeeklyHealthSummary = value;
            SaveSettingsLight();
            OnPropertyChanged();
        }
    }

    public bool HealthAlertFrequencyAll
    {
        get => _settings.HealthAlertNotificationFrequency == HealthAlertNotificationFrequency.All;
        set
        {
            if (!value)
                return;

            _settings.HealthAlertNotificationFrequency = HealthAlertNotificationFrequency.All;
            SaveSettingsLight();
            RaiseHealthMonitoringSettingChanged();
        }
    }

    public bool HealthAlertFrequencyWarningsAndCritical
    {
        get => _settings.HealthAlertNotificationFrequency == HealthAlertNotificationFrequency.WarningsAndCritical;
        set
        {
            if (!value)
                return;

            _settings.HealthAlertNotificationFrequency = HealthAlertNotificationFrequency.WarningsAndCritical;
            SaveSettingsLight();
            RaiseHealthMonitoringSettingChanged();
        }
    }

    public bool HealthAlertFrequencyCriticalOnly
    {
        get => _settings.HealthAlertNotificationFrequency == HealthAlertNotificationFrequency.CriticalOnly;
        set
        {
            if (!value)
                return;

            _settings.HealthAlertNotificationFrequency = HealthAlertNotificationFrequency.CriticalOnly;
            SaveSettingsLight();
            RaiseHealthMonitoringSettingChanged();
        }
    }

    private void RefreshRecentlyRun()
    {
        RecentlyRunFixes.Clear();
        RecentQuickActions.Clear();
        RecentFailedEntries.Clear();
        var seen = new HashSet<string>();
        foreach (var e in _repairHistory.Entries.Where(entry => !string.IsNullOrWhiteSpace(entry.FixId)))
        {
            if (seen.Count >= 3) break;
            if (!seen.Add(e.FixId)) continue;
            var fix = _catalog.GetById(e.FixId);
            if (fix is not null) RecentlyRunFixes.Add(fix);
        }

        foreach (var entry in _repairHistory.Entries
                     .Where(entry => !string.IsNullOrWhiteSpace(entry.FixId) || !string.IsNullOrWhiteSpace(entry.RunbookId))
                     .DistinctBy(entry => string.IsNullOrWhiteSpace(entry.RunbookId) ? $"fix:{entry.FixId}" : $"runbook:{entry.RunbookId}")
                     .Take(5))
        {
            RecentQuickActions.Add(new RecentQuickActionEntry
            {
                ReceiptId = entry.Id,
                DisplayTitle = entry.FixTitle,
                FixId = entry.FixId,
                RunbookId = entry.RunbookId,
                Timestamp = entry.Timestamp,
                RiskLevel = !string.IsNullOrWhiteSpace(entry.FixId) ? _catalog.GetById(entry.FixId)?.RiskLevel : null,
                RequiresAdmin = entry.RequiresAdmin,
                Glyph = string.IsNullOrWhiteSpace(entry.RunbookId) ? "\uE90F" : "\uE7C4"
            });
        }

        foreach (var failure in _repairHistory.Entries.Where(entry => !entry.Success).Take(4))
            RecentFailedEntries.Add(failure);
        OnPropertyChanged(nameof(HasRecentlyRunFixes));
        OnPropertyChanged(nameof(HasRecentQuickActions));
        OnPropertyChanged(nameof(SimplifiedRecentQuickActions));
        OnPropertyChanged(nameof(HasSimplifiedRecentQuickActions));
        OnPropertyChanged(nameof(HasRecentFailures));
        OnPropertyChanged(nameof(DashboardLastReceipt));
        OnPropertyChanged(nameof(HasDashboardLastReceipt));
        OnPropertyChanged(nameof(DashboardLastReceiptSummary));
        OnPropertyChanged(nameof(DashboardLastReceiptTitle));
        OnPropertyChanged(nameof(DashboardLastReceiptOutcomeLabel));
        OnPropertyChanged(nameof(DashboardLastReceiptRelativeText));
        OnPropertyChanged(nameof(DashboardStatusBarCollapsed));
        RefreshCommandPalette();
    }

    public async Task RefreshDashboardSuggestionsAsync()
    {
        var signals = await _dashboardSuggestionSignals.EvaluateAsync(_settings.AutomationRules, _automationHistory.Entries);
        var now = DateTime.UtcNow;
        DashboardSuggestions.Clear();
        foreach (var suggestion in FilterDismissedDashboardSuggestions(
                     _dashboardWorkspace.BuildSuggestions(signals, _settings.AutomationRules, _automationHistory.Entries, now),
                     _settings.DismissedDashboardSuggestions,
                     now)
                 .Take(5))
        {
            DashboardSuggestions.Add(suggestion);
        }

        OnPropertyChanged(nameof(HasDashboardSuggestions));
        OnPropertyChanged(nameof(DashboardSuggestionsEmptyText));
    }

    public void SyncHealthAlerts(IReadOnlyList<HealthAlert> alerts)
    {
        _healthAlertsRaw.Clear();
        _healthAlertsRaw.AddRange(alerts);

        HealthAlerts.Clear();
        foreach (var alert in alerts.Where(alert => !alert.IsDismissed).OrderByDescending(alert => alert.Severity).ThenBy(alert => alert.Title))
        {
            HealthAlerts.Add(new HealthAlert
            {
                Id = alert.Id,
                Title = alert.Title,
                Body = alert.Body,
                Severity = alert.Severity,
                ActionLabel = alert.ActionLabel,
                ActionTarget = alert.ActionTarget,
                DetectedUtc = alert.DetectedUtc,
                IsDismissed = alert.IsDismissed,
                IsHighlighted = string.Equals(alert.Id, HighlightedHealthAlertId, StringComparison.OrdinalIgnoreCase)
            });
        }

        if (!HealthAlerts.Any(alert => string.Equals(alert.Id, HighlightedHealthAlertId, StringComparison.OrdinalIgnoreCase)))
            HighlightedHealthAlertId = "";

        OnPropertyChanged(nameof(HasHealthAlerts));
        OnPropertyChanged(nameof(HealthAlertsHeaderText));
    }

    public void HighlightHealthAlert(string alertId)
    {
        HighlightedHealthAlertId = alertId;
        foreach (var alert in HealthAlerts)
            alert.IsHighlighted = string.Equals(alert.Id, alertId, StringComparison.OrdinalIgnoreCase);
    }

    public void DismissHealthAlert(HealthAlert alert)
    {
        if (string.IsNullOrWhiteSpace(alert.Id))
            return;

        _settings.DismissedHealthAlerts.RemoveAll(entry => string.Equals(entry.AlertId, alert.Id, StringComparison.OrdinalIgnoreCase));
        _settings.DismissedHealthAlerts.Add(new DismissedHealthAlert
        {
            AlertId = alert.Id,
            DismissedUntilUtc = DateTime.UtcNow.AddHours(24)
        });
        _settingsSvc.Save(_settings);
        SyncHealthAlerts(_healthAlertsRaw
            .Select(item => string.Equals(item.Id, alert.Id, StringComparison.OrdinalIgnoreCase)
                ? new HealthAlert
                {
                    Id = item.Id,
                    Title = item.Title,
                    Body = item.Body,
                    Severity = item.Severity,
                    ActionLabel = item.ActionLabel,
                    ActionTarget = item.ActionTarget,
                    DetectedUtc = item.DetectedUtc,
                    IsDismissed = true,
                    IsHighlighted = item.IsHighlighted
                }
                : item)
            .ToList());
    }

    public async Task ExecuteHealthAlertActionAsync(HealthAlert alert)
    {
        if (string.IsNullOrWhiteSpace(alert.ActionTarget))
            return;

        var target = alert.ActionTarget.Trim();
        if (_catalog.GetById(target) is not null)
        {
            await RunFixByIdAsync(target);
            return;
        }

        var bundle = Bundles.FirstOrDefault(item => string.Equals(item.Id, target, StringComparison.OrdinalIgnoreCase));
        if (bundle is not null)
        {
            await RunBundleAsync(bundle);
            return;
        }

        if (_allRunbooks.FirstOrDefault(item => string.Equals(item.Id, target, StringComparison.OrdinalIgnoreCase)) is { } runbook)
        {
            await RunRunbookByIdAsync(runbook.Id);
            return;
        }

        if (Enum.TryParse<Page>(target, ignoreCase: true, out var page))
        {
            CurrentPage = page;
            return;
        }

        if (target.Contains(':', StringComparison.Ordinal))
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not open health alert action target '{target}': {ex.Message}");
            }
        }
    }

    public static IReadOnlyList<DashboardSuggestion> FilterDismissedDashboardSuggestions(
        IEnumerable<DashboardSuggestion> suggestions,
        IEnumerable<DismissedDashboardSuggestion> dismissedEntries,
        DateTime nowUtc)
    {
        var dismissedKeys = dismissedEntries
            .Where(entry => entry.DismissedUntilUtc > nowUtc)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return suggestions
            .Where(suggestion => !dismissedKeys.Contains(suggestion.Key))
            .ToList();
    }

    public void DismissDashboardSuggestion(DashboardSuggestion suggestion)
    {
        if (suggestion is null || string.IsNullOrWhiteSpace(suggestion.Key))
            return;

        _settings.DismissedDashboardSuggestions.RemoveAll(entry =>
            string.Equals(entry.Key, suggestion.Key, StringComparison.OrdinalIgnoreCase));
        _settings.DismissedDashboardSuggestions.Add(new DismissedDashboardSuggestion
        {
            Key = suggestion.Key,
            DismissedUntilUtc = DateTime.UtcNow.AddDays(7)
        });
        SaveSettingsLight();

        DashboardSuggestions.Remove(suggestion);
        OnPropertyChanged(nameof(HasDashboardSuggestions));
        OnPropertyChanged(nameof(DashboardSuggestionsEmptyText));
    }

    public async Task RunDashboardSuggestionAsync(DashboardSuggestion suggestion)
    {
        if (suggestion is null)
            return;

        switch (suggestion.ActionKind)
        {
            case DashboardActionKind.Fix when !string.IsNullOrWhiteSpace(suggestion.ActionTargetId):
                await RunFixByIdAsync(suggestion.ActionTargetId);
                break;
            case DashboardActionKind.Runbook when !string.IsNullOrWhiteSpace(suggestion.ActionTargetId):
                await RunRunbookByIdAsync(suggestion.ActionTargetId);
                break;
            case DashboardActionKind.Page when suggestion.ActionPage.HasValue:
                CurrentPage = suggestion.ActionPage.Value;
                break;
        }
    }

    public async Task RunRecentQuickActionAsync(RecentQuickActionEntry entry)
    {
        if (entry is null)
            return;

        if (entry.RequiresAdmin && !_elevation.IsElevated)
        {
            _notifs.Add(new AppNotification
            {
                Level = NotifLevel.Warning,
                Title = "Administrator rights required",
                Message = "Relaunch FixFox as administrator to run this action again."
            });
            return;
        }

        if (entry.IsRunbook)
        {
            await RunRunbookByIdAsync(entry.RunbookId);
            return;
        }

        var fix = _catalog.GetById(entry.FixId);
        if (fix is null)
        {
            _notifs.Add(new AppNotification
            {
                Level = NotifLevel.Warning,
                Title = "Fix no longer available",
                Message = "This fix is no longer available in the current FixFox catalog."
            });
            return;
        }

        await RunFixByIdAsync(entry.FixId);
    }

    // â”€â”€ Symptom Checker â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public ObservableCollection<FixItem> SymptomResults { get; } = [];
    public ObservableCollection<TriageCandidate> TriageCandidates { get; } = [];
    public ObservableCollection<TriageCandidate> RunnerUpTriageCandidates { get; } = [];

    private string _symptomInput = "";
    private bool   _symptomSearched;
    private TriageCandidate? _topTriageCandidate;

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
    public TriageCandidate? TopTriageCandidate
    {
        get => _topTriageCandidate;
        private set
        {
            _topTriageCandidate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasTopTriageCandidate));
            OnPropertyChanged(nameof(TopTriageFix));
            OnPropertyChanged(nameof(TopTriageGuidedCheckFix));
            OnPropertyChanged(nameof(TopTriageRunbook));
            OnPropertyChanged(nameof(ShowDiagnosisFixActions));
            OnPropertyChanged(nameof(ShowDiagnosisRunbookActions));
            OnPropertyChanged(nameof(ShowDiagnosisGuidedCheckAction));
            OnPropertyChanged(nameof(ShowDiagnosisEscalateAction));
        }
    }
    public bool HasTopTriageCandidate => TopTriageCandidate is not null;
    public bool HasRunnerUpTriageCandidates => RunnerUpTriageCandidates.Count > 0;
    public FixItem? TopTriageFix => ResolveFix(TopTriageCandidate?.PrimaryFixId);
    public FixItem? TopTriageGuidedCheckFix => ResolveFix(TopTriageCandidate?.GuidedCheckFixId ?? TopTriageCandidate?.PrimaryFixId);
    public RunbookDefinition? TopTriageRunbook => ResolveRunbook(TopTriageCandidate?.PrimaryRunbookId);
    public bool ShowDiagnosisFixActions => TopTriageFix is not null;
    public bool ShowDiagnosisRunbookActions => TopTriageFix is null && TopTriageRunbook is not null;
    public bool ShowDiagnosisGuidedCheckAction => TopTriageGuidedCheckFix is not null;
    public bool ShowDiagnosisEscalateAction => HasTopTriageCandidate;

    public void RunSymptomSearch()
    {
        SymptomResults.Clear();
        TriageCandidates.Clear();
        RunnerUpTriageCandidates.Clear();
        TopTriageCandidate = null;
        if (string.IsNullOrWhiteSpace(_symptomInput))
        {
            SymptomSearched = false;
            OnPropertyChanged(nameof(SymptomCount));
            OnPropertyChanged(nameof(SymptomHasNoResults));
            OnPropertyChanged(nameof(HasTriageCandidates));
            OnPropertyChanged(nameof(HasRunnerUpTriageCandidates));
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
        TopTriageCandidate = triageResult.Candidates.FirstOrDefault();
        var runnerUpIndex = 1;
        foreach (var candidate in triageResult.Candidates.Skip(1).Take(2))
        {
            candidate.DisplayIndex = runnerUpIndex++;
            RunnerUpTriageCandidates.Add(candidate);
        }

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
        OnPropertyChanged(nameof(HasRunnerUpTriageCandidates));
    }

    // â”€â”€ Fix Bundles â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public Task RunTopDiagnosisFixAsync()
        => TopTriageFix is null ? Task.CompletedTask : RunBestDiagnosisActionAsync(TopTriageFix);

    public void RunTopDiagnosisGuidedCheck()
    {
        if (TopTriageGuidedCheckFix?.HasSteps == true)
            StartWizard(TopTriageGuidedCheckFix);
    }

    public void OpenDiagnosisEscalation()
        => CurrentPage = Page.Handoff;

    private Task RunBestDiagnosisActionAsync(FixItem fix)
    {
        if (fix.HasSteps)
        {
            StartWizard(fix);
            return Task.CompletedTask;
        }

        return fix.HasScript ? RunFixAsync(fix) : Task.CompletedTask;
    }

    private FixItem? ResolveFix(string? fixId)
        => string.IsNullOrWhiteSpace(fixId) ? null : _catalog.GetById(fixId);

    private RunbookDefinition? ResolveRunbook(string? runbookId)
        => string.IsNullOrWhiteSpace(runbookId)
            ? null
            : Runbooks.FirstOrDefault(item => string.Equals(item.Id, runbookId, StringComparison.OrdinalIgnoreCase))
                ?? _allRunbooks.FirstOrDefault(item => string.Equals(item.Id, runbookId, StringComparison.OrdinalIgnoreCase));

    public async Task RunSimplifiedProblemAsync(SimplifiedProblemOption? option)
    {
        if (option is null)
            return;

        switch (option.ActionKind)
        {
            case SupportActionKind.GlobalSearch:
                OpenGlobalSearchRequest?.Invoke();
                return;
            case SupportActionKind.Page:
                if (Enum.TryParse<Page>(option.TargetId, ignoreCase: true, out var page))
                    CurrentPage = page;
                return;
            case SupportActionKind.Runbook:
            {
                var runbook = ResolveRunbook(option.TargetId);
                if (runbook is not null)
                    await RunRunbookAsync(runbook);
                return;
            }
            case SupportActionKind.Fix:
            {
                var fix = ResolveFix(option.TargetId);
                if (fix is null)
                    return;

                if (fix.HasSteps)
                {
                    StartWizard(fix);
                    return;
                }

                await RunFixAsync(fix);
                return;
            }
        }
    }

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

    // â”€â”€ Wizard â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ System Info â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
    private InstalledProgram? _selectedInstalledProgram;
    public InstalledProgram? SelectedInstalledProgram
    {
        get => _selectedInstalledProgram;
        set
        {
            _selectedInstalledProgram = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInstalledProgramDetailPaneOpen));
            OnPropertyChanged(nameof(SelectedInstalledProgramAssociationOverflowText));
            OnPropertyChanged(nameof(HasSelectedInstalledProgramAssociationOverflow));
        }
    }
    public bool IsInstalledProgramDetailPaneOpen => SelectedInstalledProgram is not null;
    public ObservableCollection<string> SelectedInstalledProgramAssociations { get; } = [];
    public bool HasSelectedInstalledProgramAssociations => SelectedInstalledProgramAssociations.Count > 0;
    public int SelectedInstalledProgramAssociationOverflowCount { get; private set; }
    public bool HasSelectedInstalledProgramAssociationOverflow => SelectedInstalledProgramAssociationOverflowCount > 0;
    public string SelectedInstalledProgramAssociationOverflowText =>
        SelectedInstalledProgramAssociationOverflowCount > 0
            ? $"and {SelectedInstalledProgramAssociationOverflowCount} more"
            : "";
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
    public ObservableCollection<BrowserExtensionSection> BrowserExtensionSections { get; } = [];
    public bool HasBrowserExtensionSections => BrowserExtensionSections.Count > 0;
    public bool ShowBrowserExtensionEmptyState => !HasBrowserExtensionSections;
    public ObservableCollection<string> BrowserAllowlistedSites { get; } = [];
    public bool HasBrowserAllowlistedSites => BrowserAllowlistedSites.Count > 0;
    public ObservableCollection<WorkResourceCheckCard> WorkFromHomeChecks { get; } = [];
    public bool HasWorkFromHomeChecks => WorkFromHomeChecks.Count > 0;
    public bool ShowWorkFromHomeChecksEmptyState => !HasWorkFromHomeChecks;
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
    public ObservableCollection<ToolboxEntry> FavoriteToolboxEntries { get; } = [];
    public ObservableCollection<ToolboxEntry> RecentToolboxEntries { get; } = [];
    public bool HasFavoriteToolboxEntries => FavoriteToolboxEntries.Count > 0;
    public bool HasRecentToolboxEntries => RecentToolboxEntries.Count > 0;
    public string ToolboxPinWarningMessage => _toolboxWorkspace.WarningMessage;
    public bool HasToolboxPinWarning => !string.IsNullOrWhiteSpace(ToolboxPinWarningMessage);
    public ObservableCollection<SupportCenterDefinition> SupportCenters { get; } = [];
    public bool HasSupportCenters => SupportCenters.Count > 0;
    public IReadOnlyList<SupportCenterDefinition> StorageSupportCenters => GetSupportCenters("storage-center");
    public IReadOnlyList<SupportCenterDefinition> StartupPerformanceSupportCenters => GetSupportCenters("startup-center", "software-center");
    public IReadOnlyList<SupportCenterDefinition> WindowsHealthSupportCenters => GetSupportCenters("windows-repair-center");
    public IReadOnlyList<SupportCenterDefinition> NetworkSupportCenters => GetSupportCenters("browser-center", "network-center", "files-center");
    public IReadOnlyList<SupportCenterDefinition> DevicesSupportCenters => GetSupportCenters("devices-center");
    public RecoveryDecisionTreeViewModel RecoveryDecisionTree { get; } = new();
    public ObservableCollection<MaintenanceProfileDefinition> MaintenanceProfiles { get; } = [];
    public bool HasMaintenanceProfiles => MaintenanceProfiles.Count > 0;
    public ObservableCollection<AutomationRuleSettings> AutomationRules { get; } = [];
    public ObservableCollection<AutomationRuleSettings> ScheduledAutomationRules { get; } = [];
    public ObservableCollection<AutomationRuleSettings> WatcherAutomationRules { get; } = [];
    public ObservableCollection<AutomationRunReceipt> AutomationHistoryEntries { get; } = [];
    public ObservableCollection<AutomationRunReceipt> FilteredAutomationHistoryEntries { get; } = [];
    public ObservableCollection<AutomationRunReceipt> RecentAutomationHistoryEntries { get; } = [];
    public ObservableCollection<AutomationAttentionItem> AutomationAttentionEntries { get; } = [];
    public bool HasVisibleBundles => Bundles.Count > 0;
    public string AdvancedAutomationAvailabilityText => _edition.Describe(ProductCapability.AdvancedAutomation).Summary;
    public bool HasAutomationRules => AutomationRules.Count > 0;
    public bool HasScheduledAutomationRules => ScheduledAutomationRules.Count > 0;
    public bool HasWatcherAutomationRules => WatcherAutomationRules.Count > 0;
    public bool HasAutomationHistoryEntries => AutomationHistoryEntries.Count > 0;
    public bool HasFilteredAutomationHistoryEntries => FilteredAutomationHistoryEntries.Count > 0;
    public bool HasRecentAutomationHistory => RecentAutomationHistoryEntries.Count > 0;
    public bool HasAutomationAttentionEntries => AutomationAttentionEntries.Count > 0;
    public IReadOnlyList<AutomationScheduleKind> AutomationScheduleModeOptions { get; } = Enum.GetValues<AutomationScheduleKind>();
    public IReadOnlyList<string> AutomationHistoryFilterOptions { get; } = ["All", "Attention Needed", "Failures", "Skipped", "Completed"];
    public string[] QuietHoursTimeOptions { get; } = Enumerable.Range(0, 24).Select(hour => $"{hour:D2}:00").ToArray();
    public IReadOnlyList<int> AutomationIntervalDayOptions { get; } = Enumerable.Range(1, 30).ToArray();
    public IReadOnlyList<int> AutomationStartupDelayOptions { get; } = Enumerable.Range(1, 60).ToArray();

    private string _automationHistoryFilter = "All";
    private bool _preferAutomationAttentionTab;
    private string _pendingAutomationReceiptInspectionId = "";
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
    public bool PreferAutomationAttentionTab
    {
        get => _preferAutomationAttentionTab;
        set
        {
            if (_preferAutomationAttentionTab == value) return;
            _preferAutomationAttentionTab = value;
            OnPropertyChanged();
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
    public int AutomationAttentionCount => AutomationAttentionEntries.Count;
    public bool HasAutomationAttention => AutomationAttentionCount > 0;
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
    public string AutomationAttentionSummaryText => HasAutomationAttentionEntries
        ? $"{AutomationAttentionCount} automation item{(AutomationAttentionCount == 1 ? "" : "s")} need review."
        : "All automations running smoothly.";

    // â”€â”€ History â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public ObservableCollection<RepairHistoryEntry> HistoryEntries { get; } = [];
    public ObservableCollection<RepairHistoryEntry> FilteredHistoryEntries { get; } = [];
    private readonly List<RepairHistoryEntry> _filteredHistoryCache = [];
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
    public int TotalFilteredHistoryEntryCount => _filteredHistoryCache.Count;
    public int SelectedHistoryEntryCount => HistoryEntries.Count(entry => entry.IsSelectedForComparison);
    public bool HasSelectedHistoryEntries => SelectedHistoryEntryCount > 0;
    public bool CanCompareSelectedHistoryEntries => SelectedHistoryEntryCount == 2;
    public bool AreAllVisibleHistoryEntriesSelected =>
        FilteredHistoryEntries.Count > 0 && FilteredHistoryEntries.All(entry => entry.IsSelectedForComparison);
    public bool IsHistoryComparePanelOpen => HistoryComparisonRows.Count > 0;
    public string HistorySummaryText =>
        HistoryEntries.Count == 0
            ? "No repairs, workflows, or automation runs have been recorded yet."
            : FilteredHistoryEntries.Count < _filteredHistoryCache.Count || HasHistorySearchText
                ? $"{FilteredHistoryEntries.Count} of {_filteredHistoryCache.Count} activity entr{(_filteredHistoryCache.Count == 1 ? "y" : "ies")} shown"
                : $"{HistoryEntries.Count} activity entr{(HistoryEntries.Count == 1 ? "y" : "ies")}";
    public string HistoryLoadedSummaryText =>
        _filteredHistoryCache.Count == 0
            ? "No receipts loaded."
            : $"Showing {FilteredHistoryEntries.Count} of {_filteredHistoryCache.Count} receipts";
    public string HistoryEmptyStateTitle => HasHistorySearchText
        ? "No matching activity"
        : "No activity yet";
    public string HistoryEmptyStateText => HasHistorySearchText
        ? "Try a broader search or clear it to see the full receipt history."
        : "Run a repair, workflow, or automation and FixFox will keep the receipt here so you can review what changed later.";
    public ObservableCollection<ReceiptComparisonRow> HistoryComparisonRows { get; } = [];
    public string HistoryCompareLeftTitle => _historyComparePair.Item1?.FixTitle ?? "";
    public string HistoryCompareRightTitle => _historyComparePair.Item2?.FixTitle ?? "";
    public bool IsDeviceHealthSystemOverviewExpanded => GetDeviceHealthSectionState("SystemOverview");
    public bool IsDeviceHealthStorageExpanded => GetDeviceHealthSectionState("Storage");
    public bool IsDeviceHealthStartupPerformanceExpanded => GetDeviceHealthSectionState("StartupPerformance");
    public bool IsDeviceHealthWindowsHealthExpanded => GetDeviceHealthSectionState("WindowsHealth");
    public bool IsDeviceHealthNetworkExpanded => GetDeviceHealthSectionState("Network");
    public bool IsDeviceHealthDevicesPeripheralsExpanded => GetDeviceHealthSectionState("DevicesPeripherals");
    public bool IsDeviceHealthSecurityExpanded => GetDeviceHealthSectionState("Security");

    // â”€â”€ Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
            ? $"{(string.IsNullOrWhiteSpace(Deployment.OrganizationName) ? "Managed deployment" : Deployment.OrganizationName)} â€¢ {Branding.ManagedModeLabel}"
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
    public PolicyState BehaviorProfilePolicyState => GetSettingPolicyState("BehaviorProfile");
    public PolicyState NotificationModePolicyState => GetSettingPolicyState("NotificationMode");
    public PolicyState SupportBundleExportLevelPolicyState => GetSettingPolicyState("SupportBundleExportLevel");
    public PolicyState LandingPagePolicyState => GetSettingPolicyState("LandingPage");
    public PolicyState AdvancedModePolicyState => GetSettingPolicyState("AdvancedMode");
    public PolicyState RunQuickScanOnLaunchPolicyState => GetSettingPolicyState("RunQuickScanOnLaunch");
    public PolicyState ShowNotificationsPolicyState => GetSettingPolicyState("ShowNotifications");
    public PolicyState CheckForUpdatesOnLaunchPolicyState => GetSettingPolicyState("CheckForUpdatesOnLaunch");
    public PolicyState PreferSafeMaintenanceDefaultsPolicyState => GetSettingPolicyState("PreferSafeMaintenanceDefaults");
    public PolicyState RunAtStartupPolicyState => GetSettingPolicyState("RunAtStartup");
    public PolicyState MinimizeToTrayPolicyState => GetSettingPolicyState("MinimizeToTray");
    public PolicyState ShowTrayBalloonsPolicyState => GetSettingPolicyState("ShowTrayBalloons");
    public bool CanEditBehaviorProfileSetting => CanEditBehaviorProfile && BehaviorProfilePolicyState != PolicyState.Locked;
    public bool CanEditNotificationModeSetting => CanEditNotificationMode && NotificationModePolicyState != PolicyState.Locked;
    public bool CanEditLandingPageSetting => CanEditLandingPage && LandingPagePolicyState != PolicyState.Locked;
    public bool CanEditSupportBundleDetailSetting => CanEditSupportBundleDetail && SupportBundleExportLevelPolicyState != PolicyState.Locked;
    public bool CanEditAdvancedModeSetting => CanToggleAdvancedMode && AdvancedModePolicyState != PolicyState.Locked;
    public bool CanEditRunAtStartupSetting => RunAtStartupPolicyState != PolicyState.Locked;
    public bool CanEditMinimizeToTraySetting => MinimizeToTrayPolicyState != PolicyState.Locked;
    public bool CanEditShowTrayBalloonsSetting => ShowTrayBalloonsPolicyState != PolicyState.Locked;
    public string LocalTimeZoneDisplayName => TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now)
        ? TimeZoneInfo.Local.DaylightName
        : TimeZoneInfo.Local.StandardName;

    public PolicyState GetSettingPolicyState(string settingKey)
        => _deployment.GetPolicyState(settingKey, _settings);

    public bool ShouldWarnManagedSetting(string settingKey)
        => GetSettingPolicyState(settingKey) == PolicyState.Managed;
    public string SelectedBehaviorProfile
    {
        get => string.IsNullOrWhiteSpace(_settings.BehaviorProfile) ? "Standard" : _settings.BehaviorProfile;
        set
        {
            if (!CanEditBehaviorProfileSetting)
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
            if (value && !CanEditAdvancedModeSetting)
                return;
            if (_settings.AdvancedMode == value) return;
            _settings.AdvancedMode = value;
            _deployment.ApplyPolicy(_settings);
            _settingsSvc.Save(_settings);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentProfileStatusText));
            OnPropertyChanged(nameof(ToolboxGroups));
            OnPropertyChanged(nameof(LocalTimeZoneDisplayName));
            RaisePolicyStateChanged();
            RefreshCapabilityScopedContent();
            RefreshCommandPalette();
        }
    }
    public string SelectedLandingPage
    {
        get => PageToLabel(ParseLandingPage(_settings.DefaultLandingPage));
        set
        {
            if (!CanEditLandingPageSetting)
                return;
            var page = LabelToPage(value);
            if (string.Equals(_settings.DefaultLandingPage, page.ToString(), StringComparison.OrdinalIgnoreCase))
                return;

            _settings.DefaultLandingPage = page.ToString();
            SaveSettingsLight();
            OnPropertyChanged();
        }
    }
    public string SelectedNotificationMode
    {
        get => string.IsNullOrWhiteSpace(_settings.NotificationMode) ? "Standard" : _settings.NotificationMode;
        set
        {
            if (!CanEditNotificationModeSetting)
                return;
            if (string.Equals(_settings.NotificationMode, value, StringComparison.OrdinalIgnoreCase))
                return;

            _settings.NotificationMode = value;
            SaveSettingsLight();
            OnPropertyChanged();
        }
    }
    public string SelectedSupportBundleExportLevel
    {
        get => string.IsNullOrWhiteSpace(_settings.SupportBundleExportLevel) ? "Basic" : _settings.SupportBundleExportLevel;
        set
        {
            if (!CanEditSupportBundleDetailSetting)
                return;
            if (!SupportBundleExportLevelOptions.Contains(value, StringComparer.OrdinalIgnoreCase))
                value = "Basic";
            if (string.Equals(_settings.SupportBundleExportLevel, value, StringComparison.OrdinalIgnoreCase))
                return;

            _settings.SupportBundleExportLevel = value;
            _deployment.ApplyPolicy(_settings);
            SaveSettingsLight();
            OnPropertyChanged();
        }
    }
    public string SelectedAutomationQuietHoursStart
    {
        get => string.IsNullOrWhiteSpace(_settings.AutomationQuietHoursStart) ? "22:00" : _settings.AutomationQuietHoursStart;
        set
        {
            _settings.AutomationQuietHoursStart = value;
            SaveSettingsLight(refreshAutomationState: true);
            OnPropertyChanged();
        }
    }
    public string SelectedAutomationQuietHoursEnd
    {
        get => string.IsNullOrWhiteSpace(_settings.AutomationQuietHoursEnd) ? "07:00" : _settings.AutomationQuietHoursEnd;
        set
        {
            _settings.AutomationQuietHoursEnd = value;
            SaveSettingsLight(refreshAutomationState: true);
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
    public string KeyboardShortcutsPath => Path.Combine(AppContext.BaseDirectory, "Docs", "Keyboard-Shortcuts.md");
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
    public bool HasActiveWork => ScanRunning || IsBundleRunning || IsRunbookRunning || !string.IsNullOrWhiteSpace(ActiveAutomationRuleTitle) || (WizardVisible && WizardFix is not null);
    public string ActiveWorkSummary =>
        ScanRunning ? "A health scan is still running." :
        IsBundleRunning ? $"\"{RunningBundle?.Title}\" is still running." :
        IsRunbookRunning ? $"\"{RunningRunbookTitle}\" is still running." :
        !string.IsNullOrWhiteSpace(ActiveAutomationRuleTitle) ? $"\"{ActiveAutomationRuleTitle}\" is still running." :
        WizardVisible && WizardFix is not null ? $"\"{WizardFix.Title}\" is still waiting on the next guided step." :
        "";
    public bool HasDashboardActiveOperation => HasActiveWork;
    public string DashboardActiveRunLabel =>
        ScanRunning ? "Running quick health scan" :
        IsBundleRunning ? RunningBundle?.Title ?? "Running maintenance workflow" :
        IsRunbookRunning ? RunningRunbookTitle :
        !string.IsNullOrWhiteSpace(ActiveAutomationRuleTitle) ? ActiveAutomationRuleTitle :
        WizardVisible && WizardFix is not null ? WizardFix.Title :
        "";

    public int  UnreadNotifCount => _notifs.UnreadCount;
    public IReadOnlyList<AppNotification> NotificationEntries => _notifs.All.ToList();
    public bool HasNotifications => _notifs.All.Count > 0;
    public string NotificationSummaryText =>
        _notifs.All.Count == 0
            ? "No active alerts in this session."
            : $"{UnreadNotifCount} alert{(UnreadNotifCount == 1 ? "" : "s")} still need review";

    // â”€â”€ Command Palette â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
    private SupportBundlePreset _selectedSupportBundlePreset = SupportBundlePreset.Standard;
    private int? _bundleProgressPercent;
    private bool _bundleProgressIndeterminate;
    private string _bundleStatusMessage = "";
    private bool _showBundleStatusBanner;
    private string _bundleStatusFolderPath = "";
    private (RepairHistoryEntry? Item1, RepairHistoryEntry? Item2) _historyComparePair;
    private CancellationTokenSource? _bundleStatusHideCts;
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
    public SupportBundlePreset SelectedSupportBundlePreset
    {
        get => _selectedSupportBundlePreset;
        set
        {
            if (_selectedSupportBundlePreset == value)
                return;

            _selectedSupportBundlePreset = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsQuickBundlePresetSelected));
            OnPropertyChanged(nameof(IsStandardBundlePresetSelected));
            OnPropertyChanged(nameof(IsTechnicianBundlePresetSelected));
        }
    }
    public bool IsQuickBundlePresetSelected => SelectedSupportBundlePreset == SupportBundlePreset.Quick;
    public bool IsStandardBundlePresetSelected => SelectedSupportBundlePreset == SupportBundlePreset.Standard;
    public bool IsTechnicianBundlePresetSelected => SelectedSupportBundlePreset == SupportBundlePreset.Technician;
    public int? BundleProgressPercent
    {
        get => _bundleProgressPercent;
        set
        {
            _bundleProgressPercent = value;
            OnPropertyChanged();
        }
    }
    public bool BundleProgressIndeterminate
    {
        get => _bundleProgressIndeterminate;
        set
        {
            _bundleProgressIndeterminate = value;
            OnPropertyChanged();
        }
    }
    public string BundleStatusMessage
    {
        get => _bundleStatusMessage;
        set
        {
            _bundleStatusMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasBundleStatusMessage));
        }
    }
    public bool ShowBundleStatusBanner
    {
        get => _showBundleStatusBanner;
        set
        {
            _showBundleStatusBanner = value;
            OnPropertyChanged();
        }
    }
    public bool HasBundleStatusMessage => !string.IsNullOrWhiteSpace(BundleStatusMessage);
    public string BundleStatusFolderPath
    {
        get => _bundleStatusFolderPath;
        set
        {
            _bundleStatusFolderPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasBundleStatusFolderPath));
        }
    }
    public bool HasBundleStatusFolderPath => !string.IsNullOrWhiteSpace(BundleStatusFolderPath);

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
    private int _selectedCommandPaletteIndex = -1;
    private bool _isKeyboardShortcutsDialogOpen;

    public ObservableCollection<string> GlobalSearchRecentQueries { get; } = [];
    public ObservableCollection<KeyboardShortcutEntry> KeyboardShortcuts { get; } = [];
    public bool HasGlobalSearchRecentQueries => GlobalSearchRecentQueries.Count > 0;
    public bool ShowGlobalSearchRecentQueries => IsCommandPaletteOpen && string.IsNullOrWhiteSpace(CommandPaletteQuery) && HasGlobalSearchRecentQueries;
    public bool HasKeyboardShortcuts => KeyboardShortcuts.Count > 0;
    public bool IsKeyboardShortcutsDialogOpen
    {
        get => _isKeyboardShortcutsDialogOpen;
        set
        {
            if (_isKeyboardShortcutsDialogOpen == value)
                return;

            _isKeyboardShortcutsDialogOpen = value;
            OnPropertyChanged();
        }
    }

    public bool IsCommandPaletteOpen
    {
        get => _commandPaletteOpen;
        set
        {
            if (_commandPaletteOpen == value)
                return;

            _commandPaletteOpen = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowGlobalSearchRecentQueries));
        }
    }

    public string CommandPaletteQuery
    {
        get => _commandPaletteQuery;
        set
        {
            if (string.Equals(_commandPaletteQuery, value, StringComparison.Ordinal))
                return;

            _commandPaletteQuery = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowGlobalSearchRecentQueries));
            RefreshCommandPalette();
        }
    }

    public ObservableCollection<CommandPaletteItem> CommandPaletteResults { get; } = [];
    public CommandPaletteItem? SelectedCommandPaletteItem =>
        _selectedCommandPaletteIndex >= 0 && _selectedCommandPaletteIndex < CommandPaletteResults.Count
            ? CommandPaletteResults[_selectedCommandPaletteIndex]
            : null;

    public void RefreshCommandPalette()
    {
        _commandPaletteDirty = true;

        if (!IsCommandPaletteOpen && string.IsNullOrWhiteSpace(_commandPaletteQuery))
            return;

        if (string.IsNullOrWhiteSpace(_commandPaletteQuery))
        {
            _commandPaletteRefreshTimer.Stop();
            RefreshCommandPaletteCore();
            return;
        }

        _commandPaletteRefreshTimer.Stop();
        _commandPaletteRefreshTimer.Start();
    }

    private void RefreshCommandPaletteCore()
    {
        CommandPaletteResults.Clear();
        var results = _commandPaletteService.Search(_commandPaletteQuery, new CommandPaletteSearchContext
        {
            PinnedFixes = PinnedFixes.ToList(),
            FavoriteFixes = FavoriteFixes.ToList(),
            RecentFixes = RecentlyRunFixes.ToList(),
            Runbooks = Runbooks.ToList(),
            MaintenanceProfiles = MaintenanceProfiles.ToList(),
            SupportCenters = SupportCenters.ToList(),
            ToolboxGroups = ToolboxGroups.ToList(),
            RecentReceipts = HistoryEntries.Take(20).ToList(),
            AutomationRules = AutomationRules.ToList(),
            AdditionalItems = BuildAdditionalCommandPaletteItems(),
            ExcludeAdvancedFixes = SimplifiedModeEnabled
        }).ToList();

        foreach (var r in results
                     .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First())
                     .Take(16))
            CommandPaletteResults.Add(r);

        EnsureSelectedCommandPaletteItem();
        _commandPaletteDirty = false;
        OnPropertyChanged(nameof(HasCommandPaletteResults));
        OnPropertyChanged(nameof(SelectedCommandPaletteItem));
    }

    private IReadOnlyList<CommandPaletteItem> BuildAdditionalCommandPaletteItems()
    {
        var items = new List<CommandPaletteItem>
        {
            new()
            {
                Id = "action:automation-open-center",
                Title = "Automation Center",
                Subtitle = "Open schedules, watchers, and automation history.",
                ResultTypeLabel = "Page",
                Section = "Page",
                Hint = "Open page",
                Glyph = "\uE8B1",
                SearchText = "automation center schedules watchers history",
                Kind = CommandPaletteItemKind.Page,
                TargetPage = Page.Bundles,
                SearchTags = ["automation", "maintenance"],
                TooltipText = "Open schedules, watchers, and automation history."
            },
            new()
            {
                Id = "action:automation-run-quick-health",
                Title = "Run Quick Health Scan",
                Subtitle = "Run the low-noise health check now.",
                ResultTypeLabel = "Action",
                Section = "Action · Automation",
                Hint = "Run now",
                Glyph = "\uE9D2",
                SearchText = "automation quick health scan run",
                Kind = CommandPaletteItemKind.Action,
                TargetId = "run-automation:quick-health-check",
                SearchTags = ["automation", "health"],
                TooltipText = "Run the low-noise health check now."
            },
            new()
            {
                Id = "action:automation-run-safe-maintenance",
                Title = "Run Safe Maintenance Now",
                Subtitle = "Run the conservative maintenance workflow now.",
                ResultTypeLabel = "Action",
                Section = "Action · Automation",
                Hint = "Run now",
                Glyph = "\uE768",
                SearchText = "automation maintenance safe run now",
                Kind = CommandPaletteItemKind.Action,
                TargetId = "run-automation:safe-maintenance",
                SearchTags = ["automation", "maintenance"],
                TooltipText = "Run the conservative maintenance workflow now."
            },
            new()
            {
                Id = "action:automation-toggle-pause",
                Title = AutomationPaused ? "Resume Automation" : "Pause Automation For 1 Hour",
                Subtitle = AutomationPaused ? "Resume scheduled tasks and watchers." : "Pause automated runs for the next hour.",
                ResultTypeLabel = "Action",
                Section = "Action · Automation",
                Hint = AutomationPaused ? "Resume" : "Pause",
                Glyph = AutomationPaused ? "\uE768" : "\uE769",
                SearchText = "automation pause resume quiet",
                Kind = CommandPaletteItemKind.Action,
                TargetId = AutomationPaused ? "automation:resume" : "automation:pause-hour",
                SearchTags = ["automation", "pause"],
                TooltipText = AutomationPaused ? "Resume scheduled tasks and watchers." : "Pause automated runs for the next hour."
            }
        };

        items.AddRange(BuildSettingsCommandPaletteItems());

        if (CanResumeInterruptedRepair)
        {
            items.Add(new CommandPaletteItem
            {
                Id = "action:automation-resume-interrupted",
                Title = "Resume Interrupted Repair",
                Subtitle = "Continue the last guided repair that can still be resumed.",
                ResultTypeLabel = "Action",
                Section = "Action · Automation",
                Hint = "Resume",
                Glyph = "\uE72A",
                SearchText = "automation interrupted repair resume",
                Kind = CommandPaletteItemKind.Action,
                TargetId = "automation:resume-interrupted",
                SearchTags = ["resume", "automation", "repair"],
                TooltipText = "Continue the last guided repair that can still be resumed."
            });
        }

        return items;
    }

    private IEnumerable<CommandPaletteItem> BuildSettingsCommandPaletteItems()
    {
        yield return BuildSettingPaletteItem("theme", "Theme", "Change the FixFox light or dark appearance.", "appearance dark light theme color");
        yield return BuildSettingPaletteItem("advanced-mode", "Advanced Mode", "Show deeper technical detail for support and troubleshooting.", "advanced mode technical details");
        yield return BuildSettingPaletteItem("notifications", "Notifications", "Control how FixFox surfaces alerts and reminders.", "notifications alerts quiet");
        yield return BuildSettingPaletteItem("run-at-startup", "Run At Startup", "Choose whether FixFox opens automatically with Windows.", "startup autorun launch");
        yield return BuildSettingPaletteItem("support-packages", "Support Package Detail", "Choose how much detail FixFox includes in support packages.", "support package export technician basic");
        yield return BuildSettingPaletteItem("landing-page", "Startup Landing Page", "Choose which workspace FixFox opens first.", "landing page home startup");
        yield return BuildSettingPaletteItem("minimize-to-tray", "Minimize To Tray", "Keep FixFox available from the system tray.", "tray minimize background");
    }

    private static CommandPaletteItem BuildSettingPaletteItem(string key, string title, string subtitle, string searchText) => new()
    {
        Id = $"setting:{key}",
        Title = title,
        Subtitle = subtitle,
        ResultTypeLabel = "Setting",
        Section = "Setting",
        Hint = "Open settings",
        Glyph = "\uE713",
        SearchText = searchText,
        SearchTags = ["settings", key.Replace('-', ' ')],
        Kind = CommandPaletteItemKind.Setting,
        TargetId = key,
        TooltipText = subtitle
    };

    private void EnsureSelectedCommandPaletteItem()
    {
        if (CommandPaletteResults.Count == 0)
        {
            SetCommandPaletteSelection(-1);
            return;
        }

        var newIndex = _selectedCommandPaletteIndex;
        if (newIndex < 0 || newIndex >= CommandPaletteResults.Count || CommandPaletteResults[newIndex].IsGroupHeader)
            newIndex = CommandPaletteResults.IndexOf(CommandPaletteResults.First(item => !item.IsGroupHeader));

        SetCommandPaletteSelection(newIndex);
    }

    private void SetCommandPaletteSelection(int index)
    {
        for (var i = 0; i < CommandPaletteResults.Count; i++)
            CommandPaletteResults[i].IsSelected = i == index;

        _selectedCommandPaletteIndex = index;
        OnPropertyChanged(nameof(SelectedCommandPaletteItem));
    }

    public void MoveCommandPaletteSelection(int delta)
    {
        if (CommandPaletteResults.Count == 0)
            return;

        var index = _selectedCommandPaletteIndex;
        do
        {
            index = (index + delta + CommandPaletteResults.Count) % CommandPaletteResults.Count;
        }
        while (CommandPaletteResults[index].IsGroupHeader);

        SetCommandPaletteSelection(index);
    }

    public void MoveCommandPaletteGroup(int delta)
    {
        var groupHeaders = CommandPaletteResults
            .Select((item, index) => (item, index))
            .Where(pair => pair.item.IsGroupHeader)
            .ToList();

        if (groupHeaders.Count == 0)
        {
            MoveCommandPaletteSelection(delta >= 0 ? 1 : -1);
            return;
        }

        var currentGroupIndex = groupHeaders.FindIndex(pair => pair.index >= _selectedCommandPaletteIndex);
        if (currentGroupIndex < 0)
            currentGroupIndex = 0;

        var nextGroup = groupHeaders[(currentGroupIndex + delta + groupHeaders.Count) % groupHeaders.Count].index + 1;
        if (nextGroup >= CommandPaletteResults.Count)
            return;

        SetCommandPaletteSelection(nextGroup);
    }

    public async Task ActivateSelectedCommandPaletteItemAsync()
    {
        if (SelectedCommandPaletteItem is null || SelectedCommandPaletteItem.IsGroupHeader)
            return;

        await ExecuteCommandPaletteItemAsync(SelectedCommandPaletteItem);
    }

    public void RememberGlobalSearchQuery()
    {
        var query = CommandPaletteQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return;

        var existing = GlobalSearchRecentQueries.FirstOrDefault(item => string.Equals(item, query, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            GlobalSearchRecentQueries.Remove(existing);

        GlobalSearchRecentQueries.Insert(0, query);
        while (GlobalSearchRecentQueries.Count > 5)
            GlobalSearchRecentQueries.RemoveAt(GlobalSearchRecentQueries.Count - 1);

        OnPropertyChanged(nameof(HasGlobalSearchRecentQueries));
        OnPropertyChanged(nameof(ShowGlobalSearchRecentQueries));
    }

    public void UseGlobalSearchRecentQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        CommandPaletteQuery = query;
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
        RefreshVisibleToolboxSections();
        OnPropertyChanged(nameof(HasMaintenanceProfiles));
        OnPropertyChanged(nameof(HasVisibleBundles));
        RefreshAutomationWorkspace();
    }

    private void InitializeToolboxWorkspace()
    {
        _toolboxWorkspace.RegisterEntries(_toolbox.Groups.SelectMany(group => group.Entries));
        _toolboxWorkspace.RestorePinned(_settings.PinnedToolKeys);
        RefreshVisibleToolboxSections();
    }

    private void RefreshVisibleToolboxSections()
    {
        FavoriteToolboxEntries.Clear();
        foreach (var entry in _toolboxWorkspace.Favorites.Where(CanAccessToolboxEntry))
            FavoriteToolboxEntries.Add(entry);

        RecentToolboxEntries.Clear();
        foreach (var entry in _toolboxWorkspace.Recent.Where(CanAccessToolboxEntry))
            RecentToolboxEntries.Add(entry);

        OnPropertyChanged(nameof(HasFavoriteToolboxEntries));
        OnPropertyChanged(nameof(HasRecentToolboxEntries));
        OnPropertyChanged(nameof(ToolboxPinWarningMessage));
        OnPropertyChanged(nameof(HasToolboxPinWarning));
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
        _commandPaletteRefreshTimer.Stop();
        _commandPaletteQuery = "";
        OnPropertyChanged(nameof(CommandPaletteQuery));
        IsCommandPaletteOpen = true;
        OnPropertyChanged(nameof(ShowGlobalSearchRecentQueries));
        if (_commandPaletteDirty || CommandPaletteResults.Count == 0)
            RefreshCommandPaletteCore();
        else
            EnsureSelectedCommandPaletteItem();
    }

    public void OpenKeyboardShortcutsDialog() => IsKeyboardShortcutsDialogOpen = true;

    public void CloseKeyboardShortcutsDialog() => IsKeyboardShortcutsDialogOpen = false;

    public void PrimeCommandPaletteCache()
    {
        if (_commandPaletteDirty && !IsCommandPaletteOpen && string.IsNullOrWhiteSpace(_commandPaletteQuery))
            RefreshCommandPaletteCore();
    }

    public void CloseCommandPalette()
    {
        _commandPaletteRefreshTimer.Stop();
        IsCommandPaletteOpen = false;
        SetCommandPaletteSelection(-1);
    }

    // â”€â”€ Status bar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ Privacy notice â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private bool _showPrivacyNotice;
    public bool ShowPrivacyNotice
    {
        get => _showPrivacyNotice;
        set
        {
            _showPrivacyNotice = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOnboardingModeSelectionVisible));
            OnPropertyChanged(nameof(IsSimpleOnboardingVisible));
            OnPropertyChanged(nameof(IsFullOnboardingVisible));
        }
    }

    public void DismissPrivacyNotice()
    {
        ShowPrivacyNotice = false;
        _settings.PrivacyNoticeDismissed = true;
        _settings.OnboardingDismissed = true;
        _settings.FirstRunExperienceMode = string.IsNullOrWhiteSpace(_settings.FirstRunExperienceMode)
            ? "Full"
            : _settings.FirstRunExperienceMode;
        _settingsSvc.Save(_settings);
    }

    public void ChooseFirstRunExperience(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return;

        _settings.FirstRunExperienceMode = mode;
        if (string.Equals(mode, "Simple", StringComparison.OrdinalIgnoreCase))
            SimplifiedModeEnabled = true;

        SimplifiedOnboardingStep = 1;
        _settingsSvc.Save(_settings);
        OnPropertyChanged(nameof(IsOnboardingModeSelectionVisible));
        OnPropertyChanged(nameof(IsSimpleOnboardingVisible));
        OnPropertyChanged(nameof(IsFullOnboardingVisible));
    }

    public void AdvanceSimplifiedOnboarding()
    {
        if (SimplifiedOnboardingStep < 3)
        {
            SimplifiedOnboardingStep++;
            return;
        }

        _ = CompleteOnboardingAsync();
    }

    public async Task CompleteOnboardingAsync()
    {
        _settings.RunFirstHealthCheckAfterSetup = RunHealthCheckAfterSetup;
        DismissPrivacyNotice();
        if (RunHealthCheckAfterSetup)
            await RunQuickScanAsync();
    }

    private void InitializeOnboardingChecklist()
    {
        if (OnboardingChecklistItems.Count == 0)
        {
            OnboardingChecklistItems.Add(new OnboardingChecklistItem
            {
                Key = "health-scan",
                Title = "Run your first health scan",
                Description = "Start with a safe quick health scan so FixFox can surface the first useful actions.",
                AutomationId = "Onboarding_Item_RunYourFirstHealthScan_Status"
            });
            OnboardingChecklistItems.Add(new OnboardingChecklistItem
            {
                Key = "startup-items",
                Title = "Review your startup items",
                Description = "Open Device Health and review which startup items are worth trimming.",
                AutomationId = "Onboarding_Item_ReviewYourStartupItems_Status"
            });
            OnboardingChecklistItems.Add(new OnboardingChecklistItem
            {
                Key = "automation-schedule",
                Title = "Set your automation schedule",
                Description = "Choose how often FixFox should run its safe scheduled checks.",
                AutomationId = "Onboarding_Item_SetYourAutomationSchedule_Status"
            });
            OnboardingChecklistItems.Add(new OnboardingChecklistItem
            {
                Key = "support-package",
                Title = "Create your first support package",
                Description = "Build one clean local support package so escalation is ready when you need it.",
                AutomationId = "Onboarding_Item_CreateYourFirstSupportPackage_Status"
            });
        }

        SyncOnboardingChecklistState();
    }

    private void InitializeSimplifiedProblemOptions()
    {
        SimplifiedProblemOptions.Clear();
        foreach (var option in BuildSimplifiedProblemOptions())
            SimplifiedProblemOptions.Add(option);

        OnPropertyChanged(nameof(PrimarySimplifiedProblemOptions));
        OnPropertyChanged(nameof(SimplifiedOtherProblemOption));
    }

    public static IReadOnlyList<SimplifiedProblemOption> BuildSimplifiedProblemOptions() =>
    [
        CreateProblemOption("sound", "Sound isn't working", "Fix speakers, headphones, or missing sound.", "\uE767", SupportActionKind.Fix, "restart-audio-service"),
        CreateProblemOption("printer", "Printer isn't working", "Check the print queue and common printer problems.", "\uE749", SupportActionKind.Runbook, "printing-rescue-runbook"),
        CreateProblemOption("slow", "My PC is slow", "Tackle startup pressure and common cleanup problems.", "\uE823", SupportActionKind.Runbook, "slow-pc-runbook", FixRiskLevel.MayRestart),
        CreateProblemOption("internet", "Internet isn't connecting", "Check the network path and restore internet access.", "\uE701", SupportActionKind.Runbook, "internet-recovery-runbook"),
        CreateProblemOption("crash", "PC keeps crashing", "Collect the main Windows repair steps for crashes and instability.", "\uEDE1", SupportActionKind.Runbook, "windows-repair-runbook", FixRiskLevel.MayRestart),
        CreateProblemOption("display", "Display looks wrong", "Open the most useful display repair path for blurry, flickering, or wrong-size screens.", "\uE7F4", SupportActionKind.Fix, "fix-display-scaling"),
        CreateProblemOption("signin", "I can't sign in", "Check the most common account and password blockers safely.", "\uE77B", SupportActionKind.Fix, "detect-expired-passwords-and-disabled-accounts"),
        CreateProblemOption("email", "Email isn't working", "Ask a few questions first so FixFox can choose the safest email repair path.", "\uE715", SupportActionKind.Page, Page.SymptomChecker.ToString()),
        CreateProblemOption("storage", "Running out of space", "Free safe clutter and review storage pressure.", "\uE7F8", SupportActionKind.Runbook, "disk-full-rescue-runbook"),
        CreateProblemOption("update", "Windows won't update", "Open the Windows repair workflow for stuck updates.", "\uE895", SupportActionKind.Runbook, "windows-repair-runbook", FixRiskLevel.MayRestart),
        CreateProblemOption("other", "Something else", "Search for a fix, page, or recent action.", "\uE897", SupportActionKind.GlobalSearch, "global-search")
    ];

    private static SimplifiedProblemOption CreateProblemOption(
        string key,
        string title,
        string description,
        string glyph,
        SupportActionKind actionKind,
        string targetId,
        FixRiskLevel? riskLevel = FixRiskLevel.Safe,
        bool requiresAdmin = false) => new()
    {
        Key = key,
        Title = title,
        Description = description,
        Glyph = glyph,
        ActionKind = actionKind,
        TargetId = targetId,
        AutomationId = $"FixMyPC_Problem_{char.ToUpperInvariant(key[0])}{key[1..]}",
        RiskLevel = riskLevel,
        RequiresAdmin = requiresAdmin
    };

    private void InitializeKeyboardShortcuts()
    {
        if (KeyboardShortcuts.Count > 0)
            return;

        AddShortcut("Global", "Ctrl+K / Ctrl+Space", "Open global search");
        AddShortcut("Global", "Ctrl+H", "Open Activity");
        AddShortcut("Global", "Ctrl+F", "Open Repair Library");
        AddShortcut("Global", "Ctrl+D", "Open Home");
        AddShortcut("Global", "Ctrl+,", "Open Settings");
        AddShortcut("Global", "Ctrl+B", "Open Support Package");
        AddShortcut("Global", "Ctrl+Shift+R", "Run the last useful action again");
        AddShortcut("Global", "F5", "Refresh the current page");
        AddShortcut("Global", "F1", "Open help for the current page");
        AddShortcut("Global", "Alt+Left", "Go back to the previous page");
        AddShortcut("Global", "?", "Open the keyboard shortcuts reference");
        AddShortcut("Fix Center", "/", "Focus the repair search box");
        AddShortcut("Fix Center", "Enter", "Run the focused repair card");
        AddShortcut("Fix Center", "Space", "Expand or collapse the focused repair card");
        AddShortcut("History", "Ctrl+A", "Select all visible receipts");
        AddShortcut("History", "Delete", "Delete the selected receipts");
        AddShortcut("History", "Ctrl+E", "Export the selected receipts");
        AddShortcut("Automation", "Ctrl+N", "Open the Automation page and focus the first rule");
        AddShortcut("Toolbox", "Enter", "Open the focused Windows tool");
    }

    private void AddShortcut(string context, string keys, string action)
        => KeyboardShortcuts.Add(new KeyboardShortcutEntry
        {
            Context = context,
            Keys = keys,
            Action = action
        });

    private void SyncOnboardingChecklistState()
    {
        foreach (var item in OnboardingChecklistItems)
        {
            item.IsCompleted = item.Key switch
            {
                "health-scan" => _settings.OnboardingCompletedHealthScan,
                "startup-items" => _settings.OnboardingReviewedStartupItems,
                "automation-schedule" => _settings.OnboardingConfiguredAutomation,
                "support-package" => _settings.OnboardingCreatedSupportPackage,
                _ => item.IsCompleted
            };
        }

        OnPropertyChanged(nameof(HasOnboardingChecklist));
    }

    public void SetOnboardingChecklistItemState(string key, bool completed, bool saveSettings = true)
    {
        var item = OnboardingChecklistItems.FirstOrDefault(entry =>
            string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
        if (item is null)
            return;

        item.IsCompleted = completed;
        switch (key)
        {
            case "health-scan":
                _settings.OnboardingCompletedHealthScan = completed;
                break;
            case "startup-items":
                _settings.OnboardingReviewedStartupItems = completed;
                break;
            case "automation-schedule":
                _settings.OnboardingConfiguredAutomation = completed;
                break;
            case "support-package":
                _settings.OnboardingCreatedSupportPackage = completed;
                break;
        }

        if (saveSettings)
            _settingsSvc.Save(_settings);

        OnPropertyChanged(nameof(HasOnboardingChecklist));
    }

    public void DismissOnboardingChecklist()
    {
        _settings.OnboardingChecklistDismissed = true;
        _settingsSvc.Save(_settings);
        OnPropertyChanged(nameof(HasOnboardingChecklist));
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
        RaisePolicyStateChanged();
        RefreshCapabilityScopedContent();
        SyncAutomationSchedules();
        RefreshAutomationWorkspace();
        RefreshActiveFixes();
        RefreshCommandPalette();
    }

    public Task PrimeDeferredWorkspaceStateAsync()
    {
        if (_deferredWorkspacePrimed)
            return Task.CompletedTask;

        _deferredWorkspacePrimed = true;
        RefreshHistory();
        RefreshRecentlyRun();
        RefreshSupportCenters();
        RefreshAutomationWorkspace();
        RefreshDashboardWorkspace();
        RefreshCommandPalette();
        return Task.CompletedTask;
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
        SyncOnboardingChecklistState();
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
        RaisePolicyStateChanged();
        RefreshCapabilityScopedContent();
        SyncAutomationSchedules();
        RefreshAutomationWorkspace();
        RefreshActiveFixes();
        RefreshCommandPalette();
    }

    // â”€â”€ Recent searches â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ Favorites â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ Pinned fixes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ Clock timer â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _automationHeartbeat;
    private readonly DispatcherTimer _commandPaletteRefreshTimer;
    private bool _commandPaletteDirty = true;
    private bool _deferredWorkspacePrimed;

    // â”€â”€ Constructor â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
        BrowserExtensionReviewService browserExtensions,
        WorkFromHomeDependencyService workFromHomeDependencies,
        SchedulerService        scheduler,
        IToolboxService toolbox,
        IMaintenanceProfileService maintenanceProfileService,
        ISupportCenterService supportCenterService,
        ICommandPaletteService commandPaletteService,
        ITextSubstitutionService textSubstitutionService,
        IDashboardWorkspaceService dashboardWorkspace,
        IDashboardSuggestionSignalService dashboardSuggestionSignals,
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
        _browserExtensions = browserExtensions;
        _workFromHomeDependencies = workFromHomeDependencies;
        _scheduler         = scheduler;
        _toolbox = toolbox;
        _maintenanceProfileService = maintenanceProfileService;
        _supportCenterService = supportCenterService;
        _commandPaletteService = commandPaletteService;
        _dashboardWorkspace = dashboardWorkspace;
        _dashboardSuggestionSignals = dashboardSuggestionSignals;
        _automationHistory = automationHistory;
        _automationCoordinator = automationCoordinator;

        _settings = settingsSvc.Load();
        _deployment.ApplyPolicy(_settings);
        _textSubstitutions = textSubstitutionService;
        _textSubstitutions.SetSimplifiedMode(_settings.SimplifiedMode);
        _isSidebarCollapsed = _settings.SidebarCollapsed;
        RunHealthCheckAfterSetup = _settings.RunFirstHealthCheckAfterSetup;
        InitializeOnboardingChecklist();
        InitializeSimplifiedProblemOptions();
        InitializeKeyboardShortcuts();

        foreach (var c in _catalog.Categories) Categories.Add(c);
        HydrateFixLibraryPresentationFields();
        _allBundles.AddRange(_catalog.Bundles);
        _allRunbooks.AddRange(_runbookCatalog.Runbooks);
        _allMaintenanceProfiles.AddRange(_maintenanceProfileService.Profiles);
        _automationCoordinator.EnsureRules(_settings);
        NormalizeAutomationRules();
        SyncAutomationSchedules();
        RefreshCapabilityScopedContent();
        InitializeToolboxWorkspace();

        // Restore last selected category
        if (!string.IsNullOrEmpty(_settings.LastFixCategory))
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == _settings.LastFixCategory)
                               ?? Categories.FirstOrDefault();
        else
            SelectedCategory = null;

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
        foreach (var site in _settings.BrowserAllowlistedSites.Where(site => !string.IsNullOrWhiteSpace(site)))
            BrowserAllowlistedSites.Add(site);

        SelectedWeeklyTuneUpDay  = _settings.WeeklyTuneUpDay;
        SelectedWeeklyTuneUpTime = _settings.WeeklyTuneUpTime;
        RefreshWeeklyTuneUpSchedule();

        RefreshBreadcrumb();
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

        _commandPaletteRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(140)
        };
        _commandPaletteRefreshTimer.Tick += (_, _) =>
        {
            _commandPaletteRefreshTimer.Stop();
            RefreshCommandPaletteCore();
        };

        _ = LoadUpdateInfoAsync();
    }

    // â”€â”€ Quick Scan â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
        SetOnboardingChecklistItemState("health-scan", true);
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

    // â”€â”€ Fix execution â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public async Task RunFixAsync(FixItem fix)
    {
        if (SimplifiedModeEnabled && FixConfirmationRequestAsync is not null)
        {
            var decision = await FixConfirmationRequestAsync(fix);
            if (decision == SimplifiedConfirmationDecision.Cancel)
                return;

            if (decision == SimplifiedConfirmationDecision.GetHelpInstead)
            {
                CurrentPage = Page.Handoff;
                return;
            }
        }

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

    // â”€â”€ Bundle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
        if (RunbookPreflightRequestAsync is not null)
        {
            var shouldContinue = await RunbookPreflightRequestAsync(runbook);
            if (!shouldContinue)
                return;
        }

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
            if (LastRunbookSummary is not null && RunbookPostResultRequestAsync is not null)
                await RunbookPostResultRequestAsync(runbook, LastRunbookSummary);
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

    public void SelectSupportBundlePreset(SupportBundlePreset preset)
        => SelectedSupportBundlePreset = preset;

    public void DismissBundleStatusBanner()
    {
        _bundleStatusHideCts?.Cancel();
        ShowBundleStatusBanner = false;
        BundleStatusMessage = "";
        BundleStatusFolderPath = "";
        BundleProgressPercent = null;
        BundleProgressIndeterminate = false;
    }

    public async Task CreateEvidenceBundleAsync()
    {
        try
        {
            _bundleStatusHideCts?.Cancel();
            ShowBundleStatusBanner = true;
            BundleProgressIndeterminate = true;
            BundleProgressPercent = null;
            BundleStatusFolderPath = "";

            var options = EvidenceExportOptions.CreateForPreset(
                SelectedSupportBundlePreset,
                CanUseTechnicianExports);

            BundleStatusMessage = SelectedSupportBundlePreset switch
            {
                SupportBundlePreset.Quick => "Building Quick bundle...",
                SupportBundlePreset.Technician => "Building Technician bundle...",
                _ => "Building Standard bundle..."
            };

            var progress = new Progress<EvidenceBundleProgressUpdate>(update =>
            {
                BundleStatusMessage = update.StatusMessage;
                BundleProgressPercent = update.Percent;
                BundleProgressIndeterminate = !update.Percent.HasValue;
            });

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
                options,
                progress);

            SetOnboardingChecklistItemState("support-package", true);
            BundleProgressIndeterminate = false;
            BundleProgressPercent = 100;
            BundleStatusFolderPath = LastEvidenceBundle.BundleFolder;
            BundleStatusMessage = $"Done - bundle saved to {LastEvidenceBundle.BundleFolder}";

            var hideCts = new CancellationTokenSource();
            _bundleStatusHideCts = hideCts;
            _ = AutoHideBundleStatusAsync(hideCts.Token);
        }
        catch (Exception ex)
        {
            _logger.Error("Evidence bundle export failed", ex);
            BundleProgressIndeterminate = false;
            BundleProgressPercent = null;
            ShowBundleStatusBanner = true;
            BundleStatusMessage = $"Support package failed: {ex.Message}";
            throw;
        }
    }

    // â”€â”€ Wizard â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ System Info â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
            if (StartupApps.Count > 0)
                SetOnboardingChecklistItemState("startup-items", true);
            OnPropertyChanged(nameof(HasStartupApps));
            OnPropertyChanged(nameof(ShowStartupAppsEmptyState));
            OnPropertyChanged(nameof(StartupAppsSummaryText));
        }
    }

    public Task LoadBrowserExtensionSectionsAsync()
    {
        BrowserExtensionSections.Clear();
        foreach (var section in _browserExtensions.GetSections())
            BrowserExtensionSections.Add(section);

        OnPropertyChanged(nameof(HasBrowserExtensionSections));
        OnPropertyChanged(nameof(ShowBrowserExtensionEmptyState));
        return Task.CompletedTask;
    }

    public async Task LoadWorkFromHomeChecksAsync()
    {
        WorkFromHomeChecks.Clear();
        foreach (var item in await _workFromHomeDependencies.BuildChecksAsync())
            WorkFromHomeChecks.Add(item);

        OnPropertyChanged(nameof(HasWorkFromHomeChecks));
        OnPropertyChanged(nameof(ShowWorkFromHomeChecksEmptyState));
    }

    public async Task OpenInstalledProgramDetailAsync(InstalledProgram program)
    {
        SelectedInstalledProgram = program;
        SelectedInstalledProgramAssociations.Clear();
        SelectedInstalledProgramAssociationOverflowCount = 0;

        var associations = await _installedPrograms.GetDefaultAssociationsAsync(program);
        foreach (var association in associations.Take(5))
            SelectedInstalledProgramAssociations.Add(association);

        SelectedInstalledProgramAssociationOverflowCount = Math.Max(0, associations.Count - SelectedInstalledProgramAssociations.Count);
        OnPropertyChanged(nameof(HasSelectedInstalledProgramAssociations));
        OnPropertyChanged(nameof(SelectedInstalledProgramAssociationOverflowText));
        OnPropertyChanged(nameof(HasSelectedInstalledProgramAssociationOverflow));
    }

    public void CloseInstalledProgramDetail()
    {
        SelectedInstalledProgram = null;
        SelectedInstalledProgramAssociations.Clear();
        SelectedInstalledProgramAssociationOverflowCount = 0;
        OnPropertyChanged(nameof(HasSelectedInstalledProgramAssociations));
        OnPropertyChanged(nameof(SelectedInstalledProgramAssociationOverflowText));
        OnPropertyChanged(nameof(HasSelectedInstalledProgramAssociationOverflow));
    }

    public async Task DisableStartupAppAsync(StartupAppEntry entry)
    {
        await _startupApps.DisableAsync(entry);
        await LoadStartupAppsAsync();
    }

    public Task RepairInstalledProgramAsync(InstalledProgram program) => _installedPrograms.RepairAsync(program);

    public Task ResetInstalledProgramAsync(InstalledProgram program) => _installedPrograms.ResetAsync(program);

    public void AddBrowserAllowlistedSite(string domain)
    {
        var normalized = NormalizeBrowserAllowlistDomain(domain);
        if (string.IsNullOrWhiteSpace(normalized) || BrowserAllowlistedSites.Any(site => string.Equals(site, normalized, StringComparison.OrdinalIgnoreCase)))
            return;

        BrowserAllowlistedSites.Add(normalized);
        _settings.BrowserAllowlistedSites = BrowserAllowlistedSites.ToList();
        SaveSettingsLight();
        OnPropertyChanged(nameof(HasBrowserAllowlistedSites));
    }

    private static string NormalizeBrowserAllowlistDomain(string domain)
    {
        var normalized = (domain ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[7..];
        else if (normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[8..];

        return normalized.Trim('/').Trim();
    }

    public void RemoveBrowserAllowlistedSite(string domain)
    {
        var existing = BrowserAllowlistedSites.FirstOrDefault(site => string.Equals(site, domain, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return;

        BrowserAllowlistedSites.Remove(existing);
        _settings.BrowserAllowlistedSites = BrowserAllowlistedSites.ToList();
        SaveSettingsLight();
        OnPropertyChanged(nameof(HasBrowserAllowlistedSites));
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

        if (SelectedInstalledProgram is not null
            && !InstalledPrograms.Any(program => string.Equals(program.Name, SelectedInstalledProgram.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(program.Publisher, SelectedInstalledProgram.Publisher, StringComparison.OrdinalIgnoreCase)))
        {
            CloseInstalledProgramDetail();
        }

        OnPropertyChanged(nameof(HasInstalledPrograms));
        OnPropertyChanged(nameof(ShowInstalledProgramsEmptyState));
        OnPropertyChanged(nameof(InstalledProgramsSummaryText));
    }

    public void RefreshAutomationWorkspace()
    {
        _automationCoordinator.EnsureRules(_settings);
        NormalizeAutomationRules();

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

        RefreshAutomationAttentionQueue();
        FilterAutomationHistory();
        RefreshRecentAutomationHistory();
        OnPropertyChanged(nameof(HasAutomationRules));
        OnPropertyChanged(nameof(HasScheduledAutomationRules));
        OnPropertyChanged(nameof(HasWatcherAutomationRules));
        OnPropertyChanged(nameof(HasAutomationHistoryEntries));
        OnPropertyChanged(nameof(HasAutomationAttentionEntries));
        OnPropertyChanged(nameof(AutomationPaused));
        OnPropertyChanged(nameof(AutomationPauseStatusText));
        OnPropertyChanged(nameof(ActiveAutomationCount));
        OnPropertyChanged(nameof(PausedAutomationCount));
        OnPropertyChanged(nameof(AutomationAttentionCount));
        OnPropertyChanged(nameof(HasAutomationAttention));
        OnPropertyChanged(nameof(NextAutomationRunText));
        OnPropertyChanged(nameof(LastAutomationResultText));
        OnPropertyChanged(nameof(AutomationOverviewText));
        OnPropertyChanged(nameof(AutomationAttentionSummaryText));
        OnPropertyChanged(nameof(DashboardAutomationAttentionText));
        OnPropertyChanged(nameof(DashboardAutomationAttentionSubtext));
        OnPropertyChanged(nameof(DashboardStatusBarCollapsed));
        OnPropertyChanged(nameof(ShellStatusText));
    }

    private void RefreshAutomationRuntimeDetailsInPlace()
    {
        _automationCoordinator.EnsureRules(_settings);
        NormalizeAutomationRules();

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
        }

        RefreshAutomationAttentionQueue();
        FilterAutomationHistory();
        RefreshRecentAutomationHistory();
        OnPropertyChanged(nameof(HasAutomationRules));
        OnPropertyChanged(nameof(HasScheduledAutomationRules));
        OnPropertyChanged(nameof(HasWatcherAutomationRules));
        OnPropertyChanged(nameof(HasAutomationHistoryEntries));
        OnPropertyChanged(nameof(HasAutomationAttentionEntries));
        OnPropertyChanged(nameof(AutomationPaused));
        OnPropertyChanged(nameof(AutomationPauseStatusText));
        OnPropertyChanged(nameof(ActiveAutomationCount));
        OnPropertyChanged(nameof(PausedAutomationCount));
        OnPropertyChanged(nameof(AutomationAttentionCount));
        OnPropertyChanged(nameof(HasAutomationAttention));
        OnPropertyChanged(nameof(NextAutomationRunText));
        OnPropertyChanged(nameof(LastAutomationResultText));
        OnPropertyChanged(nameof(AutomationOverviewText));
        OnPropertyChanged(nameof(AutomationAttentionSummaryText));
        OnPropertyChanged(nameof(DashboardAutomationAttentionText));
        OnPropertyChanged(nameof(DashboardAutomationAttentionSubtext));
        OnPropertyChanged(nameof(DashboardStatusBarCollapsed));
        OnPropertyChanged(nameof(ShellStatusText));
    }

    private void NormalizeAutomationRules()
    {
        foreach (var rule in _settings.AutomationRules)
        {
            if (rule.IntervalDays <= 0)
                rule.IntervalDays = 1;

            rule.ScheduleKind = rule.ScheduleKind switch
            {
                AutomationScheduleKind.Daily => AutomationScheduleKind.EveryXDays,
                AutomationScheduleKind.Startup => AutomationScheduleKind.StartupDelay,
                _ => rule.ScheduleKind
            };
        }

        if (_settings.PinnedAutomationRuleIds.Count > 5)
            _settings.PinnedAutomationRuleIds = _settings.PinnedAutomationRuleIds.Take(5).ToList();

        foreach (var rule in _settings.AutomationRules)
        {
            rule.IsPinnedToTray = _settings.PinnedAutomationRuleIds.Contains(rule.Id, StringComparer.OrdinalIgnoreCase);
            if (rule.IsPinnedToTray && rule.LastPinnedAtUtc is null)
                rule.LastPinnedAtUtc = DateTime.UtcNow;
        }
    }

    private void RefreshRecentAutomationHistory()
    {
        RecentAutomationHistoryEntries.Clear();
        foreach (var entry in AutomationHistoryEntries.Take(4))
            RecentAutomationHistoryEntries.Add(entry);

        OnPropertyChanged(nameof(HasRecentAutomationHistory));
    }

    private void RefreshAutomationAttentionQueue()
    {
        AutomationAttentionEntries.Clear();
        foreach (var item in BuildAutomationAttentionItems(AutomationHistoryEntries, _settings.DismissedAutomationAttentionReceiptIds).Take(12))
        {
            AutomationAttentionEntries.Add(item);
        }
    }

    internal static IReadOnlyList<AutomationAttentionItem> BuildAutomationAttentionItems(
        IEnumerable<AutomationRunReceipt> receipts,
        IEnumerable<string> dismissedReceiptIds)
    {
        var dismissed = dismissedReceiptIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return receipts
            .Where(entry =>
                entry.Outcome is AutomationRunOutcome.Failed or AutomationRunOutcome.Skipped or AutomationRunOutcome.Blocked
                || entry.UserActionRequired)
            .Where(entry => !dismissed.Contains(entry.Id))
            .Select(entry => new AutomationAttentionItem
            {
                Receipt = entry,
                RelativeTimeText = GetRelativeTimeText(entry.StartedAt),
                ReasonText = !string.IsNullOrWhiteSpace(entry.ConditionSummary)
                    ? entry.ConditionSummary
                    : !string.IsNullOrWhiteSpace(entry.NextStep)
                        ? entry.NextStep
                        : entry.Summary
            })
            .ToList();
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

    public async Task RetryAutomationAttentionItemAsync(AutomationAttentionItem item)
    {
        var rule = item is null
            ? null
            : AutomationRules.FirstOrDefault(rule =>
                string.Equals(rule.Id, item.Receipt.RuleId, StringComparison.OrdinalIgnoreCase));
        if (rule is not null)
            await RunAutomationRuleAsync(rule);
    }

    public void SkipAutomationAttentionItemOnce(AutomationAttentionItem item)
    {
        if (item is null)
            return;

        var rule = _settings.AutomationRules.FirstOrDefault(entry =>
            string.Equals(entry.Id, item.Receipt.RuleId, StringComparison.OrdinalIgnoreCase));
        if (rule is null)
            return;

        rule.SkipNextRun = true;
        SaveSettings();
        RefreshAutomationWorkspace();
    }

    public void DismissAutomationAttentionItem(AutomationAttentionItem item)
    {
        if (item is null)
            return;

        if (!_settings.DismissedAutomationAttentionReceiptIds.Contains(item.Receipt.Id, StringComparer.OrdinalIgnoreCase))
            _settings.DismissedAutomationAttentionReceiptIds.Add(item.Receipt.Id);

        _settingsSvc.Save(_settings);
        RefreshAutomationWorkspace();
    }

    public void RequestAutomationAttentionView()
    {
        PreferAutomationAttentionTab = true;
    }

    public void ClearAutomationAttentionViewRequest()
    {
        PreferAutomationAttentionTab = false;
    }

    public void QueueLatestAutomationReceiptInspection()
    {
        _pendingAutomationReceiptInspectionId = AutomationHistoryEntries.FirstOrDefault()?.Id ?? "";
    }

    public AutomationRunReceipt? ConsumePendingAutomationReceiptInspection()
    {
        if (string.IsNullOrWhiteSpace(_pendingAutomationReceiptInspectionId))
            return null;

        var receipt = AutomationHistoryEntries.FirstOrDefault(entry =>
            string.Equals(entry.Id, _pendingAutomationReceiptInspectionId, StringComparison.OrdinalIgnoreCase));
        _pendingAutomationReceiptInspectionId = "";
        return receipt;
    }

    public async Task RunAutomationRuleAsync(AutomationRuleSettings rule, string triggerSource = "Manual", bool manualOverride = true)
    {
        if (rule is null)
            return;

        ActiveAutomationRuleTitle = rule.Title;
        try
        {
            await _automationCoordinator.RunAsync(
                rule.Id,
                triggerSource,
                manualOverride,
                HasActiveWork);

            if (rule.Kind is AutomationRuleKind.QuickHealthCheck or AutomationRuleKind.StartupQuickCheck)
            {
                SetOnboardingChecklistItemState("health-scan", true);
                await RunFullHealthCheckAsync();
            }

            RefreshAutomationWorkspace();
        }
        finally
        {
            ActiveAutomationRuleTitle = "";
        }
    }

    public void SaveAutomationRule(AutomationRuleSettings rule)
    {
        if (rule is null)
            return;

        SetOnboardingChecklistItemState("automation-schedule", true, saveSettings: false);
        SaveSettings();
        RefreshAutomationRuntimeDetailsInPlace();
    }

    public void PauseAutomationForHour()
    {
        _settings.AutomationPausedUntilUtc = DateTime.UtcNow.AddHours(1);
        SaveSettings();
        RefreshAutomationRuntimeDetailsInPlace();
    }

    public void PauseAutomationUntilTomorrow()
    {
        var tomorrow = DateTime.Today.AddDays(1).AddHours(8);
        _settings.AutomationPausedUntilUtc = tomorrow.ToUniversalTime();
        SaveSettings();
        RefreshAutomationRuntimeDetailsInPlace();
    }

    public void PauseAllAutomationUntilTomorrow()
    {
        var until = DateTime.Today.AddDays(1).ToUniversalTime();
        foreach (var rule in _settings.AutomationRules)
            rule.PausedUntilUtc = until;

        _settings.AutomationPausedUntilUtc = until;
        SaveSettings();
        RefreshAutomationRuntimeDetailsInPlace();
    }

    public void ResumeAutomation()
    {
        _settings.AutomationPausedUntilUtc = null;
        foreach (var rule in _settings.AutomationRules)
            rule.PausedUntilUtc = null;
        SaveSettings();
        RefreshAutomationRuntimeDetailsInPlace();
    }

    public void PauseAutomationRuleUntilTomorrow(AutomationRuleSettings rule)
    {
        rule.PausedUntilUtc = DateTime.Today.AddDays(1).AddHours(8).ToUniversalTime();
        SaveAutomationRule(rule);
    }

    public void ToggleAutomationRulePin(AutomationRuleSettings rule)
    {
        if (rule is null)
            return;

        rule.IsPinnedToTray = !rule.IsPinnedToTray;
        if (rule.IsPinnedToTray)
        {
            rule.LastPinnedAtUtc = DateTime.UtcNow;
            _settings.PinnedAutomationRuleIds.RemoveAll(id => string.Equals(id, rule.Id, StringComparison.OrdinalIgnoreCase));
            _settings.PinnedAutomationRuleIds.Insert(0, rule.Id);
            _settings.PinnedAutomationRuleIds = _settings.PinnedAutomationRuleIds.Take(5).ToList();
        }
        else
        {
            _settings.PinnedAutomationRuleIds.RemoveAll(id => string.Equals(id, rule.Id, StringComparison.OrdinalIgnoreCase));
        }

        SaveSettings();
        RefreshAutomationRuntimeDetailsInPlace();
    }

    public IReadOnlyList<AutomationRuleSettings> GetPinnedAutomationRules()
    {
        return _settings.AutomationRules
            .Where(rule => rule.IsPinnedToTray)
            .OrderByDescending(rule => rule.LastPinnedAtUtc ?? DateTime.MinValue)
            .Take(5)
            .ToList();
    }

    public AutomationRunReceipt? GetLastAutomationAttentionReceipt()
        => AutomationHistoryEntries.FirstOrDefault();

    public async Task RunStartupAutomationAsync()
    {
        var startupRule = ScheduledAutomationRules.FirstOrDefault(rule =>
            rule.Kind == AutomationRuleKind.StartupQuickCheck
            && rule.Enabled
            && (rule.ScheduleKind == AutomationScheduleKind.Startup
                || rule.ScheduleKind == AutomationScheduleKind.StartupDelay));
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

    // â”€â”€ History â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    public void ReloadHistoryWorkspace()
    {
        RefreshHistory();
        RefreshRecentlyRun();
    }

    public void ClearAutomationHistory()
    {
        _automationHistory.Clear();
        _settings.DismissedAutomationAttentionReceiptIds.Clear();
        RefreshAutomationWorkspace();
        RefreshCommandPalette();
    }

    private void FilterHistory()
    {
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

        _filteredHistoryCache.Clear();
        _filteredHistoryCache.AddRange(items);
        RebuildVisibleHistoryEntries(_historyPaging.BuildInitialPage(_filteredHistoryCache));

        OnPropertyChanged(nameof(HasHistoryEntries));
        OnPropertyChanged(nameof(HasFilteredHistoryEntries));
        OnPropertyChanged(nameof(HasHistorySearchText));
        OnPropertyChanged(nameof(HistorySummaryText));
        OnPropertyChanged(nameof(HistoryLoadedSummaryText));
        OnPropertyChanged(nameof(TotalFilteredHistoryEntryCount));
        OnPropertyChanged(nameof(HistoryEmptyStateTitle));
        OnPropertyChanged(nameof(HistoryEmptyStateText));
        OnPropertyChanged(nameof(AreAllVisibleHistoryEntriesSelected));
        OnPropertyChanged(nameof(SelectedHistoryEntryCount));
        OnPropertyChanged(nameof(HasSelectedHistoryEntries));
        OnPropertyChanged(nameof(CanCompareSelectedHistoryEntries));
    }

    private void RebuildVisibleHistoryEntries(IReadOnlyList<RepairHistoryEntry> items)
    {
        FilteredHistoryEntries.Clear();
        foreach (var item in items)
            FilteredHistoryEntries.Add(item);
    }

    public int LoadMoreHistoryEntries()
    {
        if (FilteredHistoryEntries.Count >= _filteredHistoryCache.Count)
            return 0;

        var beforeCount = FilteredHistoryEntries.Count;
        var nextPage = _historyPaging.BuildNextPage(_filteredHistoryCache, beforeCount);
        foreach (var item in nextPage)
            FilteredHistoryEntries.Add(item);

        OnPropertyChanged(nameof(HasFilteredHistoryEntries));
        OnPropertyChanged(nameof(HistorySummaryText));
        OnPropertyChanged(nameof(HistoryLoadedSummaryText));
        OnPropertyChanged(nameof(AreAllVisibleHistoryEntriesSelected));
        OnPropertyChanged(nameof(SelectedHistoryEntryCount));
        OnPropertyChanged(nameof(HasSelectedHistoryEntries));
        OnPropertyChanged(nameof(CanCompareSelectedHistoryEntries));
        return nextPage.Count;
    }

    public void ToggleHistoryEntrySelection(RepairHistoryEntry entry, bool isSelected)
    {
        if (entry is null)
            return;

        entry.IsSelectedForComparison = isSelected;
        if (SelectedHistoryEntryCount != 2 && HistoryComparisonRows.Count > 0)
        {
            HistoryComparisonRows.Clear();
            _historyComparePair = (null, null);
            OnPropertyChanged(nameof(IsHistoryComparePanelOpen));
            OnPropertyChanged(nameof(HistoryCompareLeftTitle));
            OnPropertyChanged(nameof(HistoryCompareRightTitle));
        }

        OnPropertyChanged(nameof(SelectedHistoryEntryCount));
        OnPropertyChanged(nameof(HasSelectedHistoryEntries));
        OnPropertyChanged(nameof(CanCompareSelectedHistoryEntries));
        OnPropertyChanged(nameof(AreAllVisibleHistoryEntriesSelected));
    }

    public void SetAllVisibleHistorySelections(bool isSelected)
    {
        foreach (var entry in FilteredHistoryEntries)
            entry.IsSelectedForComparison = isSelected;

        if (SelectedHistoryEntryCount != 2 && HistoryComparisonRows.Count > 0)
        {
            HistoryComparisonRows.Clear();
            _historyComparePair = (null, null);
            OnPropertyChanged(nameof(IsHistoryComparePanelOpen));
            OnPropertyChanged(nameof(HistoryCompareLeftTitle));
            OnPropertyChanged(nameof(HistoryCompareRightTitle));
        }

        OnPropertyChanged(nameof(SelectedHistoryEntryCount));
        OnPropertyChanged(nameof(HasSelectedHistoryEntries));
        OnPropertyChanged(nameof(CanCompareSelectedHistoryEntries));
        OnPropertyChanged(nameof(AreAllVisibleHistoryEntriesSelected));
    }

    public void ClearHistorySelections()
    {
        foreach (var entry in HistoryEntries.Where(item => item.IsSelectedForComparison))
            entry.IsSelectedForComparison = false;

        HistoryComparisonRows.Clear();
        _historyComparePair = (null, null);
        OnPropertyChanged(nameof(SelectedHistoryEntryCount));
        OnPropertyChanged(nameof(HasSelectedHistoryEntries));
        OnPropertyChanged(nameof(CanCompareSelectedHistoryEntries));
        OnPropertyChanged(nameof(AreAllVisibleHistoryEntriesSelected));
        OnPropertyChanged(nameof(IsHistoryComparePanelOpen));
        OnPropertyChanged(nameof(HistoryCompareLeftTitle));
        OnPropertyChanged(nameof(HistoryCompareRightTitle));
    }

    public bool OpenSelectedHistoryComparison()
    {
        var selected = HistoryEntries.Where(entry => entry.IsSelectedForComparison).Take(2).ToList();
        if (selected.Count != 2)
        {
            HistoryComparisonRows.Clear();
            _historyComparePair = (null, null);
            OnPropertyChanged(nameof(IsHistoryComparePanelOpen));
            OnPropertyChanged(nameof(HistoryCompareLeftTitle));
            OnPropertyChanged(nameof(HistoryCompareRightTitle));
            return false;
        }

        _historyComparePair = (selected[0], selected[1]);
        HistoryComparisonRows.Clear();
        foreach (var row in BuildHistoryComparisonRows(selected[0], selected[1]))
            HistoryComparisonRows.Add(row);

        OnPropertyChanged(nameof(IsHistoryComparePanelOpen));
        OnPropertyChanged(nameof(HistoryCompareLeftTitle));
        OnPropertyChanged(nameof(HistoryCompareRightTitle));
        return true;
    }

    public void ToggleToolboxPin(ToolboxEntry entry)
    {
        if (entry is null)
            return;

        var changed = _toolboxWorkspace.TogglePin(entry, _settings.PinnedToolKeys);
        if (changed)
            SaveSettingsLight();
        else
            OnPropertyChanged(nameof(Settings));

        RefreshVisibleToolboxSections();
    }

    public void RecordToolboxLaunch(ToolboxEntry entry)
    {
        if (entry is null)
            return;

        _toolboxWorkspace.RecordLaunch(entry, entry.LastLaunchedAt ?? DateTime.Now);
        RefreshVisibleToolboxSections();
    }

    public async Task ExportSelectedHistoryReceiptsAsync(string filePath)
    {
        var selected = HistoryEntries.Where(entry => entry.IsSelectedForComparison).ToList();
        if (selected.Count == 0 || string.IsNullOrWhiteSpace(filePath))
            return;

        var payload = selected.Select(entry => new
        {
            entry.Id,
            Title = entry.FixTitle,
            RunDate = entry.Timestamp,
            Outcome = entry.Outcome.ToString(),
            ChangesMade = entry.ChangedSummary,
            Steps = new[]
            {
                new
                {
                    Title = string.IsNullOrWhiteSpace(entry.FailedStepTitle) ? "Execution" : entry.FailedStepTitle,
                    Result = entry.Success ? "Completed" : string.IsNullOrWhiteSpace(entry.FailedStepTitle) ? "Failed" : $"Failed at {entry.FailedStepTitle}",
                    Verification = entry.VerificationSummary
                }
            }
        });

        await File.WriteAllTextAsync(filePath, Newtonsoft.Json.JsonConvert.SerializeObject(payload, Newtonsoft.Json.Formatting.Indented));
    }

    public int DeleteSelectedHistoryEntries()
    {
        var selectedIds = HistoryEntries
            .Where(entry => entry.IsSelectedForComparison)
            .Select(entry => entry.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();
        if (selectedIds.Count == 0)
            return 0;

        _repairHistory.Delete(selectedIds);
        ClearHistorySelections();
        RefreshHistory();
        return selectedIds.Count;
    }

    public async Task<string> WriteRawReceiptFileAsync(RepairHistoryEntry entry)
    {
        var dir = Path.Combine(SharedConstants.TempDir, "raw-receipts");
        Directory.CreateDirectory(dir);

        var safeName = SanitizeFileName(string.IsNullOrWhiteSpace(entry.FixTitle) ? "receipt" : entry.FixTitle);
        var filePath = Path.Combine(dir, $"{safeName}-{entry.Timestamp:yyyyMMdd-HHmmss}.json");
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(entry, Newtonsoft.Json.Formatting.Indented);
        await File.WriteAllTextAsync(filePath, json);
        return filePath;
    }

    private static IReadOnlyList<ReceiptComparisonRow> BuildHistoryComparisonRows(RepairHistoryEntry left, RepairHistoryEntry right)
    {
        var rows = new List<ReceiptComparisonRow>
        {
            CompareRow("Run date", left.Timestamp.ToString("g"), right.Timestamp.ToString("g")),
            CompareRow("Outcome", left.Outcome.ToString(), right.Outcome.ToString()),
            CompareRow("Verification", EmptyToPlaceholder(left.VerificationSummary), EmptyToPlaceholder(right.VerificationSummary)),
            CompareRow("Changes", EmptyToPlaceholder(left.ChangedSummary), EmptyToPlaceholder(right.ChangedSummary)),
            CompareRow("Failed step", EmptyToPlaceholder(left.FailedStepTitle), EmptyToPlaceholder(right.FailedStepTitle)),
            CompareRow("Next step", EmptyToPlaceholder(left.NextStep), EmptyToPlaceholder(right.NextStep))
        };

        return rows;
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
        if (entry.IsWeeklySummary && entry.WeeklySummary is not null)
        {
            var summary = entry.WeeklySummary;
            var weeklyLines = new List<string>
            {
                "Weekly Health Summary",
                "",
                $"Week ending: {summary.WeekEndingUtc.ToLocalTime():yyyy-MM-dd HH:mm}",
                $"Health score: {summary.HealthScore}",
                $"Fixes run: {summary.FixesRunCount}",
                $"Alerts raised: {summary.AlertsRaisedCount}",
                $"Automations completed: {summary.AutomationsCompletedCount}",
                $"Automations skipped: {summary.AutomationsSkippedCount}",
                $"Automations failed: {summary.AutomationsFailedCount}",
                $"Crashes detected: {summary.CrashCount}",
                "",
                summary.SummaryText
            };

            if (summary.FixesRunNames.Count > 0)
                weeklyLines.Add($"Fixes: {string.Join(", ", summary.FixesRunNames)}");
            if (summary.AlertTypes.Count > 0)
                weeklyLines.Add($"Alert types: {string.Join(", ", summary.AlertTypes)}");
            if (summary.NotableEvents.Count > 0)
            {
                weeklyLines.Add("");
                weeklyLines.Add("Notable events:");
                weeklyLines.AddRange(summary.NotableEvents.Select(item => $"- {item}"));
            }
            if (!string.IsNullOrWhiteSpace(summary.ComparedToLastWeekText))
            {
                weeklyLines.Add("");
                weeklyLines.Add(summary.ComparedToLastWeekText);
            }

            return string.Join(Environment.NewLine, weeklyLines);
        }

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

    public void OpenReceiptInHistory(string receiptId)
    {
        if (HistoryEntries.Any(entry => string.Equals(entry.Id, receiptId, StringComparison.OrdinalIgnoreCase)))
            CurrentPage = Page.History;
    }

    public void OpenFixInLibrary(string fixId)
    {
        var fix = _catalog.GetById(fixId);
        if (fix is null)
            return;

        SelectedCategory = Categories.FirstOrDefault(category =>
            string.Equals(category.Title, fix.Category, StringComparison.OrdinalIgnoreCase));
        CurrentPage = Page.Fixes;
    }

    public string? GetHelpDocumentPathForCurrentPage() => CurrentPage switch
    {
        Page.Dashboard => QuickStartPath,
        Page.Fixes => TroubleshootingGuidePath,
        Page.Bundles => SupportBundleGuidePath,
        Page.SystemInfo => RecoveryGuidePath,
        Page.SymptomChecker => TroubleshootingGuidePath,
        Page.Toolbox => QuickStartPath,
        Page.History => RecoveryGuidePath,
        Page.Handoff => SupportBundleGuidePath,
        Page.Settings => KeyboardShortcutsPath,
        _ => null
    };

    public async Task ExecuteCommandPaletteItemAsync(CommandPaletteItem? item)
    {
        if (item is null)
            return;

        RememberGlobalSearchQuery();

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
            case CommandPaletteItemKind.Receipt:
            {
                var receipt = HistoryEntries.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, item.TargetId, StringComparison.OrdinalIgnoreCase));
                if (receipt is not null)
                    CurrentPage = Page.History;
                break;
            }
            case CommandPaletteItemKind.Setting:
                CurrentPage = Page.Settings;
                break;
            case CommandPaletteItemKind.AutomationRule:
                PreferAutomationAttentionTab = false;
                CurrentPage = Page.Bundles;
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

    // â”€â”€ Notifications â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
        RaiseHealthMonitoringSettingChanged();
        RaisePolicyStateChanged();
        RefreshCapabilityScopedContent();
        SyncAutomationSchedules();
        RefreshAutomationWorkspace();
        RefreshActiveFixes();
        RefreshCommandPalette();
    }

    public void SaveSettingsLight(bool refreshAutomationState = false)
    {
        _deployment.ApplyPolicy(_settings);
        _automationCoordinator.EnsureRules(_settings);
        if (refreshAutomationState)
            _settings.OnboardingConfiguredAutomation = true;
        _settingsSvc.Save(_settings);
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(HasSuppressedItems));
        OnPropertyChanged(nameof(CurrentProfileStatusText));
        OnPropertyChanged(nameof(SelectedLandingPage));
        OnPropertyChanged(nameof(SelectedNotificationMode));
        OnPropertyChanged(nameof(SelectedSupportBundleExportLevel));
        OnPropertyChanged(nameof(SelectedAutomationQuietHoursStart));
        OnPropertyChanged(nameof(SelectedAutomationQuietHoursEnd));
        OnPropertyChanged(nameof(SettingsLoadStatus));
        OnPropertyChanged(nameof(SettingsLoadStatusText));
        OnPropertyChanged(nameof(HasSettingsLoadNotice));
        OnPropertyChanged(nameof(ShellStatusText));
        RaiseHealthMonitoringSettingChanged();
        RaisePolicyStateChanged();

        if (refreshAutomationState)
        {
            SyncAutomationSchedules();
            RefreshAutomationRuntimeDetailsInPlace();
        }
        else
        {
            RefreshCommandPalette();
        }

        OnPropertyChanged(nameof(HasOnboardingChecklist));
    }

    private void RaiseHealthMonitoringSettingChanged()
    {
        OnPropertyChanged(nameof(HealthMonitoringEnabled));
        OnPropertyChanged(nameof(ShowHealthAlertTrayNotifications));
        OnPropertyChanged(nameof(SendWeeklyHealthSummary));
        OnPropertyChanged(nameof(HealthAlertFrequencyAll));
        OnPropertyChanged(nameof(HealthAlertFrequencyWarningsAndCritical));
        OnPropertyChanged(nameof(HealthAlertFrequencyCriticalOnly));
    }

    private void RaisePolicyStateChanged()
    {
        OnPropertyChanged(nameof(BehaviorProfilePolicyState));
        OnPropertyChanged(nameof(NotificationModePolicyState));
        OnPropertyChanged(nameof(SupportBundleExportLevelPolicyState));
        OnPropertyChanged(nameof(LandingPagePolicyState));
        OnPropertyChanged(nameof(AdvancedModePolicyState));
        OnPropertyChanged(nameof(RunQuickScanOnLaunchPolicyState));
        OnPropertyChanged(nameof(ShowNotificationsPolicyState));
        OnPropertyChanged(nameof(CheckForUpdatesOnLaunchPolicyState));
        OnPropertyChanged(nameof(PreferSafeMaintenanceDefaultsPolicyState));
        OnPropertyChanged(nameof(RunAtStartupPolicyState));
        OnPropertyChanged(nameof(MinimizeToTrayPolicyState));
        OnPropertyChanged(nameof(ShowTrayBalloonsPolicyState));
        OnPropertyChanged(nameof(CanEditBehaviorProfileSetting));
        OnPropertyChanged(nameof(CanEditNotificationModeSetting));
        OnPropertyChanged(nameof(CanEditLandingPageSetting));
        OnPropertyChanged(nameof(CanEditSupportBundleDetailSetting));
        OnPropertyChanged(nameof(CanEditAdvancedModeSetting));
        OnPropertyChanged(nameof(CanEditRunAtStartupSetting));
        OnPropertyChanged(nameof(CanEditMinimizeToTraySetting));
        OnPropertyChanged(nameof(CanEditShowTrayBalloonsSetting));
    }

    public void ApplyTheme(string theme)
    {
        _settings.Theme = theme;
        App.SwitchTheme(theme);
        _settingsSvc.Save(_settings);
    }

    public void MarkAppInteraction()
    {
        if (_settings.LastAppInteractionUtc.HasValue
            && DateTime.UtcNow - _settings.LastAppInteractionUtc.Value < TimeSpan.FromSeconds(30))
            return;

        _settings.LastAppInteractionUtc = DateTime.UtcNow;
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
        OnPropertyChanged(nameof(DashboardAutomationAttentionText));
        OnPropertyChanged(nameof(DashboardAutomationAttentionSubtext));
        OnPropertyChanged(nameof(DashboardStatusBarCollapsed));
    }

    private void RefreshSupportCenters()
    {
        SupportCenters.Clear();
        foreach (var center in _supportCenterService.BuildCenters(Snapshot, _allInstalledPrograms, HistoryEntries.ToList()))
            SupportCenters.Add(center);

        OnPropertyChanged(nameof(HasSupportCenters));
        OnPropertyChanged(nameof(StorageSupportCenters));
        OnPropertyChanged(nameof(StartupPerformanceSupportCenters));
        OnPropertyChanged(nameof(WindowsHealthSupportCenters));
        OnPropertyChanged(nameof(NetworkSupportCenters));
        OnPropertyChanged(nameof(DevicesSupportCenters));
        RefreshCommandPalette();
    }

    private IReadOnlyList<SupportCenterDefinition> GetSupportCenters(params string[] ids)
        => SupportCenters
            .Where(center => ids.Contains(center.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();

    private bool GetDeviceHealthSectionState(string key)
        => _deviceHealthSectionStates.TryGetValue(key, out var expanded) && expanded;

    public void ToggleDeviceHealthSection(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        _deviceHealthSectionStates[key] = !GetDeviceHealthSectionState(key);
        RaiseDeviceHealthSectionStateChanged();
    }

    private void RaiseDeviceHealthSectionStateChanged()
    {
        OnPropertyChanged(nameof(IsDeviceHealthSystemOverviewExpanded));
        OnPropertyChanged(nameof(IsDeviceHealthStorageExpanded));
        OnPropertyChanged(nameof(IsDeviceHealthStartupPerformanceExpanded));
        OnPropertyChanged(nameof(IsDeviceHealthWindowsHealthExpanded));
        OnPropertyChanged(nameof(IsDeviceHealthNetworkExpanded));
        OnPropertyChanged(nameof(IsDeviceHealthDevicesPeripheralsExpanded));
        OnPropertyChanged(nameof(IsDeviceHealthSecurityExpanded));
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

    // â”€â”€ INotifyPropertyChanged â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
            RecordToolboxLaunch(entry);
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
        Page.FixMyPc => "Fix My PC",
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
        "Fix My PC" => Page.FixMyPc,
        "Automation" => Page.Bundles,
        "Device Health" => Page.SystemInfo,
        "Windows Tools" => Page.Toolbox,
        "Support Package" => Page.Handoff,
        "Activity" => Page.History,
        "Settings" => Page.Settings,
        _ => Page.Dashboard
    };

    public static IReadOnlyList<Page> GetNavigationPages(bool simplifiedModeEnabled)
        => simplifiedModeEnabled
            ? [Page.Dashboard, Page.FixMyPc, Page.Settings]
            : [Page.Dashboard, Page.SymptomChecker, Page.Fixes, Page.Bundles, Page.SystemInfo, Page.Toolbox, Page.Handoff, Page.History, Page.Settings];

    private static string FormatEditionLabel(AppEdition edition) => edition switch
    {
        AppEdition.ManagedServiceProvider => "MSP",
        AppEdition.Pro => "Pro",
        _ => "Basic"
    };

    private static string GetRelativeTimeText(DateTime timestamp)
    {
        var diff = DateTime.Now - timestamp;
        if (diff.TotalSeconds < 60)
            return "just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 2)
            return "Yesterday";
        return timestamp.ToString("MMM d");
    }

    private static string GetOutcomeLabel(ExecutionOutcome outcome) => outcome switch
    {
        ExecutionOutcome.Completed => "Success",
        ExecutionOutcome.Blocked => "Blocked",
        ExecutionOutcome.Cancelled => "Skipped",
        ExecutionOutcome.Interrupted => "Interrupted",
        ExecutionOutcome.Resumable => "Needs Review",
        _ => "Failed"
    };

    private static string EmptyToPlaceholder(string value)
        => string.IsNullOrWhiteSpace(value) ? "No details recorded" : value;

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray();
        return new string(chars).Trim();
    }

    private static ReceiptComparisonRow CompareRow(string label, string left, string right) => new()
    {
        Label = label,
        LeftValue = left,
        RightValue = right,
        IsDifferent = !string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
    };

    private async Task AutoHideBundleStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(6), cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
            return;

        ShowBundleStatusBanner = false;
        BundleStatusMessage = "";
        BundleStatusFolderPath = "";
        BundleProgressPercent = null;
        BundleProgressIndeterminate = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
