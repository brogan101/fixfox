using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HelpDesk.Domain;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace HelpDesk.Infrastructure.Services;

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
//  DUPLICATE FILE SERVICE  â€” SHA-256 hash comparison, user-confirms before delete
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
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

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
//  INSTALLED PROGRAMS SERVICE
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
public sealed class InstalledProgramsService
{
    public Task<IReadOnlyList<InstalledProgram>> GetInstalledAsync()
        => Task.Run(GetInstalled);

    public Task<IReadOnlyList<string>> GetDefaultAssociationsAsync(InstalledProgram program)
        => Task.Run(() => GetDefaultAssociations(program));

    public async Task RepairAsync(InstalledProgram program)
    {
        if (program.IsMsiApp)
        {
            Process.Start(new ProcessStartInfo("msiexec.exe", $"/fa {program.ProductCode}")
            {
                UseShellExecute = true,
                Verb = "runas"
            });
            return;
        }

        if (program.IsStoreApp)
        {
            var script = $"Get-AppxPackage -PackageFamilyName '{program.PackageFamilyName}' | ForEach-Object {{ Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppxManifest.xml\" -ErrorAction Stop }}";
            await RunPowerShellAsync(script);
        }
    }

    public async Task ResetAsync(InstalledProgram program)
    {
        if (!program.IsStoreApp)
            return;

        var script = $"if (Get-Command Reset-AppxPackage -ErrorAction SilentlyContinue) {{ Reset-AppxPackage -Package '{program.PackageFullName}' -ErrorAction Stop }} else {{ throw 'Reset-AppxPackage is not available on this device.' }}";
        await RunPowerShellAsync(script);
    }

