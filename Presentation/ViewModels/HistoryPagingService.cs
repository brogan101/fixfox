using HelpDesk.Domain.Models;

namespace HelpDesk.Presentation.ViewModels;

public sealed class HistoryPagingService
{
    public const int DefaultPageSize = 50;

    public HistoryPagingService(int pageSize = DefaultPageSize)
    {
        PageSize = pageSize > 0 ? pageSize : DefaultPageSize;
    }

    public int PageSize { get; }

    public IReadOnlyList<RepairHistoryEntry> BuildInitialPage(IReadOnlyList<RepairHistoryEntry> source)
        => source.Take(PageSize).ToList();

    public IReadOnlyList<RepairHistoryEntry> BuildNextPage(IReadOnlyList<RepairHistoryEntry> source, int loadedCount)
    {
        if (loadedCount < 0)
            loadedCount = 0;

        if (loadedCount >= source.Count)
            return [];

        return source.Skip(loadedCount).Take(PageSize).ToList();
    }
}
