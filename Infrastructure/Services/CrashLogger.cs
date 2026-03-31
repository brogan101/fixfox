using System.IO;
using System.Reflection;
using HelpDesk.Application.Interfaces;
using HelpDesk.Shared;

namespace HelpDesk.Infrastructure.Services;

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
//  CRASH LOGGER  â€” writes crash reports to AppData\FixFox\crashes\
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

public sealed class CrashLogger : ICrashLogger
{
    public void Log(Exception ex, string? context = null)
    {
        try
        {
            var dir = Constants.CrashDir;
            Directory.CreateDirectory(dir);

            var filename = $"crash_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N[..6]}.log";
            var path     = Path.Combine(dir, filename);

            var lines = new List<string>
            {
                $"FixFox Crash Report - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Version : {Constants.AppVersion}",
                $".NET    : {Environment.Version}",
                $"OS      : {Environment.OSVersion}",
                $"Machine : {Environment.MachineName}",
                $"User    : {Environment.UserName}",
                new string('-', 70),
            };

            if (!string.IsNullOrWhiteSpace(context))
            {
                lines.Add($"Context : {context}");
                lines.Add(new string('-', 70));
            }

            // Walk the exception chain
            var current = ex;
            var depth   = 0;
            while (current is not null)
            {
                if (depth > 0) lines.Add($"  [Inner exception {depth}]");
                lines.Add($"  Type    : {current.GetType().FullName}");
                lines.Add($"  Message : {current.Message}");
                if (current.StackTrace is string st)
                {
                    lines.Add("  Stack trace:");
                    foreach (var line in st.Split('\n'))
                        lines.Add($"    {line.Trim()}");
                }
                current = current.InnerException;
                depth++;
            }

            File.WriteAllLines(path, lines);

            // Keep only the 10 most recent crash files
            var crashFiles = new DirectoryInfo(dir)
                .GetFiles("crash_*.log")
                .OrderByDescending(f => f.LastWriteTime)
                .Skip(10)
                .ToList();
            foreach (var old in crashFiles)
            {
                try { old.Delete(); } catch { }
            }
        }
        catch { /* crash logger must never throw */ }
    }
}