    private static IReadOnlyList<InstalledProgram> GetInstalled()
    {
        var programs = new List<InstalledProgram>();
        var userAssistMap = LoadUserAssistMap();

        LoadRegistryPrograms(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", programs, userAssistMap);
        LoadRegistryPrograms(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", programs, userAssistMap);
        LoadRegistryPrograms(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", programs, userAssistMap);
        LoadStorePrograms(programs, userAssistMap);

        return programs
            .GroupBy(program => $"{program.Name}|{program.Publisher}|{program.Version}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(candidate => candidate.IsStoreApp)
                .ThenByDescending(candidate => candidate.HasInstallLocation)
                .First())
            .OrderBy(program => program.Name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    private static DateTime? ParseDate(string s)
    {
        if (s.Length == 8 && DateTime.TryParseExact(s, "yyyyMMdd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d))
            return d;
        return null;
    }

    private static void LoadRegistryPrograms(
        RegistryKey root,
        string keyPath,
        List<InstalledProgram> programs,
        IReadOnlyDictionary<string, DateTime> userAssistMap)
    {
        using var parent = root.OpenSubKey(keyPath);
        if (parent is null)
            return;

        foreach (var subName in parent.GetSubKeyNames())
        {
            try
            {
                using var sub = parent.OpenSubKey(subName);
                if (sub is null)
                    continue;

                var name = sub.GetValue("DisplayName")?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                if (sub.GetValue("SystemComponent") is int sys && sys == 1)
                    continue;

                var version = sub.GetValue("DisplayVersion")?.ToString() ?? "";
                var publisher = sub.GetValue("Publisher")?.ToString() ?? "";
                var installDate = sub.GetValue("InstallDate")?.ToString() ?? "";
                var sizekb = sub.GetValue("EstimatedSize") is int kb ? (long)kb * 1024 : 0L;
                var uninstall = sub.GetValue("UninstallString")?.ToString() ?? "";
                var quietUninstall = sub.GetValue("QuietUninstallString")?.ToString() ?? "";
                var installLocation = sub.GetValue("InstallLocation")?.ToString() ?? "";
                var displayIcon = NormalizeDisplayIcon(sub.GetValue("DisplayIcon")?.ToString() ?? "");
                var lastUsedUtc = ResolveLastUsedUtc(displayIcon, installLocation, userAssistMap);
                var productCode = ExtractProductCode(uninstall, quietUninstall, subName);

                programs.Add(new InstalledProgram(
                    name,
                    version,
                    publisher,
                    ParseDate(installDate),
                    sizekb,
                    uninstall,
                    quietUninstall,
                    installLocation,
                    displayIcon,
                    productCode,
                    false,
                    "",
                    "",
                    lastUsedUtc));
            }
            catch
            {
            }
        }
    }

    private static void LoadStorePrograms(List<InstalledProgram> programs, IReadOnlyDictionary<string, DateTime> userAssistMap)
    {
        try
        {
            var startInfo = new ProcessStartInfo(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage | Where-Object { -not $_.IsFramework -and -not $_.NonRemovable } | Select-Object Name,PackageFamilyName,PackageFullName,InstallLocation,Publisher | ConvertTo-Json -Depth 4\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10000);
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return;

            var tokens = JsonConvert.DeserializeObject<List<Newtonsoft.Json.Linq.JToken>>(output);
            if (tokens is null)
            {
                var single = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JToken>(output);
                tokens = single is null ? [] : [single];
            }

            foreach (var token in tokens)
            {
                var name = token?["Name"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var installLocation = token?["InstallLocation"]?.ToString() ?? "";
                var packageFamilyName = token?["PackageFamilyName"]?.ToString() ?? "";
                var packageFullName = token?["PackageFullName"]?.ToString() ?? "";
                var publisher = token?["Publisher"]?.ToString() ?? "";

                programs.Add(new InstalledProgram(
                    name,
                    "",
                    publisher,
                    null,
                    0,
                    "",
                    "",
                    installLocation,
                    "",
                    "",
                    true,
                    packageFamilyName,
                    packageFullName,
                    ResolveLastUsedUtc("", installLocation, userAssistMap)));
            }
        }
        catch
        {
        }
    }

    private static async Task RunPowerShellAsync(string script)
    {
        var escaped = script.Replace("\"", "\\\"");
        var startInfo = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{escaped}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("PowerShell could not be launched.");
        var stdOut = await process.StandardOutput.ReadToEndAsync();
        var stdErr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr);
    }

    private static IReadOnlyList<string> GetDefaultAssociations(InstalledProgram program)
    {
        var registeredApps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var registered = root.OpenSubKey(@"SOFTWARE\RegisteredApplications");
            if (registered is null)
                continue;

            foreach (var name in registered.GetValueNames())
            {
                var path = registered.GetValue(name)?.ToString();
                if (!string.IsNullOrWhiteSpace(path))
                    registeredApps[name] = path;
            }
        }

        var matching = registeredApps.FirstOrDefault(item =>
            item.Key.Contains(program.Name, StringComparison.OrdinalIgnoreCase)
            || program.Name.Contains(item.Key, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(matching.Value))
            return [];

        using var rootKey = Registry.CurrentUser.OpenSubKey(matching.Value)
            ?? Registry.LocalMachine.OpenSubKey(matching.Value);
        using var assocKey = rootKey?.OpenSubKey("FileAssociations");
        if (assocKey is null)
            return [];

        var progIds = assocKey.GetValueNames()
            .Select(extension => (Extension: extension, ProgId: assocKey.GetValue(extension)?.ToString() ?? ""))
            .Where(item => !string.IsNullOrWhiteSpace(item.ProgId))
            .ToList();

        using var fileExtsRoot = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts");
        if (fileExtsRoot is null)
            return [];

        var results = new List<string>();
        foreach (var (extension, progId) in progIds)
        {
            using var choiceKey = fileExtsRoot.OpenSubKey($@"{extension}\UserChoice");
            var currentProgId = choiceKey?.GetValue("ProgId")?.ToString() ?? "";
            if (string.Equals(currentProgId, progId, StringComparison.OrdinalIgnoreCase))
                results.Add(extension);
        }

        return results.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyDictionary<string, DateTime> LoadUserAssistMap()
    {
        var results = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist");
            if (root is null)
                return results;

            foreach (var guid in root.GetSubKeyNames())
            {
                using var countKey = root.OpenSubKey($@"{guid}\Count");
                if (countKey is null)
                    continue;

                foreach (var valueName in countKey.GetValueNames())
                {
                    if (countKey.GetValue(valueName) is not byte[] data || data.Length < 68)
                        continue;

                    var decodedName = Rot13(valueName);
                    var baseName = Path.GetFileNameWithoutExtension(decodedName);
                    if (string.IsNullOrWhiteSpace(baseName))
                        continue;

                    var fileTime = BitConverter.ToInt64(data, 60);
                    if (fileTime <= 0)
                        continue;

                    var lastUsed = DateTime.FromFileTimeUtc(fileTime);
                    if (!results.TryGetValue(baseName!, out var existing) || lastUsed > existing)
                        results[baseName!] = lastUsed;
                }
            }
        }
        catch
        {
        }

        return results;
    }

    private static string NormalizeDisplayIcon(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        return raw.Split(',')[0].Trim().Trim('"');
    }

    private static string ExtractProductCode(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var match = Regex.Match(candidate, @"\{[0-9A-Fa-f\-]{36}\}");
            if (match.Success)
                return match.Value;
        }

        return "";
    }

    private static DateTime? ResolveLastUsedUtc(
        string displayIcon,
        string installLocation,
        IReadOnlyDictionary<string, DateTime> userAssistMap)
    {
        var probes = new[]
        {
            Path.GetFileNameWithoutExtension(displayIcon ?? string.Empty),
            Path.GetFileNameWithoutExtension(installLocation ?? string.Empty),
            Path.GetFileNameWithoutExtension(Path.Combine(installLocation ?? string.Empty, Path.GetFileName(displayIcon ?? string.Empty)))
        };

        foreach (var probe in probes.Where(probe => !string.IsNullOrWhiteSpace(probe)))
        {
            if (userAssistMap.TryGetValue(probe!, out var lastUsed))
                return lastUsed;
        }

        return null;
    }

    private static string Rot13(string value)
    {
        var chars = value.ToCharArray();
        for (var index = 0; index < chars.Length; index++)
        {
            var current = chars[index];
            if (current is >= 'a' and <= 'z')
                chars[index] = (char)('a' + ((current - 'a' + 13) % 26));
            else if (current is >= 'A' and <= 'Z')
                chars[index] = (char)('A' + ((current - 'A' + 13) % 26));
        }

        return new string(chars);
    }
}

public sealed record InstalledProgram(
    string Name,
    string Version,
    string Publisher,
    DateTime? InstallDate,
    long SizeBytes,
    string UninstallCommand,
    string QuietUninstallCommand,
    string InstallLocation,
    string DisplayIconPath,
    string ProductCode = "",
    bool IsStoreApp = false,
    string PackageFamilyName = "",
    string PackageFullName = "",
    DateTime? LastUsedUtc = null)
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
    public bool IsMsiApp => !string.IsNullOrWhiteSpace(ProductCode);
    public bool HasUninstallAction => !string.IsNullOrWhiteSpace(UninstallCommand) || !string.IsNullOrWhiteSpace(QuietUninstallCommand) || IsStoreApp;
    public bool IsRepairAvailable => IsMsiApp || IsStoreApp;
    public bool IsResetAvailable => IsStoreApp;
    public bool HasLastUsedData => LastUsedUtc.HasValue;
    public bool IsStale => LastUsedUtc.HasValue && LastUsedUtc.Value < DateTime.UtcNow.AddDays(-90);
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

    public Task DisableAsync(StartupAppEntry entry) =>
        Task.Run(() => Disable(entry));

    internal static StartupImpactLevel ClassifyImpact(int? delayMilliseconds) => delayMilliseconds switch
    {
        null => StartupImpactLevel.Unknown,
        < 500 => StartupImpactLevel.Low,
        <= 2000 => StartupImpactLevel.Medium,
        _ => StartupImpactLevel.High
    };

    private static IReadOnlyList<StartupAppEntry> GetEntries()
    {
        var entries = new List<StartupAppEntry>();
        var delayMap = LoadStartupDelayMap();

        LoadRegistryEntries(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Current user registry startup", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", entries, delayMap);
        LoadRegistryEntries(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "Machine registry startup", "HKLM", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", entries, delayMap);
        LoadRegistryEntries(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Machine registry startup (32-bit)", "HKLM", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32", entries, delayMap);

        LoadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Current user startup folder", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", entries, delayMap);
        LoadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "All users startup folder", "HKLM", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", entries, delayMap);

        return entries
            .GroupBy(entry => $"{entry.Name}|{entry.Command}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(entry => entry.RecommendedDisableCandidate)
            .ThenBy(entry => entry.Name)
            .ToList()
            .AsReadOnly();
    }

    private static void LoadRegistryEntries(
        RegistryKey root,
        string keyPath,
        string source,
        string scope,
        string startupApprovedSubKey,
        List<StartupAppEntry> entries,
        IReadOnlyDictionary<string, int> delayMap)
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

                entries.Add(BuildStartupEntry(valueName, source, command, delayMap, scope, startupApprovedSubKey, valueName));
            }
        }
        catch
        {
        }
    }

    private static void LoadStartupFolder(
        string folder,
        string source,
        string scope,
        string startupApprovedSubKey,
        List<StartupAppEntry> entries,
        IReadOnlyDictionary<string, int> delayMap)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                entries.Add(BuildStartupEntry(name, source, file, delayMap, scope, startupApprovedSubKey, Path.GetFileName(file)));
            }
        }
        catch
        {
        }
    }

