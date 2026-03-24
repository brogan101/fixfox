using System.IO;
using HelpDesk.Application.Interfaces;
using HelpDesk.Shared;

namespace HelpDesk.Infrastructure.Services;

// ══════════════════════════════════════════════════════════════════════════
//  APP LOGGER  — file-based structured logger written to AppData\FixFox\app.log
// ══════════════════════════════════════════════════════════════════════════

public sealed class AppLogger : IAppLogger
{
    private readonly string _logPath;
    private readonly object _lock = new();

    public AppLogger()
    {
        _logPath = Constants.AppLogFile;
        try { Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!); }
        catch { /* best effort */ }
    }

    public void Info (string message) => Write("INF", message);
    public void Warn (string message) => Write("WRN", message);
    public void Error(string message, Exception? ex = null)
    {
        Write("ERR", ex is null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}");
        if (ex?.StackTrace is string st)
            Write("ERR", $"  Stack: {st.Split('\n').FirstOrDefault()?.Trim() ?? ""}");
    }

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        lock (_lock)
        {
            try { File.AppendAllText(_logPath, line + Environment.NewLine); }
            catch { /* never crash the app because logging failed */ }
        }
    }
}

/// <summary>No-op logger used in headless / test contexts where no file output is wanted.</summary>
public sealed class NullAppLogger : IAppLogger
{
    public void Info (string message) { }
    public void Warn (string message) { }
    public void Error(string message, Exception? ex = null) { }
}

/// <summary>Console logger used by the --verify-headless mode.</summary>
public sealed class ConsoleAppLogger : IAppLogger
{
    public void Info (string message) => Console.WriteLine($"[INF] {message}");
    public void Warn (string message) => Console.WriteLine($"[WRN] {message}");
    public void Error(string message, Exception? ex = null) =>
        Console.WriteLine($"[ERR] {message}{(ex is null ? "" : $" | {ex.Message}")}");
}
