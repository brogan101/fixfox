using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using LibreHardwareMonitor.Hardware;
using Newtonsoft.Json;

namespace HelpDesk.Infrastructure.Services;

// ══════════════════════════════════════════════════════════════════════════
//  SETTINGS SERVICE  — atomic writes, auto-recovery from corruption
// ══════════════════════════════════════════════════════════════════════════
public sealed class SettingsService : ISettingsService
{
    private static readonly string Dir  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FixFox");
    private static readonly string _settingsPath = System.IO.Path.Combine(Dir, "settings.json");
    private static readonly string Bak           = System.IO.Path.Combine(Dir, "settings.bak.json");

    public AppSettings Load()
    {
        foreach (var file in new[] { _settingsPath, Bak })
        {
            try
            {
                if (File.Exists(file))
                {
                    var obj = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(file));
                    if (obj is not null) return Validated(obj);
                }
            }
            catch { /* try backup */ }
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Dir);
        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        // Atomic write: temp → rename
        var tmp = _settingsPath + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_settingsPath)) File.Copy(_settingsPath, Bak, overwrite: true);
        File.Move(tmp, _settingsPath, overwrite: true);
    }

    // Clamp all numeric settings to sane ranges
    private static AppSettings Validated(AppSettings s)
    {
        s.CpuWarningPct    = Math.Clamp(s.CpuWarningPct,    50, 99);
        s.RamWarningPct    = Math.Clamp(s.RamWarningPct,    50, 99);
        s.DiskWarningPct   = Math.Clamp(s.DiskWarningPct,   50, 99);
        s.CpuTempWarningC  = Math.Clamp(s.CpuTempWarningC,  50, 110);
        s.WindowWidth      = Math.Clamp(s.WindowWidth,       800, 3840);
        s.WindowHeight     = Math.Clamp(s.WindowHeight,      500, 2160);
        return s;
    }
}

// ══════════════════════════════════════════════════════════════════════════
//  LOG SERVICE  — 500-entry ring buffer, atomic save
// ══════════════════════════════════════════════════════════════════════════
public sealed class LogService : ILogService
{
    private const int MaxEntries = 500;
    private static readonly string FilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FixFox", "history.json");

    private List<FixLogEntry> _entries = [];
    public IReadOnlyList<FixLogEntry> Entries => _entries.AsReadOnly();

    public LogService() => Load();

    public void Record(string category, FixItem fix)
    {
        _entries.Insert(0, new FixLogEntry
        {
            Category = category,
            FixTitle = fix.Title,
            FixId    = fix.Id,
            Success  = fix.Status == FixStatus.Success,
            Output   = Truncate(fix.LastOutput ?? "", 500),
        });
        if (_entries.Count > MaxEntries)
            _entries = _entries.Take(MaxEntries).ToList();
        SaveAsync();
    }

    public void Clear() { _entries.Clear(); SaveAsync(); }

    private void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                _entries = JsonConvert.DeserializeObject<List<FixLogEntry>>(
                    File.ReadAllText(FilePath)) ?? [];
        }
        catch { _entries = []; }
    }

    private void SaveAsync()
    {
        var entries = _entries.ToList(); // snapshot
        var path    = FilePath;
        Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(entries, Formatting.Indented));
                File.Move(tmp, path, overwrite: true);
            }
            catch { }
        });
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

// ══════════════════════════════════════════════════════════════════════════
//  NOTIFICATION SERVICE  — 200-item cap, atomic save
// ══════════════════════════════════════════════════════════════════════════
public sealed class NotificationService : INotificationService
{
    private const int MaxItems = 200;
    private static readonly string FilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FixFox", "notifications.json");

    private List<AppNotification> _items = [];
    public IReadOnlyList<AppNotification> All         => _items.AsReadOnly();
    public int                            UnreadCount => _items.Count(n => !n.IsRead);

    public NotificationService() => Load();

