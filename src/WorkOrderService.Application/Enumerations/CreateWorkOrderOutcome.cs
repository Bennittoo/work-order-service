namespace WorkOrderService.Application.Enumerations;

/// <summary>What happened when a work order creation was attempted.</summary>
public enum CreateWorkOrderOutcome
{
    /// <summary>The work order was created.</summary>
    Created = 1,

    /// <summary>A work order already exists with the supplied external identifier.</summary>
    DuplicateExternalId = 2
}
