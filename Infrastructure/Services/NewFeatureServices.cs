using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using HelpDesk.Domain;
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

                    programs.Add(new InstalledProgram(
                        name, version, publisher,
                        ParseDate(installDate), sizekb,
                        uninstall, quietUninstall));
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
    string    QuietUninstallCommand)
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
        var dayStr  = day.ToString().Substring(0, 3).ToUpper();
        var timeStr = $"{time.Hours:D2}:{time.Minutes:D2}";
        var runTarget = ResolveTaskCommand();
        var runAsUser = string.IsNullOrWhiteSpace(Environment.UserDomainName)
            ? Environment.UserName
            : $@"{Environment.UserDomainName}\{Environment.UserName}";

        // Remove existing
        RunSchtasks($"/delete /tn \"{TaskName}\" /f", allowFailure: true);

        // Create new weekly trigger
        var cmd = $"/create /tn \"{TaskName}\" " +
                  $"/tr \"{runTarget}\" " +
                  $"/sc weekly /d {dayStr} /st {timeStr} " +
                  $"/ru \"{runAsUser}\" " +
                  $"/rl highest /f";

        RunSchtasks(cmd);
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

    private static string ResolveTaskCommand()
    {
        var currentProcess = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        var appHostPath = Path.Combine(AppContext.BaseDirectory, "FixFox.exe");
        var dllPath = Path.Combine(AppContext.BaseDirectory, "FixFox.dll");

        if (File.Exists(appHostPath) &&
            (string.IsNullOrWhiteSpace(currentProcess) ||
             string.Equals(Path.GetFileName(currentProcess), "dotnet.exe", StringComparison.OrdinalIgnoreCase)))
        {
            return $"\\\"{appHostPath}\\\" --run-bundle weekly-tune-up";
        }

        if (!string.IsNullOrWhiteSpace(currentProcess) &&
            string.Equals(Path.GetFileName(currentProcess), "dotnet.exe", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(dllPath))
        {
            return $"\\\"{currentProcess}\\\" \\\"{dllPath}\\\" --run-bundle weekly-tune-up";
        }

        if (!string.IsNullOrWhiteSpace(currentProcess))
            return $"\\\"{currentProcess}\\\" --run-bundle weekly-tune-up";

        throw new InvalidOperationException("Cannot determine FixFox launch path.");
    }
}
