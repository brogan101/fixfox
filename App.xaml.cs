using System.Threading;
using System.Windows;
using HelpDesk.Application.Interfaces;
using HelpDesk.Application.Services;
using HelpDesk.Infrastructure.Services;
using HelpDesk.Presentation.ViewModels;
using HelpDesk.Presentation.Views;
using HelpDesk.Presentation.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using SharedConstants = HelpDesk.Shared.Constants;
using MsgBox = System.Windows.MessageBox;
using MsgButton = System.Windows.MessageBoxButton;
using MsgImage = System.Windows.MessageBoxImage;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace HelpDesk;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static Mutex? _singleInstanceMutex;
    private static int _unhandledDialogCount;
    private readonly bool _forceVerify;
    private ISettingsService? _settingsService;

    public App(bool forceVerify = false)
    {
        _forceVerify = forceVerify;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "FixFox_SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MsgBox.Show("FixFox is already running.", "FixFox", MsgButton.OK, MsgImage.Information);
            Shutdown(0);
            return;
        }

        try
        {
            OnStartupCore(e);
        }
        catch (Exception ex)
        {
            ReportUnhandledException(ex, "OnStartup", "FixFox could not finish startup.");
            Shutdown(1);
        }
    }

    private void OnStartupCore(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            ReportUnhandledException(ex.Exception, "DispatcherUnhandled", "An unexpected error occurred.");
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            if (ex.ExceptionObject is Exception err)
                ReportUnhandledException(err, "AppDomainUnhandled", "FixFox hit a fatal error.");
        };

        var svc = new ServiceCollection();

        Program.ConfigureCoreServices(svc, headless: false);

        svc.AddSingleton<ISnackbarService, SnackbarService>();

        svc.AddSingleton<MainViewModel>();
        svc.AddTransient<DashboardPage>();
        svc.AddTransient<FixCenterPage>();
        svc.AddTransient<FixMyPcPage>();
        svc.AddTransient<BundlesPage>();
        svc.AddTransient<SystemInfoPage>();
        svc.AddTransient<SymptomCheckerPage>();
        svc.AddTransient<ToolboxPage>();
        svc.AddTransient<HistoryPage>();
        svc.AddTransient<HandoffPage>();
        svc.AddTransient<SettingsPage>();
        svc.AddTransient<MainWindow>();

        Services = svc.BuildServiceProvider();

        _settingsService = Services.GetRequiredService<ISettingsService>();
        var settings = _settingsService.Load();
        settings.LastLaunchUtc = DateTime.UtcNow;
        settings.LastLaunchedVersion = SharedConstants.AppVersion;
        settings.LastSessionEndedCleanly = false;
        _settingsService.Save(settings);
        SwitchTheme(settings.Theme);

        var window = Services.GetRequiredService<MainWindow>();
        var vm = Services.GetRequiredService<MainViewModel>();
        vm.ForceShowVerifyPanel = _forceVerify;

        Services.GetRequiredService<IAppLogger>().Info("FixFox starting");
        MainWindow = window;
        window.Show();
        window.Activate();
    }

    private static void ReportUnhandledException(Exception ex, string context, string headline)
    {
        try { Services?.GetService<IAppLogger>()?.Error(context, ex); } catch { }
        try { Services?.GetService<ICrashLogger>()?.Log(ex, context); } catch { }
        try { if (Services is null) new CrashLogger().Log(ex, context); } catch { }

        var dialogNumber = Interlocked.Increment(ref _unhandledDialogCount);
        var message = dialogNumber == 1
            ? $"{headline}\n\n{ex.Message}\n\nA crash report was written to AppData\\Roaming\\FixFox\\crashes."
            : $"{headline}\n\n{ex.Message}\n\nAdditional errors are being written to the FixFox crash log.";

        MsgBox.Show(message, "FixFox", MsgButton.OK, MsgImage.Error);
    }

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
        if (existing is not null)
            merged.Remove(existing);

        var src = theme == "Light" ? "Themes/Light.xaml" : "Themes/Dark.xaml";
        merged.Add(new ResourceDictionary { Source = new Uri(src, UriKind.Relative) });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services?.GetService<IAppLogger>()?.Info("FixFox shutting down");
        try
        {
            var settingsService = _settingsService ?? Services?.GetService<ISettingsService>();
            if (settingsService is not null)
            {
                var settings = settingsService.Load();
                settings.LastSessionEndedCleanly = true;
                settings.LastCleanShutdownUtc = DateTime.UtcNow;
                settingsService.Save(settings);
            }
        }
        catch
        {
        }
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        if (Services is IDisposable d)
            d.Dispose();
        base.OnExit(e);
    }
}
