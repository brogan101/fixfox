using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;

namespace HelpDesk.Infrastructure.Fixes;

public sealed partial class FixCatalogService
{
    private static void ApplyBacklogExpansions(List<FixCategory> categories, List<FixBundle> bundles)
    {
        AddFixes(categories, "Network & Wi-Fi", NetworkBacklogFixes());
        AddFixes(categories, "Performance & Cleanup", PerformanceBacklogFixes());
        AddFixes(categories, "Audio & Display", AudioDisplayBacklogFixes());
        AddFixes(categories, "Updates & Drivers", UpdateBacklogFixes());
        AddFixes(categories, "File & Storage", StorageBacklogFixes());
        AddFixes(categories, "Security & Privacy", SecurityBacklogFixes());
        AddFixes(categories, "Blue Screen & Crashes", CrashBacklogFixes());
        AddFixes(categories, "Devices & USB", DeviceBacklogFixes());
        AddFixes(categories, "Printers & Peripherals", PrinterBacklogFixes());
        AddFixes(categories, "App Issues", AppRepairBacklogFixes());
        AddFixes(categories, "Email & Office", OfficeBacklogFixes());
        AddFixes(categories, "Remote Access & VPN", RemoteBacklogFixes());
        AddFixes(categories, "Sleep & Power", PowerBacklogFixes());
        AddFixes(categories, "Advanced Tools", AccountsAndProductBacklogFixes());
        AddFixes(categories, "Gaming & Streaming", GamingBacklogFixes());
        AddFixes(categories, "Windows Features", WindowsFeatureBacklogFixes());

        AddBundles(bundles, BacklogBundles());
    }

    private static void EnsureCatalogMetadata(List<FixCategory> categories)
    {
        var subtitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Network & Wi-Fi"] = "Repair internet, DNS, Wi-Fi, adapter, and browser connectivity issues.",
            ["Performance & Cleanup"] = "Tackle slow startup, disk pressure, cache buildup, and background slowdowns.",
            ["Audio & Display"] = "Diagnose speakers, microphones, webcams, monitors, refresh rate, and graphics issues.",
            ["Updates & Drivers"] = "Repair Windows Update, servicing, driver health, and reboot-related update blockers.",
            ["Printers & Peripherals"] = "Fix spooler, printers, scanners, ports, and common peripheral communication problems.",
            ["Gaming & Streaming"] = "Improve gaming stability, overlays, storage throughput, and streaming compatibility.",
            ["App Issues"] = "Repair broken app launches, runtimes, file associations, and install dependencies.",
            ["Security & Privacy"] = "Review Defender, firewall, SmartScreen, BitLocker, and privacy protections.",
            ["System Information"] = "Collect device, OS, and hardware details for diagnosis and support handoff.",
            ["Blue Screen & Crashes"] = "Investigate crashes, dump files, drivers, and corruption signals after failures.",
            ["Email & Office"] = "Repair Outlook, Teams, Microsoft 365, OneDrive, and mailbox-related problems.",
            ["File & Storage"] = "Review disks, large folders, duplicate files, hydration, and file-save protection issues.",
            ["Phone & Mobile"] = "Troubleshoot phone connection, charging, and mobile sync problems.",
            ["Remote Access & VPN"] = "Fix VPN, remote desktop, routes, DNS, and work-from-home connectivity paths.",
            ["Maintenance & Recovery"] = "Run conservative maintenance, restore-point, and recovery-oriented repair flows.",
            ["Sleep & Power"] = "Investigate battery, wake timers, thermal, and standby behavior.",
            ["Windows Features"] = "Repair Explorer, Start, notifications, clipboard, shell defaults, and usability features.",
            ["Advanced Tools"] = "Handle advanced sign-in, account, recovery, export, and technician-oriented tasks.",
            ["Devices & USB"] = "Inspect USB, Bluetooth, webcams, HID devices, and peripheral problem codes.",
            ["Windows Apps & Features"] = "Repair Store apps, Teams, Xbox, fonts, and Windows app platform problems.",
            ["Windows Tweaks"] = "Adjust Windows defaults, privacy, shell behavior, and safe customization settings."
        };

