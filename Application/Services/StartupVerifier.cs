using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Resources;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain;
using HelpDesk.Domain.Enums;
using Newtonsoft.Json;

namespace HelpDesk.Application.Services;

public enum VerifyStatus
{
    Pass,
    Warn,
    Fail
}

public sealed record VerifyResult(string Name, VerifyStatus Status, string Detail, long DurationMs)
{
    public string StatusIcon => Status switch
    {
        VerifyStatus.Pass => "PASS",
        VerifyStatus.Warn => "WARN",
        VerifyStatus.Fail => "FAIL",
        _ => "UNKNOWN",
    };
}

public sealed class VerifyReport
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public IReadOnlyList<VerifyResult> Checks { get; init; } = [];
    public int PassCount => Checks.Count(c => c.Status == VerifyStatus.Pass);
    public int WarnCount => Checks.Count(c => c.Status == VerifyStatus.Warn);
    public int FailCount => Checks.Count(c => c.Status == VerifyStatus.Fail);
    public bool AllPassed => FailCount == 0;
}

public sealed class StartupVerifier
{
    private readonly ISettingsService _settings;
    private readonly IFixCatalogService _catalog;
    private readonly IRepairCatalogService _repairCatalog;
    private readonly IRunbookCatalogService _runbooks;
    private readonly IKnowledgeBaseService _knowledgeBase;
    private readonly IEditionCapabilityService _edition;
    private readonly IAppUpdateService _updates;
    private readonly IAppLogger _log;

    public IReadOnlyList<VerifyResult> Results { get; private set; } = [];

    public StartupVerifier(
        ISettingsService settings,
        IFixCatalogService catalog,
        IRepairCatalogService repairCatalog,
        IRunbookCatalogService runbooks,
        IKnowledgeBaseService knowledgeBase,
        IEditionCapabilityService edition,
        IAppUpdateService updates,
        IAppLogger log)
    {
        _settings = settings;
        _catalog = catalog;
        _repairCatalog = repairCatalog;
        _runbooks = runbooks;
        _knowledgeBase = knowledgeBase;
        _edition = edition;
        _updates = updates;
        _log = log;
    }

    public async Task<VerifyReport> RunAsync()
    {
        var results = new List<VerifyResult>
        {
            Check("Settings readable", CheckSettingsReadable),
            Check("Settings writable", CheckSettingsWritable),
            Check("History file", CheckHistoryFile),
            Check("Notifications file", CheckNotificationsFile),
            Check("FixFoxLogo resource", CheckLogoResource),
            Check("Fix catalog builds", CheckFixCatalog),
            Check("Fix IDs unique", CheckFixIdsUnique),
            Check("Bundle refs valid", CheckBundleRefs),
            Check("Silent fixes have script", CheckSilentScripts),
            Check("Guided fixes have steps", CheckGuidedSteps),
            Check("Script temp dir writable", CheckScriptTempDir),
            Check("PowerShell reachable", CheckPowerShell),
            Check("LibreHardwareMonitor", CheckLibreHardware),
            Check("WMI Win32_OperatingSystem", CheckWmiOs),
            Check("WMI Win32_Processor", CheckWmiCpu),
            Check("Disk C readable", CheckDiskC),
            await CheckAsync("Internet (ping 8.8.8.8)", CheckInternet),
            Check("Windows version", CheckWindowsVersion),
            Check("x64 architecture", CheckX64),
            Check(".NET 8 runtime", CheckDotNet8),
            Check("Sufficient temp space", CheckTempSpace),
            Check("Nav enum no duplicates", CheckNavEnumDupes),
            Check("Theme resources load", CheckThemeResources),
            Check("Converters instantiate", CheckConverters),
            Check("Repair catalog metadata", CheckRepairCatalogMetadata),
            Check("Runbooks registered", CheckRunbookCatalog),
            Check("Knowledge base ready", CheckKnowledgeBase),
            Check("Edition capabilities", CheckEditionCapabilities),
            await CheckAsync("Update provider ready", CheckUpdateProviderAsync)
        };

        Results = results.AsReadOnly();
        WriteLog();

        var report = new VerifyReport { Checks = Results };
        _log.Info($"Startup verification: {report.PassCount} pass, {report.WarnCount} warn, {report.FailCount} fail");
        return report;
    }

