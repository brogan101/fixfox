using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;

namespace HelpDesk.Infrastructure.Fixes;

public sealed partial class FixCatalogService
{
    // ══════════════════════════════════════════════════════════════════════
    //  9. SYSTEM INFORMATION
    // ══════════════════════════════════════════════════════════════════════
    private static FixCategory SystemInformation() => new()
    {
        Id="sysinfo", Icon="\uE7F4", Title="System Information",
        Fixes=
        [
            new() { Id="full-system-summary", Title="Full system summary",
                Description="OS, CPU, RAM, GPU, disk, uptime, BIOS and last boot all in one view.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["system info", "pc specs", "what are my specs", "computer information", "hardware info"],
                Script="""
                    $os  = Get-CimInstance Win32_OperatingSystem
                    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
                    $gpu = Get-CimInstance Win32_VideoController | Select-Object -First 1
                    $mb  = Get-CimInstance Win32_BaseBoard | Select-Object -First 1
                    $bios = Get-CimInstance Win32_BIOS | Select-Object -First 1
                    $ram = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1)
                    $ut  = [timespan]::FromMilliseconds([Environment]::TickCount64)
                    Write-Output "=== System Summary ==="
                    Write-Output "Computer     : $env:COMPUTERNAME"
                    Write-Output "User         : $env:USERNAME"
                    Write-Output "OS           : $($os.Caption) $($os.OSArchitecture) (Build $($os.BuildNumber))"
                    Write-Output "CPU          : $($cpu.Name.Trim())"
                    Write-Output "  Cores      : $($cpu.NumberOfCores) cores / $($cpu.NumberOfLogicalProcessors) threads"
                    Write-Output "  Speed      : $([math]::Round($cpu.MaxClockSpeed/1000,2)) GHz"
                    Write-Output "RAM          : $ram GB total"
                    Write-Output "GPU          : $($gpu.Name)"
                    Write-Output "  VRAM       : $([math]::Round($gpu.AdapterRAM/1GB,1)) GB"
                    Write-Output "Motherboard  : $($mb.Manufacturer) $($mb.Product)"
                    Write-Output "BIOS         : $($bios.Manufacturer) $($bios.SMBIOSBIOSVersion)"
                    Write-Output "Uptime       : $($ut.Days)d $($ut.Hours)h $($ut.Minutes)m"
                    Write-Output "Last boot    : $($os.LastBootUpTime)"
                    """ },

            new() { Id="show-all-disk-space", Title="All drives — space and health",
                Description="Shows used/free space and health status for every attached drive.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["all drives space", "see drive usage", "storage info all drives", "check all disks"],
                Script="""
                    Get-PSDrive -PSProvider FileSystem | Where-Object {$_.Used+$_.Free -gt 0} | ForEach-Object {
                        $t = $_.Used + $_.Free
                        $p = if($t -gt 0){ [math]::Round(100*$_.Used/$t) }else{0}
                        Write-Output ("Drive {0}: {1:N1} GB used / {2:N1} GB free ({3}% full)" -f $_.Name, ($_.Used/1GB), ($_.Free/1GB), $p)
                    }
                    Write-Output ""
                    Get-PhysicalDisk | ForEach-Object {
                        Write-Output "Physical disk: $($_.FriendlyName) — Health: $($_.HealthStatus) — Media: $($_.MediaType) — Size: $([math]::Round($_.Size/1GB)) GB"
                    }
                    """ },

            new() { Id="show-event-log-errors", Title="Recent system errors (Event Log)",
                Description="Shows the last 20 critical errors from Windows Event Log — key for crash diagnosis.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["system errors", "recent errors", "event log", "what errors happened", "crash log"],
                Script="""
                    Write-Output "=== Last 20 System errors ==="
                    Get-WinEvent -FilterHashtable @{LogName='System'; Level=2} -MaxEvents 20 -EA SilentlyContinue |
                    ForEach-Object {
                        Write-Output "$($_.TimeCreated.ToString('MM/dd HH:mm'))  [$($_.ProviderName)]  $($_.Message.Split("`n")[0].Trim())"
                    }
                    """ },

            new() { Id="open-reliability-monitor", Title="Open Reliability Monitor",
                Description="Shows a timeline of crashes, errors, and app failures — the best visual crash history.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["reliability monitor", "crash history", "what changed", "stability history", "error timeline"],
                Script="Start-Process perfmon /rel" },

            new() { Id="show-boot-times", Title="Show last 5 boot times",
                Description="Shows how long your last 5 Windows startups took in seconds.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["how long does pc take to boot", "startup time", "boot speed", "measure startup"],
                Script="""
                    Write-Output "=== Recent boot times ==="
                    Get-WinEvent -ProviderName Microsoft-Windows-Diagnostics-Performance -EA SilentlyContinue |
                    Where-Object { $_.Id -eq 100 } | Select-Object -First 5 |
                    ForEach-Object {
                        $xml  = [xml]$_.ToXml()
                        $ms   = ($xml.Event.EventData.Data | Where-Object {$_.Name -eq 'BootTime'}).'#text'
                        $sec  = [math]::Round([int]$ms / 1000, 1)
                        Write-Output "$($_.TimeCreated.ToString('yyyy-MM-dd'))  Boot time: ${sec}s"
                    }
                    """ },

            new() { Id="generate-battery-report", Title="Generate battery health report",
                Description="Creates a detailed HTML battery health report and opens it in your browser.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["battery health", "battery life", "how old is my battery", "battery capacity", "battery degraded"],
                Script="""
                    $f = "$env:USERPROFILE\Desktop\BatteryReport.html"
                    powercfg /batteryreport /output $f 2>&1
                    if (Test-Path $f) {
                        Start-Process $f
                        Write-Output "✓ Battery report saved to Desktop and opened in browser."
                    } else {
                        Write-Output "No battery detected — this is a desktop PC, or power reporting is unavailable."
                    }
                    """ },

            new() { Id="export-system-report", Title="Export full system report to Desktop",
                Description="Generates a complete msinfo32 system report and saves it to your Desktop.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["export system info", "save specs", "system report", "diagnostic report"],
                Script="""
                    $f = "$env:USERPROFILE\Desktop\SystemReport_$(Get-Date -Format 'yyyyMMdd_HHmm').txt"
                    msinfo32 /report $f
                    Write-Output "✓ System report saved to Desktop: $f"
                    """ },

            new() { Id="list-running-services", Title="List non-Microsoft running services",
                Description="Shows third-party services currently running — helps spot bloatware or malware.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["what is running", "background services", "third party services", "what services are running"],
                Script="""
                    Write-Output "=== Third-party running services ==="
                    Get-WmiObject Win32_Service |
                    Where-Object { $_.State -eq 'Running' -and $_.PathName -notmatch 'system32|SysWOW64|Microsoft|Windows' } |
                    ForEach-Object { Write-Output "$($_.Name): $($_.DisplayName)" }
                    """ },
        ]
    };

