using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Models;
using HelpDesk.Domain.Enums;

namespace HelpDesk.Infrastructure.Fixes;

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
//  MASTER FIX CATALOG  â€” every fix has a stable string ID used by bundles,
//  quick scan links, and history. Zero placeholders. Every script is real.
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
public sealed partial class FixCatalogService : IFixCatalogService
{
    private readonly List<FixCategory>            _cats;
    private readonly List<FixBundle>              _bundles;
    private readonly Dictionary<string, FixItem>  _index;
    private readonly Dictionary<string, string>   _catByFixId;

    public IReadOnlyList<FixCategory> Categories => _cats;
    public IReadOnlyList<FixBundle>   Bundles    => _bundles;

    public FixItem? GetById(string id) =>
        _index.TryGetValue(id, out var fix) ? fix : null;

    public string GetCategoryTitle(FixItem fix) =>
        _catByFixId.TryGetValue(fix.Id, out var t) ? t : "";

    public FixCatalogService()
    {
        _cats    = BuildCategories();
        _bundles = BuildBundles();
        ApplyBacklogExpansions(_cats, _bundles);
        EnsureCatalogMetadata(_cats);
        _index   = _cats.SelectMany(c => c.Fixes).ToDictionary(f => f.Id);
        _catByFixId = _cats
            .SelectMany(c => c.Fixes.Select(f => (f.Id, c.Title)))
            .ToDictionary(x => x.Id, x => x.Title);
    }

    // -------------------------------------------------------------------------
    //  SMART PLAIN-ENGLISH SEARCH
    // -------------------------------------------------------------------------
    private static readonly (string[] From, string[] To)[] Synonyms =
    [
        (new[]{"wifi","wi-fi","wireless","wlan"},                         new[]{"network","wifi","internet","wireless"}),
        (new[]{"internet","online","web","connection","connect"},         new[]{"network","internet","connection"}),
        (new[]{"slow","lagging","lag","sluggish","crawling","hang","frozen","freezing","freeze"},
                                                                          new[]{"performance","slow","freeze","hang"}),
        (new[]{"no sound","silent","mute","audio","volume","speaker","headphones"},
                                                                          new[]{"audio","sound","speaker"}),
        (new[]{"screen","monitor","display","black screen","blank","flickering","blurry"},
                                                                          new[]{"display","screen","monitor"}),
        (new[]{"printer","printing","print","scanner"},                   new[]{"printer","print"}),
        (new[]{"virus","malware","antivirus","defender","infected","hacked"},new[]{"security","virus","defender"}),
        (new[]{"blue screen","bsod","crash","crashed","restart","reboot"},new[]{"crash","bsod","blue screen"}),
        (new[]{"update","updates","windows update","patch"},              new[]{"update","windows"}),
        (new[]{"driver","drivers"},                                        new[]{"driver","update"}),
        (new[]{"startup","boot","start","slow boot","takes forever"},     new[]{"startup","boot","performance"}),
        (new[]{"storage","space","full","disk","hard drive","running out"},new[]{"disk","storage","cleanup"}),
        (new[]{"game","gaming","fps","frames","low fps","game not working"},new[]{"gaming","game","fps"}),
        (new[]{"app","application","program","software","not opening","crashing"},new[]{"app","application","program"}),
        (new[]{"phone","mobile","iphone","android","not recognized"},     new[]{"phone","mobile","usb"}),
        (new[]{"fan","hot","overheating","temperature","heat"},           new[]{"temperature","fan","heat"}),
        (new[]{"battery","charging","not charging","draining"},           new[]{"battery","charging","power"}),
        (new[]{"dns","website","site","can't open","won't load","not loading"},new[]{"dns","network","website"}),
        (new[]{"sleep","wake","hibernate","won't sleep","won't wake"},  new[]{"sleep","power","hibernate"}),
        (new[]{"streaming","obs","twitch","youtube","dropped frames"},    new[]{"streaming","obs","stream"}),
        (new[]{"email","outlook","office","excel","word"},                new[]{"email","office","outlook"}),
        (new[]{"file","folder","missing","deleted","lost"},               new[]{"file","storage","recovery"}),
        (new[]{"bluetooth"},                                               new[]{"bluetooth","wireless"}),
        (new[]{"memory","ram","out of memory"},                           new[]{"memory","ram","performance"}),
    ];

    public IReadOnlyList<FixItem> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var terms = ExpandQuery(query.ToLowerInvariant());
        if (terms.Count == 0) return [];