    public void Add(AppNotification n)
    {
        _items.Insert(0, n);
        if (_items.Count > MaxItems) _items = _items.Take(MaxItems).ToList();
        SaveAsync();
    }

    public void AddFromScanResult(ScanResult scan)
    {
        if (scan.Severity == ScanSeverity.Good) return;
        Add(new AppNotification
        {
            Level       = scan.Severity == ScanSeverity.Critical ? NotifLevel.Critical : NotifLevel.Warning,
            Title       = scan.Title,
            Message     = scan.Suggestion ?? scan.Detail,
            ActionFixId = scan.FixId,
        });
    }

    public void MarkRead(string notificationId)
    {
        var item = _items.FirstOrDefault(n => n.Id == notificationId);
        if (item is null || item.IsRead) return;
        item.IsRead = true;
        SaveAsync();
    }

    public void MarkAllRead() { foreach (var n in _items) n.IsRead = true; SaveAsync(); }
    public void Remove(string notificationId)
    {
        _items.RemoveAll(n => n.Id == notificationId);
        SaveAsync();
    }
    public void Clear() { _items.Clear(); SaveAsync(); }

    private void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                _items = JsonConvert.DeserializeObject<List<AppNotification>>(
                    File.ReadAllText(FilePath)) ?? [];
        }
        catch { _items = []; }
    }

    private void SaveAsync()
    {
        var snap = _items.ToList();
        var path = FilePath;
        Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(snap, Formatting.Indented));
                File.Move(tmp, path, overwrite: true);
            }
            catch { }
        });
    }
}

// ══════════════════════════════════════════════════════════════════════════
//  SYSTEM INFO SERVICE  — every WMI query individually isolated
// ══════════════════════════════════════════════════════════════════════════
public sealed class SystemInfoService : ISystemInfoService, IDisposable
{
    private Computer? _hw;
    private bool      _hwReady;

    public SystemInfoService()
    {
        try
        {
            _hw = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
            _hw.Open();
            _hwReady = true;
        }
        catch { _hwReady = false; }
    }

    public async Task<SystemSnapshot> GetSnapshotAsync()
    {
        var base_ = new SystemSnapshot
        {
            MachineName = Environment.MachineName,
            UserName    = Environment.UserName,
            Uptime      = FormatUptime(TimeSpan.FromMilliseconds(Environment.TickCount64)),
        };

        // Run independent WMI queries in parallel, then merge
        var osTask       = Task.Run(() => WithOs(base_));
        var cpuTask      = Task.Run(() => WithCpu(base_));
        var ramTask      = Task.Run(() => WithRam(base_));
        var diskTask     = Task.Run(() => WithDisk(base_));
        var gpuTask      = Task.Run(() => WithGpu(base_));
        var netTask      = Task.Run(() => WithNetwork(base_));
        var batteryTask  = Task.Run(() => WithBattery(base_));
        var secTask      = Task.Run(() => WithSecurity(base_));
        var boardTask    = Task.Run(() => WithBoard(base_));

        await Task.WhenAll(osTask, cpuTask, ramTask, diskTask,
                           gpuTask, netTask, batteryTask, secTask, boardTask);

        // Merge all partial snapshots into one
        var snap = base_
            with
            {
                OsVersion          = osTask.Result.OsVersion,
                OsBuild            = osTask.Result.OsBuild,
                LastBoot           = osTask.Result.LastBoot,
                CpuName            = cpuTask.Result.CpuName,
                CpuCores           = cpuTask.Result.CpuCores,
                CpuThreads         = cpuTask.Result.CpuThreads,
                CpuSpeedGhz        = cpuTask.Result.CpuSpeedGhz,
                CpuUsagePct        = cpuTask.Result.CpuUsagePct,
                RamTotalMb         = ramTask.Result.RamTotalMb,
                RamFreeMb          = ramTask.Result.RamFreeMb,
                RamUsedPct         = ramTask.Result.RamUsedPct,
                DiskTotalGb        = diskTask.Result.DiskTotalGb,
                DiskFreeGb         = diskTask.Result.DiskFreeGb,
                DiskUsedPct        = diskTask.Result.DiskUsedPct,
                DiskType           = diskTask.Result.DiskType,
                DiskHealth         = diskTask.Result.DiskHealth,
                GpuName            = gpuTask.Result.GpuName,
                GpuVramMb          = gpuTask.Result.GpuVramMb,
                NetworkAdapterName = netTask.Result.NetworkAdapterName,
                NetworkType        = netTask.Result.NetworkType,
                IpAddress          = netTask.Result.IpAddress,
                NetworkSpeedMbps   = netTask.Result.NetworkSpeedMbps,
                InternetReachable  = netTask.Result.InternetReachable,
                HasBattery         = batteryTask.Result.HasBattery,
                BatteryPct         = batteryTask.Result.BatteryPct,
                BatteryStatus      = batteryTask.Result.BatteryStatus,
                DefenderEnabled    = secTask.Result.DefenderEnabled,
                DefenderUpdated    = secTask.Result.DefenderUpdated,
                WindowsActivated   = secTask.Result.WindowsActivated,
                Motherboard        = boardTask.Result.Motherboard,
                BiosVersion        = boardTask.Result.BiosVersion,
            };

        // Temps use LibreHardwareMonitor — must stay on a single thread
        snap = WithTemps(snap);
        return snap;
    }