    private static VerifyResult Check(string name, Func<(VerifyStatus Status, string Detail)> fn)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var (status, detail) = fn();
            return new VerifyResult(name, status, detail, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new VerifyResult(name, VerifyStatus.Fail, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    private static async Task<VerifyResult> CheckAsync(string name, Func<Task<(VerifyStatus Status, string Detail)>> fn)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var (status, detail) = await fn();
            return new VerifyResult(name, status, detail, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new VerifyResult(name, VerifyStatus.Fail, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    private (VerifyStatus, string) CheckSettingsReadable()
    {
        var settings = _settings.Load();
        return settings is not null
            ? (VerifyStatus.Pass, "Settings loaded successfully")
            : (VerifyStatus.Fail, "Settings.Load() returned null");
    }

    private (VerifyStatus, string) CheckSettingsWritable()
    {
        var settings = _settings.Load();
        var originalTheme = settings.Theme;
        settings.Theme = string.Equals(settings.Theme, "Dark", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        _settings.Save(settings);
        var reloaded = _settings.Load();
        settings.Theme = originalTheme;
        _settings.Save(settings);

        return !string.Equals(reloaded.Theme, originalTheme, StringComparison.OrdinalIgnoreCase)
            ? (VerifyStatus.Pass, "Settings round-trip succeeded")
            : (VerifyStatus.Fail, "Settings round-trip value did not change");
    }

    private static (VerifyStatus, string) CheckHistoryFile()
    {
        var path = Path.Combine(HelpDesk.Shared.Constants.AppDataDir, Domain.Constants.HistoryFile);
        if (!File.Exists(path))
            return (VerifyStatus.Pass, "History file does not exist yet");

        JsonConvert.DeserializeObject(File.ReadAllText(path));
        return (VerifyStatus.Pass, $"Readable ({new FileInfo(path).Length / 1024} KB)");
    }

    private static (VerifyStatus, string) CheckNotificationsFile()
    {
        var path = Path.Combine(HelpDesk.Shared.Constants.AppDataDir, Domain.Constants.NotifFile);
        if (!File.Exists(path))
            return (VerifyStatus.Pass, "Notifications file does not exist yet");

        JsonConvert.DeserializeObject(File.ReadAllText(path));
        return (VerifyStatus.Pass, $"Readable ({new FileInfo(path).Length / 1024} KB)");
    }

    private static (VerifyStatus, string) CheckLogoResource()
    {
        return HasCompiledWpfResource("fixfoxlogo.png")
            ? (VerifyStatus.Pass, "Logo resource found in compiled WPF resources")
            : (VerifyStatus.Fail, "FixFoxLogo.png not found in compiled WPF resources");
    }

    private (VerifyStatus, string) CheckFixCatalog()
    {
        var count = _catalog.Categories.Sum(category => category.Fixes.Count);
        return count >= 100
            ? (VerifyStatus.Pass, $"{count} fixes in catalog across {_catalog.Categories.Count} categories")
            : (VerifyStatus.Warn, $"Only {count} fixes found");
    }

    private (VerifyStatus, string) CheckFixIdsUnique()
    {
        var ids = _catalog.Categories.SelectMany(category => category.Fixes).Select(fix => fix.Id).ToList();
        var duplicates = ids
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        return duplicates.Count == 0
            ? (VerifyStatus.Pass, $"{ids.Count} unique fix IDs")
            : (VerifyStatus.Fail, $"Duplicate IDs: {string.Join(", ", duplicates.Take(5))}");
    }

    private (VerifyStatus, string) CheckBundleRefs()
    {
        var allIds = _catalog.Categories.SelectMany(category => category.Fixes).Select(fix => fix.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var deadRefs = new List<string>();

        foreach (var bundle in _catalog.Bundles)
        {
            foreach (var fixId in bundle.FixIds)
            {
                if (!allIds.Contains(fixId))
                    deadRefs.Add($"{bundle.Id}->{fixId}");
            }
        }

        return deadRefs.Count == 0
            ? (VerifyStatus.Pass, $"{_catalog.Bundles.Count} bundles, all refs valid")
            : (VerifyStatus.Warn, $"Dead bundle refs: {string.Join(", ", deadRefs.Take(5))}");
    }

    private (VerifyStatus, string) CheckSilentScripts()
    {
        var bad = _catalog.Categories
            .SelectMany(category => category.Fixes)
            .Where(fix => fix.Type == FixType.Silent && string.IsNullOrWhiteSpace(fix.Script))
            .Select(fix => fix.Id)
            .ToList();

        return bad.Count == 0
            ? (VerifyStatus.Pass, "All silent fixes have scripts")
            : (VerifyStatus.Fail, $"Missing scripts: {string.Join(", ", bad.Take(5))}");
    }

    private (VerifyStatus, string) CheckGuidedSteps()
    {
        var bad = _catalog.Categories
            .SelectMany(category => category.Fixes)
            .Where(fix => fix.Type == FixType.Guided && (fix.Steps is null || fix.Steps.Count == 0))
            .Select(fix => fix.Id)
            .ToList();

        return bad.Count == 0
            ? (VerifyStatus.Pass, "All guided fixes have steps")
            : (VerifyStatus.Fail, $"Missing steps: {string.Join(", ", bad.Take(5))}");
    }

    private static (VerifyStatus, string) CheckScriptTempDir()
    {
        var dir = Path.Combine(HelpDesk.Shared.Constants.TempDir, "verify_test");
        Directory.CreateDirectory(dir);
        var probe = Path.Combine(dir, "probe.txt");
        File.WriteAllText(probe, "ok");
        File.Delete(probe);
        Directory.Delete(dir);
        return (VerifyStatus.Pass, "Temp directory is writable");
    }

    private static (VerifyStatus, string) CheckPowerShell()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-NoProfile -Command \"$PSVersionTable.PSVersion.ToString()\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        process?.WaitForExit(3000);
        var output = process?.StandardOutput.ReadToEnd().Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(output)
            ? (VerifyStatus.Pass, $"PowerShell {output.Split('\n', '\r').FirstOrDefault()?.Trim()}")
            : (VerifyStatus.Fail, "powershell.exe not found in PATH");
    }

    private static (VerifyStatus, string) CheckLibreHardware()
    {
        var computer = new LibreHardwareMonitor.Hardware.Computer { IsCpuEnabled = true };
        computer.Open();
        var count = computer.Hardware.Count;
        computer.Close();
        return (VerifyStatus.Pass, $"Initialized, {count} hardware component(s) found");
    }

    private static (VerifyStatus, string) CheckWmiOs()
    {
        using var query = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
        foreach (ManagementObject result in query.Get())
            return (VerifyStatus.Pass, result["Caption"]?.ToString() ?? "OK");

        return (VerifyStatus.Fail, "No results returned");
    }

    private static (VerifyStatus, string) CheckWmiCpu()
    {
        using var query = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
        foreach (ManagementObject result in query.Get())
            return (VerifyStatus.Pass, result["Name"]?.ToString()?.Trim() ?? "OK");

        return (VerifyStatus.Fail, "No results returned");
    }

    private static (VerifyStatus, string) CheckDiskC()
    {
        var drive = new DriveInfo("C");
        if (!drive.IsReady)
            return (VerifyStatus.Fail, "C: drive is not ready");

        var freeGb = drive.AvailableFreeSpace / 1_073_741_824.0;
        return (VerifyStatus.Pass, $"C: is ready, {freeGb:N1} GB free");
    }

    private static async Task<(VerifyStatus, string)> CheckInternet()
    {
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 2000);
            return reply.Status == System.Net.NetworkInformation.IPStatus.Success
                ? (VerifyStatus.Pass, $"Reachable ({reply.RoundtripTime}ms)")
                : (VerifyStatus.Warn, $"Unreachable, status: {reply.Status}");
        }
        catch
        {
            return (VerifyStatus.Warn, "Ping failed, internet may be unavailable or ICMP may be blocked");
        }
    }

    private static (VerifyStatus, string) CheckWindowsVersion()
    {
        var version = Environment.OSVersion.Version;
        if (version.Major < 10)
            return (VerifyStatus.Warn, $"Windows {version} is older than Windows 10");

        var name = version.Build >= 22000 ? "Windows 11" : "Windows 10";
        return (VerifyStatus.Pass, $"{name} (build {version.Build})");
    }

    private static (VerifyStatus, string) CheckX64()
    {
        return Environment.Is64BitOperatingSystem && Environment.Is64BitProcess
            ? (VerifyStatus.Pass, "x64 OS and x64 process")
            : (VerifyStatus.Warn, $"OS x64={Environment.Is64BitOperatingSystem}, process x64={Environment.Is64BitProcess}");
    }

    private static (VerifyStatus, string) CheckDotNet8()
    {
        var version = Environment.Version;
        return version.Major == 8
            ? (VerifyStatus.Pass, $".NET {version}")
            : (VerifyStatus.Warn, $"Running on .NET {version} (expected 8.x)");
    }

    private static (VerifyStatus, string) CheckTempSpace()
    {
        var temp = Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath();
        var root = Path.GetPathRoot(temp) ?? "C:\\";
        var drive = new DriveInfo(root);
        var freeMb = drive.AvailableFreeSpace / (1024 * 1024);
        return freeMb >= 100
            ? (VerifyStatus.Pass, $"{freeMb:N0} MB free on temp drive")
            : (VerifyStatus.Warn, $"Only {freeMb} MB free on temp drive");
    }

    private static (VerifyStatus, string) CheckNavEnumDupes()
    {
        var values = Enum.GetValues<HelpDesk.Domain.Enums.Page>().Cast<int>().ToList();
        var duplicates = values.GroupBy(value => value).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

        return duplicates.Count == 0
            ? (VerifyStatus.Pass, $"{values.Count} unique Page enum values")
            : (VerifyStatus.Fail, $"Duplicate enum values found: {string.Join(", ", duplicates)}");
    }

    private static (VerifyStatus, string) CheckThemeResources()
    {
        foreach (var theme in new[] { "themes/dark.baml", "themes/light.baml" })
        {
            if (!HasCompiledWpfResource(theme))
                return (VerifyStatus.Fail, $"{theme} not found in compiled WPF resources");
        }

        return (VerifyStatus.Pass, "Dark and light theme resources are compiled into the app");
    }

    private static (VerifyStatus, string) CheckConverters()
    {
        var converterTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type => type.Namespace == "HelpDesk.Presentation.Helpers"
                && type.GetInterfaces().Any(@interface =>
                    @interface.FullName?.StartsWith("System.Windows.Data.IValueConverter", StringComparison.Ordinal) == true
                    || @interface.FullName?.StartsWith("System.Windows.Data.IMultiValueConverter", StringComparison.Ordinal) == true))
            .ToList();

        foreach (var type in converterTypes)
            Activator.CreateInstance(type);

        return converterTypes.Count > 0
            ? (VerifyStatus.Pass, $"{converterTypes.Count} converters instantiated")
            : (VerifyStatus.Warn, "No converters found");
    }

    private (VerifyStatus, string) CheckRepairCatalogMetadata()
    {
        var repairCount = _repairCatalog.Repairs.Count;
        var categoryCount = _repairCatalog.MasterCategories.Count;
        if (repairCount == 0)
            return (VerifyStatus.Fail, "No repair metadata was registered");

        var duplicateIds = _repairCatalog.Repairs
            .GroupBy(repair => repair.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateIds.Count > 0)
            return (VerifyStatus.Fail, $"Duplicate repair metadata IDs: {string.Join(", ", duplicateIds.Take(5))}");

        var missingFixBinding = _repairCatalog.Repairs.Count(repair => repair.Fix is null);
        if (missingFixBinding > 0)
            return (VerifyStatus.Warn, $"{missingFixBinding} repair item(s) are missing a live fix binding");

        return (VerifyStatus.Pass, $"{repairCount} repairs across {categoryCount} master categories");
    }

    private (VerifyStatus, string) CheckRunbookCatalog()
    {
        if (_runbooks.Runbooks.Count == 0)
            return (VerifyStatus.Fail, "No runbooks were registered");

        var missingSteps = _runbooks.Runbooks
            .Where(runbook => runbook.Steps is null || runbook.Steps.Count == 0)
            .Select(runbook => runbook.Id)
            .ToList();

        return missingSteps.Count == 0
            ? (VerifyStatus.Pass, $"{_runbooks.Runbooks.Count} runbooks available")
            : (VerifyStatus.Warn, $"Runbooks missing steps: {string.Join(", ", missingSteps.Take(5))}");
    }

    private (VerifyStatus, string) CheckKnowledgeBase()
    {
        if (_knowledgeBase.Entries.Count == 0)
            return (VerifyStatus.Warn, "No knowledge base entries are configured");

        var missingUrls = _knowledgeBase.Entries.Count(entry => string.IsNullOrWhiteSpace(entry.Url));
        return missingUrls == 0
            ? (VerifyStatus.Pass, $"{_knowledgeBase.Entries.Count} KB entries ready")
            : (VerifyStatus.Warn, $"{missingUrls} KB entries are missing URLs");
    }

    private (VerifyStatus, string) CheckEditionCapabilities()
    {
        var snapshot = _edition.GetSnapshot();
        var enabledAreas = new[]
        {
            snapshot.EvidenceBundles,
            snapshot.Runbooks,
            snapshot.DeepRepairs,
            snapshot.WhiteLabelBranding
        }.Count(state => state == CapabilityState.Available);

        return (VerifyStatus.Pass, $"{snapshot.Edition} edition loaded with {enabledAreas} enabled capability area(s)");
    }

    private async Task<(VerifyStatus, string)> CheckUpdateProviderAsync()
    {
        var info = await _updates.CheckForUpdatesAsync();
        var detail = $"{info.SourceName}: {info.Summary}";

        return info.SourceName switch
        {
            "Disabled" or "Not configured" => (VerifyStatus.Warn, detail),
            _ => (VerifyStatus.Pass, detail)
        };
    }

    private void WriteLog()
    {
        try
        {
            var dir = HelpDesk.Shared.Constants.AppDataDir;
            Directory.CreateDirectory(dir);
            var path = HelpDesk.Shared.Constants.VerifyLogFile;

            var pass = Results.Count(result => result.Status == VerifyStatus.Pass);
            var warn = Results.Count(result => result.Status == VerifyStatus.Warn);
            var fail = Results.Count(result => result.Status == VerifyStatus.Fail);

            var lines = new List<string>
            {
                $"FixFox Startup Verification - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Result: {pass} passed | {warn} warnings | {fail} failed",
                new string('-', 60)
            };

            foreach (var result in Results)
                lines.Add($"[{result.StatusIcon}] ({result.DurationMs,4}ms) {result.Name,-40} {result.Detail}");

            lines.Add(new string('-', 60));
            File.WriteAllLines(path, lines);
        }
        catch
        {
            // Never crash the app because log output failed.
        }
    }

    public int PrintHeadless()
    {
        var pass = Results.Count(result => result.Status == VerifyStatus.Pass);
        var warn = Results.Count(result => result.Status == VerifyStatus.Warn);
        var fail = Results.Count(result => result.Status == VerifyStatus.Fail);

        Console.WriteLine($"FixFox Self-Verification - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();
        foreach (var result in Results)
            Console.WriteLine($"  {result.StatusIcon,-7} {result.Name,-42} {result.Detail}");

        Console.WriteLine();
        Console.WriteLine($"  {pass} passed | {warn} warnings | {fail} failed");
        return fail > 0 ? 1 : 0;
    }

    private static bool HasCompiledWpfResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var manifestName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase));
        if (manifestName is null)
            return false;

        using var stream = assembly.GetManifestResourceStream(manifestName);
        if (stream is null)
            return false;

        using var reader = new ResourceReader(stream);
        var normalized = resourceName.Replace('\\', '/').TrimStart('/').ToLowerInvariant();

        foreach (DictionaryEntry entry in reader)
        {
            if (entry.Key is string key && string.Equals(key, normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
