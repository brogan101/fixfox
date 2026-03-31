using System.Collections.ObjectModel;
using HelpDesk.Domain.Models;

namespace HelpDesk.Presentation.ViewModels;

public sealed class ToolboxWorkspaceState
{
    public const int MaxPinnedTools = 10;
    public const int MaxRecentTools = 5;

    private readonly List<ToolboxEntry> _knownEntries = [];

    public ObservableCollection<ToolboxEntry> Favorites { get; } = [];
    public ObservableCollection<ToolboxEntry> Recent { get; } = [];

    public string WarningMessage { get; private set; } = "";

    public void RegisterEntries(IEnumerable<ToolboxEntry> entries)
    {
        _knownEntries.Clear();
        _knownEntries.AddRange(entries.Distinct());
    }

    public void RestorePinned(IReadOnlyCollection<string> pinnedToolKeys)
    {
        Favorites.Clear();

        foreach (var entry in _knownEntries)
            entry.IsPinned = false;

        foreach (var key in pinnedToolKeys)
        {
            var entry = _knownEntries.FirstOrDefault(candidate =>
                string.Equals(candidate.ToolKey, key, StringComparison.OrdinalIgnoreCase));

            if (entry is null || Favorites.Contains(entry))
                continue;

            entry.IsPinned = true;
            Favorites.Add(entry);
        }

        WarningMessage = "";
    }

    public bool TogglePin(ToolboxEntry entry, IList<string> pinnedToolKeys)
    {
        WarningMessage = "";

        if (entry.IsPinned)
        {
            entry.IsPinned = false;
            Favorites.Remove(entry);
            RemoveMatchingKey(pinnedToolKeys, entry.ToolKey);
            return true;
        }

        if (Favorites.Count >= MaxPinnedTools)
        {
            WarningMessage = "You can pin up to 10 tools. Unpin one to add another.";
            return false;
        }

        entry.IsPinned = true;
        if (!Favorites.Contains(entry))
            Favorites.Add(entry);

        RemoveMatchingKey(pinnedToolKeys, entry.ToolKey);
        pinnedToolKeys.Add(entry.ToolKey);
        return true;
    }

    public void RecordLaunch(ToolboxEntry entry, DateTime launchedAt)
    {
        entry.LastLaunchedAt = launchedAt;
        Recent.Remove(entry);
        Recent.Insert(0, entry);

        while (Recent.Count > MaxRecentTools)
            Recent.RemoveAt(Recent.Count - 1);
    }

    private static void RemoveMatchingKey(IList<string> pinnedToolKeys, string toolKey)
    {
        for (var i = pinnedToolKeys.Count - 1; i >= 0; i--)
        {
            if (string.Equals(pinnedToolKeys[i], toolKey, StringComparison.OrdinalIgnoreCase))
                pinnedToolKeys.RemoveAt(i);
        }
    }
}
