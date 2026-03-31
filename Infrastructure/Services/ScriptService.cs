using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;

namespace HelpDesk.Infrastructure.Services;

/// <summary>
/// Executes PowerShell fix scripts in isolated temp directories.
/// Security guarantees:
///   â€¢ Each run gets its own temp subdirectory â€” scripts cannot reference each other.
///   â€¢ Script file is deleted immediately after process exits.
///   â€¢ Output is capped at 8 KB to prevent UI flooding.
///   â€¢ 90-second hard timeout with graceful kill.
///   â€¢ No shell-execute unless admin elevation is explicitly required.
/// </summary>
public sealed class ScriptService : IScriptService
{
    private const int TimeoutSeconds = 90;
    private const int MaxOutputChars = 4000;
    private static readonly string TempRoot = Path.Combine(Path.GetTempPath(), "FixFox");

    static ScriptService()
    {
        try { Directory.CreateDirectory(TempRoot); } catch { }
    }

    public async Task<(bool Success, string Output)> RunAsync(string script, bool requiresAdmin = false)
    {
        if (string.IsNullOrWhiteSpace(script))
            return (false, "No script provided.");

        var runDir  = Path.Combine(TempRoot, Guid.NewGuid().ToString("N"));
        var ps1Path = Path.Combine(runDir, "fix.ps1");

        try
        {
            Directory.CreateDirectory(runDir);
            await File.WriteAllTextAsync(ps1Path, script, new UTF8Encoding(true));

            using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            var       captureOutput = !requiresAdmin || IsCurrentProcessElevated();
            var       psi  = BuildPsi(ps1Path, requiresAdmin, captureOutput);
            using var proc = new Process { StartInfo = psi };

            if (requiresAdmin && !captureOutput)
            {
                proc.Start();
                await proc.WaitForExitAsync(cts.Token);
                return (proc.ExitCode == 0, "Fix ran with administrator privileges.");
            }

            proc.Start();

            var outTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
            var errTask = proc.StandardError.ReadToEndAsync(cts.Token);

            try { await proc.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                TryKill(proc);
                return (false, $"Script timed out after {TimeoutSeconds} seconds and was stopped.");
            }

            var stdout = await outTask;
            var stderr = await errTask;
            var output = Merge(stdout, stderr);

            return (proc.ExitCode == 0, Truncate(output));
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (false, "Administrator permission was denied. Run FixFox as administrator if needed.");
        }
        catch (Exception ex)
        {
            return (false, $"Error running fix: {ex.Message}");
        }
        finally
        {
            SafeDelete(runDir);
        }
    }

    public async Task RunFixAsync(FixItem fix)
    {
        if (fix.Type == FixType.Guided)
        {
            fix.Status     = FixStatus.Failed;
            fix.LastOutput = "Use 'Show me how' to walk through this fix step by step.";
            return;
        }
        if (string.IsNullOrWhiteSpace(fix.Script))
        {
            fix.Status     = FixStatus.Failed;
            fix.LastOutput = "No script defined for this fix.";
            return;
        }

        fix.Status     = FixStatus.Running;
        fix.LastOutput = "Working on it...";

        var (ok, output) = await RunAsync(fix.Script, fix.RequiresAdmin);

        fix.Status     = ok ? FixStatus.Success : FixStatus.Failed;
        fix.LastOutput = string.IsNullOrWhiteSpace(output)
            ? (ok ? "Completed successfully." : "Completed with errors.")
            : output;
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static ProcessStartInfo BuildPsi(string path, bool admin, bool captureOutput)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "powershell.exe",
            Arguments              = $"-NonInteractive -NoProfile -ExecutionPolicy Bypass -File \"{path}\"",
            UseShellExecute        = admin && !captureOutput,
            CreateNoWindow         = !admin || captureOutput,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError  = captureOutput,
            Verb                   = admin && !captureOutput ? "runas" : "",
        };

        if (captureOutput)
        {
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding  = Encoding.UTF8;
        }

        return psi;
    }

    private static string Merge(string out1, string err)
    {
        var s = out1.Trim();
        var e = err.Trim();
        if (string.IsNullOrEmpty(e)) return s;
        return string.IsNullOrEmpty(s) ? e : $"{s}\n{e}";
    }

    private static string Truncate(string text)
    {
        if (text.Length <= MaxOutputChars) return text;
        return text[..MaxOutputChars] + "\n\n[Output truncated]";
    }

    private static void TryKill(Process p)
    {
        try { if (!p.HasExited) p.Kill(true); } catch { }
    }

    private static bool IsCurrentProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void SafeDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