        foreach (var category in categories)
        {
            if (string.IsNullOrWhiteSpace(category.Subtitle) && subtitles.TryGetValue(category.Title, out var subtitle))
                category.Subtitle = subtitle;

            foreach (var fix in category.Fixes)
            {
                fix.Category = category.Title;
                if (string.IsNullOrWhiteSpace(fix.Description))
                    fix.Description = $"Runs a real {category.Title.ToLowerInvariant()} repair workflow for this problem.";
                if (fix.EstimatedDurationSeconds <= 0)
                    fix.EstimatedDurationSeconds = EstimateDurationSeconds(fix.EstTime);
                if (string.IsNullOrWhiteSpace(fix.EstTime))
                    fix.EstTime = FormatDuration(fix.EstimatedDurationSeconds);
            }
        }
    }

    private static void AddFixes(List<FixCategory> categories, string categoryTitle, IEnumerable<FixItem> fixes)
    {
        var category = categories.First(category => string.Equals(category.Title, categoryTitle, StringComparison.OrdinalIgnoreCase));
        var existingIds = category.Fixes.Select(fix => fix.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var fix in fixes)
        {
            if (existingIds.Add(fix.Id))
                category.Fixes.Add(fix);
        }
    }

    private static void AddBundles(List<FixBundle> bundles, IEnumerable<FixBundle> additions)
    {
        var existingIds = bundles.Select(bundle => bundle.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var bundle in additions)
        {
            if (existingIds.Add(bundle.Id))
                bundles.Add(bundle);
        }
    }

    private static FixItem Silent(
        string id,
        string title,
        string description,
        bool requiresAdmin,
        int seconds,
        string script,
        FixRiskLevel riskLevel,
        params string[] keywords) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Type = FixType.Silent,
            RequiresAdmin = requiresAdmin,
            EstimatedDurationSeconds = seconds,
            EstTime = FormatDuration(seconds),
            RiskLevel = riskLevel,
            Script = script,
            Keywords = keywords,
            Tags = keywords
        };

    private static FixItem Guided(
        string id,
        string title,
        string description,
        bool requiresAdmin,
        int seconds,
        FixRiskLevel riskLevel,
        string[] keywords,
        params FixStep[] steps) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Type = FixType.Guided,
            RequiresAdmin = requiresAdmin,
            EstimatedDurationSeconds = seconds,
            EstTime = FormatDuration(seconds),
            RiskLevel = riskLevel,
            Keywords = keywords,
            Tags = keywords,
            Steps = steps.ToList()
        };

    private static FixStep Step(string id, string title, string instruction, string script) => new()
    {
        Id = id,
        Title = title,
        Instruction = instruction,
        Script = script
    };

    private static FixBundle Bundle(string id, string title, string description, int seconds, params string[] fixIds) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            EstTime = FormatDuration(seconds),
            FixIds = fixIds.ToList()
        };

    private static string FormatDuration(int seconds)
    {
        if (seconds <= 60)
            return "~1 min";

        var minutes = (int)Math.Ceiling(seconds / 60d);
        return $"~{minutes} min";
    }

    private static int EstimateDurationSeconds(string estTime)
    {
        if (string.IsNullOrWhiteSpace(estTime))
            return 60;

        var digits = new string(estTime.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var minutes) && minutes > 0
            ? minutes * 60
            : 60;
    }

    private static IEnumerable<FixItem> NetworkBacklogFixes()
    {
        yield return Silent(
            "export-wifi-diagnostics-report",
            "Export Wi-Fi Diagnostics Report",
            "Generates the built-in Wi-Fi report and opens the report folder so connection problems can be reviewed safely.",
            false,
            45,
            """
            netsh wlan show wlanreport | Out-Null
            $report = Join-Path $env:ProgramData 'Microsoft\Windows\WlanReport\wlan-report-latest.html'
            if (Test-Path $report) {
                Write-Output "Wi-Fi report created:"
                Write-Output $report
                Start-Process explorer.exe "/select,$report"
            } else {
                Write-Output "Wi-Fi report was requested, but the latest report was not found."
                exit 1
            }
            """,
            FixRiskLevel.Safe,
            "wifi report", "wireless diagnostics", "wlan report");

        yield return Silent(
            "measure-packet-loss-targets",
            "Measure Packet Loss By Target",
            "Tests packet loss to the router, DNS, and a public IP so users can see where the connection is dropping.",
            false,
            75,
            """
            $defaultRoute = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -AddressFamily IPv4 -ErrorAction SilentlyContinue |
                Sort-Object RouteMetric |
                Select-Object -First 1
            $gateway = if ($defaultRoute) { $defaultRoute.NextHop } else { $null }
            $targets = @()
            if ($gateway -and $gateway -ne '0.0.0.0') { $targets += @{ Name = 'Router'; Host = $gateway } }
            $targets += @(
                @{ Name = 'Cloudflare DNS'; Host = '1.1.1.1' },
                @{ Name = 'Google DNS'; Host = '8.8.8.8' }
            )
            foreach ($target in $targets) {
                $sent = 12
                $results = Test-Connection -TargetName $target.Host -Count $sent -ErrorAction SilentlyContinue
                $received = @($results).Count
                $loss = [math]::Round((($sent - $received) / $sent) * 100, 1)
                $average = if ($received -gt 0) { [math]::Round(($results | Measure-Object -Property ResponseTime -Average).Average, 1) } else { 0 }
                Write-Output "$($target.Name): $loss% loss, average ${average}ms"
            }
            """,
            FixRiskLevel.Safe,
            "packet loss", "router test", "dns test");

        yield return Silent(
            "reset-winsock-only",
            "Reset Winsock Only",
            "Resets only the Winsock catalog to fix socket corruption without doing a full network reset.",
            true,
            30,
            """
            netsh winsock reset | Write-Output
            Write-Output ""
            Write-Output "Winsock reset is complete. Restart Windows to finish applying the change."
            """,
            FixRiskLevel.NeedsAdmin,
            "winsock", "socket reset", "network stack");

        yield return Silent(
            "enable-disabled-network-adapters",
            "Enable Disabled Network Adapters",
            "Re-enables disabled network adapters and shows their current status for safe adapter recovery.",
            true,
            40,
            """
            $disabled = Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object Status -eq 'Disabled'
            if (-not $disabled) {
                Write-Output "No disabled network adapters were found."
                exit 0
            }
            foreach ($adapter in $disabled) {
                Enable-NetAdapter -Name $adapter.Name -Confirm:$false -ErrorAction SilentlyContinue
                $current = Get-NetAdapter -Name $adapter.Name -ErrorAction SilentlyContinue
                Write-Output "$($adapter.Name): $($current.Status)"
            }
            """,
            FixRiskLevel.NeedsAdmin,
            "adapter disabled", "network adapter", "enable wifi");

        yield return Silent(
            "review-metered-connection-state",
            "Review Metered Connection State",
            "Shows the metered-connection setting for active profiles and opens the Windows page that changes it.",
            false,
            35,
            """
            Get-NetConnectionProfile -ErrorAction SilentlyContinue | ForEach-Object {
                Write-Output "$($_.Name): Cost = $($_.ConnectionCost)"
            }
            Start-Process ms-settings:network-status
            Write-Output ""
            Write-Output "Open the active network, then switch off 'Set as metered connection' if downloads are being held back."
            """,
            FixRiskLevel.Safe,
            "metered connection", "slow update downloads", "network cost");

        yield return Guided(
            "forget-and-readd-wifi-profile",
            "Forget And Re-Add Wi-Fi Profile",
            "Guides users through removing a broken Wi-Fi profile, then reconnecting with a clean saved profile.",
            false,
            180,
            FixRiskLevel.Safe,
            ["forget wifi", "re-add wifi", "saved wifi profile"],
            Step(
                "wifi-profile-check",
                "Check Saved Profiles",
                "Review the saved Wi-Fi profiles before you remove the broken one.",
                """
                netsh wlan show profiles
                """
            ),
            Step(
                "wifi-settings-open",
                "Open Wi-Fi Settings",
                "Open the Windows Wi-Fi settings page for managing known networks.",
                """
                Start-Process ms-settings:network-wifi
                """
            ),
            Step(
                "wifi-interface-verify",
                "Verify Reconnected State",
                "After reconnecting, verify that Windows now shows the correct SSID and signal state.",
                """
                netsh wlan show interfaces
                """
            )
        );

        yield return Silent(
            "flush-ncsi-state",
            "Flush NCSI And Captive Portal State",
            "Clears NCSI and captive-portal cache so Windows can stop showing false 'No internet' warnings.",
            false,
            25,
            """
            ipconfig /flushdns | Out-Null
            Remove-Item "$env:LOCALAPPDATA\Microsoft\NCSI\*" -Recurse -Force -ErrorAction SilentlyContinue
            Start-Service NlaSvc -ErrorAction SilentlyContinue
            Write-Output "NCSI state was cleared. Reconnect to your network if the warning stays stuck."
            """,
            FixRiskLevel.Safe,
            "ncsi", "no internet warning", "captive portal");

        yield return Silent(
            "show-dhcp-lease-details",
            "Show DHCP Lease Details",
            "Displays DHCP lease timing details so users can see when the current address will expire or renew.",
            false,
            20,
            """
            Get-NetIPConfiguration -Detailed -ErrorAction SilentlyContinue | ForEach-Object {
                if ($_.IPv4DefaultGateway) {
                    Write-Output "Adapter: $($_.InterfaceAlias)"
                    Write-Output "Gateway: $($_.IPv4DefaultGateway.NextHop)"
                }
            }
            ipconfig /all | Select-String 'DHCP Enabled|Lease Obtained|Lease Expires' | ForEach-Object { $_.Line.Trim() }
            """,
            FixRiskLevel.Safe,
            "dhcp lease", "renew timing", "ip lease");

        yield return Silent(
            "detect-broken-ipv6-fallback",
            "Detect Broken IPv6 Fallback",
            "Shows current IPv6 adapter and route state so broken IPv6 behavior can be reviewed safely before changing adapters.",
            false,
            30,
            """
            Get-NetIPAddress -AddressFamily IPv6 -ErrorAction SilentlyContinue |
                Select-Object InterfaceAlias, IPAddress, PrefixOrigin, AddressState |
                Format-Table -AutoSize
            Get-NetRoute -AddressFamily IPv6 -ErrorAction SilentlyContinue |
                Select-Object -First 20 DestinationPrefix, NextHop, InterfaceAlias, RouteMetric |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "ipv6", "broken ipv6", "ipv6 fallback");

        yield return Silent(
            "test-dns-resolution-speed",
            "Test DNS Resolution Speed",
            "Measures lookup speed against common public DNS providers so users can compare slow name-resolution paths safely.",
            false,
            70,
            """
            $servers = @(
                @{ Name = 'Cloudflare'; Server = '1.1.1.1' },
                @{ Name = 'Google'; Server = '8.8.8.8' },
                @{ Name = 'Quad9'; Server = '9.9.9.9' }
            )
            foreach ($server in $servers) {
                $measure = Measure-Command {
                    Resolve-DnsName -Name 'www.microsoft.com' -Server $server.Server -DnsOnly -ErrorAction SilentlyContinue | Out-Null
                }
                Write-Output "$($server.Name): $([math]::Round($measure.TotalMilliseconds, 1)) ms"
            }
            """,
            FixRiskLevel.Safe,
            "dns speed", "resolve dns", "slow websites");

        yield return Silent(
            "reset-current-user-proxy",
            "Reset Current User Proxy",
            "Clears WinHTTP and current-user proxy settings to fix browser and app traffic routing problems safely.",
            false,
            20,
            """
            netsh winhttp reset proxy | Write-Output
            Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -Name ProxyEnable -Value 0 -Type DWord -ErrorAction SilentlyContinue
            Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -Name ProxyServer -ErrorAction SilentlyContinue
            Write-Output "Current user proxy settings were reset."
            """,
            FixRiskLevel.Safe,
            "proxy reset", "browser proxy", "winhttp");

        yield return Silent(
            "detect-stale-pac-script",
            "Detect Stale PAC Script",
            "Checks for auto-proxy and PAC settings that often break browsing after VPN or office-network changes.",
            false,
            20,
            """
            $settings = Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -ErrorAction SilentlyContinue
            Write-Output "Auto detect: $($settings.AutoDetect)"
            Write-Output "Auto config URL: $($settings.AutoConfigURL)"
            if ($settings.AutoConfigURL) {
                Write-Output "Review the PAC URL above if browsers are slow or cannot open internal sites."
            }
            """,
            FixRiskLevel.Safe,
            "pac script", "auto proxy", "proxy config");

        yield return Silent(
            "clear-chrome-dns-and-sockets",
            "Open Chrome DNS And Socket Cleanup",
            "Opens Chrome internal pages for clearing host resolver and socket pools without touching user content.",
            false,
            15,
            """
            Start-Process 'chrome://net-internals/#dns'
            Start-Process 'chrome://net-internals/#sockets'
            Write-Output "Chrome internal DNS and socket pages were opened."
            """,
            FixRiskLevel.Safe,
            "chrome dns cache", "chrome sockets", "browser cache");

        yield return Silent(
            "clear-edge-dns-and-sockets",
            "Open Edge DNS And Socket Cleanup",
            "Opens Edge internal pages for clearing host resolver and socket pools without clearing browsing data.",
            false,
            15,
            """
            Start-Process 'edge://net-internals/#dns'
            Start-Process 'edge://net-internals/#sockets'
            Write-Output "Edge internal DNS and socket pages were opened."
            """,
            FixRiskLevel.Safe,
            "edge dns cache", "edge sockets", "browser cache");

        yield return Guided(
            "firefox-safe-mode-helper",
            "Open Firefox Troubleshoot Mode",
            "Launches Firefox Troubleshoot Mode and checks for running processes so extension conflicts can be tested safely.",
            false,
            120,
            FixRiskLevel.Safe,
            ["firefox safe mode", "firefox extension conflict", "troubleshoot mode"],
            Step(
                "firefox-process-check",
                "Check Firefox Process State",
                "See whether Firefox is already running before opening Troubleshoot Mode.",
                """
                Get-Process firefox -ErrorAction SilentlyContinue | Select-Object ProcessName, Id, StartTime
                """
            ),
            Step(
                "firefox-safe-mode-open",
                "Open Troubleshoot Mode",
                "Open Firefox Troubleshoot Mode for extension conflict testing.",
                """
                Start-Process 'firefox.exe' '-safe-mode'
                """
            ),
            Step(
                "firefox-post-check",
                "Verify Firefox Opened",
                "Confirm that Firefox opened in Troubleshoot Mode.",
                """
                Get-Process firefox -ErrorAction SilentlyContinue | Select-Object ProcessName, Id, MainWindowTitle
                """
            )
        );

        yield return Silent(
            "reset-hosts-file-default",
            "Reset Hosts File To Default",
            "Backs up the current hosts file and restores the Microsoft default mapping safely for browser and update issues.",
            true,
            35,
            """
            $hosts = Join-Path $env:WINDIR 'System32\drivers\etc\hosts'
            $backup = "$hosts.fixfox.bak"
            Copy-Item $hosts $backup -Force
            @(
                '# Copyright (c) Microsoft Corp.',
                '#',
                '# This is a sample HOSTS file used by Microsoft TCP/IP for Windows.',
                '#',
                '127.0.0.1       localhost',
                '::1             localhost'
            ) | Set-Content -Path $hosts -Encoding ASCII
            Write-Output "Hosts file reset. Backup saved to $backup"
            """,
            FixRiskLevel.NeedsAdmin,
            "hosts file", "bad redirects", "website wrong page");

        yield return Silent(
            "detect-split-dns-vpn-issues",
            "Detect Split DNS VPN Issues",
            "Tests current DNS client settings so split-DNS problems on work VPNs can be reviewed safely.",
            false,
            30,
            """
            Get-DnsClientServerAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
                Select-Object InterfaceAlias, ServerAddresses |
                Format-Table -AutoSize
            Resolve-DnsName microsoft.com -ErrorAction SilentlyContinue | Select-Object -First 3 Name, IPAddress
            """,
            FixRiskLevel.Safe,
            "split dns", "vpn dns", "internal name resolution");

        yield return Silent(
            "open-browser-certificate-repair-tools",
            "Open Browser Certificate Repair Tools",
            "Opens certificate and date-and-time tools so TLS and certificate problems can be reviewed safely.",
            false,
            20,
            """
            Start-Process certmgr.msc
            Start-Process timedate.cpl
            Write-Output "Certificate Manager and Date & Time were opened."
            """,
            FixRiskLevel.Safe,
            "certificate error", "tls error", "browser cert");
    }

    private static IEnumerable<FixItem> PerformanceBacklogFixes()
    {
        yield return Silent(
            "list-startup-impact-apps",
            "List Startup Impact Apps",
            "Shows startup apps with publisher, path, and estimated impact so users can review what slows boot safely.",
            false,
            35,
            """
            Get-CimInstance Win32_StartupCommand -ErrorAction SilentlyContinue |
                Select-Object Name, Command, User, Location |
                Sort-Object Name |
                Format-List
            """,
            FixRiskLevel.Safe,
            "startup impact", "slow boot", "startup apps");

        yield return Silent(
            "list-scheduled-tasks-by-trigger",
            "List Scheduled Tasks By Trigger",
            "Shows scheduled tasks with trigger information and vendor clues so users can review noisy background jobs safely.",
            false,
            40,
            """
            Get-ScheduledTask -ErrorAction SilentlyContinue |
                Select-Object TaskName, TaskPath, Author, State,
                    @{Name='Triggers';Expression={($_.Triggers | ForEach-Object ToString) -join '; '}} |
                Sort-Object Author, TaskName |
                Select-Object -First 40 |
                Format-List
            """,
            FixRiskLevel.Safe,
            "scheduled tasks", "background jobs", "startup triggers");

        yield return Silent(
            "clear-delivery-optimization-cache-only",
            "Clear Delivery Optimization Cache",
            "Clears only the Delivery Optimization cache so update leftovers can be removed without resetting all update components.",
            true,
            45,
            """
            Stop-Service DoSvc -Force -ErrorAction SilentlyContinue
            $path = Join-Path $env:ProgramData 'Microsoft\Windows\DeliveryOptimization\Cache'
            $freed = 0
            if (Test-Path $path) {
                $freed = (Get-ChildItem $path -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
                Remove-Item "$path\*" -Recurse -Force -ErrorAction SilentlyContinue
            }
            Start-Service DoSvc -ErrorAction SilentlyContinue
            Write-Output "Delivery Optimization cache cleared. Freed $([math]::Round(($freed / 1MB), 1)) MB."
            """,
            FixRiskLevel.NeedsAdmin,
            "delivery optimization", "update cache");

        yield return Silent(
            "clear-directx-shader-cache-only",
            "Clear DirectX Shader Cache",
            "Clears the DirectX shader cache to fix shader corruption and reclaim space safely.",
            false,
            30,
            """
            $path = Join-Path $env:LOCALAPPDATA 'D3DSCache'
            $freed = 0
            if (Test-Path $path) {
                $freed = (Get-ChildItem $path -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
                Remove-Item "$path\*" -Recurse -Force -ErrorAction SilentlyContinue
            }
            Write-Output "DirectX shader cache cleared. Freed $([math]::Round(($freed / 1MB), 1)) MB."
            """,
            FixRiskLevel.Safe,
            "shader cache", "directx cache", "game stutter");

        yield return Silent(
            "clear-error-report-cache",
            "Clear Error Report Cache",
            "Clears Windows Error Reporting cache and temp dumps to reclaim space after repeated crashes safely.",
            true,
            40,
            """
            $paths = @(
                Join-Path $env:ProgramData 'Microsoft\Windows\WER',
                Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\WER'
            )
            $freed = 0
            foreach ($path in $paths) {
                if (Test-Path $path) {
                    $freed += (Get-ChildItem $path -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
                    Remove-Item "$path\*" -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            Write-Output "Error report cache cleared. Freed $([math]::Round(($freed / 1MB), 1)) MB."
            """,
            FixRiskLevel.NeedsAdmin,
            "wer cache", "dump cleanup", "crash cache");

        yield return Silent(
            "review-storage-sense-status",
            "Review Storage Sense Status",
            "Shows current Storage Sense settings and opens the right page if cleanup automation is disabled.",
            false,
            20,
            """
            $path = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy'
            $state = if (Test-Path $path) { (Get-ItemProperty -Path $path -Name '01' -ErrorAction SilentlyContinue).'01' } else { $null }
            Write-Output "Storage Sense enabled: $([bool]($state -eq 1))"
            Start-Process ms-settings:storagesense
            """,
            FixRiskLevel.Safe,
            "storage sense", "cleanup automation", "disk cleanup settings");

        yield return Silent(
            "show-largest-temp-and-downloads-folders",
            "Show Largest Temp And Downloads Folders",
            "Lists the largest folders under temp locations and Downloads so users can target safe cleanup first.",
            false,
            60,
            """
            $roots = @($env:TEMP, (Join-Path $env:LOCALAPPDATA 'Temp'), (Join-Path $env:USERPROFILE 'Downloads'))
            foreach ($root in $roots | Where-Object { Test-Path $_ }) {
                Write-Output "Root: $root"
                Get-ChildItem $root -Directory -Force -ErrorAction SilentlyContinue |
                    ForEach-Object {
                        $size = (Get-ChildItem $_.FullName -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
                        [pscustomobject]@{ Name = $_.FullName; SizeMB = [math]::Round(($size / 1MB), 1) }
                    } |
                    Sort-Object SizeMB -Descending |
                    Select-Object -First 10 |
                    Format-Table -AutoSize
                Write-Output ""
            }
            """,
            FixRiskLevel.Safe,
            "large temp folders", "downloads space", "disk cleanup");

        yield return Silent(
            "show-ram-heavy-background-apps",
            "Show RAM-Heavy Background Apps",
            "Lists the background apps using the most private memory so slow systems can be triaged safely.",
            false,
            25,
            """
            Get-Process -ErrorAction SilentlyContinue |
                Sort-Object PM -Descending |
                Select-Object -First 20 ProcessName, Id,
                    @{Name='PrivateMB';Expression={[math]::Round($_.PM / 1MB, 1)}},
                    @{Name='WorkingSetMB';Expression={[math]::Round($_.WS / 1MB, 1)}} |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "memory heavy apps", "ram usage", "background apps");

        yield return Silent(
            "rebuild-icon-cache-only",
            "Rebuild Icon Cache",
            "Rebuilds the Windows icon cache without clearing thumbnails so missing or wrong app icons can recover safely.",
            false,
            35,
            """
            Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
            Remove-Item "$env:LOCALAPPDATA\IconCache.db" -Force -ErrorAction SilentlyContinue
            Remove-Item "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\iconcache*" -Force -ErrorAction SilentlyContinue
            Start-Process explorer.exe
            Write-Output "Icon cache rebuilt."
            """,
            FixRiskLevel.Safe,
            "icon cache", "wrong icons", "missing app icons");

        yield return Silent(
            "rebuild-thumbnail-cache-only",
            "Rebuild Thumbnail Cache",
            "Clears the Windows thumbnail cache without touching the icon cache so bad previews can recover safely.",
            false,
            35,
            """
            Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
            Remove-Item "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\thumbcache*" -Force -ErrorAction SilentlyContinue
            Start-Process explorer.exe
            Write-Output "Thumbnail cache rebuilt."
            """,
            FixRiskLevel.Safe,
            "thumbnail cache", "bad previews", "wrong thumbnails");

        yield return Silent(
            "restart-shell-components-individually",
            "Restart Shell Components Individually",
            "Restarts Explorer, Start menu, and Search host processes to recover shell responsiveness without rebooting.",
            false,
            35,
            """
            $processes = 'explorer', 'StartMenuExperienceHost', 'SearchHost', 'SearchApp'
            foreach ($process in $processes) {
                Get-Process $process -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
            }
            Start-Process explorer.exe
            Write-Output "Explorer, Start menu, and Search host were restarted."
            """,
            FixRiskLevel.Safe,
            "restart explorer", "search host", "start menu");

        yield return Guided(
            "clean-boot-isolation-walkthrough",
            "Run Clean Boot Isolation Walkthrough",
            "Guides users through a safe clean-boot test with real service and startup checks at each step.",
            true,
            240,
            FixRiskLevel.Advanced,
            ["clean boot", "startup conflict", "safe boot troubleshooting"],
            Step(
                "clean-boot-service-check",
                "Check Non-Microsoft Services",
                "Review the current non-Microsoft service count before changing startup behavior.",
                """
                Get-CimInstance Win32_Service -ErrorAction SilentlyContinue |
                    Where-Object { $_.State -eq 'Running' -and $_.PathName -notmatch 'Microsoft' } |
                    Measure-Object | ForEach-Object { "Running non-Microsoft services: $($_.Count)" }
                """
            ),
            Step(
                "clean-boot-open-msconfig",
                "Open System Configuration",
                "Open msconfig so you can hide Microsoft services and disable the rest temporarily.",
                """
                Start-Process msconfig.exe
                """
            ),
            Step(
                "clean-boot-startup-review",
                "Review Startup Impact",
                "Open the Startup Apps page so you can disable non-essential startup entries for the isolation test.",
                """
                Start-Process ms-settings:startupapps
                """
            )
        );

        yield return Silent(
            "detect-services-stuck-transitioning",
            "Detect Services Stuck Starting Or Stopping",
            "Lists services that appear stuck in a pending state so startup and shutdown hangs can be triaged safely.",
            true,
            25,
            """
            Get-CimInstance Win32_Service -ErrorAction SilentlyContinue |
                Where-Object { $_.State -match 'Pending|Start Pending|Stop Pending' } |
                Select-Object Name, DisplayName, State, StartMode |
                Format-Table -AutoSize
            """,
            FixRiskLevel.NeedsAdmin,
            "service stuck starting", "service stuck stopping", "pending service");

        yield return Silent(
            "generate-boot-diagnostics-snapshot",
            "Generate Boot Diagnostics Snapshot",
            "Exports recent boot, shutdown, and resume events so slow or failed startup sessions can be reviewed safely.",
            false,
            45,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Boot-Diagnostics.txt'
            Get-WinEvent -FilterHashtable @{ LogName = 'System'; Id = 12, 13, 27, 41, 1074, 6005, 6006 } -MaxEvents 80 |
                Select-Object TimeCreated, Id, ProviderName, LevelDisplayName, Message |
                Format-List | Out-File $output -Encoding UTF8
            Write-Output "Boot diagnostics saved to $output"
            """,
            FixRiskLevel.Safe,
            "boot events", "slow startup", "shutdown diagnostics");

        yield return Silent(
            "show-recent-app-hangs",
            "Show Recent App Hang Events",
            "Lists recent app hang events by executable so freezing or not-responding apps can be reviewed safely.",
            false,
            35,
            """
            Get-WinEvent -FilterHashtable @{ LogName = 'Application'; ProviderName = 'Application Hang' } -MaxEvents 25 |
                Select-Object TimeCreated, Id,
                    @{Name='Executable';Expression={
                        if ($_.Message -match 'The program ([^ ]+)') { $matches[1] } else { 'Unknown' }
                    }},
                    Message |
                Format-List
            """,
            FixRiskLevel.Safe,
            "app hang", "not responding", "frozen app");

        yield return Silent(
            "detect-low-pagefile-configuration",
            "Detect Low Page File Configuration",
            "Shows current paging-file settings so memory-pressure and crash-dump issues can be reviewed safely.",
            false,
            25,
            """
            Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue | Select-Object AutomaticManagedPagefile | Format-List
            Get-CimInstance Win32_PageFileSetting -ErrorAction SilentlyContinue | Select-Object Name, InitialSize, MaximumSize | Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "page file", "paging file", "low virtual memory");

        yield return Silent(
            "detect-slow-shell-extensions",
            "Detect Slow Shell Extensions",
            "Lists non-Microsoft shell extension handlers so slow right-click and Explorer shell issues can be reviewed safely.",
            false,
            35,
            """
            Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved' -ErrorAction SilentlyContinue |
                Get-ItemProperty -ErrorAction SilentlyContinue |
                Select-Object * | Format-List
            """,
            FixRiskLevel.Safe,
            "shell extensions", "slow explorer", "slow right click");

        yield return Silent(
            "show-shell-context-menu-handlers",
            "Show Shell Context Menu Handlers",
            "Lists common shell context-menu handler registrations so Explorer extension conflicts can be reviewed safely.",
            false,
            35,
            """
            Get-ChildItem 'Registry::HKEY_CLASSES_ROOT\*\shellex\ContextMenuHandlers' -ErrorAction SilentlyContinue |
                Select-Object Name, PSChildName |
                Format-Table -AutoSize
            Get-ChildItem 'Registry::HKEY_CLASSES_ROOT\Directory\shellex\ContextMenuHandlers' -ErrorAction SilentlyContinue |
                Select-Object Name, PSChildName |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "context menu handlers", "explorer right click", "shell handlers");
    }
    private static IEnumerable<FixItem> AudioDisplayBacklogFixes()
    {
        yield return Silent(
            "show-playback-devices",
            "Show Playback Devices",
            "Lists playback audio endpoints and default-device state so speaker and headset routing can be reviewed safely.",
            false,
            25,
            """
            Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue |
                Where-Object FriendlyName -match 'Speaker|Headphone|HDMI|Display Audio|Output' |
                Select-Object FriendlyName, Status, InstanceId |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "playback devices", "speakers", "headphones");

        yield return Silent(
            "show-recording-devices",
            "Show Recording Devices",
            "Lists recording audio endpoints so microphone selection and device state can be reviewed safely.",
            false,
            25,
            """
            Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue |
                Where-Object FriendlyName -match 'Microphone|Mic|Input|Headset' |
                Select-Object FriendlyName, Status, InstanceId |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "recording devices", "microphone list", "mic input");

        yield return Guided(
            "repair-app-microphone-permission",
            "Repair App Microphone Permission",
            "Guides users through microphone privacy settings and app-level checks before they retry recording safely.",
            false,
            180,
            FixRiskLevel.Safe,
            ["microphone permission", "app cannot use mic", "mic blocked"],
            Step("microphone-privacy-open", "Open Microphone Privacy", "Open Windows microphone privacy settings and review desktop-app access.", """
                Start-Process ms-settings:privacy-microphone
                """),
            Step("microphone-device-list", "List Recording Devices", "Check that Windows still sees the intended microphone before retrying the app.", """
                Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue |
                    Where-Object FriendlyName -match 'Microphone|Mic|Input|Headset' |
                    Select-Object FriendlyName, Status |
                    Format-Table -AutoSize
                """),
            Step("microphone-app-check", "Open Sound Input Settings", "Open input-device settings and verify the correct microphone is selected.", """
                Start-Process ms-settings:sound
                """)
        );

        yield return Silent(
            "reset-spatial-audio-settings",
            "Reset Spatial Audio Settings",
            "Turns off Windows spatial-audio settings for the current user so playback distortion can be reviewed safely.",
            false,
            20,
            """
            Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Multimedia\Audio' -Name UserDuckingPreference -ErrorAction SilentlyContinue
            Start-Process mmsys.cpl
            Write-Output "Sound settings were opened so you can verify the active spatial-audio option."
            """,
            FixRiskLevel.Safe,
            "spatial audio", "windows sonic", "audio distortion");

        yield return Silent(
            "detect-exclusive-mode-conflicts",
            "Detect Exclusive Mode Conflicts",
            "Shows playback devices and audio services so exclusive-mode playback conflicts can be reviewed safely.",
            false,
            30,
            """
            Get-Service Audiosrv, AudioEndpointBuilder -ErrorAction SilentlyContinue |
                Select-Object Name, Status, StartType | Format-Table -AutoSize
            Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue |
                Where-Object FriendlyName -match 'Speaker|Headphone|HDMI|Display Audio|Output' |
                Select-Object FriendlyName, Status | Format-Table -AutoSize
            Write-Output "If a single app holds the device exclusively, close media, meeting, and game apps before retrying audio."
            """,
            FixRiskLevel.Safe,
            "exclusive mode", "audio conflict", "speaker busy");

        yield return Silent(
            "detect-mismatched-default-communication-device",
            "Detect Default Communication Device Mismatch",
            "Lists audio endpoints so a mismatched communication device versus playback device can be reviewed safely.",
            false,
            25,
            """
            Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue |
                Select-Object FriendlyName, Status, Class, InstanceId |
                Format-Table -AutoSize
            Start-Process mmsys.cpl
            Write-Output "Review the Playback and Recording tabs for the green default-device and default-communication-device badges."
            """,
            FixRiskLevel.Safe,
            "communication device", "default playback mismatch", "teams wrong speaker");

        yield return Silent(
            "export-audio-endpoint-inventory",
            "Export Audio Endpoint Inventory",
            "Exports playback and recording endpoint inventory to a desktop report for safe support review.",
            false,
            30,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Audio-Endpoints.txt'
            Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue |
                Select-Object FriendlyName, Status, InstanceId |
                Format-Table -AutoSize | Out-File $output -Encoding UTF8
            Write-Output "Audio endpoint report saved to $output"
            """,
            FixRiskLevel.Safe,
            "audio inventory", "endpoint inventory", "support audio report");

        yield return Silent(
            "restart-bluetooth-audio-services",
            "Restart Bluetooth Audio Services",
            "Restarts Bluetooth support services so headset audio routing can recover safely.",
            true,
            25,
            """
            Restart-Service bthserv -Force -ErrorAction SilentlyContinue
            Restart-Service AudioEndpointBuilder -Force -ErrorAction SilentlyContinue
            Get-Service bthserv, AudioEndpointBuilder -ErrorAction SilentlyContinue | Select-Object Name, Status | Format-Table -AutoSize
            """,
            FixRiskLevel.NeedsAdmin,
            "bluetooth audio", "headset audio", "restart audio services");

        yield return Silent(
            "show-monitor-display-state",
            "Show Monitor Display State",
            "Lists current monitor resolution, scale, refresh, and HDR-related details so display configuration can be reviewed safely.",
            false,
            35,
            """
            Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue |
                Select-Object Name, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate, DriverVersion |
                Format-Table -AutoSize
            Get-ItemProperty 'HKCU:\Control Panel\Desktop' -ErrorAction SilentlyContinue |
                Select-Object LogPixels, Win8DpiScaling | Format-List
            """,
            FixRiskLevel.Safe,
            "resolution", "refresh rate", "display scale");

        yield return Silent(
            "restart-graphics-stack-guidance",
            "Restart Graphics Stack Guidance",
            "Checks graphics driver state and reminds the user of the built-in graphics stack reset hotkey for safe display recovery.",
            false,
            20,
            """
            Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue |
                Select-Object Name, DriverVersion, DriverDate, Status |
                Format-Table -AutoSize
            Write-Output "Press Win+Ctrl+Shift+B to ask Windows to reset the graphics stack if the screen is frozen but still powered."
            """,
            FixRiskLevel.Safe,
            "graphics reset", "black screen", "display driver reset");

        yield return Silent(
            "detect-mixed-refresh-monitors",
            "Detect Mixed Refresh Monitors",
            "Lists current display refresh rates so mixed refresh-rate setups that cause flicker or stutter can be reviewed safely.",
            false,
            20,
            """
            Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue |
                Select-Object Name, CurrentRefreshRate, CurrentHorizontalResolution, CurrentVerticalResolution |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "mixed refresh rate", "monitor stutter", "refresh mismatch");

        yield return Guided(
            "set-per-app-gpu-preference",
            "Set Per-App GPU Preference",
            "Guides users through reviewing GPU preference options for a specific app before they retest graphics behavior.",
            false,
            180,
            FixRiskLevel.Safe,
            ["gpu preference", "graphics settings", "app uses wrong gpu"],
            Step("gpu-controller-check", "Review Graphics Adapters", "Review the graphics adapters Windows currently sees before changing per-app preference.", """
                Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue |
                    Select-Object Name, DriverVersion, VideoProcessor |
                    Format-Table -AutoSize
                """),
            Step("gpu-settings-open", "Open Graphics Settings", "Open the Windows Graphics settings page where per-app GPU preferences are managed.", """
                Start-Process ms-settings:display-advancedgraphics
                """),
            Step("gpu-app-retest", "Retest The App", "After changing the app preference, relaunch the app and check whether graphics behavior improved.", """
                Write-Output "Relaunch the affected app after saving the preference, then compare performance or display stability."
                """)
        );

        yield return Silent(
            "enumerate-monitor-edid-names",
            "Enumerate Monitor EDID Names",
            "Lists monitor EDID names and active display identifiers so the real connected panels can be reviewed safely.",
            false,
            30,
            """
            Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorID -ErrorAction SilentlyContinue |
                ForEach-Object {
                    [pscustomobject]@{
                        Manufacturer = ([System.Text.Encoding]::ASCII.GetString($_.ManufacturerName) -replace "`0","").Trim()
                        ProductCode  = ([System.Text.Encoding]::ASCII.GetString($_.ProductCodeID) -replace "`0","").Trim()
                        Serial       = ([System.Text.Encoding]::ASCII.GetString($_.SerialNumberID) -replace "`0","").Trim()
                    }
                } | Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "edid", "monitor name", "display output");

        yield return Silent(
            "detect-hybrid-gpu-mode-indicators",
            "Detect Hybrid GPU Mode Indicators",
            "Lists graphics adapters and hardware details so hybrid GPU mode clues can be reviewed safely.",
            false,
            25,
            """
            Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue |
                Select-Object Name, AdapterCompatibility, DriverVersion, VideoProcessor |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "hybrid gpu", "switchable graphics", "laptop gpu");

        yield return Silent(
            "export-display-driver-versions",
            "Export Display Driver Versions",
            "Exports graphics driver versions and dates to a desktop report for safe support review.",
            false,
            35,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Display-Drivers.txt'
            Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue |
                Select-Object Name, DriverVersion, DriverDate, AdapterCompatibility |
                Format-Table -AutoSize | Out-File $output -Encoding UTF8
            Write-Output "Display driver report saved to $output"
            """,
            FixRiskLevel.Safe,
            "display driver", "gpu driver version", "graphics report");
    }
    private static IEnumerable<FixItem> UpdateBacklogFixes()
    {
        yield return Silent(
            "show-failed-kbs-last-30-days",
            "Show Failed Update KBs",
            "Lists recent failed Windows Update KB installs so update recovery can start from the real failing package.",
            false,
            35,
            """
            Get-WinEvent -FilterHashtable @{ LogName = 'Setup'; StartTime = (Get-Date).AddDays(-30) } -ErrorAction SilentlyContinue |
                Where-Object { $_.Message -match 'KB\d+' } |
                Select-Object TimeCreated, Id, Message -First 40 |
                Format-List
            """,
            FixRiskLevel.Safe,
            "failed kb", "update failure", "windows update kb");

        yield return Silent(
            "reset-delivery-optimization-service-only",
            "Reset Delivery Optimization Service",
            "Restarts only the Delivery Optimization service so download transfer issues can recover without a full update reset.",
            true,
            20,
            """
            Restart-Service DoSvc -Force -ErrorAction SilentlyContinue
            Get-Service DoSvc | Select-Object Name, Status, StartType | Format-Table -AutoSize
            """,
            FixRiskLevel.NeedsAdmin,
            "delivery optimization service", "dosvc", "update download issue");

        yield return Silent(
            "trigger-windows-update-scan",
            "Trigger Windows Update Scan",
            "Starts a new Windows Update scan without clearing caches so users can check for updates again safely.",
            false,
            20,
            """
            UsoClient StartScan
            Start-Process ms-settings:windowsupdate
            Write-Output "Windows Update scan requested."
            """,
            FixRiskLevel.Safe,
            "update scan", "check for updates", "usoclient");

        yield return Silent(
            "detect-pending-reboot-markers",
            "Detect Pending Reboot Markers",
            "Checks the common registry markers that show Windows still needs a reboot before updates can finish.",
            false,
            20,
            """
            $paths = @(
                'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending',
                'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
            )
            foreach ($path in $paths) {
                Write-Output "$path : $(Test-Path $path)"
            }
            $session = Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -ErrorAction SilentlyContinue
            Write-Output "PendingFileRenameOperations present: $([bool]$session.PendingFileRenameOperations)"
            """,
            FixRiskLevel.Safe,
            "pending reboot", "reboot required", "update reboot");

        yield return Guided(
            "rollback-problem-quality-update",
            "Roll Back Problem Quality Update",
            "Guides users through uninstalling a recent quality update with real update-history and recovery checks.",
            true,
            180,
            FixRiskLevel.Advanced,
            ["rollback quality update", "bad kb", "recent update broke pc"],
            Step("quality-update-history-check", "Check Recent Update History", "Review the recent quality updates before removing one.", """
                Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 15 HotFixID, Description, InstalledOn | Format-Table -AutoSize
                """),
            Step("quality-update-open", "Open Installed Update Removal", "Open the installed update control panel page used to remove a recent quality update.", """
                Start-Process 'control.exe' '/name Microsoft.WindowsUpdate /page pageInstalledUpdates'
                """),
            Step("quality-update-reboot-check", "Check Pending Reboot State", "After removal, check whether Windows now needs a restart to finish rollback.", """
                Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
                """)
        );

        yield return Guided(
            "rollback-problem-feature-update",
            "Roll Back Problem Feature Update",
            "Guides eligible systems through the Windows recovery path used to go back from a recent feature update.",
            true,
            180,
            FixRiskLevel.Advanced,
            ["rollback feature update", "go back windows version", "feature update broke pc"],
            Step("feature-update-recovery-check", "Check Recovery Eligibility", "Review whether the system is still within the rollback period for the current feature update.", """
                Get-ItemProperty -Path 'HKLM:\SYSTEM\Setup' -ErrorAction SilentlyContinue | Select-Object CmdLine, OOBEInProgress, SetupType
                """),
            Step("feature-update-open-recovery", "Open Recovery Page", "Open Windows Recovery where the Go Back option is exposed if it is still available.", """
                Start-Process ms-settings:recovery
                """),
            Step("feature-update-windows-old-check", "Check Windows.old Availability", "Check whether Windows.old still exists for rollback support.", """
                Test-Path 'C:\Windows.old'
                """)
        );

        yield return Silent(
            "check-and-enable-winre",
            "Check And Enable Windows Recovery Environment",
            "Checks Windows Recovery Environment status and enables it if the recovery tools are currently disabled.",
            true,
            35,
            """
            $before = reagentc /info 2>&1
            Write-Output $before
            if ($before -match 'Disabled') {
                reagentc /enable | Write-Output
                Write-Output ""
                reagentc /info | Write-Output
            }
            """,
            FixRiskLevel.NeedsAdmin,
            "winre", "recovery environment", "startup repair");
    }

    private static IEnumerable<FixItem> StorageBacklogFixes()
    {
        yield return Silent(
            "detect-onedrive-hydration-issues",
            "Detect OneDrive Hydration Issues",
            "Checks for OneDrive placeholders and sync attributes so Files On-Demand hydration problems can be reviewed safely.",
            false,
            25,
            """
            Get-ChildItem (Join-Path $env:USERPROFILE 'OneDrive') -Recurse -ErrorAction SilentlyContinue |
                Select-Object -First 40 FullName, Attributes, Length |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "onedrive hydration", "files on demand", "cloud file missing");

        yield return Silent(
            "detect-controlled-folder-access-blocks",
            "Detect Controlled Folder Access Blocks",
            "Reads Defender event data for Controlled Folder Access blocks so failed saves can be triaged safely.",
            false,
            30,
            """
            Get-WinEvent -LogName 'Microsoft-Windows-Windows Defender/Operational' -ErrorAction SilentlyContinue |
                Where-Object Id -in 1123, 1124 |
                Select-Object -First 20 TimeCreated, Id, Message |
                Format-List
            """,
            FixRiskLevel.Safe,
            "controlled folder access", "save blocked", "defender block");

        yield return Silent(
            "show-smart-health-summary",
            "Show SMART Health Summary",
            "Lists physical disk health and media type so early disk problems can be reviewed safely.",
            false,
            25,
            """
            Get-PhysicalDisk -ErrorAction SilentlyContinue |
                Select-Object FriendlyName, MediaType, HealthStatus, OperationalStatus, Size |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "smart health", "disk health", "physical disk");

        yield return Silent(
            "detect-trim-and-optimize-status",
            "Detect TRIM And Optimize Status",
            "Checks TRIM support and opens the optimize-drives tool so storage maintenance state can be reviewed safely.",
            false,
            20,
            """
            fsutil behavior query DisableDeleteNotify | Write-Output
            Start-Process dfrgui.exe
            Write-Output "Review the Optimize Drives window for the last run status of each volume."
            """,
            FixRiskLevel.Safe,
            "trim status", "optimize drive", "ssd trim");

        yield return Silent(
            "export-large-folder-report",
            "Export Large Folder Report",
            "Exports the 50 largest folders under the user profile so space pressure can be reviewed safely.",
            false,
            70,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Large-Folders.txt'
            Get-ChildItem $env:USERPROFILE -Directory -Force -ErrorAction SilentlyContinue |
                ForEach-Object {
                    $size = (Get-ChildItem $_.FullName -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
                    [pscustomobject]@{ Folder = $_.FullName; SizeGB = [math]::Round(($size / 1GB), 2) }
                } |
                Sort-Object SizeGB -Descending |
                Select-Object -First 50 |
                Format-Table -AutoSize | Out-File $output -Encoding UTF8
            Write-Output "Large folder report saved to $output"
            """,
            FixRiskLevel.Safe,
            "large folder report", "disk usage report", "storage report");
    }

    private static IEnumerable<FixItem> SecurityBacklogFixes()
    {
        yield return Silent(
            "show-firewall-profiles-summary",
            "Show Firewall Profiles Summary",
            "Lists Windows Firewall profile state so public, private, and domain firewall status can be reviewed safely.",
            false,
            20,
            """
            Get-NetFirewallProfile -ErrorAction SilentlyContinue |
                Select-Object Name, Enabled, DefaultInboundAction, DefaultOutboundAction |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "firewall profiles", "windows firewall", "profile state");

        yield return Silent(
            "detect-smartscreen-disabled",
            "Detect SmartScreen Disabled State",
            "Checks current SmartScreen policy state so weakened download protection can be reviewed safely.",
            false,
            20,
            """
            $appHost = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer' -Name SmartScreenEnabled -ErrorAction SilentlyContinue
            Write-Output "Explorer SmartScreen: $($appHost.SmartScreenEnabled)"
            Start-Process windowsdefender:
            """,
            FixRiskLevel.Safe,
            "smartscreen", "download protection", "security warning");

        yield return Silent(
            "detect-defender-tamper-protection-state",
            "Detect Defender Tamper Protection State",
            "Checks Defender tamper-protection state so users can review whether malware-protection settings are too easy to change.",
            false,
            20,
            """
            Get-MpComputerStatus -ErrorAction SilentlyContinue |
                Select-Object IsTamperProtected, AntivirusEnabled, RealTimeProtectionEnabled |
                Format-List
            """,
            FixRiskLevel.Safe,
            "tamper protection", "defender protection", "security settings");

        yield return Silent(
            "list-local-admin-accounts",
            "List Local Admin Accounts",
            "Lists local administrator accounts and their last-logon details so account exposure can be reviewed safely.",
            false,
            30,
            """
            Get-LocalGroupMember -Group 'Administrators' -ErrorAction SilentlyContinue |
                Select-Object Name, ObjectClass, PrincipalSource |
                Format-Table -AutoSize
            Get-LocalUser -ErrorAction SilentlyContinue | Select-Object Name, Enabled, LastLogon | Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "local admin", "administrator accounts", "last logon");

        yield return Silent(
            "check-bitlocker-status-all-volumes",
            "Check BitLocker Status",
            "Lists BitLocker protection state for all volumes so encryption coverage can be reviewed safely.",
            false,
            25,
            """
            manage-bde -status
            """,
            FixRiskLevel.Safe,
            "bitlocker", "drive encryption", "recovery key");

        yield return Guided(
            "review-app-permissions-privacy",
            "Review App Privacy Permissions",
            "Guides users through camera, microphone, and location permission review with real settings launches.",
            false,
            180,
            FixRiskLevel.Safe,
            ["app permissions", "camera privacy", "microphone privacy", "location privacy"],
            Step("privacy-camera-open", "Open Camera Privacy", "Open camera privacy settings and review which apps are allowed to use the camera.", """
                Start-Process ms-settings:privacy-webcam
                """),
            Step("privacy-microphone-open", "Open Microphone Privacy", "Open microphone privacy settings and review which apps are allowed to use the microphone.", """
                Start-Process ms-settings:privacy-microphone
                """),
            Step("privacy-location-open", "Open Location Privacy", "Open location privacy settings and review which apps still have location access.", """
                Start-Process ms-settings:privacy-location
                """)
        );

        yield return Silent(
            "export-defender-threat-history-report",
            "Export Defender Threat History",
            "Exports recent Microsoft Defender detections to a desktop text report for safe review and escalation.",
            false,
            30,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Defender-Threat-History.txt'
            Get-MpThreatDetection -ErrorAction SilentlyContinue |
                Select-Object InitialDetectionTime, ThreatName, ActionSuccess, Resources |
                Format-List | Out-File $output -Encoding UTF8
            Write-Output "Defender threat history saved to $output"
            """,
            FixRiskLevel.Safe,
            "defender history", "threat history", "malware report");
    }

    private static IEnumerable<FixItem> CrashBacklogFixes()
    {
        yield return Silent(
            "export-minidump-inventory",
            "Export Minidump Inventory",
            "Exports the current minidump inventory with timestamps and file sizes so crash evidence can be reviewed safely.",
            false,
            25,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Minidump-Inventory.txt'
            Get-ChildItem 'C:\Windows\Minidump' -ErrorAction SilentlyContinue |
                Select-Object Name, Length, LastWriteTime |
                Format-Table -AutoSize | Out-File $output -Encoding UTF8
            Write-Output "Minidump inventory saved to $output"
            """,
            FixRiskLevel.Safe,
            "minidump inventory", "bsod dumps", "crash files");

        yield return Silent(
            "show-recent-bugcheck-codes",
            "Show Recent Bugcheck Codes",
            "Lists recent bugcheck events with stop-code details so recent blue screens can be reviewed safely.",
            false,
            30,
            """
            Get-WinEvent -FilterHashtable @{ LogName = 'System'; Id = 1001 } -MaxEvents 20 -ErrorAction SilentlyContinue |
                Select-Object TimeCreated, Id, ProviderName, Message |
                Format-List
            """,
            FixRiskLevel.Safe,
            "bugcheck", "blue screen codes", "stop code");

        yield return Silent(
            "show-recent-third-party-drivers",
            "Show Recent Third-Party Drivers",
            "Lists recently installed third-party drivers so crash investigations can focus on recent changes safely.",
            false,
            35,
            """
            Get-CimInstance Win32_PnPSignedDriver -ErrorAction SilentlyContinue |
                Where-Object { $_.DriverProviderName -and $_.DriverProviderName -notmatch 'Microsoft' } |
                Sort-Object DriverDate -Descending |
                Select-Object -First 30 DeviceName, DriverProviderName, DriverVersion, DriverDate |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "third party drivers", "recent drivers", "crash drivers");

        yield return Guided(
            "safe-mode-crash-triage",
            "Run Safe Mode Crash Triage",
            "Guides users through Safe Mode crash triage with driver and corruption checks before making changes.",
            true,
            180,
            FixRiskLevel.Advanced,
            ["safe mode crash", "bsod triage", "driver rollback"],
            Step("safe-mode-boot-option-open", "Open Advanced Startup", "Open the Recovery page so you can restart into Safe Mode.", """
                Start-Process ms-settings:recovery
                """),
            Step("safe-mode-driver-check", "Check Recent Drivers", "Review recently installed third-party drivers before uninstalling or rolling one back.", """
                Get-CimInstance Win32_PnPSignedDriver -ErrorAction SilentlyContinue |
                    Where-Object { $_.DriverProviderName -and $_.DriverProviderName -notmatch 'Microsoft' } |
                    Sort-Object DriverDate -Descending |
                    Select-Object -First 20 DeviceName, DriverProviderName, DriverVersion, DriverDate |
                    Format-Table -AutoSize
                """),
            Step("safe-mode-system-check", "Check System File Integrity", "Run a quick system integrity check from the current session before deeper rollback work.", """
                sfc /verifyonly
                """)
        );

        yield return Silent(
            "check-sfc-dism-storage-corruption-signals",
            "Check SFC And DISM Corruption Signals",
            "Reviews CBS and DISM logs for storage-corruption patterns that often appear after repeated crashes.",
            false,
            35,
            """
            $cbs = Join-Path $env:WINDIR 'Logs\CBS\CBS.log'
            $dism = Join-Path $env:WINDIR 'Logs\DISM\dism.log'
            if (Test-Path $cbs) {
                Select-String -Path $cbs -Pattern 'corrupt', 'cannot repair', 'storage' -SimpleMatch -ErrorAction SilentlyContinue |
                    Select-Object -First 20 | ForEach-Object { $_.Line }
            }
            if (Test-Path $dism) {
                Select-String -Path $dism -Pattern 'corrupt', 'repairable', 'source files could not be found' -SimpleMatch -ErrorAction SilentlyContinue |
                    Select-Object -First 20 | ForEach-Object { $_.Line }
            }
            """,
            FixRiskLevel.Safe,
            "cbs corruption", "dism corruption", "storage corruption");
    }
    private static IEnumerable<FixItem> DeviceBacklogFixes()
    {
        yield return Silent(
            "detect-usb-power-saving-hubs",
            "Detect USB Power-Saving Hubs",
            "Lists USB hubs and suspend-related power settings so disconnect issues can be reviewed safely.",
            false,
            30,
            """
            Get-PnpDevice -Class USB -ErrorAction SilentlyContinue |
                Select-Object FriendlyName, Status, Class, InstanceId |
                Format-Table -AutoSize
            powercfg /devicequery wake_armed
            """,
            FixRiskLevel.Safe,
            "usb hub", "power saving usb", "disconnecting usb");

        yield return Silent(
            "show-recent-usb-reconnect-events",
            "Show Recent USB Reconnect Events",
            "Lists recent USB disconnect and reconnect events so unstable ports or cables can be reviewed safely.",
            false,
            30,
            """
            Get-WinEvent -LogName System -ErrorAction SilentlyContinue |
                Where-Object { $_.ProviderName -match 'USB|Kernel-PnP' } |
                Select-Object -First 30 TimeCreated, Id, ProviderName, Message |
                Format-List
            """,
            FixRiskLevel.Safe,
            "usb reconnect", "usb disconnect", "kernel pnp");

        yield return Silent(
            "detect-missing-hid-and-ghost-devices",
            "Detect Missing HID And Ghost Devices",
            "Lists HID and present/hidden device state so ghost devices and missing inputs can be reviewed safely.",
            false,
            35,
            """
            Get-PnpDevice -Class HIDClass -PresentOnly:$false -ErrorAction SilentlyContinue |
                Select-Object FriendlyName, Status, Present, InstanceId |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "hid device", "ghost device", "missing keyboard");

        yield return Silent(
            "restart-bluetooth-services-and-radio",
            "Restart Bluetooth Services",
            "Restarts the main Bluetooth services so radio-stack issues can recover safely.",
            true,
            25,
            """
            Restart-Service bthserv -Force -ErrorAction SilentlyContinue
            Get-Service bthserv -ErrorAction SilentlyContinue | Select-Object Name, Status, StartType | Format-Table -AutoSize
            """,
            FixRiskLevel.NeedsAdmin,
            "bluetooth service", "restart bluetooth", "radio stack");

        yield return Silent(
            "enumerate-webcams-and-privacy-state",
            "Enumerate Webcams And Privacy State",
            "Lists webcam devices and opens camera privacy settings so device visibility and camera access can be reviewed safely.",
            false,
            25,
            """
            Get-PnpDevice -Class Camera -ErrorAction SilentlyContinue |
                Select-Object FriendlyName, Status, InstanceId |
                Format-Table -AutoSize
            Start-Process ms-settings:privacy-webcam
            """,
            FixRiskLevel.Safe,
            "webcam", "camera privacy", "camera not found");

        yield return Guided(
            "bluetooth-pairing-reset-flow",
            "Run Bluetooth Pairing Reset Flow",
            "Guides users through Bluetooth radio, pairing, and settings checks before they re-pair the device safely.",
            true,
            210,
            FixRiskLevel.Safe,
            ["bluetooth pairing", "mouse not pairing", "headset not pairing"],
            Step("bluetooth-service-check", "Check Bluetooth Service", "Check whether the Bluetooth support service is running before re-pairing.", """
                Get-Service bthserv -ErrorAction SilentlyContinue | Select-Object Name, Status, StartType | Format-Table -AutoSize
                """),
            Step("bluetooth-settings-open", "Open Bluetooth Settings", "Open the Windows Bluetooth settings page used to remove and re-add the device.", """
                Start-Process ms-settings:bluetooth
                """),
            Step("bluetooth-device-inventory", "List Present Bluetooth Devices", "Review the current Bluetooth device inventory before pairing again.", """
                Get-PnpDevice -Class Bluetooth -PresentOnly:$false -ErrorAction SilentlyContinue |
                    Select-Object FriendlyName, Status, Present |
                    Format-Table -AutoSize
                """)
        );

        yield return Silent(
            "export-device-problem-codes",
            "Export Device Problem Codes",
            "Exports device problem codes for present Plug and Play devices so support can review hardware faults safely.",
            false,
            35,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Device-Problems.txt'
            Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
                Where-Object { $_.Status -ne 'OK' } |
                Select-Object Class, FriendlyName, Status, Problem, InstanceId |
                Format-Table -AutoSize | Out-File $output -Encoding UTF8
            Write-Output "Device problem report saved to $output"
            """,
            FixRiskLevel.Safe,
            "device problem code", "pnp problems", "hardware errors");
    }

    private static IEnumerable<FixItem> PrinterBacklogFixes()
    {
        yield return Silent(
            "restart-spooler-and-clear-stuck-jobs",
            "Restart Spooler And Clear Stuck Jobs",
            "Restarts the print spooler and removes stuck queue files so blocked print jobs can recover safely.",
            true,
            35,
            """
            Stop-Service Spooler -Force -ErrorAction SilentlyContinue
            Remove-Item "$env:WINDIR\System32\spool\PRINTERS\*" -Force -ErrorAction SilentlyContinue
            Start-Service Spooler -ErrorAction SilentlyContinue
            Get-Service Spooler | Select-Object Name, Status, StartType | Format-Table -AutoSize
            """,
            FixRiskLevel.NeedsAdmin,
            "spooler", "stuck print jobs", "clear printer queue");

        yield return Silent(
            "export-printers-drivers-and-ports",
            "Export Printers Drivers And Ports",
            "Exports installed printers, drivers, and port mappings to a desktop report for safe support review.",
            false,
            35,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Printers-And-Ports.txt'
            Get-Printer -ErrorAction SilentlyContinue | Select-Object Name, DriverName, PortName, PrinterStatus |
                Format-Table -AutoSize | Out-File $output -Encoding UTF8
            Add-Content -Path $output -Value ''
            Get-PrinterPort -ErrorAction SilentlyContinue | Select-Object Name, PrinterHostAddress, PortMonitor |
                Format-Table -AutoSize | Out-File $output -Encoding UTF8 -Append
            Write-Output "Printer report saved to $output"
            """,
            FixRiskLevel.Safe,
            "printer ports", "printer driver", "printer inventory");

        yield return Silent(
            "detect-wsd-printer-ports",
            "Detect WSD Printer Ports",
            "Lists printers using WSD ports so users can review whether a TCP/IP port would be more stable.",
            false,
            25,
            """
            Get-Printer -ErrorAction SilentlyContinue |
                Where-Object PortName -match '^WSD' |
                Select-Object Name, PortName, DriverName |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "wsd printer", "tcp ip port", "printer offline");

        yield return Guided(
            "set-default-printer-guidance",
            "Set Default Printer Guidance",
            "Guides users through choosing a default printer and turning off Windows default-printer management safely.",
            false,
            180,
            FixRiskLevel.Safe,
            ["default printer", "windows manage default printer", "wrong printer selected"],
            Step("printer-list-check", "List Installed Printers", "Review installed printers before choosing the default one.", """
                Get-Printer -ErrorAction SilentlyContinue | Select-Object Name, Default, PrinterStatus | Format-Table -AutoSize
                """),
            Step("printer-settings-open", "Open Printers Settings", "Open Printers & scanners where the default printer setting can be changed.", """
                Start-Process ms-settings:printers
                """),
            Step("printer-policy-disable", "Disable Automatic Default Printer", "Turn off the Windows option that changes the default printer automatically.", """
                Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\Windows' -Name LegacyDefaultPrinterMode -Value 1 -Type DWord -ErrorAction SilentlyContinue
                Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\Windows' -Name LegacyDefaultPrinterMode -ErrorAction SilentlyContinue
                """)
        );

        yield return Silent(
            "detect-offline-printer-port-connectivity",
            "Detect Offline Printer Port Connectivity",
            "Tests network printer ports and lists offline printer state so connectivity problems can be reviewed safely.",
            false,
            35,
            """
            $printers = Get-Printer -ErrorAction SilentlyContinue
            foreach ($printer in $printers) {
                $port = Get-PrinterPort -Name $printer.PortName -ErrorAction SilentlyContinue
                if ($port.PrinterHostAddress) {
                    $test = Test-NetConnection -ComputerName $port.PrinterHostAddress -Port 9100 -WarningAction SilentlyContinue
                    Write-Output "$($printer.Name): TCP 9100 reachable = $($test.TcpTestSucceeded)"
                } else {
                    Write-Output "$($printer.Name): Port = $($printer.PortName)"
                }
            }
            """,
            FixRiskLevel.Safe,
            "offline printer", "printer port", "port connectivity");

        yield return Guided(
            "network-scanner-reconnect-flow",
            "Run Network Scanner Reconnect Flow",
            "Guides users through WIA, network reachability, and scan-target checks before they reconnect a scanner safely.",
            false,
            210,
            FixRiskLevel.Safe,
            ["scanner reconnect", "scan to folder", "wia service"],
            Step("scanner-device-check", "List Scanner Devices", "Review installed scanner devices before reconnecting the scanner target.", """
                Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object Class -match 'Image|Camera' |
                    Select-Object FriendlyName, Status, Class | Format-Table -AutoSize
                """),
            Step("scanner-service-check", "Check WIA Service", "Check whether the Windows Image Acquisition service is running.", """
                Get-Service stisvc -ErrorAction SilentlyContinue | Select-Object Name, Status, StartType | Format-Table -AutoSize
                """),
            Step("scanner-network-check", "Open Scan Targets", "Open Credential Manager to review saved SMB or email-scan target credentials.", """
                Start-Process control.exe '/name Microsoft.CredentialManager'
                """)
        );
    }
    private static IEnumerable<FixItem> AppRepairBacklogFixes()
    {
        yield return Silent(
            "detect-missing-vc-runtimes",
            "Detect Missing VC++ Runtimes",
            "Lists installed Visual C++ runtimes so missing redistributables can be reviewed safely before reinstalling an app.",
            false,
            35,
            """
            Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*, HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* -ErrorAction SilentlyContinue |
                Where-Object DisplayName -match 'Visual C\+\+.*Redistributable' |
                Select-Object DisplayName, DisplayVersion, Publisher |
                Sort-Object DisplayName |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "visual c++", "vc runtime", "missing runtime");

        yield return Silent(
            "detect-missing-directx-legacy-runtime",
            "Detect Missing DirectX Legacy Runtime",
            "Checks common legacy DirectX files so older game and app dependency gaps can be reviewed safely.",
            false,
            25,
            """
            $paths = @(
                "$env:WINDIR\System32\d3dx9_43.dll",
                "$env:WINDIR\SysWOW64\d3dx9_43.dll",
                "$env:WINDIR\System32\xinput1_3.dll"
            )
            foreach ($path in $paths) {
                Write-Output "$path : $(Test-Path $path)"
            }
            """,
            FixRiskLevel.Safe,
            "directx runtime", "legacy directx", "d3dx");

        yield return Silent(
            "detect-missing-dotnet-desktop-runtime",
            "Detect Missing .NET Desktop Runtime",
            "Lists installed .NET runtimes so missing desktop runtime versions can be reviewed safely.",
            false,
            25,
            """
            dotnet --list-runtimes 2>$null
            """,
            FixRiskLevel.Safe,
            ".net runtime", "desktop runtime", "app missing dotnet");

        yield return Silent(
            "export-app-uninstall-inventory",
            "Export App Uninstall Inventory",
            "Exports installed app inventory with publisher, version, and install date for safe support review.",
            false,
            35,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-App-Inventory.txt'
            Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*, HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* -ErrorAction SilentlyContinue |
                Where-Object DisplayName |
                Select-Object DisplayName, Publisher, DisplayVersion, InstallDate |
                Sort-Object DisplayName |
                Format-Table -AutoSize | Out-File $output -Encoding UTF8
            Write-Output "App inventory saved to $output"
            """,
            FixRiskLevel.Safe,
            "installed apps", "app inventory", "uninstall list");

        yield return Guided(
            "per-app-reset-or-repair-launcher",
            "Run Per-App Reset Or Repair Launcher",
            "Guides users to the correct repair or reset path for classic apps and Store apps using real Windows settings.",
            false,
            180,
            FixRiskLevel.Safe,
            ["repair app", "reset app", "store app repair"],
            Step("app-inventory-check", "Review Installed Apps", "Review installed apps before choosing the repair or reset target.", """
                Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*, HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* -ErrorAction SilentlyContinue |
                    Where-Object DisplayName |
                    Select-Object -First 25 DisplayName, Publisher, DisplayVersion |
                    Sort-Object DisplayName | Format-Table -AutoSize
                """),
            Step("app-settings-open", "Open Installed Apps", "Open the Installed apps page where classic and Store apps expose repair or reset options.", """
                Start-Process ms-settings:appsfeatures
                """),
            Step("store-apps-open", "Open Default Apps", "Open default apps if the app still launches the wrong component or file type.", """
                Start-Process ms-settings:defaultapps
                """)
        );

        yield return Silent(
            "repair-windows-installer-service-health",
            "Repair Windows Installer Service Health",
            "Re-registers the Windows Installer service and verifies that it can start safely.",
            true,
            35,
            """
            msiexec /unregister
            msiexec /regserver
            Start-Service msiserver -ErrorAction SilentlyContinue
            Get-Service msiserver | Select-Object Name, Status, StartType | Format-Table -AutoSize
            """,
            FixRiskLevel.NeedsAdmin,
            "windows installer", "msiexec", "installer service");

        yield return Silent(
            "detect-broken-common-file-associations",
            "Detect Broken Common File Associations",
            "Lists current handlers for common document and browser file types so broken associations can be reviewed safely.",
            false,
            25,
            """
            foreach ($ext in '.pdf', '.jpg', '.png', '.html', '.htm') {
                $progId = (Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$ext\UserChoice" -ErrorAction SilentlyContinue).ProgId
                Write-Output "$ext => $progId"
            }
            Write-Output "mailto => $((Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\mailto\UserChoice' -ErrorAction SilentlyContinue).ProgId)"
            """,
            FixRiskLevel.Safe,
            "file association", "pdf default", "mailto broken");

        yield return Silent(
            "reset-common-file-associations-defaults",
            "Reset Common File Associations To Defaults",
            "Opens the Windows default-apps experience and resets common browser and document associations safely.",
            false,
            25,
            """
            Start-Process ms-settings:defaultapps
            Write-Output "Use the Reset button under Recommended defaults or choose the specific app defaults you want to restore."
            """,
            FixRiskLevel.Safe,
            "reset default apps", "default app associations", "wrong app opens file");
    }
    private static IEnumerable<FixItem> OfficeBacklogFixes()
    {
        yield return Silent(
            "clear-outlook-temp-and-autocomplete-backup",
            "Clear Outlook Temp And Backup Autocomplete",
            "Backs up Outlook autocomplete data and clears temp attachment caches so Outlook profile clutter can be reviewed safely.",
            false,
            45,
            """
            $roam = Join-Path $env:LOCALAPPDATA 'Microsoft\Outlook\RoamCache'
            $backup = Join-Path $env:USERPROFILE 'Desktop\FixFox-Outlook-Autocomplete-Backup'
            if (Test-Path $roam) {
                New-Item -ItemType Directory -Path $backup -Force | Out-Null
                Get-ChildItem $roam -Filter 'Stream_Autocomplete*.dat' -ErrorAction SilentlyContinue | Copy-Item -Destination $backup -Force
                Get-ChildItem $roam -Filter '*.tmp' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
            }
            Write-Output "Outlook autocomplete backup path: $backup"
            """,
            FixRiskLevel.Safe,
            "outlook cache", "autocomplete cache", "outlook temp");

        yield return Guided(
            "outlook-profile-repair-path",
            "Run Outlook Profile Repair Path",
            "Guides users through the classic and new Outlook paths so they can choose the correct repair workflow safely.",
            false,
            180,
            FixRiskLevel.Safe,
            ["outlook profile", "new outlook", "classic outlook"],
            Step("outlook-install-check", "Check Outlook Install Paths", "Review common Outlook install paths before choosing the repair path.", """
                Get-ChildItem 'C:\Program Files', 'C:\Program Files (x86)' -Filter OUTLOOK.EXE -Recurse -ErrorAction SilentlyContinue |
                    Select-Object -First 10 FullName, LastWriteTime | Format-Table -AutoSize
                """),
            Step("mail-profile-open", "Open Mail Profiles", "Open the Mail control panel to review or create Outlook profiles.", """
                Start-Process control.exe '/name Microsoft.Mail'
                """),
            Step("outlook-apps-open", "Open Installed Apps", "Open Installed apps if Outlook needs repair from the Windows app-management path.", """
                Start-Process ms-settings:appsfeatures
                """)
        );

        yield return Silent(
            "detect-onedrive-version-and-status",
            "Detect OneDrive Version And Status",
            "Lists OneDrive version and process state so sync-client health can be reviewed safely.",
            false,
            25,
            """
            Get-Process OneDrive -ErrorAction SilentlyContinue | Select-Object ProcessName, Id, StartTime | Format-Table -AutoSize
            Get-Item "$env:LOCALAPPDATA\Microsoft\OneDrive\OneDrive.exe" -ErrorAction SilentlyContinue |
                Select-Object FullName, VersionInfo | Format-List
            """,
            FixRiskLevel.Safe,
            "onedrive version", "onedrive status", "sync client");

        yield return Silent(
            "restart-onedrive-and-health-check",
            "Restart OneDrive And Health Check",
            "Restarts OneDrive and opens the sync app again so stuck sync state can recover safely.",
            false,
            35,
            """
            Stop-Process -Name OneDrive -Force -ErrorAction SilentlyContinue
            $exe = "$env:LOCALAPPDATA\Microsoft\OneDrive\OneDrive.exe"
            if (Test-Path $exe) {
                Start-Process $exe
                Write-Output "OneDrive restarted."
            } else {
                Write-Output "OneDrive executable was not found in the current user profile."
                exit 1
            }
            """,
            FixRiskLevel.Safe,
            "restart onedrive", "onedrive stuck", "sync health");

        yield return Silent(
            "detect-teams-variant-conflicts",
            "Detect Teams Variant Conflicts",
            "Lists classic and new Teams installs so conflicting startup or install variants can be reviewed safely.",
            false,
            30,
            """
            Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*, HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*, HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* -ErrorAction SilentlyContinue |
                Where-Object DisplayName -match 'Teams' |
                Select-Object DisplayName, DisplayVersion, Publisher, InstallLocation |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "teams classic", "new teams", "teams conflict");

        yield return Guided(
            "m365-desktop-app-reauth",
            "Run Microsoft 365 Desktop App Re-Auth",
            "Guides users through Microsoft 365 sign-in checks and token refresh paths before they re-open Office apps safely.",
            false,
            180,
            FixRiskLevel.Safe,
            ["office sign in", "microsoft 365 reauth", "office activation"],
            Step("office-credential-check", "Open Work Or School Accounts", "Open the Windows work and school account settings page and confirm the intended account is connected.", """
                Start-Process ms-settings:workplace
                """),
            Step("credential-manager-open", "Review Stored Credentials", "Open Credential Manager to review outdated Office or Microsoft 365 saved credentials.", """
                Start-Process control.exe '/name Microsoft.CredentialManager'
                """),
            Step("office-relaunch-guidance", "Relaunch Office Apps", "Close all Office apps, then relaunch the affected app and sign in again with the intended account.", """
                Write-Output "Close Outlook, Teams, Word, and Excel before reopening the affected app and signing in again."
                """)
        );

        yield return Silent(
            "detect-ost-pst-size-risk",
            "Detect OST And PST Size Risk",
            "Lists OST and PST file locations and sizes so mailbox-data disk pressure can be reviewed safely.",
            false,
            35,
            """
            Get-ChildItem $env:USERPROFILE -Include *.ost, *.pst -Recurse -ErrorAction SilentlyContinue |
                Select-Object FullName, @{Name='SizeGB';Expression={[math]::Round($_.Length / 1GB, 2)}}, LastWriteTime |
                Sort-Object SizeGB -Descending |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "ost size", "pst size", "outlook mailbox disk");

        yield return Silent(
            "export-office-click-to-run-channel",
            "Export Office Click-To-Run Channel",
            "Exports Office update channel and version details to a desktop report for safe support review.",
            false,
            25,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Office-Channel.txt'
            Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration' -ErrorAction SilentlyContinue |
                Select-Object CDNBaseUrl, VersionToReport, UpdateChannel, ClientVersionToReport |
                Format-List | Out-File $output -Encoding UTF8
            Write-Output "Office channel report saved to $output"
            """,
            FixRiskLevel.Safe,
            "office click to run", "office channel", "office version");

        yield return Guided(
            "shared-mailbox-cached-mode-checklist",
            "Run Shared Mailbox Cached Mode Checklist",
            "Guides users through cached-mode and shared-mailbox review steps before Outlook mailbox performance changes are made.",
            false,
            180,
            FixRiskLevel.Safe,
            ["shared mailbox", "cached mode", "outlook slow mailbox"],
            Step("outlook-account-settings", "Open Outlook Account Settings", "Open Mail profiles first so shared mailbox and cached-mode settings can be reviewed.", """
                Start-Process control.exe '/name Microsoft.Mail'
                """),
            Step("mailbox-file-size-check", "Check OST And PST Size", "Review OST and PST size before reducing cached mailbox scope.", """
                Get-ChildItem $env:USERPROFILE -Include *.ost, *.pst -Recurse -ErrorAction SilentlyContinue |
                    Select-Object FullName, @{Name='SizeGB';Expression={[math]::Round($_.Length / 1GB, 2)}} |
                    Sort-Object SizeGB -Descending | Format-Table -AutoSize
                """),
            Step("disk-space-check", "Check Disk Pressure", "Check free space before changing cache behavior or moving mailbox files.", """
                Get-PSDrive -PSProvider FileSystem | Select-Object Name, @{Name='FreeGB';Expression={[math]::Round($_.Free / 1GB, 2)}}, @{Name='UsedGB';Expression={[math]::Round($_.Used / 1GB, 2)}} | Format-Table -AutoSize
                """)
        );
    }
    private static IEnumerable<FixItem> RemoteBacklogFixes()
    {
        yield return Silent(
            "show-active-vpn-routes-and-dns",
            "Show Active VPN Routes And DNS",
            "Lists VPN adapters, routes, and DNS servers so remote-work routing can be reviewed safely.",
            false,
            35,
            """
            Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object InterfaceDescription -match 'VPN|WAN Miniport|TAP|AnyConnect|Juniper|GlobalProtect|Forti' |
                Select-Object Name, Status, InterfaceDescription | Format-Table -AutoSize
            Get-NetRoute -AddressFamily IPv4 -ErrorAction SilentlyContinue | Sort-Object RouteMetric | Select-Object -First 25 DestinationPrefix, NextHop, InterfaceAlias, RouteMetric | Format-Table -AutoSize
            Get-DnsClientServerAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "vpn routes", "vpn dns", "remote work routes");

        yield return Silent(
            "detect-split-tunnel-route-leaks",
            "Detect Split-Tunnel Route Leaks",
            "Lists remaining VPN-style routes so route leaks after disconnect can be reviewed safely.",
            false,
            30,
            """
            Get-NetRoute -AddressFamily IPv4 -ErrorAction SilentlyContinue |
                Where-Object InterfaceAlias -match 'VPN|TAP|AnyConnect|Juniper|GlobalProtect|Forti' |
                Select-Object DestinationPrefix, NextHop, InterfaceAlias, RouteMetric |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "split tunnel", "route leak", "vpn disconnect");

        yield return Silent(
            "restart-vpn-core-services",
            "Restart VPN Core Services",
            "Restarts RasMan and IKEEXT so built-in VPN connectivity can recover safely.",
            true,
            25,
            """
            Restart-Service RasMan -Force -ErrorAction SilentlyContinue
            Restart-Service IKEEXT -Force -ErrorAction SilentlyContinue
            Get-Service RasMan, IKEEXT | Select-Object Name, Status, StartType | Format-Table -AutoSize
            """,
            FixRiskLevel.NeedsAdmin,
            "rasman", "ikeext", "vpn service");

        yield return Guided(
            "recreate-windows-vpn-profile-flow",
            "Recreate Windows VPN Profile",
            "Guides users through reviewing and recreating a built-in Windows VPN profile using real Windows settings.",
            false,
            180,
            FixRiskLevel.Safe,
            ["vpn profile", "windows vpn", "recreate vpn"],
            Step("vpn-profile-list", "List Existing VPN Profiles", "Review existing VPN profiles before you delete or recreate one.", """
                Get-VpnConnection -AllUserConnection -ErrorAction SilentlyContinue | Select-Object Name, ServerAddress, TunnelType, ConnectionStatus | Format-Table -AutoSize
                Get-VpnConnection -ErrorAction SilentlyContinue | Select-Object Name, ServerAddress, TunnelType, ConnectionStatus | Format-Table -AutoSize
                """),
            Step("vpn-settings-open", "Open VPN Settings", "Open the Windows VPN settings page used to remove and recreate a built-in VPN profile.", """
                Start-Process ms-settings:network-vpn
                """),
            Step("vpn-route-review", "Review Routes After Reconnect", "After reconnecting, review VPN routes and DNS settings again.", """
                Get-NetRoute -AddressFamily IPv4 -ErrorAction SilentlyContinue | Sort-Object RouteMetric | Select-Object -First 20 DestinationPrefix, NextHop, InterfaceAlias, RouteMetric | Format-Table -AutoSize
                """)
        );

        yield return Silent(
            "export-rdp-settings-and-history",
            "Export RDP Settings And History",
            "Exports saved RDP targets and current RDP policy state to a desktop report for safe review.",
            false,
            35,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Rdp-Details.txt'
            Get-ItemProperty 'HKCU:\Software\Microsoft\Terminal Server Client\Default' -ErrorAction SilentlyContinue |
                Format-List | Out-File $output -Encoding UTF8
            Add-Content $output ''
            Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server' -ErrorAction SilentlyContinue |
                Select-Object fDenyTSConnections | Format-List | Out-File $output -Append -Encoding UTF8
            Write-Output "RDP report saved to $output"
            """,
            FixRiskLevel.Safe,
            "rdp history", "saved rdp target", "remote desktop");

        yield return Silent(
            "detect-nla-and-rdp-firewall-mismatch",
            "Detect NLA And RDP Firewall Mismatch",
            "Checks Remote Desktop policy, NLA, and firewall state so connection blockers can be reviewed safely.",
            false,
            30,
            """
            Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server' -ErrorAction SilentlyContinue |
                Select-Object fDenyTSConnections | Format-List
            Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp' -ErrorAction SilentlyContinue |
                Select-Object UserAuthentication, SecurityLayer | Format-List
            Get-NetFirewallRule -DisplayGroup 'Remote Desktop' -ErrorAction SilentlyContinue |
                Select-Object DisplayName, Enabled, Profile, Direction | Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "nla", "rdp firewall", "remote desktop blocked");

        yield return Guided(
            "corporate-vpn-troubleshooting-checklist",
            "Run Corporate VPN Troubleshooting Checklist",
            "Guides users through DNS, MFA, and captive-portal checks before escalating a corporate VPN issue.",
            false,
            210,
            FixRiskLevel.Safe,
            ["corporate vpn", "vpn dns", "mfa captive portal"],
            Step("vpn-adapter-check", "Check VPN Adapters", "Review active and disconnected VPN adapters before reconnecting.", """
                Get-NetAdapter -ErrorAction SilentlyContinue |
                    Where-Object InterfaceDescription -match 'VPN|TAP|AnyConnect|Juniper|GlobalProtect|Forti' |
                    Select-Object Name, Status, InterfaceDescription | Format-Table -AutoSize
                """),
            Step("dns-check", "Check DNS Resolution", "Test whether internal and public DNS resolution are both working.", """
                Resolve-DnsName microsoft.com -ErrorAction SilentlyContinue | Select-Object -First 3 Name, IPAddress
                """),
            Step("captive-portal-check", "Open Browser For Captive Portal Check", "Open a browser test page in case the connection is waiting on MFA or captive-portal completion.", """
                Start-Process 'https://www.msftconnecttest.com/redirect'
                """)
        );
    }
    private static IEnumerable<FixItem> PowerBacklogFixes()
    {
        yield return Silent(
            "export-powercfg-energy-report",
            "Export Powercfg Energy Report",
            "Generates the built-in Windows energy report and opens it automatically for safe power troubleshooting.",
            true,
            70,
            """
            $report = Join-Path $env:USERPROFILE 'Desktop\FixFox-Energy-Report.html'
            powercfg /energy /output $report /duration 30 | Out-Null
            Start-Process $report
            Write-Output "Energy report saved to $report"
            """,
            FixRiskLevel.NeedsAdmin,
            "powercfg energy", "battery report", "sleep problems");

        yield return Silent(
            "detect-modern-standby-support",
            "Detect Modern Standby Support",
            "Lists supported sleep states so modern standby and classic sleep capability can be reviewed safely.",
            false,
            20,
            """
            powercfg /a
            """,
            FixRiskLevel.Safe,
            "modern standby", "sleep states", "s0 low power idle");

        yield return Silent(
            "show-active-wake-timers-and-source",
            "Show Active Wake Timers And Last Wake Source",
            "Lists wake timers and the last wake source so unexpected wake behavior can be reviewed safely.",
            false,
            20,
            """
            powercfg /waketimers
            powercfg /lastwake
            """,
            FixRiskLevel.Safe,
            "wake timers", "last wake", "woke from sleep");

        yield return Silent(
            "review-devices-allowed-to-wake",
            "Review Devices Allowed To Wake",
            "Lists devices allowed to wake the machine so unexpected keyboard, mouse, or network wakes can be reviewed safely.",
            false,
            20,
            """
            powercfg /devicequery wake_armed
            """,
            FixRiskLevel.Safe,
            "wake armed", "device wakes pc", "mouse wakes pc");

        yield return Silent(
            "show-cpu-power-plan-values",
            "Show CPU Power Plan Values",
            "Displays the active power plan and processor min and max state values so throttling or heat issues can be reviewed safely.",
            false,
            25,
            """
            powercfg /getactivescheme
            powercfg /query SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN
            powercfg /query SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX
            """,
            FixRiskLevel.Safe,
            "cpu power plan", "processor state", "throttling");

        yield return Guided(
            "laptop-overheating-checklist",
            "Run Laptop Overheating Checklist",
            "Guides users through thermal, power, and fan checks before they change performance settings or hardware.",
            false,
            180,
            FixRiskLevel.Safe,
            ["overheating", "hot laptop", "fan noise"],
            Step("power-plan-check", "Check Active Power Plan", "Review the current power plan before changing processor state or performance mode.", """
                powercfg /getactivescheme
                """),
            Step("temperature-hint", "Review Thermal Clues", "Review CPU load and recent thermal hints before physically cleaning vents or changing performance mode.", """
                Get-Process | Sort-Object CPU -Descending | Select-Object -First 10 ProcessName, CPU, WS | Format-Table -AutoSize
                """),
            Step("fan-guidance", "Open Power And Sleep Settings", "Open Windows power settings and lower aggressive performance mode if heat spikes continue.", """
                Start-Process ms-settings:powersleep
                """)
        );
    }
    private static IEnumerable<FixItem> AccountsAndProductBacklogFixes()
    {
        yield return Silent(
            "show-local-users-and-password-age",
            "Show Local Users And Password Age",
            "Lists local users, admin membership, and password age so sign-in issues can be reviewed safely.",
            false,
            30,
            """
            Get-LocalUser -ErrorAction SilentlyContinue |
                Select-Object Name, Enabled, PasswordLastSet, LastLogon |
                Format-Table -AutoSize
            Get-LocalGroupMember -Group 'Administrators' -ErrorAction SilentlyContinue | Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "local users", "password last set", "admins");

        yield return Silent(
            "detect-expired-passwords-and-disabled-accounts",
            "Detect Expired Passwords And Disabled Accounts",
            "Lists disabled or expired local accounts so sign-in blockers can be reviewed safely.",
            false,
            25,
            """
            Get-LocalUser -ErrorAction SilentlyContinue |
                Select-Object Name, Enabled, AccountExpires, PasswordExpires, PasswordRequired |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "expired password", "disabled account", "cannot sign in");

        yield return Guided(
            "microsoft-account-to-local-account-checklist",
            "Run Microsoft Account To Local Account Checklist",
            "Guides users through the account-conversion path using real account settings before they switch sign-in type safely.",
            false,
            180,
            FixRiskLevel.Advanced,
            ["microsoft account", "local account", "switch sign in"],
            Step("account-settings-open", "Open Your Info", "Open the Your info page where account sign-in options are managed.", """
                Start-Process ms-settings:yourinfo
                """),
            Step("backup-guidance", "Review Data Sync Impact", "Review OneDrive and Microsoft Store dependencies before switching to a local account.", """
                Write-Output "Check OneDrive sync, Microsoft Store apps, and BitLocker recovery access before changing account type."
                """),
            Step("local-user-check", "List Existing Local Users", "Check existing local users before creating or switching to a different local account.", """
                Get-LocalUser -ErrorAction SilentlyContinue | Select-Object Name, Enabled | Format-Table -AutoSize
                """)
        );

        yield return Guided(
            "windows-hello-reset-flow",
            "Run Windows Hello Reset Flow",
            "Guides users through PIN, face, and fingerprint reset paths with real sign-in settings checks before retrying Hello safely.",
            false,
            180,
            FixRiskLevel.Safe,
            ["windows hello", "pin reset", "fingerprint reset"],
            Step("signin-options-open", "Open Sign-In Options", "Open Windows sign-in options where PIN, face, and fingerprint settings are managed.", """
                Start-Process ms-settings:signinoptions
                """),
            Step("tpm-status-check", "Check TPM Status", "Review TPM presence because Windows Hello PIN reset may depend on it.", """
                Get-Tpm | Select-Object TpmPresent, TpmReady, ManagedAuthLevel | Format-List
                """),
            Step("credential-guidance", "Review Saved Credentials", "Open Credential Manager if stale work or remote credentials are also blocking sign-in.", """
                Start-Process control.exe '/name Microsoft.CredentialManager'
                """)
        );

        yield return Silent(
            "clear-cached-credentials-with-backup-list",
            "Clear Cached Credentials With Backup List",
            "Exports the current Credential Manager entries and opens Credential Manager so stale sign-in data can be removed safely.",
            false,
            35,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Credential-Backup.txt'
            cmdkey /list | Out-File $output -Encoding UTF8
            Start-Process control.exe '/name Microsoft.CredentialManager'
            Write-Output "Credential list saved to $output"
            """,
            FixRiskLevel.Safe,
            "credential manager", "clear cached credentials", "saved passwords");

        yield return Silent(
            "show-mapped-drives-and-credential-mappings",
            "Show Mapped Drives And Credential Mappings",
            "Lists mapped drives and persistent network connections so stale sign-in mappings can be reviewed safely.",
            false,
            25,
            """
            Get-PSDrive -PSProvider FileSystem | Select-Object Name, Root, Description | Format-Table -AutoSize
            net use
            """,
            FixRiskLevel.Safe,
            "mapped drive", "stale credentials", "network drive");

        yield return Silent(
            "detect-temp-profile-login-state",
            "Detect Temporary Profile Login State",
            "Checks profile-service events and profile keys so temporary-profile sign-in problems can be reviewed safely.",
            false,
            35,
            """
            Get-WinEvent -LogName Application -ErrorAction SilentlyContinue |
                Where-Object ProviderName -eq 'User Profile Service' |
                Select-Object -First 20 TimeCreated, Id, Message |
                Format-List
            Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList' -ErrorAction SilentlyContinue |
                Select-Object PSChildName | Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "temporary profile", "profile service", "temp login");

        yield return Guided(
            "forgot-pin-after-hardware-change",
            "Run Forgot PIN After Hardware Change Flow",
            "Guides users through the Windows Hello and account-recovery checks used after a motherboard or TPM change.",
            false,
            180,
            FixRiskLevel.Safe,
            ["forgot pin", "motherboard change", "hello broken after hardware change"],
            Step("hello-settings-open", "Open Sign-In Options", "Open sign-in options to remove and recreate the affected Windows Hello method.", """
                Start-Process ms-settings:signinoptions
                """),
            Step("tpm-readiness-check", "Check TPM Readiness", "Check TPM readiness because hardware changes often reset Windows Hello trust.", """
                Get-Tpm | Select-Object TpmPresent, TpmReady, RestartPending | Format-List
                """),
            Step("account-recovery-open", "Open Your Info", "Open account settings to confirm the expected Microsoft account is still connected.", """
                Start-Process ms-settings:yourinfo
                """)
        );
    }
    private static IEnumerable<FixItem> GamingBacklogFixes()
    {
        yield return Silent(
            "report-game-bar-game-mode-and-capture",
            "Report Game Bar Game Mode And Capture",
            "Reads Game Bar, Game Mode, and capture settings so gaming overlays and capture behavior can be reviewed safely.",
            false,
            30,
            """
            Get-ItemProperty 'HKCU:\Software\Microsoft\GameBar' -ErrorAction SilentlyContinue | Format-List
            Get-ItemProperty 'HKCU:\Software\Microsoft\GameBar' -Name AutoGameModeEnabled -ErrorAction SilentlyContinue | Format-List
            """,
            FixRiskLevel.Safe,
            "game bar", "game mode", "capture settings");

        yield return Silent(
            "export-gpu-branch-shader-cache-hags",
            "Export GPU Branch Shader Cache And HAGS",
            "Exports graphics driver version, shader-cache clues, and HAGS state for safe gaming support review.",
            false,
            35,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Gaming-GPU-Report.txt'
            Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue |
                Select-Object Name, DriverVersion, DriverDate | Format-Table -AutoSize | Out-File $output -Encoding UTF8
            Add-Content $output ''
            Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' -Name HwSchMode -ErrorAction SilentlyContinue |
                Format-List | Out-File $output -Append -Encoding UTF8
            Write-Output "Gaming GPU report saved to $output"
            """,
            FixRiskLevel.Safe,
            "hags", "gpu branch", "shader cache");

        yield return Silent(
            "detect-common-overlay-processes",
            "Detect Common Overlay Processes",
            "Lists common overlay and capture processes so game conflicts can be reviewed safely.",
            false,
            25,
            """
            Get-Process -ErrorAction SilentlyContinue |
                Where-Object ProcessName -match 'Discord|Steam|NVIDIA|NVContainer|Radeon|AMD|RTSS|MSIAfterburner|obs64|Overwolf' |
                Select-Object ProcessName, Id, CPU, StartTime |
                Sort-Object ProcessName |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "overlay", "discord overlay", "afterburner", "rtss");

        yield return Guided(
            "obs-scene-performance-audit",
            "Run OBS Scene Performance Audit",
            "Guides users through OBS, overlay, and browser-source checks before they change scene complexity or capture paths.",
            false,
            180,
            FixRiskLevel.Safe,
            ["obs performance", "stream lag", "scene audit"],
            Step("obs-process-check", "Check OBS Process State", "Check whether OBS is currently running before changing scene sources or output settings.", """
                Get-Process obs64 -ErrorAction SilentlyContinue | Select-Object ProcessName, Id, CPU, StartTime | Format-Table -AutoSize
                """),
            Step("overlay-check", "Check Overlay Processes", "Review active overlay processes that often compete with OBS capture.", """
                Get-Process -ErrorAction SilentlyContinue |
                    Where-Object ProcessName -match 'Discord|Steam|NVIDIA|RTSS|MSIAfterburner|Overwolf' |
                    Select-Object ProcessName, Id | Format-Table -AutoSize
                """),
            Step("browser-source-guidance", "Review Browser Sources", "If OBS still stutters, reduce browser-source count and retest a local recording before streaming live.", """
                Write-Output "Disable unused browser sources, animated widgets, and duplicate captures before retesting."
                """)
        );

        yield return Silent(
            "show-dpc-latency-driver-hints",
            "Show DPC Latency Driver Hints",
            "Lists recent driver-related system events so DPC latency suspects can be reviewed safely.",
            false,
            30,
            """
            Get-WinEvent -LogName System -ErrorAction SilentlyContinue |
                Where-Object ProviderName -match 'Kernel-PnP|Display|WHEA-Logger' |
                Select-Object -First 30 TimeCreated, ProviderName, Id, Message |
                Format-List
            """,
            FixRiskLevel.Safe,
            "dpc latency", "driver latency", "audio crackle");

        yield return Guided(
            "controller-input-mode-check",
            "Run Controller Input Mode Check",
            "Guides users through Steam Input and native controller checks before they change controller mapping behavior safely.",
            false,
            180,
            FixRiskLevel.Safe,
            ["steam input", "controller not working", "gamepad conflict"],
            Step("controller-device-check", "Check Game Controller Devices", "Review present HID and Xbox controller devices before changing Steam Input settings.", """
                Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object FriendlyName -match 'Controller|Xbox|Gamepad|HID-compliant game controller' |
                    Select-Object FriendlyName, Status, Class | Format-Table -AutoSize
                """),
            Step("steam-process-check", "Check Steam State", "Check whether Steam is running before changing controller configuration.", """
                Get-Process steam -ErrorAction SilentlyContinue | Select-Object ProcessName, Id, StartTime | Format-Table -AutoSize
                """),
            Step("game-retest-guidance", "Retest One Input Path", "After changing Steam Input or native input, retest with only one controller path enabled.", """
                Write-Output "Disable either Steam Input or the game's native controller remapping before retesting."
                """)
        );

        yield return Silent(
            "show-game-drive-throughput-and-free-space",
            "Show Game Drive Throughput And Free Space",
            "Lists physical disk performance and free space so game-drive bottlenecks can be reviewed safely.",
            false,
            30,
            """
            Get-PhysicalDisk -ErrorAction SilentlyContinue |
                Select-Object FriendlyName, MediaType, HealthStatus, Size |
                Format-Table -AutoSize
            Get-Volume -ErrorAction SilentlyContinue |
                Select-Object DriveLetter, FileSystemLabel, @{Name='SizeGB';Expression={[math]::Round($_.Size / 1GB, 2)}}, @{Name='FreeGB';Expression={[math]::Round($_.SizeRemaining / 1GB, 2)}} |
                Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "game drive", "free space", "storage throughput");
    }
    private static IEnumerable<FixItem> WindowsFeatureBacklogFixes()
    {
        yield return Silent(
            "reset-start-menu-pinned-layout-cache",
            "Reset Start Menu Pinned Layout Cache",
            "Clears the current-user Start menu pinned-layout cache so broken or stuck Start tiles can be reviewed safely.",
            false,
            25,
            """
            Remove-Item "$env:LOCALAPPDATA\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\*" -Recurse -Force -ErrorAction SilentlyContinue
            Write-Output "Start menu local state was cleared for the current user. Sign out and back in if the layout stays stuck."
            """,
            FixRiskLevel.Advanced,
            "start menu", "pinned layout", "start tiles broken");

        yield return Silent(
            "reset-taskbar-search-and-widgets-policies",
            "Reset Taskbar Search And Widgets Policies",
            "Removes current-user taskbar policy overrides so default search and widgets behavior can be restored safely.",
            false,
            25,
            """
            Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name SearchboxTaskbarMode -ErrorAction SilentlyContinue
            Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name TaskbarDa -ErrorAction SilentlyContinue
            Write-Output "Taskbar search and widgets policy overrides were cleared for the current user."
            """,
            FixRiskLevel.Safe,
            "taskbar search", "widgets", "taskbar policy");

        yield return Guided(
            "file-explorer-defaults-reset",
            "Run File Explorer Defaults Reset",
            "Guides users through resetting File Explorer visibility and navigation options with real shell settings checks.",
            false,
            180,
            FixRiskLevel.Safe,
            ["explorer defaults", "show file extensions", "hidden files"],
            Step("explorer-options-open", "Open Folder Options", "Open File Explorer options where visibility and navigation defaults are configured.", """
                Start-Process control.exe '/name Microsoft.FileExplorerOptions'
                """),
            Step("explorer-policy-check", "Check Current Explorer Policies", "Review key current-user Explorer options before changing them.", """
                Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -ErrorAction SilentlyContinue |
                    Select-Object Hidden, HideFileExt, ShowSuperHidden, NavPaneShowAllFolders |
                    Format-List
                """),
            Step("explorer-restart-guidance", "Restart Explorer If Needed", "Restart Explorer after changing defaults if File Explorer still shows stale behavior.", """
                Write-Output "If the changes do not appear immediately, restart Explorer or sign out and back in."
                """)
        );

        yield return Silent(
            "detect-broken-default-apps-after-browser-uninstall",
            "Detect Broken Default Apps After Browser Uninstall",
            "Lists current browser-related associations so a broken default browser state can be reviewed safely.",
            false,
            25,
            """
            foreach ($ext in '.htm', '.html', '.pdf') {
                $progId = (Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$ext\UserChoice" -ErrorAction SilentlyContinue).ProgId
                Write-Output "$ext => $progId"
            }
            Write-Output "http => $((Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice' -ErrorAction SilentlyContinue).ProgId)"
            Write-Output "https => $((Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice' -ErrorAction SilentlyContinue).ProgId)"
            """,
            FixRiskLevel.Safe,
            "browser uninstall", "default browser broken", "html association");

        yield return Silent(
            "reset-notification-center-permissions",
            "Reset Notification Center Permissions",
            "Opens notification settings and clears notification policy overrides so app toasts can be reviewed safely.",
            false,
            25,
            """
            Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\PushNotifications' -Name ToastEnabled -ErrorAction SilentlyContinue
            Start-Process ms-settings:notifications
            Write-Output "Notification settings opened. Review per-app toast permissions."
            """,
            FixRiskLevel.Safe,
            "notifications", "toast permissions", "notification center");

        yield return Silent(
            "toggle-clipboard-history-safely",
            "Toggle Clipboard History Safely",
            "Toggles clipboard history for the current user so clipboard syncing issues can be reviewed safely.",
            false,
            20,
            """
            $path = 'HKCU:\Software\Microsoft\Clipboard'
            $current = (Get-ItemProperty -Path $path -Name EnableClipboardHistory -ErrorAction SilentlyContinue).EnableClipboardHistory
            $newValue = if ($current -eq 1) { 0 } else { 1 }
            New-Item -Path $path -Force | Out-Null
            Set-ItemProperty -Path $path -Name EnableClipboardHistory -Value $newValue -Type DWord
            Write-Output "Clipboard history is now set to $newValue."
            """,
            FixRiskLevel.Safe,
            "clipboard history", "cloud clipboard", "clipboard settings");

        yield return Silent(
            "export-environment-variables-and-path-problems",
            "Export Environment Variables And PATH Problems",
            "Exports user and machine environment variables so broken PATH entries can be reviewed safely.",
            false,
            35,
            """
            $output = Join-Path $env:USERPROFILE 'Desktop\FixFox-Environment-Variables.txt'
            Get-ChildItem Env: | Sort-Object Name | Format-Table -AutoSize | Out-File $output -Encoding UTF8
            Add-Content $output ''
            [Environment]::GetEnvironmentVariable('Path','Machine') | Out-File $output -Append -Encoding UTF8
            Add-Content $output ''
            [Environment]::GetEnvironmentVariable('Path','User') | Out-File $output -Append -Encoding UTF8
            Write-Output "Environment variable report saved to $output"
            """,
            FixRiskLevel.Safe,
            "path problems", "environment variables", "broken path");

        yield return Silent(
            "detect-stale-shell-links",
            "Detect Stale Shell Links",
            "Finds stale shortcuts on the Desktop and Start menu so broken shell links can be reviewed safely.",
            false,
            35,
            """
            $shell = New-Object -ComObject WScript.Shell
            Get-ChildItem "$env:PUBLIC\Desktop", "$env:APPDATA\Microsoft\Windows\Start Menu", "$env:USERPROFILE\Desktop" -Filter *.lnk -Recurse -ErrorAction SilentlyContinue |
                ForEach-Object {
                    $target = $shell.CreateShortcut($_.FullName).TargetPath
                    [pscustomobject]@{
                        Shortcut = $_.FullName
                        Target = $target
                        TargetExists = [bool]($target -and (Test-Path $target))
                    }
                } | Where-Object { -not $_.TargetExists } | Format-Table -AutoSize
            """,
            FixRiskLevel.Safe,
            "stale shortcuts", "broken shell links", "dead start menu shortcut");
    }
    private static IEnumerable<FixBundle> BacklogBundles()
    {
        yield return Bundle("home-internet-recovery-bundle", "Home Internet Recovery Bundle", "Runs safe network recovery checks for Wi-Fi, DNS, and routing issues.", 300,
            "export-wifi-diagnostics-report", "measure-packet-loss-targets", "reset-winsock-only", "forget-and-readd-wifi-profile");
        yield return Bundle("browser-recovery-bundle", "Browser Recovery Bundle", "Runs safe browser and DNS recovery checks for proxy, hosts, and cache issues.", 240,
            "test-dns-resolution-speed", "reset-current-user-proxy", "detect-stale-pac-script", "clear-edge-dns-and-sockets");
        yield return Bundle("performance-tuneup-bundle", "Performance Tune-Up Bundle", "Collects safe startup, task, and cleanup signals for a slow-PC review.", 300,
            "list-startup-impact-apps", "clear-delivery-optimization-cache-only", "clear-directx-shader-cache-only", "rebuild-thumbnail-cache-only");
        yield return Bundle("slow-startup-bundle", "Slow Startup Bundle", "Collects startup, hang, and shell-state signals for slow boot investigations.", 300,
            "list-scheduled-tasks-by-trigger", "rebuild-icon-cache-only", "detect-services-stuck-transitioning", "show-recent-app-hangs");
        yield return Bundle("meeting-ready-audio-bundle", "Meeting-Ready Audio Bundle", "Reviews playback, microphone, communication-device, and permission issues safely.", 240,
            "show-playback-devices", "show-recording-devices", "repair-app-microphone-permission", "detect-mismatched-default-communication-device");
        yield return Bundle("black-screen-and-flicker-bundle", "Black Screen And Flicker Bundle", "Collects display state, driver, and refresh clues for monitor issues.", 240,
            "show-monitor-display-state", "restart-graphics-stack-guidance", "detect-mixed-refresh-monitors", "export-display-driver-versions");
        yield return Bundle("update-recovery-bundle", "Update Recovery Bundle", "Collects update failure signals and safe recovery checks before deeper servicing work.", 360,
            "show-failed-kbs-last-30-days", "reset-delivery-optimization-service-only", "trigger-windows-update-scan", "detect-pending-reboot-markers", "check-and-enable-winre");
        yield return Bundle("disk-sanity-bundle", "Disk Sanity Bundle", "Collects disk health, OneDrive, and large-folder data for storage troubleshooting.", 360,
            "detect-onedrive-hydration-issues", "show-smart-health-summary", "detect-trim-and-optimize-status", "export-large-folder-report");
        yield return Bundle("privacy-hardening-bundle", "Privacy Hardening Bundle", "Collects firewall, SmartScreen, Defender, and permission data for privacy review.", 300,
            "show-firewall-profiles-summary", "detect-smartscreen-disabled", "detect-defender-tamper-protection-state", "review-app-permissions-privacy");
        yield return Bundle("post-crash-triage-bundle", "Post-Crash Triage Bundle", "Collects dump, driver, and corruption evidence after blue screens or app crashes.", 360,
            "export-minidump-inventory", "show-recent-bugcheck-codes", "show-recent-third-party-drivers", "check-sfc-dism-storage-corruption-signals");
        yield return Bundle("usb-recovery-bundle", "USB Recovery Bundle", "Collects USB, Bluetooth, webcam, and PnP evidence for peripheral recovery.", 300,
            "detect-usb-power-saving-hubs", "show-recent-usb-reconnect-events", "detect-missing-hid-and-ghost-devices", "export-device-problem-codes");
        yield return Bundle("printer-rescue-bundle", "Printer Rescue Bundle", "Runs safe spooler, port, and default-printer checks for print recovery.", 300,
            "restart-spooler-and-clear-stuck-jobs", "export-printers-drivers-and-ports", "detect-wsd-printer-ports", "detect-offline-printer-port-connectivity");
        yield return Bundle("broken-app-startup-bundle", "Broken App Startup Bundle", "Collects runtime, installer, and file-association clues for broken apps.", 300,
            "detect-missing-vc-runtimes", "detect-missing-directx-legacy-runtime", "detect-missing-dotnet-desktop-runtime", "repair-windows-installer-service-health");
        yield return Bundle("microsoft-365-recovery-bundle", "Microsoft 365 Recovery Bundle", "Runs safe Outlook, OneDrive, Teams, and Office checks for Microsoft 365 issues.", 360,
            "clear-outlook-temp-and-autocomplete-backup", "detect-onedrive-version-and-status", "restart-onedrive-and-health-check", "detect-teams-variant-conflicts", "export-office-click-to-run-channel");
        yield return Bundle("remote-work-recovery-bundle", "Remote Work Recovery Bundle", "Collects VPN, route, RDP, and firewall evidence for work-from-home recovery.", 360,
            "show-active-vpn-routes-and-dns", "detect-split-tunnel-route-leaks", "restart-vpn-core-services", "detect-nla-and-rdp-firewall-mismatch", "export-rdp-settings-and-history");
        yield return Bundle("battery-saver-bundle", "Battery Saver Bundle", "Collects sleep, wake, and power-plan state for laptop battery and wake issues.", 300,
            "export-powercfg-energy-report", "detect-modern-standby-support", "show-active-wake-timers-and-source", "show-cpu-power-plan-values");
        yield return Bundle("signin-recovery-bundle", "Sign-In Recovery Bundle", "Collects account, credential, and profile state for sign-in recovery.", 300,
            "show-local-users-and-password-age", "detect-expired-passwords-and-disabled-accounts", "clear-cached-credentials-with-backup-list", "detect-temp-profile-login-state");
        yield return Bundle("competitive-gaming-bundle", "Competitive Gaming Bundle", "Collects graphics, overlay, controller, and storage clues for gaming issues.", 300,
            "report-game-bar-game-mode-and-capture", "export-gpu-branch-shader-cache-hags", "detect-common-overlay-processes", "show-game-drive-throughput-and-free-space");
        yield return Bundle("windows-cleanup-bundle", "Windows Cleanup Bundle", "Collects Start, taskbar, notifications, clipboard, and shell-link state for Windows usability issues.", 300,
            "reset-start-menu-pinned-layout-cache", "reset-taskbar-search-and-widgets-policies", "reset-notification-center-permissions", "toggle-clipboard-history-safely", "detect-stale-shell-links");
        yield return Bundle("first-run-onboarding-bundle", "First-Run Onboarding Bundle", "Runs a safe first-pass review of connectivity, storage, startup, and account health for a new FixFox user.", 420,
            "test-dns-resolution-speed", "show-largest-temp-and-downloads-folders", "list-startup-impact-apps", "show-local-users-and-password-age", "show-firewall-profiles-summary");
    }
}
