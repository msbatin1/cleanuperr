namespace Domain.Sonarr.Queue;

public record QueueListResponse(
    int Page,
    int PageSize,
    string SortKey,
    string SortDirection,
    int TotalRecords,
    IReadOnlyList<Record> Records
);