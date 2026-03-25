using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using HelpDesk.Domain;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace HelpDesk.Infrastructure.Services;

// ══════════════════════════════════════════════════════════════════════════════
//  DUPLICATE FILE SERVICE  — SHA-256 hash comparison, user-confirms before delete
// ══════════════════════════════════════════════════════════════════════════════
public sealed class DuplicateFileService
{
    public event Action<string>? Progress;

    public async Task<IReadOnlyList<DuplicateGroup>> ScanAsync(
        IReadOnlyList<string> folders,
        CancellationToken ct = default)
    {
        return await Task.Run(() => Scan(folders, ct), ct);
    }

    private IReadOnlyList<DuplicateGroup> Scan(IReadOnlyList<string> folders, CancellationToken ct)
    {
        // 1. Collect all files meeting minimum size
        var files = folders
            .Where(Directory.Exists)
            .SelectMany(f => EnumerateFiles(f, ct))
            .Where(fi => fi.Length >= Constants.DupFileMinSizeBytes)
            .ToList();

        Progress?.Invoke($"Found {files.Count:N0} files to scan...");

        // 2. Group by size first (fast pre-filter)
        var bySize = files.GroupBy(f => f.Length)
                          .Where(g => g.Count() > 1)
                          .ToList();

        // 3. Hash each group where sizes match
        var groups = new List<DuplicateGroup>();
        int scanned = 0;

        foreach (var sizeGroup in bySize)
        {
            ct.ThrowIfCancellationRequested();

            var byHash = new Dictionary<string, List<FileInfo>>();
            foreach (var fi in sizeGroup)
            {
                try
                {
                    var hash = HashFile(fi.FullName);
                    if (!byHash.TryGetValue(hash, out var list))
                        byHash[hash] = list = [];
                    list.Add(fi);
                }
                catch { /* skip locked/inaccessible files */ }

                scanned++;
                if (scanned % 100 == 0)
                    Progress?.Invoke($"Hashed {scanned:N0} files...");
            }

            foreach (var (hash, dupes) in byHash)
            {
                if (dupes.Count > 1)
                    groups.Add(new DuplicateGroup(hash, dupes[0].Length, dupes));
            }
        }

        Progress?.Invoke($"Done. Found {groups.Count} groups of duplicates.");
        groups.Sort((a, b) => (b.Count * b.SizeBytes).CompareTo(a.Count * a.SizeBytes));
        return groups.AsReadOnly();
    }

    private static IEnumerable<FileInfo> EnumerateFiles(string root, CancellationToken ct)
    {
        var stack = new Stack<DirectoryInfo>();
        stack.Push(new DirectoryInfo(root));
        int depth = 0;

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();
            if (++depth > Constants.DupFileScanMaxDepth) continue;

            FileInfo[] files;
            try { files = dir.GetFiles(); } catch { continue; }
            foreach (var f in files) yield return f;

            DirectoryInfo[] subdirs;
            try { subdirs = dir.GetDirectories(); } catch { continue; }
            foreach (var sub in subdirs) stack.Push(sub);
        }
    }

    private static string HashFile(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}

public sealed record DuplicateGroup(string Hash, long SizeBytes, List<FileInfo> Files)
{
    public int    Count      => Files.Count;
    public long   WasteBytes => SizeBytes * (Files.Count - 1);
    public string SizeLabel  => FormatSize(SizeBytes);
    public string WasteLabel => FormatSize(WasteBytes);

    private static string FormatSize(long bytes) =>
        bytes >= 1_073_741_824 ? $"{bytes / 1_073_741_824.0:N1} GB" :
        bytes >= 1_048_576     ? $"{bytes / 1_048_576.0:N1} MB"     :
                                 $"{bytes / 1024.0:N0} KB";
}

// ══════════════════════════════════════════════════════════════════════════════
//  INSTALLED PROGRAMS SERVICE
// ══════════════════════════════════════════════════════════════════════════════
public sealed class InstalledProgramsService
{
    public Task<IReadOnlyList<InstalledProgram>> GetInstalledAsync()
        => Task.Run(GetInstalled);

    private static IReadOnlyList<InstalledProgram> GetInstalled()
    {
        var programs = new List<InstalledProgram>();

        var keys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        };

