using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.Helpers;
using HelpDesk.Presentation.ViewModels;
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

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _vm;
    private readonly ISnackbarService _snackbar;
    private readonly IAppLogger _logger;
    private readonly IServiceProvider _services;
    private readonly Dictionary<NavPage, System.Windows.Controls.Page> _pageCache = [];
    private FormsNotifyIcon? _trayIcon;
    private bool _allowExit;
    private bool _trayBalloonShown;
    private bool _startupRendered;

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
        DataContext = vm;

        Loaded += OnLoaded;
        ContentRendered += OnContentRendered;
        StateChanged += OnWindowStateChanged;
        _vm.PropertyChanged += ViewModel_PropertyChanged;
        CreateTrayIcon();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
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
            var iconPath = Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                ?? "FixFox.exe";
            var icon = DrawingIcon.ExtractAssociatedIcon(iconPath) ?? DrawingSystemIcons.Application;

            _trayIcon = new FormsNotifyIcon
            {
                Text = _vm.ProductDisplayName,
                Icon = icon,
                Visible = false
            };
            _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
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
            or nameof(MainViewModel.AutomationAttentionCount))
        {
            UpdateTrayState();
        }

        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
            NavigateTo(_vm.CurrentPage);
    }

    private void UpdateTrayState()
    {
        if (_trayIcon is null)
            return;

        var status = string.IsNullOrWhiteSpace(_vm.ShellStatusText)
            ? "Ready"
            : _vm.ShellStatusText;
        var tooltip = $"{_vm.ProductDisplayName} - {status}";
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
            _logger.Info($"Startup background work completed in {startupWorkStopwatch.ElapsedMilliseconds} ms.");
        }
        catch (Exception ex)
        {
            _logger.Error("Startup background work failed", ex);
        }
    }

    private void SelectPage(NavPage page, bool runPageActivation = true)
    {
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
                if (_vm.CommandPaletteResults.Count > 0)
                    _ = RunPaletteSelectionAsync(_vm.CommandPaletteResults[0]);
                _vm.CloseCommandPalette();
                e.Handled = true;
                break;
        }
    }

    private void CommandPaletteBox_GotFocus(object sender, RoutedEventArgs e) => _vm.RefreshCommandPalette();

    private async void PaletteRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: CommandPaletteItem item })
            return;

        await RunPaletteSelectionAsync(item);
    }

    private async Task RunPaletteSelectionAsync(CommandPaletteItem item)
    {
        _vm.CloseCommandPalette();
        if (item.Kind == CommandPaletteItemKind.Page && item.TargetPage.HasValue)
        {
            SelectPage(item.TargetPage.Value);
            return;
        }

        await _vm.ExecuteCommandPaletteItemAsync(item);
    }

    private async void PrivacyOk_Click(object sender, RoutedEventArgs e) => await _vm.CompleteOnboardingAsync();

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

    private void OpenCommandPaletteButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.OpenCommandPalette();
        Dispatcher.BeginInvoke(() => CommandPaletteBox.Focus());
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

        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _vm.OpenCommandPalette();
            Dispatcher.BeginInvoke(() => CommandPaletteBox.Focus());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            _ = _vm.RunQuickScanAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.M && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            _ = _vm.RunRecommendedMaintenanceAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.E && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            _ = _vm.CreateEvidenceBundleAsync();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        var page = e.Key switch
        {
            Key.D1 or Key.NumPad1 => (NavPage?)NavPage.Dashboard,
            Key.D2 or Key.NumPad2 => NavPage.SymptomChecker,
            Key.D3 or Key.NumPad3 => NavPage.Fixes,
            Key.D4 or Key.NumPad4 => NavPage.Bundles,
            Key.D5 or Key.NumPad5 => NavPage.SystemInfo,
            Key.D6 or Key.NumPad6 => NavPage.Handoff,
            Key.D7 or Key.NumPad7 => NavPage.History,
            Key.D8 or Key.NumPad8 => NavPage.Toolbox,
            Key.D9 or Key.NumPad9 => NavPage.Settings,
            _ => null
        };

        if (!page.HasValue)
            return;

        SelectPage(page.Value);
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
}
