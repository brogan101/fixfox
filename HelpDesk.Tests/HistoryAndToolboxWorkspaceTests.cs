using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using Xunit;

namespace HelpDesk.Tests;

public sealed class HistoryAndToolboxWorkspaceTests
{
    [Fact]
    public void HistoryPagingService_Loads_Receipts_In_Fifty_Item_Pages()
    {
        var service = new HistoryPagingService();
        var receipts = Enumerable.Range(1, 500)
            .Select(index => new RepairHistoryEntry
            {
                Id = $"receipt-{index}",
                FixTitle = $"Fix {index}",
                CategoryName = "Network",
                Timestamp = DateTime.Now.AddMinutes(-index)
            })
            .ToList();

        var initial = service.BuildInitialPage(receipts);
        var next = service.BuildNextPage(receipts, initial.Count);

        Assert.InRange(initial.Count, 1, 50);
        Assert.InRange(next.Count, 1, 50);
        Assert.Equal(50, initial.Count);
        Assert.Equal(50, next.Count);
    }

    [Fact]
    public void ToolboxWorkspaceState_Pinning_Adds_And_Removes_Favorites()
    {
        var state = new ToolboxWorkspaceState();
        var entry = new ToolboxEntry { Title = "Task Manager", ToolKey = "task-manager" };
        var pinned = new List<string>();

        state.RegisterEntries([entry]);

        Assert.True(state.TogglePin(entry, pinned));
        Assert.Contains(entry, state.Favorites);
        Assert.Contains("task-manager", pinned);

        Assert.True(state.TogglePin(entry, pinned));
        Assert.DoesNotContain(entry, state.Favorites);
        Assert.Empty(pinned);
    }

    [Fact]
    public void ToolboxWorkspaceState_Rejects_Eleventh_Pin_And_Sets_Warning()
    {
        var state = new ToolboxWorkspaceState();
        var entries = Enumerable.Range(1, 11)
            .Select(index => new ToolboxEntry { Title = $"Tool {index}", ToolKey = $"tool-{index}" })
            .ToList();
        var pinned = new List<string>();

        state.RegisterEntries(entries);
        foreach (var entry in entries.Take(10))
            Assert.True(state.TogglePin(entry, pinned));

        var added = state.TogglePin(entries[10], pinned);

        Assert.False(added);
        Assert.Equal(10, state.Favorites.Count);
        Assert.DoesNotContain(entries[10], state.Favorites);
        Assert.Equal("You can pin up to 10 tools. Unpin one to add another.", state.WarningMessage);
    }

    [Fact]
    public void ToolboxWorkspaceState_Recent_Tools_Keep_Last_Five_In_Reverse_Order()
    {
        var state = new ToolboxWorkspaceState();
        var entries = Enumerable.Range(1, 6)
            .Select(index => new ToolboxEntry { Title = $"Tool {index}", ToolKey = $"tool-{index}" })
            .ToList();

        state.RegisterEntries(entries);
        for (var index = 0; index < entries.Count; index++)
            state.RecordLaunch(entries[index], new DateTime(2026, 3, 30, 12, 0, 0).AddMinutes(index));

        Assert.Equal(5, state.Recent.Count);
        Assert.Equal("tool-6", state.Recent[0].ToolKey);
        Assert.Equal("tool-2", state.Recent[^1].ToolKey);
    }

    [Fact]
    public void ToolboxWorkspaceState_Recent_Tools_Are_Session_Only()
    {
        var firstSession = new ToolboxWorkspaceState();
        var entry = new ToolboxEntry { Title = "Registry Editor", ToolKey = "registry-editor" };
        firstSession.RegisterEntries([entry]);
        firstSession.RecordLaunch(entry, DateTime.Now);

        var secondSession = new ToolboxWorkspaceState();
        secondSession.RegisterEntries([new ToolboxEntry { Title = "Registry Editor", ToolKey = "registry-editor" }]);

        Assert.Single(firstSession.Recent);
        Assert.Empty(secondSession.Recent);
    }
}