    private static StartupAppEntry BuildStartupEntry(
        string name,
        string source,
        string command,
        IReadOnlyDictionary<string, int> delayMap,
        string scope,
        string startupApprovedSubKey,
        string startupApprovedValueName)
    {
        var launchTarget = ExtractLaunchTarget(command);
        var catalogEntry = ResolveStartupCatalogEntry(name, command, launchTarget);
        var launchTargetKey = Path.GetFileNameWithoutExtension(launchTarget);
        var delay = (!string.IsNullOrWhiteSpace(launchTargetKey) && delayMap.TryGetValue(launchTargetKey!, out var byTarget))
            ? byTarget
            : delayMap.TryGetValue(name, out var byName)
                ? byName
                : (int?)null;

        return new StartupAppEntry(
            name,
            source,
            command,
            launchTarget,
            catalogEntry.RecommendedDisableCandidate,
            catalogEntry.RecommendationReason,
            delay,
            ClassifyImpact(delay),
            catalogEntry.WhatItDoes,
            catalogEntry.Classification,
            catalogEntry.WhatMayBreakIfDisabled,
            name,
            scope,
            startupApprovedSubKey,
            startupApprovedValueName,
            true);
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

    private static StartupItemCatalogEntry ResolveStartupCatalogEntry(string name, string command, string launchTarget)
    {
        var combined = $"{name} {command} {launchTarget}";
        foreach (var entry in StartupCatalogEntries)
        {
            if (entry.Keywords.Any(keyword => combined.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                return entry;
        }

        if (combined.Contains("microsoft", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("windows", StringComparison.OrdinalIgnoreCase))
        {
            return new StartupItemCatalogEntry(
                [],
                false,
                "Usually worth keeping unless you are troubleshooting startup pressure.",
                "A Microsoft component that helps Windows features or sign-in start correctly.",
                StartupItemClassification.MicrosoftComponent,
                "Disabling it may affect Windows shell features, sync, or built-in security prompts.");
        }

        return new StartupItemCatalogEntry(
            [],
            true,
            "Review this if startup feels heavy. It is not recognized as a core Windows startup item.",
            "A program from the installed software inventory that runs at sign-in.",
            StartupItemClassification.Unrecognized,
            "Impact unknown. Research before disabling if you rely on it for sign-in, sync, meetings, or security.");
    }

    private static IReadOnlyDictionary<string, int> LoadStartupDelayMap()
    {
        var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var query = new EventLogQuery(
                "Microsoft-Windows-Diagnostics-Performance/Operational",
                PathType.LogName,
                "*[System[(EventID=101 or EventID=106)]]")
            {
                ReverseDirection = true
            };

            using var reader = new EventLogReader(query);
            for (var index = 0; index < 64; index++)
            {
                using var record = reader.ReadEvent();
                if (record is null)
                    break;

                var xml = XDocument.Parse(record.ToXml());
                var data = xml.Descendants()
                    .Where(element => element.Name.LocalName == "Data")
                    .ToDictionary(
                        element => (string?)element.Attribute("Name") ?? string.Empty,
                        element => element.Value,
                        StringComparer.OrdinalIgnoreCase);

                var name = data.TryGetValue("FriendlyName", out var friendly) && !string.IsNullOrWhiteSpace(friendly)
                    ? friendly
                    : data.TryGetValue("FileName", out var fileName)
                        ? Path.GetFileNameWithoutExtension(fileName)
                        : "";
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var delay = data.TryGetValue("DegradationTime", out var degradation) && int.TryParse(degradation, out var parsed)
                    ? parsed
                    : data.TryGetValue("TotalTime", out var total) && int.TryParse(total, out parsed)
                        ? parsed
                        : 0;
                if (delay <= 0)
                    continue;

                results[name] = delay;

                var trimmedFile = Path.GetFileNameWithoutExtension(data.TryGetValue("FileName", out fileName) ? fileName : string.Empty);
                if (!string.IsNullOrWhiteSpace(trimmedFile))
                    results[trimmedFile] = delay;
            }
        }
        catch
        {
        }

        return results;
    }

    private static void Disable(StartupAppEntry entry)
    {
        if (!entry.CanDisable || string.IsNullOrWhiteSpace(entry.StartupApprovedSubKey) || string.IsNullOrWhiteSpace(entry.StartupApprovedValueName))
            return;

        var root = string.Equals(entry.StartupApprovedScope, "HKLM", StringComparison.OrdinalIgnoreCase)
            ? Registry.LocalMachine
            : Registry.CurrentUser;
        using var key = root.CreateSubKey(entry.StartupApprovedSubKey);
        key?.SetValue(entry.StartupApprovedValueName, BuildDisabledState(), RegistryValueKind.Binary);
    }

    private static byte[] BuildDisabledState()
    {
        var data = new byte[12];
        data[0] = 0x03;
        return data;
    }

    private sealed record StartupItemCatalogEntry(
        string[] Keywords,
        bool RecommendedDisableCandidate,
        string RecommendationReason,
        string WhatItDoes,
        StartupItemClassification Classification,
        string WhatMayBreakIfDisabled);

    private static readonly IReadOnlyList<StartupItemCatalogEntry> StartupCatalogEntries =
    [
        new(["onedrive"], false, "Keep this on if you rely on OneDrive sync at sign-in.", "Syncs OneDrive files and signs you into Microsoft cloud storage.", StartupItemClassification.MicrosoftComponent, "OneDrive files may stop syncing until you launch the app manually."),
        new(["teams"], true, "Review this if Teams startup is not important on every sign-in.", "Starts Microsoft Teams so meetings and chats are ready faster.", StartupItemClassification.KnownThirdParty, "You may miss immediate meeting reminders until Teams is opened."),
        new(["slack"], true, "Review this if Slack does not need to start with Windows.", "Launches Slack in the background at sign-in.", StartupItemClassification.KnownThirdParty, "Slack messages and notifications may be delayed until you open Slack."),
        new(["zoom"], true, "Review this if Zoom does not need to preload for every sign-in.", "Preloads Zoom components for quicker meeting launch.", StartupItemClassification.KnownThirdParty, "Zoom may take slightly longer to open for the first meeting."),
        new(["spotify"], true, "Safe to review if media apps are adding startup pressure.", "Starts Spotify background services and media controls.", StartupItemClassification.KnownThirdParty, "Spotify will wait until you open it manually."),
        new(["steam"], true, "Safe to review on work or shared PCs where Steam is not needed at sign-in.", "Launches Steam so game libraries and updates start immediately.", StartupItemClassification.KnownThirdParty, "Game update checks and chat features will wait until Steam is opened."),
        new(["discord"], true, "Review this if Discord startup is adding background pressure.", "Starts Discord so calls and notifications are ready right away.", StartupItemClassification.KnownThirdParty, "Discord messages and voice startup may be delayed until you launch it."),
        new(["creative cloud", "adobe"], true, "Review Adobe background launchers if startup feels heavy.", "Keeps Adobe licensing, sync, and app updates ready in the background.", StartupItemClassification.KnownThirdParty, "Adobe app updates, fonts, or licensing prompts may appear later."),
        new(["nvidia"], false, "Usually worth keeping if you use Nvidia graphics features.", "Starts Nvidia helper processes for display, GPU, and control panel features.", StartupItemClassification.KnownThirdParty, "Display tuning or Nvidia overlays may not load until opened manually."),
        new(["realtek", "rtkaud", "audio"], false, "Usually keep this unless audio troubleshooting points here.", "Loads audio driver helpers for speaker, headset, or microphone hardware.", StartupItemClassification.KnownThirdParty, "Audio enhancements, jack detection, or control panels may stop working correctly."),
        new(["logitech"], true, "Review this if device customization does not need to load at sign-in.", "Starts Logitech device settings and profile switching services.", StartupItemClassification.KnownThirdParty, "Custom mouse, keyboard, or webcam profiles may not apply until the app is opened."),
        new(["dropbox"], true, "Review this if Dropbox sync does not need to start immediately.", "Starts Dropbox sync and notification services at sign-in.", StartupItemClassification.KnownThirdParty, "Dropbox files may stop syncing until the app is opened.")
    ];
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

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
//  SCHEDULER SERVICE  â€” Weekly Tune-Up via Windows Task Scheduler
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
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
