using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;

namespace HelpDesk.Infrastructure.Fixes;

public sealed partial class FixCatalogService
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  DEVICES & USB
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static FixCategory DevicesAndUsb() => new()
    {
        Id="devices-usb", Icon="\uE88E", Title="Devices & USB",
        Fixes=
        [
            new() { Id="fix-webcam", Title="Fix webcam not detected",
                Description="Checks if webcam is enabled in Device Manager and Privacy settings.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["webcam not working","camera not detected","video call no camera","teams camera broken","zoom camera"],
                Script="""
                    # Check Device Manager for camera
                    $cams = Get-PnpDevice -Class Camera,Image -EA SilentlyContinue
                    if ($cams) {
                        Write-Output "=== Camera devices found ==="
                        $cams | ForEach-Object {
                            Write-Output "$($_.FriendlyName) â€” Status: $($_.Status)"
                        }
                    } else {
                        Write-Output "No camera devices found in Device Manager."
                    }
                    # Check privacy setting
                    $camPriv = Get-ItemPropertyValue -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam" -Name "Value" -EA SilentlyContinue
                    Write-Output ""
                    Write-Output "Camera privacy setting: $camPriv"
                    if ($camPriv -eq 'Deny') {
                        Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam" -Name "Value" -Value "Allow" -EA SilentlyContinue
                        Write-Output "âœ“ Camera access re-enabled."
                    } else {
                        Write-Output "  Camera access is currently: $camPriv"
                        Write-Output "  To enable apps: Settings â†’ Privacy â†’ Camera â†’ Allow apps to access your camera."
                    }
                    """ },

            new() { Id="fix-microphone", Title="Fix microphone not working",
                Description="Checks mic privacy settings and re-enables access for apps.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["microphone not working","mic not detected","no microphone","teams mic broken","zoom mic","voice chat broken"],
                Script="""
                    $mics = Get-WmiObject -Class Win32_SoundDevice -EA SilentlyContinue | Where-Object {$_.Name -match 'Microphone|Mic|Input|Array'}
                    Write-Output "=== Microphone devices ==="
                    if ($mics) {
                        $mics | ForEach-Object { Write-Output "$($_.Name) â€” Status: $($_.Status)" }
                    } else {
                        Write-Output "No dedicated microphone found â€” check Device Manager."
                    }
                    $micPriv = Get-ItemPropertyValue -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone" -Name "Value" -EA SilentlyContinue
                    Write-Output ""
                    Write-Output "Microphone privacy: $micPriv"
                    if ($micPriv -eq 'Deny') {
                        Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone" -Name "Value" -Value "Allow" -EA SilentlyContinue
                        Write-Output "âœ“ Microphone access re-enabled."
                    } else {
                        Write-Output "  If an app can't hear you, check: Settings â†’ Privacy â†’ Microphone"
                    }
                    """ },

            new() { Id="usb-selective-suspend", Title="Disable USB selective suspend",
                Description="Prevents Windows from powering down USB ports â€” fixes devices that randomly disconnect.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["usb device disconnects","usb keeps disconnecting","mouse disconnects","keyboard disconnects","usb power management"],
                Script="""
                    # Disable USB selective suspend in current power plan
                    $guid = (powercfg /getactivescheme) -replace '.*GUID: (\S+).*','$1'
                    # USB selective suspend: subgroup 2a737441... setting 48e6b7a6...
                    powercfg /setacvalueindex $guid 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0
                    powercfg /setdcvalueindex $guid 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0
                    powercfg /setactive $guid
                    Write-Output "âœ“ USB selective suspend disabled. USB devices will stay powered."
                    """ },

            new() { Id="fix-usb-not-recognized-guided", Title="Fix 'USB device not recognized'",
                Description="Resets USB controllers to fix the 'Unknown USB Device' error.",
                Type=FixType.Guided, RequiresAdmin=true,
                Keywords=["usb not recognized","unknown usb device","usb device error","usb not working"],
                Steps=[
                    new() { Title="Disable USB controllers",
                        Instruction="Device Manager will open. Expand 'Universal Serial Bus controllers', then right-click each 'USB Root Hub' and click 'Disable device'.",
                        Script="devmgmt.msc" },
                    new() { Title="Re-enable all",
                        Instruction="Right-click each disabled USB Root Hub and click 'Enable device'. Windows will reinstall the controller drivers." },
                    new() { Title="Test",
                        Instruction="Unplug and replug your USB device to test." }
                ]},

            new() { Id="usb-mouse-polling", Title="Set USB mouse polling to 1000Hz",
                Description="Sets mouse USB polling rate to 1000Hz for smoother cursor movement in gaming.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["mouse polling rate","mouse stuttering","mouse lag","gaming mouse fix","mouse hz"],
                Script="""
                    # Enable HID priority in registry for smoother mouse
                    $path = "HKLM:\SYSTEM\CurrentControlSet\Services\mouclass\Parameters"
                    if (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
                    Set-ItemProperty -Path $path -Name MouseDataQueueSize -Value 100 -Type DWord -EA SilentlyContinue
                    # Disable USB power management on HID devices
                    Get-PnpDevice -Class HIDClass | Where-Object {$_.FriendlyName -match 'Mouse|Pointing'} | ForEach-Object {
                        $devPath = "HKLM:\SYSTEM\CurrentControlSet\Enum\$($_.DeviceID)\Device Parameters"
                        if (Test-Path $devPath) {
                            Set-ItemProperty -Path $devPath -Name EnhancedPowerManagementEnabled -Value 0 -Type DWord -EA SilentlyContinue
                        }
                    }
                    Write-Output "âœ“ Mouse HID queue size increased. Disable USB selective suspend for full effect."
                    """ },

            new() { Id="fix-bluetooth-device", Title="Restart Bluetooth service",
                Description="Restarts the Bluetooth service to fix devices that fail to pair or connect.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["bluetooth not working","bluetooth device not connecting","bluetooth pairing failed","bluetooth headphones"],
                Script="""
                    Stop-Service bthserv -Force -EA SilentlyContinue
                    Start-Sleep 2
                    Start-Service bthserv -EA SilentlyContinue
                    $status = (Get-Service bthserv -EA SilentlyContinue)?.Status ?? "Not found"
                    Write-Output "âœ“ Bluetooth service: $status"
                    Write-Output "  Try removing and re-pairing your device."
                    """ },
        ]
    };

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  WINDOWS APPS & FEATURES
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static FixCategory WindowsAppsAndFeatures() => new()
    {
        Id="windows-apps", Icon="\uE71D", Title="Windows Apps & Features",
        Fixes=
        [
            new() { Id="fix-microsoft-store", Title="Fix Microsoft Store not opening",
                Description="Clears Store cache and re-registers the app to fix launch failures.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["microsoft store not working","store won't open","windows store broken","apps won't download"],
                Script="""
                    Write-Output "Resetting Microsoft Store..."
                    wsreset.exe
                    Start-Sleep 10
                    # Re-register Store
                    Get-AppXPackage *WindowsStore* -AllUsers | ForEach-Object {
                        Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\AppXManifest.xml" -EA SilentlyContinue
                    }
                    Write-Output "âœ“ Microsoft Store cache cleared. Store should now open."
                    """ },

            new() { Id="clear-teams-cache", Title="Clear Microsoft Teams cache",
                Description="Clears Teams cache to fix crashes, freezing, and login issues.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["teams not working","teams crashing","teams slow","teams won't open","teams frozen","teams login issue"],
                Script="""
                    # Kill Teams processes
                    Get-Process *teams* -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
                    Start-Sleep 2
                    $paths = @(
                        "$env:APPDATA\Microsoft\Teams\Cache",
                        "$env:APPDATA\Microsoft\Teams\blob_storage",
                        "$env:APPDATA\Microsoft\Teams\databases",
                        "$env:APPDATA\Microsoft\Teams\GPUCache",
                        "$env:APPDATA\Microsoft\Teams\IndexedDB",
                        "$env:APPDATA\Microsoft\Teams\Local Storage",
                        "$env:APPDATA\Microsoft\Teams\tmp"
                    )
                    $freed = 0L
                    foreach ($p in $paths) {
                        if (Test-Path $p) {
                            $sz = (Get-ChildItem $p -Recurse -EA SilentlyContinue | Measure-Object -Property Length -Sum -EA SilentlyContinue).Sum
                            $freed += [long]$sz
                            Get-ChildItem $p -Recurse -Force -EA SilentlyContinue | Remove-Item -Force -Recurse -EA SilentlyContinue
                        }
                    }
                    Write-Output "âœ“ Cleared $([math]::Round($freed/1MB,1)) MB of Teams cache. Restart Teams."
                    """ },

            new() { Id="disable-discord-overlay", Title="Disable Discord in-game overlay",
                Description="Disables the Discord overlay that can cause game crashes and FPS drops.",
                Type=FixType.Guided, Keywords=["discord overlay","discord game crash","fps drops discord","discord causing lag"],
                Steps=[
                    new() { Title="Open Discord settings", Instruction="Open Discord and press Ctrl+Comma to open User Settings." },
                    new() { Title="Find Game Overlay",      Instruction="Click 'Game Overlay' in the left panel." },
                    new() { Title="Disable overlay",        Instruction="Toggle off 'Enable in-game overlay'. This prevents Discord from injecting into game processes." }
                ]},

            new() { Id="fix-xbox-game-pass", Title="Fix Xbox / Game Pass app issues",
                Description="Clears Xbox Identity Provider cache and re-registers the Gaming Services app.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["xbox app not working","game pass not working","xbox game bar broken","xbox identity provider error"],
                Script="""
                    Write-Output "Re-registering Xbox and Gaming Services apps..."
                    Get-AppxPackage *Xbox* -AllUsers | ForEach-Object {
                        Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\AppXManifest.xml" -EA SilentlyContinue
                    }
                    # Clear GamingServices cache
                    $gsPath = "$env:LOCALAPPDATA\Packages\Microsoft.GamingApp_8wekyb3d8bbwe\LocalCache"
                    if (Test-Path $gsPath) {
                        Get-ChildItem $gsPath -Recurse -Force -EA SilentlyContinue | Remove-Item -Force -Recurse -EA SilentlyContinue
                    }
                    Write-Output "âœ“ Xbox apps re-registered. Restart your PC for full effect."
                    """ },

            new() { Id="rebuild-font-cache", Title="Rebuild font cache",
                Description="Clears and rebuilds the Windows font cache â€” fixes rendering issues in apps.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["fonts not loading","font rendering broken","font cache","text looks wrong","fonts missing in app"],
                Script="""
                    Write-Output "Rebuilding font cache..."
                    Stop-Service FontCache -Force -EA SilentlyContinue
                    Stop-Service FontCache3.0.0.0 -Force -EA SilentlyContinue
                    $paths = @(
                        "$env:WINDIR\ServiceProfiles\LocalService\AppData\Local\FontCache",
                        "$env:WINDIR\ServiceProfiles\LocalService\AppData\Local\FontCache-System",
                        "$env:LOCALAPPDATA\Microsoft\Windows\FontCache"
                    )
                    foreach ($p in $paths) {
                        if (Test-Path $p) { Remove-Item "$p\*" -Force -Recurse -EA SilentlyContinue }
                    }
                    Start-Service FontCache -EA SilentlyContinue
                    Write-Output "âœ“ Font cache cleared. Fonts will rebuild on next login."
                    """ },

            new() { Id="fix-desktop-icons", Title="Fix missing/wrong desktop icons",
                Description="Refreshes the desktop icon cache to fix missing, black, or wrong icons.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["desktop icons missing","desktop icons wrong","broken desktop icons","icons all same","generic icons"],
                Script="""
                    ie4uinit.exe -show
                    Stop-Process -Name explorer -Force -EA SilentlyContinue
                    Start-Sleep 1
                    # Delete icon cache
                    $cache = "$env:LOCALAPPDATA\Microsoft\Windows\Explorer"
                    Get-ChildItem "$cache\iconcache_*.db" -EA SilentlyContinue | Remove-Item -Force -EA SilentlyContinue
                    Start-Process explorer
                    Write-Output "âœ“ Icon cache cleared and Explorer restarted. Icons will rebuild."
                    """ },

            new() { Id="rebuild-search-index", Title="Rebuild Windows search index",
                Description="Rebuilds the search index â€” fixes missing files in search results.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["windows search not finding files","search not working","search results missing","cortana search broken"],
                Script="""
                    Write-Output "Rebuilding search index (this can take 15â€“30 minutes in background)..."
                    Stop-Service WSearch -Force -EA SilentlyContinue
                    Start-Sleep 2
                    $dbPath = "$env:PROGRAMDATA\Microsoft\Search\Data\Applications\Windows"
                    if (Test-Path $dbPath) {
                        Remove-Item "$dbPath\Windows.edb" -Force -EA SilentlyContinue
                    }
                    Start-Service WSearch -EA SilentlyContinue
                    Write-Output "âœ“ Search index database reset. Indexing will rebuild in background."
                    Write-Output "  Full results will be available in 15â€“30 minutes."
                    """ },

            new() { Id="enable-hags", Title="Enable Hardware-Accelerated GPU Scheduling",
                Description="Enables HAGS (Windows 10 2004+, Nvidia/AMD required) â€” reduces GPU latency in games.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["hags","hardware gpu scheduling","reduce gpu latency","gaming tweak gpu","gpu performance"],
                Script="""
                    $path = "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers"
                    $val  = (Get-ItemProperty -Path $path -Name HwSchMode -EA SilentlyContinue).HwSchMode
                    if ($val -eq 2) {
                        Write-Output "âœ“ Hardware-Accelerated GPU Scheduling is already enabled."
                    } else {
                        Set-ItemProperty -Path $path -Name HwSchMode -Value 2 -Type DWord
                        Write-Output "âœ“ HAGS enabled. A restart is required to take effect."
                        Write-Output "  Note: Requires Windows 10 2004+ and a compatible GPU driver."
                    }
                    """ },

            new() { Id="sync-clock", Title="Sync Windows clock",
                Description="Forces an immediate internet time sync to fix clock drift and certificate errors.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["clock wrong","time wrong","date wrong","certificate error time","ssl error clock","sync time"],
                Script="""
                    Write-Output "Syncing Windows time..."
                    Stop-Service w32tm -EA SilentlyContinue
                    Start-Service w32tm -EA SilentlyContinue
                    w32tm /resync /force 2>&1
                    $time = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                    Write-Output "âœ“ Current time: $time"
                    """ },

            new() { Id="disable-cortana", Title="Disable Cortana",
                Description="Disables Cortana â€” reduces background CPU/RAM usage and data collection.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["disable cortana","cortana using ram","cortana off","stop cortana","cortana cpu"],
                Script="""
                    $path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search"
                    if (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
                    Set-ItemProperty -Path $path -Name AllowCortana -Value 0 -Type DWord
                    # Also disable via user policy
                    $upath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search"
                    if (!(Test-Path $upath)) { New-Item -Path $upath -Force | Out-Null }
                    Set-ItemProperty -Path $upath -Name BingSearchEnabled -Value 0 -Type DWord -EA SilentlyContinue
                    Set-ItemProperty -Path $upath -Name CortanaEnabled -Value 0 -Type DWord -EA SilentlyContinue
                    Write-Output "âœ“ Cortana disabled. Changes take effect after sign out/restart."
                    """ },

            new() { Id="disable-telemetry", Title="Disable Windows telemetry",
                Description="Sets telemetry to Security level (minimal) and stops the DiagTrack service.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["disable telemetry","windows spying","privacy windows","stop data collection","stop microsoft data"],
                Script="""
                    # Set telemetry to Security (0) level
                    $path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection"
                    if (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
                    Set-ItemProperty -Path $path -Name AllowTelemetry -Value 0 -Type DWord
                    # Stop and disable DiagTrack
                    Stop-Service DiagTrack -Force -EA SilentlyContinue
                    Set-Service DiagTrack -StartupType Disabled -EA SilentlyContinue
                    # Disable dmwappushsvc
                    Stop-Service dmwappushservice -Force -EA SilentlyContinue
                    Set-Service dmwappushservice -StartupType Disabled -EA SilentlyContinue
                    Write-Output "âœ“ Telemetry set to minimum. DiagTrack service disabled."
                    """ },

            new() { Id="flush-arp-cache", Title="Flush ARP cache",
                Description="Clears the ARP table â€” fixes connectivity issues after network changes or router swaps.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["arp cache","network after router change","ip conflict","duplicate ip address","network not working after change"],
                Script="""
                    $before = netsh interface ip show neighbors
                    netsh interface ip delete arpcache 2>&1 | Out-Null
                    Write-Output "âœ“ ARP cache flushed."
                    Write-Output "  This resolves IP conflicts and stale MAC-to-IP mappings."
                    """ },

            new() { Id="enable-dns-over-https", Title="Enable DNS over HTTPS (DoH)",
                Description="Enables encrypted DNS queries in Windows 11 for improved privacy.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["dns over https","doh","encrypted dns","privacy dns","secure dns"],
                Script="""
                    # Enable DoH in Windows 11 (requires Cloudflare DoH)
                    $adapters = Get-NetAdapter | Where-Object {$_.Status -eq 'Up'}
                    foreach ($a in $adapters) {
                        # Set Cloudflare DoH
                        Set-DnsClientServerAddress -InterfaceIndex $a.InterfaceIndex -ServerAddresses ("1.1.1.1","1.0.0.1") -EA SilentlyContinue
                    }
                    # Enable DoH policy
                    $path = "HKLM:\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters"
                    Set-ItemProperty -Path $path -Name EnableAutoDoh -Value 2 -Type DWord -EA SilentlyContinue
                    Write-Output "âœ“ DNS set to Cloudflare (1.1.1.1) with DoH enabled."
                    Write-Output "  Full DoH in Settings: Settings â†’ Network â†’ DNS server assignment â†’ Manual"
                    """ },
        ]
    };
}
