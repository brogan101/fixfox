using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using HelpDesk.Presentation.ViewModels;
using HelpDesk.Presentation.Views;
using Xunit;

namespace HelpDesk.Tests;

public sealed class QuickAccessWorkspaceTests
{
    [Fact]
    public void Uptime_Over_Seven_Days_Generates_A_Restart_Suggestion()
    {
        var service = new DashboardWorkspaceService();

        var suggestions = service.BuildSuggestions(
            new DashboardSuggestionSignals { Uptime = TimeSpan.FromDays(9) },
            [],
            [],
            DateTime.UtcNow);

        Assert.Contains(suggestions, suggestion => suggestion.Key == "uptime-restart");
    }

    [Fact]
    public void Uptime_Under_One_Day_Does_Not_Generate_A_Restart_Suggestion()
    {
        var service = new DashboardWorkspaceService();

        var suggestions = service.BuildSuggestions(
            new DashboardSuggestionSignals { Uptime = TimeSpan.FromHours(12) },
            [],
            [],
            DateTime.UtcNow);

        Assert.DoesNotContain(suggestions, suggestion => suggestion.Key == "uptime-restart");
    }

    [Fact]
    public void Low_Disk_Space_Generates_A_Cleanup_Suggestion()
    {
        var service = new DashboardWorkspaceService();

        var suggestions = service.BuildSuggestions(
            new DashboardSuggestionSignals { SystemDriveFreePercent = 12 },
            [],
            [],
            DateTime.UtcNow);

        Assert.Contains(suggestions, suggestion => suggestion.Key == "low-disk-space");
    }

    [Fact]
    public void Dismissed_Suggestion_Does_Not_Reappear_Within_Seven_Days()
    {
        var suggestions = new[]
        {
            new DashboardSuggestion { Key = "uptime-restart", Title = "Restart and clear memory pressure" },
            new DashboardSuggestion { Key = "temp-cleanup", Title = "Clear temp file buildup" }
        };
        var dismissed = new[]
        {
            new DismissedDashboardSuggestion
            {
                Key = "uptime-restart",
                DismissedUntilUtc = DateTime.UtcNow.AddDays(3)
            }
        };

        var filtered = MainViewModel.FilterDismissedDashboardSuggestions(suggestions, dismissed, DateTime.UtcNow);

        Assert.DoesNotContain(filtered, suggestion => suggestion.Key == "uptime-restart");
        Assert.Contains(filtered, suggestion => suggestion.Key == "temp-cleanup");
    }

    [Fact]
    public async Task Slow_Signal_Probes_Time_Out_Within_Three_Seconds()
    {
        var service = CreateSignalService(
            async ct => { await Task.Delay(TimeSpan.FromSeconds(10), ct); return TimeSpan.FromDays(8); },
            async ct => { await Task.Delay(TimeSpan.FromSeconds(10), ct); return 2_000_000_000L; },
            async ct => { await Task.Delay(TimeSpan.FromSeconds(10), ct); return true; },
            async ct => { await Task.Delay(TimeSpan.FromSeconds(10), ct); return true; },
            async ct => { await Task.Delay(TimeSpan.FromSeconds(10), ct); return 12; },
            async ct => { await Task.Delay(TimeSpan.FromSeconds(10), ct); return 10d; });

        var stopwatch = Stopwatch.StartNew();
        var result = await service.EvaluateAsync([], []);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(4), $"Signal evaluation took {stopwatch.Elapsed}.");
        Assert.Null(result.Uptime);
        Assert.Null(result.TempFolderBytes);
        Assert.False(result.HasPendingWindowsUpdate);
        Assert.False(result.HasRecentCriticalCrash);
        Assert.Equal(0, result.EnabledStartupItemCount);
        Assert.Null(result.SystemDriveFreePercent);
    }

    [Theory]
    [InlineData(Key.H, ModifierKeys.Control, false, ShellShortcutAction.NavigateHistory)]
    [InlineData(Key.F, ModifierKeys.Control, false, ShellShortcutAction.NavigateFixCenter)]
    [InlineData(Key.Oem2, ModifierKeys.Shift, false, ShellShortcutAction.OpenKeyboardShortcutsDialog)]
    [InlineData(Key.H, ModifierKeys.Control, true, ShellShortcutAction.None)]
    public void Global_Shortcut_Router_Maps_Expected_Actions(
        Key key,
        ModifierKeys modifiers,
        bool textInputFocused,
        ShellShortcutAction expected)
    {
        var action = MainWindow.ResolveGlobalShortcut(key, modifiers, textInputFocused);

        Assert.Equal(expected, action);
    }

    private static DashboardSuggestionSignalService CreateSignalService(
        Func<CancellationToken, Task<TimeSpan?>> uptimeProbe,
        Func<CancellationToken, Task<long?>> tempFolderProbe,
        Func<CancellationToken, Task<bool>> pendingUpdateProbe,
        Func<CancellationToken, Task<bool>> recentCrashProbe,
        Func<CancellationToken, Task<int>> startupCountProbe,
        Func<CancellationToken, Task<double?>> systemDriveFreePercentProbe)
    {
        var ctor = typeof(DashboardSuggestionSignalService)
            .GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [
                    typeof(Func<CancellationToken, Task<TimeSpan?>>),
                    typeof(Func<CancellationToken, Task<long?>>),
                    typeof(Func<CancellationToken, Task<bool>>),
                    typeof(Func<CancellationToken, Task<bool>>),
                    typeof(Func<CancellationToken, Task<int>>),
                    typeof(Func<CancellationToken, Task<double?>>)
                ],
                modifiers: null);

        Assert.NotNull(ctor);

        return (DashboardSuggestionSignalService)ctor!.Invoke(
            [uptimeProbe, tempFolderProbe, pendingUpdateProbe, recentCrashProbe, startupCountProbe, systemDriveFreePercentProbe]);
    }
}
