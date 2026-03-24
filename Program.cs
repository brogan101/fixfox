using HelpDesk.Application.Interfaces;
using HelpDesk.Application.Services;
using HelpDesk.Domain.Enums;
using HelpDesk.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace HelpDesk;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // ── Headless verify mode — no UI, writes results to stdout + verify log ──
        if (args.Contains("--verify-headless", StringComparer.OrdinalIgnoreCase))
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            var services = BuildServices(headless: true);
            var verifier = services.GetRequiredService<StartupVerifier>();
            verifier.RunAsync().GetAwaiter().GetResult();
            Environment.Exit(verifier.PrintHeadless());
            return;
        }

        var bundleIndex = Array.FindIndex(args, a => string.Equals(a, "--run-bundle", StringComparison.OrdinalIgnoreCase));
        if (bundleIndex >= 0 && bundleIndex < args.Length - 1)
        {
            Environment.Exit(RunBundleHeadlessAsync(args[bundleIndex + 1]).GetAwaiter().GetResult());
            return;
        }

        bool forceVerify = args.Contains("--verify", StringComparer.OrdinalIgnoreCase);

        // Run on a 64 MB stack to prevent native DirectWrite stack overflow
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var app = new App(forceVerify);
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                try { new CrashLogger().Log(ex, "Program.Main thread"); } catch { }
                System.Windows.MessageBox.Show(ex.ToString(), "FixFox — Fatal Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        },
        64 * 1024 * 1024);

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    internal static IServiceProvider BuildServices(bool headless = false)
    {
        var svc = new ServiceCollection();
        ConfigureCoreServices(svc, headless);
        return svc.BuildServiceProvider();
    }

    internal static void ConfigureCoreServices(IServiceCollection svc, bool headless)
    {
        AppServiceRegistrar.Configure(svc, headless);
    }

    private static async Task<int> RunBundleHeadlessAsync(string bundleId)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        var services = BuildServices(headless: true);
        var catalog  = services.GetRequiredService<IFixCatalogService>();
        var scripts  = services.GetRequiredService<IScriptService>();
        var log      = services.GetRequiredService<ILogService>();
        var logger   = services.GetRequiredService<IAppLogger>();
        var elevation = services.GetRequiredService<IElevationService>();

        var bundle = catalog.Bundles.FirstOrDefault(b =>
            string.Equals(b.Id, bundleId, StringComparison.OrdinalIgnoreCase));
        if (bundle is null)
        {
            Console.WriteLine($"Bundle not found: {bundleId}");
            return 2;
        }

        logger.Info($"Running bundle headless: {bundle.Id}");
        Console.WriteLine($"Running bundle: {bundle.Title}");

        var adminFixCount = bundle.FixIds
            .Select(catalog.GetById)
            .Count(f => f is not null && f.Type == FixType.Silent && f.RequiresAdmin);

        if (adminFixCount > 0 && !elevation.IsElevated)
        {
            logger.Warn($"Bundle {bundle.Id} requires elevation for {adminFixCount} fix(es).");
            Console.WriteLine($"Bundle requires administrator rights for {adminFixCount} fix(es).");
            Console.WriteLine("Re-run FixFox as administrator, or use the weekly scheduler which saves the task at highest privileges.");
            return 3;
        }

        var failCount = 0;
        foreach (var fixId in bundle.FixIds)
        {
            var fix = catalog.GetById(fixId);
            if (fix is null)
            {
                failCount++;
                Console.WriteLine($"  ✗ Missing fix: {fixId}");
                continue;
            }

            if (fix.Type == FixType.Guided)
            {
                Console.WriteLine($"  - Skipped guided fix: {fix.Title}");
                continue;
            }

            Console.WriteLine($"  - {fix.Title}");
            await scripts.RunFixAsync(fix);
            log.Record(catalog.GetCategoryTitle(fix), fix);

            if (fix.Status == FixStatus.Failed)
            {
                failCount++;
                Console.WriteLine($"    ✗ {fix.LastOutput}");
            }
            else
            {
                Console.WriteLine("    ✓ Done");
            }
        }

        Console.WriteLine();
        Console.WriteLine(failCount == 0
            ? "Bundle completed successfully."
            : $"Bundle completed with {failCount} failure(s).");
        return failCount == 0 ? 0 : 1;
    }
}