    // Keep old sync path for callers that need it
    private SystemSnapshot Capture() => GetSnapshotAsync().GetAwaiter().GetResult();

    private static SystemSnapshot WithOs(SystemSnapshot s)
    {
        try
        {
            using var q = Wmi("SELECT Caption,BuildNumber,LastBootUpTime FROM Win32_OperatingSystem");
            foreach (ManagementObject o in q.Get())
            {
                var lastBoot = SafeDateTime(o["LastBootUpTime"]?.ToString());
                s = s with
                {
                    OsVersion  = o["Caption"]?.ToString() ?? "",
                    OsBuild    = o["BuildNumber"]?.ToString() ?? "",
                    LastBoot   = lastBoot,
                };
            }
        }
        catch { }
        return s;
    }

    private static SystemSnapshot WithCpu(SystemSnapshot s)
    {
        try
        {
            using var q = Wmi("SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject o in q.Get())
            {
                s = s with
                {
                    CpuName    = o["Name"]?.ToString()?.Trim() ?? "",
                    CpuCores   = SafeInt(o["NumberOfCores"]),
                    CpuThreads = SafeInt(o["NumberOfLogicalProcessors"]),
                    CpuSpeedGhz = $"{Math.Round(SafeInt(o["MaxClockSpeed"]) / 1000.0, 2):F2} GHz",
                };
            }
        }
        catch { }

        try
        {
            using var q = Wmi("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
            foreach (ManagementObject o in q.Get())
                s = s with { CpuUsagePct = SafeFloat(o["PercentProcessorTime"]) };
        }
        catch { }

        return s;
    }

    private static SystemSnapshot WithRam(SystemSnapshot s)
    {
        try
        {
            using var q = Wmi("SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject o in q.Get())
            {
                var total = SafeLong(o["TotalVisibleMemorySize"]) / 1024;
                var free  = SafeLong(o["FreePhysicalMemory"])     / 1024;
                s = s with
                {
                    RamTotalMb = total,
                    RamFreeMb  = free,
                    RamUsedPct = total > 0 ? 100f * (total - free) / total : 0,
                };
            }
        }
        catch { }
        return s;
    }

    private static SystemSnapshot WithDisk(SystemSnapshot s)
    {
        try
        {
            var drive = new System.IO.DriveInfo("C");
            if (drive.IsReady)
            {
                var total   = drive.TotalSize / (1024 * 1024 * 1024);
                var free    = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                var usedPct = total > 0 ? 100f * (total - free) / total : 0;
                s = s with { DiskTotalGb = total, DiskFreeGb = free, DiskUsedPct = usedPct };
            }
        }
        catch { }

        try
        {
            using var q = Wmi("SELECT MediaType FROM Win32_DiskDrive WHERE Index=0");
            foreach (ManagementObject o in q.Get())
            {
                var media = o["MediaType"]?.ToString() ?? "";
                s = s with { DiskType = media.Contains("SSD", StringComparison.OrdinalIgnoreCase) ? "SSD" : "HDD" };
            }
        }
        catch { }

        try
        {
            var disk = GetPhysicalDisk();
            if (disk is not null)
                s = s with { DiskHealth = disk };
        }
        catch { }

        return s;
    }

    private static string? GetPhysicalDisk()
    {
        try
        {
            using var q = new ManagementObjectSearcher(@"\\.\root\Microsoft\Windows\Storage",
                "SELECT HealthStatus FROM MSFT_PhysicalDisk WHERE BusType != 7");
            foreach (ManagementObject o in q.Get())
            {
                var h = SafeInt(o["HealthStatus"]);
                return h switch { 0 => "Healthy", 1 => "Warning", 2 => "Unhealthy", _ => "Unknown" };
            }
        }
        catch { }
        return null;
    }

    private static SystemSnapshot WithGpu(SystemSnapshot s)
    {
        try
        {
            using var q = Wmi("SELECT Name,AdapterRAM FROM Win32_VideoController WHERE PNPDeviceID LIKE 'PCI%'");
            foreach (ManagementObject o in q.Get())
            {
                var vram = SafeLong(o["AdapterRAM"]) / (1024 * 1024);
                s = s with { GpuName = o["Name"]?.ToString() ?? "", GpuVramMb = vram };
                break; // primary GPU only
            }
        }
        catch { }
        return s;
    }

    private static SystemSnapshot WithNetwork(SystemSnapshot s)
    {
        try
        {
            var iface = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                         && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderByDescending(n => n.Speed)
                .FirstOrDefault();

            if (iface is not null)
            {
                var ip = iface.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ?.Address.ToString() ?? "";

                s = s with
                {
                    NetworkAdapterName = iface.Description,
                    NetworkType        = iface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "Ethernet",
                    IpAddress          = ip,
                    NetworkSpeedMbps   = iface.Speed > 0 ? iface.Speed / 1_000_000 : 0,
                };
            }
        }
        catch { }

        try
        {
            using var ping  = new Ping();
            var       reply = ping.Send("8.8.8.8", 2000);
            s = s with { InternetReachable = reply.Status == IPStatus.Success };
        }
        catch { }

        return s;
    }

    private static SystemSnapshot WithBattery(SystemSnapshot s)
    {
        try
        {
            using var q = Wmi("SELECT BatteryStatus,EstimatedChargeRemaining FROM Win32_Battery");
            foreach (ManagementObject o in q.Get())
            {
                var pct    = SafeInt(o["EstimatedChargeRemaining"]);
                var status = SafeInt(o["BatteryStatus"]);
                var label  = status switch
                {
                    1 => "Discharging",
                    2 => "Plugged in (AC)",
                    3 => "Fully charged",
                    4 => "Low",
                    5 => "Critical",
                    _ => "Unknown"
                };
                s = s with { HasBattery = true, BatteryPct = pct, BatteryStatus = label };
                break;
            }
        }
        catch { }
        return s;
    }

    private static SystemSnapshot WithSecurity(SystemSnapshot s)
    {
        try
        {
            using var q = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Defender", "SELECT * FROM MSFT_MpComputerStatus");
            foreach (ManagementObject o in q.Get())
            {
                var avEnabled = SafeBool(o["AntivirusEnabled"]);
                var rtEnabled = SafeBool(o["RealTimeProtectionEnabled"]);
                var defAge    = SafeInt(o["AntivirusSignatureAge"]);
                s = s with
                {
                    DefenderEnabled  = avEnabled && rtEnabled,
                    DefenderUpdated  = defAge <= 7,
                };
            }
        }
        catch { }

        try
        {
            using var q = Wmi("SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE ApplicationId='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL");
            foreach (ManagementObject o in q.Get())
                s = s with { WindowsActivated = SafeInt(o["LicenseStatus"]) == 1 };
        }
        catch { }

        return s;
    }

    private SystemSnapshot WithTemps(SystemSnapshot s)
    {
        if (!_hwReady || _hw is null) return s;
        try
        {
            _hw.Accept(new SensorVisitor());
            float cpuTemp = 0, gpuTemp = 0;
            foreach (var hw in _hw.Hardware)
            {
                hw.Update();
                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                    {
                        if (hw.HardwareType == HardwareType.Cpu && cpuTemp == 0)
                            cpuTemp = sensor.Value.Value;
                        if ((hw.HardwareType == HardwareType.GpuNvidia ||
                             hw.HardwareType == HardwareType.GpuAmd ||
                             hw.HardwareType == HardwareType.GpuIntel) && gpuTemp == 0)
                            gpuTemp = sensor.Value.Value;
                    }
                }
            }
            s = s with { CpuTempC = cpuTemp, GpuTempC = gpuTemp };
        }
        catch { }
        return s;
    }

    private static SystemSnapshot WithBoard(SystemSnapshot s)
    {
        try
        {
            using var q = Wmi("SELECT Manufacturer,Product FROM Win32_BaseBoard");
            foreach (ManagementObject o in q.Get())
                s = s with { Motherboard = $"{o["Manufacturer"]} {o["Product"]}".Trim() };
        }
        catch { }
        try
        {
            using var q = Wmi("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
            foreach (ManagementObject o in q.Get())
                s = s with { BiosVersion = o["SMBIOSBIOSVersion"]?.ToString() ?? "" };
        }
        catch { }
        return s;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static ManagementObjectSearcher Wmi(string query) =>
        new(query);

    private static int    SafeInt(object? v)   => v is null ? 0 : Convert.ToInt32(v);
    private static long   SafeLong(object? v)  => v is null ? 0 : Convert.ToInt64(v);
    private static float  SafeFloat(object? v) => v is null ? 0 : Convert.ToSingle(v);
    private static bool   SafeBool(object? v)  => v is not null && Convert.ToBoolean(v);

    private static DateTime SafeDateTime(string? s)
    {
        try { return s is not null ? ManagementDateTimeConverter.ToDateTime(s) : default; }
        catch { return default; }
    }

    private static string FormatUptime(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)  return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1) return $"{ts.Hours}h {ts.Minutes}m";
        return $"{ts.Minutes}m";
    }

    public void Dispose()
    {
        try { _hw?.Close(); } catch { }
    }

    private sealed class SensorVisitor : IVisitor
    {
        public void VisitComputer(IComputer c) => c.Traverse(this);
        public void VisitHardware(IHardware h) { h.Update(); foreach (var s in h.SubHardware) s.Accept(this); }
        public void VisitSensor(ISensor s) { }
        public void VisitParameter(IParameter p) { }
    }
}

// ══════════════════════════════════════════════════════════════════════════
//  QUICK SCAN SERVICE  — each check fully isolated, never crashes scan
// ══════════════════════════════════════════════════════════════════════════
public sealed class QuickScanService : IQuickScanService
{
    private readonly AppSettings _settings;

    public QuickScanService(ISettingsService settingsSvc)
        => _settings = settingsSvc.Load();

    public Task<IReadOnlyList<ScanResult>> ScanAsync() => Task.Run(RunAllChecks);

    private IReadOnlyList<ScanResult> RunAllChecks()
    {
        // Run all checks in parallel, collect results
        var tasks = new Func<ScanResult?>[]
        {
            CheckDisk, CheckRam, CheckDefender, CheckDrivers, CheckInternet,
            CheckWindowsUpdate, CheckPrintQueue, CheckBattery, CheckTempFiles,
            CheckUptime, CheckPendingReboot, CheckDesktopSize,
            CheckStartupCount, CheckSsdHealth, CheckWindowsLicense,
        };

        var results = tasks
            .AsParallel()
            .Select(t => { try { return t(); } catch { return null; } })
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();

        AddPassingChecks(results);
        results.Sort((a, b) => b.Severity.CompareTo(a.Severity));
        return results.AsReadOnly();
    }

    private ScanResult? CheckDisk()
    {
        try
        {
            var drive  = new System.IO.DriveInfo("C");
            if (!drive.IsReady) return null;
            var pct    = 100.0 * (drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize;
            var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);

            if (pct >= _settings.DiskWarningPct + 5)
                return Crit("C: drive critically full", $"{freeGb:N1} GB free ({pct:N0}% used)",
                    "Your drive is nearly full — this causes crashes and slowdowns. Free up space now.", "clear-temp-files");
            if (pct >= _settings.DiskWarningPct)
                return Warn("C: drive getting full", $"{freeGb:N1} GB free ({pct:N0}% used)",
                    "Running low on disk space. Clear temp files to recover space.", "clear-temp-files");
        }
        catch { }
        return null;
    }

    private ScanResult? CheckRam()
    {
        try
        {
            using var q = new System.Management.ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (System.Management.ManagementObject o in q.Get())
            {
                var total  = Convert.ToInt64(o["TotalVisibleMemorySize"]);
                var free   = Convert.ToInt64(o["FreePhysicalMemory"]);
                var pct    = 100.0 * (total - free) / total;
                var freeMb = free / 1024;

                if (pct >= _settings.RamWarningPct + 5)
                    return Crit("RAM critically high", $"{freeMb} MB free ({pct:N0}% used)",
                        "Very little memory left — apps may crash. Close unused programs.", "top-memory-processes");
                if (pct >= _settings.RamWarningPct)
                    return Warn("RAM usage high", $"{freeMb} MB free ({pct:N0}% used)",
                        "Memory is getting tight. Check what's using the most RAM.", "top-memory-processes");
            }
        }
        catch { }
        return null;
    }

    private static ScanResult? CheckDefender()
    {
        try
        {
            using var q = new System.Management.ManagementObjectSearcher(
                @"root\Microsoft\Windows\Defender", "SELECT * FROM MSFT_MpComputerStatus");
            foreach (System.Management.ManagementObject o in q.Get())
            {
                var avOn  = Convert.ToBoolean(o["AntivirusEnabled"]);
                var rtOn  = Convert.ToBoolean(o["RealTimeProtectionEnabled"]);
                var age   = Convert.ToInt32(o["AntivirusSignatureAge"]);

                if (!avOn || !rtOn)
                    return Crit("Antivirus protection is OFF", "Windows Defender real-time protection is disabled",
                        "Your PC is unprotected from viruses. Re-enable Defender immediately.", "check-defender-status");
                if (age > 7)
                    return Warn("Virus definitions outdated", $"Definitions are {age} days old",
                        "Outdated definitions can miss new threats. Update now.", "update-virus-definitions");
            }
        }
        catch { }
        return null;
    }

    private static ScanResult? CheckDrivers()
    {
        try
        {
            using var q = new System.Management.ManagementObjectSearcher(
                "SELECT Name FROM Win32_PnPEntity WHERE ConfigManagerErrorCode != 0");
            var bad = new List<string>();
            foreach (System.Management.ManagementObject o in q.Get())
                bad.Add(o["Name"]?.ToString() ?? "Unknown device");

            if (bad.Count > 0)
                return Warn($"{bad.Count} device driver problem{(bad.Count > 1 ? "s" : "")}",
                    string.Join(", ", bad.Take(2)) + (bad.Count > 2 ? $" +{bad.Count - 2} more" : ""),
                    "One or more devices have driver errors. This can cause unexpected disconnects or failures.", "scan-driver-problems");
        }
        catch { }
        return null;
    }

    private static ScanResult? CheckInternet()
    {
        try
        {
            using var ping  = new System.Net.NetworkInformation.Ping();
            var       reply = ping.Send("8.8.8.8", 2000);
            if (reply.Status != System.Net.NetworkInformation.IPStatus.Success)
                return Crit("No internet connection", "Cannot reach external servers",
                    "Your PC can't reach the internet. Run the Network Fix Pack.", "full-network-reset");
        }
        catch { }
        return null;
    }

    private static ScanResult? CheckWindowsUpdate()
    {
        try
        {
            using var q = new System.Management.ManagementObjectSearcher(
                "SELECT State FROM Win32_Service WHERE Name='wuauserv'");
            foreach (System.Management.ManagementObject o in q.Get())
            {
                var state = o["State"]?.ToString();
                if (state is "Disabled")
                    return Warn("Windows Update is disabled", "Service state: Disabled",
                        "Windows Update keeps your PC secure. It should not be disabled.", "open-windows-update");
            }
        }
        catch { }
        return null;
    }

    private static ScanResult? CheckPrintQueue()
    {
        try
        {
            using var q = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_PrintJob");
            var count = q.Get().Count;
            if (count > 0)
                return Warn($"{count} stuck print job{(count > 1 ? "s" : "")}", "Print queue has documents waiting",
                    "Stuck print jobs can block all future printing. Clear the queue to fix it.", "clear-print-queue");
        }
        catch { }
        return null;
    }

    private static ScanResult? CheckBattery()
    {
        try
        {
            using var q = new System.Management.ManagementObjectSearcher(
                "SELECT BatteryStatus,EstimatedChargeRemaining FROM Win32_Battery");
            foreach (System.Management.ManagementObject o in q.Get())
            {
                var pct    = Convert.ToInt32(o["EstimatedChargeRemaining"] ?? 100);
                var status = Convert.ToInt32(o["BatteryStatus"] ?? 2);
                if (status == 1 && pct <= 10)
                    return Crit("Battery critically low", $"{pct}% remaining — not charging",
                        "Plug in your charger now to avoid losing unsaved work.", null);
                if (status == 1 && pct <= 20)
                    return Warn("Battery low", $"{pct}% remaining", "Consider plugging in soon.", null);
            }
        }
        catch { }
        return null;
    }

    private ScanResult? CheckTempFiles()
    {
        try
        {
            var path = Environment.GetEnvironmentVariable("TEMP") ?? "";
            if (!System.IO.Directory.Exists(path)) return null;
            var sizeMb = GetDirSizeMb(path);
            if (sizeMb > 1024)
                return Warn("Large temp folder", $"{sizeMb / 1024.0:N1} GB of temp files",
                    "Over 1 GB of temporary files found — safe to clear for free space.", "clear-temp-files");
        }
        catch { }
        return null;
    }

    private static ScanResult? CheckUptime()
    {
        var days = TimeSpan.FromMilliseconds(Environment.TickCount64).TotalDays;
        if (days >= 14)
            return Warn($"PC hasn't restarted in {(int)days} days",
                "Long uptime can cause memory leaks and sluggish performance",
                "A restart clears cached memory and applies any pending updates.", null);
        return null;
    }

    private static ScanResult? CheckPendingReboot()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            if (key is not null)
                return Warn("Restart required", "A Windows Update is waiting for a reboot",
                    "Pending restarts can slow your PC and delay security patches. Restart when ready.", null);
        }
        catch { }
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager");
            var val = key?.GetValue("PendingFileRenameOperations");
            if (val is string[] arr && arr.Length > 0)
                return Warn("File operations pending reboot", $"{arr.Length / 2} files queued for rename/delete",
                    "Some file operations will complete after the next restart.", null);
        }
        catch { }
        return null;
    }

    private static ScanResult? CheckDesktopSize()
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (!System.IO.Directory.Exists(desktop)) return null;
            var sizeMb = GetDirSizeMb(desktop);
            if (sizeMb > 1024)
                return Warn("Desktop has large files", $"{sizeMb / 1024.0:N1} GB of files on Desktop",
                    "Storing large files on the Desktop can slow Explorer and search indexing.", null);
        }
        catch { }
        return null;
    }

    private static ScanResult? CheckStartupCount()
    {
        try
        {
            int count = 0;
            var paths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
            };
            foreach (var path in paths)
            {
                using var key = Registry.LocalMachine.OpenSubKey(path);
                if (key is not null) count += key.ValueCount;
                using var ukey = Registry.CurrentUser.OpenSubKey(path);
                if (ukey is not null) count += ukey.ValueCount;
            }
            if (count > 10)
                return Warn($"{count} startup programs", $"Many programs load at Windows startup",
                    "Too many startup programs slow your boot time. Audit and disable unneeded ones.", "manage-startup-programs");
        }
        catch { }
        return null;
    }

    private static ScanResult? CheckSsdHealth()
    {
        try
        {
            using var q = new System.Management.ManagementObjectSearcher(
                @"\\.\root\Microsoft\Windows\Storage",
                "SELECT HealthStatus FROM MSFT_PhysicalDisk WHERE BusType != 7");
            foreach (System.Management.ManagementObject o in q.Get())
            {
                var h = Convert.ToInt32(o["HealthStatus"] ?? 0);
                if (h == 1)
                    return Warn("SSD/HDD health warning", "Storage device is reporting degraded health",
                        "A disk in warning state may fail soon. Back up your data.", null);
                if (h == 2)
                    return Crit("SSD/HDD is unhealthy", "Storage device reports critical health failure",
                        "Your disk is failing. Back up everything immediately.", null);
            }
        }
        catch { }
        return null;
    }

    private static ScanResult? CheckWindowsLicense()
    {
        try
        {
            using var q = new System.Management.ManagementObjectSearcher(
                "SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE ApplicationId='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL");
            foreach (System.Management.ManagementObject o in q.Get())
            {
                var status = Convert.ToInt32(o["LicenseStatus"] ?? 0);
                if (status != 1)
                    return Warn("Windows may not be activated", $"License status code: {status}",
                        "An unlicensed copy of Windows shows watermarks and may miss some features.", "activate-windows");
            }
        }
        catch { }
        return null;
    }

    private static void AddPassingChecks(List<ScanResult> results)
    {
        // Only add Good results for areas that weren't flagged
        var ids = results.Select(r => r.FixId).ToHashSet();

        if (!ids.Contains("clear-temp-files") && !ids.Contains("full-network-reset"))
            results.Add(Good("Internet connection", "Connected and responding normally"));
        if (!ids.Contains("check-defender-status") && !ids.Contains("update-virus-definitions"))
            results.Add(Good("Antivirus", "Defender active and up to date"));
    }

    private static long GetDirSizeMb(string path)
    {
        try
        {
            return new System.IO.DirectoryInfo(path)
                .GetFiles("*", System.IO.SearchOption.AllDirectories)
                .Sum(f => { try { return f.Length; } catch { return 0L; } }) / (1024 * 1024);
        }
        catch { return 0; }
    }

    private static ScanResult Crit(string title, string detail, string suggestion, string? fixId) =>
        new() { Title = title, Detail = detail, Severity = ScanSeverity.Critical, Suggestion = suggestion, FixId = fixId };
    private static ScanResult Warn(string title, string detail, string suggestion, string? fixId) =>
        new() { Title = title, Detail = detail, Severity = ScanSeverity.Warning,  Suggestion = suggestion, FixId = fixId };
    private static ScanResult Good(string title, string detail) =>
        new() { Title = title, Detail = detail, Severity = ScanSeverity.Good };
}

// ── List extension helper ─────────────────────────────────────────────────
internal static class ListExtensions
{
    public static void AddIfNotNull<T>(this List<T> list, T? item) where T : class
    {
        if (item is not null) list.Add(item);
    }
}
