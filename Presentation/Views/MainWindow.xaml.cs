using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using HelpDesk.Presentation.Helpers;
using HelpDesk.Presentation.ViewModels;
using HelpDesk.Presentation.Views.Dialogs;
using HelpDesk.Presentation.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Controls;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using NavPage = HelpDesk.Domain.Enums.Page;
using WpfButton = System.Windows.Controls.Button;

namespace HelpDesk.Presentation.Views;

public enum ShellShortcutAction
{
    None,
    OpenGlobalSearch,
    NavigateHistory,
    NavigateFixCenter,
    NavigateDashboard,
    NavigateSettings,
    NavigateSupportPackage,
    RunLastUsefulAction,
    RefreshCurrentPage,
    OpenHelp,
    NavigateBack,
    OpenKeyboardShortcutsDialog
}

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _vm;
    private readonly ISnackbarService _snackbar;
    private readonly IAppLogger _logger;
    private readonly IServiceProvider _services;
    private readonly IHealthMonitorService? _healthMonitor;
    private readonly IWeeklySummaryService? _weeklySummaryService;
    private readonly IShellPresenceService? _shellPresence;
    private readonly Dictionary<NavPage, System.Windows.Controls.Page> _pageCache = [];
    private readonly Stack<NavPage> _navigationHistory = [];
    private readonly Queue<QueuedBalloonNotification> _balloonQueue = [];
    private readonly Dictionary<string, DateTime> _balloonShownAtByAlertId = new(StringComparer.OrdinalIgnoreCase);
    private FormsNotifyIcon? _trayIcon;
    private CancellationTokenSource? _healthMonitorCts;
    private Task? _balloonPumpTask;
    private bool _allowExit;
    private bool _trayBalloonShown;
    private bool _startupRendered;
    private bool _suppressHistoryPush;
    private bool _healthMonitorStarted;
    private string _pendingBalloonAlertId = "";

    private sealed record QueuedBalloonNotification(string AlertId, string Title, string Body, AlertSeverity Severity);

    public MainWindow(
        MainViewModel vm,
        ISnackbarService snackbar,
        IAppLogger logger,
        IServiceProvider services)
    {
        InitializeComponent();
        _vm = vm;
        _snackbar = snackbar;
        _logger = logger;
        _services = services;
        _healthMonitor = services.GetService<IHealthMonitorService>();
        _weeklySummaryService = services.GetService<IWeeklySummaryService>();
        _shellPresence = services.GetService<IShellPresenceService>();
        DataContext = vm;
        _vm.RunbookPreflightRequestAsync = ConfirmRunbookPreflightAsync;
        _vm.RunbookPostResultRequestAsync = ShowRunbookPostResultAsync;
        _vm.FixConfirmationRequestAsync = ConfirmSimplifiedFixAsync;
        _vm.OpenGlobalSearchRequest = OpenCommandPaletteAndFocus;

        Loaded += OnLoaded;
        ContentRendered += OnContentRendered;
        Activated += OnActivated;
        StateChanged += OnWindowStateChanged;
        _vm.PropertyChanged += ViewModel_PropertyChanged;
        CreateTrayIcon();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _shellPresence?.MarkAppOpened();
            _vm.MarkAppInteraction();
            RestoreWindowPlacement();

            var logo = ImageHelper.GetLogoTransparent(_vm.Branding.LogoPath);
            if (logo is not null)
            {
                LogoImage.Source = logo;
                PrivacyLogoImage.Source = logo;
            }

            _snackbar.SetSnackbarPresenter(MainSnackbarPresenter);
            Title = _vm.ProductDisplayName;
            _vm.ShowPrivacyNotice = !_vm.Settings.OnboardingDismissed;
            UpdateTrayState();
            Dispatcher.BeginInvoke(new Action(() => _ = EnsureStartupShellReadyAsync()));
        }
        catch (Exception ex)
        {
            _logger.Error("Main window failed during load", ex);
            System.Windows.MessageBox.Show(
                $"{_vm.ProductDisplayName} hit a startup problem while loading the workspace. The error was written to the app log.",
                _vm.ProductDisplayName,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private async void OnContentRendered(object? sender, EventArgs e)
        => await EnsureStartupShellReadyAsync();

    private async Task EnsureStartupShellReadyAsync()
    {
        if (_startupRendered)
            return;

        _startupRendered = true;
        var startupStopwatch = Stopwatch.StartNew();

        try
        {
            await Task.Yield();

            SelectPage(_vm.GetStartupLandingPage(), runPageActivation: false);

            if (_vm.HasStartupRecoverySummary)
            {
                _snackbar.Show(
                    "Startup notice",
                    _vm.StartupRecoverySummaryText,
                    ControlAppearance.Secondary,
                    new SymbolIcon(SymbolRegular.Info20),
                    TimeSpan.FromSeconds(8));
            }

            _ = RunStartupWorkAsync();
            _logger.Info($"Shell became interactive in {startupStopwatch.ElapsedMilliseconds} ms.");
        }
        catch (Exception ex)
        {
            _logger.Error("Main window failed after first render", ex);
        }
    }

    private void CreateTrayIcon()
    {
        try
        {
            DrawingIcon icon;
            using (var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/FixFoxLogo.ico"))?.Stream)
            {
                icon = stream is not null
                    ? new DrawingIcon(stream)
                    : DrawingSystemIcons.Application;
            }

            _trayIcon = new FormsNotifyIcon
            {
                Text = _vm.ProductDisplayName,
                Icon = icon,
                Visible = false
            };
            _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
            _trayIcon.BalloonTipClicked += (_, _) => Dispatcher.BeginInvoke(new Action(OpenPendingBalloonAlert));
            UpdateTrayState();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to create tray icon", ex);
            _trayIcon = null;
        }
    }

    private void RestoreWindowPlacement()
    {
        if (_vm.Settings.WindowLeft >= 0 && _vm.Settings.WindowTop >= 0)
        {
            Left = _vm.Settings.WindowLeft;
            Top = _vm.Settings.WindowTop;
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_vm.Settings.MinimizeToTray && WindowState == WindowState.Minimized)
            MinimizeToTray();
    }

    private void MinimizeToTray()
    {
        Hide();
        _shellPresence?.SetTrayActive(true);
        if (_trayIcon is null)
            return;

        _trayIcon.Visible = true;
        UpdateTrayState();
        if (_vm.Settings.ShowTrayBalloons && !_trayBalloonShown)
        {
            _trayIcon.ShowBalloonTip(
                2500,
                _vm.ProductDisplayName,
                $"{_vm.ShellStatusText}. {_vm.ProductDisplayName} is still running in the tray.",
                System.Windows.Forms.ToolTipIcon.Info);
            _trayBalloonShown = true;
        }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        _shellPresence?.MarkAppOpened();
        _vm.MarkAppInteraction();
        Activate();
        if (_trayIcon is not null)
            _trayIcon.Visible = false;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.ShellStatusText)
            or nameof(MainViewModel.UnreadNotifCount)
            or nameof(MainViewModel.CurrentPageLabel)
            or nameof(MainViewModel.HasLastUsefulAction)
            or nameof(MainViewModel.LastUsefulActionLabel)
            or nameof(MainViewModel.CanResumeInterruptedRepair)
            or nameof(MainViewModel.AutomationPaused)
            or nameof(MainViewModel.AutomationPauseStatusText)
            or nameof(MainViewModel.AutomationAttentionCount)
            or nameof(MainViewModel.HasHealthAlerts)
            or nameof(MainViewModel.HealthMonitoringEnabled)
            or nameof(MainViewModel.ShowHealthAlertTrayNotifications))
        {
            UpdateTrayState();
        }

        if (e.PropertyName == nameof(MainViewModel.HealthMonitoringEnabled))
            ToggleHealthMonitoring(_vm.HealthMonitoringEnabled);

        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
            NavigateTo(_vm.CurrentPage);
    }

    private void UpdateTrayState()
    {
        if (_trayIcon is null)
            return;

        var tooltip = _vm.HealthMonitoringEnabled
            ? _vm.HasHealthAlerts
                ? $"{_vm.ProductDisplayName} - {_vm.HealthAlerts.Count} health alert(s)"
                : $"{_vm.ProductDisplayName} - System healthy"
            : $"{_vm.ProductDisplayName} - {(string.IsNullOrWhiteSpace(_vm.ShellStatusText) ? "Ready" : _vm.ShellStatusText)}";
        _trayIcon.Text = tooltip.Length <= 63 ? tooltip : tooltip[..63];
        RebuildTrayMenu();
    }

    private void RebuildTrayMenu()
    {
        if (_trayIcon is null)
            return;

        _trayIcon.ContextMenuStrip?.Dispose();

        var menu = new FormsContextMenuStrip();
        menu.Items.Add(new FormsToolStripMenuItem($"Open {_vm.ProductDisplayName}", null, (_, _) => RestoreFromTray()));
        menu.Items.Add(new FormsToolStripMenuItem("Open Home", null, (_, _) =>
        {
            RestoreFromTray();
            SelectPage(NavPage.Dashboard);
        }));
        menu.Items.Add(new FormsToolStripMenuItem("Open Automation Center", null, (_, _) =>
        {
            RestoreFromTray();
            SelectPage(NavPage.Bundles);
        }));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(new FormsToolStripMenuItem("Run Quick Scan", null, async (_, _) =>
        {
            RestoreFromTray();
            var rule = _vm.AutomationRules.FirstOrDefault(item => item.Id == "quick-health-check");
            if (rule is not null)
                await _vm.RunAutomationRuleAsync(rule);
            else
                await _vm.RunQuickScanAsync();
        }));
        menu.Items.Add(new FormsToolStripMenuItem("Safe Maintenance Now", null, async (_, _) =>
        {
            RestoreFromTray();
            var rule = _vm.AutomationRules.FirstOrDefault(item => item.Id == "safe-maintenance");
            if (rule is not null)
                await _vm.RunAutomationRuleAsync(rule);
            else
                await _vm.RunRecommendedMaintenanceAsync();
        }));

        var lastActionItem = new FormsToolStripMenuItem(
            _vm.HasLastUsefulAction ? _vm.LastUsefulActionLabel : "Run Last Useful Action",
            null,
            async (_, _) =>
            {
                RestoreFromTray();
                await _vm.RunLastUsefulActionAsync();
            })
        {
            Enabled = _vm.HasLastUsefulAction
        };
        menu.Items.Add(lastActionItem);

        var resumeItem = new FormsToolStripMenuItem("Resume Interrupted Repair", null, async (_, _) =>
        {
            RestoreFromTray();
            await _vm.ResumeInterruptedRepairAsync();
        })
        {
            Enabled = _vm.CanResumeInterruptedRepair
        };
        menu.Items.Add(resumeItem);

        menu.Items.Add(new FormsToolStripMenuItem(
            _vm.AutomationPaused ? "Resume Automation" : "Pause Automation For 1 Hour",
            null,
            (_, _) =>
            {
                if (_vm.AutomationPaused)
                    _vm.ResumeAutomation();
                else
                    _vm.PauseAutomationForHour();
            }));

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(new FormsToolStripMenuItem("Create Support Package", null, async (_, _) =>
        {
            RestoreFromTray();
            await _vm.CreateEvidenceBundleAsync();
        }));
        menu.Items.Add(new FormsToolStripMenuItem("Open Activity", null, (_, _) =>
        {
            RestoreFromTray();
            SelectPage(NavPage.History);
        }));
        menu.Items.Add(new FormsToolStripMenuItem("Settings", null, (_, _) =>
        {
            RestoreFromTray();
            SelectPage(NavPage.Settings);
        }));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(new FormsToolStripMenuItem("Quit", null, (_, _) =>
        {
            _allowExit = true;
            if (_trayIcon is not null)
                _trayIcon.Visible = false;
            Close();
        }));

        _trayIcon.ContextMenuStrip = menu;
    }

    private void NavigateTo(NavPage page)
    {
        var target = ResolvePage(page);

        if (PageFrame.Content != target)
            PageFrame.Navigate(target);

        UpdateNavVisualState(page);
    }

    private System.Windows.Controls.Page ResolvePage(NavPage page)
    {
        if (_pageCache.TryGetValue(page, out var cached))
            return cached;

        System.Windows.Controls.Page resolved = page switch
        {
            NavPage.Dashboard => _services.GetRequiredService<DashboardPage>(),
            NavPage.Fixes => _services.GetRequiredService<FixCenterPage>(),
            NavPage.FixMyPc => _services.GetRequiredService<FixMyPcPage>(),
            NavPage.Bundles => _services.GetRequiredService<BundlesPage>(),
            NavPage.SystemInfo => _services.GetRequiredService<SystemInfoPage>(),
            NavPage.SymptomChecker => _services.GetRequiredService<SymptomCheckerPage>(),
            NavPage.Toolbox => _services.GetRequiredService<ToolboxPage>(),
            NavPage.History => _services.GetRequiredService<HistoryPage>(),
            NavPage.Handoff => _services.GetRequiredService<HandoffPage>(),
            NavPage.Settings => _services.GetRequiredService<SettingsPage>(),
            _ => _services.GetRequiredService<DashboardPage>()
        };

        _pageCache[page] = resolved;
        return resolved;
    }

    private async Task RunStartupWorkAsync()
    {
        var startupWorkStopwatch = Stopwatch.StartNew();
        try
        {
            var loadSystemInfoTask = _vm.LoadSystemInfoAsync();
            var shouldWarmInstalledPrograms = _vm.GetStartupLandingPage() == NavPage.SystemInfo;
            var loadInstalledProgramsTask = shouldWarmInstalledPrograms
                ? _vm.LoadInstalledProgramsAsync()
                : Task.CompletedTask;

            await Task.WhenAll(loadSystemInfoTask, loadInstalledProgramsTask);
            await _vm.RunStartupAutomationAsync();
            await _vm.PrimeDeferredWorkspaceStateAsync();
            StartHealthMonitoringIfNeeded();
            _ = Task.Run(GenerateWeeklySummaryIfDueAsync);
            _ = Dispatcher.BeginInvoke(new Action(PrewarmShellSurfaces), DispatcherPriority.Background);
            _logger.Info($"Startup background work completed in {startupWorkStopwatch.ElapsedMilliseconds} ms.");
        }
        catch (Exception ex)
        {
            _logger.Error("Startup background work failed", ex);
        }
    }

    private void SelectPage(NavPage page, bool runPageActivation = true)
    {
        page = NormalizePageForCurrentMode(page);

        if (!_suppressHistoryPush && _vm.CurrentPage != page)
            _navigationHistory.Push(_vm.CurrentPage);

        _vm.CurrentPage = page;
        NavigateTo(page);

        if (!runPageActivation)
            return;

        switch (page)
        {
            case NavPage.SystemInfo:
                _ = _vm.LoadSystemInfoAsync();
                if (_vm.InstalledPrograms.Count == 0)
                    _ = _vm.LoadInstalledProgramsAsync();
                break;
            case NavPage.Bundles:
                _vm.RefreshWeeklyTuneUpSchedule();
                break;
            case NavPage.Handoff:
                _vm.MarkNotificationsRead();
                break;
        }
    }

    private void UpdateNavVisualState(NavPage page)
    {
        var activeStyle = (Style)FindResource("NavButtonActive");
        var defaultStyle = (Style)FindResource("NavButtonStyle");

        foreach (var button in new[]
                 {
                     NavDashboard, NavSymptomChecker, NavFixes, NavBundles,
                     NavSystemInfo, NavToolbox, NavHandoff, NavHistory, NavSettings
                 })
        {
            button.Style = defaultStyle;
        }

        var activeButton = page switch
        {
            NavPage.Dashboard => NavDashboard,
            NavPage.SymptomChecker => NavSymptomChecker,
            NavPage.FixMyPc => NavFixes,
            NavPage.Fixes => NavFixes,
            NavPage.Bundles => NavBundles,
            NavPage.SystemInfo => NavSystemInfo,
            NavPage.Handoff => NavHandoff,
            NavPage.Toolbox => NavToolbox,
            NavPage.History => NavHistory,
            NavPage.Settings => NavSettings,
            _ => NavDashboard
        };

        activeButton.Style = activeStyle;
    }

    public void NavigateToPage(NavPage page) => SelectPage(page);
    public void OpenKeyboardShortcutsDialog() => _vm.OpenKeyboardShortcutsDialog();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleMaximize();
        else
            DragMove();
    }

    private void WinMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void WinMaxRestore_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void WinClose_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaxRestoreIcon.Symbol = SymbolRegular.Maximize16;
            BtnMaxRestore.ToolTip = "Maximize";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaxRestoreIcon.Symbol = SymbolRegular.SquareMultiple16;
            BtnMaxRestore.ToolTip = "Restore";
        }
    }

    private void SidebarToggle_Click(object sender, RoutedEventArgs e) =>
        _vm.IsSidebarCollapsed = !_vm.IsSidebarCollapsed;

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button)
            return;
        if (!Enum.TryParse<NavPage>(button.Tag?.ToString(), out var page))
            return;

        SelectPage(page);
    }

    private void Category_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: FixCategory category })
            return;

        _vm.SelectedCategory = category;
        _vm.SearchText = string.Empty;
    }

    private void CommandPaletteBg_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.CloseCommandPalette();
        CloseNotifications();
        e.Handled = true;
    }

    private void CommandPaletteBox_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                _vm.CloseCommandPalette();
                e.Handled = true;
                break;
            case Key.Enter:
                _ = RunPaletteSelectionAsync(_vm.SelectedCommandPaletteItem ?? _vm.CommandPaletteResults.FirstOrDefault());
                _vm.CloseCommandPalette();
                e.Handled = true;
                break;
            case Key.Down:
                _vm.MoveCommandPaletteSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                _vm.MoveCommandPaletteSelection(-1);
                e.Handled = true;
                break;
            case Key.Tab:
                _vm.MoveCommandPaletteGroup(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
                e.Handled = true;
                break;
        }
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        _shellPresence?.MarkAppOpened();
        _vm.MarkAppInteraction();
    }

    private void CommandPaletteBox_GotFocus(object sender, RoutedEventArgs e) => _vm.RefreshCommandPalette();

    private async void PaletteRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: CommandPaletteItem item })
            return;

        await RunPaletteSelectionAsync(item);
    }

    private void RecentSearchChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string query })
            _vm.UseGlobalSearchRecentQuery(query);
    }

    private async Task RunPaletteSelectionAsync(CommandPaletteItem? item)
    {
        if (item is null || item.IsGroupHeader)
            return;

        _vm.CloseCommandPalette();
        if (item.Kind == CommandPaletteItemKind.Page && item.TargetPage.HasValue)
        {
            SelectPage(item.TargetPage.Value);
            return;
        }

        await _vm.ExecuteCommandPaletteItemAsync(item);
    }

    private async void PrivacyOk_Click(object sender, RoutedEventArgs e) => await _vm.CompleteOnboardingAsync();

    private void SimplifiedModeChoice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string mode })
            _vm.ChooseFirstRunExperience(mode);
    }

    private void SimplifiedOnboardingNext_Click(object sender, RoutedEventArgs e)
        => _vm.AdvanceSimplifiedOnboarding();

    private void OnboardingProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string profile })
            _vm.ApplyBehaviorProfile(profile);
    }

    private void OnboardingToggle_Changed(object sender, RoutedEventArgs e)
        => _vm.SaveSettingsLight();

    private void OnboardingRunAtStartup_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle)
            return;

        var enable = toggle.IsChecked == true;
        SettingsPage.SetRunAtStartupForShell(enable, _vm.ProductDisplayName);
        _vm.Settings.RunAtStartup = enable;
        _vm.SaveSettingsLight();
    }

    private void OnboardingQuickStart_Click(object sender, RoutedEventArgs e) => OpenPath(_vm.QuickStartPath);

    private void OnboardingPrivacy_Click(object sender, RoutedEventArgs e) => OpenPath(_vm.PrivacyGuidePath);

    private void NotificationsToggle_Click(object sender, RoutedEventArgs e)
    {
        NotificationsPopup.IsOpen = !NotificationsPopup.IsOpen;
    }

    private void SimpleHelp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string rawTag } anchor)
            return;

        var parts = rawTag.Split('|');
        var message = parts.ElementAtOrDefault(0) ?? string.Empty;
        var automationId = parts.ElementAtOrDefault(1) ?? "HelpPopover";
        ShowSimpleHelpPopover(anchor, message, automationId);
    }

    private void OpenCommandPaletteButton_Click(object sender, RoutedEventArgs e)
    {
        OpenCommandPaletteAndFocus();
    }

    private void KeyboardShortcutsBg_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.CloseKeyboardShortcutsDialog();
        e.Handled = true;
    }

    private void KeyboardShortcutsClose_Click(object sender, RoutedEventArgs e)
    {
        _vm.CloseKeyboardShortcutsDialog();
    }

    public Task PreviewRunbookAsync(RunbookDefinition runbook)
    {
        var window = new RunbookPreflightWindow(runbook) { Owner = this };
        window.ShowDialog();
        return Task.CompletedTask;
    }

    private Task<bool> ConfirmRunbookPreflightAsync(RunbookDefinition runbook)
    {
        if (_vm.SimplifiedModeEnabled)
        {
            var dialog = new SimplifiedConfirmationWindow(
                runbook.Title,
                BuildSimplifiedRunbookConfirmationText(runbook),
                runbook.RiskLevel == FixRiskLevel.MayRestart
                    ? "Save and run"
                    : "Run it",
                "Not now",
                showHelpAction: runbook.RiskLevel == FixRiskLevel.Advanced)
            { Owner = this };
            dialog.ShowDialog();
            if (dialog.Decision == SimplifiedConfirmationDecision.GetHelpInstead)
                SelectPage(NavPage.Handoff);
            return Task.FromResult(dialog.Decision == SimplifiedConfirmationDecision.Run);
        }

        if (!runbook.IsPreflightRequired)
            return Task.FromResult(true);

        var window = new RunbookPreflightWindow(runbook) { Owner = this };
        var result = window.ShowDialog();
        return Task.FromResult(result == true);
    }

    private Task<SimplifiedConfirmationDecision> ConfirmSimplifiedFixAsync(FixItem fix)
    {
        if (!_vm.SimplifiedModeEnabled)
            return Task.FromResult(SimplifiedConfirmationDecision.Run);

        var window = new SimplifiedConfirmationWindow(
            fix.Title,
            BuildSimplifiedFixConfirmationText(fix),
            fix.RiskLevel == FixRiskLevel.MayRestart
                ? "Save and run"
                : fix.RiskLevel == FixRiskLevel.Advanced
                    ? "I understand, run it"
                    : "Run it",
            "Not now",
            showHelpAction: fix.RiskLevel == FixRiskLevel.Advanced)
        { Owner = this };
        window.ShowDialog();
        return Task.FromResult(window.Decision);
    }

    private async Task ShowRunbookPostResultAsync(RunbookDefinition runbook, RunbookExecutionSummary summary)
    {
        var window = new RunbookPostResultWindow(runbook, summary) { Owner = this };
        window.ShowDialog();

        if (window.SaveReceiptRequested)
            SelectPage(NavPage.History);

        if (window.EscalateRequested)
        {
            SelectPage(NavPage.Handoff);
            await _vm.CreateEvidenceBundleAsync();
        }
    }

    private void NavigateBack()
    {
        if (_navigationHistory.Count == 0)
            return;

        _suppressHistoryPush = true;
        try
        {
            SelectPage(_navigationHistory.Pop());
        }
        finally
        {
            _suppressHistoryPush = false;
        }
    }

    private void NotificationsPopup_Closed(object sender, EventArgs e)
    { }

    private void NotificationRead_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: AppNotification notification })
            _vm.MarkNotificationRead(notification);
    }

    private void NotificationDismiss_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: AppNotification notification })
            _vm.DismissNotification(notification);
    }

    private void NotificationOpenSupport_Click(object sender, RoutedEventArgs e)
    {
        CloseNotifications();
        SelectPage(NavPage.Handoff);
    }

    private void NotificationRunFix_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: string fixId } || string.IsNullOrWhiteSpace(fixId))
            return;

        CloseNotifications();
        _ = _vm.RunFixByIdAsync(fixId);
    }

    private void NotificationClearAll_Click(object sender, RoutedEventArgs e)
    {
        _vm.ClearNotifications();
        CloseNotifications();
    }

    private void CloseNotifications()
    {
        NotificationsPopup.IsOpen = false;
    }

    private static bool IsTextInputFocused()
        => Keyboard.FocusedElement is System.Windows.Controls.TextBox
            or System.Windows.Controls.ComboBox
            or System.Windows.Controls.PasswordBox
            or System.Windows.Controls.RichTextBox
            or System.Windows.Controls.Primitives.Selector;

    public static ShellShortcutAction ResolveGlobalShortcut(Key key, ModifierKeys modifiers, bool isTextInputFocused)
    {
        if (key == Key.F1)
            return ShellShortcutAction.OpenHelp;

        if (key == Key.Oem2 && modifiers == ModifierKeys.Shift && !isTextInputFocused)
            return ShellShortcutAction.OpenKeyboardShortcutsDialog;

        if (isTextInputFocused)
            return ShellShortcutAction.None;

        if ((key == Key.K && modifiers == ModifierKeys.Control)
            || (key == Key.Space && modifiers == ModifierKeys.Control))
            return ShellShortcutAction.OpenGlobalSearch;

        if (key == Key.F5 && modifiers == ModifierKeys.None)
            return ShellShortcutAction.RefreshCurrentPage;

        if (key == Key.H && modifiers == ModifierKeys.Control)
            return ShellShortcutAction.NavigateHistory;

        if (key == Key.F && modifiers == ModifierKeys.Control)
            return ShellShortcutAction.NavigateFixCenter;

        if (key == Key.D && modifiers == ModifierKeys.Control)
            return ShellShortcutAction.NavigateDashboard;

        if (key == Key.OemComma && modifiers == ModifierKeys.Control)
            return ShellShortcutAction.NavigateSettings;

        if (key == Key.B && modifiers == ModifierKeys.Control)
            return ShellShortcutAction.NavigateSupportPackage;

        if (key == Key.R && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            return ShellShortcutAction.RunLastUsefulAction;

        if (key == Key.Left && modifiers == ModifierKeys.Alt)
            return ShellShortcutAction.NavigateBack;

        return ShellShortcutAction.None;
    }

    private async Task RefreshCurrentPageAsync()
    {
        switch (_vm.CurrentPage)
        {
            case NavPage.Dashboard:
                await _vm.RefreshDashboardSuggestionsAsync();
                break;
            case NavPage.SystemInfo:
                await _vm.LoadSystemInfoAsync();
                await _vm.LoadInstalledProgramsAsync();
                await _vm.LoadStartupAppsAsync();
                break;
            case NavPage.Bundles:
                _vm.RefreshAutomationWorkspace();
                break;
            case NavPage.History:
                _vm.ClearHistorySelections();
                break;
            case NavPage.Settings:
            case NavPage.Fixes:
            case NavPage.Toolbox:
            case NavPage.Handoff:
            case NavPage.SymptomChecker:
            default:
                break;
        }
    }

    private void OpenCurrentPageHelp()
    {
        var path = _vm.GetHelpDocumentPathForCurrentPage();
        if (string.IsNullOrWhiteSpace(path))
            return;

        OpenPath(path);
    }

    private bool TryHandlePageSpecificShortcut(KeyEventArgs e)
    {
        switch (_vm.CurrentPage)
        {
            case NavPage.Fixes when ResolvePage(NavPage.Fixes) is FixCenterPage fixesPage:
                if (e.Key == Key.Oem2 && Keyboard.Modifiers == ModifierKeys.None)
                {
                    fixesPage.FocusSearchBox();
                    return true;
                }

                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
                {
                    _ = fixesPage.RunFocusedFixAsync();
                    return true;
                }

                if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None)
                {
                    fixesPage.ToggleFocusedFixExpansion();
                    return true;
                }
                break;
            case NavPage.History when ResolvePage(NavPage.History) is HistoryPage historyPage:
                if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    _vm.SetAllVisibleHistorySelections(true);
                    return true;
                }

                if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    historyPage.ExportSelectedFromShortcut();
                    return true;
                }

                if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
                {
                    historyPage.DeleteSelectedFromShortcut();
                    return true;
                }
                break;
            case NavPage.Bundles when ResolvePage(NavPage.Bundles) is BundlesPage bundlesPage:
                if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    bundlesPage.FocusFirstAutomationRule();
                    return true;
                }
                break;
            case NavPage.Toolbox when ResolvePage(NavPage.Toolbox) is ToolboxPage toolboxPage:
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
                {
                    _ = toolboxPage.OpenFocusedToolAsync();
                    return true;
                }
                break;
        }

        return false;
    }

    private void StartHealthMonitoringIfNeeded()
    {
        if (_healthMonitor is null)
            return;

        if (!_healthMonitorStarted)
        {
            _healthMonitorStarted = true;
            if (_healthMonitor is HealthMonitorService concreteMonitor)
            {
                concreteMonitor.AlertRaised += HealthMonitor_AlertRaised;
                concreteMonitor.AlertsChanged += HealthMonitor_AlertsChanged;
            }
        }

        if (!_vm.HealthMonitoringEnabled)
        {
            _vm.SyncHealthAlerts([]);
            UpdateTrayState();
            return;
        }

        if (_healthMonitorCts is null || _healthMonitorCts.IsCancellationRequested)
            _healthMonitorCts = new CancellationTokenSource();

        _ = _healthMonitor.StartAsync(_healthMonitorCts.Token);
        SyncHealthAlertsFromMonitor();
    }

    private void ToggleHealthMonitoring(bool enabled)
    {
        if (_healthMonitor is null)
            return;

        if (enabled)
        {
            StartHealthMonitoringIfNeeded();
            return;
        }

        _healthMonitor.Stop();
        _vm.SyncHealthAlerts([]);
        UpdateTrayState();
    }

    private void HealthMonitor_AlertRaised(object? sender, HealthAlert alert)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            SyncHealthAlertsFromMonitor();
            QueueHealthAlertNotification(alert);
        }));
    }

    private void HealthMonitor_AlertsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(SyncHealthAlertsFromMonitor));
    }

    private void SyncHealthAlertsFromMonitor()
    {
        if (_healthMonitor is null)
            return;

        _vm.SyncHealthAlerts(_healthMonitor.GetActiveAlerts());
        UpdateTrayState();
    }

    private void QueueHealthAlertNotification(HealthAlert alert)
    {
        if (_trayIcon is null || !ShouldShowHealthAlertNotification(alert))
            return;

        EnqueueBalloon(new QueuedBalloonNotification(
            alert.Id,
            $"{_vm.ProductDisplayName} - {alert.Title}",
            TruncateBalloonBody(alert.Body),
            alert.Severity));
    }

    private bool ShouldShowHealthAlertNotification(HealthAlert alert)
    {
        if (!_vm.HealthMonitoringEnabled || !_vm.ShowHealthAlertTrayNotifications)
            return false;

        if (IsFocusAssistActive())
            return false;

        if (!IsSeverityAllowedByFrequency(alert.Severity))
            return false;

        if (alert.Severity == AlertSeverity.Warning)
        {
            var lastOpenedUtc = _shellPresence?.LastAppOpenUtc
                ?? _vm.Settings.LastAppInteractionUtc
                ?? DateTime.UtcNow;

            if (DateTime.UtcNow - lastOpenedUtc <= TimeSpan.FromHours(2))
                return false;
        }

        if (alert.Severity == AlertSeverity.Info
            && _balloonShownAtByAlertId.TryGetValue(alert.Id, out var lastShownUtc)
            && DateTime.UtcNow - lastShownUtc < TimeSpan.FromDays(1))
        {
            return false;
        }

        return true;
    }

    private bool IsSeverityAllowedByFrequency(AlertSeverity severity)
    {
        return _vm.Settings.HealthAlertNotificationFrequency switch
        {
            HealthAlertNotificationFrequency.CriticalOnly => severity == AlertSeverity.Critical,
            HealthAlertNotificationFrequency.WarningsAndCritical => severity is AlertSeverity.Warning or AlertSeverity.Critical,
            _ => true
        };
    }

    private void EnqueueBalloon(QueuedBalloonNotification notification)
    {
        _balloonQueue.Enqueue(notification);
        _balloonPumpTask ??= PumpBalloonQueueAsync();
    }

    private async Task PumpBalloonQueueAsync()
    {
        while (_balloonQueue.Count > 0 && _trayIcon is not null)
        {
            var notification = _balloonQueue.Dequeue();
            _pendingBalloonAlertId = notification.AlertId;
            _balloonShownAtByAlertId[notification.AlertId] = DateTime.UtcNow;

            var hideAfter = !_trayIcon.Visible && IsVisible && WindowState != WindowState.Minimized;
            _trayIcon.Visible = true;
            _trayIcon.ShowBalloonTip(
                notification.Severity == AlertSeverity.Critical ? 10000 : 5000,
                notification.Title,
                notification.Body,
                notification.Severity == AlertSeverity.Critical
                    ? System.Windows.Forms.ToolTipIcon.Error
                    : notification.Severity == AlertSeverity.Warning
                        ? System.Windows.Forms.ToolTipIcon.Warning
                        : System.Windows.Forms.ToolTipIcon.Info);

            await Task.Delay(notification.Severity == AlertSeverity.Critical ? 10000 : 5000);
            await Task.Delay(5000);

            if (hideAfter)
                _trayIcon.Visible = false;
        }

        _pendingBalloonAlertId = "";
        _balloonPumpTask = null;
    }

    private void OpenPendingBalloonAlert()
    {
        RestoreFromTray();
        SelectPage(NavPage.Dashboard);
        if (!string.IsNullOrWhiteSpace(_pendingBalloonAlertId))
            _vm.HighlightHealthAlert(_pendingBalloonAlertId);
    }

    private async Task GenerateWeeklySummaryIfDueAsync()
    {
        if (_weeklySummaryService is null || !_vm.SendWeeklyHealthSummary || !_weeklySummaryService.IsSummaryDueToday())
            return;

        try
        {
            var summary = _weeklySummaryService.Generate();
            _weeklySummaryService.Save(summary);
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                _vm.ReloadHistoryWorkspace();
                if (_trayIcon is not null && !IsFocusAssistActive())
                {
                    EnqueueBalloon(new QueuedBalloonNotification(
                        $"weekly-summary-{summary.WeekEndingUtc:yyyyMMdd}",
                        $"{_vm.ProductDisplayName} - Weekly summary ready",
                        "Your weekly health summary is ready.",
                        AlertSeverity.Info));
                }
            }));
        }
        catch (Exception ex)
        {
            _logger.Error("Weekly health summary generation failed", ex);
        }
    }

    private static string TruncateBalloonBody(string value)
        => value.Length <= 200 ? value : $"{value[..197]}...";

    private static bool IsFocusAssistActive()
        => NativeHealthNotificationMethods.SHQueryUserNotificationState(out var state) == 0
           && state != QueryUserNotificationState.QunsAcceptsNotifications;

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_vm.IsCommandPaletteOpen)
            {
                _vm.CloseCommandPalette();
                e.Handled = true;
                return;
            }

            if (NotificationsPopup.IsOpen)
            {
                CloseNotifications();
                e.Handled = true;
                return;
            }
        }

        switch (ResolveGlobalShortcut(e.Key, Keyboard.Modifiers, IsTextInputFocused()))
        {
            case ShellShortcutAction.OpenHelp:
                OpenCurrentPageHelp();
                e.Handled = true;
                return;
            case ShellShortcutAction.OpenKeyboardShortcutsDialog:
                _vm.OpenKeyboardShortcutsDialog();
                e.Handled = true;
                return;
            case ShellShortcutAction.None:
                break;
            case ShellShortcutAction.OpenGlobalSearch:
                OpenCommandPaletteAndFocus();
                e.Handled = true;
                return;
            case ShellShortcutAction.RefreshCurrentPage:
                _ = RefreshCurrentPageAsync();
                e.Handled = true;
                return;
            case ShellShortcutAction.NavigateHistory:
                SelectPage(NavPage.History);
                e.Handled = true;
                return;
            case ShellShortcutAction.NavigateFixCenter:
                SelectPage(_vm.SimplifiedModeEnabled ? NavPage.FixMyPc : NavPage.Fixes);
                e.Handled = true;
                return;
            case ShellShortcutAction.NavigateDashboard:
                SelectPage(NavPage.Dashboard);
                e.Handled = true;
                return;
            case ShellShortcutAction.NavigateSettings:
                SelectPage(NavPage.Settings);
                e.Handled = true;
                return;
            case ShellShortcutAction.NavigateSupportPackage:
                SelectPage(NavPage.Handoff);
                e.Handled = true;
                return;
            case ShellShortcutAction.RunLastUsefulAction:
                _ = _vm.RunLastUsefulActionAsync();
                e.Handled = true;
                return;
            case ShellShortcutAction.NavigateBack:
                NavigateBack();
                e.Handled = true;
                return;
        }

        if (IsTextInputFocused())
            return;

        if (TryHandlePageSpecificShortcut(e))
            e.Handled = true;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_vm.HasActiveWork && _vm.Settings.ConfirmBeforeClosingActiveWork && !_allowExit)
        {
            if (_vm.Settings.MinimizeToTray)
            {
                var choice = System.Windows.MessageBox.Show(
                    $"{_vm.ActiveWorkSummary}\n\nYes keeps {_vm.ProductDisplayName} running in the tray.\nNo stays in the app.\nCancel closes {_vm.ProductDisplayName} anyway.",
                    $"{_vm.ProductDisplayName} is still working",
                    System.Windows.MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (choice == System.Windows.MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    MinimizeToTray();
                    return;
                }

                if (choice == System.Windows.MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            else
            {
                var choice = System.Windows.MessageBox.Show(
                    $"{_vm.ActiveWorkSummary}\n\nClose {_vm.ProductDisplayName} anyway?",
                    $"{_vm.ProductDisplayName} is still working",
                    System.Windows.MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (choice != System.Windows.MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        if (_vm.Settings.MinimizeToTray && !_allowExit)
        {
            e.Cancel = true;
            MinimizeToTray();
            return;
        }

        if (WindowState == WindowState.Normal)
        {
            _vm.Settings.WindowLeft = Left;
            _vm.Settings.WindowTop = Top;
        }

        _healthMonitor?.Stop();
        _healthMonitorCts?.Cancel();
        _healthMonitorCts?.Dispose();
        if (_healthMonitor is HealthMonitorService concreteMonitor)
        {
            concreteMonitor.AlertRaised -= HealthMonitor_AlertRaised;
            concreteMonitor.AlertsChanged -= HealthMonitor_AlertsChanged;
        }

        _trayIcon?.Dispose();
        _vm.PropertyChanged -= ViewModel_PropertyChanged;
        _vm.SaveSettings();
    }

    private static void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"The app could not open that item.\n\n{ex.Message}",
                "Open Item",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void PrewarmShellSurfaces()
    {
        try
        {
            _ = ResolvePage(NavPage.Fixes);
            _ = ResolvePage(NavPage.Bundles);
            _ = ResolvePage(NavPage.History);
            _vm.PrimeCommandPaletteCache();
            CommandPaletteBox.ApplyTemplate();
            CommandPaletteBox.UpdateLayout();
            _logger.Info("Idle shell warmup completed.");
        }
        catch (Exception ex)
        {
            _logger.Error("Idle shell warmup failed", ex);
        }
    }

    private void OpenCommandPaletteAndFocus()
    {
        var stopwatch = Stopwatch.StartNew();
        _vm.OpenCommandPalette();
        Dispatcher.BeginInvoke(() =>
        {
            CommandPaletteBox.Focus();
            _logger.Info($"Command palette opened in {stopwatch.ElapsedMilliseconds} ms.");
        }, DispatcherPriority.Loaded);
    }

    private NavPage NormalizePageForCurrentMode(NavPage page)
    {
        if (_vm.SimplifiedModeEnabled && page == NavPage.Fixes)
            return NavPage.FixMyPc;

        if (!_vm.SimplifiedModeEnabled && page == NavPage.FixMyPc)
            return NavPage.Fixes;

        return page;
    }

    private static string BuildSimplifiedFixConfirmationText(FixItem fix)
    {
        var timeText = fix.EstimatedDurationSeconds > 0
            ? $"about {Math.Max(1, fix.EstimatedDurationSeconds / 60.0):0.#} minute{(fix.EstimatedDurationSeconds >= 90 ? "s" : "")}"
            : "a short time";
        var description = string.IsNullOrWhiteSpace(fix.Description)
            ? "try a built-in repair for this problem"
            : char.ToLowerInvariant(fix.Description[0]) + fix.Description[1..];

        if (fix.RiskLevel == FixRiskLevel.MayRestart)
            return $"This will {description}. Your PC will restart when it finishes. Save anything you're working on before continuing. It takes {timeText}.";

        if (fix.RiskLevel == FixRiskLevel.Advanced)
            return $"This will {description}. It takes {timeText}. This is a more advanced fix. If you're not sure, you can always create a support package and ask for help.";

        return $"This will {description}. It takes {timeText}.";
    }

    private static string BuildSimplifiedRunbookConfirmationText(RunbookDefinition runbook)
    {
        var timeText = runbook.EstimatedDurationSeconds > 0
            ? $"about {Math.Max(1, runbook.EstimatedDurationSeconds / 60.0):0.#} minute{(runbook.EstimatedDurationSeconds >= 90 ? "s" : "")}"
            : "a short time";
        var description = string.IsNullOrWhiteSpace(runbook.Description)
            ? "run a guided repair flow for this problem"
            : char.ToLowerInvariant(runbook.Description[0]) + runbook.Description[1..];

        if (runbook.RiskLevel == FixRiskLevel.MayRestart)
            return $"This will {description}. Your PC will restart when it finishes. Save anything you're working on before continuing. It takes {timeText}.";

        if (runbook.RiskLevel == FixRiskLevel.Advanced)
            return $"This will {description}. It takes {timeText}. This is a more advanced fix. If you're not sure, you can always create a support package and ask for help.";

        return $"This will {description}. It takes {timeText}.";
    }

    private static void ShowSimpleHelpPopover(FrameworkElement anchor, string message, string automationId)
    {
        var menu = new System.Windows.Controls.ContextMenu
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Right,
            StaysOpen = false
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(menu, automationId);
        menu.Items.Add(new System.Windows.Controls.MenuItem
        {
            Header = message,
            IsEnabled = false,
            MaxWidth = 320
        });
        menu.IsOpen = true;
    }
}

internal enum QueryUserNotificationState
{
    QunsNotPresent = 1,
    QunsBusy = 2,
    QunsRunningD3dFullScreen = 3,
    QunsPresentationMode = 4,
    QunsAcceptsNotifications = 5,
    QunsQuietTime = 6,
    QunsApp = 7
}

internal static partial class NativeHealthNotificationMethods
{
    [LibraryImport("shell32.dll")]
    internal static partial int SHQueryUserNotificationState(out QueryUserNotificationState state);
}