        foreach (var keyPath in keys)
        {
            using var hklm = Registry.LocalMachine.OpenSubKey(keyPath);
            if (hklm is null) continue;
            foreach (var subName in hklm.GetSubKeyNames())
            {
                try
                {
                    using var sub = hklm.OpenSubKey(subName);
                    if (sub is null) continue;

                    var name = sub.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (sub.GetValue("SystemComponent") is int sys && sys == 1) continue;

                    var version     = sub.GetValue("DisplayVersion")?.ToString() ?? "";
                    var publisher   = sub.GetValue("Publisher")?.ToString() ?? "";
                    var installDate = sub.GetValue("InstallDate")?.ToString() ?? "";
                    var sizekb      = sub.GetValue("EstimatedSize") is int kb ? (long)kb * 1024 : 0L;
                    var uninstall   = sub.GetValue("UninstallString")?.ToString() ?? "";
                    var quietUninstall = sub.GetValue("QuietUninstallString")?.ToString() ?? "";
                    var installLocation = sub.GetValue("InstallLocation")?.ToString() ?? "";
                    var displayIcon = sub.GetValue("DisplayIcon")?.ToString() ?? "";

                    programs.Add(new InstalledProgram(
                        name, version, publisher,
                        ParseDate(installDate), sizekb,
                        uninstall, quietUninstall, installLocation, displayIcon));
                }
                catch { }
            }
        }

