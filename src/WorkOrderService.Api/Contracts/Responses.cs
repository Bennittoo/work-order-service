using WorkOrderService.Domain;

namespace WorkOrderService.Api.Contracts;

/// <summary>Returned by the list endpoint. Deliberately carries no history: that is a per-work-order read.</summary>
public sealed record WorkOrderSummaryResponse(
    Guid Id,
    string ExternalId,
    string SiteCode,
    string Description,
    WorkOrderStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WorkOrderDetailResponse(
    Guid Id,
    string ExternalId,
    string SiteCode,
    string Description,
    WorkOrderStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<StatusHistoryEntryResponse> StatusHistory);

public sealed record StatusHistoryEntryResponse(
    WorkOrderStatus? FromStatus,
    WorkOrderStatus ToStatus,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    StatusChangeSource Source,
    string? Details,
    Guid? EventId);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
