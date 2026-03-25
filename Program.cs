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

        var automationIndex = Array.FindIndex(args, a => string.Equals(a, "--run-automation", StringComparison.OrdinalIgnoreCase));
        if (automationIndex >= 0 && automationIndex < args.Length - 1)
        {
            Environment.Exit(RunAutomationHeadlessAsync(args[automationIndex + 1]).GetAwaiter().GetResult());
            return;
        }

        var forceVerify = args.Contains("--verify", StringComparer.OrdinalIgnoreCase);

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
                System.Windows.MessageBox.Show(
                    $"FixFox could not start.\n\n{ex.Message}\n\nA crash report was written to AppData\\Roaming\\FixFox\\crashes.",
                    "FixFox",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }, 64 * 1024 * 1024);

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
        var catalog = services.GetRequiredService<IFixCatalogService>();
        var logger = services.GetRequiredService<IAppLogger>();
        var elevation = services.GetRequiredService<IElevationService>();
        var repairExecution = services.GetRequiredService<IRepairExecutionService>();
        var runbookCatalog = services.GetRequiredService<IRunbookCatalogService>();
        var runbookExecution = services.GetRequiredService<IRunbookExecutionService>();

        var bundle = catalog.Bundles.FirstOrDefault(b =>
            string.Equals(b.Id, bundleId, StringComparison.OrdinalIgnoreCase));
        if (bundle is null)
        {
            Console.WriteLine($"Bundle not found: {bundleId}");
            return 2;
        }

        logger.Info($"Running bundle headless: {bundle.Id}");
        Console.WriteLine($"Running bundle: {bundle.Title}");

        var mappedRunbook = runbookCatalog.Runbooks.FirstOrDefault(runbook =>
            string.Equals(runbook.Id, bundle.Id, StringComparison.OrdinalIgnoreCase));
        if (mappedRunbook is not null)
        {
            var summary = await runbookExecution.ExecuteAsync(mappedRunbook);
            foreach (var line in summary.Timeline)
                Console.WriteLine($"  - {line}");

            Console.WriteLine();
            Console.WriteLine(summary.Summary);
            return summary.Success ? 0 : 1;
        }

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
                Console.WriteLine($"  x Missing fix: {fixId}");
                continue;
            }

            if (fix.Type == FixType.Guided)
            {
                Console.WriteLine($"  - Skipped guided fix: {fix.Title}");
                continue;
            }

            Console.WriteLine($"  - {fix.Title}");
            var result = await repairExecution.ExecuteAsync(fix);
            if (!result.Success)
            {
                failCount++;
                Console.WriteLine($"    x {result.FailureSummary}");
            }
            else
            {
                Console.WriteLine($"    v {result.Summary}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(failCount == 0
            ? "Bundle completed successfully."
            : $"Bundle completed with {failCount} failure(s).");
        return failCount == 0 ? 0 : 1;
    }

    private static async Task<int> RunAutomationHeadlessAsync(string ruleId)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        var services = BuildServices(headless: true);
        var automation = services.GetRequiredService<IAutomationCoordinatorService>();

        var receipt = await automation.RunAsync(ruleId, "Scheduled", manualOverride: false);
        Console.WriteLine($"{receipt.RuleTitle}");
        Console.WriteLine($"{receipt.Outcome}: {receipt.Summary}");
        if (!string.IsNullOrWhiteSpace(receipt.ConditionSummary))
            Console.WriteLine(receipt.ConditionSummary);
        if (!string.IsNullOrWhiteSpace(receipt.NextStep))
            Console.WriteLine(receipt.NextStep);

        return receipt.Outcome switch
        {
            AutomationRunOutcome.Completed => receipt.UserActionRequired ? 1 : 0,
            AutomationRunOutcome.Skipped => 0,
            _ => 1
        };
    }
}
