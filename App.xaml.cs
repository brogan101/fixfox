using System.Threading;
using System.Windows;
using MsgBox    = System.Windows.MessageBox;
using MsgButton = System.Windows.MessageBoxButton;
using MsgImage  = System.Windows.MessageBoxImage;
using HelpDesk.Application.Interfaces;
using HelpDesk.Application.Services;
using HelpDesk.Infrastructure.Services;
using HelpDesk.Presentation.ViewModels;
using HelpDesk.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace HelpDesk;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static Mutex? _singleInstanceMutex;
    private readonly bool _forceVerify;

    public App(bool forceVerify = false)
    {
        _forceVerify = forceVerify;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // ── Single-instance guard ────────────────────────────────────────────
        _singleInstanceMutex = new Mutex(true, "FixFox_SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MsgBox.Show("FixFox is already running.", "FixFox",
                MsgButton.OK, MsgImage.Information);
            Shutdown(0);
            return;
        }

        try { OnStartupCore(e); }
        catch (Exception ex)
        {
            // Write crash log before showing dialog
            try
            {
                var crashSvc = new CrashLogger();
                crashSvc.Log(ex, "OnStartup");
            }
            catch { }

            MsgBox.Show(ex.ToString(), "FixFox — Startup Error", MsgButton.OK, MsgImage.Error);
            Shutdown(1);
        }
    }

    private void OnStartupCore(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Unhandled exception guards ───────────────────────────────────────
        DispatcherUnhandledException += (_, ex) =>
        {
            try { Services?.GetService<ICrashLogger>()?.Log(ex.Exception, "DispatcherUnhandled"); }
            catch { }
            MsgBox.Show(
                $"An unexpected error occurred:\n\n{ex.Exception.Message}",
                "FixFox", MsgButton.OK, MsgImage.Error);
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            if (ex.ExceptionObject is Exception err)
            {
                try { Services?.GetService<ICrashLogger>()?.Log(err, "AppDomainUnhandled"); }
                catch { }
                MsgBox.Show($"Fatal error: {err.Message}", "FixFox",
                    MsgButton.OK, MsgImage.Error);
            }
        };

        var svc = new ServiceCollection();

        Program.ConfigureCoreServices(svc, headless: false);

        // WPF-UI services
        svc.AddSingleton<ISnackbarService, SnackbarService>();

        // Presentation
        svc.AddSingleton<MainViewModel>();
        svc.AddTransient<MainWindow>();

        Services = svc.BuildServiceProvider();

        // Load settings and apply persisted theme before the window opens
        var settings = Services.GetRequiredService<ISettingsService>().Load();
        SwitchTheme(settings.Theme);

        var window = Services.GetRequiredService<MainWindow>();

        // Pass the --verify flag into the ViewModel so the Dashboard can show the panel
        var vm = Services.GetRequiredService<MainViewModel>();
        vm.ForceShowVerifyPanel = _forceVerify;

        var logger = Services.GetRequiredService<IAppLogger>();
        logger.Info("FixFox starting");

        window.Show();
    }

    /// <summary>Switches between Dark and Light themes at runtime.</summary>
    public static void SwitchTheme(string theme)
    {
        var appTheme = theme == "Light"
            ? ApplicationTheme.Light
            : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(appTheme);

        var merged = Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d =>
            d.Source?.OriginalString?.Contains("Themes/") == true &&
            (d.Source.OriginalString.Contains("Dark.xaml") || d.Source.OriginalString.Contains("Light.xaml")));
        if (existing is not null) merged.Remove(existing);

        var src = theme == "Light" ? "Themes/Light.xaml" : "Themes/Dark.xaml";
        merged.Add(new ResourceDictionary { Source = new Uri(src, UriKind.Relative) });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services?.GetService<IAppLogger>()?.Info("FixFox shutting down");
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        if (Services is IDisposable d) d.Dispose();
        base.OnExit(e);
    }
}
