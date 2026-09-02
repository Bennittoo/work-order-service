namespace WorkOrderService.Application.Models;

/// <summary>One page of results.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="Page">The one-based page number returned.</param>
/// <param name="PageSize">How many items a full page holds. Fixed by the service.</param>
/// <param name="TotalCount">How many items match in total, across all pages.</param>
/// <param name="TotalPages">How many pages the total spans.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