        programs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return programs.AsReadOnly();
    }

    private static DateTime? ParseDate(string s)
    {
        if (s.Length == 8 && DateTime.TryParseExact(s, "yyyyMMdd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d))
            return d;
        return null;
    }
}

public sealed record InstalledProgram(
    string    Name,
    string    Version,
    string    Publisher,
    DateTime? InstallDate,
    long      SizeBytes,
    string    UninstallCommand,
    string    QuietUninstallCommand,
    string    InstallLocation,
    string    DisplayIconPath)
{
    public string SizeLabel => SizeBytes > 0
        ? (SizeBytes >= 1_073_741_824
            ? $"{SizeBytes / 1_073_741_824.0:N1} GB"
            : SizeBytes >= 1_048_576
                ? $"{SizeBytes / 1_048_576.0:N1} MB"
                : $"{SizeBytes / 1024.0:N0} KB")
        : "";

    public string InstallDateLabel => InstallDate.HasValue
        ? InstallDate.Value.ToString("yyyy-MM-dd")
        : "";

    public bool HasInstallLocation => !string.IsNullOrWhiteSpace(InstallLocation) && Directory.Exists(InstallLocation);
}

public sealed class StartupAppsService
{
    private readonly Func<IReadOnlyList<StartupAppEntry>>? _provider;

    public StartupAppsService(Func<IReadOnlyList<StartupAppEntry>>? provider = null)
    {
        _provider = provider;
    }

    public Task<IReadOnlyList<StartupAppEntry>> GetEntriesAsync() =>
        Task.Run(() => _provider?.Invoke() ?? GetEntries());

    private static IReadOnlyList<StartupAppEntry> GetEntries()
    {
        var entries = new List<StartupAppEntry>();

        LoadRegistryEntries(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Current user registry startup", entries);
        LoadRegistryEntries(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "Machine registry startup", entries);
        LoadRegistryEntries(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Machine registry startup (32-bit)", entries);

        LoadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Current user startup folder", entries);
        LoadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "All users startup folder", entries);

        return entries
            .GroupBy(entry => $"{entry.Name}|{entry.Command}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(entry => entry.RecommendedDisableCandidate)
            .ThenBy(entry => entry.Name)
            .ToList()
            .AsReadOnly();
    }

    private static void LoadRegistryEntries(RegistryKey root, string keyPath, string source, List<StartupAppEntry> entries)
    {
        try
        {
            using var key = root.OpenSubKey(keyPath);
            if (key is null)
                return;

            foreach (var valueName in key.GetValueNames())
            {
                var command = key.GetValue(valueName)?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                entries.Add(BuildStartupEntry(valueName, source, command));
            }
        }
        catch
        {
        }
    }

    private static void LoadStartupFolder(string folder, string source, List<StartupAppEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                entries.Add(BuildStartupEntry(name, source, file));
            }
        }
        catch
        {
        }
    }

    private static StartupAppEntry BuildStartupEntry(string name, string source, string command)
    {
        var launchTarget = ExtractLaunchTarget(command);
        var recommendedDisable = IsReviewCandidate(name, command);
        var reason = recommendedDisable
            ? "Review this if startup feels heavy. It is not a core Windows startup item."
            : "Usually worth keeping unless you are troubleshooting startup pressure.";

        return new StartupAppEntry(name, source, command, launchTarget, recommendedDisable, reason);
    }

    private static bool IsReviewCandidate(string name, string command)
    {
        var combined = $"{name} {command}";
        var knownKeepers = new[]
        {
            "windows", "microsoft", "securityhealth", "defender", "onedrive",
            "realtek", "intel", "nvidia", "amd", "synaptics", "touchpad", "audio"
        };

        return !knownKeepers.Any(item => combined.Contains(item, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractLaunchTarget(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "";

        var trimmed = command.Trim();
        if (trimmed.StartsWith('"'))
        {
            var end = trimmed.IndexOf('"', 1);
            if (end > 1)
                return trimmed[1..end];
        }

        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex > 0)
            return trimmed[..(exeIndex + 4)].Trim();

        return File.Exists(trimmed) ? trimmed : "";
    }
}

public sealed class StorageInsightsService
{
    private readonly IReadOnlyList<string>? _roots;

    public StorageInsightsService(IReadOnlyList<string>? roots = null)
    {
        _roots = roots;
    }

    public Task<IReadOnlyList<StorageInsight>> GetInsightsAsync() =>
        Task.Run(GetInsights);

    private IReadOnlyList<StorageInsight> GetInsights()
    {
        var roots = (_roots ?? BuildDefaultRoots())
            .Where(root => !string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var files = new List<(string Root, FileInfo File)>();
        foreach (var root in roots)
        {
            files.AddRange(EnumerateCandidateFiles(root)
                .Select(file => (root, file)));
        }

        return files
            .OrderByDescending(item => item.File.Length)
            .Take(10)
            .Select(item => new StorageInsight(
                item.File.Name,
                item.File.FullName,
                BuildLocationLabel(item.Root),
                item.File.Length,
                BuildSafeRemovalSummary(item.Root, item.File.FullName),
                BuildCaution(item.Root, item.File.FullName)))
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyList<string> BuildDefaultRoots()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return
        [
            Path.Combine(userProfile, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        ];
    }

    private static IEnumerable<FileInfo> EnumerateCandidateFiles(string root)
    {
        var files = new List<FileInfo>();

        TryAddFiles(root, files);
        try
        {
            foreach (var subDirectory in Directory.EnumerateDirectories(root).Take(8))
                TryAddFiles(subDirectory, files);
        }
        catch
        {
        }

        return files;
    }

    private static void TryAddFiles(string folder, List<FileInfo> files)
    {
        try
        {
            files.AddRange(Directory.EnumerateFiles(folder)
                .Select(path => new FileInfo(path))
                .Where(info => info.Exists)
                .OrderByDescending(info => info.Length)
                .Take(8));
        }
        catch
        {
        }
    }

    private static string BuildLocationLabel(string root)
    {
        var normalized = root.Replace('/', '\\');
        if (normalized.EndsWith("\\Downloads", StringComparison.OrdinalIgnoreCase))
            return "Downloads";
        if (normalized.EndsWith("\\Desktop", StringComparison.OrdinalIgnoreCase))
            return "Desktop";
        if (normalized.EndsWith("\\Documents", StringComparison.OrdinalIgnoreCase))
            return "Documents";
        return Path.GetFileName(normalized);
    }

    private static string BuildSafeRemovalSummary(string root, string fullPath)
    {
        if (root.EndsWith("Downloads", StringComparison.OrdinalIgnoreCase))
            return "Often safe once you are done with installers, archives, or exported files.";
        if (fullPath.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
            return "Review carefully because this item may also be synced to OneDrive.";
        return "Review before deleting. This location often holds user-created or working files.";
    }

    private static string BuildCaution(string root, string fullPath)
    {
        if (fullPath.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
            return "This path appears to be inside OneDrive.";
        if (root.EndsWith("Desktop", StringComparison.OrdinalIgnoreCase))
            return "Desktop items are highly visible to the user and often kept intentionally.";
        if (root.EndsWith("Documents", StringComparison.OrdinalIgnoreCase))
            return "Documents often contain real user data rather than disposable cache.";
        return "Review the file name and date before removing anything large.";
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  SCHEDULER SERVICE  — Weekly Tune-Up via Windows Task Scheduler
// ══════════════════════════════════════════════════════════════════════════════
public sealed class SchedulerService
{
    private static readonly string TaskName =
        Constants.ScheduledTaskName;

    /// <summary>Creates or updates a weekly scheduled task for the Tune-Up bundle.</summary>
    public void Schedule(DayOfWeek day, TimeSpan time)
    {
        ScheduleInternal(TaskName, $"/sc weekly /d {day.ToString()[..3].ToUpperInvariant()} /st {time.Hours:D2}:{time.Minutes:D2}", ResolveTaskCommand("weekly-tune-up"));
    }

    /// <summary>Removes the scheduled task if it exists.</summary>
    public void Unschedule()
        => RunSchtasks($"/delete /tn \"{TaskName}\" /f", allowFailure: true);

    /// <summary>Returns the next run time, or null if no task exists.</summary>
    public DateTime? GetNextRun()
    {
        try
        {
            var result = RunSchtasks($"/query /tn \"{TaskName}\" /fo LIST", allowFailure: true);
            var line = result.Split('\n')
                .FirstOrDefault(l => l.StartsWith("Next Run Time:", StringComparison.OrdinalIgnoreCase));
            if (line is null) return null;
            var dateStr = line.Split(':', 2)[1].Trim();
            return DateTime.TryParse(dateStr, out var dt) ? dt : null;
        }
        catch { return null; }
    }

    /// <summary>Returns true if a task is currently scheduled.</summary>
    public bool IsScheduled()
        => GetNextRun().HasValue;

    public void SyncAutomationRule(AutomationRuleSettings rule)
    {
        if (!rule.Enabled || rule.IsWatcher || !rule.SupportsScheduling)
        {
            Unschedule(rule.Id);
            return;
        }

        switch (rule.ScheduleKind)
        {
            case AutomationScheduleKind.Daily:
                if (!TimeSpan.TryParse(rule.ScheduleTime, out var dailyTime))
                    dailyTime = TimeSpan.FromHours(9);
                ScheduleDaily(rule.Id, dailyTime);
                break;
            case AutomationScheduleKind.Weekly:
                if (!Enum.TryParse<DayOfWeek>(rule.ScheduleDay, ignoreCase: true, out var day))
                    day = DayOfWeek.Sunday;
                if (!TimeSpan.TryParse(rule.ScheduleTime, out var weeklyTime))
                    weeklyTime = TimeSpan.FromHours(9);
                ScheduleWeekly(rule.Id, day, weeklyTime);
                break;
            default:
                Unschedule(rule.Id);
                break;
        }
    }

    public void ScheduleDaily(string ruleId, TimeSpan time)
        => ScheduleInternal(GetTaskName(ruleId), $"/sc daily /st {time.Hours:D2}:{time.Minutes:D2}", ResolveTaskCommand(ruleId));

    public void ScheduleWeekly(string ruleId, DayOfWeek day, TimeSpan time)
        => ScheduleInternal(GetTaskName(ruleId), $"/sc weekly /d {day.ToString()[..3].ToUpperInvariant()} /st {time.Hours:D2}:{time.Minutes:D2}", ResolveTaskCommand(ruleId));

    public void Unschedule(string ruleId)
        => RunSchtasks($"/delete /tn \"{GetTaskName(ruleId)}\" /f", allowFailure: true);

    public DateTime? GetNextRun(string ruleId)
    {
        try
        {
            var result = RunSchtasks($"/query /tn \"{GetTaskName(ruleId)}\" /fo LIST", allowFailure: true);
            var line = result.Split('\n')
                .FirstOrDefault(l => l.StartsWith("Next Run Time:", StringComparison.OrdinalIgnoreCase));
            if (line is null)
                return null;

            var dateStr = line.Split(':', 2)[1].Trim();
            return DateTime.TryParse(dateStr, out var dt) ? dt : null;
        }
        catch
        {
            return null;
        }
    }

    public bool IsScheduled(string ruleId)
        => GetNextRun(ruleId).HasValue;

    private static void ScheduleInternal(string taskName, string triggerArguments, string runTarget)
    {
        var runAsUser = string.IsNullOrWhiteSpace(Environment.UserDomainName)
            ? Environment.UserName
            : $@"{Environment.UserDomainName}\{Environment.UserName}";

        RunSchtasks($"/delete /tn \"{taskName}\" /f", allowFailure: true);
        var cmd = $"/create /tn \"{taskName}\" /tr \"{runTarget}\" {triggerArguments} /ru \"{runAsUser}\" /rl highest /f";
        RunSchtasks(cmd);
    }

    private static string RunSchtasks(string args, bool allowFailure = false)
    {
        var psi = new ProcessStartInfo("schtasks", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        var error  = p.StandardError.ReadToEnd();
        p.WaitForExit(10_000);
        if (!allowFailure && p.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
        return output;
    }

    private static string ResolveTaskCommand(string ruleId)
    {
        var currentProcess = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        var appHostPath = Path.Combine(AppContext.BaseDirectory, "FixFox.exe");
        var dllPath = Path.Combine(AppContext.BaseDirectory, "FixFox.dll");

        if (File.Exists(appHostPath) &&
            (string.IsNullOrWhiteSpace(currentProcess) ||
             string.Equals(Path.GetFileName(currentProcess), "dotnet.exe", StringComparison.OrdinalIgnoreCase)))
        {
            return $"\\\"{appHostPath}\\\" --run-automation {ruleId}";
        }

        if (!string.IsNullOrWhiteSpace(currentProcess) &&
            string.Equals(Path.GetFileName(currentProcess), "dotnet.exe", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(dllPath))
        {
            return $"\\\"{currentProcess}\\\" \\\"{dllPath}\\\" --run-automation {ruleId}";
        }

        if (!string.IsNullOrWhiteSpace(currentProcess))
            return $"\\\"{currentProcess}\\\" --run-automation {ruleId}";

        throw new InvalidOperationException("Cannot determine FixFox launch path.");
    }

    private static string GetTaskName(string ruleId)
    {
        var safeRuleId = string.Concat(ruleId.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        return $"{Constants.AutomationTaskPrefix}{safeRuleId}";
    }
}
