using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;

namespace HelpDesk.Infrastructure.Fixes;

public sealed partial class FixCatalogService
{
    // ══════════════════════════════════════════════════════════════════════════
    //  WINDOWS TWEAKS & CUSTOMIZATION
    // ══════════════════════════════════════════════════════════════════════════
    private static FixCategory WindowsTweaksAndCustomization() => new()
    {
        Id="windows-tweaks", Icon="\uE70F", Title="Windows Tweaks",
        Fixes=
        [
            new() { Id="disable-ads-tips", Title="Disable ads & tips in Windows",
                Description="Disables Windows tips, Start menu suggestions, lock screen ads, and notification ads.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["windows ads","remove ads","stop windows suggestions","tips off","ads in start menu","lock screen ads"],
                Script="""
                    $path = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"
                    if (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
                    $tweaks = @{
                        "SystemPaneSuggestionsEnabled" = 0
                        "SubscribedContent-338388Enabled" = 0   # Start suggestions
                        "SubscribedContent-338389Enabled" = 0   # Lock screen tips
                        "SubscribedContent-353694Enabled" = 0   # Timeline suggestions
                        "SubscribedContent-353696Enabled" = 0   # Settings suggestions
                        "SoftLandingEnabled" = 0                # Tips
                        "RotatingLockScreenOverlayEnabled" = 0
                        "SilentInstalledAppsEnabled" = 0        # Auto-installed apps
                        "OemPreInstalledAppsEnabled" = 0
                    }
                    foreach ($k in $tweaks.Keys) {
                        Set-ItemProperty -Path $path -Name $k -Value $tweaks[$k] -Type DWord -EA SilentlyContinue
                    }
                    # Disable advertising ID
                    $adPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo"
                    if (!(Test-Path $adPath)) { New-Item -Path $adPath -Force | Out-Null }
                    Set-ItemProperty -Path $adPath -Name Enabled -Value 0 -Type DWord
                    Write-Output "✓ Windows ads, tips, and lock screen suggestions disabled."
                    """ },

            new() { Id="disable-advertising-id", Title="Disable advertising tracking ID",
                Description="Disables the unique advertising ID used to track you across apps.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["advertising id","app tracking","stop app tracking","privacy advertising"],
                Script="""
                    $path = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo"
                    if (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
                    Set-ItemProperty -Path $path -Name Enabled -Value 0 -Type DWord
                    Write-Output "✓ Advertising ID disabled."
                    """ },

            new() { Id="fix-right-click-menu", Title="Restore classic right-click menu (Win 11)",
                Description="Restores the full right-click context menu in Windows 11 without clicking 'Show more options'.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["right click menu","context menu windows 11","show more options","restore right click","old context menu"],
                Script="""
                    $path = "HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"
                    if (!(Test-Path $path)) {
                        New-Item -Path $path -Force | Out-Null
                        Set-Item -Path $path -Value "" -EA SilentlyContinue
                    }
                    Stop-Process -Name explorer -Force -EA SilentlyContinue
                    Start-Sleep 1
                    Start-Process explorer
                    Write-Output "✓ Classic right-click menu restored. To undo, delete the registry key and restart Explorer."
                    """ },

            new() { Id="fix-taskbar-restart", Title="Restart Windows Explorer (fix taskbar)",
                Description="Restarts Explorer to fix a frozen taskbar, Start menu, or file explorer.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["taskbar frozen","taskbar not working","start menu frozen","explorer crashed","file explorer not responding","taskbar unresponsive"],
                Script="""
                    Write-Output "Restarting Windows Explorer..."
                    Stop-Process -Name explorer -Force -EA SilentlyContinue
                    Start-Sleep 2
                    Start-Process explorer
                    Write-Output "✓ Explorer restarted. Taskbar and Start menu should be responsive."
                    """ },

            new() { Id="empty-recycle-bin", Title="Empty Recycle Bin (all drives)",
                Description="Empties the Recycle Bin on all drives to reclaim disk space.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["recycle bin full","empty trash","recycle bin space","delete recycle bin"],
                Script="""
                    $before = (Get-ChildItem -Path "C:\`$Recycle.Bin" -Recurse -Force -EA SilentlyContinue | Measure-Object -Property Length -Sum -EA SilentlyContinue).Sum
                    Clear-RecycleBin -Force -EA SilentlyContinue
                    Write-Output "✓ Recycle Bin emptied ($([math]::Round($before/1MB,1)) MB freed)."
                    """ },

            new() { Id="fix-windows-hello-signin", Title="Fix Windows Hello PIN / fingerprint",
                Description="Opens Windows Hello settings to reset or re-enroll your sign-in options.",
                Type=FixType.Guided, Keywords=["windows hello","pin not working","fingerprint not working","face recognition broken","sign in broken"],
                Steps=[
                    new() { Title="Open Sign-in options",  Instruction="Settings will open on Sign-in options.", Script="Start-Process ms-settings:signinoptions" },
                    new() { Title="Remove PIN",             Instruction="Click 'Windows Hello PIN' → 'Remove' → confirm removal." },
                    new() { Title="Re-add PIN",             Instruction="Click 'Windows Hello PIN' → 'Set up' → follow the wizard to create a new PIN." }
                ]},

            new() { Id="disable-fast-startup", Title="Disable Fast Startup",
                Description="Disables Fast Startup — fixes issues where PC doesn't properly shut down or USB devices lose settings.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["fast startup","shutdown issues","pc not fully shutting down","usb reset on boot","hibernate boot"],
                Script="""
                    powercfg /hibernate off 2>&1 | Out-Null
                    $path = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power"
                    Set-ItemProperty -Path $path -Name HiberbootEnabled -Value 0 -Type DWord -EA SilentlyContinue
                    Write-Output "✓ Fast Startup disabled. PC will now perform a full shutdown."
                    """ },

            new() { Id="fix-explorer-settings", Title="Fix Explorer folder view settings",
                Description="Resets Explorer to show file extensions, hidden files, and fix the view settings.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["show file extensions","show hidden files","folder options","explorer settings","can't see file type"],
                Script="""
                    $path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
                    Set-ItemProperty -Path $path -Name HideFileExt          -Value 0 -Type DWord  # Show extensions
                    Set-ItemProperty -Path $path -Name Hidden                -Value 1 -Type DWord  # Show hidden files
                    Set-ItemProperty -Path $path -Name ShowSuperHidden       -Value 1 -Type DWord  # Show system files
                    Set-ItemProperty -Path $path -Name NavPaneExpandToCurrentFolder -Value 1 -Type DWord
                    Set-ItemProperty -Path $path -Name NavPaneShowAllFolders  -Value 0 -Type DWord
                    Stop-Process -Name explorer -Force -EA SilentlyContinue
                    Start-Sleep 1
                    Start-Process explorer
                    Write-Output "✓ Explorer now shows: file extensions, hidden files, system files."
                    """ },

            new() { Id="fix-dpi-scaling", Title="Fix DPI scaling for specific app",
                Description="Walks through overriding DPI scaling for a specific blurry application.",
                Type=FixType.Guided, Keywords=["blurry app","app looks blurry","dpi scaling","high dpi issue","program blurry on 4k"],
                Steps=[
                    new() { Title="Find the executable",  Instruction="Right-click the blurry app's shortcut → 'Open file location' to find its .exe file." },
                    new() { Title="Open Properties",       Instruction="Right-click the .exe file → Properties → Compatibility tab." },
                    new() { Title="Override DPI",          Instruction="Click 'Change high DPI settings' → check 'Override high DPI scaling behavior' → set dropdown to 'Application' → OK." }
                ]},

            new() { Id="fix-screen-tearing", Title="Fix screen tearing",
                Description="Configures VSync and NVIDIA/AMD settings to eliminate screen tearing.",
                Type=FixType.Guided, Keywords=["screen tearing","tearing display","fps tearing","game tearing","monitor tearing"],
                Steps=[
                    new() { Title="Enable VSync globally",  Instruction="For NVIDIA: open NVIDIA Control Panel → Manage 3D settings → Vertical sync → Force on.\nFor AMD: open Radeon Software → Gaming → Global Settings → Wait for Vertical Refresh → Always on." },
                    new() { Title="Or enable FreeSync/GSync", Instruction="If your monitor supports it: Settings → Display → Advanced display → Toggle on G-Sync or FreeSync." },
                    new() { Title="Check refresh rate",     Instruction="Settings → System → Display → Advanced display settings → make sure refresh rate is at maximum for your monitor." }
                ]},

            new() { Id="fix-packet-loss", Title="Diagnose and fix packet loss",
                Description="Runs a traceroute and packet loss test to identify where your connection is dropping.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["packet loss","connection dropping","game lag spikes","ping spikes","unstable internet"],
                Script="""
                    Write-Output "=== Packet loss test (Google DNS, 20 pings) ==="
                    $results = Test-Connection -ComputerName "8.8.8.8" -Count 20 -EA SilentlyContinue
                    $sent    = 20
                    $recv    = ($results | Where-Object {$_.ResponseTime -ne $null}).Count
                    $loss    = [math]::Round(100 * ($sent - $recv) / $sent, 1)
                    $avg     = if ($recv -gt 0) { [math]::Round(($results | Where-Object {$_.ResponseTime -ne $null} | Measure-Object -Property ResponseTime -Average).Average, 1) } else { 0 }
                    $max     = if ($recv -gt 0) { ($results | Where-Object {$_.ResponseTime -ne $null} | Measure-Object -Property ResponseTime -Maximum).Maximum } else { 0 }
                    Write-Output "Sent: $sent  Received: $recv  Lost: $($sent-$recv) ($loss%)"
                    Write-Output "Avg latency: ${avg}ms   Max: ${max}ms"
                    if ($loss -gt 5) {
                        Write-Output ""
                        Write-Output "✗ Significant packet loss detected ($loss%)."
                        Write-Output "  Try: run 'Full network stack reset', disable USB selective suspend,"
                        Write-Output "       or connect via ethernet instead of Wi-Fi."
                    } else {
                        Write-Output "✓ Packet loss is acceptable."
                    }
                    """ },

            new() { Id="enable-wake-on-lan", Title="Enable Wake-on-LAN",
                Description="Enables Wake-on-LAN in the network adapter settings so you can wake the PC remotely.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["wake on lan","wol","remote wake","wake pc remotely","network wake"],
                Script="""
                    $adapters = Get-NetAdapter | Where-Object {$_.Status -eq 'Up' -and $_.InterfaceDescription -notmatch 'Virtual|Hyper-V'}
                    foreach ($a in $adapters) {
                        $devPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}"
                        $keys = Get-ChildItem $devPath -EA SilentlyContinue
                        foreach ($key in $keys) {
                            $net = (Get-ItemProperty -Path $key.PSPath -Name NetCfgInstanceId -EA SilentlyContinue).NetCfgInstanceId
                            if ($net -eq $a.InterfaceGuid) {
                                Set-ItemProperty -Path $key.PSPath -Name "*WakeOnMagicPacket" -Value 1 -Type String -EA SilentlyContinue
                                Set-ItemProperty -Path $key.PSPath -Name "*WakeOnPattern"     -Value 1 -Type String -EA SilentlyContinue
                                Set-ItemProperty -Path $key.PSPath -Name "WakeOnMagicPacket"  -Value 1 -Type DWord  -EA SilentlyContinue
                            }
                        }
                    }
                    Write-Output "✓ Wake-on-LAN enabled. Also enable WoL in your BIOS/UEFI settings."
                    """ },

            new() { Id="fix-ethernet-autoneg", Title="Fix Ethernet auto-negotiation issues",
                Description="Forces Ethernet to 1Gbps full-duplex — fixes slow speeds and half-duplex fallback.",
                Type=FixType.Guided, Keywords=["ethernet slow","ethernet not 1gbps","ethernet speed issue","gigabit not working","1000mbps not connecting"],
                Steps=[
                    new() { Title="Open adapter settings",  Instruction="Device Manager will open.", Script="devmgmt.msc" },
                    new() { Title="Open Properties",         Instruction="Expand 'Network adapters', right-click your Ethernet adapter → Properties → Advanced tab." },
                    new() { Title="Set speed",               Instruction="Find 'Speed & Duplex' in the property list. Change from 'Auto Negotiation' to '1.0 Gbps Full Duplex'. Click OK." }
                ]},

            new() { Id="activate-windows", Title="Activate Windows",
                Description="Opens the Windows activation page to enter or verify your product key.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["windows not activated","activate windows","product key","windows watermark","windows activation error"],
                Script="""
                    $status = cscript //Nologo "$env:WINDIR\system32\slmgr.vbs" /xpr 2>&1
                    Write-Output $status
                    Write-Output ""
                    Start-Process ms-settings:activation
                    """ },

            new() { Id="dism-component-cleanup", Title="DISM component store cleanup",
                Description="Cleans up superseded components from the Windows component store — frees several GBs.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["dism cleanup","component store large","winsxs folder large","free up windows space","c drive space"],
                Script="""
                    Write-Output "Analyzing component store..."
                    DISM /Online /Cleanup-Image /AnalyzeComponentStore
                    Write-Output ""
                    Write-Output "Running cleanup (this may take 5–15 minutes)..."
                    DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase
                    Write-Output "✓ Component store cleaned. Restart recommended."
                    """ },

            new() { Id="fix-stuck-windows-update", Title="Fix a stuck Windows Update",
                Description="Forces a stuck update to restart — stops the update services, renames the cache folder, and restarts.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["windows update stuck","update stuck 0%","windows update frozen","update not installing","update hanging"],
                Script="""
                    Write-Output "Resetting stuck Windows Update..."
                    foreach ($s in @('wuauserv','bits','cryptSvc','TrustedInstaller')) {
                        Stop-Service $s -Force -EA SilentlyContinue
                    }
                    $sdPath = 'C:\Windows\SoftwareDistribution'
                    $bakPath = "$sdPath.bak_$(Get-Date -Format 'yyyyMMddHHmm')"
                    if (Test-Path $sdPath) {
                        Rename-Item $sdPath $bakPath -EA SilentlyContinue
                        Write-Output "  Moved SoftwareDistribution to: $bakPath"
                    }
                    foreach ($s in @('cryptSvc','bits','wuauserv')) {
                        Start-Service $s -EA SilentlyContinue
                    }
                    Write-Output "✓ Windows Update reset. Open Windows Update and check for updates again."
                    """ },

            new() { Id="disable-game-bar-notifications", Title="Disable Game Bar notifications only",
                Description="Silences Game Bar popups without disabling Game Mode or game recording.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["game bar popup","xbox game bar notification","disable game bar popup","game bar annoying"],
                Script="""
                    $path = "HKCU:\SOFTWARE\Microsoft\GameBar"
                    if (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
                    Set-ItemProperty -Path $path -Name ShowStartupPanel -Value 0 -Type DWord -EA SilentlyContinue
                    Set-ItemProperty -Path $path -Name UseNexusForGameBarEnabled -Value 0 -Type DWord -EA SilentlyContinue
                    Write-Output "✓ Game Bar startup notification disabled."
                    Write-Output "  Game Mode and Game DVR are still active."
                    """ },

            new() { Id="clear-edge-cache", Title="Clear Microsoft Edge cache & data",
                Description="Clears Edge cache, cookies, and history to fix slow browsing or login issues.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["edge slow","edge not loading","edge cache","clear edge","edge memory high","browser slow edge"],
                Script="""
                    Get-Process msedge -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
                    Start-Sleep 2
                    $cachePaths = @(
                        "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Cache",
                        "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Code Cache",
                        "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\GPUCache"
                    )
                    $freed = 0L
                    foreach ($p in $cachePaths) {
                        if (Test-Path $p) {
                            $sz = (Get-ChildItem $p -Recurse -EA SilentlyContinue | Measure-Object -Property Length -Sum -EA SilentlyContinue).Sum
                            $freed += [long]$sz
                            Get-ChildItem $p -Recurse -Force -EA SilentlyContinue | Remove-Item -Force -Recurse -EA SilentlyContinue
                        }
                    }
                    Write-Output "✓ Cleared $([math]::Round($freed/1MB,1)) MB of Edge cache. Restart Edge."
                    """ },

            new() { Id="clear-browser-cache-all", Title="Clear all browser caches",
                Description="Clears cache for Chrome, Firefox, and Edge in one pass.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["clear browser cache","browser slow","chrome cache","firefox cache","edge cache","browser not loading"],
                Script="""
                    Get-Process chrome,firefox,msedge -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
                    Start-Sleep 2
                    $paths = @(
                        "$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Cache",
                        "$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Code Cache",
                        "$env:LOCALAPPDATA\Mozilla\Firefox\Profiles\*\cache2",
                        "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Cache",
                        "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Code Cache"
                    )
                    $freed = 0L
                    foreach ($p in $paths) {
                        foreach ($resolved in (Resolve-Path $p -EA SilentlyContinue)) {
                            if (Test-Path $resolved) {
                                $sz = (Get-ChildItem $resolved -Recurse -EA SilentlyContinue | Measure-Object -Property Length -Sum -EA SilentlyContinue).Sum
                                $freed += [long]$sz
                                Get-ChildItem $resolved -Recurse -Force -EA SilentlyContinue | Remove-Item -Force -Recurse -EA SilentlyContinue
                            }
                        }
                    }
                    Write-Output "✓ Cleared $([math]::Round($freed/1MB,1)) MB of browser caches."
                    """ },

            new() { Id="fix-slow-file-copy", Title="Fix slow file copy speed",
                Description="Disables Remote Differential Compression (RDC) and Large Send Offload which can throttle file transfers.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["slow copy","file transfer slow","copying files slow","usb transfer slow","slow file move"],
                Script="""
                    # Disable Remote Differential Compression (causes slow network copies)
                    Disable-WindowsOptionalFeature -Online -FeatureName MSRDC-Infrastructure -NoRestart -EA SilentlyContinue | Out-Null
                    # Disable Large Send Offload
                    $adapters = Get-NetAdapter | Where-Object {$_.Status -eq 'Up'}
                    foreach ($a in $adapters) {
                        Disable-NetAdapterLso -Name $a.Name -EA SilentlyContinue
                    }
                    Write-Output "✓ RDC disabled, LSO disabled. File transfers should be faster."
                    Write-Output "  Also ensure your USB port is USB 3.0+ (blue port) for fast transfers."
                    """ },

            new() { Id="fix-dll-errors", Title="Fix missing DLL errors",
                Description="Runs SFC and re-registers common system DLLs — fixes 'DLL not found' app errors.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["dll error","dll not found","missing dll","msvcrt dll","vcruntime dll","program won't start dll"],
                Script="""
                    Write-Output "Scanning for DLL issues with SFC..."
                    sfc /scannow
                    Write-Output ""
                    Write-Output "Re-registering common runtime DLLs..."
                    $dlls = @('msvcrt.dll','msvcp140.dll','vcruntime140.dll','ucrtbase.dll','d3dx9_43.dll','xinput1_3.dll')
                    foreach ($dll in $dlls) {
                        $path = "$env:WINDIR\System32\$dll"
                        if (Test-Path $path) {
                            regsvr32.exe /s $path 2>&1 | Out-Null
                        }
                    }
                    Write-Output "✓ SFC scan and DLL re-registration complete."
                    Write-Output "  If errors persist, try installing Visual C++ Redistributables from Microsoft."
                    """ },

            new() { Id="optimize-ssd", Title="Optimize SSD (TRIM + defrag-off)",
                Description="Ensures TRIM is enabled and auto-defragmentation is disabled for SSDs.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["ssd optimization","trim ssd","ssd slow","optimize solid state drive"],
                Script="""
                    Write-Output "Checking SSD optimization settings..."
                    # Enable TRIM
                    $trim = fsutil behavior query DisableDeleteNotify
                    Write-Output "TRIM status: $trim"
                    if ($trim -match "= 1") {
                        fsutil behavior set DisableDeleteNotify 0
                        Write-Output "✓ TRIM enabled."
                    } else {
                        Write-Output "✓ TRIM is already enabled."
                    }
                    # Disable scheduled defrag for SSDs via Task Scheduler
                    Disable-ScheduledTask -TaskName "Microsoft\Windows\Defrag\ScheduledDefrag" -EA SilentlyContinue | Out-Null
                    Write-Output "✓ Scheduled defragmentation disabled (not needed for SSDs)."
                    # Run TRIM once
                    Write-Output "Running optimization (TRIM)..."
                    defrag C: /L /U 2>&1 | Out-Null
                    Write-Output "✓ SSD optimization complete."
                    """ },

            new() { Id="fix-duplicate-drives", Title="Fix duplicate drive letters in Explorer",
                Description="Cleans up phantom drive letters by rescanning and reassigning via diskpart.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["duplicate drives","extra drives explorer","ghost drive","phantom drive letter","drive appearing twice"],
                Script="""
                    Write-Output "Rescanning disk configuration..."
                    "rescan`nexit" | diskpart
                    Write-Output ""
                    Write-Output "Current volumes:"
                    Get-Volume | Where-Object {$_.DriveLetter} | ForEach-Object {
                        Write-Output "  $($_.DriveLetter): — $($_.FileSystemLabel) — $([math]::Round($_.Size/1GB,1)) GB — Type: $($_.DriveType)"
                    }
                    Write-Output ""
                    Write-Output "✓ Disk rescan complete. Restart Explorer if duplicates persist."
                    Stop-Process -Name explorer -Force -EA SilentlyContinue
                    Start-Sleep 1
                    Start-Process explorer
                    """ },

            new() { Id="manage-startup-programs", Title="Audit startup programs",
                Description="Lists all startup programs with their registry location — identify and disable slow boot culprits.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["startup programs list","what starts on boot","slow startup","too many startup apps","boot audit"],
                Script="""
                    Write-Output "=== Startup programs ==="
                    $paths = @(
                        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
                    )
                    $total = 0
                    foreach ($p in $paths) {
                        if (Test-Path $p) {
                            $items = Get-ItemProperty -Path $p -EA SilentlyContinue
                            $items.PSObject.Properties | Where-Object {$_.Name -notmatch '^PS'} | ForEach-Object {
                                Write-Output "[$($p -replace 'HKLM:\\|HKCU:\\','')] $($_.Name)"
                                Write-Output "  Path: $($_.Value)"
                                $total++
                            }
                        }
                    }
                    Write-Output ""
                    Write-Output "Total startup entries: $total"
                    if ($total -gt 10) {
                        Write-Output "  WARNING: More than 10 startup items may slow boot time."
                        Write-Output "  Use Task Manager (Startup tab) to disable unneeded items."
                    }
                    """ },

            new() { Id="check-defender-history", Title="View Defender threat history",
                Description="Shows recent threats detected by Windows Defender.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["defender history","virus found","malware detected","defender threats","security history"],
                Script="""
                    Write-Output "=== Windows Defender recent detections ==="
                    try {
                        Get-MpThreatDetection -EA SilentlyContinue | Sort-Object InitialDetectionTime -Descending | Select-Object -First 20 |
                        ForEach-Object {
                            Write-Output "$($_.InitialDetectionTime.ToString('yyyy-MM-dd HH:mm'))  $($_.ThreatName)  — Action: $($_.ActionSuccess)"
                        }
                    } catch {
                        Write-Output "Could not retrieve threat history. Run as administrator."
                    }
                    """ },

            new() { Id="check-ssd-smart", Title="Check SSD / HDD SMART health",
                Description="Reads SMART health data from all physical disks to check for early failure signs.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["ssd health","hard drive health","smart check","disk failing","disk smart","hdd health"],
                Script="""
                    Write-Output "=== Physical disk health (SMART) ==="
                    try {
                        Get-PhysicalDisk | ForEach-Object {
                            Write-Output "Disk  : $($_.FriendlyName)"
                            Write-Output "  Size  : $([math]::Round($_.Size/1GB)) GB"
                            Write-Output "  Media : $($_.MediaType)"
                            Write-Output "  Health: $($_.HealthStatus)"
                            Write-Output "  Usage : $($_.Usage)"
                            Write-Output "  Hours : $($_.SpindleSpeed)"
                            Write-Output "---"
                        }
                    } catch {
                        Write-Output "Unable to read SMART data. Ensure Storage Management is available."
                    }
                    """ },

            new() { Id="fix-onedrive", Title="Fix OneDrive sync issues",
                Description="Resets OneDrive to fix sync errors, stuck files, and connection problems.",
                Type=FixType.Guided, Keywords=["onedrive sync","onedrive not syncing","onedrive error","onedrive stuck","cloud sync broken"],
                Steps=[
                    new() { Title="Reset OneDrive",     Instruction="OneDrive will be reset (this doesn't delete your files).", Script="""& "$env:LOCALAPPDATA\Microsoft\OneDrive\onedrive.exe" /reset""" },
                    new() { Title="Wait 2 minutes",     Instruction="Wait for OneDrive to restart automatically. If it doesn't start, open it manually from the Start menu." },
                    new() { Title="Check sync status",  Instruction="Click the OneDrive cloud icon in the taskbar. If files are still stuck, right-click → 'View sync issues' for details." }
                ]},
        ]
    };
}
