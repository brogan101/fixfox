using System.Diagnostics;
using System.Security.Principal;
using HelpDesk.Application.Interfaces;

namespace HelpDesk.Infrastructure.Services;

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
//  ELEVATION SERVICE  â€” checks and requests UAC elevation
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

public sealed class ElevationService : IElevationService
{
    public bool IsElevated
    {
        get
        {
            using var identity  = WindowsIdentity.GetCurrent();
            var       principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>
    /// Re-launches the current executable with administrator privileges.
    /// Returns true if the relaunch was initiated, false if already elevated or launch failed.
    /// </summary>
    public bool RelaunchElevated(string? extraArgs = null)
    {
        if (IsElevated) return false;

        try
        {
            var exe  = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var args = Environment.GetCommandLineArgs().Skip(1).ToList();
            if (!string.IsNullOrWhiteSpace(extraArgs)) args.Add(extraArgs);

            Process.Start(new ProcessStartInfo
            {
                FileName        = exe,
                Arguments       = string.Join(" ", args),
                UseShellExecute = true,
                Verb            = "runas",
            });

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
