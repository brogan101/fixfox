using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.Helpers;
using HelpDesk.Presentation.ViewModels;
using HelpDesk.Presentation.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Controls;
using DrawingIcon = System.Drawing.Icon;
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

    private DashboardPage? _dashPage;
    private FixCenterPage? _fixPage;
    private BundlesPage? _bundlesPage;
    private SystemInfoPage? _sysInfoPage;
    private SymptomCheckerPage? _symptomPage;
    private HistoryPage? _historyPage;
    private HandoffPage? _handoffPage;
    private SettingsPage? _settingsPage;
    private FormsNotifyIcon? _trayIcon;
    private bool _allowExit;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _snackbar = App.Services.GetRequiredService<ISnackbarService>();
        DataContext = vm;

        Loaded += OnLoaded;
        StateChanged += OnWindowStateChanged;
        CreateTrayIcon();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestoreWindowPlacement();

        var logo = ImageHelper.GetLogoTransparent();
        if (logo is not null)
        {
            LogoImage.Source = logo;
            PrivacyLogoImage.Source = logo;
        }

        _snackbar.SetSnackbarPresenter(MainSnackbarPresenter);

        NavigateTo(NavPage.Dashboard);

        await _vm.LoadSystemInfoAsync();
        _ = _vm.LoadInstalledProgramsAsync();
        if (_vm.Settings.RunQuickScanOnLaunch)
            await _vm.RunQuickScanAsync();
    }

    private void CreateTrayIcon()
    {
        var menu = new FormsContextMenuStrip();
        menu.Items.Add(new FormsToolStripMenuItem("Open FixFox", null, (_, _) => RestoreFromTray()));
        menu.Items.Add(new FormsToolStripMenuItem("Run Quick Scan", null, async (_, _) =>
        {
            RestoreFromTray();
            await _vm.RunQuickScanAsync();
        }));
        menu.Items.Add(new FormsToolStripMenuItem("Exit", null, (_, _) =>
        {
            _allowExit = true;
            if (_trayIcon is not null)
                _trayIcon.Visible = false;
            Close();
        }));

        _trayIcon = new FormsNotifyIcon
        {
            Text = "FixFox",
            Icon = DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                ?? "FixFox.exe"),
            Visible = false,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
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
        if (_vm.Settings.ShowTrayBalloons)
        {
            _trayIcon.ShowBalloonTip(
                2500,
                "FixFox",
                "FixFox is still running in the tray.",
                System.Windows.Forms.ToolTipIcon.Info);
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

    private void NavigateTo(NavPage page)
    {
        Page target = page switch
        {
            NavPage.Dashboard => _dashPage ??= new DashboardPage(),
            NavPage.Fixes => _fixPage ??= new FixCenterPage(),
            NavPage.Bundles => _bundlesPage ??= new BundlesPage(),
            NavPage.SystemInfo => _sysInfoPage ??= new SystemInfoPage(),
            NavPage.SymptomChecker => _symptomPage ??= new SymptomCheckerPage(),
            NavPage.History => _historyPage ??= new HistoryPage(),
            NavPage.Handoff => _handoffPage ??= new HandoffPage(),
            NavPage.Settings => _settingsPage ??= new SettingsPage(),
            _ => _dashPage ??= new DashboardPage()
        };

        if (PageFrame.Content != target)
            PageFrame.Navigate(target);
    }

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

        _vm.CurrentPage = page;
        NavigateTo(page);

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
                    _ = _vm.RunFixAsync(_vm.CommandPaletteResults[0]);
                _vm.CloseCommandPalette();
                e.Handled = true;
                break;
        }
    }

    private void CommandPaletteBox_GotFocus(object sender, RoutedEventArgs e) => _vm.RefreshCommandPalette();

    private async void PaletteRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: FixItem fix })
            return;

        _vm.CloseCommandPalette();
        await _vm.RunFixAsync(fix);
    }

    private void PrivacyOk_Click(object sender, RoutedEventArgs e) => _vm.DismissPrivacyNotice();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _vm.OpenCommandPalette();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        var page = e.Key switch
        {
            Key.D1 or Key.NumPad1 => (NavPage?)NavPage.Dashboard,
            Key.D2 or Key.NumPad2 => NavPage.Fixes,
            Key.D3 or Key.NumPad3 => NavPage.Bundles,
            Key.D4 or Key.NumPad4 => NavPage.SystemInfo,
            Key.D5 or Key.NumPad5 => NavPage.SymptomChecker,
            Key.D6 or Key.NumPad6 => NavPage.Handoff,
            Key.D7 or Key.NumPad7 => NavPage.History,
            Key.D8 or Key.NumPad8 => NavPage.Settings,
            _ => null
        };

        if (!page.HasValue)
            return;

        _vm.CurrentPage = page.Value;
        NavigateTo(page.Value);
        e.Handled = true;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
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
        _vm.SaveSettings();
    }
}
