using WorkOrderService.Domain.Enumerations;

namespace WorkOrderService.Application.Models;

/// <summary>
/// A work order without its status trail, for list results. History is a per-work-order read.
/// </summary>
/// <param name="Id">Identifier assigned by this service.</param>
/// <param name="ExternalId">The upstream system's key for this work order.</param>
/// <param name="SiteCode">The site the work is at.</param>
/// <param name="Description">What the work is.</param>
/// <param name="Status">Current lifecycle position.</param>
/// <param name="CreatedAt">When the work order was created.</param>
/// <param name="UpdatedAt">When the status last changed.</param>
public sealed record WorkOrderSummaryModel(
    Guid Id,
    string ExternalId,
    string SiteCode,
    string Description,
    WorkOrderStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