        return _cats.SelectMany(c => c.Fixes)
            .Select(fix => (fix, score: ScoreFix(fix, terms)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(30)
            .Select(x => x.fix)
            .ToList()
            .AsReadOnly();
    }

    private static HashSet<string> ExpandQuery(string query)
    {
        var words = query
            .Split(new char[]{' ',',','.','!','?'}, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();
        foreach (var (fromPhrases, toTerms) in Synonyms)
            foreach (var phrase in fromPhrases)
                if (query.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    foreach (var t in toTerms) words.Add(t);
        return words;
    }

    private static int ScoreFix(FixItem fix, HashSet<string> terms)
    {
        var score = 0;
        var title = fix.Title.ToLowerInvariant();
        var desc  = fix.Description.ToLowerInvariant();
        foreach (var term in terms)
        {
            if (title.Contains(term))                                                               score += 10;
            if (fix.Keywords.Any(k => k.Contains(term, StringComparison.OrdinalIgnoreCase)))       score += 8;
            if (desc.Contains(term))                                                                score += 4;
            if (fix.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)))           score += 2;
        }
        return score;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    //  CATEGORIES
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static List<FixCategory> BuildCategories() =>
    [
        NetworkAndWifi(),
        PerformanceAndCleanup(),
        AudioAndDisplay(),
        UpdatesAndDrivers(),
        PrintersAndPeripherals(),
        GamingAndStreaming(),
        AppIssues(),
        SecurityAndPrivacy(),
        SystemInformation(),
        BlueScreenAndCrashes(),
        EmailAndOffice(),
        FileAndStorage(),
        PhoneAndMobile(),
        RemoteAndVpn(),
        MaintenanceAndRecovery(),
        SleepAndPower(),
        WindowsFeatures(),
        AdvancedTools(),
        // Part 4 & 5 categories
        DevicesAndUsb(),
        WindowsAppsAndFeatures(),
        WindowsTweaksAndCustomization(),
    ];

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  1. NETWORK & WI-FI
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static FixCategory NetworkAndWifi() => new()
    {
        Id="network", Icon="\uE968", Title="Network & Wi-Fi",
        Fixes=
        [
            new() { Id="flush-dns", Title="Flush DNS cache",
                Description="Clears stale DNS entries causing sites to fail or load the wrong page.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["internet not working", "website not loading", "can't open websites", "dns error", "site won't load", "pages not loading"],
                Script="""
                    ipconfig /flushdns
                    Write-Output "âœ“ DNS cache cleared successfully."
                    """ },

            new() { Id="full-network-reset", Title="Full network stack reset",
                Description="Resets TCP/IP, Winsock, firewall and renews IP. Fixes most 'no internet' issues.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["no internet", "internet stopped working", "lost connection", "wifi not working", "can't connect", "network broken"],
                Script="""
                    Write-Output "Resetting network stack â€” this takes ~10 seconds..."
                    netsh int ip reset 2>&1 | Out-Null
                    netsh winsock reset 2>&1 | Out-Null
                    netsh advfirewall reset 2>&1 | Out-Null
                    ipconfig /release 2>&1 | Out-Null
                    ipconfig /flushdns 2>&1 | Out-Null
                    ipconfig /renew 2>&1 | Out-Null
                    Write-Output "âœ“ Network stack reset. Restart your PC to complete."
                    """ },

            new() { Id="renew-ip", Title="Renew IP address",
                Description="Releases and renews your IP from the router. Fixes 'limited connectivity'.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["limited connectivity", "no ip address", "169 address", "can't connect to router", "wifi connected but no internet"],
                Script="""
                    ipconfig /release 2>&1 | Out-Null
                    Start-Sleep 2
                    ipconfig /renew 2>&1 | Out-Null
                    $ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.PrefixOrigin -ne 'WellKnown'} | Select-Object -First 1).IPAddress
                    Write-Output "âœ“ IP renewed. New address: $ip"
                    """ },

            new() { Id="test-connection", Title="Test internet connection",
                Description="Pings multiple servers to verify your internet is actually working.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["check internet", "is internet working", "test wifi", "internet speed", "ping test"],
                Script="""
                    $targets = @(
                        @{Name="Google DNS";    Host="8.8.8.8"},
                        @{Name="Cloudflare";    Host="1.1.1.1"},
                        @{Name="Google.com";    Host="google.com"},
                        @{Name="Microsoft.com"; Host="microsoft.com"}
                    )
                    foreach ($t in $targets) {
                        $r = Test-Connection $t.Host -Count 2 -Quiet -ErrorAction SilentlyContinue
                        $ms = if($r){ (Test-Connection $t.Host -Count 1 -ErrorAction SilentlyContinue).ResponseTime }else{ "N/A" }
                        Write-Output "$(if($r){'âœ“'}else{'âœ—'}) $($t.Name): $(if($r){"Reachable (${ms}ms)"}else{'UNREACHABLE'})"
                    }
                    """ },

            new() { Id="show-adapter-status", Title="Show network adapter status",
                Description="Displays all active adapters with speed, IP address, and MAC address.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["check network card", "adapter info", "network speed", "what network am i on"],
                Script="""
                    Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | ForEach-Object {
                        $ip = (Get-NetIPAddress -InterfaceIndex $_.InterfaceIndex -AddressFamily IPv4 -EA SilentlyContinue | Select-Object -First 1).IPAddress
                        $speed = if($_.LinkSpeed -gt 0){"$([math]::Round($_.LinkSpeed/1MB)) Mbps"}else{"Unknown"}
                        Write-Output "Adapter : $($_.Name)"
                        Write-Output "  Speed : $speed"
                        Write-Output "  IP    : $ip"
                        Write-Output "  MAC   : $($_.MacAddress)"
                        Write-Output "  Type  : $($_.InterfaceDescription)"
                        Write-Output "---"
                    }
                    """ },

            new() { Id="cycle-adapter", Title="Disable/re-enable network adapter",
                Description="Toggles your adapter â€” the network equivalent of unplugging and replugging.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["restart wifi adapter", "toggle wifi", "network adapter not working", "refresh connection"],
                Script="""
                    $a = Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | Select-Object -First 1
                    if (!$a) { Write-Output "âœ— No active adapter found."; exit 1 }
                    Write-Output "Cycling adapter: $($a.Name)..."
                    Disable-NetAdapter -Name $a.Name -Confirm:$false
                    Start-Sleep 3
                    Enable-NetAdapter -Name $a.Name -Confirm:$false
                    Start-Sleep 4
                    $status = (Get-NetAdapter -Name $a.Name).Status
                    Write-Output "âœ“ Adapter cycled. Status: $status"
                    """ },

            new() { Id="show-wifi-networks", Title="Scan nearby Wi-Fi networks",
                Description="Lists all nearby Wi-Fi networks, their signal strength, and channel.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["see nearby wifi", "wifi channels", "wifi interference", "which channel is best"],
                Script="""
                    Write-Output "Scanning Wi-Fi networks..."
                    $output = netsh wlan show networks mode=bssid
                    $output | Select-String 'SSID|Signal|Channel|Authentication|Band' | ForEach-Object {
                        Write-Output $_.Line.Trim()
                    }
                    """ },

            new() { Id="set-dns-cloudflare", Title="Switch to Cloudflare DNS (faster)",
                Description="Changes your DNS to Cloudflare 1.1.1.1 â€” faster and more private than ISP DNS.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["make internet faster", "slow dns", "cloudflare dns", "1.1.1.1"],
                Script="""
                    $adapters = Get-NetAdapter | Where-Object {$_.Status -eq 'Up'}
                    foreach ($a in $adapters) {
                        Set-DnsClientServerAddress -InterfaceIndex $a.InterfaceIndex -ServerAddresses ("1.1.1.1","1.0.0.1") -EA SilentlyContinue
                    }
                    ipconfig /flushdns | Out-Null
                    Write-Output "âœ“ DNS changed to Cloudflare (1.1.1.1 / 1.0.0.1)"
                    Write-Output "  Note: To revert, open Network adapter settings and set DNS back to 'Automatic'."
                    """ },

            new() { Id="set-dns-google", Title="Switch to Google DNS",
                Description="Changes your DNS to Google 8.8.8.8 â€” very reliable for most users.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["google dns", "8.8.8.8", "faster dns", "change dns"],
                Script="""
                    $adapters = Get-NetAdapter | Where-Object {$_.Status -eq 'Up'}
                    foreach ($a in $adapters) {
                        Set-DnsClientServerAddress -InterfaceIndex $a.InterfaceIndex -ServerAddresses ("8.8.8.8","8.8.4.4") -EA SilentlyContinue
                    }
                    ipconfig /flushdns | Out-Null
                    Write-Output "âœ“ DNS changed to Google (8.8.8.8 / 8.8.4.4)"
                    """ },

            new() { Id="reset-dns-auto", Title="Reset DNS to automatic (ISP default)",
                Description="Reverts DNS back to your ISP's automatic settings.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["reset dns to default", "undo dns change", "revert dns"],
                Script="""
                    $adapters = Get-NetAdapter | Where-Object {$_.Status -eq 'Up'}
                    foreach ($a in $adapters) {
                        Set-DnsClientServerAddress -InterfaceIndex $a.InterfaceIndex -ResetServerAddresses -EA SilentlyContinue
                    }
                    ipconfig /flushdns | Out-Null
                    Write-Output "âœ“ DNS reset to automatic (ISP default)"
                    """ },

            new() { Id="run-network-diag", Title="Run Windows network diagnostics",
                Description="Launches the built-in Windows network troubleshooter wizard.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["troubleshoot internet", "run network troubleshooter", "network help"],
                Script="msdt.exe /id NetworkDiagnosticsNetworkAdapter" },

            new() { Id="measure-bandwidth", Title="Measure network adapter throughput",
                Description="Checks bytes sent/received over 3 seconds to estimate live bandwidth usage.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["test network speed", "how fast is my network", "bandwidth test"],
                Script="""
                    $a = Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | Select-Object -First 1
                    if (!$a) { Write-Output "No active adapter."; exit }
                    $s1 = Get-NetAdapterStatistics -Name $a.Name
                    Start-Sleep 3
                    $s2 = Get-NetAdapterStatistics -Name $a.Name
                    $rx = [math]::Round(($s2.ReceivedBytes - $s1.ReceivedBytes) / 3 / 1KB, 1)
                    $tx = [math]::Round(($s2.SentBytes     - $s1.SentBytes)     / 3 / 1KB, 1)
                    Write-Output "Adapter  : $($a.Name)"
                    Write-Output "Download : $rx KB/s"
                    Write-Output "Upload   : $tx KB/s"
                    """ },

            new() { Id="check-open-ports", Title="Show active network connections",
                Description="Lists established connections â€” useful for spotting suspicious remote access.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["check connections", "suspicious network", "who is connected", "open connections"],
                Script="""
                    Write-Output "=== Active established connections ==="
                    $conns = Get-NetTCPConnection | Where-Object {$_.State -eq 'Established'}
                    foreach ($c in $conns) {
                        try {
                            $proc = Get-Process -Id $c.OwningProcess -EA SilentlyContinue
                            $name = if($proc){$proc.ProcessName}else{"Unknown"}
                            Write-Output "$($c.LocalAddress):$($c.LocalPort) â†’ $($c.RemoteAddress):$($c.RemotePort)  [$name]"
                        } catch {}
                    }
                    """ },

            new() { Id="disable-netbios", Title="Disable NetBIOS (reduces latency)",
                Description="Disables NetBIOS over TCP/IP on all adapters â€” reduces latency for gaming and streaming.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["reduce ping", "lower latency", "gaming network tweak", "disable legacy network"],
                Script="""
                    $adapters = Get-WmiObject Win32_NetworkAdapterConfiguration | Where-Object {$_.IPEnabled -eq $true}
                    foreach ($a in $adapters) {
                        $a.SetTcpipNetbios(2) | Out-Null  # 2 = Disable
                    }
                    Write-Output "âœ“ NetBIOS disabled on all active adapters. Restart to apply."
                    """ },

            new() { Id="forget-rejoin-wifi", Title="Forget & reconnect to Wi-Fi",
                Description="Walks through forgetting a bad Wi-Fi profile and reconnecting fresh.",
                Type=FixType.Guided, Keywords=["rejoin wifi", "reconnect wifi", "wifi password wrong", "wifi not connecting", "forget network"],
                Steps=[
                    new() { Title="Open Wi-Fi settings",  Instruction="Wi-Fi settings will open â€” click 'Manage known networks'.", Script="Start-Process ms-settings:network-wifi" },
                    new() { Title="Forget your network",  Instruction="Find your Wi-Fi network in the list, click it, then click 'Forget'." },
                    new() { Title="Reconnect",            Instruction="Click the Wi-Fi icon in the taskbar, select your network, enter your password, and connect." }
                ]},

            new() { Id="view-hosts-file", Title="Check Windows Hosts file",
                Description="Displays hosts file entries â€” malware sometimes redirects websites here.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["hosts file", "blocked websites", "site redirected", "malware redirecting browser"],
                Script="""
                    Write-Output "=== Windows Hosts file entries (non-comments) ==="
                    $entries = Get-Content 'C:\Windows\System32\drivers\etc\hosts' |
                        Where-Object { $_ -notmatch '^\s*#' -and $_ -match '\S' }
                    if ($entries) { $entries | ForEach-Object { Write-Output $_ } }
                    else { Write-Output "No custom entries found. File looks clean." }
                    """ },
        ]
    };

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  2. PERFORMANCE & CLEANUP
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static FixCategory PerformanceAndCleanup() => new()
    {
        Id="performance", Icon="\uE9D9", Title="Performance & Cleanup",
        Fixes=
        [
            new() { Id="clear-temp-files", Title="Clear all temp files",
                Description="Deletes temporary files from %TEMP%, Windows\\Temp, Prefetch, and Recent.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["free up space", "computer slow", "clean junk files", "disk full", "delete temporary files", "slow computer"],
                Script="""
                    $freed = 0L
                    $paths = @($env:TEMP, "$env:WINDIR\Temp", "$env:WINDIR\Prefetch",
                                "$env:APPDATA\Microsoft\Windows\Recent\AutomaticDestinations",
                                "$env:LOCALAPPDATA\Microsoft\Windows\INetCache")
                    foreach ($p in $paths) {
                        if (Test-Path $p) {
                            $sz = (Get-ChildItem $p -Recurse -Force -EA SilentlyContinue |
                                   Measure-Object -Property Length -Sum -EA SilentlyContinue).Sum
                            $freed += [long]$sz
                            Get-ChildItem $p -Recurse -Force -EA SilentlyContinue |
                                Remove-Item -Force -Recurse -EA SilentlyContinue
                        }
                    }
                    Write-Output "âœ“ Freed $([math]::Round($freed/1MB, 1)) MB of temporary files."
                    """ },

            new() { Id="clear-thumbnail-cache", Title="Rebuild thumbnail & icon cache",
                Description="Rebuilds thumbnail cache. Fixes black squares, missing icons, and wrong thumbnails.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["icons missing", "black thumbnails", "broken icons", "photos not showing", "explorer slow"],
                Script="""
                    Stop-Process -Name explorer -Force -EA SilentlyContinue
                    $cachePath = "$env:LOCALAPPDATA\Microsoft\Windows\Explorer"
                    $files = Get-ChildItem "$cachePath\thumbcache_*.db" -EA SilentlyContinue
                    $count = $files.Count
                    $files | Remove-Item -Force -EA SilentlyContinue
                    ie4uinit.exe -show
                    Start-Process explorer
                    Write-Output "âœ“ Removed $count thumbnail cache files. Explorer restarted."
                    """ },

            new() { Id="clear-dns-clipboard-cache", Title="Clear DNS, clipboard & shadow copies cache",
                Description="Clears DNS resolver cache, clipboard, and old Windows component store backups.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["clipboard not working", "clear cache", "reset clipboard"],
                Script="""
                    ipconfig /flushdns | Out-Null
                    cmd /c "echo off | clip" 2>&1 | Out-Null
                    # WinSxS cleanup (component store)
                    Dism /Online /Cleanup-Image /StartComponentCleanup /EA SilentlyContinue | Out-Null
                    Write-Output "âœ“ DNS cache flushed, clipboard cleared, component store cleaned."
                    """ },

            new() { Id="top-memory-processes", Title="Top 10 memory-hungry processes",
                Description="Shows which programs are using the most RAM right now.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["what is using my ram", "ram full", "high memory usage", "out of memory", "apps using memory"],
                Script="""
                    Write-Output "=== Top 10 processes by RAM usage ==="
                    Get-Process | Sort-Object WorkingSet -Descending | Select-Object -First 10 |
                    ForEach-Object {
                        Write-Output ("{0,-30} {1,8:N0} MB" -f $_.ProcessName, ($_.WorkingSet/1MB))
                    }
                    """ },

            new() { Id="top-cpu-processes", Title="Top 10 CPU-hungry processes",
                Description="Shows which programs are consuming the most CPU cycles.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["cpu at 100", "high cpu usage", "what is using my cpu", "computer hot", "cpu maxed out"],
                Script="""
                    Write-Output "=== Top 10 processes by CPU usage ==="
                    Get-Process | Sort-Object CPU -Descending | Select-Object -First 10 |
                    ForEach-Object {
                        Write-Output ("{0,-30} {1,10:N1} sec total CPU" -f $_.ProcessName, $_.CPU)
                    }
                    """ },

            new() { Id="set-high-performance", Title="Set power plan to High Performance",
                Description="Disables CPU throttling by activating the High Performance power plan.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["make computer faster", "speed up pc", "high performance mode", "max performance", "boost speed"],
                Script="""
                    powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c
                    $plan = (powercfg /getactivescheme) -replace '.*: ',''
                    Write-Output "âœ“ Power plan set. Active scheme: $plan"
                    """ },

            new() { Id="set-ultimate-performance", Title="Enable Ultimate Performance power plan",
                Description="Enables Windows' hidden 'Ultimate Performance' plan â€” maximum speed, higher power draw.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["ultimate performance", "maximum speed", "gaming performance mode"],
                Script="""
                    # Create the Ultimate Performance plan if it doesn't exist
                    $result = powercfg /duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 2>&1
                    if ($result -match '{(.+)}') {
                        $guid = $Matches[1]
                        powercfg /setactive $guid | Out-Null
                        Write-Output "âœ“ Ultimate Performance plan created and activated."
                    } else {
                        # Already exists â€” find and activate it
                        $plans = powercfg /list
                        $ultimate = $plans | Where-Object { $_ -match 'Ultimate' }
                        if ($ultimate -match '\*\s+({[^}]+})') {
                            powercfg /setactive $Matches[1] | Out-Null
                            Write-Output "âœ“ Ultimate Performance plan activated."
                        } else {
                            Write-Output "Ultimate Performance plan not available on this edition of Windows."
                        }
                    }
                    """ },

            new() { Id="disable-startup-apps", Title="Disable startup programs",
                Description="Opens Task Manager on the Startup tab â€” disable items causing slow boots.",
                Type=FixType.Guided, Keywords=["slow startup", "slow boot", "too many startup programs", "apps starting automatically", "computer takes forever to start"],
                Steps=[
                    new() { Title="Open Startup tab",   Instruction="Task Manager will open on the Startup tab.", Script="Start-Process taskmgr -ArgumentList '/0 /startup'" },
                    new() { Title="Disable slow items", Instruction="Right-click any 'High' impact app you don't need at startup and click 'Disable'." },
                    new() { Title="Restart",            Instruction="Restart your PC to see faster boot times." }
                ]},

            new() { Id="disk-space-all-drives", Title="Disk space on all drives",
                Description="Shows used and free space on every drive connected to this PC.",
                Type=FixType.Silent, RequiresAdmin=false,
                Script="""
                    Get-PSDrive -PSProvider FileSystem | Where-Object {$_.Used+$_.Free -gt 0} | ForEach-Object {
                        $total = $_.Used + $_.Free
                        $pct   = if($total -gt 0){[math]::Round(100*$_.Used/$total,0)}else{0}
                        $bar   = "â–ˆ" * [math]::Round($pct/5) + "â–‘" * (20 - [math]::Round($pct/5))
                        Write-Output ("{0}: [{1}] {2}% â€” {3:N1}GB used / {4:N1}GB free" -f $_.Name,$bar,$pct,($_.Used/1GB),($_.Free/1GB))
                    }
                    """ },

            new() { Id="find-large-files", Title="Find 20 largest files on C:",
                Description="Scans C: and lists the biggest files to help reclaim space.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["disk full", "find big files", "storage full", "hard drive full", "what is taking up space"],
                Script="""
                    Write-Output "Scanning C: for large files (may take 30â€“60 seconds)..."
                    Get-ChildItem 'C:\' -Recurse -File -ErrorAction SilentlyContinue |
                        Sort-Object Length -Descending |
                        Select-Object -First 20 |
                        ForEach-Object {
                            Write-Output ("{0,8:N0} MB  {1}" -f ($_.Length/1MB), $_.FullName)
                        }
                    """ },

            new() { Id="run-disk-cleanup", Title="Run Windows Disk Cleanup",
                Description="Runs Windows Disk Cleanup with all standard cleanup categories selected.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["clean disk", "disk cleanup", "free up space", "delete junk files"],
                Script="""
                    Write-Output "Configuring and running Disk Cleanup..."
                    # Set all cleanup categories (StateFlags0064 = run all)
                    $key = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches"
                    $categories = Get-ChildItem $key
                    foreach ($cat in $categories) {
                        Set-ItemProperty -Path $cat.PSPath -Name StateFlags0064 -Value 2 -Type DWord -EA SilentlyContinue
                    }
                    Start-Process cleanmgr -ArgumentList "/sagerun:64" -Wait
                    Write-Output "âœ“ Disk Cleanup complete."
                    """ },

            new() { Id="optimize-drive", Title="Optimize / defrag drive (C:)",
                Description="Optimizes C: â€” defragments HDDs and sends TRIM command to SSDs.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["defrag", "optimize ssd", "defragment hard drive", "slow drive"],
                Script="""
                    Write-Output "Optimizing C: drive (may take several minutes)..."
                    $result = defrag C: /U /V 2>&1
                    Write-Output $result
                    """ },

            new() { Id="schedule-chkdsk", Title="Schedule CHKDSK disk error scan",
                Description="Schedules a full disk error scan that runs on next restart.",
                Type=FixType.Guided, RequiresAdmin=true,
                Keywords=["disk errors", "bad sectors", "hard drive errors", "fix disk", "chkdsk"], Steps=[
                    new() { Title="Schedule scan",    Instruction="Click 'Done' to schedule a full disk scan on next restart.", Script="echo Y | chkdsk C: /f /r /x" },
                    new() { Title="Restart your PC",  Instruction="Save all open files and restart. The scan runs before Windows loads and takes 20â€“60 minutes depending on drive size." }
                ]},

            new() { Id="clear-wsu-download-cache", Title="Clear Windows Update download cache",
                Description="Deletes old Windows Update download files that waste disk space.",
                Type=FixType.Silent, RequiresAdmin=true,
                Script="""
                    Stop-Service wuauserv -Force -EA SilentlyContinue
                    $path = 'C:\Windows\SoftwareDistribution\Download'
                    $sz = (Get-ChildItem $path -Recurse -EA SilentlyContinue | Measure-Object -Property Length -Sum -EA SilentlyContinue).Sum
                    Remove-Item "$path\*" -Recurse -Force -EA SilentlyContinue
                    Start-Service wuauserv -EA SilentlyContinue
                    Write-Output "âœ“ Freed $([math]::Round($sz/1MB)) MB of Windows Update download cache."
                    """ },

            new() { Id="optimize-visual-effects", Title="Optimize visual effects for speed",
                Description="Turns off animations and visual effects that slow down older PCs.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["slow animations", "laggy windows", "windows animations slow", "speed up visual effects"],
                Script="""
                    $path = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects'
                    if (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
                    Set-ItemProperty -Path $path -Name VisualFXSetting -Value 2 -Type DWord
                    # Additional animation tweaks
                    Set-ItemProperty -Path 'HKCU:\Control Panel\Desktop' -Name MenuShowDelay -Value 0
                    Set-ItemProperty -Path 'HKCU:\Control Panel\Desktop\WindowMetrics' -Name MinAnimate -Value 0
                    Write-Output "âœ“ Visual effects optimized for performance. Sign out and back in to apply."
                    """ },

            new() { Id="disable-superfetch", Title="Disable SysMain (SuperFetch)",
                Description="Stops the SysMain service that pre-loads apps â€” helps on older/low-RAM machines.",
                Type=FixType.Silent, RequiresAdmin=true,
                Script="""
                    Stop-Service SysMain -Force -EA SilentlyContinue
                    Set-Service SysMain -StartupType Disabled -EA SilentlyContinue
                    Write-Output "âœ“ SysMain (SuperFetch) disabled. This can reduce HDD thrashing on older PCs."
                    Write-Output "  Note: On fast SSDs, SysMain usually helps rather than hurts."
                    """ },

            new() { Id="repair-wmi", Title="Repair WMI repository",
                Description="Fixes Windows Management Instrumentation errors that break many diagnostic tools.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["wmi error", "wmi broken", "management tools not working", "performance monitor broken"],
                Script="""
                    Write-Output "Repairing WMI repository..."
                    Stop-Service winmgmt -Force -EA SilentlyContinue
                    Start-Sleep 2
                    $wmiPath = "$env:SystemRoot\System32\wbem"
                    Push-Location $wmiPath
                    mofcomp cimwin32.mof 2>&1 | Out-Null
                    winmgmt /regserver 2>&1 | Out-Null
                    Pop-Location
                    Start-Service winmgmt
                    Write-Output "âœ“ WMI service repaired and restarted."
                    """ },
        ]
    };

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  3. AUDIO & DISPLAY
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static FixCategory AudioAndDisplay() => new()
    {
        Id="audio", Icon="\uE767", Title="Audio & Display",
        Fixes=
        [
            new() { Id="restart-audio-service", Title="Restart audio service",
                Description="Fixes sudden 'no sound' by restarting Windows Audio and endpoint services.",
                Type=FixType.Silent, RequiresAdmin=true,
                Script="""
                    Write-Output "Restarting audio services..."
                    Stop-Service AudioSrv -Force -EA SilentlyContinue
                    Stop-Service AudioEndpointBuilder -Force -EA SilentlyContinue
                    Start-Sleep 2
                    Start-Service AudioEndpointBuilder -EA SilentlyContinue
                    Start-Service AudioSrv -EA SilentlyContinue
                    $status = (Get-Service AudioSrv).Status
                    Write-Output "âœ“ Audio service status: $status"
                    """ },

            new() { Id="list-audio-devices", Title="List all audio devices",
                Description="Shows every audio device Windows sees, including disabled ones.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["see audio devices", "find speakers", "find headphones", "audio output list"],
                Script="""
                    Write-Output "=== Audio devices ==="
                    Get-WmiObject Win32_SoundDevice | ForEach-Object {
                        Write-Output "Device : $($_.Name)"
                        Write-Output "Status : $($_.Status)"
                        Write-Output "---"
                    }
                    """ },

            new() { Id="run-audio-troubleshooter", Title="Run audio troubleshooter",
                Description="Launches the Windows built-in audio troubleshooter.",
                Type=FixType.Silent, RequiresAdmin=false,
                Script="msdt.exe /id AudioPlaybackDiagnostic" },

            new() { Id="fix-audio-driver", Title="Reinstall audio driver",
                Description="Uninstalls and reinstalls the audio driver â€” fixes persistent sound problems.",
                Type=FixType.Guided, Keywords=["reinstall audio driver", "sound driver", "audio driver broken", "crackling sound", "distorted audio"],
                Steps=[
                    new() { Title="Open Device Manager",        Instruction="Device Manager will open.", Script="devmgmt.msc" },
                    new() { Title="Expand Sound controllers",   Instruction="Click 'Sound, video and game controllers'." },
                    new() { Title="Uninstall audio device",     Instruction="Right-click your audio device â†’ 'Uninstall device' â†’ check 'Delete driver software' â†’ Uninstall." },
                    new() { Title="Reinstall driver",           Instruction="Click Action â†’ 'Scan for hardware changes'. Windows reinstalls the driver automatically. Restart when prompted." }
                ]},

            new() { Id="set-audio-output", Title="Change audio output device",
                Description="Opens Sound settings so you can select the correct speakers or headphones.",
                Type=FixType.Guided, Steps=[
                    new() { Title="Open Sound settings", Instruction="Sound settings will open.", Script="Start-Process ms-settings:sound" },
                    new() { Title="Select output",       Instruction="Under 'Output', choose your correct audio device from the dropdown." },
                    new() { Title="Test",                Instruction="Click 'Test' to verify audio is working on the selected device." }
                ]},

            new() { Id="fix-audio-distortion", Title="Fix audio distortion / crackling",
                Description="Disables audio enhancements that can cause distortion and crackling.",
                Type=FixType.Guided, Steps=[
                    new() { Title="Open Playback devices",    Instruction="Sound settings will open.", Script="Start-Process mmsys.cpl" },
                    new() { Title="Open Properties",          Instruction="Double-click your default playback device to open its Properties." },
                    new() { Title="Disable enhancements",     Instruction="Click the 'Enhancements' or 'Advanced' tab â†’ check 'Disable all enhancements' â†’ click OK. Also try changing the sample rate to 24 bit, 44100 Hz." }
                ]},

            new() { Id="list-monitors", Title="List connected monitors",
                Description="Shows all monitors Windows currently detects and their status.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["monitor not detected", "second monitor", "display not found", "extend display"],
                Script="""
                    Write-Output "=== Connected monitors ==="
                    Get-PnpDevice -Class Monitor | ForEach-Object {
                        Write-Output "$($_.FriendlyName) â€” Status: $($_.Status)"
                    }
                    $count = (Get-PnpDevice -Class Monitor | Where-Object {$_.Status -eq 'OK'}).Count
                    Write-Output "Active monitors: $count"
                    """ },

            new() { Id="fix-display-scaling", Title="Fix blurry apps / DPI scaling",
                Description="Opens display settings to fix scaling issues on high-DPI screens.",
                Type=FixType.Guided, Steps=[
                    new() { Title="Open display settings",     Instruction="Display Settings will open.", Script="Start-Process ms-settings:display" },
                    new() { Title="Adjust scale",              Instruction="Under 'Scale', try 100%, 125%, or 150%. Laptops usually look best at 125% or 150%. Click Apply." },
                    new() { Title="Fix individual blurry app", Instruction="For a specific blurry app: right-click its shortcut â†’ Properties â†’ Compatibility â†’ 'Change high DPI settings' â†’ check 'Override high DPI scaling behavior' â†’ set to 'Application'." }
                ]},

            new() { Id="fix-screen-flicker", Title="Fix screen flickering",
                Description="Diagnoses and fixes screen flickering by checking drivers and refresh rate.",
                Type=FixType.Guided, Keywords=["screen flickering", "monitor flickers", "flashing screen", "flickering display"],
                Steps=[
                    new() { Title="Open Task Manager",    Instruction="Task Manager will open. If the taskbar flickers along with the screen, it's a driver issue. If Task Manager itself flickers, it's an app conflict.", Script="Start-Process taskmgr" },
                    new() { Title="Update display driver", Instruction="Open Device Manager to update your GPU driver.", Script="devmgmt.msc" },
                    new() { Title="Lower refresh rate",    Instruction="Settings â†’ System â†’ Display â†’ Advanced display â†’ try 60Hz instead of a higher rate." }
                ]},

            new() { Id="update-display-driver", Title="Update display driver",
                Description="Opens Device Manager to update your GPU driver.",
                Type=FixType.Guided, Keywords=["gpu driver", "graphics driver update", "display driver", "video driver"],
                Steps=[
                    new() { Title="Open Device Manager",      Instruction="Device Manager will open.", Script="devmgmt.msc" },
                    new() { Title="Expand Display Adapters",  Instruction="Click 'Display adapters' to expand." },
                    new() { Title="Update driver",            Instruction="Right-click your GPU â†’ 'Update driver' â†’ 'Search automatically for drivers'." }
                ]},

            new() { Id="calibrate-display", Title="Calibrate display colors",
                Description="Runs the Windows Display Color Calibration wizard.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["color calibration", "screen colors wrong", "display calibration", "monitor colors"],
                Script="Start-Process dccw.exe" },

            new() { Id="set-display-settings", Title="Open display settings",
                Description="Opens display settings â€” for resolution, scale, and multi-monitor setup.",
                Type=FixType.Silent, RequiresAdmin=false,
                Script="Start-Process ms-settings:display" },

            new() { Id="fix-hdmi-no-sound", Title="Fix no sound through HDMI/DisplayPort",
                Description="Sets the HDMI/DisplayPort audio device as default â€” fixes TV and monitor audio.",
                Type=FixType.Guided, Steps=[
                    new() { Title="Open Playback devices",   Instruction="The Playback devices window will open.", Script="Start-Process mmsys.cpl" },
                    new() { Title="Show all devices",        Instruction="Right-click anywhere in the list â†’ check 'Show Disabled Devices' and 'Show Disconnected Devices'." },
                    new() { Title="Set HDMI as default",     Instruction="Find your HDMI or DisplayPort audio device, right-click it â†’ 'Set as Default Device'. Click OK." }
                ]},
        ]
    };

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  4. WINDOWS UPDATE & DRIVERS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static FixCategory UpdatesAndDrivers() => new()
    {
        Id="updates", Icon="\uE777", Title="Updates & Drivers",
        Fixes=
        [
            new() { Id="open-windows-update", Title="Open Windows Update",
                Description="Opens Windows Update to check for and install available updates.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["check for updates", "install windows updates", "update windows", "update settings"],
                Script="Start-Process ms-settings:windowsupdate" },

            new() { Id="reset-windows-update", Title="Reset Windows Update components",
                Description="Fixes stuck or perpetually failing Windows Updates by resetting the entire update engine.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["windows update stuck", "update error", "update not working", "update failing", "can't update windows"],
                Script="""
                    Write-Output "Stopping update services..."
                    foreach ($s in @('wuauserv','cryptSvc','bits','msiserver','TrustedInstaller')) {
                        Stop-Service $s -Force -EA SilentlyContinue
                    }
                    Write-Output "Clearing update cache folders..."
                    Remove-Item 'C:\Windows\SoftwareDistribution' -Recurse -Force -EA SilentlyContinue
                    Remove-Item 'C:\Windows\System32\catroot2'     -Recurse -Force -EA SilentlyContinue
                    Write-Output "Reregistering update DLLs..."
                    $dlls = @('atl.dll','urlmon.dll','mshtml.dll','shdocvw.dll','browseui.dll','jscript.dll','vbscript.dll','scrrun.dll','msxml.dll','msxml3.dll','msxml6.dll','actxprxy.dll','softpub.dll','wintrust.dll','dssenh.dll','rsaenh.dll','gpkcsp.dll','sccbase.dll','slbcsp.dll','cryptdlg.dll','oleaut32.dll','ole32.dll','shell32.dll','initpki.dll','wuapi.dll','wuaueng.dll','wuaueng1.dll','wucltui.dll','wups.dll','wups2.dll','wuweb.dll','qmgr.dll','qmgrprxy.dll','wucltux.dll','muweb.dll','wuwebv.dll')
                    foreach ($dll in $dlls) { regsvr32.exe /s $dll 2>&1 | Out-Null }
                    netsh winhttp reset proxy 2>&1 | Out-Null
                    Write-Output "Restarting update services..."
                    foreach ($s in @('bits','wuauserv','cryptSvc','msiserver')) {
                        Start-Service $s -EA SilentlyContinue
                    }
                    Write-Output "âœ“ Windows Update reset complete. Try checking for updates again."
                    """ },

            new() { Id="scan-driver-problems", Title="Scan for driver problems",
                Description="Lists all devices in Device Manager with errors or missing drivers.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["driver error", "missing driver", "device not working", "yellow exclamation device manager"],
                Script="""
                    $bad = Get-PnpDevice | Where-Object { $_.Status -ne 'OK' }
                    if ($bad) {
                        Write-Output "=== Devices with problems ==="
                        $bad | ForEach-Object {
                            Write-Output "âœ— $($_.FriendlyName) â€” Status: $($_.Status) â€” Class: $($_.Class)"
                        }
                    } else {
                        Write-Output "âœ“ All devices are working correctly. No driver issues found."
                    }
                    """ },

            new() { Id="run-sfc", Title="Run System File Checker (SFC)",
                Description="Scans and repairs corrupted Windows system files. Takes 5â€“15 minutes.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["fix corrupted files", "windows file checker", "system file repair", "sfc scannow"],
                Script="""
                    Write-Output "Running SFC scan â€” please wait (5â€“15 minutes)..."
                    sfc /scannow
                    Write-Output "âœ“ SFC scan complete. Check the output above for results."
                    """ },

            new() { Id="run-dism", Title="Run DISM â€” repair Windows image",
                Description="Repairs the Windows component store. Run this if SFC reports it cannot fix files.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["repair windows", "windows image repair", "dism restore", "fix windows components"],
                Script="""
                    Write-Output "Running DISM health check..."
                    DISM /Online /Cleanup-Image /CheckHealth
                    Write-Output "Running DISM full restore (requires internet, 10â€“25 min)..."
                    DISM /Online /Cleanup-Image /RestoreHealth
                    Write-Output "âœ“ DISM complete."
                    """ },

            new() { Id="check-activation", Title="Check Windows activation status",
                Description="Shows whether Windows is properly activated.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["windows not activated", "activation error", "is windows activated", "windows activation"],
                Script="slmgr /xpr" },

            new() { Id="show-installed-updates", Title="View recent Windows updates",
                Description="Lists the last 20 Windows updates installed on this PC.",
                Type=FixType.Silent, RequiresAdmin=false,
                Script="""
                    Write-Output "=== Last 20 installed Windows updates ==="
                    Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 20 |
                    ForEach-Object {
                        Write-Output "$($_.HotFixID)  $($_.Description)  Installed: $($_.InstalledOn)"
                    }
                    """ },

            new() { Id="schedule-memory-test", Title="Schedule RAM (memory) test",
                Description="Schedules a Windows Memory Diagnostic to detect bad RAM on next restart.",
                Type=FixType.Guided, Steps=[
                    new() { Title="Schedule the test",   Instruction="Memory Diagnostic will be scheduled for next restart.", Script="mdsched.exe" },
                    new() { Title="Restart your PC",     Instruction="Save your work and restart. The test runs before Windows loads and takes 10â€“30 minutes." },
                    new() { Title="Check results",       Instruction="After restart: search 'Event Viewer' â†’ Windows Logs â†’ System â†’ look for 'MemoryDiagnostics-Results' events." }
                ]},

            new() { Id="install-directx", Title="Update DirectX runtime",
                Description="Downloads and runs the latest DirectX End-User Runtime to fix game DLL errors.",
                Type=FixType.Guided, Keywords=["directx error", "missing directx", "game won't start directx", "dx11 error"],
                Steps=[
                    new() { Title="Download DirectX runtime", Instruction="The Microsoft DirectX download page will open.", Script="Start-Process 'https://www.microsoft.com/en-us/download/details.aspx?id=35'" },
                    new() { Title="Install and restart",      Instruction="Run the downloaded dxwebsetup.exe, install, then restart your PC." }
                ]},
        ]
    };
}
