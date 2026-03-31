using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;

namespace HelpDesk.Infrastructure.Fixes;

public sealed partial class FixCatalogService
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  5. PRINTERS & PERIPHERALS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static FixCategory PrintersAndPeripherals() => new()
    {
        Id="printers", Icon="\uE749", Title="Printers & Peripherals",
        Fixes=
        [
            new() { Id="clear-print-queue", Title="Clear stuck print queue",
                Description="Stops the spooler, deletes all stuck jobs, and restarts â€” fixes 99% of printer jams.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["printer stuck", "print queue stuck", "printer not printing", "can't print", "printing paused", "documents stuck"],
                Script="""
                    Write-Output "Stopping print spooler..."
                    Stop-Service Spooler -Force -EA SilentlyContinue
                    Start-Sleep 2
                    $count = (Get-ChildItem 'C:\Windows\System32\spool\PRINTERS' -EA SilentlyContinue).Count
                    Remove-Item 'C:\Windows\System32\spool\PRINTERS\*' -Force -EA SilentlyContinue
                    Start-Service Spooler
                    $status = (Get-Service Spooler).Status
                    Write-Output "âœ“ Removed $count stuck print job(s). Spooler: $status"
                    """ },

            new() { Id="list-printers", Title="List installed printers & status",
                Description="Shows all printers on this PC with current status and port.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["see installed printers", "find printer", "available printers"],
                Script="""
                    Write-Output "=== Installed printers ==="
                    Get-Printer | ForEach-Object {
                        Write-Output "Printer : $($_.Name)"
                        Write-Output "  Status  : $($_.PrinterStatus)"
                        Write-Output "  Default : $($_.Default)"
                        Write-Output "  Port    : $($_.PortName)"
                        Write-Output "---"
                    }
                    """ },

            new() { Id="run-printer-troubleshooter", Title="Run printer troubleshooter",
                Description="Launches the Windows built-in printer troubleshooter.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["printer not working", "fix printer", "printer problem", "printer help"],
                Script="msdt.exe /id PrinterDiagnostic" },

            new() { Id="set-default-printer", Title="Manage default printer",
                Description="Opens Printers & Scanners settings to change the default printer.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["change default printer", "printer settings", "which printer"],
                Script="Start-Process ms-settings:printers" },

            new() { Id="fix-usb-not-recognized", Title="Fix USB device not recognized",
                Description="Resets all USB controllers to fix 'USB device not recognized' errors.",
                Type=FixType.Guided, RequiresAdmin=true,
                Keywords=["usb not working", "device not recognized", "usb error", "plug in usb nothing happens"], Steps=[
                    new() { Title="Unplug the device",      Instruction="Unplug the USB device and wait 10 seconds." },
                    new() { Title="Reset USB controllers",  Instruction="Click 'Done' to reset all USB controllers.", Script="""
                        Get-PnpDevice -Class USB | Where-Object {$_.Status -eq 'OK'} | ForEach-Object {
                            $_ | Disable-PnpDevice -Confirm:$false -EA SilentlyContinue
                            Start-Sleep -Milliseconds 400
                            $_ | Enable-PnpDevice -Confirm:$false -EA SilentlyContinue
                        }
                        Write-Output "âœ“ USB controllers reset."
                        """ },
                    new() { Title="Plug back in",           Instruction="Plug the USB device into a different port. Windows should now detect it." }
                ]},

            new() { Id="scan-new-hardware", Title="Scan for new hardware",
                Description="Asks Windows to re-scan for devices it may have missed.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["device not detected", "new hardware not found", "hardware not showing up"],
                Script="pnputil /scan-devices; Write-Output 'âœ“ Hardware scan triggered.'" },

            new() { Id="fix-bluetooth", Title="Fix Bluetooth not connecting",
                Description="Restarts the Bluetooth service and guides you through re-pairing a device.",
                Type=FixType.Guided, Keywords=["bluetooth not connecting", "bluetooth pairing", "bluetooth not working", "can't pair device"],
                Steps=[
                    new() { Title="Restart Bluetooth",  Instruction="Click 'Done' to restart the Bluetooth service.",
                Script="""
                        Restart-Service bthserv -EA SilentlyContinue
                        Write-Output "âœ“ Bluetooth service restarted."
                        """ },
                    new() { Title="Open Bluetooth settings", Instruction="Bluetooth settings will open.", Script="Start-Process ms-settings:bluetooth" },
                    new() { Title="Remove and re-pair",      Instruction="Find your device â†’ click it â†’ 'Remove device'. Then put your device in pairing mode and click 'Add device'." }
                ]},

            new() { Id="restart-spooler", Title="Restart print spooler service",
                Description="Restarts just the print spooler without clearing jobs â€” a lighter-touch fix.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["printer not working", "restart print spooler", "print service", "spooler crashed"],
                Script="""
                    Restart-Service Spooler -Force -EA SilentlyContinue
                    Write-Output "âœ“ Print spooler restarted. Status: $((Get-Service Spooler).Status)"
                    """ },
        ]
    };

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  6. GAMING & STREAMING  (Most elaborate section)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static FixCategory GamingAndStreaming() => new()
    {
        Id="gaming", Icon="\uE7FC", Title="Gaming & Streaming",
        Fixes=
        [
            // â”€â”€ GENERAL GAMING OPTIMIZATIONS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            new() { Id="enable-game-mode", Title="Enable Game Mode",
                Description="Turns on Windows Game Mode to prioritize CPU and GPU resources for games.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["gaming mode", "boost gaming", "improve gaming performance", "game mode windows"],
                Script="""
                    $p = 'HKCU:\SOFTWARE\Microsoft\GameBar'
                    if (!(Test-Path $p)) { New-Item -Path $p -Force | Out-Null }
                    Set-ItemProperty -Path $p -Name AutoGameModeEnabled -Value 1 -Type DWord
                    Set-ItemProperty -Path $p -Name AllowAutoGameMode   -Value 1 -Type DWord
                    Write-Output "âœ“ Game Mode enabled."
                    """ },

            new() { Id="disable-game-bar", Title="Disable Xbox Game Bar & Game DVR",
                Description="Disables Xbox Game Bar and Game DVR â€” known causes of FPS drops and stuttering.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["game bar overlay", "disable overlay", "fps drops", "game bar stutter", "xbox overlay"],
                Script="""
                    # Disable Game Bar
                    $gb = 'HKCU:\SOFTWARE\Microsoft\GameBar'
                    if (!(Test-Path $gb)) { New-Item -Path $gb -Force | Out-Null }
                    Set-ItemProperty -Path $gb -Name UseNexusForGameBarEnabled -Value 0 -Type DWord

                    # Disable Game DVR
                    $gs = 'HKCU:\System\GameConfigStore'
                    if (!(Test-Path $gs)) { New-Item -Path $gs -Force | Out-Null }
                    Set-ItemProperty -Path $gs -Name GameDVR_Enabled -Value 0 -Type DWord
                    Set-ItemProperty -Path $gs -Name GameDVR_FSEBehaviorMode -Value 2 -Type DWord

                    $policyPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR'
                    if (!(Test-Path $policyPath)) { New-Item -Path $policyPath -Force | Out-Null }
                    Set-ItemProperty -Path $policyPath -Name AllowGameDVR -Value 0 -Type DWord

                    # Stop and disable Xbox services
                    foreach ($svc in @('XblGameSave','XblAuthManager','XboxNetApiSvc','XboxGipSvc')) {
                        Stop-Service $svc -Force -EA SilentlyContinue
                        Set-Service  $svc -StartupType Disabled -EA SilentlyContinue
                    }
                    Write-Output "âœ“ Xbox Game Bar and Game DVR disabled. Restart to fully apply."
                    """ },

            new() { Id="gaming-registry-tweaks", Title="Apply gaming registry optimizations",
                Description="Applies MMCSS priority, network throttling disable, and GPU scheduling tweaks.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["gaming tweaks", "registry performance", "speed up gaming", "optimize for games"],
                Script="""
                    # MMCSS â€” give games priority CPU scheduling
                    $games = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games'
                    if (!(Test-Path $games)) { New-Item -Path $games -Force | Out-Null }
                    Set-ItemProperty -Path $games -Name 'GPU Priority'       -Value 8    -Type DWord
                    Set-ItemProperty -Path $games -Name 'Priority'           -Value 6    -Type DWord
                    Set-ItemProperty -Path $games -Name 'Scheduling Category'-Value 'High' -Type String
                    Set-ItemProperty -Path $games -Name 'SFIO Priority'      -Value 'High' -Type String

                    # Disable network throttling for gaming
                    $sp = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile'
                    Set-ItemProperty -Path $sp -Name SystemResponsiveness    -Value 0    -Type DWord
                    Set-ItemProperty -Path $sp -Name NetworkThrottlingIndex  -Value 0xffffffff -Type DWord

                    # Hardware-accelerated GPU scheduling (HAGS) â€” Windows 10 2004+
                    $hags = 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers'
                    Set-ItemProperty -Path $hags -Name HwSchMode -Value 2 -Type DWord -EA SilentlyContinue

                    # Disable power throttling for better CPU consistency
                    $pt = 'HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling'
                    if (!(Test-Path $pt)) { New-Item -Path $pt -Force | Out-Null }
                    Set-ItemProperty -Path $pt -Name PowerThrottlingOff -Value 1 -Type DWord

                    Write-Output "âœ“ Gaming registry tweaks applied. Restart to fully activate."
                    """ },

            new() { Id="gaming-network-tweaks", Title="Apply low-latency network tweaks",
                Description="Sets TcpAckFrequency and TCPNoDelay on all adapters â€” reduces game ping.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["reduce lag", "lower ping", "gaming network settings", "tcp tweak", "network latency"],
                Script="""
                    # Apply TCP No-Delay and Ack tweaks per network interface
                    $interfaces = Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces'
                    $applied = 0
                    foreach ($iface in $interfaces) {
                        $dhcp = (Get-ItemProperty -Path $iface.PSPath -EA SilentlyContinue).DhcpIPAddress
                        if ($dhcp -and $dhcp -ne '0.0.0.0') {
                            Set-ItemProperty -Path $iface.PSPath -Name TcpAckFrequency -Value 1 -Type DWord -EA SilentlyContinue
                            Set-ItemProperty -Path $iface.PSPath -Name TCPNoDelay      -Value 1 -Type DWord -EA SilentlyContinue
                            Set-ItemProperty -Path $iface.PSPath -Name TcpDelAckTicks  -Value 0 -Type DWord -EA SilentlyContinue
                            $applied++
                        }
                    }

                    # Global TCP tweaks
                    $tcp = 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters'
                    Set-ItemProperty -Path $tcp -Name 'MaxUserPort'      -Value 65534 -Type DWord -EA SilentlyContinue
                    Set-ItemProperty -Path $tcp -Name 'TcpTimedWaitDelay'-Value 32    -Type DWord -EA SilentlyContinue

                    # MSMQ TCP no delay
                    $msmq = 'HKLM:\SOFTWARE\Microsoft\MSMQ\Parameters'
                    if (!(Test-Path $msmq)) { New-Item -Path $msmq -Force | Out-Null }
                    Set-ItemProperty -Path $msmq -Name 'TCPNoDelay' -Value 1 -Type DWord -EA SilentlyContinue

                    Write-Output "âœ“ Low-latency network tweaks applied to $applied interface(s). Restart to activate."
                    """ },

            new() { Id="force-dedicated-gpu", Title="Force game to use dedicated GPU",
                Description="Opens Windows graphics settings to assign a game to your dedicated GPU (not integrated).",
                Type=FixType.Guided, Keywords=["game using wrong gpu", "integrated graphics", "use nvidia", "use amd", "gpu not being used"],
                Steps=[
                    new() { Title="Open graphics settings", Instruction="Graphics settings will open.", Script="Start-Process ms-settings:display-advancedgraphics" },
                    new() { Title="Add your game",          Instruction="Click 'Browse', navigate to your game's .exe file, and click Add." },
                    new() { Title="Set High Performance",   Instruction="Click on the game in the list â†’ Options â†’ select 'High Performance' â†’ Save." }
                ]},

            new() { Id="clear-shader-cache", Title="Clear GPU shader cache (NVIDIA/AMD)",
                Description="Deletes compiled shader caches for both NVIDIA and AMD â€” fixes stuttering and visual glitches.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["game stuttering", "graphical glitches", "gpu cache", "shader stutter", "game loading slow"],
                Script="""
                    $paths = @(
                        "$env:LOCALAPPDATA\NVIDIA\DXCache",
                        "$env:LOCALAPPDATA\NVIDIA\GLCache",
                        "$env:APPDATA\NVIDIA\ComputeCache",
                        "$env:LOCALAPPDATA\NVIDIA Corporation\NV_Cache",
                        "$env:TEMP\AMD\DxcCache",
                        "$env:LOCALAPPDATA\AMD\DxCache",
                        "$env:LOCALAPPDATA\D3DSCache"
                    )
                    $freed = 0L
                    foreach ($p in $paths) {
                        if (Test-Path $p) {
                            $sz = (Get-ChildItem $p -Recurse -EA SilentlyContinue | Measure-Object -Property Length -Sum -EA SilentlyContinue).Sum
                            $freed += [long]$sz
                            Remove-Item "$p\*" -Recurse -Force -EA SilentlyContinue
                        }
                    }
                    Write-Output "âœ“ GPU shader caches cleared â€” freed $([math]::Round($freed/1MB,1)) MB."
                    """ },

            new() { Id="optimize-mouse-gaming", Title="Optimize mouse for gaming",
                Description="Disables mouse acceleration (enhance pointer precision) for raw, consistent input.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["mouse acceleration off", "raw input", "gaming mouse", "precise mouse", "mouse settings"],
                Script="""
                    $mouse = 'HKCU:\Control Panel\Mouse'
                    Set-ItemProperty -Path $mouse -Name MouseSpeed      -Value 0
                    Set-ItemProperty -Path $mouse -Name MouseThreshold1 -Value 0
                    Set-ItemProperty -Path $mouse -Name MouseThreshold2 -Value 0
                    Write-Output "âœ“ Mouse acceleration disabled. Log out and back in to apply."
                    Write-Output "  Note: You may need to re-adjust in-game sensitivity after this change."
                    """ },

            new() { Id="disable-fullscreen-optimizations-global", Title="Disable fullscreen optimizations (global)",
                Description="Disables Windows fullscreen optimizations globally â€” reduces input lag in many games.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["fullscreen optimization", "game fps drop", "disable fullscreen opt", "input lag"],
                Script="""
                    $path = 'HKCU:\System\GameConfigStore'
                    if (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
                    Set-ItemProperty -Path $path -Name GameDVR_FSEBehaviorMode -Value 2 -Type DWord
                    Set-ItemProperty -Path $path -Name GameDVR_HonorUserFSEBehaviorMode -Value 1 -Type DWord
                    Set-ItemProperty -Path $path -Name GameDVR_DXGIHonorFSEWindowsCompatible -Value 1 -Type DWord
                    Write-Output "âœ“ Fullscreen optimizations disabled globally."
                    """ },

            new() { Id="check-temperatures", Title="Check CPU & GPU temperatures",
                Description="Reads current thermal sensor data â€” warns if your PC is running too hot for gaming.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["cpu temperature", "gpu temperature", "computer hot", "overheating", "check temps", "thermal throttling"],
                Script="""
                    Write-Output "=== Thermal sensor readings ==="
                    try {
                        $temps = Get-WmiObject MSAcpi_ThermalZoneTemperature -Namespace root/wmi -EA Stop
                        foreach ($t in $temps) {
                            $c = [math]::Round(($t.CurrentTemperature - 2732) / 10, 1)
                            $status = if($c -gt 90){"âš  CRITICAL"}elseif($c -gt 80){"âš  Hot"}elseif($c -gt 70){"â—‰ Warm"}else{"âœ“ Normal"}
                            Write-Output "Thermal zone: $cÂ°C  $status"
                        }
                    } catch {
                        Write-Output "WMI thermal sensors not accessible without elevation."
                        Write-Output "For full temp readings, use the System Info tab."
                    }
                    """ },

            new() { Id="add-defender-game-exclusions", Title="Add game folders to Defender exclusions",
                Description="Stops Defender from scanning your Steam/Epic/game folders while you play â€” reduces stutters.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["antivirus slowing game", "defender affecting fps", "whitelist game folder", "exclude game from scan"],
                Script="""
                    $paths = @()
                    # Common game library locations
                    $steam = "C:\Program Files (x86)\Steam\steamapps"
                    $epic  = "$env:LOCALAPPDATA\EpicGamesLauncher\Data"
                    $gog   = "C:\Program Files (x86)\GOG Galaxy\Games"
                    $xbox  = "$env:LOCALAPPDATA\Packages\Microsoft.GamingApp_8wekyb3d8bbwe"
                    foreach ($p in @($steam,$epic,$gog,$xbox)) {
                        if (Test-Path $p) { $paths += $p }
                    }
                    if ($paths.Count -eq 0) {
                        Write-Output "No standard game library folders found. Add paths manually in Windows Security â†’ Exclusions."
                    } else {
                        foreach ($p in $paths) {
                            Add-MpPreference -ExclusionPath $p -EA SilentlyContinue
                            Write-Output "âœ“ Added exclusion: $p"
                        }
                        Write-Output "âœ“ Game folders added to Windows Defender exclusions."
                    }
                    """ },

            // â”€â”€ GAME STREAMING (OBS / STREAMLABS / etc.) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            new() { Id="streaming-diagnose-dropped-frames", Title="Diagnose dropped frames (streaming)",
                Description="Checks CPU, GPU, RAM, and network to identify why your stream is dropping frames.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["obs dropped frames", "streaming stuttering", "frames dropping obs", "stream dropping", "twitch lag"],
                Script="""
                    Write-Output "=== Stream Health Diagnostic ==="
                    Write-Output ""

                    # CPU
                    $cpu = (Get-WmiObject Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
                    $cpuStatus = if($cpu -gt 90){"âš  CRITICAL â€” encoder likely overloaded"}elseif($cpu -gt 75){"âš  High â€” use hardware encoder (NVENC/AMF)"}else{"âœ“ OK ($cpu%)"}
                    Write-Output "CPU Usage       : $cpu%  $cpuStatus"

                    # RAM
                    $os = Get-CimInstance Win32_OperatingSystem
                    $ramPct = [math]::Round(100 * ($os.TotalVisibleMemorySize - $os.FreePhysicalMemory) / $os.TotalVisibleMemorySize)
                    $ramStatus = if($ramPct -gt 90){"âš  CRITICAL â€” close background apps"}elseif($ramPct -gt 80){"âš  High"}else{"âœ“ OK ($ramPct%)"}
                    Write-Output "RAM Usage       : $ramPct%  $ramStatus"

                    # GPU (via WMI)
                    $gpuVid = (Get-CimInstance Win32_VideoController | Select-Object -First 1)
                    Write-Output "GPU             : $($gpuVid.Name)"

                    # Network â€” quick ping jitter test
                    $pings = 1..10 | ForEach-Object { (Test-Connection 8.8.8.8 -Count 1 -EA SilentlyContinue).ResponseTime }
                    $pings = $pings | Where-Object {$_}
                    if ($pings) {
                        $avg    = [math]::Round(($pings | Measure-Object -Average).Average)
                        $max    = ($pings | Measure-Object -Maximum).Maximum
                        $jitter = $max - ($pings | Measure-Object -Minimum).Minimum
                        $netStatus = if($jitter -gt 50){"âš  High jitter â€” likely causing dropped frames"}elseif($jitter -gt 20){"âš  Moderate jitter"}else{"âœ“ Stable"}
                        Write-Output "Network Ping    : avg ${avg}ms  jitter ${jitter}ms  $netStatus"
                    } else {
                        Write-Output "Network         : âœ— No response from ping servers"
                    }

                    Write-Output ""
                    Write-Output "=== Recommendations ==="
                    if ($cpu -gt 75) {
                        Write-Output "â€¢ High CPU: Switch OBS encoder from x264 (CPU) to NVENC (NVIDIA GPU) or AMF (AMD GPU)"
                        Write-Output "â€¢ High CPU: Lower in-game resolution or cap FPS to 120/144"
                        Write-Output "â€¢ High CPU: Close Chrome, Discord video, and other background apps while streaming"
                    }
                    if ($ramPct -gt 80) { Write-Output "â€¢ Low RAM: Close browser tabs and non-essential apps" }
                    if ($pings -and $jitter -gt 20) {
                        Write-Output "â€¢ Network jitter: Use a wired Ethernet connection instead of Wi-Fi"
                        Write-Output "â€¢ Network jitter: Try a different ingest server in OBS/Streamlabs settings"
                    }
                    """ },

            new() { Id="streaming-obs-settings-guide", Title="Fix OBS 'Encoding Overloaded' warning",
                Description="Step-by-step guide to fix the most common OBS encoding overloaded error.",
                Keywords=["obs settings", "obs setup", "best obs settings", "streaming quality settings"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Switch to hardware encoder",
                        Instruction="In OBS: Settings â†’ Output â†’ Encoder. Change from 'Software (x264)' to 'NVENC H.264' (NVIDIA) or 'AMD HW H.264' (AMD). Hardware encoding offloads work from your CPU to your GPU chip." },
                    new() { Title="Lower output resolution",
                        Instruction="In OBS: Settings â†’ Video â†’ Output (Scaled) Resolution. Change from 1920Ã—1080 to 1280Ã—720. Your viewers won't notice at typical stream bitrates, and this halves the encoding workload." },
                    new() { Title="Cap your in-game FPS",
                        Instruction="In your game settings: enable V-sync or set a 120â€“144 FPS cap. Uncapped FPS makes the GPU work overtime, leaving less headroom for OBS to encode frames." },
                    new() { Title="Close background apps",
                        Instruction="Before going live: close Chrome, Discord (leave audio only), Spotify desktop, and any game overlays you don't need. Each one steals resources from OBS." },
                    new() { Title="Enable 'Process Priority'",
                        Instruction="In OBS: Settings â†’ Advanced â†’ Process Priority â†’ set to 'High'. This tells Windows to give OBS more CPU time relative to background tasks." }
                ]},

            new() { Id="streaming-fix-dropped-frames-network", Title="Fix dropped frames â€” network causes",
                Description="Step-by-step guide to fix stream dropped frames caused by network issues.",
                Keywords=["dropped frames network", "stream disconnecting", "internet causing drops", "streaming connection"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Check your upload speed",
                        Instruction="Go to speedtest.net and run a test. For 1080p60 streaming, you need at least 8 Mbps upload. For 720p60, at least 4 Mbps. If you're below this, your bitrate is too high for your connection." },
                    new() { Title="Lower your bitrate",
                        Instruction="In OBS/Streamlabs: Settings â†’ Output â†’ Bitrate. A safe formula: set bitrate to 75% of your stable upload speed. E.g. if your upload is 10 Mbps, use 7500 kbps or lower." },
                    new() { Title="Switch to Ethernet",
                        Instruction="Plug directly into your router with an Ethernet cable. Wi-Fi adds unpredictable latency spikes that cause dropped frames no matter how good your plan is." },
                    new() { Title="Change ingest server",
                        Instruction="In OBS/Streamlabs: Settings â†’ Stream â†’ Server. Select 'Auto' or manually pick a server geographically close to you. The auto-selected server is not always the best one." },
                    new() { Title="Run network latency fix",
                        Instruction="Click 'Done' to flush DNS and reset the network stack.", Script="""
                            ipconfig /flushdns | Out-Null
                            netsh int tcp set global autotuninglevel=normal | Out-Null
                            Write-Output "âœ“ DNS flushed and TCP auto-tuning restored."
                            """ },
                    new() { Title="Open port 1935 (RTMP)",
                        Instruction="If your stream still drops, your firewall may be blocking RTMP. In Windows Security: Firewall â†’ Advanced Settings â†’ Outbound Rules â†’ New Rule â†’ Port â†’ TCP 1935 â†’ Allow." }
                ]},

            new() { Id="streaming-clear-obs-cache", Title="Clear OBS / Streamlabs cache",
                Description="Clears OBS Studio and Streamlabs Desktop cache files that cause crashes and setting corruption.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["clear obs cache", "obs slow", "reset obs", "obs not working", "obs cleanup"],
                Script="""
                    $obs = "$env:APPDATA\obs-studio"
                    $slobs = "$env:APPDATA\slobs-client"
                    $freed = 0L

                    foreach ($base in @($obs, $slobs)) {
                        if (Test-Path $base) {
                            # Clear cache (not config)
                            $cacheDirs = @("$base\logs", "$base\crashes", "$base\updates")
                            foreach ($dir in $cacheDirs) {
                                if (Test-Path $dir) {
                                    $sz = (Get-ChildItem $dir -Recurse -EA SilentlyContinue | Measure-Object -Property Length -Sum -EA SilentlyContinue).Sum
                                    $freed += [long]$sz
                                    Get-ChildItem $dir -Recurse -Force -EA SilentlyContinue | Remove-Item -Force -Recurse -EA SilentlyContinue
                                    Write-Output "Cleared: $dir"
                                }
                            }
                        }
                    }
                    Write-Output "âœ“ OBS/Streamlabs caches cleared â€” freed $([math]::Round($freed/1KB)) KB."
                    """ },

            new() { Id="streaming-optimize-obs-for-gpu", Title="Set OBS to use dedicated GPU",
                Description="Forces OBS Studio to run on your discrete GPU instead of integrated graphics.",
                Keywords=["obs gpu encoding", "hardware encoding", "obs gpu", "nvenc", "amd encoding"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Open Graphics settings", Instruction="Graphics settings will open.", Script="Start-Process ms-settings:display-advancedgraphics" },
                    new() { Title="Find OBS Studio",        Instruction="Click 'Browse'. Navigate to C:\\Program Files\\obs-studio\\bin\\64bit\\ and select obs64.exe." },
                    new() { Title="Set High Performance",   Instruction="Click OBS Studio in the list â†’ Options â†’ select 'High Performance' (your dedicated GPU) â†’ Save." },
                    new() { Title="For NVIDIA users only",  Instruction="Open NVIDIA Control Panel â†’ Manage 3D Settings â†’ Program Settings â†’ Add obs64.exe â†’ set 'Preferred graphics processor' to your NVIDIA GPU â†’ Apply." }
                ]},

            new() { Id="streaming-network-adapter-tune", Title="Tune network adapter for streaming",
                Description="Sets network adapter interrupt moderation and offloading for optimal streaming performance.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["network for streaming", "adapter settings streaming", "streaming network optimize"],
                Script="""
                    $adapters = Get-NetAdapter | Where-Object {$_.Status -eq 'Up'}
                    foreach ($a in $adapters) {
                        # Disable interrupt moderation for lower latency
                        Set-NetAdapterAdvancedProperty -Name $a.Name -DisplayName 'Interrupt Moderation' -DisplayValue 'Disabled' -EA SilentlyContinue
                        # Enable large send offload
                        Set-NetAdapterAdvancedProperty -Name $a.Name -DisplayName 'Large Send Offload V2 (IPv4)' -DisplayValue 'Enabled' -EA SilentlyContinue
                        Set-NetAdapterAdvancedProperty -Name $a.Name -DisplayName 'Large Send Offload V2 (IPv6)' -DisplayValue 'Enabled' -EA SilentlyContinue
                        # Enable checksum offload
                        Set-NetAdapterAdvancedProperty -Name $a.Name -DisplayName 'TCP Checksum Offload (IPv4)' -DisplayValue 'TX Enabled' -EA SilentlyContinue
                        Write-Output "âœ“ Tuned: $($a.Name)"
                    }
                    Write-Output "Network adapter stream tuning complete. Restart to activate."
                    """ },

            new() { Id="game-repair-steam", Title="Repair Steam game files",
                Description="Walks you through verifying and repairing a game's local files through Steam.",
                Keywords=["steam game corrupted", "steam game crashing", "verify steam files", "steam game not launching"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Open Steam Library",      Instruction="Open Steam and click 'Library' in the top navigation." },
                    new() { Title="Right-click your game",   Instruction="Right-click the game that's crashing or broken â†’ click 'Properties'." },
                    new() { Title="Verify integrity",        Instruction="Click 'Local Files' on the left â†’ click 'Verify integrity of game files'. Steam checks every file and re-downloads corrupted ones. This can take a few minutes." }
                ]},

            new() { Id="game-repair-epic", Title="Repair Epic Games files",
                Description="Walks you through verifying and repairing a game through Epic Games Launcher.",
                Keywords=["epic games broken", "epic game corrupted", "fortnite not working", "epic repair"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Open Epic Games Library", Instruction="Open the Epic Games Launcher and click 'Library'." },
                    new() { Title="Find your game",          Instruction="Find the game with issues and click the three dots (...) next to it." },
                    new() { Title="Verify",                  Instruction="Click 'Verify'. Epic will check all files and repair any that are corrupted or missing." }
                ]},

            new() { Id="game-flush-steam-cache", Title="Flush Steam download cache",
                Description="Clears corrupted Steam download data that causes stuck or looping updates.",
                Keywords=["steam update stuck", "steam slow", "clear steam download", "steam cache"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Open Steam Settings",    Instruction="In Steam: click Steam (top-left menu) â†’ Settings." },
                    new() { Title="Clear download cache",   Instruction="Click 'Downloads' in the left panel â†’ click 'Clear Download Cache'. Steam will sign you out and restart." }
                ]},

            new() { Id="game-check-directx", Title="Check DirectX and graphics diagnostics",
                Description="Opens DirectX Diagnostic Tool to verify GPU, DirectX version, and display info.",
                Type=FixType.Silent, RequiresAdmin=false,
                Script="Start-Process dxdiag.exe" },

            new() { Id="game-disable-hpet", Title="Disable HPET timer (reduce input latency)",
                Description="Disables the High Precision Event Timer â€” can reduce input latency on some systems.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["directx error", "dx11 missing", "directx not installed", "game directx error"],
                Script="""
                    bcdedit /deletevalue useplatformclock 2>&1 | Out-Null
                    bcdedit /set disabledynamictick yes 2>&1 | Out-Null
                    Write-Output "âœ“ HPET adjustments applied. Restart to take effect."
                    Write-Output "  Note: Effects vary by system. If gaming feels worse, reverse with:"
                    Write-Output "  bcdedit /deletevalue disabledynamictick"
                    """ },

            new() { Id="game-enable-resizable-bar", Title="Check Resizable BAR / SAM status",
                Description="Shows whether Resizable BAR (AMD SAM) is enabled â€” this boosts GPU performance in games.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["resizable bar", "rebar", "smart access memory", "gpu performance"],
                Script="""
                    $result = (Get-WmiObject -Class Win32_VideoController | Select-Object -First 1).Name
                    Write-Output "GPU: $result"

                    $rebar = Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' -EA SilentlyContinue
                    if ($rebar) {
                        Write-Output ""
                        Write-Output "To check Resizable BAR status:"
                        Write-Output "1. Open GPU software (NVIDIA App or AMD Software)"
                        Write-Output "2. Look for 'Resizable BAR' or 'Smart Access Memory' in GPU info"
                        Write-Output "3. If disabled: enable it in your PC's BIOS/UEFI settings under PCI Express or Advanced"
                        Write-Output ""
                        Write-Output "Resizable BAR can improve game performance by 5â€“15% on compatible systems."
                    }
                    """ },

            new() { Id="streaming-check-bitrate-settings", Title="Check optimal streaming bitrate for your connection",
                Description="Measures your actual upload speed and recommends the correct OBS/Streamlabs bitrate.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["streaming bitrate", "what bitrate should i use", "obs bitrate", "stream quality bitrate"],
                Script="""
                    Write-Output "=== Streaming Bitrate Advisor ==="
                    Write-Output ""
                    Write-Output "Measuring network adapter throughput to estimate upload bandwidth..."
                    Write-Output "(For a precise speed test, visit fast.com or speedtest.net)"
                    Write-Output ""
                    Write-Output "=== Bitrate Recommendations by Internet Speed ==="
                    Write-Output "Upload Speed  â”‚ 1080p60 Bitrate  â”‚ 720p60 Bitrate  â”‚ 720p30 Bitrate"
                    Write-Output "â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€"
                    Write-Output "4-6 Mbps      â”‚ Not recommended  â”‚ 2500-3500 kbps  â”‚ 1500-2500 kbps"
                    Write-Output "6-10 Mbps     â”‚ Not recommended  â”‚ 4000-5000 kbps  â”‚ 2500-3000 kbps"
                    Write-Output "10-20 Mbps    â”‚ 5000-6000 kbps   â”‚ 4500-5000 kbps  â”‚ 3000-3500 kbps"
                    Write-Output "20-50 Mbps    â”‚ 6000-8000 kbps   â”‚ 5000-6000 kbps  â”‚ 3500-4000 kbps"
                    Write-Output "50+ Mbps      â”‚ 8000-12000 kbps  â”‚ 6000+ kbps      â”‚ 4000+ kbps"
                    Write-Output ""
                    Write-Output "Rule: Set bitrate to no more than 75% of your upload speed."
                    Write-Output "Rule: Use NVENC/AMD hardware encoder â€” much lighter on CPU than x264."
                    Write-Output "Rule: If viewers report buffering, lower bitrate by 500 kbps."
                    """ },

            new() { Id="streaming-audio-delay-fix", Title="Fix audio/video sync in streams",
                Description="Step-by-step guide to fix audio that's out of sync with video in OBS/Streamlabs.",
                Keywords=["audio sync streaming", "audio delay obs", "audio out of sync", "streaming audio"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Identify which is delayed",
                        Instruction="Watch a recording or VOD: if audio is ahead of video, add positive audio delay. If audio is behind video, add negative delay. Note which direction." },
                    new() { Title="Apply audio delay in OBS",
                        Instruction="In OBS: click the âš™ gear icon next to your audio source (mic or desktop audio) â†’ Properties â†’ add a delay value in milliseconds. Start with Â±200ms and adjust." },
                    new() { Title="Check capture card timing",
                        Instruction="If using a capture card: its hardware introduces latency. Open capture card settings and look for 'Audio offset' or 'Sync correction' and adjust there first." },
                    new() { Title="Set all audio to same sample rate",
                        Instruction="Mismatched sample rates (44.1kHz vs 48kHz) cause drift over time. In OBS: Settings â†’ Audio â†’ set Sample Rate to 48kHz. In Windows Sound settings, set all devices to 48000 Hz." }
                ]},

            new() { Id="streaming-record-mode-fix", Title="Fix OBS recordings (not live streaming)",
                Description="Optimizes OBS recording settings for local gameplay capture â€” higher quality than streaming.",
                Keywords=["obs recording", "record gameplay", "obs record settings", "recording quality"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Switch to CRF recording mode",
                        Instruction="In OBS: Settings â†’ Output â†’ Recording. Set Output Mode to 'Advanced'. Set Encoder to NVENC or AMD. Enable 'CRF' mode and set CRF to 18â€“23. Lower = better quality, larger file." },
                    new() { Title="Set recording format to MKV",
                        Instruction="In OBS: Settings â†’ Output â†’ Recording â†’ Recording Format â†’ set to MKV. MKV protects recordings if OBS crashes mid-stream. You can remux to MP4 after via File â†’ Remux Recordings." },
                    new() { Title="Record to a separate drive",
                        Instruction="In OBS: Settings â†’ Output â†’ Recording Path â†’ choose a drive different from your OS drive. This prevents the recording write from competing with Windows disk I/O." }
                ]},

            new() { Id="game-fps-cap-tool", Title="Show current FPS and resource usage while gaming",
                Description="Opens performance overlay options to display live FPS during gameplay.",
                Keywords=["fps cap", "limit fps", "frame limiter", "rtss", "rivatuner", "v-sync"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Enable Xbox Game Bar overlay",
                        Instruction="Press Win+G while in-game to open Game Bar. Click 'Performance' to pin a real-time overlay showing FPS, CPU %, GPU %, and RAM." },
                    new() { Title="Or use Steam FPS counter",
                        Instruction="In Steam: Settings â†’ In-Game â†’ In-game FPS counter â†’ select a screen corner. This shows FPS for any Steam game without using extra resources." },
                    new() { Title="Or use NVIDIA/AMD overlays",
                        Instruction="NVIDIA: Open NVIDIA App â†’ Settings â†’ Overlay. AMD: Open AMD Software â†’ Performance â†’ Metrics. Both show GPU-specific data like VRAM usage and GPU temp in-game." }
                ]},
        ]
    };

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  7. APP ISSUES
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static FixCategory AppIssues() => new()
    {
        Id="apps", Icon="\uE7B8", Title="App Issues",
        Fixes=
        [
            new() { Id="open-task-manager", Title="Kill a frozen app",
                Description="Opens Task Manager so you can force-close any unresponsive program.",
                Type=FixType.Silent, RequiresAdmin=false,
                Script="Start-Process taskmgr" },

            new() { Id="clear-browser-cache", Title="Clear browser cache (all browsers)",
                Description="Clears cached data for Chrome, Edge, and Firefox â€” fixes slow or broken browsing.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["browser slow", "website not loading", "clear chrome cache", "clear edge cache", "browser error", "cache too big"],
                Script="""
                    $paths = @(
                        "$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Cache",
                        "$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Code Cache",
                        "$env:LOCALAPPDATA\Google\Chrome\User Data\Default\GPUCache",
                        "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Cache",
                        "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Code Cache",
                        "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\GPUCache"
                    )
                    $freed = 0L
                    foreach ($p in $paths) {
                        if (Test-Path $p) {
                            $sz = (Get-ChildItem $p -Recurse -EA SilentlyContinue | Measure-Object -Property Length -Sum -EA SilentlyContinue).Sum
                            $freed += [long]$sz
                            Remove-Item "$p\*" -Recurse -Force -EA SilentlyContinue
                        }
                    }
                    Write-Output "âœ“ Browser caches cleared â€” freed $([math]::Round($freed/1MB,1)) MB."
                    """ },

            new() { Id="reregister-store", Title="Re-register Microsoft Store",
                Description="Fixes a broken or crashing Microsoft Store by re-registering its AppX package.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["microsoft store not working", "store broken", "store won't open", "store error", "app store crashed"],
                Script="""
                    Write-Output "Re-registering Microsoft Store..."
                    Get-AppxPackage -AllUsers Microsoft.WindowsStore | ForEach-Object {
                        Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\AppXManifest.xml" -EA SilentlyContinue
                    }
                    Write-Output "âœ“ Done. Try opening the Store again."
                    """ },

            new() { Id="reregister-all-appx", Title="Re-register all built-in Windows apps",
                Description="Re-registers all inbox apps â€” fixes missing Start Menu tiles and broken apps.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["missing windows apps", "start menu apps gone", "built in apps broken", "reset inbox apps"],
                Script="""
                    Write-Output "Re-registering all Windows apps (~1 minute)..."
                    Get-AppxPackage -AllUsers | ForEach-Object {
                        try {
                            Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\AppXManifest.xml" -EA SilentlyContinue
                        } catch {}
                    }
                    Write-Output "âœ“ Done. Restart your PC to apply."
                    """ },

            new() { Id="repair-store-app", Title="Repair or reset a Store app",
                Description="Opens app settings to repair or reset a broken Store app without losing data.",
                Type=FixType.Guided, Keywords=["app not working", "app crashing", "store app broken", "reset app", "repair app"],
                Steps=[
                    new() { Title="Open Apps settings", Instruction="Apps & Features will open.", Script="Start-Process ms-settings:appsfeatures" },
                    new() { Title="Find the broken app", Instruction="Scroll to the app â†’ click it â†’ 'Advanced options'." },
                    new() { Title="Repair or Reset",     Instruction="Click 'Repair' first â€” this fixes the app without losing settings. If still broken, click 'Reset'." }
                ]},

            new() { Id="fix-dotnet-runtime", Title="Fix missing .NET runtime",
                Description="Opens the Microsoft .NET download page to install missing runtime components.",
                Keywords=["dotnet error", "net runtime missing", "application wont start", "runtime not found", "dotnet 6", "dotnet 8"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Note the version",    Instruction="Check your error â€” it says something like '.NET 6', '.NET 7', or '.NET 8'. Note the exact version number." },
                    new() { Title="Download runtime",    Instruction="The .NET download page will open.", Script="Start-Process 'https://dotnet.microsoft.com/download'" },
                    new() { Title="Install and retry",   Instruction="Install the matching runtime version, restart, then try the app again." }
                ]},

            new() { Id="fix-vcredist", Title="Fix missing Visual C++ DLL",
                Description="Installs Microsoft Visual C++ Redistributable to fix 'VCRUNTIME140.dll not found'.",
                Type=FixType.Guided, Keywords=["vcruntime missing", "dll not found", "vcredist error", "missing dll", "vc++ error"],
                Steps=[
                    new() { Title="Confirm error type",   Instruction="Your error should mention a file like 'VCRUNTIME140.dll', 'MSVCP140.dll', or similar." },
                    new() { Title="Download and install", Instruction="The Visual C++ Redistributable download will open.", Script="Start-Process 'https://aka.ms/vs/17/release/vc_redist.x64.exe'" },
                    new() { Title="Restart and retry",    Instruction="Install, restart your PC, then try the app again." }
                ]},

            new() { Id="fix-windows-search", Title="Fix Windows Search not working",
                Description="Restarts the Windows Search indexing service and rebuilds the search index.",
                Type=FixType.Guided, RequiresAdmin=true,
                Keywords=["search not working", "start menu search broken", "cortana search", "search returns nothing"], Steps=[
                    new() { Title="Restart search service", Instruction="Click 'Done' to restart the Windows Search service.", Script="""
                        Stop-Service WSearch -Force -EA SilentlyContinue
                        Start-Sleep 3
                        Start-Service WSearch -EA SilentlyContinue
                        Write-Output "âœ“ Windows Search service: $((Get-Service WSearch).Status)"
                        """ },
                    new() { Title="Rebuild search index",   Instruction="Search settings will open â€” click 'Advanced search indexer settings' â†’ Advanced â†’ Rebuild.", Script="Start-Process ms-settings:search" }
                ]},

            new() { Id="reset-file-associations", Title="Reset default file associations",
                Description="Opens Default Apps settings to fix 'how do you want to open this?' problems.",
                Type=FixType.Silent, RequiresAdmin=false,
                Script="Start-Process ms-settings:defaultapps" },

            new() { Id="list-installed-apps", Title="List all installed programs",
                Description="Shows every installed program â€” useful before repairs or uninstalls.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["what programs are installed", "see all software", "installed programs"],
                Script="""
                    Write-Output "=== Installed programs ==="
                    $keys = @(
                        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
                        'HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
                        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*'
                    )
                    $apps = $keys | ForEach-Object { Get-ItemProperty $_ -EA SilentlyContinue } |
                        Where-Object { $_.DisplayName } |
                        Select-Object DisplayName, DisplayVersion, Publisher |
                        Sort-Object DisplayName
                    $apps | ForEach-Object { Write-Output "$($_.DisplayName)  v$($_.DisplayVersion)" }
                    Write-Output ""
                    Write-Output "Total: $($apps.Count) programs"
                    """ },

            new() { Id="fix-start-menu", Title="Fix broken Start Menu",
                Description="Re-registers Start Menu components to fix a blank or unresponsive Start Menu.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["start menu broken", "start button not working", "start menu wont open", "taskbar broken"],
                Script="""
                    Write-Output "Fixing Start Menu..."
                    Get-AppxPackage -AllUsers Microsoft.Windows.ShellExperienceHost | ForEach-Object {
                        Add-AppxPackage -Register "$($_.InstallLocation)\AppXManifest.xml" -DisableDevelopmentMode -EA SilentlyContinue
                    }
                    Get-AppxPackage -AllUsers Microsoft.Windows.Cortana | ForEach-Object {
                        Add-AppxPackage -Register "$($_.InstallLocation)\AppXManifest.xml" -DisableDevelopmentMode -EA SilentlyContinue
                    }
                    Stop-Process -Name StartMenuExperienceHost -Force -EA SilentlyContinue
                    Start-Sleep 2
                    Start-Process "$env:WINDIR\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\StartMenuExperienceHost.exe" -EA SilentlyContinue
                    Write-Output "âœ“ Start Menu components refreshed."
                    """ },

            new() { Id="fix-context-menu-slow", Title="Fix slow right-click context menu",
                Description="Identifies and removes shell extensions that slow down the right-click menu.",
                Keywords=["right click slow", "context menu delay", "slow right click", "right click takes forever"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Open ShellExView suggestion",
                        Instruction="The most effective tool for this is NirSoft ShellExView (free). Search for 'ShellExView NirSoft' and download it. It shows all shell extensions and lets you disable slow ones." },
                    new() { Title="Quick registry fix",
                        Instruction="Click 'Done' to apply a quick registry fix for common slow menu causes.", Script="""
                            # Disable 'Send to' scanning
                            $path = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
                            Set-ItemProperty -Path $path -Name Start_ShowSetProgramAccessAndDefaults -Value 0 -EA SilentlyContinue
                            Write-Output "âœ“ Quick context menu optimizations applied."
                            """ },
                    new() { Title="Test the menu",
                        Instruction="Right-click your desktop. If still slow, use ShellExView to identify and disable slow third-party extensions (highlighted in pink)." }
                ]},
        ]
    };

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  8. SECURITY & PRIVACY
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static FixCategory SecurityAndPrivacy() => new()
    {
        Id="security", Icon="\uE72E", Title="Security & Privacy",
        Fixes=
        [
            new() { Id="check-defender-status", Title="Check Windows Defender status",
                Description="Verifies antivirus is active, real-time protection is on, and definitions are current.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["is antivirus on", "defender status", "virus protection", "check antivirus"],
                Script="""
                    $s = Get-MpComputerStatus -EA SilentlyContinue
                    if ($s) {
                        $avStatus  = if($s.AntivirusEnabled){"âœ“ Enabled"}else{"âœ— DISABLED"}
                        $rtStatus  = if($s.RealTimeProtectionEnabled){"âœ“ Active"}else{"âœ— INACTIVE"}
                        $defAge    = $s.AntivirusSignatureAge
                        $defStatus = if($defAge -le 3){"âœ“ Current ($defAge days old)"}elseif($defAge -le 7){"âš  Getting old ($defAge days old)"}else{"âœ— OUTDATED ($defAge days old)"}
                        Write-Output "Antivirus        : $avStatus"
                        Write-Output "Real-time prot.  : $rtStatus"
                        Write-Output "Definitions      : $defStatus"
                        Write-Output "Last quick scan  : $($s.QuickScanAge) days ago"
                        Write-Output "Last full scan   : $($s.FullScanAge) days ago"
                    } else {
                        Write-Output "âš  Could not read Defender status. It may be managed by a third-party AV."
                    }
                    """ },

            new() { Id="quick-virus-scan", Title="Run quick virus scan",
                Description="Triggers a Windows Defender quick scan right now.",
                Type=FixType.Silent, RequiresAdmin=true,
                Script="""
                    Write-Output "Starting Windows Defender quick scan..."
                    Start-MpScan -ScanType QuickScan
                    Write-Output "âœ“ Scan initiated. Check Windows Security for results."
                    """ },

            new() { Id="full-virus-scan", Title="Run full virus scan",
                Description="Triggers a full Windows Defender scan of all drives. Takes 15â€“60 minutes.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["full scan", "deep virus scan", "thorough malware scan"],
                Script="""
                    Write-Output "Starting full scan â€” this will take 15-60 minutes..."
                    Start-MpScan -ScanType FullScan
                    Write-Output "âœ“ Full scan initiated. Check Windows Security for progress and results."
                    """ },

            new() { Id="update-virus-definitions", Title="Update virus definitions",
                Description="Forces Windows Defender to download the latest signature updates immediately.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["update antivirus", "virus definitions old", "defender definitions update", "outdated antivirus"],
                Script="""
                    Write-Output "Updating Windows Defender definitions..."
                    Update-MpSignature
                    $v = (Get-MpComputerStatus).AntivirusSignatureVersion
                    Write-Output "âœ“ Definitions updated. Version: $v"
                    """ },

            new() { Id="check-firewall", Title="Check Windows Firewall status",
                Description="Shows firewall state for all three network profiles (domain, private, public).",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["firewall on", "is firewall enabled", "check firewall status", "firewall settings"],
                Script="""
                    Get-NetFirewallProfile | ForEach-Object {
                        $status = if($_.Enabled){"âœ“ ENABLED"}else{"âœ— DISABLED"}
                        Write-Output "$($_.Name) profile: $status"
                    }
                    """ },

            new() { Id="list-startup-programs", Title="List all startup programs",
                Description="Shows every program that runs at startup, including registry entries.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["see startup programs", "what starts with windows", "auto-start programs", "slow startup programs"],
                Script="""
                    Write-Output "=== Startup programs ==="
                    Get-CimInstance Win32_StartupCommand | ForEach-Object {
                        Write-Output "Name    : $($_.Name)"
                        Write-Output "Command : $($_.Command)"
                        Write-Output "User    : $($_.User)"
                        Write-Output "---"
                    }
                    """ },

            new() { Id="list-user-accounts", Title="List all user accounts",
                Description="Shows all local user accounts â€” helps spot unauthorized accounts.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["see user accounts", "find accounts", "user list", "unknown accounts"],
                Script="""
                    Get-LocalUser | ForEach-Object {
                        $status = if($_.Enabled){"Active"}else{"Disabled"}
                        Write-Output "$($_.Name)  [$status]  Last logon: $($_.LastLogon)"
                    }
                    """ },

            new() { Id="check-shared-folders", Title="List network shared folders",
                Description="Shows all folders shared on the network â€” useful for privacy and security audits.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["shared folders", "network shares", "what is shared", "file sharing", "shared drives"],
                Script="""
                    Write-Output "=== Network shares (non-system) ==="
                    Get-SmbShare | Where-Object { $_.Name -notmatch '^\w+\$' } | ForEach-Object {
                        Write-Output "Share: $($_.Name)  Path: $($_.Path)  Description: $($_.Description)"
                    }
                    """ },

            new() { Id="reduce-telemetry", Title="Set Windows telemetry to minimum",
                Description="Sets data collection to the minimum 'Security' level to reduce Microsoft telemetry.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["disable telemetry", "privacy settings", "stop data collection", "microsoft data", "privacy tweak"],
                Script="""
                    $path = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection'
                    if (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
                    Set-ItemProperty -Path $path -Name AllowTelemetry -Value 0 -Type DWord

                    # Disable DiagTrack (Connected User Experiences and Telemetry)
                    Stop-Service DiagTrack -Force -EA SilentlyContinue
                    Set-Service  DiagTrack -StartupType Disabled -EA SilentlyContinue

                    # Disable WAP push
                    Stop-Service dmwappushservice -Force -EA SilentlyContinue
                    Set-Service  dmwappushservice -StartupType Disabled -EA SilentlyContinue

                    Write-Output "âœ“ Telemetry set to minimum. Restart to apply."
                    """ },

            new() { Id="disable-remote-access", Title="Disable Remote Desktop & Remote Assistance",
                Description="Disables RDP and Remote Assistance â€” reduces attack surface if you don't use them.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["disable remote desktop", "turn off remote access", "rdp off", "stop remote access"],
                Script="""
                    # Disable Remote Desktop
                    Set-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Control\Terminal Server' -Name fDenyTSConnections -Value 1
                    # Disable Remote Assistance
                    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Remote Assistance' -Name fAllowToGetHelp -Value 0 -EA SilentlyContinue
                    # Update firewall
                    netsh advfirewall firewall set rule group='remote desktop' new enable=No 2>&1 | Out-Null
                    Write-Output "âœ“ Remote Desktop and Remote Assistance disabled."
                    """ },

            new() { Id="check-bitlocker", Title="Check BitLocker encryption status",
                Description="Shows whether your drives are encrypted with BitLocker.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["is drive encrypted", "bitlocker status", "encryption check", "drive encrypted"],
                Script="manage-bde -status C: 2>&1 | Select-String 'Protection Status|Percentage|Conversion Status'" },
        ]
    };
}