    // ══════════════════════════════════════════════════════════════════════
    //  10. BLUE SCREEN & CRASHES
    // ══════════════════════════════════════════════════════════════════════
    private static FixCategory BlueScreenAndCrashes() => new()
    {
        Id="bsod", Icon="\uE814", Title="Blue Screen & Crashes",
        Fixes=
        [
            new() { Id="bsod-enable-minidumps", Title="Enable crash minidump logging",
                Description="Configures Windows to save minidump files when it crashes — required for BSOD analysis.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["enable crash dump", "bsod log", "blue screen log", "save crash info", "minidump"],
                Script="""
                    $crashPath = 'HKLM:\SYSTEM\CurrentControlSet\Control\CrashControl'
                    Set-ItemProperty -Path $crashPath -Name CrashDumpEnabled    -Value 3     # Small memory dump
                    Set-ItemProperty -Path $crashPath -Name MinidumpDir         -Value '%SystemRoot%\Minidump'
                    Set-ItemProperty -Path $crashPath -Name AutoReboot          -Value 1
                    Set-ItemProperty -Path $crashPath -Name LogEvent            -Value 1
                    Set-ItemProperty -Path $crashPath -Name Overwrite           -Value 1
                    if (!(Test-Path "$env:WINDIR\Minidump")) { New-Item -Path "$env:WINDIR\Minidump" -ItemType Directory | Out-Null }
                    Write-Output "✓ Minidump logging enabled. Next BSOD will create a file in C:\Windows\Minidump"
                    """ },

            new() { Id="bsod-read-minidumps", Title="Read recent BSOD crash dumps",
                Description="Reads any existing minidump files and shows the crash stop code and faulting module.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["read crash dump", "analyze bsod", "what caused blue screen", "bsod reason", "crash analysis"],
                Script="""
                    $dumpPath = "$env:WINDIR\Minidump"
                    $dumps = Get-ChildItem $dumpPath -Filter "*.dmp" -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 10
                    if (!$dumps) {
                        Write-Output "No minidump files found in $dumpPath"
                        Write-Output "Run 'Enable crash minidump logging' first, then wait for the next BSOD."
                    } else {
                        Write-Output "=== Recent crash dump files ==="
                        foreach ($d in $dumps) {
                            Write-Output "File : $($d.Name)"
                            Write-Output "Date : $($d.LastWriteTime)"
                            Write-Output "Size : $([math]::Round($d.Length/1KB)) KB"
                            Write-Output "---"
                        }
                        Write-Output "For deep analysis: open WinDbg Preview (Microsoft Store, free)"
                        Write-Output "Command: !analyze -v"
                    }
                    """ },

            new() { Id="bsod-check-recent-crashes", Title="Show recent BSOD events from Event Log",
                Description="Reads Windows Event Log for unexpected shutdowns and BSOD events.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["recent crashes", "bsod history", "blue screen history", "crash events"],
                Script="""
                    Write-Output "=== Unexpected shutdown / BSOD events ==="
                    $events = Get-WinEvent -FilterHashtable @{LogName='System'; Id=6008} -MaxEvents 10 -EA SilentlyContinue
                    if ($events) {
                        $events | ForEach-Object {
                            Write-Output "$($_.TimeCreated.ToString('yyyy-MM-dd HH:mm'))  $($_.Message.Split("`n")[0].Trim())"
                        }
                    } else {
                        Write-Output "✓ No unexpected shutdown events found in the last 90 days."
                    }
                    """ },

            new() { Id="bsod-common-fixes", Title="Apply common BSOD prevention fixes",
                Description="Applies the most effective known BSOD prevention fixes — update drivers, SFC, check RAM.",
                Keywords=["fix blue screen", "bsod fix", "blue screen of death help", "pc keeps crashing", "random restarts"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Check for driver problems",  Instruction="Click 'Done' to scan for problem devices.", Script="""
                        $bad = Get-PnpDevice | Where-Object {$_.Status -ne 'OK'}
                        if ($bad) { $bad | ForEach-Object { Write-Output "PROBLEM: $($_.FriendlyName) — $($_.Status)" } }
                        else { Write-Output "✓ No driver problems found." }
                        """ },
                    new() { Title="Update display driver",       Instruction="Open Device Manager to check for GPU driver updates — outdated GPU drivers cause ~30% of BSODs.", Script="devmgmt.msc" },
                    new() { Title="Run System File Checker",     Instruction="Click 'Done' to run SFC and repair any corrupted system files.", Script="Write-Output 'Running SFC (5-15 min)...'; sfc /scannow" },
                    new() { Title="Schedule RAM test",           Instruction="Click 'Done' to schedule a memory test on next restart — bad RAM is a very common BSOD cause.", Script="mdsched.exe" },
                    new() { Title="Check temperatures",          Instruction="Overheating causes random BSODs. Use the System Info tab to check CPU and GPU temps. Clean dust from the PC if temps are over 85°C." }
                ]},

            new() { Id="bsod-disable-auto-restart", Title="Show BSOD stop code (disable auto-restart)",
                Description="Stops Windows from rebooting instantly after a crash so you can read the stop code.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["pc restarts automatically", "auto restart crash", "stop automatic reboot", "dont restart on crash"],
                Script="""
                    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\CrashControl' -Name AutoReboot -Value 0
                    Write-Output "✓ Auto-restart on BSOD disabled."
                    Write-Output "  Next crash: hold power button or press Enter to restart manually."
                    Write-Output "  Re-enable: Set AutoReboot back to 1 in the same registry path."
                    """ },

            new() { Id="bsod-driver-verifier", Title="Enable Driver Verifier (advanced)",
                Description="Enables Driver Verifier to catch the exact driver causing BSODs — advanced troubleshooting.",
                Type=FixType.Guided, RequiresAdmin=true, Keywords=["driver verifier", "find bad driver", "which driver is crashing", "driver test"],
                Steps=[
                    new() { Title="⚠ Warning — read carefully",
                        Instruction="Driver Verifier stress-tests all drivers and WILL cause BSODs intentionally. Only do this if you're having unexplained random BSODs and need to find the culprit driver. Create a restore point first." },
                    new() { Title="Enable standard verification",
                        Instruction="Click 'Done' to enable standard Driver Verifier settings.", Script="""
                            verifier /standard /all
                            Write-Output "✓ Driver Verifier enabled. Restart your PC to begin — it will cause a BSOD when it finds a problem driver."
                            """ },
                    new() { Title="After the crash",
                        Instruction="Read the stop code and faulting module in the BSOD screen. Then disable Driver Verifier with: verifier /reset (in admin Command Prompt) and restart." }
                ]},
        ]
    };

    // ══════════════════════════════════════════════════════════════════════
    //  11. EMAIL & OFFICE
    // ══════════════════════════════════════════════════════════════════════
    private static FixCategory EmailAndOffice() => new()
    {
        Id="email", Icon="\uE715", Title="Email & Office",
        Fixes=
        [
            new() { Id="fix-outlook-stuck", Title="Fix Outlook stuck / not responding",
                Description="Force-quits Outlook processes and clears the temp Outlook files.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["outlook stuck", "outlook not sending", "outlook frozen", "email stuck", "outlook error"],
                Script="""
                    Write-Output "Closing Outlook and clearing temp files..."
                    Stop-Process -Name OUTLOOK -Force -EA SilentlyContinue
                    Start-Sleep 2
                    $tempPath = "$env:LOCALAPPDATA\Microsoft\Windows\Temporary Internet Files\Content.Outlook"
                    if (Test-Path $tempPath) {
                        $sz = (Get-ChildItem $tempPath -Recurse -EA SilentlyContinue | Measure-Object -Property Length -Sum -EA SilentlyContinue).Sum
                        Remove-Item "$tempPath\*" -Recurse -Force -EA SilentlyContinue
                        Write-Output "✓ Cleared $([math]::Round($sz/1MB,1)) MB of Outlook temp files."
                    }
                    Write-Output "✓ Restart Outlook to apply."
                    """ },

            new() { Id="repair-outlook-profile", Title="Repair Outlook profile",
                Description="Runs the Outlook profile repair wizard to fix corrupt email profiles.",
                Type=FixType.Guided, Keywords=["outlook profile broken", "outlook keeps crashing", "outlook won't start", "repair outlook"],
                Steps=[
                    new() { Title="Open Mail settings",       Instruction="Mail settings (Control Panel) will open.", Script=@"Start-Process 'C:\Windows\SysWOW64\control.exe' -ArgumentList 'mlcfg32.cpl'" },
                    new() { Title="Select your profile",      Instruction="Click your email account in the list → click 'Repair'." },
                    new() { Title="Follow the wizard",        Instruction="Click 'Next' through the repair wizard. Outlook will test and repair the connection to your mail server." }
                ]},

            new() { Id="fix-office-activation", Title="Fix Microsoft Office activation errors",
                Description="Runs the Office Software Protection Platform repair to fix activation failures.",
                Type=FixType.Guided, Keywords=["office not activated", "office activation error", "microsoft office license", "office product key"],
                Steps=[
                    new() { Title="Open Apps settings",  Instruction="Apps & Features will open.", Script="Start-Process ms-settings:appsfeatures" },
                    new() { Title="Find Microsoft 365",  Instruction="Search for 'Microsoft 365' or 'Microsoft Office' → click it → 'Advanced options'." },
                    new() { Title="Repair the app",      Instruction="Click 'Quick Repair' first. If still failing, run 'Online Repair' (requires internet, takes ~30 minutes)." }
                ]},

            new() { Id="rebuild-ost-file", Title="Rebuild Outlook OST/PST file",
                Description="Runs the Inbox Repair Tool (scanpst.exe) to fix corrupted Outlook data files.",
                Keywords=["outlook data file", "pst error", "ost error", "outlook email database", "rebuild outlook"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Find your data file",   Instruction="In Outlook: File → Account Settings → Account Settings → Data Files tab. Note the path to your .ost or .pst file." },
                    new() { Title="Run scanpst.exe",        Instruction="The Inbox Repair Tool will open.", Script=@"Start-Process 'C:\Program Files\Microsoft Office\root\office16\SCANPST.EXE' -EA SilentlyContinue; Start-Process 'C:\Program Files (x86)\Microsoft Office\root\office16\SCANPST.EXE' -EA SilentlyContinue" },
                    new() { Title="Repair the file",        Instruction="Click 'Browse', navigate to your .ost or .pst file → click 'Start'. If errors are found, click 'Repair'. This may take 10–60 minutes." }
                ]},

            new() { Id="clear-office-cache", Title="Clear Microsoft Office cache",
                Description="Clears the Office document and thumbnail cache — fixes 'file in use' and sync errors.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["office slow", "clear office temp files", "office cache", "reset office"],
                Script="""
                    $paths = @(
                        "$env:LOCALAPPDATA\Microsoft\Office\16.0\OfficeFileCache",
                        "$env:LOCALAPPDATA\Microsoft\Office\UnsavedFiles",
                        "$env:TEMP\Word",
                        "$env:TEMP\Excel"
                    )
                    $freed = 0L
                    foreach ($p in $paths) {
                        if (Test-Path $p) {
                            $sz = (Get-ChildItem $p -Recurse -EA SilentlyContinue | Measure-Object -Property Length -Sum -EA SilentlyContinue).Sum
                            $freed += [long]$sz
                            Remove-Item "$p\*" -Recurse -Force -EA SilentlyContinue
                        }
                    }
                    Write-Output "✓ Office cache cleared — freed $([math]::Round($freed/1MB,1)) MB."
                    """ },
        ]
    };

    // ══════════════════════════════════════════════════════════════════════
    //  12. FILE & STORAGE
    // ══════════════════════════════════════════════════════════════════════
    private static FixCategory FileAndStorage() => new()
    {
        Id="files", Icon="\uED25", Title="File & Storage",
        Fixes=
        [
            new() { Id="fix-file-permissions", Title="Fix file permission errors",
                Description="Resets ownership and permissions on a file or folder that says 'Access denied'.",
                Type=FixType.Guided, RequiresAdmin=true,
                Keywords=["access denied", "can't open file", "permission error", "not enough permissions", "file locked"], Steps=[
                    new() { Title="Find the file/folder",    Instruction="Locate the file or folder you can't access and note its full path (e.g. C:\\Users\\Name\\Documents\\file.txt)." },
                    new() { Title="Take ownership",          Instruction="In File Explorer: right-click the file → Properties → Security → Advanced → Owner → Edit → type your username → Check Names → OK → Apply." },
                    new() { Title="Grant full control",      Instruction="Back in Security → Edit → Add → type your username → Check Names → OK → check 'Full control' → Apply → OK." }
                ]},

            new() { Id="recover-deleted-files", Title="Find recently deleted files",
                Description="Checks the Recycle Bin and common recovery paths for recently deleted files.",
                Type=FixType.Guided, Keywords=["recover deleted file", "undelete", "file recovery", "restore deleted", "accidental delete"],
                Steps=[
                    new() { Title="Check Recycle Bin",         Instruction="Open the Recycle Bin on your desktop. Right-click and 'Sort by Date Deleted' to find recent files." },
                    new() { Title="Check Previous Versions",   Instruction="Right-click the folder where the file was → Properties → Previous Versions tab. Restore an earlier version of the folder." },
                    new() { Title="Check OneDrive Recycle Bin", Instruction="If you use OneDrive: go to onedrive.live.com → Recycle Bin. Files deleted from OneDrive folders stay here for 30 days." },
                    new() { Title="Try file recovery software", Instruction="For truly deleted files: download Recuva (free, from Piriform/CCleaner) or TestDisk. Run immediately — the longer you wait, the less chance of recovery." }
                ]},

            new() { Id="fix-onedrive-sync", Title="Fix OneDrive sync issues",
                Description="Resets OneDrive sync and fixes the most common 'sync pending' or 'error' states.",
                Type=FixType.Guided, Keywords=["onedrive not syncing", "onedrive stuck", "cloud sync broken", "onedrive error"],
                Steps=[
                    new() { Title="Reset OneDrive",    Instruction="Click 'Done' to reset OneDrive. It will restart and re-sync.",
                Script="""
                        $onedriveExe = "$env:LOCALAPPDATA\Microsoft\OneDrive\onedrive.exe"
                        if (Test-Path $onedriveExe) {
                            Stop-Process -Name OneDrive -Force -EA SilentlyContinue
                            Start-Sleep 2
                            Start-Process $onedriveExe -ArgumentList "/reset"
                            Start-Sleep 3
                            Start-Process $onedriveExe
                            Write-Output "✓ OneDrive reset and restarted."
                        } else {
                            Write-Output "OneDrive not found in the standard location."
                        }
                        """ },
                    new() { Title="Sign back in",       Instruction="OneDrive will appear in your taskbar and ask you to sign in if needed. Sign in with your Microsoft account." },
                    new() { Title="Wait for sync",      Instruction="Allow 5–10 minutes for OneDrive to re-sync your files. Files sync in order of most recently modified first." }
                ]},

            new() { Id="show-folder-sizes", Title="Show folder sizes on C:",
                Description="Lists the top 10 largest folders on C: to identify what's using the most space.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["which folder is biggest", "folder size", "what is taking up space", "folder too big"],
                Script="""
                    Write-Output "Scanning top-level folders on C: (may take a moment)..."
                    $folders = Get-ChildItem 'C:\' -Directory -EA SilentlyContinue
                    $results = foreach ($f in $folders) {
                        $sz = (Get-ChildItem $f.FullName -Recurse -File -EA SilentlyContinue |
                               Measure-Object -Property Length -Sum -EA SilentlyContinue).Sum
                        [PSCustomObject]@{ Path=$f.FullName; SizeGB=[math]::Round($sz/1GB,2) }
                    }
                    $results | Sort-Object SizeGB -Descending | Select-Object -First 10 |
                    ForEach-Object { Write-Output ("{0,8:N2} GB  {1}" -f $_.SizeGB, $_.Path) }
                    """ },

            new() { Id="fix-file-explorer-crash", Title="Fix File Explorer crashes",
                Description="Resets File Explorer settings and clears the history that causes crashes.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["file explorer crashing", "windows explorer crash", "folder crashes", "explorer freezes", "explorer not responding"],
                Script="""
                    Stop-Process -Name explorer -Force -EA SilentlyContinue
                    # Clear Quick Access history
                    $qa = "$env:APPDATA\Microsoft\Windows\Recent\AutomaticDestinations"
                    Get-ChildItem $qa -File -EA SilentlyContinue | Remove-Item -Force -EA SilentlyContinue
                    # Reset File Explorer view settings
                    Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Streams' -Recurse -Force -EA SilentlyContinue
                    Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3' -Recurse -Force -EA SilentlyContinue
                    Start-Process explorer
                    Write-Output "✓ File Explorer reset and restarted."
                    """ },
        ]
    };

    // ══════════════════════════════════════════════════════════════════════
    //  13. PHONE & MOBILE
    // ══════════════════════════════════════════════════════════════════════
    private static FixCategory PhoneAndMobile() => new()
    {
        Id="phone", Icon="\uE8EA", Title="Phone & Mobile",
        Fixes=
        [
            new() { Id="open-phone-link", Title="Open Phone Link app",
                Description="Opens the Phone Link app to sync your Android or iPhone with your PC.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["phone link", "connect phone to pc", "mirror phone", "android on pc", "iphone on pc"],
                Script="Start-Process 'ms-settings:mobile-devices'" },

            new() { Id="phone-not-charging", Title="Phone not charging — diagnosis",
                Description="Step-by-step guide to find and fix why your phone stopped charging.",
                Keywords=["phone wont charge", "charging not working", "phone charger not working", "phone battery not charging"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Test with a different cable",  Instruction="Try a different USB cable. Cables break internally without visible damage — this fixes ~40% of charging problems." },
                    new() { Title="Test with a different charger", Instruction="Plug the cable into a different charger or wall adapter. Chargers fail silently without warning." },
                    new() { Title="Clean the charging port",       Instruction="Look inside the charging port with a flashlight. Gently remove lint or debris with a dry wooden toothpick. Do NOT use metal. This is the #1 cause on phones over 1 year old." },
                    new() { Title="Force restart the phone",       Instruction="Hold the power button for 10+ seconds until the phone forces off, then power back on. Some charging circuits require a restart to reset." },
                    new() { Title="Check for port damage",         Instruction="If nothing worked: look for bent pins or liquid damage in the charging port. Physical port damage requires professional repair (~$50–80)." }
                ]},

            new() { Id="phone-not-recognized", Title="Phone not recognized by Windows",
                Description="Gets Windows to detect your Android or iPhone over USB.",
                Keywords=["phone not detected", "usb phone not showing", "connect phone usb", "phone not appearing"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Set phone to File Transfer",   Instruction="Plug in your phone. On Android: tap the USB notification → select 'File Transfer' or 'MTP'. On iPhone: tap 'Trust' on the phone screen." },
                    new() { Title="Try a different cable & port", Instruction="Most USB cables are charge-only and carry no data. Try a different cable and a different USB port on your PC." },
                    new() { Title="Scan for phone drivers",       Instruction="Windows Update will check for phone drivers.", Script="Start-Process ms-settings:windowsupdate" },
                    new() { Title="iPhone: install Apple Devices", Instruction="iPhones require the 'Apple Devices' app (free in Microsoft Store) to be recognized. Open the Store and install it if missing." }
                ]},

            new() { Id="phone-wifi-wont-connect", Title="Phone not connecting to Wi-Fi",
                Description="Fixes common phone Wi-Fi connection problems step by step.",
                Keywords=["phone wont connect wifi", "phone wifi problem", "phone keeps disconnecting wifi"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Toggle Airplane Mode",    Instruction="Swipe down on your phone and tap Airplane Mode ON. Wait 15 seconds. Tap it OFF again." },
                    new() { Title="Forget and re-join",      Instruction="Settings → Wi-Fi → tap your network → Forget. Reconnect manually and re-enter the password." },
                    new() { Title="Restart your router",     Instruction="Unplug your router and modem from the wall. Wait 30 seconds. Plug them back in. Wait 2 minutes." },
                    new() { Title="Reset network settings",  Instruction="Last resort: Settings → General Management (Android: System → Reset) → Reset Network Settings. This forgets all saved Wi-Fi passwords." }
                ]},
        ]
    };

    // ══════════════════════════════════════════════════════════════════════
    //  14. REMOTE ACCESS & VPN
    // ══════════════════════════════════════════════════════════════════════
    private static FixCategory RemoteAndVpn() => new()
    {
        Id="remote", Icon="\uE753", Title="Remote Access & VPN",
        Fixes=
        [
            new() { Id="fix-vpn-disconnect", Title="Fix VPN keeps disconnecting",
                Description="Step-by-step fixes for a VPN that drops or can't connect.",
                Keywords=["vpn keeps disconnecting", "vpn dropping", "vpn not stable", "vpn connection lost"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Reset network stack",      Instruction="Click 'Done' to flush DNS and reset TCP/IP — fixes most VPN connectivity errors.", Script="""
                        ipconfig /flushdns | Out-Null
                        netsh winsock reset | Out-Null
                        netsh int ip reset | Out-Null
                        Write-Output "✓ Network stack reset. Restart your PC for full effect."
                        """ },
                    new() { Title="Switch VPN protocol",      Instruction="In your VPN app settings, try switching the protocol: WireGuard is fastest, OpenVPN UDP is most stable, IKEv2 works best on mobile. Try each if one keeps dropping." },
                    new() { Title="Use a wired connection",   Instruction="Plug directly into your router via Ethernet. VPNs over Wi-Fi are prone to disconnects due to signal fluctuation." },
                    new() { Title="Disable IPv6",             Instruction="Open Network adapter settings → right-click your adapter → Properties → uncheck 'Internet Protocol Version 6 (TCP/IPv6)'. Re-enable if this causes other issues." }
                ]},

            new() { Id="check-rdp-status", Title="Check Remote Desktop status",
                Description="Shows whether Remote Desktop is enabled and who has access.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["is remote desktop on", "rdp enabled", "remote desktop status", "check rdp"],
                Script="""
                    $rdp = (Get-ItemProperty 'HKLM:\System\CurrentControlSet\Control\Terminal Server').fDenyTSConnections
                    $status = if($rdp -eq 0){"ENABLED — Remote connections are allowed"}else{"DISABLED"}
                    Write-Output "Remote Desktop: $status"
                    Write-Output ""
                    Write-Output "Users with RDP access:"
                    net localgroup "Remote Desktop Users" 2>&1 | Select-String -Pattern '^\s*\w' | ForEach-Object { Write-Output "  $($_.Line.Trim())" }
                    """ },

            new() { Id="enable-rdp", Title="Enable Remote Desktop",
                Description="Enables Remote Desktop so you can connect to this PC from another computer.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["enable remote desktop", "turn on rdp", "allow remote desktop", "remote in to pc"],
                Script="""
                    Set-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Control\Terminal Server' -Name fDenyTSConnections -Value 0
                    Enable-NetFirewallRule -DisplayGroup 'Remote Desktop' -EA SilentlyContinue
                    Write-Output "✓ Remote Desktop enabled."
                    $ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.PrefixOrigin -ne 'WellKnown'} | Select-Object -First 1).IPAddress
                    Write-Output "  Connect from another PC using: $ip"
                    Write-Output "  ⚠ Only enable this on trusted networks — disable when not in use."
                    """ },

            new() { Id="disable-rdp", Title="Disable Remote Desktop",
                Description="Disables Remote Desktop — reduces attack surface when you don't need remote access.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["disable remote desktop", "turn off rdp", "block remote access", "stop rdp"],
                Script="""
                    Set-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Control\Terminal Server' -Name fDenyTSConnections -Value 1
                    Disable-NetFirewallRule -DisplayGroup 'Remote Desktop' -EA SilentlyContinue
                    Write-Output "✓ Remote Desktop disabled."
                    """ },
        ]
    };

    // ══════════════════════════════════════════════════════════════════════
    //  15. MAINTENANCE & RECOVERY
    // ══════════════════════════════════════════════════════════════════════
    private static FixCategory MaintenanceAndRecovery() => new()
    {
        Id="maintenance", Icon="\uE90F", Title="Maintenance & Recovery",
        Fixes=
        [
            new() { Id="create-restore-point", Title="Create system restore point",
                Description="Creates a Windows restore point right now — your safety net before major changes.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["create restore point", "backup windows", "system snapshot", "before i make changes"],
                Script="""
                    $desc = "HelpDesk restore point — $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
                    Enable-ComputerRestore -Drive "C:\" -EA SilentlyContinue
                    Checkpoint-Computer -Description $desc -RestorePointType MODIFY_SETTINGS -EA SilentlyContinue
                    Write-Output "✓ Restore point created: $desc"
                    """ },

            new() { Id="open-system-restore", Title="Open System Restore wizard",
                Description="Opens System Restore to roll Windows back to a previous working state.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["restore windows", "system restore", "go back to restore point", "undo changes", "restore point"],
                Script="rstrui.exe" },

            new() { Id="open-recovery-options", Title="Open Windows Recovery options",
                Description="Opens Windows Recovery settings for Reset, Startup Repair, and advanced options.",
                Type=FixType.Silent, RequiresAdmin=false,
                Script="Start-Process ms-settings:recovery" },

            new() { Id="clear-event-logs", Title="Clear Windows Event Logs",
                Description="Clears Application and System event logs that can grow very large over time.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["event log full", "clear logs", "system logs"],
                Script="""
                    foreach ($log in @('Application','System','Setup')) {
                        try {
                            Clear-EventLog -LogName $log -EA SilentlyContinue
                            Write-Output "✓ Cleared: $log"
                        } catch {
                            Write-Output "⚠ Skipped: $log (access denied)"
                        }
                    }
                    Write-Output "Event logs cleared."
                    """ },

            new() { Id="check-scheduled-tasks", Title="Open Task Scheduler",
                Description="Opens Task Scheduler to view and manage automated tasks.",
                Type=FixType.Silent, RequiresAdmin=false,
                Script="taskschd.msc" },

            new() { Id="reset-power-plans", Title="Reset all power plans to default",
                Description="Restores all Windows power plans to their factory defaults.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["reset power settings", "restore power plans", "power plan reset", "fix power settings"],
                Script="""
                    powercfg /restoredefaultschemes
                    Write-Output "✓ All power plans restored to Windows defaults."
                    """ },

            new() { Id="clear-font-cache", Title="Clear font cache",
                Description="Clears Windows font cache — fixes corrupted or missing font rendering.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["font rendering wrong", "fonts corrupted", "text looking wrong", "font cache"],
                Script="""
                    Stop-Service FontCache -Force -EA SilentlyContinue
                    Stop-Service 'FontCache3.0.0.0' -Force -EA SilentlyContinue
                    Remove-Item "$env:WINDIR\ServiceProfiles\LocalService\AppData\Local\FontCache*" -Force -EA SilentlyContinue
                    Remove-Item "$env:WINDIR\System32\FNTCACHE.DAT" -Force -EA SilentlyContinue
                    Start-Service FontCache -EA SilentlyContinue
                    Write-Output "✓ Font cache cleared."
                    """ },
        ]
    };

    // ══════════════════════════════════════════════════════════════════════
    //  16. SLEEP & POWER
    // ══════════════════════════════════════════════════════════════════════
    private static FixCategory SleepAndPower() => new()
    {
        Id="sleep", Icon="\uE945", Title="Sleep & Power",
        Fixes=
        [
            new() { Id="fix-pc-wont-sleep", Title="Fix PC won't go to sleep",
                Description="Finds processes and settings blocking sleep mode.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["computer wont sleep", "pc not going to sleep", "sleep not working", "screen stays on", "keep awake"],
                Script="""
                    Write-Output "=== Sleep blockers ==="
                    $requests = powercfg /requests
                    Write-Output $requests
                    Write-Output ""
                    Write-Output "=== Wake sources (what woke the PC last time) ==="
                    powercfg /lastwake
                    Write-Output ""
                    Write-Output "=== Devices that can wake the PC ==="
                    powercfg /devicequery wake_armed
                    """ },

            new() { Id="fix-pc-wont-wake", Title="Fix PC won't wake from sleep",
                Description="Troubleshoots a PC that goes to sleep and won't wake up.",
                Keywords=["computer wont wake", "stuck asleep", "wont wake from sleep", "pc wont turn back on"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Check wake timer settings",  Instruction="Click 'Done' to allow wake timers.", Script="""
                        $plans = powercfg /list
                        $active = ($plans | Where-Object {$_ -match '\*'} -EA SilentlyContinue) -replace '.*\*\s*',''
                        powercfg /change standby-timeout-ac 30 2>&1 | Out-Null
                        Write-Output "✓ Wake timers enabled. Sleep timeout set to 30 min."
                        """ },
                    new() { Title="Enable keyboard wake",        Instruction="Click 'Done' to enable USB keyboard as a wake device.", Script="""
                        Get-WmiObject Win32_NetworkAdapterConfiguration | Where-Object {$_.IPEnabled} | ForEach-Object { $_.SetWakeOnMagicPacket($true) | Out-Null }
                        Write-Output "✓ Wake-on-LAN configured. For USB keyboard wake, see Device Manager → HID Keyboard → Power Management → 'Allow this device to wake the computer'."
                        """ },
                    new() { Title="Update chipset/USB drivers",  Instruction="If still can't wake: update USB controller and chipset drivers in Device Manager. These driver bugs cause most wake failures." }
                ]},

            new() { Id="fix-fast-startup", Title="Disable Fast Startup (fixes post-update issues)",
                Description="Disables Fast Startup which can cause problems after Windows updates. Increases boot time slightly.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["fast startup", "slow shutdown", "shutdown taking long", "disable fast boot", "hibernate on shutdown"],
                Script="""
                    $path = 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power'
                    Set-ItemProperty -Path $path -Name HiberbootEnabled -Value 0 -Type DWord
                    Write-Output "✓ Fast Startup disabled."
                    Write-Output "  PC will do a full cold boot every time. Re-enable in Power Options → Choose what the power buttons do."
                    """ },

            new() { Id="show-power-report", Title="Generate power efficiency report",
                Description="Creates an HTML power efficiency report showing battery drain sources.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["power report", "battery report", "energy usage", "power plan report"],
                Script="""
                    $f = "$env:USERPROFILE\Desktop\PowerReport.html"
                    powercfg /energy /output $f /duration 60
                    if (Test-Path $f) {
                        Start-Process $f
                        Write-Output "✓ Power efficiency report saved to Desktop and opened."
                    } else {
                        Write-Output "Power report generation failed. Try running as administrator."
                    }
                    """ },
        ]
    };

    // ══════════════════════════════════════════════════════════════════════
    //  17. WINDOWS FEATURES
    // ══════════════════════════════════════════════════════════════════════
    private static FixCategory WindowsFeatures() => new()
    {
        Id="winfeatures", Icon="\uE782", Title="Windows Features",
        Fixes=
        [
            new() { Id="fix-windows-hello", Title="Reset Windows Hello PIN / fingerprint",
                Description="Removes and resets Windows Hello PIN and biometrics to fix login problems.",
                Keywords=["windows hello broken", "fingerprint not working", "face recognition broken", "pin not working", "hello setup"],
                Type=FixType.Guided, Steps=[
                    new() { Title="Open Sign-in options",   Instruction="Sign-in options will open.", Script="Start-Process ms-settings:signinoptions" },
                    new() { Title="Remove PIN",             Instruction="Click 'Windows Hello PIN' → 'Remove'. If this fails: open an admin CMD and run: net user [username] *" },
                    new() { Title="Set up a new PIN",       Instruction="Click 'Windows Hello PIN' → 'Set up' → follow the wizard to create a new PIN." }
                ]},

            new() { Id="fix-cortana-search", Title="Rebuild Windows Search index",
                Description="Deletes and rebuilds the Windows Search index — fixes searches returning no results.",
                Type=FixType.Guided, RequiresAdmin=true, Keywords=["search not working", "cortana broken", "start menu search empty", "search nothing", "rebuild search"],
                Steps=[
                    new() { Title="Open Indexing Options",   Instruction="Indexing Options will open.", Script="control.exe srchadmin.dll" },
                    new() { Title="Advanced settings",       Instruction="Click 'Advanced' → click 'Rebuild' under Troubleshooting → click OK. Wait 30–60 minutes for the index to rebuild." }
                ]},

            new() { Id="enable-sandbox", Title="Enable Windows Sandbox",
                Description="Enables Windows Sandbox — a safe, isolated environment to run untrusted software.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["windows sandbox", "test programs safely", "isolated environment", "sandbox"],
                Script="""
                    Enable-WindowsOptionalFeature -Online -FeatureName 'Containers-DisposableClientVM' -All -EA SilentlyContinue
                    Write-Output "✓ Windows Sandbox enabled (or already enabled). Restart may be required."
                    Write-Output "  Search for 'Windows Sandbox' in the Start Menu to launch it."
                    """ },

            new() { Id="fix-microsoft-store-updates", Title="Fix Microsoft Store not updating apps",
                Description="Clears the Store cache and resets update settings.",
                Type=FixType.Silent, RequiresAdmin=false,
                Script="""
                    Stop-Process -Name WinStore.App -Force -EA SilentlyContinue
                    wsreset.exe
                    Write-Output "✓ Microsoft Store cache reset. The Store will reopen in a few seconds."
                    """ },

            new() { Id="toggle-dark-mode", Title="Toggle dark/light mode",
                Description="Switches Windows between dark mode and light mode.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["dark mode", "light mode", "switch theme", "dark theme windows"],
                Script="""
                    $path = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize'
                    $current = (Get-ItemProperty -Path $path).AppsUseLightTheme
                    $new = if($current -eq 1){0}else{1}
                    Set-ItemProperty -Path $path -Name AppsUseLightTheme   -Value $new
                    Set-ItemProperty -Path $path -Name SystemUsesLightTheme -Value $new
                    $mode = if($new -eq 0){"Dark"}else{"Light"}
                    Write-Output "✓ Switched to $mode mode."
                    """ },
        ]
    };

    // ══════════════════════════════════════════════════════════════════════
    //  18. ADVANCED TOOLS
    // ══════════════════════════════════════════════════════════════════════
    private static FixCategory AdvancedTools() => new()
    {
        Id="advanced", Icon="\uE713", Title="Advanced Tools",
        Fixes=
        [
            new() { Id="open-group-policy", Title="Open Group Policy Editor",
                Description="Opens the Local Group Policy Editor for advanced Windows settings.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["group policy", "gpedit", "advanced windows policy", "enterprise settings"],
                Script="gpedit.msc" },

            new() { Id="open-registry-editor", Title="Open Registry Editor",
                Description="Opens the Windows Registry Editor (regedit) — for advanced users.",
                Type=FixType.Guided, Steps=[
                    new() { Title="Open Registry Editor", Instruction="⚠ The Registry Editor will open. Incorrect edits can break Windows. Create a restore point before making changes.", Script="regedit.exe" }
                ]},

            new() { Id="open-resource-monitor", Title="Open Resource Monitor",
                Description="Opens Resource Monitor — detailed real-time view of CPU, RAM, disk, and network usage.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["registry editor", "regedit", "edit registry", "advanced registry", "windows registry"],
                Script="resmon.exe" },

            new() { Id="flush-kerberos-tickets", Title="Clear Kerberos authentication tickets",
                Description="Clears cached network credentials — fixes 'access denied' on domain networks.",
                Type=FixType.Silent, RequiresAdmin=false,
                Keywords=["kerberos", "network auth", "active directory login", "domain credentials"],
                Script="klist purge; Write-Output '✓ Kerberos tickets purged.'" },

            new() { Id="repair-wmi-2", Title="Full WMI repository rebuild",
                Description="Completely rebuilds the WMI repository — fixes deep WMI corruption issues.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["wmi broken", "wmi error", "management instrumentation", "performance monitor not working", "wmi repair"],
                Script="""
                    Write-Output "Rebuilding WMI repository (may take 2-5 minutes)..."
                    Stop-Service winmgmt -Force
                    Rename-Item "$env:WINDIR\System32\wbem\repository" "$env:WINDIR\System32\wbem\repository.old" -EA SilentlyContinue
                    Start-Service winmgmt
                    Start-Sleep 5
                    $mofs = Get-ChildItem "$env:WINDIR\System32\wbem" -Filter "*.mof" -EA SilentlyContinue
                    foreach ($m in $mofs) {
                        mofcomp "$($m.FullName)" 2>&1 | Out-Null
                    }
                    Write-Output "✓ WMI repository rebuilt. Restart recommended."
                    """ },

            new() { Id="enable-verbose-boot", Title="Enable verbose boot messages",
                Description="Shows detailed text during Windows startup/shutdown instead of the spinning circle.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["slow startup diagnosis", "what slows boot", "verbose boot", "startup verbose", "analyze startup time"],
                Script="""
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -Name VerboseStatus -Value 1 -Type DWord
                    Write-Output "✓ Verbose startup/shutdown messages enabled."
                    Write-Output "  Disable: Set VerboseStatus to 0 in the same registry path."
                    """ },

            new() { Id="generate-full-health-report", Title="Generate comprehensive PC health report",
                Description="Runs SFC, checks disk health, Defender status, and exports a full report to Desktop.",
                Type=FixType.Silent, RequiresAdmin=true,
                Keywords=["full health check", "comprehensive report", "system status report", "pc health report", "overall pc check"],
                Script="""
                    $report = "$env:USERPROFILE\Desktop\PCHealthReport_$(Get-Date -Format 'yyyyMMdd_HHmm').txt"
                    $lines = @()
                    $lines += "=== PC Health Report — $(Get-Date) ==="
                    $lines += ""

                    # System info
                    $os  = Get-CimInstance Win32_OperatingSystem
                    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
                    $lines += "SYSTEM"
                    $lines += "  OS    : $($os.Caption) Build $($os.BuildNumber)"
                    $lines += "  CPU   : $($cpu.Name.Trim())"
                    $lines += "  RAM   : $([math]::Round($os.TotalVisibleMemorySize/1MB,1)) GB"
                    $lines += "  Uptime: $([timespan]::FromMilliseconds([Environment]::TickCount64).ToString('d\d\ h\h\ m\m'))"
                    $lines += ""

                    # Disk
                    $lines += "DISK"
                    Get-PhysicalDisk | ForEach-Object {
                        $lines += "  $($_.FriendlyName) — $($_.HealthStatus) — $($_.MediaType)"
                    }
                    $lines += ""

                    # Defender
                    $def = Get-MpComputerStatus -EA SilentlyContinue
                    if ($def) {
                        $lines += "SECURITY"
                        $lines += "  Antivirus: $(if($def.AntivirusEnabled){'Enabled'}else{'DISABLED'})"
                        $lines += "  Real-time: $(if($def.RealTimeProtectionEnabled){'Active'}else{'INACTIVE'})"
                        $lines += "  Definitions age: $($def.AntivirusSignatureAge) days"
                        $lines += ""
                    }

                    # Driver problems
                    $bad = Get-PnpDevice | Where-Object {$_.Status -ne 'OK'} -EA SilentlyContinue
                    $lines += "DRIVERS"
                    if ($bad) { $bad | ForEach-Object { $lines += "  PROBLEM: $($_.FriendlyName)" } }
                    else { $lines += "  No driver problems found." }
                    $lines += ""

                    # Recent errors
                    $lines += "RECENT ERRORS (last 10)"
                    Get-WinEvent -FilterHashtable @{LogName='System';Level=2} -MaxEvents 10 -EA SilentlyContinue |
                    ForEach-Object { $lines += "  $($_.TimeCreated.ToString('MM/dd HH:mm')) [$($_.ProviderName)] $($_.Message.Split("`n")[0].Trim())" }

                    $lines | Out-File $report -Encoding UTF8
                    Write-Output "✓ Health report saved to: $report"
                    Start-Process notepad $report
                    """ },
        ]
    };

    // ══════════════════════════════════════════════════════════════════════
    //  BUNDLES
    // ══════════════════════════════════════════════════════════════════════
    private static List<FixBundle> BuildBundles() =>
    [
        new() {
            Id="weekly-tune-up", Icon="\uE8A0", Title="Weekly Tune-Up",
            Description="Essential weekly maintenance — clears junk, resets caches, checks health.",
            EstTime="~3 min",
            FixIds=["clear-temp-files","clear-thumbnail-cache","flush-dns","check-defender-status","disk-space-all-drives"]
        },
        new() {
            Id="network-full-reset", Icon="\uE968", Title="Network Fix Pack",
            Description="Full network repair — flushes DNS, renews IP, resets TCP/IP stack.",
            EstTime="~2 min",
            FixIds=["flush-dns","renew-ip","full-network-reset","test-connection"]
        },
        new() {
            Id="gaming-boost", Icon="\uE7FC", Title="Gaming Boost",
            Description="Maximizes gaming performance — Game Mode, network tweaks, shader cache clear, registry tweaks.",
            EstTime="~3 min",
            FixIds=["enable-game-mode","disable-game-bar","gaming-registry-tweaks","gaming-network-tweaks","clear-shader-cache","set-ultimate-performance"]
        },
        new() {
            Id="streaming-boost", Icon="\uE704", Title="Streaming Optimization",
            Description="Optimizes your PC specifically for game streaming with OBS/Streamlabs.",
            EstTime="~2 min",
            FixIds=["enable-game-mode","disable-game-bar","gaming-network-tweaks","streaming-network-adapter-tune","clear-shader-cache","add-defender-game-exclusions"]
        },
        new() {
            Id="security-check", Icon="\uE72E", Title="Security Health Check",
            Description="Verifies antivirus, firewall, scans for suspicious startup programs.",
            EstTime="~2 min",
            FixIds=["check-defender-status","update-virus-definitions","check-firewall","list-startup-programs","check-open-ports"]
        },
        new() {
            Id="full-repair", Icon="\uE90F", Title="Full System Repair",
            Description="Runs SFC and DISM to fix corrupted Windows files. Requires internet. Takes 20–40 minutes.",
            EstTime="~30 min",
            FixIds=["run-sfc","run-dism","scan-driver-problems","check-defender-status","bsod-check-recent-crashes"]
        },
        new() {
            Id="startup-speed", Icon="\uE9D9", Title="Speed Up Boot",
            Description="Disables Game Bar, sets High Performance power, clears temp files for faster startup.",
            EstTime="~2 min",
            FixIds=["clear-temp-files","set-high-performance","disable-game-bar","clear-thumbnail-cache","optimize-visual-effects"]
        },
        new() {
            Id="deep-clean", Icon="\uECFC", Title="Deep Clean",
            Description="Maximum cleanup — temp files, browser caches, thumbnails, Windows Update cache, event logs.",
            EstTime="~5 min",
            FixIds=["clear-temp-files","clear-thumbnail-cache","clear-browser-cache","clear-wsu-download-cache","run-disk-cleanup","clear-event-logs"]
        },
        new() {
            Id="privacy-lockdown", Icon="\uE72E", Title="Privacy Lockdown",
            Description="Disables telemetry, Cortana, advertising ID, and tips/ads for maximum privacy.",
            EstTime="~2 min",
            FixIds=["disable-telemetry","disable-cortana","disable-ads-tips","disable-advertising-id","check-open-ports"]
        },
        new() {
            Id="fresh-start-prep", Icon="\uE8F4", Title="Fresh Start Prep",
            Description="Cleans up your PC before a reset — clears caches, defragments, repairs Windows image.",
            EstTime="~25 min",
            FixIds=["clear-temp-files","run-sfc","run-dism","optimize-drive","clear-wsu-download-cache","clear-thumbnail-cache"]
        },
        new() {
            Id="laptop-battery-saver", Icon="\uE83F", Title="Laptop Battery Saver",
            Description="Maximizes battery life — balanced power plan, USB selective suspend, reduces background activity.",
            EstTime="~2 min",
            FixIds=["set-high-performance","disable-superfetch","optimize-visual-effects","disable-telemetry","usb-selective-suspend"]
        },
        new() {
            Id="gaming-pc-setup", Icon="\uE7FC", Title="Gaming PC Setup",
            Description="Full gaming optimization — Ultimate Performance plan, HAGS, Game Mode, disable Game Bar, clear shader cache.",
            EstTime="~5 min",
            FixIds=["set-ultimate-performance","enable-game-mode","disable-game-bar","gaming-registry-tweaks","gaming-network-tweaks","clear-shader-cache","enable-hags","add-defender-game-exclusions"]
        },
        new() {
            Id="new-user-setup", Icon="\uE77B", Title="New User Setup",
            Description="First-time PC setup — activates Windows, checks updates, sets DNS, adjusts visual effects.",
            EstTime="~5 min",
            FixIds=["open-windows-update","check-activation","set-dns-cloudflare","optimize-visual-effects","check-defender-status","disable-ads-tips"]
        },
        new() {
            Id="work-from-home-pack", Icon="\uE821", Title="Work From Home Pack",
            Description="Optimizes your PC for remote work — fixes webcam, mic, network, Teams, and DNS.",
            EstTime="~4 min",
            FixIds=["fix-webcam","fix-microphone","flush-dns","set-dns-cloudflare","clear-teams-cache","full-network-reset","set-high-performance"]
        },
        new() {
            Id="student-low-end-pack", Icon="\uE7BE", Title="Student / Low-End PC Pack",
            Description="Maximum performance on limited hardware — disables animations, SuperFetch, optimizes RAM.",
            EstTime="~3 min",
            FixIds=["optimize-visual-effects","disable-superfetch","clear-temp-files","set-high-performance","disable-startup-apps","top-memory-processes"]
        },
        new() {
            Id="post-malware-cleanup", Icon="\uEADF", Title="Post-Malware Cleanup",
            Description="Post-infection cleanup — updates Defender, clears startup programs, checks hosts file, resets network.",
            EstTime="~5 min",
            FixIds=["update-virus-definitions","check-defender-status","list-startup-programs","view-hosts-file","full-network-reset","check-open-ports","reset-windows-update"]
        },
    ];
}
